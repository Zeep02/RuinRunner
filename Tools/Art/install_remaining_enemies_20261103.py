"""Install the PixelLab downloads from generate_remaining_enemies_20261103.py into Separated/Pilot.

Nothing is resampled. Frames are copied byte-for-byte, except that west is a lossless horizontal mirror of east,
loose sparks are erased from attack and hurt frames, and a frame holding an effect flash the generator drew anyway
is replaced by its nearest clean neighbour, because the game plays its own pixelfx over these.

Each direction and clip keeps its generated canvas, so the foot pivot is worked out per clip by locating
the character's rotation inside the clip's first frame; a clip with no reference frame uses the canvas centre.

python Tools/Art/install_remaining_enemies_20261103.py [--dry] [Name ...]
"""
import os
import re
import shutil
import sys
import uuid
from collections import deque

import numpy
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
STAGING = os.path.join(HERE, "Staging", "PixelLab", "20261103-remaining")
ART = os.path.join(PROJECT, "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated")
TEMPLATE = open(os.path.join(ART, "External", "CraftPix", "Slime1", "idle", "down", "00.png.meta"), encoding="utf-8").read()
FOLDERS = {"south": "down", "north": "up", "east": "right"}

# clip -> (game folder, stored frame indices). Looping clips end on a copy of the first frame, which is dropped; deaths drop their last frame only when the game wants fewer.
MINIONS = {
    "Shooter": {"rr_idle": ("idle", range(0, 6)), "rr_walk": ("walk", range(0, 8)), "rr_attack": ("attack", range(0, 9)),
                "rr_hurt": ("hurt", range(1, 6)), "rr_death": ("death", range(0, 10))},
    "Leaper": {"rr_idle": ("idle", range(0, 6)), "rr_walk": ("walk", range(0, 8)), "rr_attack": ("attack", range(0, 11)),
               "rr_hurt": ("hurt", range(1, 6)), "rr_death": ("death", range(0, 10))},
    "Tidecaller": {"rr_idle": ("idle", range(0, 4)), "rr_walk": ("walk", range(0, 6)), "rr_attack": ("attack", range(0, 12)),
                   "rr_hurt": ("hurt", range(1, 5)), "rr_death": ("death", range(0, 11))},
}
MINIONS["Sentinel"] = MINIONS["Tidecaller"]
SECOND_PASS = {"q_idle": ("idle", range(0, 6)), "q_walk": ("walk", range(0, 8)), "q_attack": ("attack", range(0, 10)),
               "q_hurt": ("hurt", range(1, 6)), "q_death": ("death", range(0, 10))}
for name in ("Grunt", "Sporeling", "RuneTotem", "TideCrab", "Wisp", "SealKnight", "Hexer"):
    MINIONS[name] = SECOND_PASS
MINIONS["Charger"] = {"q_idle": ("idle", range(0, 4)), "q_run": ("run", range(0, 8)), "q_attack": ("attack", range(0, 8)),
                      "q_run_attack": ("run_attack", range(0, 8)), "q_hurt": ("hurt", range(1, 7)), "q_death": ("death", range(0, 8))}
# Everything else these two play is authored by author_boss_rigs_20261103.py from the same rotation.
BOSSES = {
    "Tidekeeper": {"tk_death": ("death", range(0, 14))},
    "SealHeart": {"sh_death": ("death", range(0, 16))},
    "Warden": {"wd_idle": ("idle", range(0, 16)), "wd_walk2": ("walk", range(0, 16)), "wd_pound": ("pound", range(0, 16)),
               "wd_punch": ("punch", range(0, 16)), "wd_charge": ("charge", range(0, 16)), "wd_leap": ("leap", range(0, 16)),
               "wd_hit": ("hit", range(0, 16)), "wd_death": ("death", range(0, 16))},
}
# The boss rises out of its own rubble: its death clip played backwards.
ENTRANCE_FROM_DEATH = ("Warden",)
# Loose pixels below this size are sparks, not body parts. Clips where debris is the point (deaths) are left alone.
SPECK_PIXELS = 14
SPECK_CLIPS = ("rr_attack", "rr_hurt", "q_attack", "q_run_attack", "q_hurt", "wd_idle", "wd_walk2", "wd_pound", "wd_punch", "wd_charge", "wd_leap", "wd_hit")
# A frame is a flash when this share of its pixels is further than FLASH_DISTANCE from every colour of the design.
FLASH_DISTANCE = 75
FLASH_SHARE = .045
FLASH_CLIPS = ("rr_attack", "rr_hurt", "rr_death", "q_idle", "q_walk", "q_run", "q_attack", "q_run_attack", "q_hurt", "q_death", "wd_idle", "wd_walk2", "wd_pound", "wd_punch", "wd_charge", "wd_leap", "wd_hit", "wd_death")
# Or when this share of its pixels is brighter than nearly everything in the design.
BRIGHT_SHARE = .12
# White flashes the design's own white hides from both measures, found by eye on the contact sheet.
# From behind the generator turned the face toward the camera mid-clip, twice. Play out and back on the frames before that.
FRAME_ORDER = {("Tidecaller", "rr_attack", "north"): [0, 1, 2, 3, 4, 4, 4, 4, 3, 2, 1, 12],
               # The generator drew its own impact flash over these contact frames. The game plays its own, so land on the clean pose after it.
               ("Warden", "wd_pound", "south"): [0, 1, 2, 3, 4, 5, 6, 7, 12, 12, 12, 12, 12, 13, 14, 15],
               ("Warden", "wd_punch", "south"): [0, 1, 2, 3, 4, 5, 6, 7, 8, 11, 11, 11, 12, 13, 14, 15],
               ("Warden", "wd_charge", "south"): [0, 1, 2, 3, 4, 5, 6, 7, 12, 12, 12, 12, 12, 13, 14, 15],
               ("Warden", "wd_death", "south"): [0, 1, 1, 6, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 16]}
