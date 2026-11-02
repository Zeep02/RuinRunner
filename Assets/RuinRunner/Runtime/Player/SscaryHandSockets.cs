// Baked from reviewed native hands by Tools/Art/author_sscary_sockets.py. No sprite pixels modified.
using UnityEngine;
namespace RuinRunner
{
    public static class SscaryHandSockets
    {
        private static readonly int[] DirectionRows={3,2,1,1,0,5,5,4};
        private static readonly Vector2[,] Idle={
            {new Vector2(22f,34.5f),new Vector2(22f,35.5f),new Vector2(22f,35f),new Vector2(22f,34f),new Vector2(22f,34.5f),new Vector2(22f,35.5f),new Vector2(22f,35f),new Vector2(22f,34.5f)},
            {new Vector2(23f,34.5f),new Vector2(23f,35.5f),new Vector2(23f,35f),new Vector2(23f,34f),new Vector2(23f,34.5f),new Vector2(23f,35.5f),new Vector2(23f,35f),new Vector2(23f,34.5f)},
            {new Vector2(30f,34f),new Vector2(30f,35f),new Vector2(30f,35f),new Vector2(30f,34f),new Vector2(30f,34f),new Vector2(30f,35f),new Vector2(30f,35f),new Vector2(30f,34f)},
            {new Vector2(28f,34.5f),new Vector2(28f,35.5f),new Vector2(28f,34.5f),new Vector2(28f,33.5f),new Vector2(28f,34f),new Vector2(28f,35f),new Vector2(28f,34.5f),new Vector2(28f,34.5f)},
            {new Vector2(21f,36f),new Vector2(21f,37f),new Vector2(21f,36f),new Vector2(21f,35f),new Vector2(21f,35f),new Vector2(21f,36.5f),new Vector2(21f,36f),new Vector2(21f,36f)},
            {new Vector2(27f,37f),new Vector2(27f,38f),new Vector2(27f,37f),new Vector2(27f,36f),new Vector2(27f,36f),new Vector2(27f,37.5f),new Vector2(27f,37f),new Vector2(27f,37f)},
        };
        private static readonly Vector2[,] Walk={
            {new Vector2(20f,35.5f),new Vector2(20.5f,36.5f),new Vector2(20.5f,36f),new Vector2(20f,35.5f),new Vector2(20f,34.5f),new Vector2(20f,34.5f),new Vector2(20f,34f),new Vector2(20f,35f)},
            {new Vector2(24.5f,35.5f),new Vector2(24.5f,36.5f),new Vector2(24.5f,35.5f),new Vector2(23.5f,35.5f),new Vector2(21.5f,34.5f),new Vector2(20.5f,35.5f),new Vector2(21f,36.5f),new Vector2(23.5f,35.5f)},
            {new Vector2(28.5f,34.5f),new Vector2(28.5f,34f),new Vector2(28.5f,35.5f),new Vector2(26.5f,35f),new Vector2(24f,35f),new Vector2(23.5f,36f),new Vector2(25f,36f),new Vector2(28f,35f)},
            {new Vector2(28.5f,33f),new Vector2(28.5f,33f),new Vector2(28.5f,34f),new Vector2(28.5f,34f),new Vector2(28f,34.5f),new Vector2(28f,35.5f),new Vector2(28.5f,35.5f),new Vector2(28f,34f)},
            {new Vector2(19.5f,34.5f),new Vector2(19.5f,34f),new Vector2(19.5f,35.5f),new Vector2(21.5f,35f),new Vector2(24f,35f),new Vector2(24.5f,36f),new Vector2(23f,36f),new Vector2(20f,35f)},
            {new Vector2(23.5f,35.5f),new Vector2(23.5f,36.5f),new Vector2(23.5f,35.5f),new Vector2(24.5f,35.5f),new Vector2(26.5f,34.5f),new Vector2(27.5f,35.5f),new Vector2(27f,36.5f),new Vector2(24.5f,35.5f)},
        };
        private static readonly Vector2[,] Dash={
            {new Vector2(22f,34.5f),new Vector2(21.5f,36.5f),new Vector2(19.5f,33f),new Vector2(19.5f,32f),new Vector2(19.5f,33f),new Vector2(19.5f,34f),new Vector2(19.5f,36f),new Vector2(21.5f,36.5f)},
            {new Vector2(23f,34.5f),new Vector2(24.5f,34.5f),new Vector2(23f,32f),new Vector2(24f,30f),new Vector2(24f,31f),new Vector2(24f,31f),new Vector2(24f,34.5f),new Vector2(24.5f,34.5f)},
            {new Vector2(30f,34f),new Vector2(29.5f,34.5f),new Vector2(26f,34f),new Vector2(26f,32f),new Vector2(26f,33f),new Vector2(26f,33f),new Vector2(26.5f,36f),new Vector2(29.5f,35.5f)},
            {new Vector2(28f,34.5f),new Vector2(28f,34f),new Vector2(29.5f,33f),new Vector2(29.5f,32f),new Vector2(29.5f,33f),new Vector2(29.5f,34f),new Vector2(29.5f,35f),new Vector2(28f,35f)},
            {new Vector2(21f,36f),new Vector2(19.5f,34.5f),new Vector2(23f,34f),new Vector2(23f,32f),new Vector2(23f,33f),new Vector2(23f,33f),new Vector2(22.5f,36f),new Vector2(19.5f,35.5f)},
            {new Vector2(27f,37f),new Vector2(24.5f,34.5f),new Vector2(26f,32f),new Vector2(25f,30f),new Vector2(25f,31f),new Vector2(25f,31f),new Vector2(25f,34.5f),new Vector2(24.5f,34.5f)},
        };
        public static Vector2 Pixel(string action,int direction,int frame)
        {
            var data=action=="dash"?Dash:action=="walk"?Walk:Idle;
            return data[DirectionRows[Mathf.Clamp(direction,0,7)],Mathf.Clamp(frame,0,7)];
        }
    }
}
