using UnityEngine;

namespace RuinRunner
{
    /// <summary>Original Pixel Frog Pawn cels. One view, mirrored; eight-way input is independent.</summary>
    public static class ExternalHeroArt
    {
        public const string Root="External/PixelFrog/Pawn/";
        public const float PixelsPerUnit=64;
        public static readonly Vector2 Pivot=new Vector2(96,64);
        public static readonly Vector3 FootOffset=new Vector3(0,-.28f,0);
        public static readonly string[] Clips={"idle","move","swing","thrust","heavy","hammer"};
        public static readonly int[] Counts={8,6,6,4,6,3};
        // Source glove centers, reviewed on the contact sheet. No runtime color/bounds heuristic.
        static readonly Vector2[][] Hands={
            new[]{new Vector2(117.029f,113.676f),new Vector2(118.792f,112.417f),new Vector2(119.154f,109.692f),new Vector2(119.397f,110.224f),new Vector2(117.907f,113.241f),new Vector2(117.389f,113.167f),new Vector2(116,110),new Vector2(116.633f,111.9f)},
            new[]{new Vector2(123.271f,109.157f),new Vector2(124.455f,105.455f),new Vector2(122.354f,112.744f),new Vector2(114.455f,113.818f),new Vector2(94.676f,108.147f),new Vector2(118.167f,116.278f)},
            new[]{new Vector2(78.433f,111.833f),new Vector2(76.25f,112.375f),new Vector2(132.056f,114.611f),new Vector2(132.056f,114.611f),new Vector2(117.69f,122.071f),new Vector2(88.071f,119.643f)},
            new[]{new Vector2(126.333f,103.833f),new Vector2(115.5f,125.5f),new Vector2(116.643f,116.786f),new Vector2(121.5f,116.75f)},
            new[]{new Vector2(78.55f,110.75f),new Vector2(75.912f,112.382f),new Vector2(135.5f,118),new Vector2(134.5f,117),new Vector2(127.5f,104),new Vector2(100.5f,98)},
            new[]{new Vector2(126.333f,95.583f),new Vector2(124.9f,118.7f),new Vector2(126.286f,111.143f)}
        };
        public static Sprite[] Frames(int clip)=>IndividualArt.Frames(Root+Clips[clip],Counts[clip]);
        public static Vector2 Hand(int clip,int frame,bool flipped)
        {
            var p=Hands[clip][Mathf.Clamp(frame,0,Hands[clip].Length-1)];
            return (Vector2)FootOffset+new Vector2((p.x-96)*(flipped?-1:1)/PixelsPerUnit,(128-p.y)/PixelsPerUnit);
        }
        public static int SwingFrame(float progress)
        {
            // Original contact is frame 2, unlike the old generated frame-3 strike.
            if(progress<.18f)return 0;
            if(progress<MeleeMotionTiming.Contact)return 1;
            if(progress<.55f)return 2;
            if(progress<MeleeMotionTiming.Recovery)return 3;
            return progress<.84f?4:5;
        }
        public static int ThrustFrame(float progress)=>progress<MeleeMotionTiming.ThrustContact?0:
            progress<MeleeMotionTiming.ThrustEnd?1:progress<.8f?2:3;
    }
}
