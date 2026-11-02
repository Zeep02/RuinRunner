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
public static class AuthorHeroRecoil20261004
{
    const string Source="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/";
    const string Output="ArtDirection/Generated/SscaryMotion-20261004/hurt-native-v1/";
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

    static readonly int[] Profiles={0,1,2,4,5};
    // Impact withdrawal, asymmetric bracing, then an open-elbow recovery.
    // These edit the established native joint source, not a whole-sprite shake.
    static readonly float[] Withdraw={0,.65f,1,1,.82f,.6f,.35f,.1f,0};
    static readonly float[] LiftHand={0,-1,-2,-2,-1,0,.8f,.7f,0};
    static readonly float[] Bend={1.1f,2.1f,3.1f,3.6f,3.2f,2.4f,1.8f,1.4f,1.1f};
    static readonly float[] FreeLift={0,-1.2f,-2.5f,-3.5f,-3,-1.8f,-.4f,.8f,0};
    static Color32[] Recoil(Color32[] source,int profile,int direction,int key,Vector2 initial,out Vector2 grip,out Vector2Int lean)
    {
        int d=View[direction];lean=Vector2Int.zero;grip=initial;
        if(key==0||key==8)return (Color32[])source.Clone();
        float w=Withdraw[key];
        var reach=new Vector2(Mathf.Sin(direction*Mathf.PI/4),-Mathf.Cos(direction*Mathf.PI/4)*.45f);
        lean=new Vector2Int(Mathf.RoundToInt(-reach.x*w),key>=2&&key<=4?1:0);
        var sourceIdle=Read(Source+"idle/"+Dirs[d]+"/00.png");
        int bob=Mathf.Clamp(HeadTop(source)-HeadTop(sourceIdle),-1,1);
        var shoulder=Near[d]+new Vector2(0,bob)+(Vector2)lean;
        var far=Far[d]+new Vector2(0,bob)+(Vector2)lean;
        var from=initial-new Vector2(-.5f,.5f);
        var hand=from+(Vector2)lean-reach*(profile==4?1.8f:2.5f)*w+new Vector2(0,LiftHand[key]);
        hand.y=Mathf.Max(32+bob+lean.y,hand.y);
        hand=new Vector2(Mathf.Round(hand.x),Mathf.Round(hand.y));
        var support=profile==2?hand-reach*.7f:profile==4?hand+reach*3.2f:
            far+new Vector2(-reach.x*(1.3f+2*w),3+FreeLift[key]);
        support.y=Mathf.Max(32+bob+lean.y,support.y);
        var stripped=(Color32[])source.Clone();
        for(int y=33;y<40;y++)for(int x=0;x<48;x++)
            if(Flesh(source[At(x,y)])||FarArmEdge(source,x,y))stripped[At(x,y)]=x>=BodyMin[d]&&x<=BodyMax[d]&&y<=36?(d==0?new Color32(110,39,73,255):Cloth):Clear;
        var pixels=new Color32[source.Length];
        for(int y=0;y<40;y++)for(int x=0;x<48;x++)if(stripped[At(x,y)].a>0)Pixel(pixels,x+lean.x,y+lean.y,stripped[At(x,y)]);
        Array.Copy(source,40*48,pixels,40*48,24*48);
        Arm(pixels,far,support,d,Bend[key]*.8f,d==0||d==1||d==5);
        Arm(pixels,shoulder,hand,d,Bend[key],false);
        // Keep the approved identity rigid. Only limbs and torso posture articulate.
        for(int y=0;y<33;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)Pixel(pixels,x+lean.x,y+lean.y,source[At(x,y)]);
        var pack=Packs[d];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(source[At(x,y)].a>0&&!Flesh(source[At(x,y)])&&!FarArmEdge(source,x,y))Pixel(pixels,x+lean.x,y+lean.y,source[At(x,y)]);
        Array.Copy(source,40*48,pixels,40*48,24*48);
        grip=hand+new Vector2(-.5f,.5f);return pixels;
    }
    public static object Main()
    {
        if(Application.isPlaying||Directory.Exists(Output))throw new Exception("Stop play/preserve versioned candidates");
        Directory.CreateDirectory(Output);var records=new JArray();var unique=new Dictionary<string,int>();var poseCounts=new JObject();
        for(int profile=0;profile<5;profile++)for(int direction=0;direction<8;direction++)for(int variant=0;variant<16;variant++)
        {
            Vector2 hand;bool behind;var sprite=WeaponStandingFrames.Sample(Profiles[profile],direction,variant,8,out hand,out behind);
            string sourcePath=AssetDatabase.GetAssetPath(sprite);var source=Read(sourcePath);var keys=new HashSet<string>();
            for(int key=0;key<9;key++)
            {
                Vector2 grip;Vector2Int lean;var pixels=Recoil(source,profile,direction,key,hand,out grip,out lean);
                string hash=Hash(pixels);keys.Add(hash);int index;
                if(!unique.TryGetValue(hash,out index)){index=unique.Count;unique.Add(hash,index);Save(pixels,Output+index.ToString("D5")+".png");}
                records.Add(new JObject{{"profile",profile},{"standingProfile",Profiles[profile]},{"direction",direction},{"variant",variant},{"key",key},{"runtimeIndex",index},{"path",Output+index.ToString("D5")+".png"},{"source",sourcePath},{"sourceSha256",Hash(source)},{"sha256",hash},{"grip",new JArray(grip.x,grip.y)},{"lean",new JArray(lean.x,lean.y)}});
            }
            if(variant==0)poseCounts[profile+"/"+direction]=keys.Count;
        }
        File.WriteAllText(Output+"manifest.json",new JObject{{"uniqueSprites",unique.Count},{"scope","Original Sscary identity and independent eight-frame breathing/gait. Native editable joint recoil; nine short keys including identical entry/exit. No character replacement, raster resampling or runtime whole-image wobble. Actual unique poses recorded separately from gait combinations."},{"standUniquePoses",poseCounts},{"frames",records}}.ToString(),new UTF8Encoding(false));
        return new{samples=records.Count,unique=unique.Count,poseCounts,output=Output};
    }
}
