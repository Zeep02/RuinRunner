using UnityEngine;
namespace RuinRunner
{
    public static class GeneratedActorFrames
    {
        public static Sprite[] GetBoss(int row)
        {
            row = Mathf.Clamp(row, 0, 7);
            return IndividualArt.Frames(BossSpriteAnimator.RowId(row), BossSpriteAnimator.FrameCount);
        }
    }
}
