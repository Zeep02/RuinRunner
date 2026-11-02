"""Create/validate a narrowly bound recovery ticket after the owned game stops.

No game launch, process termination, HTTP call, identity or token decryption.
Only validation journals and cached run/profile numeric state are read.
"""
import argparse
import ctypes
from ctypes import wintypes
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
VALIDATION = ROOT / "Logs" / "Validation"
PROXY = ROOT / "Backend" / "Validation" / "FaultProxy"
ORIGIN = "http://127.0.0.1:5200/"
PURPOSE = "isolated-windows-recovery-v1"
spec = importlib.util.spec_from_file_location("validation_journal", Path(__file__).with_name("Read-ValidationJournal.py"))
journal = importlib.util.module_from_spec(spec)
spec.loader.exec_module(journal)


class EvidenceCheckError(ValueError):
    """Only fixed, developer-authored guard labels may be reported."""


def require(condition, message):
    if not condition:
        raise EvidenceCheckError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def bounded(path, root):
    path = Path(path).resolve()
    require(path.is_relative_to(root.resolve()), "Path is outside the dedicated validation boundary.")
    return path


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def same_utc_instant(first, second):
    if not isinstance(first, str) or not isinstance(second, str):
        return False
    try:
        a = dt.datetime.fromisoformat(first.replace("Z", "+00:00"))
        b = dt.datetime.fromisoformat(second.replace("Z", "+00:00"))
        return a.tzinfo is not None and b.tzinfo is not None and a == b
    except ValueError:
        return False


def argument(args, name):
    require(args.count(name) == 1, "Required launch argument is missing or duplicated.")
    return args[args.index(name) + 1]


def process_running(pid):
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel.GetExitCodeProcess.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x1000, False, int(pid))
    if not handle:
        require(ctypes.get_last_error() == 87, "Cannot verify that the source process stopped.")
        return False
    try:
        code = wintypes.DWORD()
        require(kernel.GetExitCodeProcess(handle, ctypes.byref(code)), "Cannot read source process exit status.")
        return code.value == 259
    finally:
        kernel.CloseHandle(handle)


def read_store(profile, category, key):
    require(re.fullmatch(r"[A-Za-z0-9_-]{1,80}", profile), "Invalid validation profile name.")
    require(category in ("operations", "economy-queue", "runs", "profiles"), "Unsupported journal category.")
    root = bounded(VALIDATION / profile / "Saves" / "BackendRecovery", VALIDATION)
    path = bounded(root / category / (hashlib.sha256(key.encode("utf-8")).hexdigest() + ".dpapi"), root)
    encrypted = path.read_bytes()
    value = json.loads(journal.decrypt(encrypted).decode("utf-8"))
    return value, hashlib.sha256(encrypted).hexdigest()


