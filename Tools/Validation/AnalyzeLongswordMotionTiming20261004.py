"""Read actual capture telemetry; never alters frames, timing, or runtime assets."""
import argparse
import csv
import json
from collections import OrderedDict
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("capture", type=Path)
parser.add_argument("output", type=Path)
parser.add_argument("--require-clean-start", action="store_true")
args = parser.parse_args()
with (args.capture / "body-telemetry.csv").open(encoding="utf-8-sig", newline="") as stream:
    rows = list(csv.DictReader(stream))
groups = OrderedDict()
for row in rows:
    groups.setdefault(row["phase"], []).append(row)
report = {"capture": str(args.capture), "clips": [], "sourceFrames": len(rows)}
for name, frames in groups.items():
    actions = OrderedDict()
    for row in frames:
        if "sweep" in row["pose"]:
            actions.setdefault(row["sequence"], []).append(row)
    clip = {"name": name, "frameCount": len(frames), "firstDelta": float(frames[0]["delta"]),
            "firstPose": frames[0]["pose"], "firstProgress": float(frames[0]["progress"]),
            "wallSeconds": float(frames[-1]["realtime"]) - float(frames[0]["realtime"]),
            "simulationSeconds": float(frames[-1]["simulation"]) - float(frames[0]["simulation"]),
            "maxCapturedDelta": max(float(r["delta"]) for r in frames), "actions": []}
    for sequence, action in actions.items():
        clip["actions"].append({"sequence": int(sequence), "step": int(action[0]["step"]),
            "pose": action[0]["pose"], "frames": len(action),
            "firstProgress": float(action[0]["progress"]), "lastProgress": float(action[-1]["progress"]),
            "wallStartFromClip": float(action[0]["realtime"]) - float(frames[0]["realtime"]),
            "wallSpan": float(action[-1]["realtime"]) - float(action[0]["realtime"]),
            "simulationSpan": float(action[-1]["simulation"]) - float(action[0]["simulation"]),
            "maxDelta": max(float(r["delta"]) for r in action)})
    report["clips"].append(clip)
report["allActionsBeginAtZero"] = all(a["firstProgress"] == 0 for c in report["clips"] for a in c["actions"])
report["maxActionDelta"] = max(a["maxDelta"] for c in report["clips"] for a in c["actions"])
report["cleanStarts"] = all(c["firstDelta"] < .05 for c in report["clips"])
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({key: value for key, value in report.items() if key != "clips"}, ensure_ascii=False))
if args.require_clean_start:
    assert report["cleanStarts"], "a recording includes an encoding-stall delta at entry"