SEEN_FLASHES = {("Tidecaller", "rr_attack", "south"): (7, 8), ("Tidecaller", "rr_hurt", "east"): (2,),
                ("Wisp", "q_attack", "south"): (4, 5), ("Wisp", "q_hurt", "south"): (2, 3), ("Wisp", "q_hurt", "east"): (2, 3), ("Wisp", "q_hurt", "north"): (2, 3)}
# Loops should breathe, not pop: a frame this far from the clip's typical frame-to-frame change is replaced by its neighbour.
LOOP_CLIPS = ("q_idle", "q_walk", "q_run", "rr_idle", "rr_walk", "wd_idle", "wd_walk2")
LOOP_OUTLIER = 2.2
# The Hexer's floating gem should stay magenta; the generator keeps blowing it out to white.
GEM_ACTORS = ("Hexer",)
# Ground line of the floating boss, measured from the rotation's top: its body centre must stay 47px above
# the pivot, where the fixed hurtbox has always been (1.47 units). Other enemies stand on their lowest pixel.
FLOAT_GROUND = {"Tidekeeper": 100}


def bbox(image):
    return image.getchannel("A").point(lambda a: 255 if a > 40 else 0).getbbox()


def locate(reference, frame):
    """Translation of the reference inside a frame, or None when the frame is not the reference."""
    ref, got = bbox(reference), bbox(frame)
    if ref is None or got is None or ref[2] - ref[0] != got[2] - got[0] or ref[3] - ref[1] != got[3] - got[1]:
        return None
    cut = reference.crop(ref)
    if cut.tobytes() != frame.crop(got).tobytes():
        return None
    return got[0] - ref[0], got[1] - ref[1]


