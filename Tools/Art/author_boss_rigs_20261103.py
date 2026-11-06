"""Author the Tidekeeper and SealHeart loops, casts and recoils from their PixelLab rotations.

Generated clips redraw the boss every frame, so its details shimmer and, at the frame counts the generator allows,
a windup runs near 8 frames per second. Both bosses are already separate floating parts, so here each part is moved
by whole pixels along eased paths at 15 frames per second. The only pixels that change colour are the glowing ones.
Deaths stay as generated, because they need rubble the rotation does not have.

python Tools/Art/author_boss_rigs_20261103.py
"""
import math
import os
import sys

import numpy
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from install_remaining_enemies_20261103 import ART, FLOAT_GROUND, STAGING, bbox, components, make_dirs, write_meta

# Every cast shares this clock so the art code can address its phases: windup, release, recovery.
CAST = 24
WINDUP = 12
RELEASE = 4
# Room for the parts to travel. The rotation stays centred, so the pivot's x stays at the canvas middle.
PAD = 28
HOT = numpy.array([225, 255, 250], float)


def ease(t):
    return t * t * (3 - 2 * t)


def path(i, *keys):
    """Value at frame i along (frame, value) keys, smoothly eased between neighbours."""
    if i <= keys[0][0]:
        return keys[0][1]
    for (a, va), (b, vb) in zip(keys, keys[1:]):
        if i <= b:
            return va + (vb - va) * ease((i - a) / (b - a))
    return keys[-1][1]


def wave(i, count, amount, phase=0.0):
    return amount * math.sin(2 * math.pi * (i / count + phase))


class Rig:
    def __init__(self, character):
        self.character = character
        rotation = Image.open(os.path.join(STAGING, character, "download", "Idle", "rotations", "south.png")).convert("RGBA")
        self.pixels = numpy.array(rotation)
        self.size = rotation.width
        self.parts = sorted(components(self.pixels[..., 3] > 40), key=len, reverse=True)
        ground = FLOAT_GROUND.get(character) or bbox(rotation)[3]
        canvas = self.size + PAD * 2
        self.pivot_y = round((canvas - (ground + PAD)) / canvas, 5)
        red, green, blue = (self.pixels[..., c].astype(int) for c in range(3))
        # Only the cyan-teal light: saturated, and brighter than anything the stone or gold reaches.
        self.glow = (self.pixels[..., 3] > 40) & (blue > 150) & (green > 140) & (red < 150) & (blue - red > 70)

    def write(self, folder, count, frame):
        """frame(i) -> list of (part, dx, dy) back to front, and glow 0..1."""
        target = os.path.join(ART, "Pilot", self.character, folder)
        make_dirs(target, os.path.join(ART, "Pilot"))
        canvas = self.size + PAD * 2
        for i in range(count):
            moves, glow = frame(i)
            image = numpy.zeros((canvas, canvas, 4), numpy.uint8)
            # Light steps in four bands, like hand-picked ramp colours rather than a smooth fade.
            band = round(max(0.0, min(1.0, glow)) * 4) / 4 * .62
            for part, dx, dy in moves:
                for y, x in part:
                    colour = self.pixels[y, x].astype(float)
                    if self.glow[y, x]:
                        colour[:3] += (HOT - colour[:3]) * band
                    image[y + PAD + round(dy), x + PAD + round(dx)] = colour.astype(numpy.uint8)
            path_out = os.path.join(target, f"{i:02d}.png")
            Image.fromarray(image, "RGBA").save(path_out)
            write_meta(path_out, .5, self.pivot_y)
        # A shorter clip than before must not leave stale frames for anyone listing the folder.
        for name in os.listdir(target):
            if name.endswith(".png") and int(name[:2]) >= count:
                os.remove(os.path.join(target, name))
                os.remove(os.path.join(target, name + ".meta"))
        print(self.character, folder, count, "pivot", self.pivot_y)


def middle(part):
    return sum(p[1] for p in part) / len(part), sum(p[0] for p in part) / len(part)


