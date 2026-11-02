"""Summarize existing sparse frame-timing evidence without modifying its source."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import statistics


def summary(values):
    data = sorted(values)
    return {"n": len(data), "p50": data[int((len(data)-1)*.50)],
            "p95": data[int((len(data)-1)*.95)], "p99": data[int((len(data)-1)*.99)],
            "mean": statistics.mean(data), "max": data[-1]} if data else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    folder = Path(args.run).resolve()
    output = Path(args.output).resolve()
    if output.exists():
        raise ValueError("Choose a new diagnostic output; prior evidence is preserved.")
    timing = folder / "frame-timing.csv"
    rows = [{key: float(value) for key, value in row.items()} for row in csv.DictReader(timing.open(encoding="utf-8-sig"))]
    available = [row for row in rows if row["timing_available"] == 1 and row["seconds"] > 10 and row["focused"] == 1]
    native = [row for row in rows if row["timing_available"] == 1 and row["seconds"] > 10
              and row.get("native_foreground_available") == 1 and row.get("native_foreground") == 1]
    fields = ["cpu_frame_ms", "cpu_main_ms", "cpu_render_ms", "present_wait_ms", "gpu_ms"]
    report = {"source": str(folder), "sourceSha256": hashlib.sha256(timing.read_bytes()).hexdigest(),
              "scope": "Once-per-second engine timing samples; not an independent all-frame FPS distribution",
              "rows": len(rows), "availableFocusedAfter10s": len(available),
              "availableNativeForegroundAfter10s": len(native),
              "nativeForegroundRecorded": any("native_foreground" in row for row in rows),
              "unityFocusNativeBackgroundRows": sum(row["focused"] == 1 and row.get("native_foreground_available") == 1
                                                      and row.get("native_foreground") == 0 for row in rows), "groups": {}}
    for name, sample in [("focused_all", available), ("focused_combat", [r for r in available if r["combat"]]),
                         ("focused_noncombat", [r for r in available if not r["combat"]]),
                         ("native_foreground_all", native),
                         ("native_foreground_combat", [r for r in native if r["combat"]]),
                         ("native_foreground_noncombat", [r for r in native if not r["combat"]])]:
        report["groups"][name] = {field: summary([r[field] for r in sample]) for field in fields}
        report["groups"][name]["presentWaitFraction"] = summary([r["present_wait_ms"]/r["cpu_frame_ms"] for r in sample if r["cpu_frame_ms"] > 0])
        report["groups"][name]["near50msCount"] = sum(abs(r["cpu_frame_ms"]-50) < 1 for r in sample)
        report["groups"][name]["near100msCount"] = sum(abs(r["cpu_frame_ms"]-100) < 1 for r in sample)
    performance = json.loads((folder / "performance.json").read_text(encoding="utf-8-sig"))
    report["allFrameCombatSummary"] = performance["measurements"]
    report["nativeForegroundCombatSummary"] = performance.get("foregroundMeasurements")
    report["allFramePhaseSummary"] = performance.get("phaseMeasurements")
    report["phaseLabels"] = performance.get("phaseLabels")
    count = sum(x["samples"] for x in performance["measurements"])
    seconds = sum(x["seconds"] for x in performance["measurements"])
    report["overallMeasuredCombatFps"] = count / seconds if seconds else None
    report["measuredCombatFrames"] = count
    report["measuredCombatSeconds"] = seconds
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
