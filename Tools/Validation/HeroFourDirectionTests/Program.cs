using System;
using RuinRunner;

public class Program
{
    static int checks;
    static void Equal(int expected, int actual, string label)
    {
        checks++;
        if (expected != actual) throw new Exception(label + ": " + actual + " != " + expected);
    }
    public static void Main()
    {
        Equal(1, DiagonalHeroFacing.Resolve(1,1), "NE");
        Equal(3, DiagonalHeroFacing.Resolve(1,-1), "SE");
        Equal(5, DiagonalHeroFacing.Resolve(-1,-1), "SW");
        Equal(7, DiagonalHeroFacing.Resolve(-1,1), "NW");
        foreach (int previous in new[]{1,3,5,7})
        {
            Equal(previous, DiagonalHeroFacing.Resolve(0,0,previous), "stationary retains facing");
            for (int degree=0;degree<360;degree++)
            {
                float x=(float)Math.Sin(degree*Math.PI/180),y=(float)Math.Cos(degree*Math.PI/180);
                int direction=DiagonalHeroFacing.Resolve(x,y,previous);
                if (direction!=1 && direction!=3 && direction!=5 && direction!=7)
                    throw new Exception("Cardinal view leaked");
                checks++;
            }
        }
        Equal(7, DiagonalHeroFacing.Resolve(.02f,1,7), "north-axis jitter stays NW");
        Equal(1, DiagonalHeroFacing.Resolve(.2f,1,7), "deliberate crossing becomes NE");
        Equal(3, DiagonalHeroFacing.Resolve(1,.02f,3), "east-axis jitter stays SE");
        Equal(1, DiagonalHeroFacing.Resolve(1,.2f,3), "deliberate crossing becomes NE");
        Equal(5, DiagonalHeroFacing.Resolve(-1,0,5), "west cardinal retains lower hemisphere");
        Equal(3, DiagonalHeroFacing.Resolve(0,0,4), "old cardinal index has safe default");
        Console.WriteLine("PASS: " + checks + " four-direction/axis-hysteresis checks");
    }
}
