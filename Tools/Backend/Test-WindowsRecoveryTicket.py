"""Offline ticket guard checks using synthetic CurrentUser DPAPI records.

No HTTP, game launch, termination, user profile, or production database access.
The process-exit probe alone is stubbed; this is not restart/gameplay evidence.
"""
import copy
import ctypes
from ctypes import wintypes
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("recovery_ticket", Path(__file__).with_name("Prepare-WindowsRecovery.py"))
ticket = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ticket)


def protect(data):
    crypt = ctypes.WinDLL("crypt32", use_last_error=True)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    blob = ticket.journal.Blob
    crypt.CryptProtectData.argtypes = [ctypes.POINTER(blob), ctypes.c_wchar_p,
                                      ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p,
                                      wintypes.DWORD, ctypes.POINTER(blob)]
    crypt.CryptProtectData.restype = wintypes.BOOL
    kernel.LocalFree.argtypes = [ctypes.c_void_p]
    buffer = ctypes.create_string_buffer(data)
    source, result = blob(len(data), ctypes.cast(buffer, ctypes.c_void_p)), blob()
    if not crypt.CryptProtectData(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(result)):
        raise RuntimeError("Synthetic DPAPI fixture protection failed.")
    try:
        return ctypes.string_at(result.data, result.size)
    finally:
        kernel.LocalFree(result.data)


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding="utf-8")


