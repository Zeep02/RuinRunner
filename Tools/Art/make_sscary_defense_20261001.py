"""Build native, editable joint author/import tools. No raster resampling."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
src=(root/'Tools/Validation/AuthorSscaryMelee20261001.cs').read_text(encoding='utf-8')
src=src.replace('AuthorSscaryMelee20261001','AuthorSscaryDefense20261001').replace('melee-v2','defense-v2')
src=src.replace('"Sweep","Reverse","Cleave"','"Guard","Parry","Charge","ChargeCut"')
a=src.index('    static Color32[] Pose(');b=src.index('    public static string Main()',a)
src=src[:a]+'''    static readonly float[] GuardWeights={0,.06f,.14f,.24f,.36f,.48f,.61f,.73f,.83f,.91f,.96f,.99f,1,1,1,1};
    static readonly float[] ParryWeights={0,.2f,.5f,.8f,1,1,1,1,.9f,.7f,.5f,.3f,.15f,.05f,0,0};
    static readonly float[] ChargeWeights={0,.07f,.15f,.23f,.32f,.4f,.48f,.56f,.64f,.72f,.8f,.87f,.92f,.96f,.99f,1};
    static readonly float[] CutReach={-2.6f,-2.9f,-3.1f,-3.2f,-2.6f,-2.1f,-.8f,1.8f,3,3.3f,2.8f,2.2f,1.6f,1,.5f,0};
    static readonly float[] CutLift={-2.4f,-2.5f,-2.6f,-2.7f,-2.4f,-2,-1.2f,0,.8f,1.2f,1,.8f,.5f,.3f,.1f,0};
    static readonly float[] CutSpread={2,2.3f,2.6f,2.8f,3,3.2f,3.4f,3.4f,3.7f,3.2f,2.8f,2.3f,1.6f,1,.5f,0};
    static Color32[] Pose(Color32[] source,Color32[] legs,int d,int clip,int f,Vector2 sourceHand,int bob,int seam,out Vector2 grip,out Vector2Int lean)
    {
        lean=Vector2Int.zero;grip=sourceHand;
        if(clip!=3&&f==0 || (clip==1||clip==3)&&f==15)return (Color32[])source.Clone();
        bool twoHand=clip!=1;var forward=Forward[d].normalized;
        float weight=clip==0?GuardWeights[f]:clip==1?ParryWeights[f]:clip==2?ChargeWeights[f]:f<11?1:Weights[f];
        if(clip==2)lean=new Vector2Int(Mathf.RoundToInt(-forward.x*weight),f>=6&&f<=11?1:0);
        else if(clip==3)lean=new Vector2Int(Mathf.RoundToInt(forward.x*(f<7?-1:1)*weight),f>=7&&f<=10?1:0);
        else if(clip==1)lean=new Vector2Int(Mathf.RoundToInt(forward.x*(f<8?-.8f:.7f)*weight),0);
        var stripped=(Color32[])source.Clone();StripHand(stripped,d,sourceHand);
        if(twoHand)for(int y=33;y<40;y++)for(int x=0;x<48;x++)
            if(Flesh(source[At(x,y)])||FarArmEdge(source,x,y))stripped[At(x,y)]=x>=BodyMin[d]&&x<=BodyMax[d]&&y<=36?(d==0?new Color32(110,39,73,255):Cloth):Clear;
        var pixels=new Color32[source.Length];
        for(int y=0;y<seam;y++)for(int x=0;x<48;x++)if(stripped[At(x,y)].a>0)Pixel(pixels,x+lean.x,y+lean.y,stripped[At(x,y)]);
        Array.Copy(legs,seam*48,pixels,seam*48,(64-seam)*48);
        var shoulder=Shoulders[d]+new Vector2(0,bob)+(Vector2)lean;
        var rest=Rest[d]+new Vector2(0,bob)+(Vector2)lean;
        if(clip==2||clip==3)
        {
            var target=rest+new Vector2(forward.x,forward.y*.48f)*(clip==2?-2.6f:CutReach[f])+Vector2.up*(clip==2?-2.4f:CutLift[f]);
            if(d==0)target.x+=clip==2?2:CutSpread[f];
            grip=clip==2?Vector2.Lerp(sourceHand+(Vector2)lean,target,weight):target;
        }
        else
        {
            var target=shoulder+new Vector2(forward.x,forward.y*.45f)*(clip==0?3.8f:f>=4&&f<=7?5.1f:3.7f)+new Vector2(0,clip==0?2.8f:2.2f);
            grip=Vector2.Lerp(sourceHand+(Vector2)lean,target,weight);
            if(clip==0)grip+=new Vector2(-forward.y,forward.x*.45f)*Mathf.Sin(weight*Mathf.PI)*2;
        }
        grip.y=Mathf.Min(39,grip.y);if(Vector2.Distance(shoulder,grip)>7.4f)grip=shoulder+(grip-shoulder).normalized*7.4f;
        grip=new Vector2(Mathf.Round(grip.x),Mathf.Round(grip.y));
        if(twoHand)
        {
            var far=FarShoulders[d]+new Vector2(0,bob)+(Vector2)lean;
            var support=grip-new Vector2(forward.x*.9f,forward.y*.4f);
            if(clip==0||clip==2)support=Vector2.Lerp(far+new Vector2(0,3),support,weight);
            Arm(pixels,far,support,d,1.2f,d==0||d==1||d==5);
        }
        Arm(pixels,shoulder,grip,d,Mathf.Lerp(2,.6f,Mathf.InverseLerp(2,7,Vector2.Distance(shoulder,grip))),false);
        for(int y=0;y<33;y++)for(int x=0;x<48;x++)if(source[At(x,y)].a>0)Pixel(pixels,x+lean.x,y+lean.y,source[At(x,y)]);
        var pack=Packs[d];for(int y=pack.yMin;y<pack.yMax;y++)for(int x=pack.xMin;x<pack.xMax;x++)if(source[At(x,y)].a>0&&!Flesh(source[At(x,y)])&&!FarArmEdge(source,x,y))Pixel(pixels,x+lean.x,y+lean.y,source[At(x,y)]);
        grip+=new Vector2(-.5f,.5f);return pixels;
    }
''' +src[b:]
src=src.replace('if(variant<8&&f>=4&&f<=13)legPath=Source+"walk/"+Dirs[d]+"/"+(f<8?0:f<12?2:4).ToString("D2")+".png";', '''if(variant<8)
                    {
                        int plant=clip==0?(f>=9?0:f>=5?2:f>=2?4:-1):clip==2?(f>=10?0:f>=6?2:f>=2?4:-1):clip==3?(f<=6?0:f<=10?2:f<=13?4:-1):(f>=4&&f<=7?0:f>=8&&f<=11?2:-1);
                        if(plant>=0)legPath=Source+"walk/"+Dirs[d]+"/"+plant.ToString("D2")+".png";
                    }''')
src=src.replace('Three native joint clips;','Four distinct defense/preparation/release joint clips;').replace('exact source endpoints.','source entry/recovery endpoints; Charge end and ChargeCut start match exactly.')
(root/'Tools/Validation/AuthorSscaryDefense20261001.cs').write_text(src,encoding='utf-8')
imp=(root/'Tools/Validation/ImportSscaryMelee20261001.cs').read_text(encoding='utf-8')
imp=imp.replace('ImportSscaryMelee20261001','ImportSscaryDefense20261001').replace('SscaryMeleeFrames','SscaryDefenseFrames').replace('SscaryMaleCombat/Melee','SscaryMaleCombat/Defense').replace('melee-v2','defense-v2').replace('hero-melee','hero-defense').replace('4608','6144').replace('"Sweep","Reverse","Cleave"','"Guard","Parry","Charge","ChargeCut"').replace('Mathf.Clamp(clip,0,2)','Mathf.Clamp(clip,0,3)')
imp=imp.replace('Authored native sweep/reverse/cleave joints','Authored native guard/parry/charge/release joints').replace('Three native melee joint clips','Four distinct defense/preparation/release clips')
imp=imp.replace('  public static int PhaseIndex(float p)', '  private static readonly float[] CutPhases={.18f,.21f,.24f,.27f,.30f,.34f,.38f,.42f,.48f,.54f,.60f,.68f,.76f,.84f,.92f,1};\\n  public static int PhaseIndex(int clip,float p)')
imp=imp.replace('if(p>=Phases[i])','if(p>=(clip==3?CutPhases:Phases)[i])')
(root/'Tools/Validation/ImportSscaryDefense20261001.cs').write_text(imp,encoding='utf-8')
qa=(root/'Tools/Art/review_sscary_melee_20261001.py').read_text(encoding='utf-8').replace('hero-melee','hero-defense')
qa=qa.replace("if row['frame'] in (0,15):assert im.tobytes()==src.tobytes(),row", "if row['clip']!='ChargeCut' and row['frame']==0 or row['clip'] in ('Parry','ChargeCut') and row['frame']==15:assert im.tobytes()==src.tobytes(),row")
qa=qa.replace("for (clip,direction,variant),rows in groups.items():", "for direction in packs:\n    for variant in range(16):\n        charge=groups[('Charge',direction,variant)][-1];cut=groups[('ChargeCut',direction,variant)][0]\n        assert charge['pixelsSha256']==cut['pixelsSha256'] and charge['grip']==cut['grip'],(charge,cut)\nfor (clip,direction,variant),rows in groups.items():")
qa=qa.replace('exact original endpoint pixels','source entries/recoveries and exact charge/release seam')
(root/'Tools/Art/review_sscary_defense_20261001.py').write_text(qa,encoding='utf-8')
print('Generated 6144-slot native defense author/import and read-only identity reviewer.')
