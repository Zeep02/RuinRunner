"""Two-step profile reset checks. Creates its own accounts on a dedicated isolated cluster only."""
import argparse
import base64
import concurrent.futures
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
RESULTS = []


def uid():
    return str(uuid.uuid4())


def check(name, condition=True):
    assert condition, name
    RESULTS.append({"name": name, "status": "PASS"})
    print("PASS", name)


def main(instance):
    assert instance and all(c.isalnum() or c == "-" for c in instance)
    private_root = ROOT / "Backend/.local/isolated" / instance
    marker = json.loads((private_root / "instance.json").read_text(encoding="utf-8-sig"))
    assert Path(marker["dll"]).resolve().is_relative_to(private_root.resolve())
    base = f"http://127.0.0.1:{int(marker['apiPort'])}"

    def call(method, path, body=None, token=None, status=200):
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = "Bearer " + token
        request = urllib.request.Request(base + path, None if body is None else json.dumps(body, ensure_ascii=False).encode("utf-8"), headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                actual, data = response.status, json.load(response)
        except urllib.error.HTTPError as response:
            actual, data = response.code, json.load(response)
        assert actual == status, (method, path, actual, status, data.get("code"))
        return data

    def sql(statement):
        env = os.environ.copy()
        env["PGPASSFILE"] = str(private_root / "private/pgpass")
        result = subprocess.run([str(ROOT / "Backend/.local/postgresql/pgsql/bin/psql.exe"), "-X", "-h", "127.0.0.1", "-p", str(marker["databasePort"]), "-U", "postgres", "-d", "ruinrunner_isolated", "-Atc", statement], env=env, capture_output=True, text=True, encoding="utf-8")
        assert result.returncode == 0, "Isolated test SQL failed"
        return result.stdout.strip()

    def guest():
        return call("POST", "/v1/auth/guest", {"installationId": uid(), "guestSecret": base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip("=")})["accessToken"]

    owner, foreign = guest(), guest()
    catalog = call("GET", "/v1/catalogs/current", token=owner)
    p = call("POST", "/v1/profiles", {"operationId": uid(), "slot": 1, "name": "격리 초기화 검사"}, owner)
    other = call("POST", "/v1/profiles", {"operationId": uid(), "slot": 2, "name": "보존 대조 슬롯"}, owner)
    path = "/v1/profiles/" + p["profileId"]
    original = json.dumps(dict(version=1, slotNumber=1, totalRuns=5, victories=2, campaignVictories=1, bestCampaignStage=3, deepestRoom=13, totalKills=172, legacyShards=88, upgrades=[dict(type=0, rank=2)]), ensure_ascii=False, separators=(",", ":"))
    imported = call("POST", path + "/legacy-import", dict(operationId=uid(), expectedRevision=p["revision"], migrationVersion=1, sourceSha256=hashlib.sha256(original.encode()).hexdigest(), sourceJson=original), owner)
    p = imported["profile"]

    def begin(expected=None, token=owner, status=200):
        return call("POST", path + "/reset-challenge", dict(operationId=uid(), expectedRevision=p["revision"] if expected is None else expected), token, status)

    def confirm(challenge, operation=None, expected=None, token=owner, status=200):
        return call("POST", path + "/reset", dict(operationId=operation or uid(), expectedRevision=p["revision"] if expected is None else expected, challengeId=challenge["challengeId"]), token, status)

    check("direct reset without challenge is denied", confirm({"challengeId": uid()}, status=409)["code"] == "reset_challenge_missing")
    check("foreign owner cannot prepare reset", begin(token=foreign, status=404)["code"] == "profile_missing")
    check("stale profile revision cannot prepare reset", begin(expected=0, status=409)["code"] == "revision_conflict")
    first = begin()
    check("preparation does not change progress or balance", call("GET", path, token=owner) == p)
    check("foreign owner cannot confirm reset", confirm(first, token=foreign, status=404)["code"] == "profile_missing")
    check("challenge is bound to exact profile", call("POST", "/v1/profiles/" + other["profileId"] + "/reset", dict(operationId=uid(), expectedRevision=other["revision"], challengeId=first["challengeId"]), owner, 409)["code"] == "reset_challenge_missing")
    stale = begin()
    p = call("PUT", path + "/configuration", dict(operationId=uid(), expectedRevision=p["revision"], startingWeaponId="rust_sword", activeTalentIds=[]), owner)
    check("profile change invalidates earlier confirmation", confirm(stale, status=409)["code"] == "revision_conflict")
    expiry = begin()
    sql("UPDATE profile_reset_challenges SET expires_utc=now()-interval '1 second' WHERE id='" + expiry["challengeId"] + "'")
    check("expired challenge is rejected", confirm(expiry, status=409)["code"] == "reset_challenge_expired")
    before_run = begin()
    run = call("POST", "/v1/runs", dict(operationId=uid(), profileId=p["profileId"], expectedProfileRevision=p["revision"], requestedSeed=20261009, contentVersion=catalog["version"], generatorVersion="modules-v1.0.0", buildId="isolated-reset-check"), owner)
    check("active expedition blocks reset preparation", begin(status=409)["code"] == "run_active")
    check("expedition started after challenge blocks confirmation", confirm(before_run, status=409)["code"] == "run_active")
    settled = call("POST", "/v1/runs/" + run["runId"] + "/settle", dict(operationId=uid(), expectedRunRevision=run["revision"], outcome="abandoned", enemiesDefeated=0, roomsReached=0, stagesCleared=0), owner)
    p = settled["profile"]
    old_profile = p
    challenge = begin()
    operation = uid()
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        a, b = [f.result() for f in [pool.submit(confirm, challenge, operation), pool.submit(confirm, challenge, operation)]]
    p = a["profile"]
    check("concurrent same-operation confirmations return identical receipt", a == b)
    check("reset preserves identity name and slot", all(p[k] == old_profile[k] for k in ("profileId", "playerId", "slot", "name")))
    check("reset clears counters and restores current defaults", p["legacyShards"] == catalog["startingLegacy"] and p["state"]["totalRuns"] == 0 and p["state"]["totalKills"] == 0 and p["state"]["unlockedWeaponIds"] == ["rust_sword"] and p["state"]["activeTalentIds"] == catalog["defaultUnlockedTalentIds"])
    check("reset increments revision and permanent import tombstone", p["revision"] == old_profile["revision"] + 1 and p["state"]["resetCount"] == 1 and p["state"]["legacyImportBlocked"])
    check("reset retry after lost reply is stable", confirm(challenge, operation, old_profile["revision"]) == a)
    check("used challenge cannot reset twice", confirm(challenge, status=409)["code"] in ("revision_conflict", "reset_challenge_used"))
    blocked = call("POST", path + "/legacy-import", dict(operationId=uid(), expectedRevision=p["revision"], migrationVersion=1, sourceSha256=hashlib.sha256(original.encode()).hexdigest(), sourceJson=original), owner, 409)
    check("old save cannot re-import after reset", blocked["code"] == "import_conflict")
    check("migration history and exact original snapshot are preserved", sql("SELECT source_sha256 FROM legacy_imports WHERE profile_id='" + p["profileId"] + "'") == hashlib.sha256(original.encode()).hexdigest())
    check("reset audit and ledger are exactly once", sql("SELECT count(*) FROM profile_resets WHERE profile_id='" + p["profileId"] + "'") == "1" and sql("SELECT count(*) FROM currency_ledger WHERE profile_id='" + p["profileId"] + "' AND reason='profile.reset'") == "1")
    check("historical run survives reset", call("GET", "/v1/runs/" + run["runId"], token=owner)["status"] == "abandoned")
    check("other slot is unchanged", call("GET", "/v1/profiles/" + other["profileId"], token=owner) == other)
    fresh = call("POST", "/v1/runs", dict(operationId=uid(), profileId=p["profileId"], expectedProfileRevision=p["revision"], requestedSeed=20261010, contentVersion=catalog["version"], generatorVersion="modules-v1.0.0", buildId="isolated-reset-check"), owner)
    check("reset profile can depart again", fresh["runId"] != run["runId"] and fresh["status"] == "active")
    check("stale pre-reset operation cannot replace new progress", call("PUT", path + "/configuration", dict(operationId=uid(), expectedRevision=old_profile["revision"], startingWeaponId="rust_sword", activeTalentIds=[]), owner, 409)["code"] == "revision_conflict")
    report = {"status": "PASS", "normalGameplay": False, "scope": "isolated HTTP/PostgreSQL profile reset", "instance": instance, "checks": RESULTS}
    filename = "profile-reset-" + datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S") + ".json"
    output = ROOT / "Backend/Validation" / filename
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--instance", default="goal-20261009")
    main(parser.parse_args().instance)
