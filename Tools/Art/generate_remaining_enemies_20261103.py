"""Queue PixelLab animations for the last enemies still on old art, then download them.

Every looping or returning clip interpolates from the character's rotation back to it, so all clips
share one stance and can follow one another without a jump. Finished requests are recorded in
Tools/Art/Staging/PixelLab/20261103-remaining/<Name>/request.json and are never queued twice.

Run with PIXELLAB_API_TOKEN set:  python Tools/Art/generate_remaining_enemies_20261103.py [Name ...]
"""
import io
import json
import os
import re
import sys
import time
import zipfile

import requests

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixellab_mcp_client import PixelLab

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Staging", "PixelLab", "20261103-remaining")
STYLE_ANCHOR = "External/SscaryMale/idle/down/00.png"
THREE = ["south", "north", "east"]
SOUTH = ["south"]
# The game plays pixelfx effects over these. Generated sparks or blobs would double up and read as part of the body.
BODY_ONLY = " Only the character's own body moves and it keeps its exact design. No projectiles, sparks, smoke, light bursts, motion lines or other effects, and nothing new grows on it."


def clip(name, action, frames, loop=True, directions=None):
    # loop=True returns to the rotation pose. loop=False ends wherever the motion ends (deaths).
    return {"name": name, "action": action, "frames": frames, "loop": loop, "directions": directions or THREE}


def minion(idle, walk, attack, hurt, death, idle_n, walk_n, attack_n, hurt_n, death_n):
    return [clip("rr_idle", idle, idle_n), clip("rr_walk", walk, walk_n), clip("rr_attack", attack, attack_n),
            clip("rr_hurt", hurt, hurt_n), clip("rr_death", death, death_n, loop=False)]


def second_pass(idle, walk, attack, hurt, death):
    return [clip("q_idle", idle, 6), clip("q_walk", walk, 8), clip("q_attack", attack, 10), clip("q_hurt", hurt, 6), clip("q_death", death, 10, loop=False)]


def boss(*clips):
    return [clip(n, a, f, loop=l, directions=SOUTH) for n, a, f, l in clips]


