"""PixelLab 장애물 그림을 받아 Campaign/Landmark/<Theme>/<가로>x<세로>-<n>.png 로 설치한다.

기존 소품(Campaign/Dressing)과 같은 create_1_direction_object 묶음 생성에, 각 테마의 기존 그림에서 뽑은 팔레트(palettes.json)를
주어 화풍을 맞췄다. 원본은 Tools/Art/Staging/PixelLab/20261103-landmarks 에 그대로 둔다. 설치본은 투명 여백만 잘라 내고,
바닥 중앙 기준점을 쓰는 스프라이트 설정을 함께 쓴다. 크기 키는 장애물이 막는 칸 수이고, 그림 폭이 그 칸에 맞는 것만 골랐다(StageLandmarks).
"""
import json, os, re, urllib.request, uuid
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STAGING = os.path.join(ROOT, "Tools", "Art", "Staging", "PixelLab", "20261103-landmarks")
ART = os.path.join(ROOT, "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated", "Campaign")
META = os.path.join(ART, "Dressing", "Core", "stand-pot.png.meta")
URL = "https://backblaze.pixellab.ai/file/pixellab-characters/objects/5ba0435a-6d49-45f7-aa17-9ce34a6bd358/%s/rotations/frame_%d.png"

# 묶음 하나가 후보 여럿을 준다. 보상 상자와 헷갈리는 후보는 고르지 않았다.
SETS = {
    "Outer": {
        "4f82674f-9b5d-4ebc-9c6b-906a83c78646": {"2x2-0": 3},
        "352f55c4-de8b-4105-bf72-82890e34cdce": {"2x2-1": 2, "2x2-2": 3},
        "abb07dc5-ae67-40d5-a7fd-625586db9c70": {"3x3-0": 0, "3x3-1": 2},
        "401e0593-4844-46ca-a629-c25ab8410a5e": {"5x2-0": 0, "2x4-0": 1, "4x4-0": 2, "4x4-1": 3},
    },
    "Flooded": {
        "3fa699bb-4c6a-4a16-ab6b-bae4ead47216": {"2x2-0": 3, "2x2-1": 5},
        "20cf84dc-0195-499a-9dfd-3ed19f5e8e56": {"2x2-2": 1, "3x3-0": 2, "3x3-1": 3},
        "976bfb67-ec65-42d3-9876-cd32c8a1bbe4": {"5x2-0": 0, "2x4-0": 1, "4x4-0": 2, "4x4-1": 3},
    },
    "Core": {
        "1d5d0c7a-4002-494c-bc90-3e90b9c4e4fa": {"2x2-0": 13},
        "a067b8f4-15f9-4d9e-9284-e2396fccb1d9": {"2x2-1": 1, "2x2-2": 3},
        "119027d6-4712-431e-b597-faee986b48ec": {"3x3-0": 0, "3x3-1": 1},
        "c677a0c7-46e8-4377-9b69-8ec66dbcf644": {"5x2-0": 0, "2x4-0": 1, "4x4-0": 2, "4x4-1": 3},
    },
}


def meta_for(guid):
    text = open(META, encoding="utf8").read()
    text = re.sub(r"^guid: .*$", "guid: " + guid, text, flags=re.M)
    text = re.sub(r"^  alignment: \d+$", "  alignment: 7", text, count=1, flags=re.M)
    text = re.sub(r"^  spritePivot: .*$", "  spritePivot: {x: 0.5, y: 0}", text, count=1, flags=re.M)
    return text


os.makedirs(STAGING, exist_ok=True)
record = []
for theme, sets in SETS.items():
    folder = os.path.join(ART, "Landmark", theme)
    os.makedirs(folder, exist_ok=True)
    for object_id, picks in sets.items():
        for key, frame in picks.items():
            original = os.path.join(STAGING, f"set-{object_id[:8]}-{frame}.png")
            if not os.path.exists(original):
                request = urllib.request.Request(URL % (object_id, frame), headers={"User-Agent": "Mozilla/5.0"})
                open(original, "wb").write(urllib.request.urlopen(request, timeout=60).read())
            image = Image.open(original).convert("RGBA")
            cropped = image.crop(image.getchannel("A").getbbox())
            target = os.path.join(folder, key + ".png")
            cropped.save(target)
            if not os.path.exists(target + ".meta"):
                open(target + ".meta", "w", encoding="utf8", newline="\n").write(meta_for(uuid.uuid4().hex))
            print(f"{theme} {key}: {cropped.size[0]}x{cropped.size[1]} (footprint width {int(key.split('x')[0]) * 32})")
            record.append({"theme": theme, "key": key, "pixellab_object": object_id, "frame": frame})
json.dump(record, open(os.path.join(STAGING, "request.json"), "w", encoding="utf8"), ensure_ascii=False, indent=1)
