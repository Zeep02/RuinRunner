"""Generate native-size four-diagonal animation candidates from the selected hero."""
from pathlib import Path
import argparse
import base64
import hashlib
import json
import re
from pixellab_mcp_client import PixelLab, without_images

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Tools/Art/Staging/PixelLab/20261103-hero-fourdir'
SOURCE = ROOT / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale'
DIRECTIONS = {'SW': ('left_down', 'south-west'), 'SE': ('right_down', 'south-east'),
              'NW': ('left_up', 'north-west'), 'NE': ('right_up', 'north-east')}
MOTIONS = {
    'dagger': ('A swift compact dagger attack: draw the weapon hand back near the waist, push off the rear foot, extend the elbow into a short forward diagonal stab, follow with a tight wrist cut and smoothly recover to ready stance. The free hand balances the body. Small fast arc, not a broad sword swing.', .5),
    'greatsword': ('A powerful two-handed greatsword attack: spread the feet, bend both knees and lift both hands above the shoulder together, transfer body weight into a heavy diagonal downward cleave, lower the hips at impact, follow through and recover. Both hands cooperate on a single grip. Clearly heavier and wider motion than a dagger.', .85),
    'katana': ('A precise katana draw-cut: lower the stance, bring the sword hand to the opposite hip while the other hand steadies the imaginary scabbard, sharply rotate torso and extend the sword arm into one clean sweeping draw-cut, settle the forward foot then return the sword hand to the hip. Controlled fluid motion, no overhead smash.', .65),
    'spear': ('A long two-handed spear thrust: hands initially spaced along an imaginary shaft at waist height, draw both hands back while shifting weight onto the rear leg, step the lead foot forward and extend both arms along the facing diagonal, then smoothly pull the hands back and restore the stance. Long linear thrust, no sword slash or overhead swing.', .65),
    'staff': ('A staff spell cast: one hand holds an imaginary upright staff beside the body, the other hand opens near the chest then sweeps outward, knees flex, raise and point the staff hand forward to release a spell, fingers and torso settle back to neutral. Tall relaxed casting stance, no gun recoil and no melee hit.', .8),
    'orb': ('An orb spell cast: both open hands cradle an imaginary floating orb in front of the waist without gripping it, spread the palms in a small circular gathering gesture, lift the chest, push both open palms forward to release the spell, then bring hands back underneath the orb. Clear open-hand conjuring, distinct from staff and firearm.', .8),
    'gun': ('A controlled firearm shot and recovery: raise the main hand to aim a compact imaginary gun, support it with the other hand, slight forward stance, one sharp wrist and elbow recoil with a small shoulder response, then controlled return to the exact aim and relaxed ready position. No muzzle flash. No sword swing. Keep both hands below the face.', .5),
    'idle': ('A complete seamless calm idle breathing cycle. Feet stay planted, ribcage expands subtly, shoulders and head follow, the backpack responds with a slight delay, then all parts return smoothly. Natural small articulated movement, not a whole-image vertical bob. Keep the same facing.', 1.6),
    'move': ('One complete seamless brisk running cycle in place toward the facing diagonal, with a full left and right step. Clear planted foot weight acceptance, bent knees, passing feet, heel lift and push-off. Torso counterbalances, arms swing naturally, head and backpack follow the stride with subtle delay. Keep the center anchored and the same facing.', .7),
    'dash': ('One low forward dash without rolling: immediate knee compression, powerful foot push-off and forward body lean, legs briefly trail, then lead foot catches weight, knees absorb and the body recovers to neutral. Rapid launch and travel in the first 40 percent, relaxed visual recovery afterward. Do not translate across the canvas.', .4),
    'hurt': ('One nonfatal hit reaction: immediate torso recoil and knee flex, head and backpack follow slightly later, arms catch balance, a foot adjusts and body smoothly returns to neutral. No anticipation, no collapse, no death, no color flash. Keep the same facing and character identity.', .4),
}
SUFFIX = (' Animate the existing character ONLY: preserve the exact original face, hair shape, blue clothes, backpack, proportions and pixel style. '
          'Do not draw any weapon, orb, spell, dust, trail or effect; equipment is rendered separately. '
          'Keep facing fixed, camera fixed, native 48x64 canvas and body scale. '
          'Dense coherent in-between poses, no duplicate padding, no blur or palette flicker. End at the starting ready pose.')

def save(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')

def submit(motion, direction):
    folder = OUT / motion / direction
    receipt = folder / 'request.json'
    if receipt.exists():
        print(f'{motion}/{direction}: already submitted; no duplicate charge')
        return
    source_dir, facing = DIRECTIONS[direction]
    source = SOURCE / 'idle' / source_dir / '00.png'
    source_data = source.read_bytes()
    b64 = base64.b64encode(source_data).decode('ascii')
    desc, duration = MOTIONS[motion]
    description = desc + SUFFIX
    assert len(description) <= 1000, (motion, len(description))
    args = {'first_frame_base64': b64, 'last_frame_base64': b64,
            'description': description, 'frame_count': 32,
            'no_background': True, 'seed': 113000 + list(MOTIONS).index(motion) * 4 + list(DIRECTIONS).index(direction),
            'direction': facing, 'view': 'low top-down', 'enhance_prompt': False,
            'subject_description': 'The existing small dark-haired human adventurer in blue clothes and boots with a red-brown backpack and two normal arms and hands',
            'initial_pose': 'Standing upright at rest, feet grounded, arms relaxed, facing ' + facing}
    result = PixelLab().call('animate_image_pixminimax', args)
    record_args = {k: v for k, v in args.items() if not k.endswith('_base64')}
    record = {'tool': 'animate_image_pixminimax', 'source': source.relative_to(ROOT).as_posix(),
              'source_sha256': hashlib.sha256(source_data).hexdigest(), 'parameters': record_args,
              'first_frame': 'source PNG bytes', 'last_frame': 'same source PNG bytes',
              'duration_seconds': duration, 'response': without_images(result)}
    save(receipt, record)
    print(json.dumps({'clip': motion + '/' + direction, 'response': without_images(result)}, ensure_ascii=False))

def poll(motion, direction):
    folder = OUT / motion / direction
    record = json.loads((folder / 'request.json').read_text(encoding='utf-8'))
    response = record['response']
    text = '\n'.join(x.get('text','') for x in response.get('content', []))
    match = re.search(r'(?:job_id|job id|Job ID)\s*[":= ]+([a-zA-Z0-9-]+)', text, re.I)
    if not match:
        raise ValueError('No job ID in receipt: ' + text[:1000])
    job = match.group(1)
    result = PixelLab().call('get_image', {'job_id': job})
    save(folder / 'status.json', without_images(result))
    print(json.dumps({'clip': motion + '/' + direction, 'response': without_images(result)}, ensure_ascii=False))

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('operation', choices=['submit', 'poll'])
    parser.add_argument('motion', choices=MOTIONS)
    parser.add_argument('direction', choices=DIRECTIONS)
    args = parser.parse_args()
    (submit if args.operation == 'submit' else poll)(args.motion, args.direction)
