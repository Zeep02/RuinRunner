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
public static class AuthorArmedGrip20261001
{
    const string Source="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/";
    const string Output="ArtDirection/Generated/SscaryMotion-20261001/armed-grip-v3/";
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
    static readonly Vector2[] Carry={new Vector2(31,34),new Vector2(34,34),new Vector2(33,34),new Vector2(18,34),new Vector2(16,34),new Vector2(15,34)};
    static readonly Vector2[] Near={new Vector2(27,32),new Vector2(28,32),new Vector2(28,32),new Vector2(21,32),new Vector2(21,32),new Vector2(21,32)};
    static readonly Vector2[] Far={new Vector2(20,32),new Vector2(24,32),new Vector2(22,32),new Vector2(28,32),new Vector2(27,32),new Vector2(24,32)};
    static readonly string[] Kinds={"Carry","Melee","Short","Thrust","Defense","Ranged"};
    static readonly int[] ClipCounts={2,3,2,1,4,6},VariantCounts={16,16,16,9,16,16},KeyCounts={1,16,16,16,16,16};
    static readonly float[] Kick={0,.35f,.7f,1,.94f,.83f,.7f,.58f,.47f,.37f,.28f,.2f,.13f,.07f,.03f,0};
    static readonly float[] Draw={1,.8f,.55f,.3f,.08f,0,.04f,.12f,.24f,.38f,.53f,.68f,.8f,.9f,.96f,1};
    static readonly float[] Magnitude={2.7f,2.2f,3.7f,2.6f,4.6f,6};
    static Color32[] Retarget(Color32[] source,Color32[] neutral,Color32[] idle0,Vector2 oldHand,Vector2 oldNeutral,int d,int kind,int clip,int key,bool twoHand,out Vector2 grip)
    {
        int bob=Mathf.Clamp(HeadTop(neutral)-HeadTop(idle0),-1,1);
        var carry=Carry[d]+new Vector2(0,bob);var delta=oldHand-oldNeutral;
        // Follow the existing authored joints; remove the low hip anchoring.
        // Keep forward reach and recoil, but foreshorten downward travel above
        // the trousers. Both endpoints share the same ready hands and pose.
        var target=carry+new Vector2(delta.x*.75f,delta.y*.65f);
        var reach=new Vector2(Forward[d].normalized.x,Forward[d].normalized.y*.5f);
        if(kind==5)target=carry-reach*(Kick[key]*Magnitude[clip]);
        target.y=Mathf.Clamp(target.y,31.5f+bob,35f+bob);
        var shoulder=Near[d]+new Vector2(0,bob);
        if(Vector2.Distance(shoulder,target)>8.5f)target=shoulder+(target-shoulder).normalized*8.5f;
        target=new Vector2(Mathf.Round(target.x),Mathf.Round(target.y));
        var pixels=(Color32[])source.Clone();
        for(int y=33;y<40;y++)for(int x=0;x<48;x++)
            if(Flesh(source[At(x,y)])||FarArmEdge(source,x,y))pixels[At(x,y)]=x>=BodyMin[d]&&x<=BodyMax[d]&&y<=36?(d==0?new Color32(110,39,73,255):Cloth):Clear;
        var far=Far[d]+new Vector2(0,bob);
        if(twoHand)
        {
            var support=kind==5?(clip==0?target-reach*(1.5f+5.5f*Draw[key])+new Vector2(0,-.7f):target+reach*(clip==1?2.5f:clip>=4?4f:3.2f)):target+new Vector2(Forward[d].x*1.2f,Forward[d].y*.5f);
            if(Vector2.Distance(far,support)>12f)support=far+(support-far).normalized*12f;
            float farBend=kind==5?clip==0?1.1f+Draw[key]:clip==4?.8f+Mathf.Sin(key/15f*Mathf.PI)*1.7f:1.1f:1;
            Arm(pixels,far,support,d,farBend,d==0||d==1||d==5);
        }
        else Arm(pixels,far,far+new Vector2(-Forward[d].x,3),d,1.3f,d==0||d==1||d==5);
        float nearBend=kind==5?clip==0?.65f:Mathf.Lerp(.5f,2.8f,Kick[key]):Mathf.Lerp(1.8f,.6f,Mathf.InverseLerp(2,8,Vector2.Distance(shoulder,target)));
        Arm(pixels,shoulder,target,d,nearBend,false);
        // Retain every original opaque face/hair pixel and backpack/strap.
        for(int y=0;y<33;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)Pixel(pixels,x,y,source[At(x,y)]);
        var pack=Packs[d];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(source[At(x,y)].a>0&&!Flesh(source[At(x,y)])&&!FarArmEdge(source,x,y))Pixel(pixels,x,y,source[At(x,y)]);
        grip=target+new Vector2(-.5f,.5f);return pixels;
    }
    static Sprite Original(int kind,int clip,int d,int variant,int key,out Vector2 hand)
    {
        bool behind;int direction=DirectionIndex[d];
        if(kind==0){string action=variant<8?"idle":"walk";hand=SscaryHandSockets.Pixel(action,direction,variant%8);return AssetDatabase.LoadAssetAtPath<Sprite>(Source+action+"/"+Dirs[d]+"/"+(variant%8).ToString("D2")+".png");}
        if(kind==1)return SscaryMeleeFrames.Sample(clip,direction,variant,key,out hand,out behind);
        if(kind==2)return SscaryShortBladeFrames.Sample(clip,direction,variant,key,out hand,out behind);
        if(kind==3)return SscaryThrustFrames.Sample(direction,variant-1,key,out hand);
        if(kind==4)return SscaryDefenseFrames.Sample(clip,direction,variant,key,out hand,out behind);
        return SscaryRangedFrames.Sample(clip,direction,variant,key,out hand,out behind);
    }
    public static object Main()
    {
        if(Application.isPlaying||Directory.Exists(Output))throw new Exception("Stop play / preserve versioned candidates");
        Directory.CreateDirectory(Output);var records=new JArray();var unique=new Dictionary<string,int>();var cache=new Dictionary<string,Color32[]>();
        var counts=new JObject();
        for(int kind=0;kind<6;kind++)for(int clip=0;clip<ClipCounts[kind];clip++)for(int d=0;d<6;d++)
        {
            var standing=new HashSet<string>();var idle0=Read(Source+"idle/"+Dirs[d]+"/00.png");
            for(int variant=0;variant<VariantCounts[kind];variant++)
            {
                Vector2 oldNeutral;var rest=Original(kind,clip,d,variant,kind==5?15:0,out oldNeutral);var neutral=Read(AssetDatabase.GetAssetPath(rest));
                for(int key=0;key<KeyCounts[kind];key++)
                {
                    Vector2 oldHand;var sprite=Original(kind,clip,d,variant,key,out oldHand);string sourcePath=AssetDatabase.GetAssetPath(sprite);
                    Color32[] source;if(!cache.TryGetValue(sourcePath,out source))cache[sourcePath]=source=Read(sourcePath);
                    bool twoHand=kind==0?clip==1:kind==1?clip==2:kind==4?clip>=2:kind==5?clip!=2:false;
                    Vector2 grip;var pixels=Retarget(source,neutral,idle0,oldHand,oldNeutral,d,kind,clip,key,twoHand,out grip);string hash=Hash(pixels);int index;
                    if(!unique.TryGetValue(hash,out index)){index=unique.Count;unique.Add(hash,index);Save(pixels,Output+index.ToString("D5")+".png");}
                    if(variant==0)standing.Add(hash);
                    records.Add(new JObject{{"kind",kind},{"kindName",Kinds[kind]},{"clip",clip},{"direction",d},{"variant",variant},{"key",key},{"runtimeIndex",index},
                        {"path",Output+index.ToString("D5")+".png"},{"source",sourcePath},{"sourceSha256",Hash(source)},{"sha256",hash},{"oldGrip",new JArray(oldHand.x,oldHand.y)},{"grip",new JArray(grip.x,grip.y)},{"twoHand",twoHand}});
                }
            }
            counts[Kinds[kind]+"/"+clip+"/"+Dirs[d]]=standing.Count;
        }
        File.WriteAllText(Output+"manifest.json",new JObject{{"sourcePage","https://sscary.itch.io/the-adventurer-male"},{"scope","Revision of the existing native joint code: raised ready/recovery hands and lower-direction foreshortening, same exact face/pack/gait/body poses. Sample combinations are not new authored keys."},{"uniqueSprites",unique.Count},{"clipCounts",new JArray(ClipCounts)},{"variantCounts",new JArray(VariantCounts)},{"keyCounts",new JArray(KeyCounts)},{"standUnique",counts},{"frames",records}}.ToString(),new UTF8Encoding(false));
        return new{samples=records.Count,unique=unique.Count,output=Output};
    }
}
