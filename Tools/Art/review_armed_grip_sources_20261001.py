"""Read-only pixel invariants for the revised native joint-code output."""
from pathlib import Path
import json,sys,numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[2];version=sys.argv[1]
data=json.loads((root/f'ArtDirection/Generated/SscaryMotion-20261001/{version}/manifest.json').read_text(encoding='utf-8'))
cache={};head=feet=0;bad=[]
for r in data['frames']:
    for key in ('source','path'):
        p=r[key]
        if p not in cache:cache[p]=np.asarray(Image.open(root/p).convert('RGBA'))
    source,new=cache[r['source']],cache[r['path']]
    assert new.shape==(64,48,4)
    assert np.all((new[:,:,3]==0)|(new[:,:,3]==255))
    mask=source[:33,:,3]>0
    h=int(np.count_nonzero(np.any(new[:33][mask]!=source[:33][mask],axis=1)))
    f=int(np.count_nonzero(np.any(new[40:]!=source[40:],axis=2)))
    head+=h;feet+=f
    if h or f:bad.append({'kind':r['kindName'],'clip':r['clip'],'direction':r['direction'],'variant':r['variant'],'key':r['key'],'head':h,'feet':f})
report={'result':'PASS' if head==feet==0 else 'FAIL','sampleSlots':len(data['frames']),'uniqueImages':data['uniqueSprites'],'changedOriginalUpperBodyPixels':head,'changedFootPixels':feet,'mismatches':bad,'scope':'Exact source opaque upper33 rows, exact original lower24 rows, native48x64, binary alpha. Retargeted joints are not new authored motion keys; actual Game View motion still required.'}
out=root/'ArtDirection/Review-20261001/weapon-carry'/f'{version}-source-qa.json';out.write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k!='mismatches'}))
assert report['result']=='PASS'