CAST = {
    "Shooter": {"id": "786c14c5-9a1d-48bc-ac11-61a48a7c82a2", "seed": 1111, "size": 64, "clips": minion(
        "idle breathing loop: the round pot body gently rises and sinks, the chipped rim wobbles a little, feet stay planted",
        "waddling walk loop: stubby feet step alternately, the round pot body rocks side to side",
        "spitting shots: frames 1-3 leans back and inhales so the round pot swells a little, frames 4-6 snaps forward in a sharp recoil with the small tube mouth on its face wide open, frames 7-8 settles back to standing",
        "hit reaction: the pot rattles and tilts backward sharply, eyes squeeze shut, then it wobbles back to standing",
        "death: cracks spread over the pot, the eyes go dark, pieces fall away and it breaks down into a small low pile of terracotta shards on the floor",
        6, 8, 8, 6, 10)},
    "Leaper": {"id": "2eae9233-b626-4c9f-abcd-388591154ed2", "seed": 1112, "size": 64, "clips": minion(
        "idle breathing loop: the throat puffs in and out, the plump body gently squashes, feet stay planted",
        "short hopping walk loop: the springy hind legs push, the body makes small low hops and lands softly",
        "leap slam staying centered in the canvas: frames 1-3 squats deep and compresses flat, frames 4-6 stretches and tucks its legs into a round ball as if in mid air, frames 7-10 lands in a wide flat splat then springs back to standing",
        "hit reaction: squashes flat with eyes shut, jiggles, then springs back to standing",
        "death: flips onto its back, legs twitch and curl, the stone plate slides off and the body deflates into a flat puddle",
        6, 8, 10, 6, 10)},
    "Tidecaller": {"id": "a14e9425-d028-4eb3-8fbc-03ac2abcfe29", "seed": 1117, "size": 64, "clips": minion(
        "idle loop: the plump robed body sways gently, fish eyes blink, conch held in both hands",
        "shuffling walk loop: tiny feet patter under the short robe, the body bobs side to side",
        "calling the tide: frames 1-4 lifts the conch high and swells up taking a deep breath, frames 5-7 thrusts the conch forward blowing it hard with the whole body leaning in, frames 8-12 lowers the conch and settles back to standing",
        "hit reaction: flinches backward with eyes shut, then steadies",
        "death: wobbles and drops the conch, the fish priest slumps down and melts into a foamy puddle with an empty robe",
        4, 6, 12, 4, 10)},
    "Sentinel": {"id": "c67d4823-d3e7-43a0-b300-0f6c29bdb28a", "seed": 1114, "size": 64, "clips": minion(
        "idle loop: stands guard, the round bronze body breathes slowly, the visor glow pulses, glaive held still",
        "heavy marching walk loop: short thick legs stomp in turn, the round body sways with weight, glaive carried steady",
        "reach cut: frames 1-4 coils the body and draws the glaive far back with both hands, frames 5-7 whips the glaive through a fast wide forward arc, frames 8-12 lets the swing carry through and hauls it back to its guard stance",
        "hit reaction: bronze body rings and jolts backward, visor flickers, then steadies",
        "death: the visor glow dies, the glaive falls, gears spill out and the empty bronze shell tips over into a heap",
        4, 6, 12, 4, 10)},
    "Warden": {"id": "522d4fe2-17c1-4413-aab2-845511f7341a", "seed": 1203, "size": 112, "clips": boss(
        ("wd_idle", "idle breathing loop: the huge stone chest slowly rises and sinks, the shoulders lift a little, both boulder fists hang and sway very slightly, the ember core pulses, feet stay planted", 16, True),
        ("wd_walk2", "heavy stomping walk in place, seen from the front: one thick leg lifts a little and stomps down while the whole body leans to that side, then the other leg. Both huge boulder fists stay the same size, out at its sides, swaying gently in every frame, the head and chest keep their exact shape", 16, True),
        ("wd_pound", "double fist slam: frames 1-6 raises both huge boulder fists high above its head and leans back, frames 7-12 smashes both fists straight down to the floor in front of its feet with the body bent forward, frames 13-16 straightens up and lowers its arms back to standing", 16, True),
        ("wd_punch", "huge straight punch: frames 1-6 twists the body and pulls one boulder fist far back behind the shoulder, frames 7-12 lunges and throws that fist straight forward toward the viewer at full arm length, frames 13-16 pulls the arm back to standing", 16, True),
        ("wd_charge", "bull rush staying centered in the canvas: frames 1-6 crouches low, lowers its head and tucks its fists in, frames 7-12 throws its shoulders and head forward in a running lunge with legs driving, frames 13-16 brakes and rises back to standing", 16, True),
        ("wd_leap", "leap slam staying centered in the canvas: frames 1-6 squats deep with fists pulled up, frames 7-12 stretches upward with both legs tucked as if in mid air and fists held overhead, then lands in a wide low stance with both fists on the floor, frames 13-16 rises back to standing", 16, True),
        ("wd_hit", "stagger: frames 1-4 the whole body jolts backward with the eyes squeezed shut and fists flung apart, frames 5-12 sways dazed with heavy shoulders drooping and the core flickering, frames 13-16 steadies back to standing", 16, True),
        ("wd_death", "death: the ember core flickers and goes dark, the eyes go out, cracks spread, the arms fall off, the body collapses and crumbles down into a low heap of mossy boulders on the floor", 16, False))},
    # Second pass over the first cast. Their old clips each began and ended on a different drawing, so faces and limbs jumped between actions.
    "Grunt": {"id": "7bf3b852-fdb2-4f4b-b4b3-6e4209b698f4", "seed": 1101, "size": 64, "clips": second_pass(
        "idle breathing loop: the round mossy stone body gently squashes and rises, feet stay planted, eyes stay open",
        "waddling walk loop: stubby feet step alternately, the round body rocks side to side",
        "body slam staying centered in the canvas: frames 1-3 squashes down low and leans back, frames 4-6 stretches forward in a hard headbutt, frames 7-10 rocks back and settles to standing",
        "hit reaction: the stone body jolts backward with eyes squeezed shut, wobbles, then returns to standing",
        "death: cracks spread over the stone body, the eyes go dark, moss and chunks fall away and it crumbles into a low heap of rubble")},
    "Charger": {"id": "67ff2133-b9d6-479c-a6a6-8311b9b96a4a", "seed": 1102, "size": 64, "clips": [
        clip("q_idle", "idle breathing loop: the heavy boar body slowly rises and sinks, ears flick, feet stay planted" , 4),
        clip("q_run", "heavy trotting loop: the four stubby legs step in turn, the body bobs, tusks stay the same size", 8),
        clip("q_attack", "tusk swipe: frames 1-2 lowers the head and pulls back, frames 3-6 swings both tusks in a wide upward sweep, frames 7-8 settles back to standing", 8),
        clip("q_run_attack", "charging run staying centered in the canvas: frames 1-2 digs in with the head lowered, frames 3-6 legs gallop hard with tusks thrust forward, frames 7-8 brakes back to standing", 8),
        clip("q_hurt", "hit reaction: the boar jolts backward with eyes shut, shakes its head, then steadies", 6),
        clip("q_death", "death: the boar staggers, topples onto its side, the legs kick once and go still", 8, loop=False)]},
    "Sporeling": {"id": "27d333ab-ff0f-45bc-88ae-1411f584bec7", "seed": 1103, "size": 64, "clips": second_pass(
        "idle breathing loop: the teal mushroom cap gently bobs and squashes, the small stem body breathes, eyes stay open",
        "tiny waddling walk loop: short stubby feet patter, the cap bobs side to side",
        "about to burst: frames 1-3 the cap swells and trembles with the eyes wide, frames 4-6 the cap puffs out at its biggest, frames 7-10 it shrinks back to standing",
        "hit reaction: the cap squashes down with eyes shut, jiggles, then springs back to standing",
        "death: the cap wilts and droops, the stem buckles, and it collapses into a small flat pile with the limp cap on top")},
    "RuneTotem": {"id": "ca286333-e5c4-4cb4-a044-0ffae75d73fd", "seed": 1104, "size": 64, "clips": second_pass(
        "idle loop: the stone totem stands still, its floating stone chips slowly circle it, the amber rune pulses softly",
        "idle loop: the stone totem stands still, its floating stone chips slowly circle it a little faster, the amber rune pulses",
        "casting: frames 1-3 the floating chips pull in tight and the amber rune brightens, frames 4-6 the chips fling outward with the rune at its brightest, frames 7-10 the chips drift back to their places",
        "hit reaction: the totem rocks backward, the floating chips scatter a little and the rune flickers, then everything settles back",
        "death: cracks spread, the rune goes dark, the floating chips drop, the upper stones topple and it breaks into a low pile of rubble")},
    "TideCrab": {"id": "7404457f-a64a-4a1a-9681-09d563f22ad7", "seed": 1105, "size": 64, "clips": second_pass(
        "idle loop: the crab's round shell gently rises and sinks, the two claws open and close slowly, eye stalks sway",
        "sideways scuttling loop: the small legs step quickly, the shell bobs, both claws stay raised at the same size",
        "claw sweep: frames 1-3 raises both claws high and spreads wide, frames 4-6 sweeps both claws fast and low across the front, frames 7-10 brings them back to rest",
        "hit reaction: the shell jolts back, the claws fly apart and the eye stalks recoil, then it settles",
        "death: the crab flips onto its back, the legs curl up and twitch, the claws fall limp and it goes still")},
    "Wisp": {"id": "83c6c968-acf4-4ea1-938a-631918fb4991", "seed": 1106, "size": 64, "clips": second_pass(
        "floating idle loop: the pale blue flame body hovers up and down, the flame tip sways, the small sparks around it drift, the dark round eyes keep their exact shape. It has no arms and no legs",
        "drifting loop: the flame body leans and glides with its tail trailing, the small sparks drift, the dark round eyes keep their exact shape. It has no arms and no legs",
        "firing: frames 1-3 the flame body pulls in small and low, frames 4-6 it flares taller and leans forward with the sparks pushed out, frames 7-10 it settles back to hovering. It has no arms and no legs and the eyes keep their shape",
        "hit reaction: the flame body flickers and leans back with its eyes shut, then steadies. It has no arms and no legs",
        "death: the flame body shrinks and flickers, the sparks go out, and it fades away into a tiny curl of smoke. It has no arms and no legs")},
    "SealKnight": {"id": "793f146f-5de4-40b5-9ca8-03d759d30abd", "seed": 1107, "size": 64, "clips": second_pass(
        "idle loop: the armoured knight breathes slowly, the sword is held still at the side, feet stay planted",
        "heavy marching walk loop: armoured legs step in turn, the body sways, the sword is carried steady at the same size",
        "great sword swing: frames 1-3 raises the sword high over the shoulder with both hands, frames 4-6 slashes down and across at full reach leaning into it, frames 7-10 hauls the sword back to its guard stance",
        "hit reaction: the armour jolts back and the helmet tips, then the knight steadies back to standing",
        "death: the sword drops, the knight sinks to its knees and topples forward into a low heap of armour")},
    "Hexer": {"id": "06d00e9a-417d-4ca2-a832-92a10290390e", "seed": 1108, "size": 64, "clips": second_pass(
        "idle loop: the hooded caster breathes slowly, the robe sways a little, the staff stays still, the purple gem floating over the staff bobs",
        "shuffling walk loop: tiny feet patter under the robe, the body bobs, the staff keeps its size, the purple gem floats with it",
        "casting: frames 1-3 lifts the staff high with both hands, frames 4-6 thrusts the staff forward leaning in, frames 7-10 lowers the staff back to standing",
        "hit reaction: the hooded caster flinches back with eyes shut, the staff tilts, then steadies back to standing",
        "death: the staff falls, the gem goes out, the robe crumples down and collapses into an empty heap of cloth")},
    "Tidekeeper": {"id": "896d80bc-6050-46d1-b110-8d53d7aa7f49", "seed": 1115, "size": 112, "clips": boss(
        ("tk_idle", "hovering loop: the whole floating stone body rises and sinks slowly, both detached fists drift gently up and down with a small delay, the chest core pulses softly, water drips from the base", 8, True),
        ("tk_lanes", "summoning tide lanes: frames 1-7 slowly raises both floating fists high to the sides and the chest core brightens more and more, frames 8-10 slams both fists forward and down with the core flaring, frames 11-16 fists drift back to rest and the glow calms down", 16, True),
        ("tk_pools", "summoning water pools: frames 1-7 pulls both floating fists in close to the chest and hunches as the core swells bright, frames 8-10 throws both fists wide open with the body rearing up and the core blazing, frames 11-16 fists drift back to rest and the glow calms down", 16, True),
        ("tk_hurt", "hit reaction: the floating body jolts backward and tilts, fists scatter a little outward, the core flickers, then everything drifts back to place", 8, True),
        ("tk_death", "death: the core flickers and goes dark, cracks spread, the head and both fists drop, the floating body falls to the floor and breaks into a heap of barnacled rubble", 14, False))},
    "SealHeart": {"id": "6b3bc634-bb43-4593-a89f-817856cbe4f8", "seed": 1116, "size": 112, "clips": boss(
        ("sh_idle", "idle loop: the heart slowly beats, swelling and shrinking, the crown ring and the four keystones bob gently around it, the eye and teal cracks pulse", 8, True),
        ("sh_move", "gliding loop: the heart leans and slides with a slow heavy throb, the keystones and crown trail behind and catch up", 8, True),
        ("sh_cast", "cross seal cast: frames 1-7 the heart swells and the four keystones pull in tight as the eye and cracks brighten, frames 8-11 the keystones fling outward and the crown jumps up with a blazing flash, frames 12-16 everything settles back into place", 16, True),
        ("sh_sweep", "sweeping cast: frames 1-7 the keystones spiral to one side and the heart leans the other way winding up, frames 8-11 the keystones whip around the heart in a fast sweep, frames 12-16 they slow and return to their places", 16, True),
        ("sh_charge", "charging up then rushing: frames 1-7 the heart compresses down low and the keystones lock tightly around it, frames 8-11 it stretches forward with crown and stones blown back, frames 12-16 it brakes, wobbles and returns to rest", 16, True),
        ("sh_hurt", "hit reaction: the heart squashes and flinches, the eye squeezes shut, crown and keystones are knocked askew, then all drift back into place", 8, True),
        ("sh_death", "death: the seal bands snap, the crown falls, the keystones drop one by one, the eye goes dark and the heart cracks and crumbles into a heap of ink stone rubble", 16, False))},
}


