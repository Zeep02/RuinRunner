"""Contact sheet of unmodified test captures; not a game asset."""
from pathlib import Path
from PIL import Image, ImageDraw
root=Path('Logs/Validation/2026-09-21-wall-facing/004900')
sheet=Image.new('RGB',(1920,512),(20,17,31));draw=ImageDraw.Draw(sheet)
for i,label in enumerate(['OUTER RUINS','FLOODED HALLS','SEAL CORE']):
    shot=Image.open(root/f'stage-{i+1}-corner.png').convert('RGB')
    assert shot.size==(640,480)
    sheet.paste(shot,(i*640,32));draw.text((i*640+16,10),label,fill=(235,230,218))
sheet.save(root/'terrain-review.png')
print(root/'terrain-review.png')
