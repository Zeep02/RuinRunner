using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Newtonsoft.Json.Linq;

// Offline native joint poses assembled from the approved character's exact parts.
// Face/pack remain rigid source shapes. No generated raster or image resampling.
public static class AuthorSscaryRanged20261001
{
    const string Source="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/";
    const string Output="ArtDirection/Generated/SscaryMotion-20261001/ranged-v7/";
    static readonly string[] Dirs={"up","right_up","right_down","down","left_down","left_up"};
    static readonly string[] Clips={"Bow","Crossbow","Pistol","Rifle","Shotgun","Cannon"};
    static readonly int[] DirectionIndex={0,1,2,4,5,7};
    static readonly float[] Phases={0,.06f,.12f,.18f,.23f,.28f,.34f,.42f,.48f,.54f,.60f,.68f,.76f,.84f,.92f,1};
    static readonly float[] SweepAngles={0,-30,-65,-92,-83,-68,-40,0,16,36,54,68,57,37,14,0};
    static readonly float[] Radii={3,3.5f,4,4.3f,4.8f,5.4f,6,6.4f,6.5f,6.2f,6,5.8f,5.3f,4.7f,3.8f,3};
    static readonly float[] Weights={0,.28f,.55f,.8f,1,1,1,1,1,1,.9f,.75f,.55f,.35f,.15f,0};
    static readonly float[] Reach={0,-.7f,-1.4f,-2.1f,-2.6f,-2.1f,-.8f,1.8f,3,3.3f,2.8f,2.2f,1.6f,1,.5f,0};
    static readonly float[] Lift={0,-.4f,-1,-1.8f,-2.4f,-2,-1.2f,0,.8f,1.2f,1,.8f,.5f,.3f,.1f,0};
    static readonly float[] RearSpread={0,.5f,1,1.5f,2,2.5f,3,3.4f,3.7f,3.2f,2.8f,2.3f,1.6f,1,.5f,0};
    static readonly Vector2[] Shoulders={new Vector2(27,32),new Vector2(28,32),new Vector2(28,32),new Vector2(21,32),new Vector2(21,32),new Vector2(21,32)};
    static readonly Vector2[] FarShoulders={new Vector2(20,32),new Vector2(24,32),new Vector2(22,32),new Vector2(28,32),new Vector2(27,32),new Vector2(24,32)};
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
    static readonly float[] Kick={0,.35f,.7f,1,.94f,.83f,.7f,.58f,.47f,.37f,.28f,.2f,.13f,.07f,.03f,0};
    static readonly float[] Draw={1,.8f,.55f,.3f,.08f,0,.04f,.12f,.24f,.38f,.53f,.68f,.8f,.9f,.96f,1};
    static readonly float[] Magnitude={2.7f,2.2f,3.7f,2.6f,4.6f,6f};
    static Color32[] Pose(Color32[] source,Color32[] legs,int d,int clip,int f,int bob,int seam,out Vector2 grip,out Vector2Int lean)
    {
        var forward=Forward[d].normalized;
        float kick=Kick[f],strength=Magnitude[clip];
        lean=new Vector2Int(Mathf.RoundToInt(-forward.x*kick*(clip>=4?1.2f:.55f)),clip>=4&&f>=2&&f<=6?1:0);
        bool twoHand=clip!=2;
        var stripped=(Color32[])source.Clone();
        for(int y=33;y<40;y++)for(int x=0;x<48;x++)
            if(Flesh(source[At(x,y)])||FarArmEdge(source,x,y))stripped[At(x,y)]=x>=BodyMin[d]&&x<=BodyMax[d]&&y<=36?(d==0?new Color32(110,39,73,255):Cloth):Clear;
        var pixels=new Color32[source.Length];
        for(int y=0;y<seam;y++)for(int x=0;x<48;x++)if(stripped[At(x,y)].a>0)Pixel(pixels,x+lean.x,y+lean.y,stripped[At(x,y)]);
        Array.Copy(legs,seam*48,pixels,seam*48,(64-seam)*48);
        var shoulder=Shoulders[d]+new Vector2(0,bob)+(Vector2)lean;
        var far=FarShoulders[d]+new Vector2(0,bob)+(Vector2)lean;
        // Ready grip stays below the face. The bow arm remains extended while
        // its string hand releases/redraws; firearms compress then recover.
        var reach=new Vector2(forward.x,forward.y*.5f);
        grip=shoulder+reach*(clip==0?5.2f:clip==2?4.4f:3.2f)+new Vector2(0,d>=2&&d<=4?5.5f:3.2f)-reach*(kick*strength);
        if(d==0)grip.x+=4f;
        grip.y=Mathf.Clamp(grip.y,(d==0?35:33)+bob+lean.y,39);
        if(Vector2.Distance(shoulder,grip)>8.5f)grip=shoulder+(grip-shoulder).normalized*8.5f;
        grip=new Vector2(Mathf.Round(grip.x),Mathf.Round(grip.y));
        if(twoHand)
        {
            var support=clip==0?grip-reach*(1.5f+5.5f*Draw[f])+new Vector2(0,-.7f):grip+reach*(clip==1?2.5f:clip>=4?4f:3.2f);
            if(Vector2.Distance(far,support)>12f)support=far+(support-far).normalized*12f;
            support.y=Mathf.Min(39,support.y);
            Arm(pixels,far,support,d,clip==0?1.1f+Draw[f]:clip==4?.8f+Mathf.Sin(f/15f*Mathf.PI)*1.7f:1.1f,clip!=0&&(d==0||d==1||d==5));
        }
        else Arm(pixels,far,far+new Vector2(-forward.x*1.4f,4.3f),d,1.4f,d==0||d==1||d==5);
        Arm(pixels,shoulder,grip,d,clip==0?.65f:Mathf.Lerp(.5f,2.8f,kick),false);
        for(int y=0;y<33;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)Pixel(pixels,x+lean.x,y+lean.y,source[At(x,y)]);
        var pack=Packs[d];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(source[At(x,y)].a>0&&!Flesh(source[At(x,y)])&&!FarArmEdge(source,x,y))Pixel(pixels,x+lean.x,y+lean.y,source[At(x,y)]);
        grip+=new Vector2(-.5f,.5f);return pixels;
    }
    public static string Main()
    {
        if(Application.isPlaying||Directory.Exists(Output))throw new Exception("Stop play / preserve versioned candidates");
        var records=new JArray();var counts=new JObject();
        for(int clip=0;clip<Clips.Length;clip++)for(int d=0;d<6;d++)
        {
            var standing=new HashSet<string>();var idle0=Read(Source+"idle/"+Dirs[d]+"/00.png");
            for(int variant=0;variant<16;variant++)
            {
                string action=variant<8?"idle":"walk";int sourceFrame=variant%8;
                string sourcePath=Source+action+"/"+Dirs[d]+"/"+sourceFrame.ToString("D2")+".png";var src=Read(sourcePath);
                var hand=RuinRunner.SscaryHandSockets.Pixel(action,DirectionIndex[d],sourceFrame);int bob=Mathf.Clamp(HeadTop(src)-HeadTop(idle0),-1,1);
                for(int f=0;f<16;f++)
                {
                    int seam=d==0||d==1||d==5?39:37;string legPath=sourcePath;
                    if(variant<8 && clip>=4 && f>=2 && f<=10)legPath=Source+"walk/"+Dirs[d]+"/"+(f<=6?0:2).ToString("D2")+".png";
                    var legs=Read(legPath);Vector2 grip;Vector2Int lean;var pixels=Pose(src,legs,d,clip,f,bob,seam,out grip,out lean);
                    string path=Output+Clips[clip]+"/"+Dirs[d]+"/"+variant.ToString("D2")+"/"+f.ToString("D2")+".png";Save(pixels,path);string hash=Hash(pixels);if(variant==0)standing.Add(hash);
                    records.Add(new JObject{{"path",path},{"clip",Clips[clip]},{"direction",Dirs[d]},{"variant",variant},{"frame",f},{"phase",Phases[f]},{"source",sourcePath},{"sourcePixelsSha256",Hash(src)},{"legSource",legPath},{"legSeam",seam},{"pixelsSha256",hash},{"grip",new JArray(grip.x,grip.y)},{"lean",new JArray(lean.x,lean.y)}});
                }
            }
            counts[Clips[clip]+"/"+Dirs[d]]=standing.Count;
        }
        File.WriteAllText(Output+"manifest.json",new JObject{{"sourcePage","https://sscary.itch.io/the-adventurer-male"},{"scope","Six ranged joint clips: bow string-arm release/redraw, crossbow brace, one-hand pistol kick, rifle shoulder recoil, shotgun support cycle and cannon planted kick. Fixed source head/pack/feet/canvas. Ready and recovered endpoints match; idle/gait combinations are not new authored keys."},{"clips",new JArray(Clips)},{"phases",new JArray(Phases)},{"standUniquePoses",counts},{"frames",records}}.ToString(),new UTF8Encoding(false));
        return records.Count+" samples; stand poses "+counts;
    }
}
