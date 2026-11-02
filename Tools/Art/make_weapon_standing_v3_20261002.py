"""Preserve actual ready face/foot seams while using clean carry only for the new resting arms."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/AuthorWeaponStandingV220261002.cs').read_text(encoding='utf-8').replace('AuthorWeaponStandingV220261002','AuthorWeaponStandingV320261002').replace('weapon-standing-v2/','weapon-standing-v3/')
needle='Vector2 grip;var pixels=Pose(cleanPixels,idle0,profile,direction,key,hand,out grip);string hash=Hash(pixels);int index;'
change=r'''Vector2 grip;var pixels=Pose(cleanPixels,idle0,profile,direction,key,hand,out grip);
                // Clean carry is the arm substrate only. Keep actual action-ready
                // rigid head/face, backpack and footfall at the return boundary.
                for(int y=0;y<30;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)Pixel(pixels,x,y,source[At(x,y)]);
                for(int y=30;y<33;y++)for(int x=22;x<=27;x++)if(source[At(x,y)].a>0)Pixel(pixels,x,y,source[At(x,y)]);
                var pack=Packs[View[direction]];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(source[At(x,y)].a>0&&!Flesh(source[At(x,y)])&&!FarArmEdge(source,x,y))Pixel(pixels,x,y,source[At(x,y)]);
                Array.Copy(source,40*48,pixels,40*48,24*48);
                string hash=Hash(pixels);int index;'''
assert needle in t;t=t.replace(needle,change)
t=t.replace('Bow held vertically beside the torso while resting.','Bow held vertically beside the torso while resting. Actual ready head/face/backpack/feet restored to avoid an unrelated return seam.')
(root/'Tools/Validation/AuthorWeaponStandingV320261002.cs').write_text(t,encoding='utf-8')
t=(root/'Tools/Validation/ImportWeaponStandingV220261002.cs').read_text(encoding='utf-8').replace('ImportWeaponStandingV220261002','ImportWeaponStandingV320261002').replace('WeaponStandingV2','WeaponStandingV3').replace('weapon-standing-v2','weapon-standing-v3').replace('standing-import-v2','standing-import-v3')
(root/'Tools/Validation/ImportWeaponStandingV320261002.cs').write_text(t,encoding='utf-8')
print('Generated corrected v3 preservation of actual ready identity/footfall. V1/V2 left intact.')

