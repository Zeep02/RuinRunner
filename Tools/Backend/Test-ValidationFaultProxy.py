"""Real HTTP self-check for the dedicated fault proxy; synthetic fresh guests only."""
import base64
import concurrent.futures
import datetime as dt
import http.client
import json
import os
from pathlib import Path
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[2]
checks = []


def uid():
    return str(uuid.uuid4())


def check(name, condition):
    assert condition, name
    checks.append(name)
    print("PASS", name, flush=True)


def credentials():
    return {"installationId": uid(), "guestSecret": base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip("=")}


def call(method, path, body=None, token=None, port=5200, expected=200, dropped=False):
    connection = http.client.HTTPConnection("127.0.0.1", port, timeout=20)
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    try:
        connection.request(method, path, None if body is None else json.dumps(body).encode(), headers)
        try:
            response = connection.getresponse()
        except (http.client.RemoteDisconnected, ConnectionResetError):
            assert dropped, "Unexpected response loss"
            return None
        payload = json.loads(response.read())
        assert not dropped, "Expected post-commit response loss did not happen"
        assert response.status == expected, (method, path, response.status, expected, payload.get("code"))
        return payload
    finally:
        connection.close()


def main():
    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%d-%H%M%S")
    destination = ROOT / "Backend/Validation" / ("fault-proxy-selftest-" + stamp + ".json")
    log = destination.with_suffix(".launch.log")
    marker = ROOT / "Backend/.local/fault-proxy-5200/process.json"
    powershell = Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe"
    launcher = ROOT / "Tools/Backend/Start-ValidationFaultProxy.ps1"
    report = {"status": "RUNNING", "normalGameplay": False, "realHttp": True,
              "apiPort": 5197, "proxyPort": 5200, "checks": checks, "proxyEvidence": []}
    owned = False
    tokens = []
    guest_secrets = []

    def launch(enabled):
        nonlocal owned
        args = [str(powershell), "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(launcher),
                "-NewState", "-Name", "http-selftest-" + ("enabled" if enabled else "disabled")]
        if enabled:
            args.append("-EnableFaults")
        with log.open("a", encoding="utf-8") as output:
            result = subprocess.run(args, stdout=output, stderr=output, timeout=30)
        assert result.returncode == 0, "Dedicated proxy launcher failed (existing session is never stopped)"
        owned = True
        saved = json.loads(marker.read_text(encoding="utf-8-sig"))
        report["proxyEvidence"].append(saved["evidence"])

    def stop():
        nonlocal owned
        with log.open("a", encoding="utf-8") as output:
            result = subprocess.run([str(powershell), "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(launcher), "-Stop"],
                                    stdout=output, stderr=output, timeout=30)
        assert result.returncode == 0
        owned = False

    def sign_in(identity, port=5200, expected=200):
        value = call("POST", "/v1/auth/guest", identity, port=port, expected=expected)
        guest_secrets.append(identity["guestSecret"])
        if "accessToken" in value:
            tokens.append(value["accessToken"])
        return value

    def new_run(token):
        catalog = call("GET", "/v1/catalogs/current", token=token)
        profile = call("POST", "/v1/profiles", {"operationId": uid(), "slot": 1, "name": "Proxy synthetic check"}, token)
        run = call("POST", "/v1/runs", {"operationId": uid(), "profileId": profile["profileId"],
                   "expectedProfileRevision": profile["revision"], "requestedSeed": 20261009,
                   "contentVersion": catalog["version"], "generatorVersion": "modules-v1.0.0", "buildId": "proxy-http-selftest"}, token)
        return catalog, profile, run

    def settlement(run):
        return {"operationId": uid(), "expectedRunRevision": run["revision"], "outcome": "abandoned",
                "enemiesDefeated": 0, "roomsReached": 0, "stagesCleared": 0}

    try:
        launch(False)
        disabled_token = sign_in(credentials())["accessToken"]
        _, _, disabled_run = new_run(disabled_token)
        call("POST", "/v1/runs/" + disabled_run["runId"] + "/settle", settlement(disabled_run), disabled_token)
        state = call("GET", "/__faults/status")
        check("faults remain off unless explicitly enabled", not state["faultsEnabled"] and state["dropped"] == {"buy": None, "settle": None})
        stop()

        # This fixture is a deliberately pre-existing account. It cannot enroll
        # in a new proxy process, so a mistaken real-profile launch fails safely.
        existing_identity = credentials()
        existing_token = sign_in(existing_identity, port=5197)["accessToken"]
        call("POST", "/v1/profiles", {"operationId": uid(), "slot": 1, "name": "Must not enroll"}, existing_token, port=5197)
        launch(True)
        check("fresh state arms only buy and settle", call("GET", "/__faults/status")["dropped"] == {"buy": None, "settle": None})
        check("pre-existing profile guest is refused", sign_in(existing_identity, expected=403)["code"] == "proxy_fresh_guest_required")
        check("direct upstream token cannot bypass enrollment", call("GET", "/v1/profiles", token=existing_token, expected=403)["code"] == "proxy_unenrolled_guest")
        identity = credentials()
        auth = sign_in(identity)
        token = auth["accessToken"]
        catalog, profile, run = new_run(token)
        check("dedicated new expedition pins current v2", catalog["version"] == "campaign-v2" and run["contentVersion"] == "campaign-v2")
        check("another new guest cannot join this proxy session", sign_in(credentials(), expected=403)["code"] == "proxy_fresh_guest_required")
        refreshed = sign_in(identity)
        check("enrolled guest can renew authentication after profile creation", refreshed["playerId"] == auth["playerId"])
        token = refreshed["accessToken"]
        path = "/v1/runs/" + run["runId"]
        economy = call("GET", path + "/economy", token=token)
        offer = next(o for o in economy["shops"][0]["offers"] if o["kind"] != "heal")
        unsuccessful = {"operationId": uid(), "expectedRevision": economy["revision"], "action": "buy",
                        "shopId": "stage-0", "offerId": offer["offerId"]}
        failed = call("POST", path + "/trades", unsuccessful, token, expected=409)
        check("failed purchase response passes through without consuming fault", failed["code"] == "insufficient_gold" and call("GET", "/__faults/status")["dropped"]["buy"] is None)
        while economy["gold"] < offer["price"] + catalog["economy"]["healPrice"]:
            reward = call("POST", path + "/rewards", {"operationId": uid(), "eventId": uid(), "source": "combat", "stageIndex": 0, "roomId": "proxy-selftest"}, token)
            economy = reward["economy"]
        before = economy
        purchase = {**unsuccessful, "operationId": uid(), "expectedRevision": economy["revision"]}
        call("POST", path + "/trades", purchase, token, dropped=True)
        committed = call("GET", path + "/economy", token=token, port=5197)
        check("first purchase commits upstream before its response is lost", committed["gold"] == before["gold"] - offer["price"] and len(committed["items"]) == len(before["items"]) + 1 and committed["revision"] == before["revision"] + 1)
        receipt = call("POST", path + "/trades", purchase, token)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            replayed = list(pool.map(lambda _: call("POST", path + "/trades", purchase, token), range(2)))
        check("all purchase retries pass through with one exact receipt", receipt["economy"] == committed and all(x == receipt for x in replayed))
        check("purchase retries never duplicate server inventory or charge", call("GET", path + "/economy", token=token) == committed)
        heal = {"operationId": uid(), "expectedRevision": committed["revision"], "action": "buy", "shopId": "stage-0", "offerId": "heal"}
        check("later distinct buy is not faulted", call("POST", path + "/trades", heal, token)["healAmount"] == catalog["economy"]["healAmount"])
        settle = settlement(run)
        call("POST", path + "/settle", settle, token, dropped=True)
        settled_profile = call("GET", "/v1/profiles/" + profile["profileId"], token=token, port=5197)
        check("first settlement commits before response loss", settled_profile["state"]["totalRuns"] == 1)
        settled = call("POST", path + "/settle", settle, token)
        check("settlement retry returns committed state exactly once", settled["profile"] == settled_profile and call("POST", path + "/settle", settle, token) == settled)
        state = call("GET", "/__faults/status")
        buy_record = next(x for x in state["operations"] if x["operationId"] == purchase["operationId"])
        settle_record = next(x for x in state["operations"] if x["operationId"] == settle["operationId"])
        check("secret-free evidence counts exact drops and request retries", buy_record["requests"] == 4 and buy_record["droppedResponses"] == 1 and buy_record["upstreamStatuses"] == [200] * 4 and settle_record["requests"] == 3 and settle_record["droppedResponses"] == 1 and settle_record["upstreamStatuses"] == [200] * 3)
        stop()
        evidence_text = "\n".join(p.read_text(encoding="utf-8-sig") for name in report["proxyEvidence"] for p in Path(name).parent.iterdir() if p.is_file())
        check("evidence and logs contain no guest secrets tokens or authorization headers", not any(secret in evidence_text for secret in tokens + guest_secrets) and 'authorization' not in evidence_text.lower() and 'guestsecret' not in evidence_text.lower())
        report["status"] = "PASS"
    except Exception as error:
        report["status"] = "FAIL"
        report["failure"] = str(error)
        raise
    finally:
        if owned:
            stop()
        report["finishedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
        destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("Evidence:", destination, flush=True)


if __name__ == "__main__":
    main()
