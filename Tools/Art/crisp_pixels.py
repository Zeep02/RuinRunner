"""Make a generated sprite read as clean pixel art instead of a shrunken painting, without resampling it.

Small PixelLab objects come back with far too many colours for their size (a 121px gun with 52 colours), so neighbouring
pixels differ by tiny steps and the shape looks soft. This keeps every pixel where it is and only changes colours:
loose specks go, the colours collapse to a short ramp, and single stray pixels take the colour around them.
"""
from collections import Counter, deque

import numpy
from PIL import Image

MIN_PART_PIXELS = 3
NEIGHBOURS = [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)]


def without_specks(pixels):
    height, width = pixels.shape[:2]
    seen = numpy.zeros((height, width), bool)
    solid = pixels[..., 3] > 0
    for y, x in zip(*numpy.nonzero(solid)):
        if seen[y, x]:
            continue
        seen[y, x] = True
        queue, part = deque([(y, x)]), []
        while queue:
            cy, cx = queue.popleft()
            part.append((cy, cx))
            for dx, dy in NEIGHBOURS:
                ny, nx = cy + dy, cx + dx
                if 0 <= ny < height and 0 <= nx < width and solid[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    queue.append((ny, nx))
        if len(part) < MIN_PART_PIXELS:
            for cy, cx in part:
                pixels[cy, cx] = 0


def fewer_colours(pixels, colours):
    solid = pixels[..., 3] > 0
    if not solid.any():
        return
    strip = Image.fromarray(pixels[solid][:, :3].reshape(-1, 1, 3).astype("uint8"), "RGB")
    reduced = numpy.array(strip.quantize(colors=colours, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB"))
    pixels[solid, :3] = reduced.reshape(-1, 3)


def luminance(colour):
    return colour[0] * .3 + colour[1] * .59 + colour[2] * .11


def no_strays(pixels):
    # An inside pixel with nothing of its colour around it is noise. Outlines and highlights keep a neighbour of their own colour, so they stay.
    height, width = pixels.shape[:2]
    out = pixels.copy()
    for y in range(height):
        for x in range(width):
            if pixels[y, x, 3] == 0:
                continue
            around = [tuple(pixels[y + dy, x + dx][:3]) for dx, dy in NEIGHBOURS if 0 <= y + dy < height and 0 <= x + dx < width and pixels[y + dy, x + dx, 3] > 0]
            own = tuple(pixels[y, x][:3])
            if len(around) < 7 or own in around:
                continue
            out[y, x, :3] = Counter(around).most_common(1)[0][0]
    pixels[...] = out


def crisp(image, colours):
    pixels = numpy.array(image.convert("RGBA"))
    pixels[pixels[..., 3] < 128] = 0
    pixels[pixels[..., 3] > 0, 3] = 255
    without_specks(pixels)
    fewer_colours(pixels, colours)
    no_strays(pixels)
    result = Image.fromarray(pixels, "RGBA")
    return result.crop(result.getbbox())
