"""Read-only measurements for active equipment sprites; no pixels are changed."""

from pathlib import Path
from PIL import Image
import json

ROOT = Path(__file__).resolve().parents[2]
EQUIPMENT = ROOT / "Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment"
OUT = ROOT / "ArtDirection/Review-20260930/equipment-pixels.json"


def measure(path: Path) -> dict:
    with Image.open(path) as source:
        image = source.convert("RGBA")
    width, height = image.size
    alpha = image.getchannel("A")
    box = alpha.getbbox()
    if box is None:
        raise ValueError(f"Empty active icon: {path}")
    pixels = image.load()
    edge = []
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a < 128:
                continue
            if any(
                nx < 0 or ny < 0 or nx >= width or ny >= height or pixels[nx, ny][3] < 128
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))
            ):
                edge.append((r, g, b))
    dark = sum(max(rgb) < 95 for rgb in edge)
    return {
        "id": path.stem,
        "kind": path.parent.name,
        "canvas": [width, height],
        "visibleBounds": list(box),
        "visibleSize": [box[2] - box[0], box[3] - box[1]],
        "perimeterPixels": len(edge),
        "darkPerimeterFraction": round(dark / len(edge), 3) if edge else 0,
    }


def main() -> None:
    items = [measure(path) for path in sorted(EQUIPMENT.rglob("*.png"))]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
    for item in items:
        print(f"{item['kind']:10} {item['id']:24} {item['visibleSize']} dark={item['darkPerimeterFraction']:.3f}")


if __name__ == "__main__":
    main()
