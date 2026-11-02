"""Preserve source pixels for the reviewed melee/bow group; explicit hand landmarks."""
from pathlib import Path
import json, shutil, hashlib
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[2]
folder=root/'ArtDirection/ExternalCandidates/20260922/native-item-selection'
entries=json.loads((folder/'manifest.json').read_text())['images'][:19]
# Coordinates are on the PADDED image, top origin; reviewed against original handles.
handles=[(24,28),(27,27),(27,27),(28,29),(32,31),(29,30),
         (30,30),(31,31),(31,31),(32,31),(31,31),(32,31),
         (32,31),(31,31),(33,32),(13,24),(15,25),(11,26),(10,13)]
angles=[135]*15+[-135,-135,-135,135]
names=[]
for entry,hand,angle in zip(entries,handles,angles):
    source=root/entry['path'];im=Image.open(source)
    dest=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Weapon'/source.name
    dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest)
    assert Image.open(dest).tobytes()==im.tobytes()
    entry['path']=dest.relative_to(root).as_posix()
    entry['grip']=list(hand);entry['angle']=angle
    entry['pivot']=[hand[0]/im.width,1-hand[1]/im.height]
    entry['muzzle']=[hand[0]-3,hand[1]+(3 if angle<0 else -3)]
    entry['spriteSize']=list(im.size)
    names.append('            { "'+entry['id']+'", new Pose('+','.join(str(v)+'f' for v in (*hand,*entry['muzzle'],angle))+') },')
(folder/'melee-bow-import.json').write_text(json.dumps(dict(images=entries),indent=2))
cs='''using System.Collections.Generic;
using UnityEngine;
namespace RuinRunner
{
    // Authored native pixels. Legacy art remains only for the unreviewed equipment.
    public static class NativeWeaponArt
    {
        public readonly struct Pose
        {
            public readonly Vector2 Grip, Muzzle;
            public readonly float ForwardAngle;
            public Pose(float x,float y,float mx,float my,float angle)
            {Grip=new Vector2(x,y);Muzzle=new Vector2(mx,my);ForwardAngle=angle;}
        }
        private static readonly Dictionary<string,Pose> poses=new Dictionary<string,Pose>
        {
ENTRIES
        };
        public static bool Supports(string id)=>id!=null&&poses.ContainsKey(id);
        public static bool TryGet(string id,out Pose pose)=>poses.TryGetValue(id,out pose);
        public static string Path(string id)=>"Art/Pixel/Separated/Equipment/Weapon/"+id;
    }
}
'''.replace('ENTRIES','\n'.join(names))
(root/'Assets/RuinRunner/Runtime/Player/NativeWeaponArt.cs').write_text(cs)
print('Prepared19 native weapons and authored handle table; source pixels unchanged')

