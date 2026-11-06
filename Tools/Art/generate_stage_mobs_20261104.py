"""Fifteen more enemies, five per stage, on the same recipe as generate_remaining_enemies_20261103.py.

Every clip interpolates back to the character's rotation so they share one stance. Character ids and requests are recorded in
Tools/Art/Staging/PixelLab/20261104-mobs/<Name>/request.json; nothing already queued is queued twice.

Run with PIXELLAB_API_TOKEN set:
  python Tools/Art/generate_stage_mobs_20261104.py create [Name ...]    first image and rotations, for review
  python Tools/Art/generate_stage_mobs_20261104.py animate [Name ...]   clips, then download
"""
import base64
import io
import json
import os
import re
import sys
import time
import zipfile

import requests

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pixellab_mcp_client import PixelLab

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261104-mobs")
STYLE_ANCHOR = os.path.join(HERE, "..", "..", "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated", "External", "SscaryMale", "idle", "down", "00.png")
DIRECTIONS = ["south", "north", "east"]
BODY_ONLY = " Only the character's own body moves and it keeps its exact design. No projectiles, sparks, smoke, light bursts, motion lines or other effects, and nothing new grows on it."
LOOK = ", a small chibi monster about 32 pixels tall for a cute top-down action roguelike, big readable silhouette, standing facing the viewer, full body, clean dark outline, no ground, no shadow, no text"
CLIPS = (("q_idle", 6, True), ("q_walk", 8, True), ("q_attack", 10, True), ("q_hurt", 6, True), ("q_death", 10, False))