def tidekeeper():
    rig = Rig("Tidekeeper")
    body = rig.parts[0]
    fists = sorted(rig.parts[1:3], key=lambda part: middle(part)[0])
    drips = rig.parts[3:]

    def scene(i, body_dy, fist_left, fist_right, glow, drip_speed=1.0):
        moves = [(body, 0, body_dy)]
        moves += [(fists[0], *fist_left), (fists[1], *fist_right)]
        # Drips fall from the base and start over.
        for n, drip in enumerate(drips):
            moves.append((drip, 0, body_dy + (i * drip_speed + n * 3) % 6))
        return moves, glow

    def idle(i):
        return scene(i, wave(i, 24, 2), (0, wave(i, 24, 3, -.12)), (0, wave(i, 24, 3, -.2)), .12 + .12 * math.sin(2 * math.pi * i / 24), .5)

    def lanes(i):
        # Fists rise wide while the core charges, slam down and forward, then drift home.
        out = path(i, (0, 0), (11, 11), (12, 11), (14, 3), (16, 2), (23, 0))
        up = path(i, (0, 0), (11, -24), (12, -24), (14, 9), (16, 7), (23, 0))
        lift = path(i, (0, 0), (11, -4), (12, -4), (14, 4), (16, 3), (23, 0))
        glow = path(i, (0, 0), (11, .7), (13, 1), (16, .6), (23, .12))
        return scene(i, lift, (-out, up), (out, up), glow, 1.5)

    def pools(i):
        # Fists gather at the chest while the body hunches, then the whole guardian rears open.
        out = path(i, (0, 0), (11, -13), (12, -13), (14, 18), (16, 16), (23, 0))
        up = path(i, (0, 0), (11, -2), (12, -2), (14, -10), (16, -8), (23, 0))
        lift = path(i, (0, 0), (11, 3), (12, 3), (14, -6), (16, -4), (23, 0))
        glow = path(i, (0, 0), (11, .7), (13, 1), (16, .6), (23, .12))
        return scene(i, lift, (-out, up), (out, up), glow, 1.5)

    def hurt(i):
        kick = (3, 5, 5, 4, 3, 2, 1, 0)[i]
        dip = (1, 2, 2, 1, 1, 0, 0, 0)[i]
        return scene(i, -dip, (-kick, kick // 2), (kick, kick // 2), .5 if i < 3 else .2, .5)

    rig.write("idle", 24, idle)
    rig.write("lanes", CAST, lanes)
    rig.write("pools", CAST, pools)
    rig.write("hurt", 8, hurt)


def sealheart():
    rig = Rig("SealHeart")
    heart = rig.parts[0]
    crown = next(p for p in rig.parts[1:] if middle(p)[1] < 32)
    stones = [p for p in rig.parts[1:] if p is not crown and len(p) > 100]
    debris = [p for p in rig.parts[1:] if p is not crown and len(p) <= 100]
    centre_x, centre_y = middle(heart)
    offsets = [(middle(s)[0] - centre_x, middle(s)[1] - centre_y) for s in stones]

    def scene(heart_dy, crown_dy, stone_moves, glow):
        moves = [(d, 0, 0) for d in debris] + [(heart, 0, heart_dy), (crown, 0, crown_dy)]
        moves += [(s, dx, dy) for s, (dx, dy) in zip(stones, stone_moves)]
        return moves, glow

    def spread(amount, lift=0.0):
        # Along each stone's own line from the heart: negative pulls in, positive throws out.
        return [(amount * ox / math.hypot(ox, oy), amount * oy / math.hypot(ox, oy) + lift) for ox, oy in offsets]

    def idle(i):
        bob = [(0, wave(i, 24, 2, (.25 if oy < 0 else .6) + (.12 if ox < 0 else 0))) for ox, oy in offsets]
        beat = max(0.0, math.sin(2 * math.pi * i / 24)) ** 3
        return scene(wave(i, 24, 1), wave(i, 24, 2, .15), bob, .08 + .3 * beat)

    def move(i):
        bob = [(wave(i, 16, 2, .1 if ox < 0 else .6), wave(i, 16, 3, (.25 if oy < 0 else .6) + (.12 if ox < 0 else 0))) for ox, oy in offsets]
        return scene(wave(i, 8, 1), wave(i, 16, 3, .15), bob, .2)

    def cast(i):
        reach = path(i, (0, 0), (11, -9), (12, -9), (14, 16), (16, 14), (23, 0))
        crown_dy = path(i, (0, 0), (11, 3), (12, 3), (14, -11), (16, -9), (23, 0))
        heart_dy = path(i, (0, 0), (11, 2), (12, 2), (14, -2), (16, -1), (23, 0))
        glow = path(i, (0, 0), (11, .7), (13, 1), (16, .6), (23, .1))
        return scene(heart_dy, crown_dy, spread(reach), glow)

    def sweep(i):
        # The stones swing back one way, then whip all the way round the heart.
        angle = math.radians(path(i, (0, 0), (11, -35), (12, -35), (15, 200), (17, 300), (23, 360)))
        wide = path(i, (0, 1), (11, 1), (14, 1.2), (23, 1))
        turned = []
        for ox, oy in offsets:
            x = ox * math.cos(angle) - oy * math.sin(angle) * .85
            y = ox * math.sin(angle) * .85 + oy * math.cos(angle)
            turned.append((x * wide - ox, y * wide - oy))
        lean = path(i, (0, 0), (11, 2), (15, -1), (23, 0))
        glow = path(i, (0, 0), (11, .5), (14, .9), (18, .4), (23, .1))
        return scene(lean, -abs(lean), turned, glow)

    def charge(i):
        # Compress and lock on, then everything loose is blown back while it rushes, then settles.
        reach = path(i, (0, 0), (11, -11), (12, -11), (13, 6), (15, 8), (23, 0))
        trail = path(i, (0, 0), (12, 0), (13, -7), (15, -7), (23, 0))
        crown_dy = path(i, (0, 0), (11, 5), (12, 5), (13, -9), (15, -8), (23, 0))
        heart_dy = path(i, (0, 0), (11, 4), (12, 4), (13, -2), (15, -2), (23, 0))
        glow = path(i, (0, 0), (11, .8), (13, 1), (16, .5), (23, .1))
        return scene(heart_dy, crown_dy, spread(reach, trail), glow)

    def hurt(i):
        kick = (3, 5, 5, 4, 3, 2, 1, 0)[i]
        dip = (1, 2, 2, 1, 1, 0, 0, 0)[i]
        return scene(dip, -kick, spread(kick), .5 if i < 3 else .15)

    rig.write("idle", 24, idle)
    rig.write("move", 16, move)
    rig.write("cast", CAST, cast)
    rig.write("sweep", CAST, sweep)
    rig.write("charge", CAST, charge)
    rig.write("hurt", 8, hurt)


if __name__ == "__main__":
    tidekeeper()
    sealheart()
