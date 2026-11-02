"""Generate the native Unity C# author/importer, without editing raster pixels in Python."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
src=(root/'Tools/Validation/AuthorSscaryDefense20261001.cs').read_text(encoding='utf-8')
head=src[:src.index('    static readonly float[] GuardWeights=')]
head=head.replace('AuthorSscaryDefense20261001','AuthorSscaryRanged20261001').replace('defense-v2','ranged-v7')
head=head.replace('"Guard","Parry","Charge","ChargeCut"','"Bow","Crossbow","Pistol","Rifle","Shotgun","Cannon"')
import re
head=re.sub(r'static readonly Vector2\[\] Shoulders=.*?;','static readonly Vector2[] Shoulders={new Vector2(27,32),new Vector2(28,32),new Vector2(28,32),new Vector2(21,32),new Vector2(21,32),new Vector2(21,32)};',head)
head=re.sub(r'static readonly Vector2\[\] FarShoulders=.*?;','static readonly Vector2[] FarShoulders={new Vector2(20,32),new Vector2(24,32),new Vector2(22,32),new Vector2(28,32),new Vector2(27,32),new Vector2(24,32)};',head)
body=r'''    static readonly float[] Kick={0,.35f,.7f,1,.94f,.83f,.7f,.58f,.47f,.37f,.28f,.2f,.13f,.07f,.03f,0};
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
'''
tail=src[src.index('    public static string Main()'):]
tail=tail.replace('Vector2 sourceHand,int bob','int bob')
a=tail.index('                    if(variant<8)');b=tail.index('                    var legs=Read(legPath);',a)
tail=tail[:a]+'''                    if(variant<8 && clip>=4 && f>=2 && f<=10)legPath=Source+"walk/"+Dirs[d]+"/"+(f<=6?0:2).ToString("D2")+".png";
'''+tail[b:]
tail=tail.replace('Pose(src,legs,d,clip,f,hand,bob,seam','Pose(src,legs,d,clip,f,bob,seam')
tail=tail.replace('Four distinct defense/preparation/release joint clips; original eight idle/eight gait variants are sample combinations, not additional authored keys. Fixed canvas/foot, rigid face/pack, source entry/recovery endpoints; Charge end and ChargeCut start match exactly.','Six ranged joint clips: bow string-arm release/redraw, crossbow brace, one-hand pistol kick, rifle shoulder recoil, shotgun support cycle and cannon planted kick. Fixed source head/pack/feet/canvas. Ready and recovered endpoints match; idle/gait combinations are not new authored keys.')
(root/'Tools/Validation/AuthorSscaryRanged20261001.cs').write_text(head+body+tail,encoding='utf-8')
imp=(root/'Tools/Validation/ImportSscaryDefense20261001.cs').read_text(encoding='utf-8')
imp=imp.replace('ImportSscaryDefense20261001','ImportSscaryRanged20261001').replace('SscaryDefenseFrames','SscaryRangedFrames').replace('Combat/Defense','Combat/RangedV7').replace('defense-v2','ranged-v7').replace('hero-defense','hero-ranged').replace('6144','9216')
imp=imp.replace('"Guard","Parry","Charge","ChargeCut"','"Bow","Crossbow","Pistol","Rifle","Shotgun","Cannon"').replace('Mathf.Clamp(clip,0,3)','Mathf.Clamp(clip,0,5)')
imp=imp.replace('Four distinct defense/preparation/release clips.','Six distinct ranged joint clips.').replace('guard/parry/charge/release joints','bow/bolt/pistol/rifle/shotgun/cannon joints')
imp=imp.replace('source and native modification records below.','native ranged modification records below.')
imp=imp.replace('private static readonly float[] CutPhases={.18f,.21f,.24f,.27f,.30f,.34f,.38f,.42f,.48f,.54f,.60f,.68f,.76f,.84f,.92f,1};\\n  ','')
imp=imp.replace('(clip==3?CutPhases:Phases)[i]','Phases[i]')
(root/'Tools/Validation/ImportSscaryRanged20261001.cs').write_text(imp,encoding='utf-8')
print('Generated native C# author and safe SpriteEditor importer.')
