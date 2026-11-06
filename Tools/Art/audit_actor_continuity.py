"""Find drawings that jump: frames of an actor that differ too much from their neighbours or from the actor's own idle.

Frames are aligned on their foot pivot and compared the way the game shows them. Reports, per clip,
  jump     share of the silhouette that changes between neighbouring frames, against the clip's own median
  area     visible area of a frame against the idle pose
  foreign  share of a frame's pixels whose colour is not in the idle pose's palette (redrawn or flashed frames)
  entry    how far the clip's first frame and last frame are from the idle pose (a visible snap when the clip starts or ends)

python Tools/Art/audit_actor_continuity.py [--all] > report.txt
"""
import glob
import os
import re
import sys

import numpy
from PIL import Image

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated")
CANVAS = 320
pivot_pattern = re.compile(r"spritePivot: \{x: ([^,]+), y: ([^}]+)\}")


def load(path):
    image = Image.open(path).convert("RGBA")
    match = pivot_pattern.search(open(path + ".meta", encoding="utf-8").read())
    px, py = float(match.group(1)), float(match.group(2))
    plate = numpy.zeros((CANVAS, CANVAS, 4), numpy.uint8)
    x = CANVAS // 2 - round(px * image.width)
    y = CANVAS // 2 - round((1 - py) * image.height)
    pixels = numpy.array(image)
    plate[y:y + image.height, x:x + image.width] = pixels
    return plate


def difference(a, b):
    sa, sb = a[..., 3] > 40, b[..., 3] > 40
    union = sa | sb
    if not union.any():
        return 0.0
    colour = numpy.sum((a[..., :3].astype(int) - b[..., :3].astype(int)) ** 2, axis=2) > 60 ** 2
    return float(numpy.mean(((sa != sb) | (colour & sa & sb))[union]))


def palette(frame):
    colours = frame[frame[..., 3] > 40][:, :3]
    return numpy.unique(colours // 8, axis=0).astype(int) * 8


def foreign(frame, reference):
    colours = frame[frame[..., 3] > 40][:, :3].astype(int)
    if not len(colours) or not len(reference):
        return 0.0
    unique, counts = numpy.unique(colours, axis=0, return_counts=True)
    distance = numpy.min(numpy.sum((unique[:, None, :] - reference[None, :, :]) ** 2, axis=2), axis=1)
    return float(counts[distance > 48 ** 2].sum() / counts.sum())


def clip_frames(folder):
    return [os.path.join(folder, n) for n in sorted(os.listdir(folder)) if re.fullmatch(r"\d+\.png", n)]


def clips_of(actor):
    found = {}
    for path, _, files in os.walk(actor):
        if any(re.fullmatch(r"\d+\.png", f) for f in files):
            found[os.path.relpath(path, actor).replace("\\", "/")] = path
    return found


findings = []
for root in sorted(glob.glob(os.path.join(ART, "Pilot", "*"))):
    if not os.path.isdir(root):
        continue
    actor = os.path.basename(root)
    clips = clips_of(root)
    for name, folder in sorted(clips.items()):
        parts = name.split("/")
        direction = parts[1] if len(parts) > 1 else None
        idle_name = "idle/" + direction if direction and "idle/" + direction in clips else "idle"
        if idle_name not in clips:
            continue
        idle = load(clip_frames(clips[idle_name])[0])
        base_area = int((idle[..., 3] > 40).sum())
        base_palette = palette(idle)
        frames = [load(p) for p in clip_frames(folder)]
        jumps = [difference(a, b) for a, b in zip(frames, frames[1:])]
        median = float(numpy.median(jumps)) if jumps else 0.0
        clip = parts[0]
        dying = clip in ("death", "entrance")
        for i, value in enumerate(jumps):
            if value > .42 and value > 2.2 * max(median, .08) and not dying:
                findings.append((value, f"{actor}/{name} jump {i}->{i + 1}: {value:.0%} (clip median {median:.0%})"))
        for i, frame in enumerate(frames):
            area = int((frame[..., 3] > 40).sum())
            ratio = area / max(1, base_area)
            if not dying and (ratio > 1.55 or ratio < .55):
                findings.append((abs(ratio - 1) / 2, f"{actor}/{name} frame {i}: area {ratio:.2f}x idle"))
            alien = foreign(frame, base_palette)
            if not dying and alien > .12:
                findings.append((alien, f"{actor}/{name} frame {i}: {alien:.0%} colours outside idle palette"))
        if name != idle_name and frames:
            start, end = difference(idle, frames[0]), difference(idle, frames[-1])
            if start > .32:
                findings.append((start, f"{actor}/{name} starts {start:.0%} away from idle"))
            if end > .32 and not dying:
                findings.append((end, f"{actor}/{name} ends {end:.0%} away from idle"))
findings.sort(key=lambda item: -item[0])
for _, text in findings:
    print(text)
print("findings", len(findings))
