"""Copy reviewed baked PNGs and export their authored hand socket table."""
from pathlib import Path
import csv,shutil
root=Path(__file__).resolve().parents[2]
author=root/'Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth'
runtime=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroWalk/move'
labels=['north','northeast','east','southeast','south','southwest','west','northwest']
lines=['using UnityEngine;','', 'namespace RuinRunner','{',
 '    // Generated from reviewed authoring coordinates; no runtime pixel/bounds inference.',
 '    internal static class AuthoredPlayerWalk','    {',
 '        public const int FrameCount = 12;',
 '        public static Vector2 HandPixel(int direction,int frame)=>Hands[direction,frame];',
 '        private static readonly Vector2[,] Hands = {']
for direction,label in enumerate(labels):
    src=author/('rigBaked' if direction==4 else f'bakedDirections/{direction:02}')
    dst=runtime/f'{direction:02}';dst.mkdir(parents=True,exist_ok=True)
    rows=list(csv.DictReader((root/f'ArtDirection/Generated/HeroWalk-20260921/{label}-rig-poses.csv').open()))
    assert len(rows)==12
    for frame in range(12):
        assert int(rows[frame]['frame'])==frame
        shutil.copy2(src/f'{frame:02}.png',dst/f'{frame:02}.png')
    lines.append('            { // '+label)
    for i in range(0,12,3):
        lines.append('                '+','.join(f"new Vector2({r['handX']}f,{r['handY']}f)" for r in rows[i:i+3])+(',' if i<9 else ''))
    lines.append('            }'+(',' if direction<7 else ''))
lines.extend(['        };','    }','}'])
(root/'Assets/RuinRunner/Runtime/Player/AuthoredPlayerWalk.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Exported 96 complete walk PNGs and 96 explicit hand coordinates. Original HeroMotion preserved.')
