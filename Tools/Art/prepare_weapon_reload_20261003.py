"""Write native joint author/import tools; never edits raster assets in Python."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
review = root / 'ArtDirection/Review-20261003/weapon-reload'
backup = review / 'before'
backup.mkdir(parents=True, exist_ok=True)
for name in ['Assets/RuinRunner/Runtime/Player/PlayerAnimationController.cs',
             'Assets/RuinRunner/Runtime/Feedback/NativeHeroFrameCatalog.cs',
             'Assets/RuinRunner/Runtime/Diagnostics/RuntimeSmokeTest.cs',
             'Assets/RuinRunner/Editor/NativeHeroAtlasPrebuild.cs', 'WORK_STATUS.md']:
    dst = backup / Path(name).name
    if dst.exists():
        raise RuntimeError(f'Preserve previous backup: {dst}')
    dst.write_bytes((root / name).read_bytes())

standing = (root / 'Tools/Validation/AuthorWeaponStandingV420261002.cs').read_text(encoding='utf-8')
helpers = standing[standing.index('    static readonly Color32 Clear='):standing.index('    static Sprite Ready(')]
author = '''using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using RuinRunner;

// Native arm joints, exact approved head/backpack/footfall; no resampling.
public static class AuthorWeaponReload20261003
{
    const string Output="ArtDirection/Generated/SscaryMotion-20261003/weapon-reload-v1/";
    const string Source="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/";
    static readonly string[] Dirs={"up","right_up","right_down","down","left_down","left_up"};
    static readonly int[] BodyMin={19,25,23,21,21,21}, BodyMax={27,29,28,28,26,23};
    static readonly RectInt[] Packs={new RectInt(19,32,9,7),new RectInt(17,30,11,9),new RectInt(18,30,4,9),new RectInt(0,0,0,0),new RectInt(28,30,4,9),new RectInt(22,30,11,9)};
'''+helpers+'''
    static readonly WeaponFamily[] Families={WeaponFamily.Bow,WeaponFamily.Crossbow,WeaponFamily.Pistol,WeaponFamily.Rifle,WeaponFamily.Shotgun,WeaponFamily.Cannon};
    static readonly float[] Steps={0,.10f,.22f,.36f,.48f,.59f,.72f,.84f,1};
    static Vector2 Curve(Vector2[] points,float p)
    {
        int segment=0;while(segment<Steps.Length-2&&p>Steps[segment+1])segment++;
        return Vector2.Lerp(points[segment],points[segment+1],Mathf.SmoothStep(0,1,Mathf.InverseLerp(Steps[segment],Steps[segment+1],p)));
    }
    static Color32[] Pose(Color32[] start,Color32[] clean,int clip,int direction,int key,Vector2 startGrip,Vector2 readyGrip,int bob,out Vector2 grip)
    {
        if(key==0){grip=startGrip;return (Color32[])start.Clone();}
        int d=View[direction];var forward=new Vector2(Mathf.Sin(direction*Mathf.PI/4),-Mathf.Cos(direction*Mathf.PI/4));
        var reach=new Vector2(forward.x,forward.y*.45f);var shoulder=Near[d]+new Vector2(0,bob);var far=Far[d]+new Vector2(0,bob);
        var a=startGrip-new Vector2(-.5f,.5f);var ready=readyGrip-new Vector2(-.5f,.5f);
        // The main hand supports a stable weapon. The other hand retrieves,
        // inserts, seats/cocks and returns in a different path for each family.
        var lift=clip==5?new Vector2(0,-1):clip==2?new Vector2(-forward.x,0):new Vector2(0,-.5f);
        var n=Vector2.Lerp(a,ready,Mathf.SmoothStep(0,1,Mathf.Min(1,key/4f)))+lift*Mathf.Sin(key/15f*Mathf.PI);
        n=new Vector2(Mathf.Round(n.x),Mathf.Round(n.y));n.y=Mathf.Clamp(n.y,32+bob,35+bob);
        Vector2 rest=clip==0?far+new Vector2(-forward.x*.7f,3):clip==2?far+new Vector2(-forward.x*1.3f,3):a+reach*3.2f;
        Vector2 hold=clip==0?ready-reach*7+new Vector2(0,-.7f):clip==2?far+new Vector2(-forward.x,3):ready+reach*(clip==1?2.5f:clip==3?3.2f:4);
        var belt=far+new Vector2(-forward.x*1.8f,5);
        var socket=n+reach*(clip==0?-2:clip==1?1.2f:clip==2?-.7f:clip==3?-.8f:clip==4?2.2f:-1.3f)+new Vector2(0,clip==5?1.2f:1);
        Vector2[] points;
        switch(clip)
        {
            case 0: points=new[]{rest,belt,belt+new Vector2(forward.x,0),socket,socket-reach*2,socket-reach*4,hold-reach,hold,hold};break;
            case 1: points=new[]{rest,belt,belt,socket,socket+reach*2, socket-reach*2,hold-reach,hold,hold};break;
            case 2: points=new[]{rest,n-reach,n-reach*2+new Vector2(0,3),belt,socket,socket-new Vector2(0,1),n+reach,hold,hold};break;
            case 3: points=new[]{rest,n-reach,belt,belt,socket,socket-new Vector2(0,1),n+reach*1.4f,hold-reach,hold};break;
            case 4: points=new[]{rest,belt,socket,belt,socket,belt,socket,hold-reach*2,hold};break;
            default:points=new[]{rest,n-reach*2,belt,belt,socket,socket+reach,socket-reach*2,hold-reach,hold};break;
        }
        // Auto entry already has the exact firing-ready support arm.
        if(startGrip==readyGrip)points[0]=hold;
        var support=Curve(points,key/15f);support.y=Mathf.Min(38+bob,support.y);
        if(Vector2.Distance(far,support)>12)support=far+(support-far).normalized*12;
        var pixels=(Color32[])clean.Clone();
        for(int y=33;y<40;y++)for(int x=0;x<48;x++)
            if(Flesh(clean[At(x,y)])||FarArmEdge(clean,x,y))pixels[At(x,y)]=x>=BodyMin[d]&&x<=BodyMax[d]&&y<=36?(d==0?new Color32(110,39,73,255):Cloth):Clear;
        Arm(pixels,far,support,d,clip==0?1.7f:clip==5?.9f:1.25f,d==0||d==1||d==5);
        Arm(pixels,shoulder,n,d,clip==2?1.7f:clip==5?.8f:1.1f,false);
        for(int y=0;y<33;y++)for(int x=0;x<48;x++)if(start[At(x,y)].a>0)Pixel(pixels,x,y,start[At(x,y)]);
        Array.Copy(start,0,pixels,0,30*48);
        for(int y=30;y<33;y++)for(int x=22;x<=27;x++)Pixel(pixels,x,y,start[At(x,y)]);
        var pack=Packs[d];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(start[At(x,y)].a>0&&!Flesh(start[At(x,y)])&&!FarArmEdge(start,x,y))Pixel(pixels,x,y,start[At(x,y)]);
        Array.Copy(start,40*48,pixels,40*48,24*48);
        grip=n+new Vector2(-.5f,.5f);return pixels;
    }
    public static object Main()
    {
        if(Application.isPlaying||Directory.Exists(Output))throw new Exception("Stop play/preserve versioned authoring");
        Directory.CreateDirectory(Output);var records=new JArray();var unique=new Dictionary<string,int>();
        for(int clip=0;clip<6;clip++)for(int entry=0;entry<2;entry++)for(int direction=0;direction<8;direction++)for(int variant=0;variant<16;variant++)
        {
            Vector2 readyGrip,startGrip,cleanGrip;bool behind;
            var readySprite=SscaryArmedFrames.Ranged(clip,direction,variant,15,out readyGrip,out behind);
            var startSprite=entry==1?readySprite:WeaponStandingFrames.Sample(WeaponStandingFrames.Profile(Families[clip]),direction,variant,8,out startGrip,out behind);
            if(entry==1)startGrip=readyGrip;
            var cleanSprite=SscaryArmedFrames.Carry(false,direction,variant,out cleanGrip,out behind);
            string startPath=AssetDatabase.GetAssetPath(startSprite),readyPath=AssetDatabase.GetAssetPath(readySprite),cleanPath=AssetDatabase.GetAssetPath(cleanSprite);
            var start=Read(startPath);var ready=Read(readyPath);var clean=Read(cleanPath);
            var idle0=Read(Source+"idle/"+Dirs[View[direction]]+"/00.png");int bob=Mathf.Clamp(HeadTop(start)-HeadTop(idle0),-1,1);
            for(int key=0;key<16;key++)
            {
                Vector2 grip;var pixels=key==15?(Color32[])ready.Clone():Pose(start,clean,clip,direction,key,startGrip,readyGrip,bob,out grip);if(key==15)grip=readyGrip;
                string hash=Hash(pixels);int index;
                if(!unique.TryGetValue(hash,out index)){index=unique.Count;unique.Add(hash,index);Save(pixels,Output+index.ToString("D5")+".png");}
                records.Add(new JObject{{"clip",clip},{"family",Families[clip].ToString()},{"entry",entry},{"direction",direction},{"variant",variant},{"key",key},{"runtimeIndex",index},{"path",Output+index.ToString("D5")+".png"},{"source",startPath},{"readySource",readyPath},{"sha256",hash},{"grip",new JArray(grip.x,grip.y)}});
            }
        }
        File.WriteAllText(Output+"manifest.json",new JObject{{"uniqueSprites",unique.Count},{"scope","Six native support-hand reload actions, manual standing and auto ready entry, exact endpoints and original16 idle/gait variants. 24576 slots are combinations, not24576 independently authored actions."},{"frames",records}}.ToString(),new UTF8Encoding(false));
        return new{samples=records.Count,unique=unique.Count,output=Output};
    }
}
'''
# Definite assignment on conditional expressions needs initialized out variables.
author = author.replace('Vector2 readyGrip,startGrip,cleanGrip;', 'Vector2 readyGrip,cleanGrip;Vector2 startGrip=Vector2.zero;')
author = author.replace('Vector2 grip;var pixels=key==15?', 'Vector2 grip=Vector2.zero;var pixels=key==15?')
(root / 'Tools/Validation/AuthorWeaponReload20261003.cs').write_text(author, encoding='utf-8')

importer = (root / 'Tools/Validation/ImportWeaponStandingV420261002.cs').read_text(encoding='utf-8')
importer = importer.replace('ImportWeaponStandingV420261002', 'ImportWeaponReload20261003').replace('WeaponStandingV4', 'WeaponReloadV1')
importer = importer.replace('ArtDirection/Generated/SscaryMotion-20261001/weapon-standing-v4/', 'ArtDirection/Generated/SscaryMotion-20261003/weapon-reload-v1/')
importer = importer.replace('ArtDirection/Review-20261001/weapon-carry/standing-import-v4.json', 'ArtDirection/Review-20261003/weapon-reload/reload-import-v1.json')
importer = importer.replace('WeaponStandingFrames', 'WeaponReloadFrames')
importer = importer.replace('Six separate native standing profiles and short returns, exact original breathing/gait/head.', 'Six native reloads, two exact entries and original breathing/gait/head.')
importer = importer.replace(' public const float ReturnSeconds=.14f;', '')
profile_start = importer.index('        code.Append(" public static int Profile(')
profile_end = importer.index('        File.WriteAllText(', profile_start)
importer = importer[:profile_start]+'''        code.Append(" public static int Clip(WeaponFamily f)=>f==WeaponFamily.Bow?0:f==WeaponFamily.Crossbow?1:f==WeaponFamily.Pistol?2:f==WeaponFamily.Rifle?3:f==WeaponFamily.Shotgun?4:5;\\n");
        code.Append(" public static Sprite Sample(int clip,int entry,int d,int variant,int key,out Vector2 h,out bool b){Preload();int slot=(((Mathf.Clamp(clip,0,5)*2+Mathf.Clamp(entry,0,1))*8+Mathf.Clamp(d,0,7))*16+Mathf.Clamp(variant,0,15))*16+Mathf.Clamp(key,0,15);h=Hands[slot];b=Behind[slot];return frames[Cells[slot]];}\\n");
        code.Append(" public static float Angle(int clip,int entry,float progress,Vector2 aim)=>clip==0&&entry==0?WeaponStandingFrames.Angle(3,1,aim)*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.28f))):0;\\n }\\n}\\n");
'''+importer[profile_end:]
importer = importer.replace('separate standing native images', 'separate reload native images').replace('original breathing/gait/short return combinations', 'manual/auto reload breathing/gait combinations')
(root / 'Tools/Validation/ImportWeaponReload20261003.cs').write_text(importer, encoding='utf-8')
print('Prepared source tools and protected backups; no raster import or runtime change yet.')
