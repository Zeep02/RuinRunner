"""Apply approved native sprite landmark data to runtime C#; no image operations."""
from pathlib import Path
import json,re
ROOT=Path(__file__).resolve().parents[2]
rows=json.loads((ROOT/'ArtDirection/Review-20261004/weapon-redesign/accepted-selection.json').read_text(encoding='utf-8'))
path=ROOT/'Assets/RuinRunner/Runtime/Player/NativeWeaponArt.cs'
text=path.read_text(encoding='utf-8')
added=[]
for row in rows:
    coords=[*row['grip'],*row['muzzle'],row['forwardAngle']]
    statement='            { "'+row['id']+'", new Pose('+','.join(f'{n:g}f' for n in coords)+') },'
    pattern=r'^\s*\{ "'+re.escape(row['id'])+r'", new Pose\([^\n]+$'
    if re.search(pattern,text,re.M):text=re.sub(pattern,statement,text,flags=re.M)
    else:added.append(statement)
if added:text=text.replace('        };','\n'.join(added)+'\n        };',1)
path.write_text(text,encoding='utf-8')
print('Updated',len(rows),'native weapon grip/muzzle/forward records')
