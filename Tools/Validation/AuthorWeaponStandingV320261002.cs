using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Newtonsoft.Json.Linq;
using RuinRunner;
using UnityEditor;

// Offline native joint poses assembled from the approved character's exact parts.
// Face/pack remain rigid source shapes. No generated raster or image resampling.
public static class AuthorWeaponStandingV320261002
{
    const string Source="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/";
    const string Output="ArtDirection/Generated/SscaryMotion-20261001/weapon-standing-v3/";
    static readonly string[] Dirs={"up","right_up","right_down","down","left_down","left_up"};
    static readonly string[] Clips={"Sweep","Reverse","Cleave"};
    static readonly int[] DirectionIndex={0,1,2,4,5,7};
    static readonly float[] Phases={0,.06f,.12f,.18f,.23f,.28f,.34f,.42f,.48f,.54f,.60f,.68f,.76f,.84f,.92f,1};
    static readonly float[] SweepAngles={0,-30,-65,-92,-83,-68,-40,0,16,36,54,68,57,37,14,0};
    static readonly float[] Radii={3,3.5f,4,4.3f,4.8f,5.4f,6,6.4f,6.5f,6.2f,6,5.8f,5.3f,4.7f,3.8f,3};
    static readonly float[] Weights={0,.28f,.55f,.8f,1,1,1,1,1,1,.9f,.75f,.55f,.35f,.15f,0};
    static readonly float[] Reach={0,-.7f,-1.4f,-2.1f,-2.6f,-2.1f,-.8f,1.8f,3,3.3f,2.8f,2.2f,1.6f,1,.5f,0};
    static readonly float[] Lift={0,-.4f,-1,-1.8f,-2.4f,-2,-1.2f,0,.8f,1.2f,1,.8f,.5f,.3f,.1f,0};
    static readonly float[] RearSpread={0,.5f,1,1.5f,2,2.5f,3,3.4f,3.7f,3.2f,2.8f,2.3f,1.6f,1,.5f,0};
    static readonly Vector2[] Shoulders={new Vector2(27,32),new Vector2(28,32),new Vector2(22,32),new Vector2(21,32),new Vector2(27,32),new Vector2(21,32)};
    static readonly Vector2[] FarShoulders={new Vector2(20,32),new Vector2(24,32),new Vector2(28,32),new Vector2(28,32),new Vector2(21,32),new Vector2(24,32)};
    static readonly Vector2[] Rest={new Vector2(28,34.5f),new Vector2(30,34),new Vector2(23,34.5f),new Vector2(22,34.5f),new Vector2(27,37),new Vector2(21,36)};
    static readonly Vector2[] Forward={new Vector2(0,-1),new Vector2(.85f,-.53f),new Vector2(.95f,.31f),new Vector2(0,1),new Vector2(-.95f,.31f),new Vector2(-.85f,-.53f)};
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
    static Sprite Ready(int profile,int d,int variant,out Vector2 grip,out bool behind)
    {
        if(profile<3)return SscaryArmedFrames.Carry(profile==2,d,variant,out grip,out behind);
        return SscaryArmedFrames.Ranged(profile==3?0:profile==4?2:3,d,variant,15,out grip,out behind);
    }
    static Color32[] Pose(Color32[] source,Color32[] idle0,int profile,int direction,int key,Vector2 initial,out Vector2 grip)
    {
        if(key==0){grip=initial;return (Color32[])source.Clone();}
        int d=View[direction];int bob=Mathf.Clamp(HeadTop(source)-HeadTop(idle0),-1,1);
        var forward=new Vector2(Mathf.Sin(direction*Mathf.PI/4),-Mathf.Cos(direction*Mathf.PI/4));
        var reach=new Vector2(forward.x,forward.y*.45f);
        var shoulder=Near[d]+new Vector2(0,bob);var far=Far[d]+new Vector2(0,bob);
        var from=initial-new Vector2(-.5f,.5f);
        var target=from+(profile==0?-reach:profile==1?reach*.7f:profile==2?new Vector2(0,-1):profile==3?reach*1.1f:profile==4?-reach*.5f:Vector2.zero);
        target.y=Mathf.Clamp(target.y,33+bob,35+bob);
        float phase=key/8f;
        var current=Vector2.Lerp(from,target,Mathf.SmoothStep(0,1,phase));
        current=new Vector2(Mathf.Round(current.x),Mathf.Round(current.y));
        Vector2 oldFar=profile==3?from-reach*7+new Vector2(0,-.7f):profile==4?far+new Vector2(-Forward[d].x*1.4f,4.3f):profile==5?from+reach*3.2f:profile==2?from+new Vector2(Forward[d].x*1.2f,Forward[d].y*.5f):far+new Vector2(-Forward[d].x,3);
        Vector2 nextFar=profile==3?far+new Vector2(-forward.x*.7f,3):profile==5?target+reach*3.2f:profile==2?target-reach*.7f:far+new Vector2(-forward.x*1.3f,3);
        var support=Vector2.Lerp(oldFar,nextFar,Mathf.SmoothStep(0,1,phase));support.y=Mathf.Min(36+bob,support.y);
        if(Vector2.Distance(far,support)>12)support=far+(support-far).normalized*12;
        var pixels=(Color32[])source.Clone();
        for(int y=33;y<40;y++)for(int x=0;x<48;x++)
            if(Flesh(source[At(x,y)])||FarArmEdge(source,x,y))pixels[At(x,y)]=x>=BodyMin[d]&&x<=BodyMax[d]&&y<=36?(d==0?new Color32(110,39,73,255):Cloth):Clear;
        Arm(pixels,far,support,d,profile==3?1.8f:profile==2?.8f:1.2f,d==0||d==1||d==5);
        Arm(pixels,shoulder,current,d,profile==0?2:profile==2?.9f:profile==3?.65f:profile==4?1.8f:1.1f,false);
        for(int y=0;y<33;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)Pixel(pixels,x,y,source[At(x,y)]);
        var pack=Packs[d];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(source[At(x,y)].a>0&&!Flesh(source[At(x,y)])&&!FarArmEdge(source,x,y))Pixel(pixels,x,y,source[At(x,y)]);
        grip=current+new Vector2(-.5f,.5f);return pixels;
    }
    public static object Main()
    {
        if(Application.isPlaying||Directory.Exists(Output))throw new Exception("Stop play/preserve versioned candidates");
        Directory.CreateDirectory(Output);var records=new JArray();var unique=new Dictionary<string,int>();
        for(int profile=0;profile<6;profile++)for(int direction=0;direction<8;direction++)for(int variant=0;variant<16;variant++)
        {
            Vector2 hand;bool behind;var sprite=Ready(profile,direction,variant,out hand,out behind);
            string sourcePath=AssetDatabase.GetAssetPath(sprite);var source=Read(sourcePath);var idle0=Read(Source+"idle/"+Dirs[View[direction]]+"/00.png");
            for(int key=0;key<9;key++)
            {
                Vector2 cleanHand;bool cleanBehind;
                var clean=key==0?sprite:SscaryArmedFrames.Carry(profile==2,direction,variant,out cleanHand,out cleanBehind);
                string cleanPath=AssetDatabase.GetAssetPath(clean);var cleanPixels=Read(cleanPath);
                Vector2 grip;var pixels=Pose(cleanPixels,idle0,profile,direction,key,hand,out grip);
                // Clean carry is the arm substrate only. Keep actual action-ready
                // rigid head/face, backpack and footfall at the return boundary.
                for(int y=0;y<30;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)Pixel(pixels,x,y,source[At(x,y)]);
                for(int y=30;y<33;y++)for(int x=22;x<=27;x++)if(source[At(x,y)].a>0)Pixel(pixels,x,y,source[At(x,y)]);
                var pack=Packs[View[direction]];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(source[At(x,y)].a>0&&!Flesh(source[At(x,y)])&&!FarArmEdge(source,x,y))Pixel(pixels,x,y,source[At(x,y)]);
                Array.Copy(source,40*48,pixels,40*48,24*48);
                string hash=Hash(pixels);int index;
                if(!unique.TryGetValue(hash,out index)){index=unique.Count;unique.Add(hash,index);Save(pixels,Output+index.ToString("D5")+".png");}
                records.Add(new JObject{{"profile",profile},{"profileName",Profiles[profile]},{"direction",direction},{"variant",variant},{"key",key},{"runtimeIndex",index},{"path",Output+index.ToString("D5")+".png"},{"source",cleanPath},{"readySource",sourcePath},{"sourceSha256",Hash(cleanPixels)},{"sha256",hash},{"grip",new JArray(grip.x,grip.y)}});
            }
        }
        File.WriteAllText(Output+"manifest.json",new JObject{{"uniqueSprites",unique.Count},{"scope","Six separate native armed standing/short return profiles. Original eight breathing/gait variants retained;9 return keys are not counted as nine new idle poses. Bow off-hand relaxes from a clean carry source instead of preserving old drawn upper arms. Bow held vertically beside the torso while resting. Actual ready head/face/backpack/feet restored to avoid an unrelated return seam."},{"frames",records}}.ToString(),new UTF8Encoding(false));
        return new{samples=records.Count,unique=unique.Count,output=Output};
    }
}
