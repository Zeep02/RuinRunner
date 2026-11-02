using UnityEngine;
namespace RuinRunner
{
    public static class GeneratedActorFrames
    {
        private static readonly Sprite[][] MeadowStates = new Sprite[12][];
        private static readonly int[][] StateFrames = { new[] { 0, 1 }, new[] { 0, 1, 2, 3 }, new[] { 4, 5, 3, 0 } };
        private static readonly int[][] ShooterStateFrames = { new[] { 0, 5 }, new[] { 0, 2, 1, 3 }, new[] { 4, 5, 0, 0 } };
        public static Sprite[] GetBoss(int row)
        {
            row = Mathf.Clamp(row, 0, 7);
            return IndividualArt.Frames(BossSpriteAnimator.FrameRoot + row.ToString("D2"), 8);
        }
        public static Sprite[] GetEnemy(int row)
        {
            row = Mathf.Clamp(row, 0, 11);
            if (MeadowStates[row] != null) return MeadowStates[row];
            var type = (EnemyType)(row / 3);
            var indices = (type == EnemyType.Shooter ? ShooterStateFrames : StateFrames)[row % 3];
            var frames = new Sprite[indices.Length];
            var path = "CreatureMotion/" + type + "/frames/";
            for (var i = 0; i < frames.Length; i++)
                frames[i] = IndividualArt.Sprite(path + indices[i].ToString("D2"));
            return MeadowStates[row] = frames;
        }
    }
}
