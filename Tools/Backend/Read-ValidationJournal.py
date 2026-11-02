"""Read only IDs/status from one Windows validation profile's recovery journal.

Never opens identity, binding, profile, token, or legacy-backup files. Decrypted
command bodies stay in memory and are not printed or copied into the report.
"""
import argparse
from collections import Counter
import ctypes
from ctypes import wintypes
import datetime as dt
import hashlib
import json
from pathlib import Path
import re
import sys
import uuid


WORKSPACE = Path(__file__).resolve().parents[2]
VALIDATION = WORKSPACE / "Logs" / "Validation"
STATUSES = {"pending", "confirmed-unprojected", "confirmed", "rejected"}


class Blob(ctypes.Structure):
    _fields_ = [("size", wintypes.DWORD), ("data", ctypes.c_void_p)]


def decrypt(data):
    crypt = ctypes.WinDLL("crypt32", use_last_error=True)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    crypt.CryptUnprotectData.argtypes = [ctypes.POINTER(Blob), ctypes.c_void_p,
                                        ctypes.c_void_p, ctypes.c_void_p,
                                        ctypes.c_void_p, wintypes.DWORD,
                                        ctypes.POINTER(Blob)]
    crypt.CryptUnprotectData.restype = wintypes.BOOL
    kernel.LocalFree.argtypes = [ctypes.c_void_p]
    kernel.LocalFree.restype = ctypes.c_void_p
    buffer = ctypes.create_string_buffer(data)
    source, result = Blob(len(data), ctypes.cast(buffer, ctypes.c_void_p)), Blob()
    if not crypt.CryptUnprotectData(ctypes.byref(source), None, None, None,
                                   None, 1, ctypes.byref(result)):
        raise RuntimeError("Current-user journal decryption failed.")
    try:
        return ctypes.string_at(result.data, result.size)
    finally:
        kernel.LocalFree(result.data)


def identifier(value):
    try:
        return str(uuid.UUID(value))
    except (ValueError, TypeError, AttributeError):
        return None


def inspect(profile, endpoint):
    if not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", profile):
        raise ValueError("Validation profile must be a single safe name.")
    root = (VALIDATION / profile / "Saves" / "BackendRecovery").resolve()
    if not root.is_relative_to(VALIDATION.resolve()) or not root.is_dir():
        raise ValueError("Existing workspace validation recovery directory required.")
    records, ignored = [], 0
    for category in ("operations", "economy-queue"):
        directory = (root / category).resolve()
        if not directory.is_relative_to(root):
            raise ValueError("Recovery category escapes its validation profile.")
        for path in sorted(directory.glob("*.dpapi")):
            if not path.resolve().is_relative_to(directory):
                raise ValueError("Recovery record escapes its category.")
            encrypted = path.read_bytes()
            value = json.loads(decrypt(encrypted).decode("utf-8"))
            if value.get("apiOrigin") != endpoint:
                ignored += 1
                continue
            state = value.get("status")
            record = {"category": category, "operationId": identifier(value.get("operationId")),
                      "status": state if state in STATUSES else "unknown",
                      "encryptedSha256": hashlib.sha256(encrypted).hexdigest()}
            if category == "operations":
                route = str(value.get("path", ""))
                record["routeKind"] = route.rsplit("/", 1)[-1] if route.endswith(("/trades", "/rewards", "/settle")) else "other"
                record["responseStored"] = bool(value.get("responseJson"))
                record["lastStatusCode"] = value.get("lastStatusCode") if isinstance(value.get("lastStatusCode"), int) else None
            else:
                record["runId"] = identifier(value.get("runId"))
                record["prepared"] = value.get("prepared") is True
                record["receiptStored"] = isinstance(value.get("receipt"), dict)
            # A game may write a newer atomic revision while this read is in flight.
            record["sourceStableDuringRead"] = encrypted == path.read_bytes()
            records.append(record)
    return {"schema": 1, "utc": dt.datetime.now(dt.timezone.utc).isoformat(),
            "validationProfile": profile, "apiOrigin": endpoint, "readOnly": True,
            "normalGameplayProof": False, "scope": "Only operations/economy-queue IDs and statuses; no credentials or bodies",
            "ignoredOtherOrigins": ignored, "counts": dict(Counter(x["category"] + ":" + x["status"] for x in records)),
            "records": records}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", required=True)
    parser.add_argument("--api-port", type=int, choices=(5196, 5197, 5200), default=5200)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    output = Path(args.output).resolve()
    if not output.is_relative_to(VALIDATION.resolve()) or output.exists():
        raise ValueError("Choose a new report under workspace Logs/Validation.")
    report = inspect(args.profile, f"http://127.0.0.1:{args.api_port}/")
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
    print(json.dumps({"report": str(output), "counts": report["counts"], "readOnly": True}))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
