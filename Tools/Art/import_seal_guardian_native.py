"""Lossless source-cell extraction. No resampling, recoloring or generated pixels."""
from pathlib import Path
from PIL import Image
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtDirection/ExternalCandidates/20260930/10k-rock-boss/original/TopDown_RockBoss_EBrosAssets"
DEST = ROOT / "Assets/RuinRunner/Resources/Art/Pixel/Separated/External/10k/SealGuardian"
rows = ("down", "up", "left", "right")
manifest = {"source": "https://10kstudios.itch.io/top-down-boss-enemy-animated", "sourceCell": [64, 64],
            "pixelsPerUnit": 32, "groundPivotPixels": [32, 7], "resampling": False, "frames": []}
for clip, count in (("idle", 7), ("walk", 10), ("swipe", 10), ("stomp", 13)):
    source = SOURCE / clip / f"BossEnemy_{clip}.png"
    sheet = Image.open(source).convert("RGBA")
    assert sheet.size == (count * 64, 256), (source, sheet.size)
    for row, direction in enumerate(rows):
        for frame in range(count):
            rect = [frame * 64, row * 64, (frame + 1) * 64, (row + 1) * 64]
            cell = sheet.crop(rect)
            output = DEST / clip / direction / f"{frame:02}.png"
            output.parent.mkdir(parents=True, exist_ok=True)
            cell.save(output)
            assert Image.open(output).convert("RGBA").tobytes() == cell.tobytes()
            manifest["frames"].append({"source": str(source.relative_to(ROOT)), "rect": rect,
                "output": str(output.relative_to(ROOT)), "pixelSha256": hashlib.sha256(cell.tobytes()).hexdigest()})
review = ROOT / "ArtDirection/Review-20261004/seal-guardian"
review.mkdir(parents=True, exist_ok=True)
(review / "native-source.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Extracted {len(manifest['frames'])} native 64x64 cells without resampling")
