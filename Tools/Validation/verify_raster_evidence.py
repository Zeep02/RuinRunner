"""Reject unreadable or uniform full-screen captures before visual review.

This is an evidence sanity check, not a visual-quality or gameplay assertion.
It never edits an image. Small diagnostic masks are outside its scope.
"""
import argparse
import json
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
args = parser.parse_args()
checked, failures = [], []
for path in sorted(args.directory.rglob("*.png")):
    try:
        with Image.open(path) as image:
            if image.width < 640 or image.height < 360:
                continue
            extrema = image.convert("RGB").getextrema()
            checked.append(str(path.relative_to(args.directory)))
            if all(low == high for low, high in extrema):
                failures.append({"path": checked[-1], "reason": "Uniform screen; rendering evidence is invalid."})
    except Exception as error:
        failures.append({"path": str(path), "reason": str(error)})
result = {"passed": bool(checked) and not failures, "screens": len(checked), "failures": failures,
          "scope": "Full-screen raster content sanity only; manual review still required."}
(args.directory / "raster-evidence.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps({"passed": result["passed"], "screens": len(checked), "invalid": len(failures)}))
raise SystemExit(0 if result["passed"] else 1)
