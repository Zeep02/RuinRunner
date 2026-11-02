"""Exercise a portable candidate with a new, explicitly isolated PostgreSQL cluster.

Never launches Unity or touches the default player data root. PowerShell 5.1 is used
because it is what the shipped .cmd shortcuts run on a clean Windows installation.
"""
import argparse
import base64
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import socket
import subprocess
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]


def main(package, api_port, database_port):
    package = Path(package).resolve(strict=True)
    manifest = json.loads((package / "manifest.json").read_text(encoding="utf-8-sig"))
    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%d-%H%M%S")
    isolated_parent = (ROOT / "Backend/.local").resolve()
    data = isolated_parent / ("release-check-" + stamp + " with spaces")
    assert not data.exists() and data.is_relative_to(isolated_parent)
    evidence = ROOT / "Backend/Validation" / ("local-release-" + stamp + ".json")
    launcher_log = evidence.with_suffix(".launch.log")
    powershell = Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe"
    command = [str(powershell), "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
               str(package / "Start-LocalBackend.ps1"), "-DataRoot", str(data),
               "-ApiPort", str(api_port), "-DatabasePort", str(database_port)]
    results = []
    report = {"package": str(package), "contentVersion": manifest["contentVersion"],
              "dataRoot": str(data), "apiPort": api_port, "databasePort": database_port,
              "normalGameplay": False, "unityLaunched": False, "checks": results,
              "powershell": "Windows PowerShell 5.1", "status": "RUNNING"}

    def check(name, condition):
        assert condition, name
        results.append({"name": name, "status": "PASS"})
        print("PASS", name, flush=True)

    def launch(stop=False, expected=0):
        # Daemons inherit their parent's handles. A real file prevents a pipe-based
        # subprocess wait from hanging after PowerShell has successfully returned.
        with launcher_log.open("a", encoding="utf-8") as log:
            code = subprocess.run(command + (["-Stop"] if stop else []), stdout=log,
                                  stderr=log, timeout=60).returncode
        assert (code == 0) if expected == 0 else (code != 0), "Launcher exit status"

    def call(method, path, body=None, token=None, expected=200):
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = "Bearer " + token
        request = urllib.request.Request(f"http://127.0.0.1:{api_port}" + path,
                                         None if body is None else json.dumps(body).encode(),
                                         headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                code, value = response.status, json.load(response)
        except urllib.error.HTTPError as response:
            code, value = response.code, json.load(response)
        assert code == expected, (path, code, expected, value.get("code"))
        return value

    def verify_manifest():
        for item in manifest["files"]:
            path = (package / item["path"]).resolve(strict=True)
            assert path.is_relative_to(package)
            assert path.stat().st_size == item["bytes"]
            assert hashlib.file_digest(path.open("rb"), "sha256").hexdigest().upper() == item["sha256"]
        declared = {str((package / item["path"]).resolve()) for item in manifest["files"]}
        actual = {str(p.resolve()) for p in package.rglob("*") if p.is_file() and p.name != "manifest.json"}
        assert actual == declared, "Unlisted package files"

    started = False
    try:
        verify_manifest()
        check("all packaged file sizes and SHA-256 values match manifest", True)
        names = {p.name.lower() for p in package.rglob("*") if p.is_file()}
        check("package contains no cluster credentials or protected player data",
              not (names & {"secrets.json", "pgpass", "pg_version", "postmaster.pid"})
              and not any(p.suffix.lower() in (".dpapi", ".dump") for p in package.rglob("*")))
        check("runtime is bundled without SDK or package cache",
              (package / "Server/dotnet/dotnet.exe").is_file()
              and not (package / "Server/dotnet/sdk").exists()
              and (package / "Server/postgresql/bin/initdb.exe").is_file())
        with socket.socket() as occupied:
            occupied.bind(("127.0.0.1", api_port))
            occupied.listen()
            launch(expected=1)
            check("occupied API port is rejected before any data is created", not data.exists())
        with socket.socket() as available:
            available.bind(("127.0.0.1", database_port))
        launch()
        started = True
        check("cold start creates UTF-8 database and readies API",
              (data / "postgres/PG_VERSION").is_file() and call("GET", "/health/ready")["status"] == "ready")
        first_pid = json.loads((data / "private/process.json").read_text(encoding="utf-8-sig"))["pid"]
        launch()
        check("second launch reuses its own healthy process",
              first_pid == json.loads((data / "private/process.json").read_text(encoding="utf-8-sig"))["pid"])
        credentials = {"installationId": str(uuid.uuid4()),
                       "guestSecret": base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip("=")}
        auth = call("POST", "/v1/auth/guest", credentials)
        token = auth["accessToken"]
        catalog = call("GET", "/v1/catalogs/current", token=token)
        old_catalog = call("GET", "/v1/catalogs/campaign-v1", token=token)
        check("explicit v2 current and immutable v1 remain available",
              catalog["version"] == "campaign-v2" and old_catalog["version"] == "campaign-v1")
        profile = call("POST", "/v1/profiles", {"operationId": str(uuid.uuid4()), "slot": 1,
                       "name": "패키지 저장 검사"}, token)
        profile_path = "/v1/profiles/" + profile["profileId"]
        foreign = call("POST", "/v1/auth/guest", {"installationId": str(uuid.uuid4()),
                       "guestSecret": base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip("=")})
        check("different new guest cannot read the test profile",
              call("GET", profile_path, token=foreign["accessToken"], expected=404)["code"] == "profile_missing")
        run = call("POST", "/v1/runs", {"operationId": str(uuid.uuid4()), "profileId": profile["profileId"],
                   "expectedProfileRevision": profile["revision"], "requestedSeed": 20261009,
                   "contentVersion": catalog["version"], "generatorVersion": "modules-v1.0.0",
                   "buildId": "portable-release-smoke"}, token)
        run_path = "/v1/runs/" + run["runId"]
        economy = call("GET", run_path + "/economy", token=token)
        launch(stop=True)
        started = False
        check("clean stop removes the owned database PID file", not (data / "postgres/postmaster.pid").exists())
        launch()
        started = True
        second_auth = call("POST", "/v1/auth/guest", credentials)
        token = second_auth["accessToken"]
        check("same guest and exact Korean profile survive restart",
              second_auth["playerId"] == auth["playerId"] and call("GET", profile_path, token=token) == profile)
        check("active run keeps seed loadout and catalog after restart", call("GET", run_path, token=token) == run)
        check("run economy survives restart unchanged", call("GET", run_path + "/economy", token=token) == economy)
        settlement = {"operationId": str(uuid.uuid4()), "expectedRunRevision": run["revision"],
                      "outcome": "abandoned", "enemiesDefeated": 0, "roomsReached": 0, "stagesCleared": 0}
        receipt = call("POST", run_path + "/settle", settlement, token)
        launch(stop=True)
        started = False
        launch()
        started = True
        check("settlement replay after server restart returns identical committed receipt",
              call("POST", run_path + "/settle", settlement, token) == receipt)
        final_profile = call("GET", profile_path, token=token)
        check("settlement profile increment is exactly once",
              final_profile == receipt["profile"] and final_profile["state"]["totalRuns"] == 1
              and final_profile["legacyShards"] == profile["legacyShards"])
        check("runtime data and private credentials are outside the distributable",
              (data / "private/secrets.json").is_file() and not data.is_relative_to(package))
        verify_manifest()
        check("cold start and restarts do not mutate any packaged file", True)
        report["status"] = "PASS"
    except Exception as error:
        report["status"] = "FAIL"
        report["error"] = str(error)
        raise
    finally:
        if started:
            launch(stop=True)
        report["finishedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
        evidence.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("Evidence:", evidence, flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", required=True)
    parser.add_argument("--api-port", type=int, default=5198)
    parser.add_argument("--database-port", type=int, default=55441)
    args = parser.parse_args()
    main(args.package, args.api_port, args.database_port)
