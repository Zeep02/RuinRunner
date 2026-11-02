using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

// Native pixel authoring. The generated arm key is a pose reference only;
// none of its resampled pixels or changed face enter the game.
public static class AuthorSscaryThrust20261001
{
    const string Source="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/";
    const string Output="ArtDirection/Generated/SscaryMotion-20261001/thrust-v3/";
    static readonly string[] Dirs={"up","right_up","right_down","down","left_down","left_up"};
    static readonly int[] DirectionIndex={0,1,2,4,5,7};
    static readonly float[] Phases={0,.06f,.12f,.18f,.20f,.24f,.28f,.34f,.40f,.46f,.52f,.56f,.64f,.74f,.86f,1};
    // Explicit keyed joint paths, not duplicated files, alpha morphing, or runtime scaling.
    static readonly float[] Reach={0,-.6f,-1.0f,-1.5f,-1.75f,-.75f,1.25f,3.25f,4,4.25f,4.5f,4.1f,3.25f,2,.9f,0};
    static readonly float[] Bend={0,.4f,.8f,1.2f,1.4f,1.1f,.8f,.2f,-.3f,-.6f,-.4f,-.2f,0,.2f,.1f,0};
    static readonly Vector2[] Shoulders={new Vector2(27,32),new Vector2(28,32),new Vector2(22,32),new Vector2(21,32),new Vector2(27,32),new Vector2(21,32)};
    static readonly Vector2[] Rest={new Vector2(28,34.5f),new Vector2(30,34),new Vector2(23,34.5f),new Vector2(22,34.5f),new Vector2(27,37),new Vector2(21,36)};
    static readonly Vector2[] Forward={new Vector2(0,-1),new Vector2(.85f,-.53f),new Vector2(.95f,.31f),new Vector2(0,1),new Vector2(-.95f,.31f),new Vector2(-.85f,-.53f)};
    static readonly int[] BodyMin={19,25,23,21,21,21}, BodyMax={27,29,28,28,26,23};
    static readonly Color32 Clear=new Color32(0,0,0,0), Ink=new Color32(0,0,0,255), Skin=new Color32(229,149,159,255), SkinDark=new Color32(193,106,125,255), Cloth=new Color32(60,84,143,255), ClothDark=new Color32(50,65,127,255);
    static string Hash(Color32[] p){using(var s=SHA256.Create()){var b=new byte[p.Length*4];for(int i=0;i<p.Length;i++){b[i*4]=p[i].r;b[i*4+1]=p[i].g;b[i*4+2]=p[i].b;b[i*4+3]=p[i].a;}return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}}
    static Color32[] Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));if(t.width!=48||t.height!=64)throw new Exception(path);var src=t.GetPixels32();var top=new Color32[src.Length];for(int y=0;y<64;y++)Array.Copy(src,y*48,top,(63-y)*48,48);UnityEngine.Object.DestroyImmediate(t);return top;}
    static void Save(Color32[] p,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var bottom=new Color32[p.Length];for(int y=0;y<64;y++)Array.Copy(p,y*48,bottom,(63-y)*48,48);var t=new Texture2D(48,64,TextureFormat.RGBA32,false);t.SetPixels32(bottom);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
    static int At(int x,int y)=>y*48+x;
    static void Pixel(Color32[] p,int x,int y,Color32 c){if(x>=0&&x<48&&y>=0&&y<64)p[At(x,y)]=c;}
    static void StripHand(Color32[] p,int dir,Vector2 sourceHand)
    {
        // Retouch only the reviewed gripping arm region below the face. The backpack,
        // face and far hand remain original; the coat underneath the near arm is restored.
        int loX=Mathf.FloorToInt(sourceHand.x)-2,hiX=Mathf.CeilToInt(sourceHand.x)+1;
        int loY=Math.Max(32,Mathf.FloorToInt(sourceHand.y)-2),hiY=Math.Min(38,Mathf.CeilToInt(sourceHand.y)+1);
        for(int y=loY;y<=hiY;y++)for(int x=loX;x<=hiX;x++)
        {
            var c=p[At(x,y)];if(c.a==0)continue;
            // Preserve hair, straps, buckles and trouser pixels; flesh is replaced
            // with the local underlay. Arm-edge ink is cleared only beyond the torso.
            bool flesh=c.Equals(Skin)||c.Equals(SkinDark);
            bool edge=c.Equals(Ink)&&x<BodyMin[dir]||c.Equals(Ink)&&x>BodyMax[dir];
            if(flesh||edge)p[At(x,y)]=x>=BodyMin[dir]&&x<=BodyMax[dir]&&y<=36
                ?(dir==0?new Color32(110,39,73,255):Cloth):Clear;
        }
    }
    static void Line(Dictionary<Vector2Int,Color32> cells,Vector2 a,Vector2 b,Color32 shade,Color32 lit)
    {
        int steps=Math.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*3));
        for(int i=0;i<=steps;i++){var v=Vector2.Lerp(a,b,i/(float)steps);int x=Mathf.RoundToInt(v.x),y=Mathf.RoundToInt(v.y);cells[new Vector2Int(x,y)]=lit;cells[new Vector2Int(x,y+1)]=shade;}
    }
    static Vector2 Elbow(Vector2 s,Vector2 h,int dir,float bend)
    {
        Vector2 delta=h-s;float distance=Mathf.Max(.1f,delta.magnitude),along=distance*.46f;
        var perpendicular=new Vector2(-delta.y,delta.x)/distance;
        float side=dir==2||dir==3||dir==5?-1:1;
        return s+delta/distance*along+perpendicular*side*(Mathf.Lerp(2.1f,.55f,Mathf.InverseLerp(2,7,distance))+bend*.25f);
    }
    static Color32[] Pose(Color32[] source,int dir,int phase,Vector2 sourceHand,out Vector2 grip)
    {
        var result=(Color32[])source.Clone();StripHand(result,dir,sourceHand);
        var shift=sourceHand-Rest[dir];
        // Walking's old hand swings are not shoulder motion. Preserve only its native
        // one-pixel vertical body bob; the attack joint path owns the hand now.
        shift=new Vector2(0,Mathf.Clamp(Mathf.Round(shift.y),-1,1));
        var shoulder=Shoulders[dir]+shift;
        var forward=Forward[dir].normalized;var side=new Vector2(-forward.y,forward.x);
        // Foreshorten the reach along the ground's vertical axis. An unprojected
        // south thrust puts the fist at the feet instead of out in front of the waist.
        var projected=new Vector2(forward.x,forward.y*.55f);
        var projectedSide=new Vector2(side.x,side.y*.55f);
        grip=Rest[dir]+shift+projected*Reach[phase]+projectedSide*Bend[phase];
        grip.y=Mathf.Min(38,grip.y);
        if(Vector2.Distance(shoulder,grip)>7.4f)grip=shoulder+(grip-shoulder).normalized*7.4f;
        grip=new Vector2(Mathf.Round(grip.x),Mathf.Round(grip.y));
        var elbow=Elbow(shoulder,grip,dir,Bend[phase]);
        var cells=new Dictionary<Vector2Int,Color32>();
        Line(cells,shoulder,elbow,ClothDark,Cloth);Line(cells,elbow,grip,SkinDark,Skin);
        // A closed 2x2 gripping hand, using exactly the official skin palette.
        int hx=Mathf.RoundToInt(grip.x),hy=Mathf.RoundToInt(grip.y);
        cells[new Vector2Int(hx-1,hy)]=Skin;cells[new Vector2Int(hx,hy)]=Skin;
        cells[new Vector2Int(hx-1,hy+1)]=SkinDark;cells[new Vector2Int(hx,hy+1)]=SkinDark;
        var neighbor=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        foreach(var pair in cells)foreach(var n in neighbor){var p=pair.Key+n;if(!cells.ContainsKey(p))Pixel(result,p.x,p.y,Ink);}
        foreach(var pair in cells)Pixel(result,pair.Key.x,pair.Key.y,pair.Value);
        // Rear-facing arms pass behind the original head. Never redraw the face.
        for(int y=0;y<32;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)result[At(x,y)]=source[At(x,y)];
        grip=new Vector2(hx-.5f,hy+.5f);return result;
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before authoring");
        if(Directory.Exists(Output))throw new Exception("Preserve previous candidates; version the output");
        var records=new JArray();var unique=new JObject();int total=0;
        for(int d=0;d<Dirs.Length;d++)
        {
            var hashes=new HashSet<string>();
            for(int gait=-1;gait<8;gait++)
            {
                string action=gait<0?"idle":"walk",sourcePath=Source+action+"/"+Dirs[d]+"/"+(gait<0?0:gait).ToString("D2")+".png";
                var source=Read(sourcePath);var sourceHand=RuinRunner.SscaryHandSockets.Pixel(action,DirectionIndex[d],Math.Max(0,gait));
                for(int f=0;f<Phases.Length;f++)
                {
                    var basePixels=(Color32[])source.Clone();string legSource=sourcePath;
                    // A short planted step and recovery uses the official trouser/boot
                    // poses at native size. Moving attacks keep the independent gait.
                    int legFrame=f<4||f>=14?-1:f<8?0:f<12?2:4;
                    int seam=d==0||d==1||d==5?39:37;
                    if(gait<0&&legFrame>=0)
                    {
                        legSource=Source+"walk/"+Dirs[d]+"/"+legFrame.ToString("D2")+".png";
                        var legs=Read(legSource);Array.Copy(legs,seam*48,basePixels,seam*48,(64-seam)*48);
                    }
                    Vector2 grip;var pixels=Pose(basePixels,d,f,sourceHand,out grip);
                    string path=Output+(gait<0?"stand/":"move/")+Dirs[d]+"/"+(gait<0?"":gait.ToString("D2")+"/")+f.ToString("D2")+".png";
                    Save(pixels,path);var hash=Hash(pixels);if(gait<0)hashes.Add(hash);
                    records.Add(new JObject{{"path",path},{"source",sourcePath},{"sourcePixelsSha256",Hash(source)},{"legSource",legSource},{"legSourcePixelsSha256",Hash(Read(legSource))},{"legSeam",seam},{"pixelsSha256",hash},{"direction",Dirs[d]},{"gait",gait},{"phase",Phases[f]},{"frame",f},{"grip",new JArray(grip.x,grip.y)}});total++;
                }
            }
            unique[Dirs[d]]=hashes.Count;
        }
        File.WriteAllText(Output+"manifest.json",new JObject{{"sourcePage","https://sscary.itch.io/the-adventurer-male"},{"scope","Editor review only. Authored native thrust joint/closed-hand poses, 6 perspectives, 16 keyed phases, 8 original gait combinations. Gait combinations are not extra authored attack phases. No generated raster adopted; no image scaling/tween pixels/face edits."},{"phases",new JArray(Phases)},{"standUniquePoses",unique},{"frames",records}}.ToString(),new UTF8Encoding(false));
        return "Editor-only native candidates "+total+"; stand unique poses "+unique;
    }
}
