"""Read-only native PNG QA and license notices. Never alters asset pixels or metadata."""
from pathlib import Path
import hashlib,json,html,re
from PIL import Image
root=Path(__file__).resolve().parents[2]
review=root/'ArtDirection/Review-20261001/thrust-fx'
manifest=json.loads((review/'native-import.json').read_text(encoding='utf-8'))
visible={};clips={}
for r in manifest['images']:
    source=Image.open(root/r['source']).convert('RGBA')
    x,y,w,h=r['sourceRect'];original=source.crop((x,y,x+w,y+h))
    actual=Image.open(root/r['path']).convert('RGBA')
    assert actual.size==(32,32) and actual.tobytes()==original.tobytes()
    assert hashlib.sha256((root/r['source']).read_bytes()).hexdigest()==r['sourceSha256']
    assert hashlib.sha256((root/r['path']).read_bytes()).hexdigest()==r['fileSha256']
    raw=actual.tobytes();bbox=actual.getbbox();clips.setdefault(r['clip'],[]).append(dict(key=r['frame'],alphaBounds=bbox))
    if bbox:visible.setdefault(r['clip'],set()).add(hashlib.sha256(raw).hexdigest())
assert len(visible['LongswordStab'])==6 and len(visible['DaggerStab'])==5
assert clips['LongswordStab'][2]['alphaBounds']==(9,11,32,21)
assert clips['DaggerStab'][1]['alphaBounds']==(2,12,27,20)
qa=dict(result='PASS',exactOriginalCells=len(manifest['images']),uniqueVisiblePoses={k:len(v) for k,v in visible.items()},clips=clips,scope='Independent exact original PNG RGBA/native canvas/contact-alpha checks. No newly generated poses, image resizing or motion/performance claims.')
(review/'native-png-qa.json').write_text(json.dumps(qa,indent=2),encoding='utf-8')
notice=root/'Assets/StreamingAssets/ThirdPartyNotices';notice.mkdir(parents=True,exist_ok=True)
shear=(root/'ArtDirection/ExternalCandidates/20260921/shearline/original/SHEARLINE_Free_Sampler/LICENSE.txt').read_text(encoding='utf-8')
(notice/'SHEARLINE-Thrust.txt').write_text('Source: https://godboyhappy.itch.io/shearline-pixel-melee-slash-vfx\nUsed: sheets_32/thrust-stab.png, eight exact native cells (six visible poses). No downloaded engine code is executed.\n\n'+shear,encoding='utf-8')
page=(root/'ArtDirection/ExternalCandidates/20261001/icemaan-vfx/official-page.html').read_text(encoding='utf-8')
terms=page.split('<h3>License</h3>',1)[1].split('</ul>',1)[0]
terms=html.unescape(re.sub('<[^>]+>',' ',terms))
terms=re.sub(r'\s+',' ',terms).strip()
assert 'personal or commercial' in terms and 'AI training set' in terms
(notice/'IceMaan-VFX-Essentials.txt').write_text('FREE PIXEL VFX ESSENTIALS — IceMaan\nSource: https://icemaan.itch.io/pixel-vfx-essentials\nUsed: Spritesheets/Slashes/Thrust.png, five exact native 32x32 cells; no repainting or resampling.\nOfficial license text retrieved 2026-10-01:\n'+terms+'\n\nFull source page retained in ArtDirection/ExternalCandidates/20261001/icemaan-vfx/official-page.html. These effects are shipped inside the game, not distributed as an asset pack.\n',encoding='utf-8')
print(json.dumps({k:v for k,v in qa.items() if k!='clips'}))
