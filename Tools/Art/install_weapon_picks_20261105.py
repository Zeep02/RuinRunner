"""Install the weapons chosen from the 2026-11-05 candidates. Run after build_weapons70.py if that is ever run again.

Nothing is resampled. A candidate with extra pieces in its cell keeps only its largest connected part.
PICKS: id -> (candidate set, frame, kind, grip, degrees the weapon points above level)
kind decides how grip and tip are found; a gun's grip is read off the measuring grid because the lowest point is often the stock.

python Tools/Art/install_weapon_picks_20261105.py [--grid out.png]
"""
import os
import re
import sys
from collections import deque

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
STAGE = os.path.join(HERE, "Staging", "PixelLab")
OUT = os.path.join(ROOT, "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated", "Equipment", "Weapon")
CS = os.path.join(ROOT, "Assets", "RuinRunner", "Runtime", "Data", "SignatureWeaponCatalog.cs")
SHORT, LONG, DAGGER = "20261105-weapons-clean/guns-short_0", "20261105-weapons-clean/guns-long_0", "20261105-weapons-clean/daggers_0"
THREE = "20261105-weapons-clean/guns-three_0"
COOL = "20261105-weapons-cool/"
# Drawn with the hilt at the top. Look at every upright pick in the hand before keeping it: the shape alone does not say which end is which.
HILT_UP = {"gravity_stele", "executioner_scale"}
PICKS = {
    "scrap_revolver": (SHORT, 4, "gun", (4, 11), 0), "ricochet_pistol": (SHORT, 10, "gun", (4, 13), 0), "gate_shotgun": (SHORT, 26, "gun", (5, 12), 15),
    "furnace_sprayer": (SHORT, 38, "gun", (3, 15), 0), "meteor_waybill": (SHORT, 49, "gun", (4, 18), 30),
    "scout_carbine": (LONG, 0, "gun", (6, 11), 0), "frost_bolt_rifle": (THREE, 1, "gun", (4, 17), 35), "lightning_carbine": (THREE, 22, "gun", (5, 10), 0),
    "siege_engineer": (THREE, 42, "gun", (4, 11), 0), "last_bell": (LONG, 49, "gun", (6, 4), 0),
    "alley_awl": (DAGGER, 1, "up", None, 90), "saw_fang": (DAGGER, 7, "up", None, 90), "acrobat_knife": (DAGGER, 13, "up", None, 90),
    "frost_fang": (DAGGER, 18, "up", None, 90), "viper_tongue": (DAGGER, 26, "up", None, 90), "ember_claw": (DAGGER, 30, "up", None, 90),
    "shadow_suture": (DAGGER, 40, "up", None, 90), "blood_bank": (DAGGER, 43, "up", None, 90), "hour_minute": (DAGGER, 53, "up", None, 90),
    "night_thread": (DAGGER, 57, "up", None, 90),
    "whetstone_orb": (COOL + "Orb_0", 3, "orb", None, 0), "lantern_orb": (COOL + "Orb_0", 10, "orb", None, 0), "iron_core": (COOL + "Orb_0", 16, "orb", None, 0),
    "frost_satellite": (COOL + "Orb_0", 21, "orb", None, 0), "ember_heart": (COOL + "Orb_0", 29, "orb", None, 0), "storm_marble": (COOL + "Orb_0", 34, "orb", None, 0),
    "twin_star": (COOL + "Orb_0", 40, "orb", None, 0), "mirror_eye": (COOL + "Orb_0", 45, "orb", None, 0), "small_eclipse": (COOL + "Orb_0", 50, "orb", None, 0),
    "world_pendulum": (COOL + "Orb_0", 57, "orb", None, 0),
    "moon_white": (COOL + "Katana_0", 20, "diagonal", None, 45), "red_lotus": (COOL + "Katana_0", 26, "diagonal", None, 45),
    "falling_blossom": (COOL + "Katana_0", 39, "diagonal", None, 45), "instant_king": (COOL + "Katana_0", 50, "diagonal", None, 45),
    "thousand_returns": (COOL + "Katana_0", 58, "diagonal", None, 45),
    "lumber_blade": (COOL + "Greatsword_2", 1, "up", None, 90), "cinder_cleaver": (COOL + "Greatsword_0", 3, "up", None, 90),
    "thunder_slug": (COOL + "Greatsword_2", 5, "up", None, 90), "gravity_stele": (COOL + "Greatsword_0", 6, "up", None, 90),
    "executioner_scale": (COOL + "Greatsword_0", 7, "up", None, 90), "dawn_gate": (COOL + "Greatsword_0", 8, "up", None, 90),
    "thunder_needle": (COOL + "Spear_1", 3, "diagonal", None, 45), "tide_fang": (COOL + "Spear_1", 4, "diagonal", None, 45),
    "chain_horizon": (COOL + "Spear_1", 6, "diagonal", None, 45), "bannerless": (COOL + "Spear_1", 7, "diagonal", None, 45),
    "dragon_spine": (COOL + "Spear_1", 8, "diagonal", None, 45),
    "charcoal_staff": (COOL + "Staff_1", 1, "staff", None, 65), "storm_cloud_staff": (COOL + "Staff_0", 4, "staff", None, 90),
    "swamp_lantern": (COOL + "Staff_0", 5, "staff", None, 90), "refractor_staff": (COOL + "Staff_1", 6, "staff", None, 65),
    "undertaker_candle": (COOL + "Staff_0", 7, "staff", None, 90), "star_fold": (COOL + "Staff_1", 8, "staff", None, 65),
}


