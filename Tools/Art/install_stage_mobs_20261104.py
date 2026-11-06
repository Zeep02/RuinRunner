"""Install the fifteen stage mobs from generate_stage_mobs_20261104.py into Separated/Pilot.

The rules are install_remaining_enemies_20261103.py's: frames copied as generated, west a lossless mirror of east,
loose sparks and effect flashes removed because the game plays its own pixelfx over them. This batch differs in two ways:
- The generator drew its own bursts in colours the body also has, so a colour test misses them. An attack or hurt frame
  whose silhouette swells well past the clip's usual size is treated as a flash as well.
- Walks and idles only lose loose sparks and isolated pops. A colour test there froze whole walk cycles.

python Tools/Art/install_stage_mobs_20261104.py [--dry] [Name ...]
"""
import os
import sys

import numpy
from PIL import Image

import install_remaining_enemies_20261103 as base

base.STAGING = os.path.join(base.HERE, "Staging", "PixelLab", "20261104-mobs")
base.SPECK_CLIPS = base.SPECK_CLIPS + ("q_idle", "q_walk")
base.FLASH_CLIPS = tuple(clip for clip in base.FLASH_CLIPS if clip not in ("q_idle", "q_walk"))
base.FLASH_SHARE = .03
SWOLLEN = 1.3
# Bursts neither test catches, found by eye on the contact sheet: play clean frames over them.
base.FRAME_ORDER.update({
    ("BoneShield", "q_idle", "south"): [0, 5, 2, 3, 4, 5],
    ("BoneShield", "q_idle", "east"): [0, 1, 5, 5, 1, 0],
    ("VineLasher", "q_idle", "south"): [0, 1, 4, 5, 4, 1],
    ("VineLasher", "q_walk", "south"): [0, 1, 2, 6, 7, 6, 2, 1],
    ("VineLasher", "q_walk", "east"): [0, 1, 6, 7, 6, 1, 6, 7],
    ("RuinRat", "q_idle", "south"): [0, 1, 2, 4, 4, 5],
    ("MossArcher", "q_attack", "south"): [0, 1, 2, 3, 4, 5, 5, 7, 8, 9],
    ("PowderBeetle", "q_idle", "south"): [0, 1, 4, 5, 4, 1],
    ("PowderBeetle", "q_attack", "south"): [0, 1, 2, 3, 4, 5, 6, 6, 6, 9],
    ("InkShade", "q_hurt", "east"): [3, 3, 3, 4, 5],
    ("SealGolem", "q_attack", "south"): [0, 1, 2, 3, 4, 4, 4, 7, 8, 9],
    ("GildedBolter", "q_attack", "south"): [0, 1, 1, 1, 1, 1, 6, 7, 8, 9],
    ("GildedBolter", "q_hurt", "east"): [1, 2, 3, 3, 3],
    ("SealPriest", "q_hurt", "south"): [1, 1, 1, 4, 5],
    ("Drowned", "q_attack", "south"): [0, 1, 2, 3, 4, 4, 4, 4, 4, 9],
    ("Drowned", "q_hurt", "south"): [1, 1, 1, 4, 5],
    ("EelSpitter", "q_hurt", "east"): [1, 1, 1, 4, 5],
    ("CoralGuard", "q_walk", "south"): [0, 1, 2, 7, 6, 7, 2, 1],
    ("CoralGuard", "q_attack", "south"): [0, 1, 2, 3, 4, 5, 5, 5, 8, 9],
    ("CoralGuard", "q_attack", "east"): [0, 1, 2, 3, 3, 5, 5, 7, 8, 9],
    ("JellyLamp", "q_attack", "south"): [0, 1, 2, 2, 4, 5, 6, 7, 8, 9],
})
CAST = ["BoneShield", "VineLasher", "RuinRat", "MossArcher", "PowderBeetle",
        "Drowned", "EelSpitter", "CoralGuard", "JellyLamp", "TideSiren",
        "InkShade", "SealGolem", "GildedBolter", "ChainWarden", "SealPriest"]


def mark_swollen_frames(character):
    for clip, (_, indices) in base.SECOND_PASS.items():
        if clip not in ("q_attack", "q_hurt"):
            continue
        for direction in base.FOLDERS:
            source = base.clip_folders(character, clip, direction)
            areas = {i: int(numpy.sum(numpy.array(Image.open(os.path.join(source, f"frame_{i:03d}.png")).convert("RGBA"))[..., 3] > 40)) for i in indices}
            usual = float(numpy.median(list(areas.values())))
            swollen = tuple(i for i, area in areas.items() if area > usual * SWOLLEN)
            if swollen and len(swollen) < len(areas):
                base.SEEN_FLASHES[(character, clip, direction)] = swollen


def main():
    args = sys.argv[1:]
    dry = "--dry" in args
    report = []
    for character in [a for a in args if not a.startswith("--")] or CAST:
        mark_swollen_frames(character)
        for clip, (folder, indices) in base.SECOND_PASS.items():
            for direction in base.FOLDERS:
                base.install_clip(character, clip, direction, os.path.join(base.ART, "Pilot", character, folder), indices, dry, report)
        if not dry:
            report.append(f"{character} VisualTop={base.visual_top(character)}px")
    print("\n".join(line for line in report if dry or "VisualTop" in line or "held over" in line))


if __name__ == "__main__":
    main()
