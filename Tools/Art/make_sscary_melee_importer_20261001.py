"""Generate Editor API importer source; no PNG edits outside Unity."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
text=(root/'Tools/Validation/ImportSscaryThrust20261001.cs').read_text(encoding='utf-8')
text=text.replace('ImportSscaryThrust20261001','ImportSscaryMelee20261001').replace('SscaryThrustFrames','SscaryMeleeFrames').replace('SscaryMaleCombat/Thrust','SscaryMaleCombat/Melee').replace('thrust-v3','melee-v2').replace('hero-thrust','hero-melee').replace('864','4608')
text=text.replace('var rows=new JArray();','var rows=new JArray();var behind=new bool[4608];')
text=text.replace('int slot=(direction*9+((int)row["gait"]+1))*16+(int)row["frame"];','int clip=Array.IndexOf(new[]{"Sweep","Reverse","Cleave"},(string)row["clip"]);\n            int slot=((clip*6+direction)*16+(int)row["variant"])*16+(int)row["frame"];')
text=text.replace('var adopted=(JObject)row.DeepClone();','behind[slot]=CoveredGrip((string)row["path"],hands[slot]);\n            var adopted=(JObject)row.DeepClone();adopted["handBehindBody"]=behind[slot];')
text=text.replace('code.Append("  public static void Preload()', 'code.Append("  private static readonly bool[] Behind={");foreach(var b in behind)code.Append(b?"true,":"false,");code.Append("};\\n");\n        code.Append("  public static void Preload()')
text=text.replace('public static Sprite Sample(int direction,int gait,int key,out Vector2 grip)', 'public static Sprite Sample(int clip,int direction,int sourceVariant,int key,out Vector2 grip,out bool handBehindBody)')
text=text.replace('(DirectionRows[Mathf.Clamp(direction,0,7)]*9+Mathf.Clamp(gait+1,0,8))*KeyCount+Mathf.Clamp(key,0,KeyCount-1)', '((Mathf.Clamp(clip,0,2)*6+DirectionRows[Mathf.Clamp(direction,0,7)])*16+Mathf.Clamp(sourceVariant,0,15))*KeyCount+Mathf.Clamp(key,0,KeyCount-1)')
text=text.replace('grip=Hands[slot];return frames[Cells[slot]];', 'grip=Hands[slot];handBehindBody=Behind[slot];return frames[Cells[slot]];')
text=text.replace('Authored native thrust joints + original gait composition.', 'Authored native sweep/reverse/cleave joints + exact original idle/gait composition.')
text=text.replace('Only thrust. Six views', 'Three native melee joint clips. Six views')
text=text.replace('public static string Main()', '''static bool CoveredGrip(string path,Vector2 grip)
    {
        var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));var p=t.GetPixels32();bool visible=false;
        for(int y=Mathf.Max(0,Mathf.FloorToInt(grip.y)-1);y<=Mathf.Min(63,Mathf.FloorToInt(grip.y)+1);y++)
        for(int x=Mathf.Max(0,Mathf.FloorToInt(grip.x)-1);x<=Mathf.Min(47,Mathf.FloorToInt(grip.x)+1);x++)
        {var c=p[(63-y)*48+x];if(c.a>0&&(c.r==229&&c.g==149&&c.b==159||c.r==193&&c.g==106&&c.b==125))visible=true;}
        UnityEngine.Object.DestroyImmediate(t);return !visible;
    }
    public static string Main()''')
(root/'Tools/Validation/ImportSscaryMelee20261001.cs').write_text(text,encoding='utf-8')
print('Generated Unity Editor API importer for 4608 sample slots.')
