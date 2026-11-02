"""Revise the existing native joint authoring pipeline; preserve source and previous candidates."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/AuthorSscaryMelee20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static Color32[] Pose(')]
t=t.replace('AuthorSscaryMelee20261001','AuthorArmedGrip20261001').replace('melee-v2/','armed-grip-v3/')
t=t.replace('using Newtonsoft.Json.Linq;','using Newtonsoft.Json.Linq;\nusing RuinRunner;\nusing UnityEditor;')
t+=r'''    static readonly Vector2[] Carry={new Vector2(31,34),new Vector2(34,34),new Vector2(33,34),new Vector2(18,34),new Vector2(16,34),new Vector2(15,34)};
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
'''
(root/'Tools/Validation/AuthorArmedGrip20261001.cs').write_text(t,encoding='utf-8')
print('Generated guarded native joint-code revision; originals and old body poses untouched.')
