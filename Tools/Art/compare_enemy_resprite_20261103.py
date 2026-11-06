"""Before/after sheet for each resprited enemy: old and new key frames at the same scale, on one ground line.

The hero stands first in every row for size. Nearest-neighbour enlargement is for viewing only.
python Tools/Art/compare_enemy_resprite_20261103.py
"""
import glob
import os
import re

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated"))
OUT = os.path.join(HERE, "Staging", "PixelLab", "20261103-remaining", "compare")
HERO = os.path.join(ART, "External", "SscaryMale", "idle", "down", "00.png")
FONT = ImageFont.truetype(r"C:\Windows\Fonts\malgun.ttf", 18)
SMALL = ImageFont.truetype(r"C:\Windows\Fonts\malgun.ttf", 14)
SCALE = 3
BACK = (66, 72, 62, 255)

# label -> (old clip folder, new clip folder), with the directional subfolder applied to minions.
MINION = [("대기", "idle", "idle"), ("걷기", "walk", "walk"), ("공격", "attack", "attack"), ("피격", "hurt", "hurt"), ("사망", "death", "death")]
CAST = {
    "Shooter": ("External/CraftPix/Slime3", MINION),
    "Leaper": ("External/CraftPix/Slime2", MINION),
    "Tidecaller": ("External/CraftPix/Vampires2", MINION),
    "Sentinel": ("External/CraftPix/Vampires3", MINION),
    "Tidekeeper": ("External/Kronovi/Golem", [("대기", "idle", "idle"), ("물길 시전", "shield_cast", "lanes"), ("웅덩이 시전", "laser_cast", "pools"), ("피격", "immune", "hurt"), ("사망", "death", "death")]),
    "SealHeart": ("Campaign/SealCoreV4", [("대기", "idle", "idle"), ("이동", "move", "move"), ("십자 시전", "cast", "cast"), ("휩쓸기", "sweep", "sweep"), ("돌진", "charge", "charge"), ("피격", "hurt", "hurt"), ("사망", "death", "death")]),
}


def load(path):
    image = Image.open(path).convert("RGBA")
    meta = open(path + ".meta", encoding="utf-8").read()
    x, y = re.search(r"spritePivot: \{x: ([^,]+), y: ([^}]+)\}", meta).groups()
    return image, round(float(x) * image.width), round(float(y) * image.height)


def pick(folder, count):
    files = sorted(glob.glob(os.path.join(folder, "*.png")))
    if not files:
        raise FileNotFoundError(folder)
    if len(files) <= count:
        return files
    return [files[round(i * (len(files) - 1) / (count - 1))] for i in range(count)]


def row(frames):
    """Lay frames left to right on one ground line, each placed by its own pivot. Returns the strip and its ground y."""
    above = max(f[0].height - f[2] for f in frames)
    below = max(f[2] for f in frames)
    width = sum(f[0].getbbox()[2] - f[0].getbbox()[0] + 8 for f in frames)
    strip = Image.new("RGBA", (width, above + below), (0, 0, 0, 0))
    x = 0
    for image, _, pivot_y in frames:
        box = image.getbbox()
        strip.alpha_composite(image.crop(box), (x, above + box[1] - (image.height - pivot_y)))
        x += box[2] - box[0] + 8
    return strip, above


def sheet(name, old_root, clips, direction):
    hero = load(HERO)
    samples = 5 if direction else 3
    lines = []
    for title, root, index in (("전", old_root, 1), ("후", "Pilot/" + name, 2)):
        cells = []
        for clip in clips:
            sub = clip[index]
            folder = os.path.join(ART, *root.split("/"), sub)
            if direction:
                folder = os.path.join(folder, direction)
            frames = [frame for frame in (load(f) for f in pick(folder, samples)) if frame[0].getbbox()]
            cells.append((clip[0], row(frames)))
        lines.append((title, cells))

    label_w = 60
    hero_strip, hero_above = row([hero])
    hero_w = (hero_strip.width + 4) * SCALE
    col_w = [max(line[1][c][1][0].width for line in lines) * SCALE + 24 for c in range(len(clips))]
    tops = [max([hero_above] + [c[1][1] for c in cells]) for _, cells in lines]
    bottoms = [max(hero_strip.height - hero_above, *[c[1][0].height - c[1][1] for c in cells]) for _, cells in lines]
    heights = [(t + b) * SCALE + 36 for t, b in zip(tops, bottoms)]
    canvas = Image.new("RGBA", (label_w + hero_w + sum(col_w) + 20, sum(heights) + 56), BACK)
    draw = ImageDraw.Draw(canvas)
    subtitle = f" ({direction})" if direction else ""
    draw.text((12, 8), name + subtitle + " — 왼쪽 첫 칸은 주인공. 같은 배율, 같은 바닥선", font=FONT, fill=(240, 240, 230, 255))
    y = 44
    for (title, cells), height, top in zip(lines, heights, tops):
        draw.text((14, y + 30), title, font=FONT, fill=(255, 230, 150, 255))
        base = y + 28 + top * SCALE
        draw.line((label_w, base, canvas.width - 8, base), fill=(120, 130, 110, 255))
        canvas.alpha_composite(hero_strip.resize((hero_strip.width * SCALE, hero_strip.height * SCALE), Image.NEAREST), (label_w, base - hero_above * SCALE))
        x = label_w + hero_w
        for c, (clip_label, (strip, above)) in enumerate(cells):
            draw.text((x, y), clip_label, font=SMALL, fill=(210, 220, 210, 255))
            canvas.alpha_composite(strip.resize((strip.width * SCALE, strip.height * SCALE), Image.NEAREST), (x, base - above * SCALE))
            x += col_w[c]
        y += height
    os.makedirs(OUT, exist_ok=True)
    suffix = "-" + direction if direction else ""
    path = os.path.join(OUT, f"{name}{suffix}.png")
    canvas.save(path)
    print(path, canvas.size)


def main():
    for name, (old, clips) in CAST.items():
        if clips is MINION:
            for direction in ("down", "right"):
                sheet(name, old, clips, direction)
        else:
            sheet(name, old, clips, None)


if __name__ == "__main__":
    main()
