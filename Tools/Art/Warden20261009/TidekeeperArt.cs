using UnityEngine;

namespace RuinRunner
{
    // Kronovi's native stone guardian. The original 100px canvas retains the
    // levitation/collapse distance; the ground pivot is shared by every pose.
    public static class TidekeeperArt
    {
        public const string Root = "External/Kronovi/Golem/";
        public static void Preload()
        {
            Frames(0); Frames(3);
            IndividualArt.Frames(Root + "glow", 8);
            IndividualArt.Frames(Root + "immune", 8);
            IndividualArt.Frames(Root + "laser_cast", 7);
            IndividualArt.Frames(Root + "shield_cast", 10);
        }
        public static Sprite[] Frames(int state) => IndividualArt.Frames(Root + (state == 3 ? "death" : state == 2 ? "shield_cast" : "idle"), state == 3 ? 14 : state == 2 ? 10 : 4);
        public static Sprite Idle(float time) => Frames(0)[Mathf.FloorToInt(time * 6f) % 4];

        public const float EntranceDuration = 1.05f;
        public static Sprite Entrance(float time)
        {
            // Kronovi explicitly authored this fourteen-pose strip for both
            // defeat and appearance. Reassembly is its intended reverse use.
            if (time >= EntranceDuration) return Idle(time - EntranceDuration);
            var appearance = Frames(3);
            int step = Mathf.Clamp(Mathf.FloorToInt(time / EntranceDuration * appearance.Length), 0, appearance.Length - 1);
            return appearance[appearance.Length - 1 - step];
        }

        public static Sprite Action(CampaignBossAction action, int phase, float time, float phaseDuration)
        {
            bool lanes = action == CampaignBossAction.TideLanes || action == CampaignBossAction.TideFan;
            var cast = IndividualArt.Frames(Root + (lanes ? "shield_cast" : "laser_cast"), lanes ? 10 : 7);
            if (phase == 0)
            {
                // Charge first, then reach the actual release pose at the hazard
                // activation time. Fan and surge use these same authored clips,
                // but their shorter action clocks must still reach every cast pose.
                float warning = Mathf.Max(.01f, phaseDuration);
                float glowTime = warning * .6f;
                if (time < glowTime)
                    return IndividualArt.Frames(Root + "glow", 8)[Mathf.Min(7, (int)(time / glowTime * 8))];
                return cast[Mathf.Clamp((int)((time - glowTime) / (warning - glowTime) * 4), 0, 3)];
            }
            if (phase == 1) return cast[4 + Mathf.Clamp((int)(time / Mathf.Max(.01f, phaseDuration) * 3), 0, 2)];
            if (lanes && time < .3f) return cast[7 + Mathf.Clamp((int)(time / .3f * 3), 0, 2)];
            if (!lanes && time < .42f) return cast[6 - Mathf.Clamp((int)(time / .42f * 7), 0, 6)];
            return Idle(Mathf.Max(0, time - (lanes ? .3f : .42f)));
        }

        // Compatibility for the original two-pattern art review. Gameplay passes
        // the committed action and its actual phase duration through the overload above.
        public static Sprite Action(bool lanes, int phase, float time) =>
            Action(lanes ? CampaignBossAction.TideLanes : CampaignBossAction.TidePools, phase, time,
                phase == 0 ? (lanes ? TideLanePattern.WarningSeconds : .95f) : .5f);

        public const float RecoilDuration = .28f;
        public static Sprite Recoil(float time)
        {
            // A short partial fold from the authored guard poses. This is visual
            // recoil only: it does not grant immunity or cancel a boss attack.
            int step = Mathf.Clamp((int)(time / RecoilDuration * 7), 0, 6);
            return IndividualArt.Frames(Root + "immune", 8)[step <= 3 ? step : 6 - step];
        }
    }
}
