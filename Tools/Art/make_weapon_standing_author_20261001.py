"""Separate armed standing profiles in the existing native joint-code pipeline."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/AuthorSscaryMelee20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static Color32[] Pose(')]
t=t.replace('AuthorSscaryMelee20261001','AuthorWeaponStanding20261001').replace('melee-v2/','weapon-standing-v1/')
t=t.replace('using Newtonsoft.Json.Linq;','using Newtonsoft.Json.Linq;\nusing RuinRunner;\nusing UnityEditor;')
t+=r'''    static readonly Vector2[] Near={new Vector2(27,32),new Vector2(28,32),new Vector2(28,32),new Vector2(21,32),new Vector2(21,32),new Vector2(21,32)};
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
                Vector2 grip;var pixels=Pose(source,idle0,profile,direction,key,hand,out grip);string hash=Hash(pixels);int index;
                if(!unique.TryGetValue(hash,out index)){index=unique.Count;unique.Add(hash,index);Save(pixels,Output+index.ToString("D5")+".png");}
                records.Add(new JObject{{"profile",profile},{"profileName",Profiles[profile]},{"direction",direction},{"variant",variant},{"key",key},{"runtimeIndex",index},{"path",Output+index.ToString("D5")+".png"},{"source",sourcePath},{"sourceSha256",Hash(source)},{"sha256",hash},{"grip",new JArray(grip.x,grip.y)}});
            }
        }
        File.WriteAllText(Output+"manifest.json",new JObject{{"uniqueSprites",unique.Count},{"scope","Six separate native armed standing/short return profiles. Original eight breathing/gait variants retained;9 return keys are not counted as nine new idle poses. Bow off-hand relaxes instead of holding permanent draw."},{"frames",records}}.ToString(),new UTF8Encoding(false));
        return new{samples=records.Count,unique=unique.Count,output=Output};
    }
}
'''
(root/'Tools/Validation/AuthorWeaponStanding20261001.cs').write_text(t,encoding='utf-8')
print('Generated six native weapon standing profiles with original head/pack/gait and short return keys.')