CAST = {
    # Stage 1: sunlit mossy ruins
    "BoneShield": ("little skeleton soldier hiding behind a huge round wooden shield with a mossy iron rim", (
        "idle loop: the skeleton breathes slowly behind the big shield, the shield stays held in front, feet stay planted",
        "marching walk loop: bony feet step in turn, the body sways, the big shield stays held in front at the same size",
        "shield bash: frames 1-3 pulls the shield back to the chest, frames 4-6 shoves the shield hard forward leaning in, frames 7-10 brings it back to guard",
        "hit reaction: the shield rattles and the skull tips back, then it steadies back to standing",
        "death: the shield drops, the bones come apart and fall into a small heap with the shield on top")),
    "VineLasher": ("walking carnivorous plant bulb with a toothy mouth, two long green vine whips for arms and short root feet", (
        "idle loop: the bulb sways, the vine arms curl and uncurl slowly, roots stay planted",
        "shuffling walk loop: the short roots step in turn, the bulb bobs, the vines trail at the same size",
        "vine lash: frames 1-3 draws both vines far back and the mouth opens, frames 4-6 whips both vines forward fast, frames 7-10 curls them back to rest",
        "hit reaction: the bulb squashes with eyes shut, the vines flinch, then it straightens",
        "death: the vines wilt, the bulb droops and withers into a small dry heap of leaves")),
    "RuinRat": ("quick rat bandit wearing a small brown hood and holding a tiny rusty dagger, standing upright on its hind legs", (
        "idle loop: the rat sniffs, whiskers twitch, tail sways, dagger held low, feet stay planted",
        "fast scurrying run loop: small feet patter quickly, the body leans low, the tail whips behind",
        "dagger dart: frames 1-3 crouches low and draws the dagger back, frames 4-6 lunges with a fast forward stab, frames 7-10 springs back to standing",
        "hit reaction: the rat flinches back with eyes shut, ears flat, then recovers",
        "death: the dagger drops, the rat spins and flops onto its back, tail limp")),
    "MossArcher": ("small goblin archer wearing a mossy hood and cape, holding a long wooden bow", (
        "idle loop: the goblin breathes, the cape sways a little, the bow is held at the side, feet stay planted",
        "sneaking walk loop: careful steps in turn, the body low, the bow carried at the same size",
        "aimed shot: frames 1-4 raises the bow and slowly draws the string, frames 5-6 releases with a sharp recoil, frames 7-10 lowers the bow to rest",
        "hit reaction: the goblin stumbles back with eyes shut, the bow tilts, then steadies",
        "death: the bow falls, the goblin sinks to its knees and collapses into a heap of mossy cloth")),
    "PowderBeetle": ("round black beetle carrying a large clay powder keg with a short fuse strapped on its back, six stubby legs", (
        "idle loop: the beetle's shell breathes slowly, antennae twitch, the keg stays strapped on, legs stay planted",
        "skittering walk loop: the six stubby legs step quickly, the shell bobs, the keg keeps its size",
        "placing a bomb: frames 1-3 the beetle rears up and tilts forward, frames 4-6 it stomps down hard with the keg shaking, frames 7-10 settles back",
        "hit reaction: the shell jolts, the antennae recoil and the keg shakes, then it settles",
        "death: the beetle flips onto its back, the legs curl and the keg rolls off")),
    # Stage 2: dark teal flooded corridors
    "Drowned": ("bloated drowned sailor with pale teal skin, a torn striped shirt and seaweed on the shoulders, long heavy arms", (
        "idle loop: the drowned body sways slowly, the heavy arms hang, seaweed drips, feet stay planted",
        "slow lurching walk loop: the feet drag in turn, the bloated body rocks side to side",
        "grabbing swipe: frames 1-3 lifts both heavy arms high, frames 4-6 swings them down and forward, frames 7-10 lets them drop back",
        "hit reaction: the bloated body wobbles back with eyes shut, then sways upright",
        "death: the body slumps, sinks down and melts into a flat puddle with the torn shirt")),
    "EelSpitter": ("tall teal eel rising upright out of a small round puddle, with a wide open mouth and small fins", (
        "idle loop: the eel body sways slowly in an S shape above its puddle, fins flutter",
        "gliding loop: the eel slides with its puddle, the body weaves side to side, the same size throughout",
        "spitting: frames 1-3 rears back and inflates its throat, frames 4-6 snaps forward with the mouth wide open, frames 7-10 sways back to rest",
        "hit reaction: the eel recoils with eyes shut and shrinks a little, then rises again",
        "death: the eel droops, sinks into its puddle and the puddle dries to a small dark stain")),
    "CoralGuard": ("stout crab-like guard armored in branching orange coral plates, one huge coral club arm, heavy legs", (
        "idle loop: the coral armored body rises and sinks heavily, the coral club hangs, feet stay planted",
        "heavy stomping walk loop: thick legs step in turn, the armored body sways with weight",
        "club smash: frames 1-3 raises the coral club high, frames 4-6 smashes it straight down in front, frames 7-10 lifts it back to rest",
        "hit reaction: the armor rattles, the body jolts back, then it steadies",
        "death: the coral plates crack and fall away and the body collapses into a heap of coral")),
    "JellyLamp": ("floating round jellyfish with a glowing pale teal bell like a lantern and short trailing tentacles. It has no arms and no legs", (
        "floating idle loop: the bell slowly pulses wider and narrower and hovers up and down, tentacles sway. It has no arms and no legs",
        "drifting loop: the bell pumps and glides, tentacles trail behind. It has no arms and no legs",
        "charging pulse: frames 1-4 the bell shrinks small and tight, frames 5-7 it bursts wide at its largest, frames 8-10 it settles back. It has no arms and no legs",
        "hit reaction: the bell squashes and flickers, tentacles recoil, then it settles. It has no arms and no legs",
        "death: the bell deflates and sags, the tentacles go limp and it sinks into a flat clear blob. It has no arms and no legs")),
    "TideSiren": ("small hooded sea priestess with fins for ears and a shell staff, wearing a teal robe with pearls", (
        "idle loop: the priestess breathes slowly, the robe sways, the shell staff held still, feet stay planted",
        "gliding walk loop: tiny steps under the robe, the body bobs gently, the staff keeps its size",
        "chanting: frames 1-4 raises the shell staff high with both hands, frames 5-7 holds it up singing with the head tilted back, frames 8-10 lowers it to rest",
        "hit reaction: she flinches back with eyes shut, the staff tilts, then she steadies",
        "death: the staff falls, the robe crumples down into an empty heap with scattered pearls")),
    # Stage 3: ink dark deep core, gold seals
    "InkShade": ("thin assassin made of dripping black ink with a pale mask and two small gold blades", (
        "idle loop: the ink body ripples and drips, the mask stays still, blades held low",
        "gliding run loop: the ink body leans far forward and flows, the blades trail, the mask keeps its shape",
        "cross slash: frames 1-3 crosses both blades at the chest, frames 4-6 slashes both outward fast, frames 7-10 settles back",
        "hit reaction: the ink body splashes back and reforms, the mask tilts",
        "death: the blades fall, the ink body collapses into a black puddle with the mask on top")),
    "SealGolem": ("short very wide golem of black stone wrapped with gold seal bands, glowing gold eyes, huge square fists", (
        "idle loop: the heavy stone body rises and sinks slowly, the gold bands stay in place, fists hang, feet stay planted",
        "very heavy stomping walk loop: thick legs lift and stomp in turn, the whole wide body rocks",
        "double fist slam: frames 1-3 raises both huge fists overhead, frames 4-6 slams them down to the floor in front, frames 7-10 lifts up to standing",
        "hit reaction: the stone body jolts and the eyes flicker, then it steadies",
        "death: the gold bands snap, the eyes go dark and the golem crumbles into a heap of black rubble")),
    "GildedBolter": ("small soldier in black armor with gold trim holding a big gold crossbow, tall plumed helmet", (
        "idle loop: the soldier breathes slowly, the plume sways, the crossbow held across the body, feet stay planted",
        "measured marching walk loop: armored legs step in turn, the crossbow carried at the same size",
        "firing the crossbow: frames 1-4 raises and levels the crossbow, frames 5-6 fires with a hard recoil, frames 7-10 lowers it and reloads to rest",
        "hit reaction: the armor jolts, the helmet tips back, then it steadies",
        "death: the crossbow falls, the soldier kneels and topples into a heap of armor")),
    "ChainWarden": ("hunched jailer in a dark hood with a heavy iron mask, holding a long hooked chain coiled in one hand", (
        "idle loop: the jailer breathes heavily, the chain swings slightly, feet stay planted",
        "slow dragging walk loop: heavy feet step in turn, the hunched body sways, the chain keeps its size",
        "throwing the hook: frames 1-3 winds the chain back behind the shoulder, frames 4-6 hurls the arm forward, frames 7-10 hauls the arm back to rest",
        "hit reaction: the jailer flinches back, the mask tilts, the chain rattles, then steadies",
        "death: the chain falls, the jailer sinks and collapses into an empty hood and mask")),
    "SealPriest": ("gaunt priest in a long black and gold robe with a tall gold mitre, holding a staff topped with a glowing gold seal", (
        "idle loop: the priest breathes slowly, the robe sways, the staff held still, feet stay planted",
        "gliding walk loop: tiny steps under the long robe, the body bobs gently, the staff keeps its size",
        "blessing: frames 1-4 raises the seal staff high with both hands, frames 5-7 holds it aloft with the head bowed, frames 8-10 lowers it to rest",
        "hit reaction: the priest flinches back with eyes shut, the staff tilts, then steadies",
        "death: the staff falls, the mitre tips off and the robe crumples into an empty heap")),
}


