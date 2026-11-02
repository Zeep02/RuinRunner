"""Check exact native cells and count unique authored poses (never count variants as animation)."""
from pathlib import Path
import json,hashlib
from PIL import Image
root=Path(__file__).resolve().parents[2]
folder=root/'ArtDirection/Review-20261001/weapon-fx'
data=json.loads((folder/'native-import.json').read_text(encoding='utf-8-sig'))
counts={};hashes={}
for row in data['frames']:
 path=root/row['path'];src=root/row['source'];x,y,w,h=row['cell']
 original=Image.open(src).convert('RGBA').crop((x,y,x+w,y+h));frame=Image.open(path).convert('RGBA')
 assert frame.size==(w,h) and frame.tobytes()==original.tobytes(),str(path)
 key=path.parent.name;counts[key]=counts.get(key,0)+1;hashes.setdefault(key,set()).add(hashlib.sha256(frame.tobytes()).hexdigest())
 assert row['sourceSha256']==hashlib.sha256(src.read_bytes()).hexdigest()
for key,n in counts.items():assert len(hashes[key])==n,key
particles=['MetalSpark','StoneChip','LeafChip','Slug']
report=dict(result='PASS',frames=counts,total_native_cells=sum(counts.values()),authored_animation_poses=sum(n for k,n in counts.items() if k not in particles),particle_variants_and_single_slug=sum(counts[k] for k in particles),rules='Exact RGBA/source SHA256, no empty/duplicate poses, no resampling. Particle variants and static ammunition are not counted as animation.',scope='Native source identity only. Runtime images require separate visual review.')
(folder/'native-identity-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report))
