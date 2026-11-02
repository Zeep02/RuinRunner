from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/AuthorWeaponStandingV320261002.cs').read_text(encoding='utf-8').replace('AuthorWeaponStandingV320261002','AuthorWeaponStandingV420261002').replace('weapon-standing-v3/','weapon-standing-v4/')
old='for(int y=0;y<30;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)Pixel(pixels,x,y,source[At(x,y)]);'
assert old in t;t=t.replace(old,'Array.Copy(source,0,pixels,0,30*48);')
(root/'Tools/Validation/AuthorWeaponStandingV420261002.cs').write_text(t,encoding='utf-8')
t=(root/'Tools/Validation/ImportWeaponStandingV320261002.cs').read_text(encoding='utf-8').replace('ImportWeaponStandingV320261002','ImportWeaponStandingV420261002').replace('WeaponStandingV3','WeaponStandingV4').replace('weapon-standing-v3','weapon-standing-v4').replace('standing-import-v3','standing-import-v4')
t=t.replace('const string Root="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMaleCombat/WeaponStandingV4/";', 'const string Root=RuinRunner.NativeHeroFrameCatalog.SourceRoot+"WeaponStandingV4/";')
(root/'Tools/Validation/ImportWeaponStandingV420261002.cs').write_text(t,encoding='utf-8')
