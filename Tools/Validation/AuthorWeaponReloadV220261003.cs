using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using RuinRunner;

// Native arm joints, exact approved head/backpack/footfall; no resampling.
public static class AuthorWeaponReloadV220261003
{
    const string Output="ArtDirection/Generated/SscaryMotion-20261003/weapon-reload-v2/";
    const string Source="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/";
    static readonly string[] Dirs={"up","right_up","right_down","down","left_down","left_up"};
    static readonly int[] BodyMin={19,25,23,21,21,21}, BodyMax={27,29,28,28,26,23};
    static readonly RectInt[] Packs={new RectInt(19,32,9,7),new RectInt(17,30,11,9),new RectInt(18,30,4,9),new RectInt(0,0,0,0),new RectInt(28,30,4,9),new RectInt(22,30,11,9)};
    static readonly Color32 Clear=new Color32(0,0,0,0),Ink=new Color32(0,0,0,255),Skin=new Color32(229,149,159,255),SkinDark=new Color32(193,106,125,255),Cloth=new Color32(60,84,143,255),ClothDark=new Color32(50,65,127,255);
    static int At(int x,int y)=>y*48+x;
    static string Hash(Color32[] p){using(var s=SHA256.Create()){var b=new byte[p.Length*4];for(int i=0;i<p.Length;i++){b[i*4]=p[i].r;b[i*4+1]=p[i].g;b[i*4+2]=p[i].b;b[i*4+3]=p[i].a;}return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}}
    static Color32[] Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));if(t.width!=48||t.height!=64)throw new Exception(path);var src=t.GetPixels32();var top=new Color32[src.Length];for(int y=0;y<64;y++)Array.Copy(src,y*48,top,(63-y)*48,48);UnityEngine.Object.DestroyImmediate(t);return top;}
    static void Save(Color32[] p,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var bottom=new Color32[p.Length];for(int y=0;y<64;y++)Array.Copy(p,y*48,bottom,(63-y)*48,48);var t=new Texture2D(48,64,TextureFormat.RGBA32,false);t.SetPixels32(bottom);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
    static void Pixel(Color32[] p,int x,int y,Color32 c){if(x>=0&&x<48&&y>=0&&y<64)p[At(x,y)]=c;}
    static int HeadTop(Color32[] p){for(int y=0;y<32;y++)for(int x=0;x<48;x++)if(p[At(x,y)].a>0)return y;return 0;}
    static void StripHand(Color32[] p,int dir,Vector2 hand)
    {
        for(int y=Math.Max(33,Mathf.FloorToInt(hand.y)-2);y<=Math.Min(39,Mathf.CeilToInt(hand.y)+1);y++)
        for(int x=Mathf.FloorToInt(hand.x)-2;x<=Mathf.CeilToInt(hand.x)+1;x++)
        {
            var c=p[At(x,y)];bool flesh=c.Equals(Skin)||c.Equals(SkinDark);
            bool edge=c.Equals(Ink)&&(x<BodyMin[dir]||x>BodyMax[dir]);
            if(flesh||edge)p[At(x,y)]=x>=BodyMin[dir]&&x<=BodyMax[dir]&&y<=36?(dir==0?new Color32(110,39,73,255):Cloth):Clear;
        }
    }
    static void Line(Dictionary<Vector2Int,Color32> cells,Vector2 a,Vector2 b,Color32 shade,Color32 lit)
    {
        int steps=Math.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*3));
        for(int i=0;i<=steps;i++){var p=Vector2.Lerp(a,b,i/(float)steps);int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y);cells[new Vector2Int(x,y)]=lit;cells[new Vector2Int(x,y+1)]=shade;}
    }
    static bool Flesh(Color32 c)=>c.Equals(Skin)||c.Equals(SkinDark);
    static bool FarArmEdge(Color32[] source,int x,int y)
    {
        if(!source[At(x,y)].Equals(Ink))return false;
        return x>0&&Flesh(source[At(x-1,y)])||x<47&&Flesh(source[At(x+1,y)])||y>32&&Flesh(source[At(x,y-1)])||y<39&&Flesh(source[At(x,y+1)]);
    }
    static void Arm(Color32[] pixels,Vector2 s,Vector2 h,int dir,float bend,bool behind)
    {
        var delta=h-s;float dist=Mathf.Max(.1f,delta.magnitude);var perpendicular=new Vector2(-delta.y,delta.x)/dist;
        var elbow=s+delta*.46f+perpendicular*(dir==2||dir==3||dir==5?-1:1)*bend;
        var cells=new Dictionary<Vector2Int,Color32>();Line(cells,s,elbow,ClothDark,Cloth);Line(cells,elbow,h,SkinDark,Skin);
        int hx=Mathf.RoundToInt(h.x),hy=Mathf.RoundToInt(h.y);
        cells[new Vector2Int(hx-1,hy)]=Skin;cells[new Vector2Int(hx,hy)]=Skin;cells[new Vector2Int(hx-1,hy+1)]=SkinDark;cells[new Vector2Int(hx,hy+1)]=SkinDark;
        var neighbors=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        foreach(var pair in cells)foreach(var n in neighbors){var p=pair.Key+n;if(!cells.ContainsKey(p)&&(!behind||pixels[At(Mathf.Clamp(p.x,0,47),Mathf.Clamp(p.y,0,63))].a==0))Pixel(pixels,p.x,p.y,Ink);}
        foreach(var pair in cells)if(!behind||pixels[At(Mathf.Clamp(pair.Key.x,0,47),Mathf.Clamp(pair.Key.y,0,63))].a==0)Pixel(pixels,pair.Key.x,pair.Key.y,pair.Value);
    }
    static readonly Vector2[] Near={new Vector2(27,32),new Vector2(28,32),new Vector2(28,32),new Vector2(21,32),new Vector2(21,32),new Vector2(21,32)};
    static readonly Vector2[] Far={new Vector2(20,32),new Vector2(24,32),new Vector2(22,32),new Vector2(28,32),new Vector2(27,32),new Vector2(24,32)};
    static readonly int[] View={0,1,2,2,3,4,4,5};
    static readonly string[] Profiles={"Dagger","Sword","Heavy","Bow","Pistol","BracedFirearm"};

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
            Vector2 readyGrip,cleanGrip;Vector2 startGrip=Vector2.zero;bool behind;
            var readySprite=SscaryArmedFrames.Ranged(clip,direction,variant,15,out readyGrip,out behind);
            var startSprite=entry==1?readySprite:WeaponStandingFrames.Sample(WeaponStandingFrames.Profile(Families[clip]),direction,variant,8,out startGrip,out behind);
            if(entry==1)startGrip=readyGrip;
            var cleanSprite=SscaryArmedFrames.Carry(false,direction,variant,out cleanGrip,out behind);
            string startPath=AssetDatabase.GetAssetPath(startSprite),readyPath=AssetDatabase.GetAssetPath(readySprite),cleanPath=AssetDatabase.GetAssetPath(cleanSprite);
            var start=Read(startPath);var ready=Read(readyPath);var clean=Read(cleanPath);
            Vector2 rigidGrip;var rigidSprite=WeaponStandingFrames.Sample(WeaponStandingFrames.Profile(Families[clip]),direction,variant,8,out rigidGrip,out behind);
            string rigidPath=AssetDatabase.GetAssetPath(rigidSprite);var rigid=Read(rigidPath);
            // Earlier crossbow/gun support-arm outlines left1-3 pixels in the foot region.
            // Keep the actual equipped standing footfall throughout reload.
            Array.Copy(rigid,40*48,start,40*48,24*48);Array.Copy(rigid,40*48,ready,40*48,24*48);
            var idle0=Read(Source+"idle/"+Dirs[View[direction]]+"/00.png");int bob=Mathf.Clamp(HeadTop(start)-HeadTop(idle0),-1,1);
            for(int key=0;key<16;key++)
            {
                Vector2 grip=Vector2.zero;var pixels=key==15?(Color32[])ready.Clone():Pose(start,clean,clip,direction,key,startGrip,readyGrip,bob,out grip);if(key==15)grip=readyGrip;
                string hash=Hash(pixels);int index;
                if(!unique.TryGetValue(hash,out index)){index=unique.Count;unique.Add(hash,index);Save(pixels,Output+index.ToString("D5")+".png");}
                records.Add(new JObject{{"clip",clip},{"family",Families[clip].ToString()},{"entry",entry},{"direction",direction},{"variant",variant},{"key",key},{"runtimeIndex",index},{"path",Output+index.ToString("D5")+".png"},{"source",startPath},{"readySource",readyPath},{"rigidSource",rigidPath},{"sha256",hash},{"grip",new JArray(grip.x,grip.y)}});
            }
        }
        File.WriteAllText(Output+"manifest.json",new JObject{{"uniqueSprites",unique.Count},{"scope","Six native support-hand reload actions, manual standing and auto ready entry, exact endpoints and original16 idle/gait variants. 24576 slots are combinations, not24576 independently authored actions."},{"frames",records}}.ToString(),new UTF8Encoding(false));
        return new{samples=records.Count,unique=unique.Count,output=Output};
    }
}
