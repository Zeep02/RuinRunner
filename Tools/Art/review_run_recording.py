"""Create traceable review stills from a real-time screen recording.

Source frames are copied without alteration. Contact sheets are review aids,
not new game art or substitutes for the timestamped video.
"""
import argparse
import csv
import json
import shutil
from pathlib import Path
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
args = parser.parse_args()
rows = list(csv.DictReader((args.directory / "recording.csv").open(encoding="utf-8-sig")))
out = args.directory / "review"
out.mkdir(exist_ok=True)
groups = {}
for row in rows:
    groups.setdefault((int(row["stage"]), int(row["room"])), []).append(row)
manifest = []
for (stage, room), group in groups.items():
    # Four evenly spaced samples from each encounter, preserving actual time.
    samples = [group[round((len(group) - 1) * fraction)] for fraction in (.15, .4, .65, .9)]
    sheet = Image.new("RGB", (1280, 768), "#181822")
    draw = ImageDraw.Draw(sheet)
    for index, row in enumerate(samples):
        source = args.directory / "frames" / f"frame-{int(row['frame']):06d}.png"
        destination = out / f"s{stage}-r{room}-{index + 1}.png"
        shutil.copyfile(source, destination)
        img = Image.open(source).convert("RGB")
        img.thumbnail((640, 360), Image.Resampling.NEAREST)
        x, y = (index % 2) * 640, (index // 2) * 384
        sheet.paste(img, (x, y + 24))
        draw.text((x + 8, y + 5), f"{float(row['realtime']):.2f}s | {row['weapon']} | {row['pose']} {row['pose_frame']} | HP {row['health']}", fill="white")
        manifest.append(dict(stage=stage, room=room, frame=int(row['frame']), realtime=float(row['realtime']), file=str(destination)))
    sheet.save(out / f"contact-s{stage}-r{room}.png")
ending = args.directory / "frames" / f"frame-{int(rows[-1]['frame']):06d}.png"
shutil.copyfile(ending, out / "ending.png")
summary = [dict(stage=k[0], room=k[1], start=float(g[0]['realtime']), end=float(g[-1]['realtime']), samples=len(g)) for k, g in groups.items()]
(out / "review-manifest.json").write_text(json.dumps(dict(encounters=summary, stills=manifest), indent=2), encoding="utf-8")
print(json.dumps(summary))
