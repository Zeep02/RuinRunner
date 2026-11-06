"""Seal posts: cut the chosen candidate per stage and write eight rising frames, the post coming up out of the floor with its base fixed.
Gate: animate the chosen door candidates, then install the opening clip.

python Tools/Art/install_gate_and_seals_20261103.py seals
python Tools/Art/install_gate_and_seals_20261103.py animate    queue the door opening on each candidate
python Tools/Art/install_gate_and_seals_20261103.py sheet      contact sheet of the door candidates' clips
python Tools/Art/install_gate_and_seals_20261103.py gate <object id>
"""
import glob
import io
import os
import re
import sys
import zipfile

import requests
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from install_remaining_enemies_20261103 import ART, folder_meta, write_meta
from pixellab_mcp_client import PixelLab

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261103-gate")
OWNER = "5ba0435a-6d49-45f7-aa17-9ce34a6bd358"
SHEET = "975f7a38-4ef7-4d3b-9c7e-316da026197d"
# Slots on the review sheet, chosen by eye.
POSTS = {"Outer": 0, "Flooded": 5, "Core": 10}
DOORS = ["bd961c54-11f8-46e3-8e28-9e36c193f981", "af941350-2a66-4185-8fcf-99cc3b38c1a8"]
RISE_FRAMES = 8
GATE_FRAMES = 12
OPENING = ("The two heavy dark door leaves slowly swing open inward, parting from the middle and folding to the left and right sides until the doorway is fully open, "
           "revealing a dark passage filled with a soft glowing amber light inside. The stone arch, moss and steps stay exactly still and keep their exact shape. "
           "No sparks, no smoke, no effects outside the doorway.")


def ensure(folder):
    if not os.path.isdir(folder):
        os.makedirs(folder)
    folder_meta(folder)


def seals():
    root = os.path.join(ART, "Campaign", "Seal")
    ensure(root)
    os.makedirs(os.path.join(ROOT, "posts"), exist_ok=True)
    for theme, slot in POSTS.items():
        source = os.path.join(ROOT, "posts", f"{slot:02d}.png")
        if not os.path.exists(source):
            response = requests.get(f"https://backblaze.pixellab.ai/file/pixellab-characters/objects/{OWNER}/{SHEET}/rotations/frame_{slot}.png", timeout=60)
            response.raise_for_status()
            open(source, "wb").write(response.content)
        image = Image.open(source).convert("RGBA")
        image = image.crop(image.getbbox())
        target = os.path.join(root, theme)
        ensure(target)
        for k in range(RISE_FRAMES):
            rows = max(1, round(image.height * (k + 1) / RISE_FRAMES))
            frame = image.crop((0, 0, image.width, rows))
            path = os.path.join(target, f"{k:02d}.png")
            frame.save(path)
            write_meta(path, .5, round(1 / (rows * 2), 5))
        print(theme, image.size)


def animate():
    api = PixelLab()
    for door in DOORS:
        for attempt in range(40):
            result = api.call("animate_object", {"object_id": door, "animation_description": OPENING, "frame_count": 12, "display_name": "gate_open"})
            text = " ".join(part.get("text", "") for part in result.get("content", []) if part.get("type") == "text")
            if not result.get("isError") and "slots" not in text and "rate limit" not in text:
                print(door, text[:160], flush=True)
                break
            api.call("wait_for_jobs", {"min_finished": 1, "timeout_seconds": 50})


def download(door):
    folder = os.path.join(ROOT, door)
    headers = {"Authorization": "Bearer " + os.environ["PIXELLAB_API_TOKEN"]}
    response = requests.get(f"https://api.pixellab.ai/mcp/objects/{door}/download", headers=headers, timeout=120)
    response.raise_for_status()
    zipfile.ZipFile(io.BytesIO(response.content)).extractall(folder)
    return folder


def clip_files(folder):
    found = sorted(p for p in glob.glob(os.path.join(folder, "**", "*.png"), recursive=True) if "animations" in p.replace("\\", "/"))
    return found


def sheet():
    cell = 112
    canvas = Image.new("RGB", (13 * (cell + 2), len(DOORS) * (cell + 2)), (70, 74, 66))
    for row, door in enumerate(DOORS):
        for column, path in enumerate(clip_files(download(door))[:13]):
            image = Image.open(path).convert("RGBA")
            canvas.paste(image, (column * (cell + 2), row * (cell + 2)), image)
    canvas.save(os.path.join(ROOT, "doors.png"))


def gate(door):
    files = clip_files(os.path.join(ROOT, door))
    target = os.path.join(ART, *"Campaign/Gate".split("/"))
    ensure(target)
    # Every frame shares the first frame's crop, so the arch cannot shift while the doors open.
    box = None
    images = [Image.open(p).convert("RGBA") for p in files]
    for image in images:
        bounds = image.getbbox()
        box = bounds if box is None else (min(box[0], bounds[0]), min(box[1], bounds[1]), max(box[2], bounds[2]), max(box[3], bounds[3]))
    picks = [round(i * (len(images) - 1) / (GATE_FRAMES - 1)) for i in range(GATE_FRAMES)]
    for out, pick in enumerate(picks):
        frame = images[pick].crop(box)
        path = os.path.join(target, f"{out:02d}.png")
        frame.save(path)
        write_meta(path, .5, round(2 / frame.height, 5))
    print("gate", box, picks)


if __name__ == "__main__":
    action = sys.argv[1]
    if action == "gate":
        gate(sys.argv[2])
    else:
        {"seals": seals, "animate": animate, "sheet": sheet}[action]()
