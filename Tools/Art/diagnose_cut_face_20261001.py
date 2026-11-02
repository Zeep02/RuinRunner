"""Read-only mismatch colors and native source contact; never edits gameplay assets."""
from pathlib import Path
from PIL import Image,ImageDraw
import csv,json,numpy as np,sys
root=Path(__file__).resolve().parents[2]
folder=root/(sys.argv[1] if len(sys.argv)>1 else 'Logs/Validation/2026-10-01-greatsword-fx/presentation-163650')
out=root/'ArtDirection/Review-20261001/weapon-identity-more'/('diagnosis-'+folder.name);out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
report=[]
if len(sys.argv)>2:frames=[int(f) for f in sys.argv[2:]]
else:frames=(657,723,778,555)
for frame in frames:
    r=next(r for r in rows if int(r['frame'])==frame)
    source=np.asarray(Image.open(root/r['bodyAsset']).convert('RGBA'))
    screen=np.asarray(Image.open(folder/'frames'/f'frame-{frame:06d}.png').convert('RGB'))
    sy,sx=np.nonzero(source[:33,:,3]==255)
    x=np.floor(float(r['originSX'])+(sx+.5)*float(r['scaleX'])).astype(int)
    y=np.floor(screen.shape[0]-float(r['originSY'])+(sy+.5)*float(r['scaleY'])).astype(int)
    bad=np.max(abs(screen[y,x].astype(int)-source[sy,sx,:3].astype(int)),axis=1)>3
    comparisons=[]
    for xx,yy,ssx,ssy in zip(x[bad],y[bad],sx[bad],sy[bad]):
        comparisons.append(dict(sourceXY=[int(ssx),int(ssy)],screenXY=[int(xx),int(yy)],expected=source[ssy,ssx,:3].tolist(),actual=screen[yy,xx].tolist()))
    offsets=[]
    for dy in range(-8,9):
        for dx in range(-8,9):
            wrong=int(np.count_nonzero(np.max(abs(screen[y+dy,x+dx].astype(int)-source[sy,sx,:3].astype(int)),axis=1)>3))
            offsets.append((wrong,dx,dy))
    palette={tuple(p) for p in source[:,:,:3].reshape(-1,3)}
    unfamiliar=[c for c in comparisons if tuple(c['actual']) not in palette]
    report.append(dict(frame=frame,telemetry=r,mismatches=comparisons,diagnosticOffsets=sorted(offsets)[:5],unfamiliar=unfamiliar))
    image=Image.fromarray(screen);left=int(float(r['originSX']));top=int(720-float(r['originSY']))
    image.crop((left,top,left+96,top+128)).save(out/f'frame-{frame}-native-body.png')
    Image.fromarray(source).save(out/f'frame-{frame}-source.png')
(out/'mismatch-colors.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
source=Image.open(root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/GreatswordCut/04.png').convert('RGBA')
canvas=Image.new('RGBA',source.size,(32,28,45,255));canvas.alpha_composite(source)
ImageDraw.Draw(canvas).line((0,64,127,64),fill=(127,85,85),width=1);ImageDraw.Draw(canvas).line((64,0,64,127),fill=(127,85,85),width=1)
canvas.convert('RGB').save(out/'source-cut4-native.png')
print(json.dumps([dict(frame=r['frame'],mismatchCount=len(r['mismatches']),diagnosticOffsets=r['diagnosticOffsets'],unfamiliar=r['unfamiliar'][:6]) for r in report]))
matches=[]
for p in (root/'Assets/RuinRunner/Resources/Art/Pixel/Separated').rglob('*.png'):
    if 'SscaryMaleCombat' in str(p):continue
    a=np.asarray(Image.open(p).convert('RGBA'))
    if np.any(np.all(a[:,:,:3]==(210,159,112),axis=2)&(a[:,:,3]>0)):matches.append(str(p.relative_to(root)))
print(json.dumps({'overlayColorSources':matches[:35],'count':len(matches)}))
weapon=np.asarray(Image.open(root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Weapon/iron_greatsword.png').convert('RGBA'))
y,x=np.nonzero(np.all(weapon[:,:,:3]==(210,159,112),axis=2)&(weapon[:,:,3]>0))
print(json.dumps({'weaponSize':list(weapon.shape),'woodCoords':list(zip(x.tolist(),y.tolist()))}))
