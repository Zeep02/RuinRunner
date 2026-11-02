using UnityEngine;

namespace RuinRunner
{
    // Generated from reviewed authoring coordinates; no runtime pixel/bounds inference.
    internal static class AuthoredPlayerWalk
    {
        public const int FrameCount = 12;
        public static Vector2 HandPixel(int direction,int frame)=>Hands[direction,frame];
        private static readonly Vector2[,] Hands = {
            { // north
                new Vector2(30.250f,31.550f),new Vector2(29.875f,31.206f),new Vector2(29.600f,30.450f),
                new Vector2(29.500f,29.850f),new Vector2(29.600f,29.750f),new Vector2(29.875f,29.994f),
                new Vector2(30.250f,30.150f),new Vector2(30.625f,29.994f),new Vector2(30.900f,29.750f),
                new Vector2(31.000f,29.850f),new Vector2(30.900f,30.450f),new Vector2(30.625f,31.206f)
            },
            { // northeast
                new Vector2(28.510f,31.550f),new Vector2(28.022f,31.281f),new Vector2(27.440f,30.675f),
                new Vector2(26.920f,30.150f),new Vector2(26.600f,29.975f),new Vector2(26.568f,30.069f),
                new Vector2(26.830f,30.150f),new Vector2(27.318f,30.069f),new Vector2(27.900f,29.975f),
                new Vector2(28.420f,30.150f),new Vector2(28.740f,30.675f),new Vector2(28.772f,31.281f)
            },
            { // east
                new Vector2(27.350f,31.550f),new Vector2(27.537f,31.331f),new Vector2(27.300f,30.825f),
                new Vector2(26.700f,30.350f),new Vector2(25.900f,30.125f),new Vector2(25.113f,30.119f),
                new Vector2(24.550f,30.150f),new Vector2(24.363f,30.119f),new Vector2(24.600f,30.125f),
                new Vector2(25.200f,30.350f),new Vector2(26.000f,30.825f),new Vector2(26.787f,31.331f)
            },
            { // southeast
                new Vector2(28.510f,31.550f),new Vector2(28.772f,31.281f),new Vector2(28.740f,30.675f),
                new Vector2(28.420f,30.150f),new Vector2(27.900f,29.975f),new Vector2(27.318f,30.069f),
                new Vector2(26.830f,30.150f),new Vector2(26.568f,30.069f),new Vector2(26.600f,29.975f),
                new Vector2(26.920f,30.150f),new Vector2(27.440f,30.675f),new Vector2(28.022f,31.281f)
            },
            { // south
                new Vector2(30.250f,31.550f),new Vector2(30.625f,31.206f),new Vector2(30.900f,30.450f),
                new Vector2(31.000f,29.850f),new Vector2(30.900f,29.750f),new Vector2(30.625f,29.994f),
                new Vector2(30.250f,30.150f),new Vector2(29.875f,29.994f),new Vector2(29.600f,29.750f),
                new Vector2(29.500f,29.850f),new Vector2(29.600f,30.450f),new Vector2(29.875f,31.206f)
            },
            { // southwest
                new Vector2(19.490f,31.550f),new Vector2(19.228f,31.281f),new Vector2(19.260f,30.675f),
                new Vector2(19.580f,30.150f),new Vector2(20.100f,29.975f),new Vector2(20.682f,30.069f),
                new Vector2(21.170f,30.150f),new Vector2(21.432f,30.069f),new Vector2(21.400f,29.975f),
                new Vector2(21.080f,30.150f),new Vector2(20.560f,30.675f),new Vector2(19.978f,31.281f)
            },
            { // west
                new Vector2(20.650f,31.550f),new Vector2(20.463f,31.331f),new Vector2(20.700f,30.825f),
                new Vector2(21.300f,30.350f),new Vector2(22.100f,30.125f),new Vector2(22.887f,30.119f),
                new Vector2(23.450f,30.150f),new Vector2(23.637f,30.119f),new Vector2(23.400f,30.125f),
                new Vector2(22.800f,30.350f),new Vector2(22.000f,30.825f),new Vector2(21.213f,31.331f)
            },
            { // northwest
                new Vector2(19.490f,31.550f),new Vector2(19.978f,31.281f),new Vector2(20.560f,30.675f),
                new Vector2(21.080f,30.150f),new Vector2(21.400f,29.975f),new Vector2(21.432f,30.069f),
                new Vector2(21.170f,30.150f),new Vector2(20.682f,30.069f),new Vector2(20.100f,29.975f),
                new Vector2(19.580f,30.150f),new Vector2(19.260f,30.675f),new Vector2(19.228f,31.281f)
            }
        };
    }
}