def largest_part(image):
    width, height = image.size
    pixels = image.load()
    seen, best = set(), []
    for start_y in range(height):
        for start_x in range(width):
            if (start_x, start_y) in seen or pixels[start_x, start_y][3] == 0:
                continue
            part, queue = [], deque([(start_x, start_y)])
            seen.add((start_x, start_y))
            while queue:
                x, y = queue.popleft()
                part.append((x, y))
                for nx in (x - 1, x, x + 1):
                    for ny in (y - 1, y, y + 1):
                        if 0 <= nx < width and 0 <= ny < height and (nx, ny) not in seen and pixels[nx, ny][3] > 0:
                            seen.add((nx, ny))
                            queue.append((nx, ny))
            if len(part) > len(best):
                best = part
    out = Image.new("RGBA", image.size, (0, 0, 0, 0))
    for x, y in best:
        out.putpixel((x, y), pixels[x, y])
    return out


def picked_image(weapon_id):
    folder, frame, kind, _, _ = PICKS[weapon_id]
    image = Image.open(os.path.join(STAGE, folder, f"{frame:02}.png")).convert("RGBA")
    if kind != "orb":
        image = largest_part(image)
    if weapon_id in HILT_UP:
        image = image.transpose(Image.FLIP_TOP_BOTTOM)
    return image.crop(image.getbbox())


def centre_of_row(image, y):
    alpha = image.getchannel("A")
    xs = [x for x in range(image.width) if alpha.getpixel((x, min(max(y, 0), image.height - 1))) > 0]
    return sum(xs) / len(xs) if xs else image.width / 2


def pose(weapon_id, image):
    _, _, kind, grip, angle = PICKS[weapon_id]
    width, height = image.size
    alpha = image.getchannel("A")
    if kind == "gun":
        right = [y for y in range(height) if alpha.getpixel((width - 1, y)) > 0] or [height // 3]
        return grip[0], grip[1], width - 1, sum(right) / len(right), angle
    if kind == "up":
        y = height - max(4, round(height * .13))
        return centre_of_row(image, y) + .5, y, centre_of_row(image, 1) + .5, 1, angle
    if kind == "staff":
        y = round(height * .62)
        return centre_of_row(image, y) + .5, y, centre_of_row(image, 1) + .5, 1, angle
    if kind == "diagonal":
        return width * .22, height * .78, width - 1, 1, angle
    return width / 2, height / 2, width / 2, height / 2, 0


def grid(path):
    guns = [w for w, p in PICKS.items() if p[2] == "gun"]
    scale = 11
    sheet = Image.new("RGBA", (5 * 400, 2 * 400), (58, 62, 74, 255))
    draw = ImageDraw.Draw(sheet)
    for index, gun in enumerate(guns):
        image = picked_image(gun)
        print(gun, image.size)
        x0, y0 = (index % 5) * 400 + 24, (index // 5) * 400 + 24
        sheet.alpha_composite(image.resize((image.width * scale, image.height * scale), Image.NEAREST), (x0, y0))
        for t in range(0, image.width + 1, 5):
            draw.line((x0 + t * scale, y0 - 6, x0 + t * scale, y0 + image.height * scale), fill=(255, 255, 255, 70))
            draw.text((x0 + t * scale - 3, y0 - 18), str(t), fill=(255, 255, 120, 255))
        for t in range(0, image.height + 1, 5):
            draw.line((x0 - 6, y0 + t * scale, x0 + image.width * scale, y0 + t * scale), fill=(255, 255, 255, 70))
            draw.text((x0 - 20, y0 + t * scale - 5), str(t), fill=(255, 255, 120, 255))
        draw.text((x0, y0 + image.height * scale + 6), gun, fill=(255, 255, 255, 255))
        px, py = PICKS[gun][3]
        draw.ellipse((x0 + px * scale - 4, y0 + py * scale - 4, x0 + px * scale + 4, y0 + py * scale + 4), outline=(0, 255, 120, 255), width=2)
    sheet.save(path)


def install():
    raw = open(CS, "rb").read()
    bom = raw[:3] == b"\xef\xbb\xbf"
    catalog = raw[3 if bom else 0:].decode("utf-8")
    for weapon_id in PICKS:
        image = picked_image(weapon_id)
        width, height = image.size
        grip_x, grip_y, tip_x, tip_y, angle = pose(weapon_id, image)
        path = os.path.join(OUT, weapon_id + ".png")
        image.save(path)
        meta = open(path + ".meta", encoding="utf-8", newline="").read()
        meta = re.sub(r"spritePivot: \{x: [^,]+, y: [^}]+\}", f"spritePivot: {{x: {grip_x / width:.6f}, y: {1 - grip_y / height:.6f}}}", meta)
        open(path + ".meta", "w", encoding="utf-8", newline="").write(meta)
        line = f'{{"{weapon_id}",new NativeWeaponArt.Pose({grip_x:.1f}f,{grip_y:.0f}f,{tip_x:.1f}f,{tip_y:.1f}f,{angle}f)}}'
        catalog, count = re.subn(r'\{"' + weapon_id + r'",new NativeWeaponArt\.Pose\([^)]*\)\}', line, catalog)
        if count != 1:
            raise RuntimeError(weapon_id)
    open(CS, "wb").write((b"\xef\xbb\xbf" if bom else b"") + catalog.encode("utf-8"))
    print(len(PICKS), "weapons installed")


if __name__ == "__main__":
    if "--grid" in sys.argv:
        grid(sys.argv[sys.argv.index("--grid") + 1])
    else:
        install()