def components(alpha):
    height, width = alpha.shape
    seen = numpy.zeros(alpha.shape, bool)
    found = []
    for start_y, start_x in zip(*numpy.nonzero(alpha)):
        if seen[start_y, start_x]:
            continue
        seen[start_y, start_x] = True
        queue, pixels = deque([(start_y, start_x)]), []
        while queue:
            y, x = queue.popleft()
            pixels.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < width and alpha[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
        found.append(pixels)
    return found


def without_specks(image):
    pixels = numpy.array(image)
    changed = False
    for part in components(pixels[..., 3] > 40):
        if len(part) < SPECK_PIXELS:
            changed = True
            for y, x in part:
                pixels[y, x] = 0
    return Image.fromarray(pixels, "RGBA") if changed else image


def flash_share(image, palette):
    pixels = numpy.array(image)
    colours = pixels[pixels[..., 3] > 40][:, :3].astype(int)
    if not len(colours):
        return 0.0
    nearest = numpy.min(numpy.sum((colours[:, None, :] - palette[None, :, :]) ** 2, axis=2), axis=1)
    return float(numpy.mean(nearest > FLASH_DISTANCE ** 2))


def luminance(colours):
    return colours[..., 0] * .3 + colours[..., 1] * .59 + colours[..., 2] * .11


def bright_share(image, limit):
    pixels = numpy.array(image)
    return float(numpy.mean(luminance(pixels[pixels[..., 3] > 40].astype(float)) > limit))


def frame_change(a, b):
    pa, pb = numpy.array(a), numpy.array(b)
    if pa.shape != pb.shape:
        return 1.0
    sa, sb = pa[..., 3] > 40, pb[..., 3] > 40
    union = sa | sb
    colour = numpy.sum((pa[..., :3].astype(int) - pb[..., :3].astype(int)) ** 2, axis=2) > 60 ** 2
    return float(numpy.mean(((sa != sb) | (colour & sa & sb))[union])) if union.any() else 0.0


def loop_outliers(images, indices):
    """Frames that differ sharply from both neighbours while the rest of the loop moves gently."""
    order = list(indices)
    changes = [frame_change(images[order[i]], images[order[(i + 1) % len(order)]]) for i in range(len(order))]
    typical = max(float(numpy.median(changes)), .06)
    found = []
    for i in range(1, len(order)):
        if changes[i - 1] > typical * LOOP_OUTLIER and changes[i] > typical * LOOP_OUTLIER:
            found.append(order[i])
    return found


def calm_gem(image):
    pixels = numpy.array(image)
    alpha = pixels[..., 3] > 40
    for part in components(alpha):
        if len(part) > 90:
            continue
        for y, x in part:
            if int(pixels[y, x, 0]) + int(pixels[y, x, 1]) + int(pixels[y, x, 2]) > 600:
                pixels[y, x, :3] = (232, 120, 255)
    return Image.fromarray(pixels, "RGBA")


def curated(character, clip, direction, source, indices, rotation, report):
    """The frames this clip installs, as (image, untouched source path or None)."""
    pixels = numpy.array(rotation)
    palette = numpy.unique(pixels[pixels[..., 3] > 40][:, :3], axis=0).astype(int)
    paths = {i: os.path.join(source, f"frame_{i:03d}.png") for i in indices}
    images = {i: Image.open(paths[i]).convert("RGBA") for i in indices}
    limit = numpy.percentile(luminance(pixels[pixels[..., 3] > 40].astype(float)), 98) + 10
    flashed = [i for i in indices if clip in FLASH_CLIPS and (flash_share(images[i], palette) > FLASH_SHARE or
                                                              bright_share(images[i], limit) > BRIGHT_SHARE or
                                                              i in SEEN_FLASHES.get((character, clip, direction), ()))]
    if clip in LOOP_CLIPS:
        flashed += [i for i in loop_outliers(images, indices) if i not in flashed]
    clean = [i for i in indices if i not in flashed]
    if flashed:
        report.append(f"  held over flash {character} {clip} {direction}: {flashed}")
    result = []
    for i in indices:
        use = i if i in clean else min(clean, key=lambda c: (abs(c - i), c > i))
        image = images[use]
        stripped = without_specks(image) if clip in SPECK_CLIPS else image
        if character in GEM_ACTORS:
            stripped = calm_gem(stripped)
        result.append((stripped, paths[use] if stripped is image else None))
    return result


def clip_folders(character, clip, direction):
    # Queued one direction at a time, so PixelLab may keep each direction under its own named group.
    root = os.path.join(STAGING, character, "download", "Idle", "animations")
    found = []
    for name in os.listdir(root):
        if name == clip or name.startswith(clip + "-") or name.startswith(clip + "_"):
            path = os.path.join(root, name, direction)
            if os.path.isdir(path):
                found.append(path)
    if not found:
        raise FileNotFoundError(f"{character} {clip} {direction}")
    return max(found, key=os.path.getmtime)


def write_meta(path, pivot_x, pivot_y):
    meta_path = path + ".meta"
    guid = None
    if os.path.exists(meta_path):
        match = re.search(r"^guid: ([0-9a-f]+)", open(meta_path, encoding="utf-8").read(), re.M)
        guid = match.group(1) if match else None
    meta = re.sub(r"(?m)^guid: [0-9a-f]+", "guid: " + (guid or uuid.uuid4().hex), TEMPLATE)
    meta = re.sub(r"spritePivot: \{x: [^,]+, y: [^}]+\}", f"spritePivot: {{x: {pivot_x}, y: {pivot_y}}}", meta)
    open(meta_path, "w", encoding="utf-8", newline="\n").write(meta)


def folder_meta(path):
    meta_path = path + ".meta"
    if not os.path.exists(meta_path):
        open(meta_path, "w", encoding="utf-8", newline="\n").write(
            "fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def make_dirs(path, stop):
    parts = []
    while path != stop and not os.path.isdir(path):
        parts.append(path)
        path = os.path.dirname(path)
    for item in reversed(parts):
        os.mkdir(item)
        folder_meta(item)


def install_clip(character, clip, direction, target, indices, dry, report):
    source = clip_folders(character, clip, direction)
    rotation = Image.open(os.path.join(STAGING, character, "download", "Idle", "rotations", direction + ".png")).convert("RGBA")
    first = Image.open(os.path.join(source, "frame_000.png")).convert("RGBA")
    width, height = first.size
    ground = FLOAT_GROUND.get(character)
    if ground is None:
        ground = bbox(rotation)[3]
    shift = locate(rotation, first)
    exact = shift is not None
    if shift is None:
        # The clip grew around the rotation's own centre.
        shift = ((width - rotation.width) // 2, (height - rotation.height) // 2)
    centre_x = (rotation.width // 2 + shift[0]) / width
    pivot_y = round((height - (ground + shift[1])) / height, 5)
    report.append(f"{character:10} {clip:9} {direction:5} canvas={width}x{height} shift={shift} {'exact' if exact else 'centred'} pivot={centre_x:.4f},{pivot_y}")
    if dry:
        return
    frames = curated(character, clip, direction, source, FRAME_ORDER.get((character, clip, direction), indices), rotation, report)
    mirrors = [(FOLDERS[direction], False)] + ([("left", True)] if direction == "east" else [])
    for folder, mirror in mirrors:
        destination = os.path.join(target, folder)
        make_dirs(destination, os.path.join(ART, "Pilot"))
        for out, (image, original) in enumerate(frames):
            path = os.path.join(destination, f"{out:02d}.png")
            if mirror:
                image.transpose(Image.FLIP_LEFT_RIGHT).save(path)
            elif original:
                shutil.copyfile(original, path)
            else:
                image.save(path)
            write_meta(path, round(1 - centre_x if mirror else centre_x, 5), pivot_y)


def install_boss_clip(character, clip, target, indices, dry, report):
    source = clip_folders(character, clip, "south")
    rotation = Image.open(os.path.join(STAGING, character, "download", "Idle", "rotations", "south.png")).convert("RGBA")
    first = Image.open(os.path.join(source, "frame_000.png")).convert("RGBA")
    width, height = first.size
    ground = FLOAT_GROUND.get(character)
    if ground is None:
        ground = bbox(rotation)[3]
    shift = locate(rotation, first)
    exact = shift is not None
    if shift is None:
        shift = ((width - rotation.width) // 2, (height - rotation.height) // 2)
    pivot_x = round((rotation.width // 2 + shift[0]) / width, 5)
    pivot_y = round((height - (ground + shift[1])) / height, 5)
    report.append(f"{character:10} {clip:9} south canvas={width}x{height} shift={shift} {'exact' if exact else 'centred'} pivot={pivot_x},{pivot_y}")
    if dry:
        return
    make_dirs(target, os.path.join(ART, "Pilot"))
    frames = curated(character, clip, "south", source, FRAME_ORDER.get((character, clip, "south"), indices), rotation, report)
    for out, (image, original) in enumerate(frames):
        path = os.path.join(target, f"{out:02d}.png")
        if original:
            shutil.copyfile(original, path)
        else:
            image.save(path)
        write_meta(path, pivot_x, pivot_y)


def visual_top(character):
    """Highest opaque pixel over the shared foot pivot across idle, move, attack and hurt, for the health bar."""
    top = 0
    root = os.path.join(ART, "Pilot", character)
    for clip in ("idle", "walk", "run", "attack", "hurt"):
        for direction in ("down", "up", "left", "right"):
            folder = os.path.join(root, clip, direction)
            if not os.path.isdir(folder):
                continue
            for name in os.listdir(folder):
                if not name.endswith(".png"):
                    continue
                path = os.path.join(folder, name)
                image = Image.open(path).convert("RGBA")
                box = bbox(image)
                pivot = float(re.search(r"spritePivot: \{x: [^,]+, y: ([^}]+)\}", open(path + ".meta", encoding="utf-8").read()).group(1))
                top = max(top, image.height - box[1] - round(image.height * pivot))
    return top


def main():
    args = sys.argv[1:]
    dry = "--dry" in args
    names = [a for a in args if not a.startswith("--")] or list(MINIONS) + list(BOSSES)
    report = []
    for character in names:
        if character in MINIONS:
            for clip, (folder, indices) in MINIONS[character].items():
                for direction in FOLDERS:
                    install_clip(character, clip, direction, os.path.join(ART, "Pilot", character, folder), indices, dry, report)
            if not dry:
                report.append(f"{character} VisualTop={visual_top(character)}px")
        else:
            for clip, (folder, indices) in BOSSES[character].items():
                install_boss_clip(character, clip, os.path.join(ART, "Pilot", character, folder), indices, dry, report)
            if character in ENTRANCE_FROM_DEATH and not dry:
                death = os.path.join(ART, "Pilot", character, "death")
                entrance = os.path.join(ART, "Pilot", character, "entrance")
                make_dirs(entrance, os.path.join(ART, "Pilot"))
                names = sorted(n for n in os.listdir(death) if n.endswith(".png"))
                for out, name in enumerate(reversed(names)):
                    path = os.path.join(entrance, f"{out:02d}.png")
                    shutil.copyfile(os.path.join(death, name), path)
                    pivot = re.search(r"spritePivot: \{x: ([^,]+), y: ([^}]+)\}", open(os.path.join(death, name) + ".meta", encoding="utf-8").read())
                    write_meta(path, pivot.group(1), pivot.group(2))
    print("\n".join(report))


if __name__ == "__main__":
    main()