def text_of(result):
    return " ".join(part.get("text", "") for part in result.get("content", []) if part.get("type") == "text")


def record_path(name):
    folder = os.path.join(ROOT, name)
    os.makedirs(folder, exist_ok=True)
    return os.path.join(folder, "request.json")


def load(name):
    path = record_path(name)
    return json.load(open(path, encoding="utf-8")) if os.path.exists(path) else None


def save(name, record):
    json.dump(record, open(record_path(name), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


def create(api, names):
    anchor = base64.b64encode(open(STYLE_ANCHOR, "rb").read()).decode()
    for index, name in enumerate(names):
        if load(name):
            continue
        description = CAST[name][0] + LOOK
        seed = 2100 + list(CAST).index(name)
        arguments = {"description": description, "name": "mob_" + name, "width": 64, "height": 64, "seed": seed, "view": "low top-down",
                     "style_image": {"base64": anchor, "usage_description": "Match this pixel size, outline and shading"},
                     "style_options": {"detail": True, "shading": True, "outline": True, "color_palette": False}}
        for attempt in range(40):
            result = api.call("create_character_pro_flash", arguments)
            body = text_of(result)
            if not result.get("isError"):
                match = re.search(r"character[_ ]idW+([0-9a-f-]{36})", body, re.I) or re.search(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", body)
                save(name, {"tool": "create_character_pro_flash + animate_character(v3)", "character_id": match.group(0), "seed": seed, "width": 64, "height": 64,
                            "view": "low top-down", "description": description, "style_image": "External/SscaryMale/idle/down/00.png",
                            "style_options": arguments["style_options"], "date": "2026-11-04", "animations": []})
                print(name, "created", match.group(0), flush=True)
                break
            if re.search(r"slot|rate|busy|too many|concurrent|limit", body, re.I):
                api.call("wait_for_jobs", {"min_finished": 1, "timeout_seconds": 90})
                continue
            raise RuntimeError(name + ": " + body[:400])
    wait(api)
    for name in names:
        download(name)


def rotation_urls(api, cid):
    urls = {}
    for line in text_of(api.call("get_character", {"character_id": cid, "include_preview": False})).splitlines():
        match = re.match(r"\s*(south|north|east|west): (https://\S+/rotations/\S+)", line)
        if match:
            urls[match.group(1)] = match.group(2)
    return urls


def animate(api, names):
    for name in names:
        record = load(name)
        done = {(a["name"], a["direction"]) for a in record["animations"]}
        rotations = rotation_urls(api, record["character_id"])
        for (clip, frames, loop), action in zip(CLIPS, CAST[name][1]):
            for direction in DIRECTIONS:
                if (clip, direction) in done:
                    continue
                arguments = {"character_id": record["character_id"], "mode": "v3", "animation_name": clip, "action_description": action + BODY_ONLY,
                             "directions": [direction], "frame_count": frames, "keep_first_frame": True}
                if loop:
                    arguments["end_frame_url"] = rotations[direction]
                for attempt in range(60):
                    result = api.call("animate_character", arguments)
                    body = text_of(result)
                    if not result.get("isError"):
                        print(f"{name}/{clip}/{direction} queued", flush=True)
                        record["animations"].append({"name": clip, "direction": direction, "action_description": arguments["action_description"], "frame_count": frames,
                                                     "loop": loop, "keep_first_frame": True, "end_frame": direction + " rotation" if loop else None, "response": body[:300]})
                        save(name, record)
                        break
                    if re.search(r"slot|rate|busy|too many|concurrent|limit", body, re.I):
                        api.call("wait_for_jobs", {"min_finished": 1, "timeout_seconds": 90})
                        continue
                    raise RuntimeError(f"{name}/{clip}/{direction}: {body[:400]}")
                else:
                    raise RuntimeError(f"{name}/{clip}/{direction}: no slot after retries")
    wait(api)
    for name in names:
        download(name)


def wait(api):
    for _ in range(40):
        body = text_of(api.call("wait_for_jobs", {"min_finished": 50, "timeout_seconds": 80}))
        if re.search(r"nothing else is running|no active jobs|no running", body, re.I):
            return
        time.sleep(1)


def download(name):
    record = load(name)
    headers = {"Authorization": "Bearer " + os.environ["PIXELLAB_API_TOKEN"]}
    for attempt in range(8):
        response = requests.get(f"https://api.pixellab.ai/mcp/characters/{record['character_id']}/download", headers=headers, timeout=180)
        if response.status_code == 200:
            break
        time.sleep(15)
    response.raise_for_status()
    folder = os.path.join(ROOT, name)
    open(os.path.join(folder, "character.zip"), "wb").write(response.content)
    zipfile.ZipFile(io.BytesIO(response.content)).extractall(os.path.join(folder, "download"))
    print(name, "zip", len(response.content), flush=True)


if __name__ == "__main__":
    phase = sys.argv[1]
    chosen = sys.argv[2:] or list(CAST)
    api = PixelLab()
    (create if phase == "create" else animate)(api, chosen)
    print("done", flush=True)
