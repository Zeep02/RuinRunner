using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using RuinRunner;

// External staging only. Re-author native joint poses from the current, repaired
// standing PNGs. This does not import, change a catalog, or modify gameplay.
public static class GoalHeroHurt20261009
{
    const string Native="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/";
    const string Standing="Assets/RuinRunner/ArtSources/Hero/WeaponStandingV4/";
    const string Root="ArtDirection/Generated/SscaryMotion-20261009/";
    const BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly string[] Dirs={"up","right_up","right_down","down","left_down","left_up"};
    static readonly int[] View={0,1,2,2,3,4,4,5};
    static readonly Vector2[] Near={new Vector2(27,32),new Vector2(28,32),new Vector2(28,32),new Vector2(21,32),new Vector2(21,32),new Vector2(21,32)};
    static readonly Vector2[] Far={new Vector2(20,32),new Vector2(24,32),new Vector2(22,32),new Vector2(28,32),new Vector2(27,32),new Vector2(24,32)};
    static readonly int[] BodyMin={19,25,23,21,21,21},BodyMax={27,29,28,28,26,23};
    static readonly RectInt[] Packs={new RectInt(19,32,9,7),new RectInt(17,30,11,9),new RectInt(18,30,4,9),new RectInt(),new RectInt(28,30,4,9),new RectInt(22,30,11,9)};
    static readonly float[] Withdraw={0,.65f,1,1,.82f,.6f,.35f,.1f,0};
    static readonly float[] LiftHand={0,-1,-2,-2,-1,0,.8f,.7f,0};
    static readonly float[] Bend={1.1f,2.1f,3.1f,3.6f,3.2f,2.4f,1.8f,1.4f,1.1f};
    static readonly float[] FreeLift={0,-1.2f,-2.5f,-3.5f,-3,-1.8f,-.4f,.8f,0};
    static readonly Color32 Clear=new Color32(0,0,0,0),Ink=new Color32(0,0,0,255),Skin=new Color32(229,149,159,255),SkinDark=new Color32(193,106,125,255),Cloth=new Color32(60,84,143,255),ClothDark=new Color32(50,65,127,255);
    static int abandonedOutlinePixels;
    static int At(int x,int y)=>y*48+x;
    static bool Flesh(Color32 color)=>color.Equals(Skin)||color.Equals(SkinDark);
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static string PixelHash(Color32[] p)
    {var b=new byte[p.Length*4];for(int i=0;i<p.Length;i++){b[i*4]=p[i].a==0?(byte)0:p[i].r;b[i*4+1]=p[i].a==0?(byte)0:p[i].g;b[i*4+2]=p[i].a==0?(byte)0:p[i].b;b[i*4+3]=p[i].a;}return Hash(b);}
    static Color32[] Read(string path)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(path))||texture.width!=48||texture.height!=64)throw new Exception("Expected approved native canvas: "+path);
            var bottom=texture.GetPixels32();var top=new Color32[bottom.Length];for(int y=0;y<64;y++)Array.Copy(bottom,y*48,top,(63-y)*48,48);return top;
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    static void Save(Color32[] pixels,string path)
    {
        var texture=new Texture2D(48,64,TextureFormat.RGBA32,false);
        try{var bottom=new Color32[pixels.Length];for(int y=0;y<64;y++)Array.Copy(pixels,y*48,bottom,(63-y)*48,48);texture.SetPixels32(bottom);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    static void Pixel(Color32[] pixels,int x,int y,Color32 color){if(x>=0&&x<48&&y>=0&&y<64)pixels[At(x,y)]=color;}
    static int HeadTop(Color32[] pixels){for(int y=0;y<32;y++)for(int x=0;x<48;x++)if(pixels[At(x,y)].a>0)return y;throw new Exception("No native head");}
    static bool IdentityPixel(Color32[] source,Color32[] idle,int x,int y,int bob)
    {
        if(source[At(x,y)].a==0)return false;
        // Raised weapon hands reach rows 31 and 32. A rectangular "head" copy
        // reintroduces them after reposing; compare the approved idle silhouette.
        if(y<31+bob)return true;
        return y<33+bob&&y-bob>=0&&source[At(x,y)].Equals(idle[At(x,y-bob)]);
    }
    static bool OldArmColor(Color32[] source,Color32[] idle,int x,int y,int d,int bob)
    {
        if(y<31+bob||y>=40||IdentityPixel(source,idle,x,y,bob))return false;
        var color=source[At(x,y)];
        return Flesh(color)||((color.Equals(Cloth)||color.Equals(ClothDark))&&(x<BodyMin[d]||x>BodyMax[d]));
    }
    static bool ArmEdge(Color32[] source,Color32[] idle,int x,int y,int d,int bob)
    {
        if(!source[At(x,y)].Equals(Ink)||IdentityPixel(source,idle,x,y,bob))return false;
        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            if(x+dx>=0&&x+dx<48&&y+dy>=0&&y+dy<40&&OldArmColor(source,idle,x+dx,y+dy,d,bob))return true;
        return false;
    }
    static void Line(Dictionary<Vector2Int,Color32> cells,Vector2 from,Vector2 to,Color32 shade,Color32 light)
    {
        int steps=Math.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)*3));
        for(int i=0;i<=steps;i++){var p=Vector2.Lerp(from,to,i/(float)steps);int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y);cells[new Vector2Int(x,y)]=light;cells[new Vector2Int(x,y+1)]=shade;}
    }
    static void Arm(Color32[] pixels,Vector2 shoulder,Vector2 hand,int view,float bend,bool behind)
    {
        var delta=hand-shoulder;var perpendicular=new Vector2(-delta.y,delta.x)/Mathf.Max(.1f,delta.magnitude);
        var elbow=shoulder+delta*.46f+perpendicular*(view==2||view==3||view==5?-1:1)*bend;
        var cells=new Dictionary<Vector2Int,Color32>();Line(cells,shoulder,elbow,ClothDark,Cloth);Line(cells,elbow,hand,SkinDark,Skin);
        int hx=Mathf.RoundToInt(hand.x),hy=Mathf.RoundToInt(hand.y);
        cells[new Vector2Int(hx-1,hy)]=cells[new Vector2Int(hx,hy)]=Skin;cells[new Vector2Int(hx-1,hy+1)]=cells[new Vector2Int(hx,hy+1)]=SkinDark;
        var neighbors=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        foreach(var c in cells)foreach(var d in neighbors){var p=c.Key+d;if(!cells.ContainsKey(p)&&(!behind||pixels[At(Mathf.Clamp(p.x,0,47),Mathf.Clamp(p.y,0,63))].a==0))Pixel(pixels,p.x,p.y,Ink);}
        foreach(var c in cells)if(!behind||pixels[At(Mathf.Clamp(c.Key.x,0,47),Mathf.Clamp(c.Key.y,0,63))].a==0)Pixel(pixels,c.Key.x,c.Key.y,c.Value);
    }
    static bool BackpackInk(Color32[] source,int x,int y,int d)
    {
        // A down-reaching off hand can overlap the rectangular pack bounds.
        // Its black outline is not backpack anatomy; protect only real bag ink.
        var pack=Packs[d];
        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
        {
            int xx=x+dx,yy=y+dy;if(!pack.Contains(new Vector2Int(xx,yy)))continue;var c=source[At(xx,yy)];
            if(c.Equals(new Color32(110,39,73,255))||c.Equals(new Color32(149,66,106,255))||c.Equals(new Color32(81,28,58,255)))return true;
        }
        return false;
    }
    static void RemoveAbandonedOutline(Color32[] source,Color32[] idle,Color32[] result,Vector2Int lean,int d,int bob)
    {
        var seen=new bool[result.Length];var pack=Packs[d];
        for(int start=0;start<result.Length;start++)
        {
            if(seen[start]||result[start].a==0)continue;
            bool abandoned=true;var queue=new Queue<int>();var component=new List<int>();queue.Enqueue(start);seen[start]=true;
            while(queue.Count>0)
            {
                int current=queue.Dequeue(),x=current%48,y=current/48;component.Add(current);
                int sx=x-lean.x,sy=y-lean.y;
                bool original=sx>=0&&sx<48&&sy>=30&&sy<40&&source[At(sx,sy)].Equals(Ink);
                bool identity=original&&IdentityPixel(source,idle,sx,sy,bob);
                bool backpack=original&&pack.Contains(new Vector2Int(sx,sy))&&BackpackInk(source,sx,sy,d)&&!OldArmColor(source,idle,sx,sy,d,bob)&&!ArmEdge(source,idle,sx,sy,d,bob);
                abandoned&=result[current].Equals(Ink)&&y>=30&&y<40&&original&&!identity&&!backpack;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int xx=x+dx,yy=y+dy;if(xx<0||xx>=48||yy<0||yy>=64)continue;int next=At(xx,yy);
                    if(!seen[next]&&result[next].a>0){seen[next]=true;queue.Enqueue(next);}
                }
            }
            // Only a now-disconnected, all-black component copied from the old
            // arm can be removed. Never erase colored anatomy, source identity,
            // backpack, feet, or a new joint pixel to make validation pass.
            if(abandoned)foreach(int index in component){result[index]=Clear;abandonedOutlinePixels++;}
        }
    }
    static Color32[] Recoil(Color32[] source,Color32[] idle,int profile,int direction,int key,Vector2 initial,out Vector2 grip,out Vector2Int lean)
    {
        int d=View[direction];lean=Vector2Int.zero;grip=initial;if(key==0||key==8)return (Color32[])source.Clone();
        float weight=Withdraw[key];var reach=new Vector2(Mathf.Sin(direction*Mathf.PI/4),-Mathf.Cos(direction*Mathf.PI/4)*.45f);
        lean=new Vector2Int(Mathf.RoundToInt(-reach.x*weight),key>=2&&key<=4?1:0);
        int bob=Mathf.Clamp(HeadTop(source)-HeadTop(idle),-1,1);
        var shoulder=Near[d]+new Vector2(0,bob)+(Vector2)lean;var far=Far[d]+new Vector2(0,bob)+(Vector2)lean;
        var hand=initial-new Vector2(-.5f,.5f)+(Vector2)lean-reach*(profile==5?1.8f:2.5f)*weight+new Vector2(0,LiftHand[key]);
        hand.y=Mathf.Max(32+bob+lean.y,hand.y);hand=new Vector2(Mathf.Round(hand.x),Mathf.Round(hand.y));
        var support=profile==2?hand-reach*.7f:profile==3?hand+reach*2.8f:profile==5?hand+reach*3.2f:far+new Vector2(-reach.x*(1.3f+2*weight),3+FreeLift[key]);
        support.y=Mathf.Max(32+bob+lean.y,support.y);
        var stripped=(Color32[])source.Clone();
        for(int y=31+bob;y<40;y++)for(int x=0;x<48;x++)if(OldArmColor(source,idle,x,y,d,bob)||ArmEdge(source,idle,x,y,d,bob))stripped[At(x,y)]=x>=BodyMin[d]&&x<=BodyMax[d]&&y<=36?(d==0?new Color32(110,39,73,255):Cloth):Clear;
        var result=new Color32[source.Length];
        for(int y=0;y<40;y++)for(int x=0;x<48;x++)if(stripped[At(x,y)].a>0)Pixel(result,x+lean.x,y+lean.y,stripped[At(x,y)]);
        Arm(result,far,support,d,Bend[key]*.8f,d==0||d==1||d==5);Arm(result,shoulder,hand,d,Bend[key],false);
        // Rigid approved face and backpack; the shoulders/elbows articulate around them.
        for(int y=0;y<33+bob;y++)for(int x=0;x<48;x++)if(IdentityPixel(source,idle,x,y,bob))Pixel(result,x+lean.x,y+lean.y,source[At(x,y)]);
        var pack=Packs[d];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(source[At(x,y)].a>0&&!OldArmColor(source,idle,x,y,d,bob)&&!ArmEdge(source,idle,x,y,d,bob))Pixel(result,x+lean.x,y+lean.y,source[At(x,y)]);
        // Copy the already repaired lower strip byte-for-byte, including transparency.
        Array.Copy(source,40*48,result,40*48,24*48);RemoveAbandonedOutline(source,idle,result,lean,d,bob);grip=hand+new Vector2(-.5f,.5f);return result;
    }
    static void Validate(Color32[] source,Color32[] idle,Color32[] result,Vector2Int lean,int direction,string label)
    {
        int bob=Mathf.Clamp(HeadTop(source)-HeadTop(idle),-1,1),d=View[direction];
        for(int y=0;y<33+bob;y++)for(int x=0;x<48;x++)if(IdentityPixel(source,idle,x,y,bob)&&!source[At(x,y)].Equals(result[At(x+lean.x,y+lean.y)]))throw new Exception("Face/identity changed "+label);
        var pack=Packs[d];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)
            if(source[At(x,y)].a>0&&(!source[At(x,y)].Equals(Ink)||BackpackInk(source,x,y,d))&&!OldArmColor(source,idle,x,y,d,bob)&&!ArmEdge(source,idle,x,y,d,bob)&&!source[At(x,y)].Equals(result[At(x+lean.x,y+lean.y)]))throw new Exception("Approved backpack changed "+label);
        for(int i=40*48;i<source.Length;i++)if(!source[i].Equals(result[i]))throw new Exception("Approved foot strip changed "+label);
        foreach(var color in result)if(color.a!=0&&color.a!=255)throw new Exception("Partial alpha introduced "+label);
        var visited=new bool[result.Length];
        for(int start=0;start<result.Length;start++)
        {
            if(visited[start]||result[start].a==0)continue;
            bool blackOnly=true,armBand=true;var queue=new Queue<int>();queue.Enqueue(start);visited[start]=true;
            while(queue.Count>0)
            {
                int current=queue.Dequeue(),x=current%48,y=current/48;
                blackOnly&=result[current].Equals(Ink);armBand&=y>=30&&y<40;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int xx=x+dx,yy=y+dy;if(xx<0||xx>=48||yy<0||yy>=64)continue;int next=At(xx,yy);
                    if(!visited[next]&&result[next].a>0){visited[next]=true;queue.Enqueue(next);}
                }
            }
            if(blackOnly&&armBand)throw new Exception("Detached black arm component "+label+" at "+start%48+","+start/48);
        }
    }
    public static object Main()=>Prepare(false);
    public static object Full()=>Prepare(true);
    static object Prepare(bool full)
    {
        if(Application.isPlaying)throw new Exception("Stop Play; author only outside Assets.");
        abandonedOutlinePixels=0;
        string output=Root+(full?"hurt-native-v5-full/":"hurt-native-v5-preview/");
        if(Directory.Exists(output))throw new Exception("Preserve prior candidate: "+output);
        var cells=(int[])typeof(WeaponStandingFrames).GetField("Cells",Flags).GetValue(null);
        var hands=(Vector2[])typeof(WeaponStandingFrames).GetField("Hands",Flags).GetValue(null);
        var behind=(bool[])typeof(WeaponStandingFrames).GetField("Behind",Flags).GetValue(null);
        if(cells.Length!=6*8*16*9||hands.Length!=cells.Length||behind.Length!=cells.Length)throw new Exception("Current standing contract changed.");
        var baseline=new Dictionary<string,string>();
        foreach(JObject row in JObject.Parse(File.ReadAllText(Root+"hurt-native-v4-full/manifest.json",Encoding.UTF8))["frames"])
            baseline[row["profile"]+"/"+row["direction"]+"/"+row["variant"]+"/"+row["key"]]=row.Value<string>("visiblePixelSha256");
        Directory.CreateDirectory(output);var records=new JArray();var unique=new Dictionary<string,int>();var sourceHashes=new Dictionary<string,string>();var poseCounts=new JObject();
        for(int profile=0;profile<7;profile++)for(int direction=0;direction<8;direction++)for(int variant=0;variant<(full?16:1);variant++)
        {
            int slot=((Math.Min(profile,5)*8+direction)*16+variant)*9+8;
            string nativeAction=variant<8?"idle":"walk";
            string sourcePath=profile<6?Standing+cells[slot].ToString("D5")+".png":Native+nativeAction+"/"+Dirs[View[direction]]+"/"+(variant%8).ToString("D2")+".png";
            var initial=profile<6?hands[slot]:SscaryHandSockets.Pixel(nativeAction,direction,variant%8);bool handBehind=profile<6&&behind[slot];
            var source=Read(sourcePath);sourceHashes[sourcePath]=Hash(File.ReadAllBytes(sourcePath));var idle=Read(Native+"idle/"+Dirs[View[direction]]+"/00.png");var poses=new HashSet<string>();
            for(int key=0;key<9;key++)
            {
                string label=profile+"/"+direction+"/"+variant+"/"+key;
                int priorOutlines=abandonedOutlinePixels;var pixels=Recoil(source,idle,profile,direction,key,initial,out var grip,out var lean);Validate(source,idle,pixels,lean,direction,label);
                string hash=PixelHash(pixels);if(profile<6&&baseline[label]!=hash)throw new Exception("Approved weapon hurt changed: "+label);
                poses.Add(hash);if(!unique.TryGetValue(hash,out int index)){index=unique.Count;unique.Add(hash,index);Save(pixels,output+index.ToString("D5")+".png");}
                records.Add(new JObject{{"profile",profile},{"direction",direction},{"variant",variant},{"key",key},{"runtimeIndex",index},{"path",output+index.ToString("D5")+".png"},{"source",sourcePath},{"sourcePngSha256",sourceHashes[sourcePath]},{"visiblePixelSha256",hash},{"grip",new JArray(grip.x,grip.y)},{"behind",handBehind},{"lean",new JArray(lean.x,lean.y)},{"abandonedSourceOutlinePixels",abandonedOutlinePixels-priorOutlines}});
            }
            if(poses.Count<4)throw new Exception("Fewer than four distinct articulated poses: "+profile+"/"+direction+"/"+variant);
            if(variant==0)poseCounts[profile+"/"+direction]=poses.Count;
        }
        foreach(var source in sourceHashes)if(Hash(File.ReadAllBytes(source.Key))!=source.Value)throw new Exception("Original modified during authoring.");
        var report=new JObject{{"status","CANDIDATE ONLY; no Runtime/catalog adoption or animation acceptance"},{"fullGaitVariants",full},{"profiles",7},{"preservedWeaponKeys",6*8*(full?16:1)*9},{"unarmedKeys",8*(full?16:1)*9},{"uniqueSprites",unique.Count},{"poseCountsPerStandingView",poseCounts},{"approvedFootStripPreserved",true},{"faceAndBackpackSourcePreserved",true},{"detachedBlackArmComponents",0},{"abandonedSourceOutlinePixels",abandonedOutlinePixels},{"identityMask","approved idle head silhouette; raised old hands excluded"},{"nominalHurtSeconds",.18f},{"nativeCanvas",new JArray(48,64)},{"pivotPixels",new JArray(24,22)},{"pixelsPerUnit",32},{"frames",records}};
        File.WriteAllText(output+"manifest.json",report.ToString(),new UTF8Encoding(false));return new{output,frames=records.Count,uniqueSprites=unique.Count,full};
    }
}