def source_evidence(launch_path, action, fault_state_path):
    launch_path = bounded(launch_path, VALIDATION)
    launch = read_json(launch_path)
    require(action in ("buy", "settle"), "Unsupported response-loss scenario.")
    require(launch.get("api") == ORIGIN and launch.get("freshValidationProfile") is True, "Original launch must own a new 5200 validation profile.")
    require(launch.get("stopAfterResponseDrop") == action, "Launch did not request this bounded interruption.")
    args = launch["args"]
    require("--automated-stage-validation" in args and "--validation-profile" in args and "--validation-recovery-ticket" not in args,
            "Original launch must be the normal new-profile pilot.")
    profile = argument(args, "--validation-profile-name")
    require(re.fullmatch(r"[A-Za-z0-9_-]{1,80}", profile), "Invalid validation profile name.")
    exe = bounded(launch["exe"], ROOT / "Builds")
    require(exe.name == "RuinRunner.exe", "Expected the known candidate executable.")
    runtime = exe.parent / "RuinRunner_Data" / "Managed" / "RuinRunner.Runtime.dll"
    require(digest(exe).lower() == launch["executableSha256"].lower() and digest(runtime).lower() == launch["runtimeSha256"].lower(), "Candidate changed after the original launch.")
    stop_path = launch_path.parent / "crash-stop.json"
    stopped = read_json(stop_path)
    require(stopped.get("stoppedByLauncher") is True and stopped.get("pid") == launch["pid"]
            and stopped.get("sourceProcessStartUtc") == launch["processStartUtc"]
            and stopped.get("executableSha256", "").lower() == launch["executableSha256"].lower(), "Owned-process stop evidence does not match.")
    require(not process_running(launch["pid"]), "Source game is still running; no recovery ticket was created.")
    fault_state_path = bounded(fault_state_path, PROXY)
    require(str(fault_state_path).lower() == str(Path(launch["faultStatePath"]).resolve()).lower(), "Proxy evidence does not match original launch.")
    frozen_path = launch_path.parent / "fault-at-stop.json"
    frozen, live = read_json(frozen_path), read_json(fault_state_path)
    require(frozen.get("listen") == ORIGIN and frozen.get("upstream") == "http://127.0.0.1:5197/" and frozen.get("faultsEnabled") is True, "Dedicated response-loss fixture required.")
    operation = str(uuid.UUID(frozen["dropped"][action]))
    require(stopped.get("operationId") == operation and same_utc_instant(live.get("startedUtc"), frozen.get("startedUtc"))
            and live.get("dropped", {}).get(action) == operation, "Proxy session changed or interruption target mismatched.")
    row = next((x for x in frozen["operations"] if x["operationId"] == operation and x["action"] == action), None)
    require(row and row.get("requests") == 1 and row.get("droppedResponses") == 1 and row.get("upstreamStatuses") == [200], "First upstream committed-response loss was not captured before retry.")
    command, command_hash = read_store(profile, "operations", operation)
    require(command.get("apiOrigin") == ORIGIN and command.get("operationId") == operation and command.get("status") == "pending",
            "Command was not pending after interruption; this is not an unresolved-restart test.")
    suffix = "trades" if action == "buy" else "settle"
    route = re.fullmatch(r"v1/runs/([0-9a-f-]{36})/" + suffix, command.get("path", ""))
    require(route and command.get("method") == "POST", "Unexpected interrupted command route.")
    run_id = str(uuid.UUID(route.group(1)))
    payload = json.loads(command["payload"])
    require(payload.get("operationId") == operation, "Interrupted request UUID mismatch.")
    require(payload.get("action") == "buy" if action == "buy" else payload.get("outcome") == "victory" and payload.get("stagesCleared") == 3,
            "Expected first buy or actual three-stage victory settlement.")
    run, _ = read_store(profile, "runs", ORIGIN + ":" + run_id)
    require(run.get("runId") == run_id and run.get("status") == "active", "Cached original active run is required.")
    profile_id = str(uuid.UUID(run["profileId"]))
    cached, _ = read_store(profile, "profiles", ORIGIN + ":" + profile_id)
    require(cached.get("profileId") == profile_id and cached.get("slot") == 1 and cached.get("playerId") == command.get("playerId")
            and cached["state"].get("totalRuns") == 0 and cached["state"].get("legacyMigrationVersion") == 0,
            "Only this launch's fresh, unmigrated first-run validation profile is eligible.")
    queue_hash = None
    if action == "buy":
        queue, queue_hash = read_store(profile, "economy-queue", ORIGIN + ":" + operation)
        require(queue.get("status") == "pending" and queue.get("runId") == run_id and queue.get("apiOrigin") == ORIGIN,
                "The interrupted purchase queue must still be pending.")
    return {"schemaVersion": 1, "purpose": PURPOSE, "action": action,
            "validationProfile": profile, "apiOrigin": ORIGIN, "operationId": operation, "runId": run_id, "profileId": profile_id,
            "executablePath": str(exe), "executableSha256": launch["executableSha256"], "runtimeSha256": launch["runtimeSha256"],
            "sourceLaunchPath": str(launch_path), "sourceLaunchSha256": digest(launch_path),
            "sourceStopPath": str(stop_path), "sourceStopSha256": digest(stop_path),
            "proxyStatePath": str(fault_state_path), "faultSnapshotPath": str(frozen_path), "faultSnapshotSha256": digest(frozen_path),
            "operationJournalSha256": command_hash, "queueJournalSha256": queue_hash,
            "beforeLegacy": cached["legacyShards"], "beforeTotalRuns": cached["state"]["totalRuns"],
            "beforeCampaignVictories": cached["state"]["campaignVictories"]}


def validate_ticket(path, executable=None, profile=None, api_url=None):
    path = bounded(path, VALIDATION)
    ticket = read_json(path)
    require(ticket.get("schemaVersion") == 1 and ticket.get("purpose") == PURPOSE, "Unsupported recovery ticket.")
    current = source_evidence(ticket["sourceLaunchPath"], ticket["action"], ticket["proxyStatePath"])
    require(all(ticket.get(key) == value for key, value in current.items()), "Recovery ticket or pending evidence changed.")
    if executable is not None:
        require(Path(executable).resolve() == Path(ticket["executablePath"]).resolve(), "Recovery must use the original executable.")
    if profile is not None:
        require(profile == ticket["validationProfile"], "Recovery must use the same isolated profile.")
    if api_url is not None:
        require(api_url == ORIGIN, "Recovery must use the same 5200 endpoint.")
    return ticket


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--launch")
    parser.add_argument("--fault-state")
    parser.add_argument("--action", choices=("buy", "settle"))
    parser.add_argument("--output")
    parser.add_argument("--validate-ticket")
    parser.add_argument("--executable")
    parser.add_argument("--profile")
    parser.add_argument("--api-url")
    args = parser.parse_args()
    if args.validate_ticket:
        ticket = validate_ticket(args.validate_ticket, args.executable, args.profile, args.api_url)
        print(json.dumps({"ticketValid": True, "action": ticket["action"], "operationId": ticket["operationId"]}))
        return
    require(args.launch and args.fault_state and args.action and args.output, "Ticket preparation arguments are required.")
    ticket = source_evidence(args.launch, args.action, args.fault_state)
    output = bounded(args.output, VALIDATION)
    require(output.parent == Path(args.launch).resolve().parent and not output.exists(), "Choose a new ticket beside original launch evidence.")
    ticket["createdUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    with output.open("x", encoding="utf-8") as stream:
        json.dump(ticket, stream, indent=2)
    print(json.dumps({"ticket": str(output), "action": ticket["action"], "operationId": ticket["operationId"], "sourcePending": True}))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        main()
    except EvidenceCheckError as error:
        print("Recovery ticket rejected: " + str(error), file=sys.stderr)
        sys.exit(2)
    except (ValueError, KeyError, OSError, RuntimeError, StopIteration, IndexError, TypeError, AttributeError) as error:
        # Decrypted commands contain request/receipt bodies: never include them or
        # arbitrary exception text in a tool log.
        print("Recovery ticket rejected: evidence parsing failed (" + type(error).__name__ + ").", file=sys.stderr)
        sys.exit(2)