def main():
    evidence = ROOT / "Logs" / "Validation" / "2026-10-09-goal" / ("recovery-ticket-preflight-" + dt.datetime.now().strftime("%H%M%S") + "-" + uuid.uuid4().hex[:6])
    evidence.mkdir(parents=True)
    ticket.ROOT = evidence / "synthetic-workspace"
    ticket.VALIDATION = ticket.ROOT / "Logs" / "Validation"
    ticket.PROXY = ticket.ROOT / "Backend" / "Validation" / "FaultProxy"
    ticket.process_running = lambda pid: False
    profile, run_id, operation, profile_id, player_id = "synthetic-only", *(str(uuid.uuid4()) for _ in range(4))
    exe = ticket.ROOT / "Builds" / "synthetic" / "RuinRunner.exe"
    runtime = exe.parent / "RuinRunner_Data" / "Managed" / "RuinRunner.Runtime.dll"
    runtime.parent.mkdir(parents=True)
    exe.write_bytes(b"This file is only a hash fixture, not a runnable game.")
    runtime.write_bytes(b"Synthetic runtime hash fixture.")
    output = ticket.VALIDATION / "source"
    launch_path, proxy_path = output / "launch.json", ticket.PROXY / "synthetic" / "state.json"
    launch = {"api": ticket.ORIGIN, "freshValidationProfile": True, "stopAfterResponseDrop": "buy",
              "args": ["--automated-stage-validation", "--validation-profile", "--validation-profile-name", profile],
              "exe": str(exe), "executableSha256": ticket.digest(exe), "runtimeSha256": ticket.digest(runtime),
              "pid": 12345, "processStartUtc": "synthetic-start", "faultStatePath": str(proxy_path)}
    stop = {"stoppedByLauncher": True, "pid": 12345, "sourceProcessStartUtc": "synthetic-start",
            "executableSha256": ticket.digest(exe), "operationId": operation}
    fault = {"listen": ticket.ORIGIN, "upstream": "http://127.0.0.1:5197/", "faultsEnabled": True,
             "startedUtc": "2026-10-09T08:49:34.633076Z", "dropped": {"buy": operation, "settle": None},
             "operations": [{"operationId": operation, "action": "buy", "requests": 1,
                             "droppedResponses": 1, "upstreamStatuses": [200]}]}
    command = {"apiOrigin": ticket.ORIGIN, "operationId": operation, "status": "pending", "method": "POST",
               "playerId": player_id, "path": "v1/runs/" + run_id + "/trades",
               "payload": json.dumps({"operationId": operation, "action": "buy"})}
    run = {"runId": run_id, "profileId": profile_id, "status": "active"}
    cached = {"profileId": profile_id, "playerId": player_id, "slot": 1, "legacyShards": 0,
              "state": {"totalRuns": 0, "legacyMigrationVersion": 0, "campaignVictories": 0}}
    queue = {"operationId": operation, "runId": run_id, "apiOrigin": ticket.ORIGIN, "status": "pending"}
    paths = {}

    def store(category, key, value):
        path = ticket.VALIDATION / profile / "Saves" / "BackendRecovery" / category / (hashlib.sha256(key.encode()).hexdigest() + ".dpapi")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(protect(json.dumps(value).encode()))
        paths[category] = path

    write_json(launch_path, launch)
    write_json(output / "crash-stop.json", stop)
    write_json(output / "fault-at-stop.json", fault)
    write_json(proxy_path, fault)
    store("operations", operation, command)
    store("runs", ticket.ORIGIN + ":" + run_id, run)
    store("profiles", ticket.ORIGIN + ":" + profile_id, cached)
    store("economy-queue", ticket.ORIGIN + ":" + operation, queue)
    generated = ticket.source_evidence(launch_path, "buy", proxy_path)
    ticket_path = output / "crash-ticket.json"
    write_json(ticket_path, generated)
    checks = []

    def check(name, action, expected_rejection=False):
        try:
            action()
        except (ValueError, KeyError, OSError, RuntimeError, StopIteration):
            if not expected_rejection:
                raise
        else:
            if expected_rejection:
                raise AssertionError("Guard accepted invalid synthetic evidence: " + name)
        checks.append({"name": name, "passed": True})

    check("synthetic buy pending ticket accepted with matching executable/profile/origin", lambda: ticket.validate_ticket(ticket_path, exe, profile, ticket.ORIGIN))
    check("different executable rejected", lambda: ticket.validate_ticket(ticket_path, exe.parent / "wrong.exe"), True)
    check("different profile rejected", lambda: ticket.validate_ticket(ticket_path, profile="another"), True)
    check("ordinary backend origin rejected", lambda: ticket.validate_ticket(ticket_path, api_url="http://127.0.0.1:5186/"), True)
    check("outside validation ticket rejected", lambda: ticket.validate_ticket(exe), True)
    ticket.process_running = lambda pid: True
    check("source still running rejected", lambda: ticket.validate_ticket(ticket_path), True)
    ticket.process_running = lambda pid: False

    def changed_json(name, path, mutate):
        original = path.read_bytes()
        changed = json.loads(original)
        mutate(changed)
        write_json(path, changed)
        try:
            check(name, lambda: ticket.validate_ticket(ticket_path), True)
        finally:
            path.write_bytes(original)

    changed_json("tampered ticket numeric baseline rejected", ticket_path, lambda x: x.update(beforeLegacy=999))
    changed_json("different proxy session rejected", proxy_path, lambda x: x.update(startedUtc="other-session"))
    changed_json("missing dropped response rejected", output / "fault-at-stop.json", lambda x: x["operations"][0].update(droppedResponses=0))
    changed_json("unconfirmed upstream rejected", output / "fault-at-stop.json", lambda x: x["operations"][0].update(upstreamStatuses=[500]))
    changed_json("already retried fault snapshot rejected", output / "fault-at-stop.json", lambda x: x["operations"][0].update(requests=2, upstreamStatuses=[200, 200]))
    changed_json("wrong owned PID evidence rejected", output / "crash-stop.json", lambda x: x.update(pid=54321))
    changed_json("nonfresh original launch rejected", launch_path, lambda x: x.update(freshValidationProfile=False))
    original_runtime = runtime.read_bytes()
    runtime.write_bytes(b"changed")
    check("candidate runtime changed rejected", lambda: ticket.validate_ticket(ticket_path), True)
    runtime.write_bytes(original_runtime)
    original_command = paths["operations"].read_bytes()
    changed = copy.deepcopy(command)
    changed["status"] = "confirmed"
    store("operations", operation, changed)
    check("already confirmed command rejected", lambda: ticket.validate_ticket(ticket_path), True)
    paths["operations"].write_bytes(original_command)
    original_profile = paths["profiles"].read_bytes()
    changed = copy.deepcopy(cached)
    changed["state"]["totalRuns"] = 1
    store("profiles", ticket.ORIGIN + ":" + profile_id, changed)
    check("prior completed-run profile rejected", lambda: ticket.validate_ticket(ticket_path), True)
    paths["profiles"].write_bytes(original_profile)
    original_queue = paths["economy-queue"].read_bytes()
    changed = copy.deepcopy(queue)
    changed["runId"] = str(uuid.uuid4())
    store("economy-queue", ticket.ORIGIN + ":" + operation, changed)
    check("queue owned by different run rejected", lambda: ticket.validate_ticket(ticket_path), True)
    paths["economy-queue"].write_bytes(original_queue)

    # A separate victory fixture proves that abandoned/partial settlement is not
    # silently labelled a completed-campaign settlement restart.
    launch["stopAfterResponseDrop"] = "settle"
    stop["action"] = "settle"
    fault["dropped"] = {"buy": None, "settle": operation}
    fault["operations"][0]["action"] = "settle"
    command["path"] = "v1/runs/" + run_id + "/settle"
    command["payload"] = json.dumps({"operationId": operation, "outcome": "victory", "stagesCleared": 3})
    write_json(launch_path, launch)
    write_json(output / "crash-stop.json", stop)
    write_json(output / "fault-at-stop.json", fault)
    write_json(proxy_path, fault)
    store("operations", operation, command)
    generated = ticket.source_evidence(launch_path, "settle", proxy_path)
    write_json(ticket_path, generated)
    check("synthetic three-stage victory settlement ticket accepted", lambda: ticket.validate_ticket(ticket_path))
    command["payload"] = json.dumps({"operationId": operation, "outcome": "abandoned", "stagesCleared": 0})
    store("operations", operation, command)
    check("abandoned settlement cannot be victory restart fixture", lambda: ticket.source_evidence(launch_path, "settle", proxy_path), True)
    # Exercise the real evidence path after PowerShell-style timezone conversion.
    changed = copy.deepcopy(fault)
    changed["startedUtc"] = "2026-10-09T17:49:34.633076+09:00"
    write_json(output / "fault-at-stop.json", changed)
    command["payload"] = json.dumps({"operationId": operation, "outcome": "victory", "stagesCleared": 3})
    store("operations", operation, command)
    check("equivalent UTC and local-offset proxy timestamps accepted", lambda: ticket.source_evidence(launch_path, "settle", proxy_path))
    changed["startedUtc"] = "2026-10-09T17:49:35.633076+09:00"
    write_json(output / "fault-at-stop.json", changed)
    check("different proxy start instant still rejected", lambda: ticket.source_evidence(launch_path, "settle", proxy_path), True)
    report = {"passed": True, "normalGameplay": False, "actualRestartTest": False,
              "scope": "offline synthetic DPAPI ticket guards; process exit probe stubbed; no HTTP or game process actions",
              "checks": checks}
    write_json(evidence / "result.json", report)
    print(json.dumps({"report": str(evidence / "result.json"), "passed": True, "checks": len(checks), "actualRestartTest": False}))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
