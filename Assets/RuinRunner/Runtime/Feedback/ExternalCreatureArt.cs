using UnityEngine;

namespace RuinRunner
{
    /// <summary>Artist-authored, losslessly separated creature poses. Direction is never a texture rotation.</summary>
    public static class ExternalCreatureArt
    {
        private static readonly string[] Directions={"down","up","left","right"};
        public static bool Supports(EnemyType type)=>!EnemyRoles.IsBoss(type);
        public static string Root(EnemyType type)=>"Pilot/"+type;
        public static int Direction(Vector2 aim)=>Mathf.Abs(aim.x)>Mathf.Abs(aim.y)?(aim.x<0?2:3):(aim.y>0?1:0);
        public static Sprite[] HurtFrames(EnemyType type,int direction)=>
            IndividualArt.Frames(Root(type)+"/hurt/"+Directions[Mathf.Clamp(direction,0,3)],type==EnemyType.Charger?6:type==EnemyType.Tidecaller||type==EnemyType.Sentinel?4:5);
        public static Sprite[] Frames(EnemyType type,int state,int direction)
        {
            var charger=type==EnemyType.Charger;
            var clip=state==0?"idle":state==1?(charger?"run":"walk"):state==2?"attack":"death";
            var vampire=type==EnemyType.Tidecaller||type==EnemyType.Sentinel;
            var count=charger?(state==0?4:8):vampire?(state==0?4:state==1?6:state==3?11:12):
                state==0?6:state==1?8:state==3?10:type==EnemyType.Leaper?11:type==EnemyType.Shooter?9:10;
            return IndividualArt.Frames(Root(type)+"/"+clip+"/"+Directions[Mathf.Clamp(direction,0,3)],count);
        }
        public static Sprite[] AttackFrames(EnemyType type,int direction,int phase)=>type==EnemyType.Charger&&phase==1?
            IndividualArt.Frames(Root(type)+"/run_attack/"+Directions[Mathf.Clamp(direction,0,3)],8):Frames(type,2,direction);
        public static float IdleFramesPerSecond(EnemyType type)=>type==EnemyType.Charger?4f/1.2f:6f;
        public static int AttackFrame(EnemyType type,int phase,float seconds)
        {
            if(type==EnemyType.Charger)
                // The full windup/rush/recovery remains .38/.40/.35 seconds.
                // Native running attack middle poses advance with the collision-active rush.
                return phase==0?Mathf.Min(1,(int)(seconds/.19f)):
                    phase==1?2+Mathf.Min(3,(int)(seconds/.1f)):6+Mathf.Min(1,(int)(seconds/.175f));
            if(type==EnemyType.Tidecaller||type==EnemyType.Sentinel)
            {
                if(phase==0)return Mathf.Min(3,(int)(seconds/.075f));
                if(phase==1)return 4+Mathf.Min(2,(int)(seconds/.07f));
                return 7+Mathf.Min(4,(int)(seconds/.045f));
            }
            if(type==EnemyType.Shooter)
            {
                if(phase==0)return Mathf.Min(2,(int)(seconds/.07f));
                if(phase==1)return 3+Mathf.Min(2,(int)(seconds/.07f));
                return 6+Mathf.Min(type==EnemyType.Shooter?2:1,(int)(seconds/.06f));
            }
            if(type==EnemyType.Leaper)
            {
                // Compress -> airborne ball -> landing burst; same phases as the real slam.
                if(phase==0)return Mathf.Min(2,(int)(seconds/.087f));
                if(phase==1)return 3+Mathf.Min(2,(int)(seconds/.14f));
                return 6+Mathf.Min(4,(int)(seconds/.055f));
            }
            if(phase==0)return Mathf.Min(2,(int)(seconds/.074f));
            if(phase==1)return 3+Mathf.Min(2,(int)(seconds/.067f));
            return 6+Mathf.Min(3,(int)(seconds/.055f));
        }
        // Where the body is drawn, measured from the idle frames of every facing around the foot pivot at 32 PPU.
        // A blow that visibly touches the art has to land, so these follow the art whenever it is redrawn.
        public static Vector2 BodySize(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.BoneShield: return new Vector2(.94f,.88f);
                case EnemyType.ChainWarden: return new Vector2(.94f,1.12f);
                case EnemyType.Charger: return new Vector2(1.38f,1.06f);
                case EnemyType.CoralGuard: return new Vector2(1.44f,1.16f);
                case EnemyType.Drowned: return new Vector2(1f,1.06f);
                case EnemyType.EelSpitter: return new Vector2(.81f,1.28f);
                case EnemyType.GildedBolter: return new Vector2(1.19f,1.12f);
                case EnemyType.Grunt: return new Vector2(.81f,.75f);
                case EnemyType.Hexer: return new Vector2(1.5f,1.91f);
                case EnemyType.InkShade: return new Vector2(1f,.97f);
                case EnemyType.JellyLamp: return new Vector2(.62f,.81f);
                case EnemyType.Leaper: return new Vector2(.88f,.75f);
                case EnemyType.MossArcher: return new Vector2(.81f,.84f);
                case EnemyType.PowderBeetle: return new Vector2(.88f,.97f);
                case EnemyType.RuinRat: return new Vector2(1f,.75f);
                case EnemyType.RuneTotem: return new Vector2(1.06f,.91f);
                case EnemyType.SealGolem: return new Vector2(1.12f,1.03f);
                case EnemyType.SealKnight: return new Vector2(1.19f,1.13f);
                case EnemyType.SealPriest: return new Vector2(.88f,.91f);
                case EnemyType.Sentinel: return new Vector2(1.25f,.91f);
                case EnemyType.Shooter: return new Vector2(.88f,.84f);
                case EnemyType.Sporeling: return new Vector2(1f,.84f);
                case EnemyType.Tidecaller: return new Vector2(.69f,.97f);
                case EnemyType.TideCrab: return new Vector2(1f,.97f);
                case EnemyType.TideSiren: return new Vector2(1f,.91f);
                case EnemyType.VineLasher: return new Vector2(1.25f,1.03f);
                case EnemyType.Wisp: return new Vector2(1.5f,1.94f);
                default: return new Vector2(.57f,.58f);
            }
        }
        public static float BodyCenterY(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.BoneShield: return .44f;
                case EnemyType.ChainWarden: return .56f;
                case EnemyType.Charger: return .53f;
                case EnemyType.CoralGuard: return .58f;
                case EnemyType.Drowned: return .53f;
                case EnemyType.EelSpitter: return .55f;
                case EnemyType.GildedBolter: return .56f;
                case EnemyType.Grunt: return .38f;
                case EnemyType.Hexer: return .95f;
                case EnemyType.InkShade: return .45f;
                case EnemyType.JellyLamp: return .41f;
                case EnemyType.Leaper: return .38f;
                case EnemyType.MossArcher: return .36f;
                case EnemyType.PowderBeetle: return .45f;
                case EnemyType.RuinRat: return .38f;
                case EnemyType.RuneTotem: return .45f;
                case EnemyType.SealGolem: return .48f;
                case EnemyType.SealKnight: return .53f;
                case EnemyType.SealPriest: return .45f;
                case EnemyType.Sentinel: return .45f;
                case EnemyType.Shooter: return .42f;
                case EnemyType.Sporeling: return .42f;
                case EnemyType.Tidecaller: return .48f;
                case EnemyType.TideCrab: return .48f;
                case EnemyType.TideSiren: return .45f;
                case EnemyType.VineLasher: return .52f;
                case EnemyType.Wisp: return .97f;
                default: return BodySize(type).y*.5f;
            }
        }

        // Maximum opaque height across the authored idle/move/attack/hurt poses,
        // measured from their shared foot pivot at 32 PPU. Keep this independent
        // of the hurtbox and current frame so wings do not push the bar up/down.
        public static float VisualTop(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Grunt: return 25f / 32f;
                case EnemyType.Shooter: return 28f / 32f;
                case EnemyType.Charger: return 34f / 32f;
                case EnemyType.Leaper: return 27f / 32f;
                case EnemyType.Tidecaller: return 31f / 32f;
                case EnemyType.Sentinel: return 30f / 32f;
                case EnemyType.RuneTotem: return 30f / 32f;
                case EnemyType.SealKnight: return 37f / 32f;
                case EnemyType.Hexer: return 52f / 32f;
                case EnemyType.Wisp: return 56f / 32f;
                case EnemyType.TideCrab: return 32f / 32f;
                case EnemyType.Sporeling: return 27f / 32f;
                case EnemyType.BoneShield: return 30f / 32f;
                case EnemyType.VineLasher: return 33f / 32f;
                case EnemyType.RuinRat: return 25f / 32f;
                case EnemyType.MossArcher: return 26f / 32f;
                case EnemyType.PowderBeetle: return 31f / 32f;
                case EnemyType.Drowned: return 34f / 32f;
                case EnemyType.EelSpitter: return 38f / 32f;
                case EnemyType.CoralGuard: return 38f / 32f;
                case EnemyType.JellyLamp: return 26f / 32f;
                case EnemyType.TideSiren: return 30f / 32f;
                case EnemyType.InkShade: return 31f / 32f;
                case EnemyType.SealGolem: return 32f / 32f;
                case EnemyType.GildedBolter: return 36f / 32f;
                case EnemyType.ChainWarden: return 38f / 32f;
                case EnemyType.SealPriest: return 30f / 32f;
                default: return BodySize(type).y;
            }
        }
    }
}