def text_of(result):
    return " ".join(part.get("text", "") for part in result.get("content", []) if part.get("type") == "text")


def rotation_urls(api, cid):
    urls = {}
    body = text_of(api.call("get_character", {"character_id": cid, "include_preview": False}))
    for line in body.splitlines():
        m = re.match(r"\s*(south|north|east|west): (https://\S+/rotations/\S+)", line)
        if m:
            urls[m.group(1)] = m.group(2)
    return urls


def queue_all(api, names):
    for name in names:
        who = CAST[name]
        folder = os.path.join(ROOT, name)
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, "request.json")
        record = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {
            "tool": "create_character_pro_flash + animate_character(v3)", "character_id": who["id"], "seed": who["seed"],
            "width": who["size"], "height": who["size"], "view": "low top-down",
            "style_image": STYLE_ANCHOR, "style_options": {"detail": True, "shading": True, "outline": True, "color_palette": False},
            "date": "2026-11-03", "animations": []}
        done = {(a["name"], a["direction"]) for a in record["animations"]}
        rotations = rotation_urls(api, who["id"])
        for item in who["clips"]:
            for direction in item["directions"]:
                if (item["name"], direction) in done:
                    continue
                arguments = {"character_id": who["id"], "mode": "v3", "animation_name": item["name"],
                             "action_description": item["action"] + BODY_ONLY, "directions": [direction],
                             "frame_count": item["frames"], "keep_first_frame": True}
                if item["loop"]:
                    arguments["end_frame_url"] = rotations[direction]
                for attempt in range(40):
                    result = api.call("animate_character", arguments)
                    body = text_of(result)
                    if not result.get("isError"):
                        print(f"{name}/{item['name']}/{direction} queued: {body[:110]!r}", flush=True)
                        record["animations"].append({"name": item["name"], "direction": direction,
                                                     "action_description": arguments["action_description"],
                                                     "frame_count": item["frames"], "loop": item["loop"],
                                                     "keep_first_frame": True,
                                                     "end_frame": direction + " rotation" if item["loop"] else None,
                                                     "response": body[:400]})
                        json.dump(record, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
                        break
                    if re.search(r"slot|rate|busy|too many|concurrent|limit", body, re.I):
                        api.call("wait_for_jobs", {"min_finished": 1, "timeout_seconds": 90})
                        continue
                    raise RuntimeError(f"{name}/{item['name']}/{direction}: {body[:400]}")
                else:
                    raise RuntimeError(f"{name}/{item['name']}/{direction}: no slot after retries")


def download(names):
    headers = {"Authorization": "Bearer " + os.environ["PIXELLAB_API_TOKEN"]}
    for name in names:
        response = requests.get(f"https://api.pixellab.ai/mcp/characters/{CAST[name]['id']}/download", headers=headers, timeout=180)
        response.raise_for_status()
        folder = os.path.join(ROOT, name)
        open(os.path.join(folder, "character.zip"), "wb").write(response.content)
        zipfile.ZipFile(io.BytesIO(response.content)).extractall(os.path.join(folder, "download"))
        print(name, "zip", len(response.content), flush=True)


if __name__ == "__main__":
    names = sys.argv[1:] or list(CAST)
    api = PixelLab()
    queue_all(api, names)
    for _ in range(30):
        body = text_of(api.call("wait_for_jobs", {"min_finished": 50, "timeout_seconds": 80}))
        if re.search(r"nothing else is running|no active jobs|no running", body, re.I):
            break
        time.sleep(1)
    download(names)
    print("done", flush=True)
