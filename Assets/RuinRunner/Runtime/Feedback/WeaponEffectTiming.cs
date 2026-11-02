using UnityEngine;

namespace RuinRunner
{
    /// <summary>Quick contact, readable bright pose, then the authored breakup frames.</summary>
    public static class WeaponEffectTiming
    {
        public static int Frame(float progress, int count)
        {
            if (count == 4)
                return progress < .10f ? 0 : progress < .52f ? 1 : progress < .78f ? 2 : 3;
            if (count == 6)
                return progress < .08f ? 0 : progress < .30f ? 1 : progress < .55f ? 2 :
                    progress < .73f ? 3 : progress < .88f ? 4 : 5;
            return Mathf.Clamp(Mathf.FloorToInt(progress * count), 0, count - 1);
        }
    }
}
