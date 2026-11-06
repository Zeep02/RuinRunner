using UnityEngine;

namespace RuinRunner
{
    // A floating stone guardian. Every clip starts from and returns to one shared
    // stance, and the shared pivot keeps the body over its fixed hurtbox.
    public static class TidekeeperArt
    {
        public const string Root = "Pilot/Tidekeeper/";
        private const int CastFrames = 24;
        private const int HurtFrames = 8;
        public static void Preload()
        {
            Frames(0);
            Frames(3);
            IndividualArt.Frames(Root + "lanes", CastFrames);
            IndividualArt.Frames(Root + "pools", CastFrames);
            IndividualArt.Frames(Root + "hurt", HurtFrames);
        }
        public static Sprite[] Frames(int state) => IndividualArt.Frames(Root + (state == 3 ? "death" : state == 2 ? "lanes" : "idle"), state == 3 ? 14 : state == 2 ? CastFrames : 24);
        public static Sprite Idle(float time) => Frames(0)[Mathf.FloorToInt(time * 15f) % 24];

        public const float EntranceDuration = 1.05f;
        public static Sprite Entrance(float time)
        {
            // The collapse played backward is the guardian pulling itself together.
            if (time >= EntranceDuration)
                return Idle(time - EntranceDuration);
            var appearance = Frames(3);
            int step = Mathf.Clamp(Mathf.FloorToInt(time / EntranceDuration * appearance.Length), 0, appearance.Length - 1);
            return appearance[appearance.Length - 1 - step];
        }

        public static Sprite Action(CampaignBossAction action, int phase, float time, float phaseDuration)
        {
            bool lanes = action == CampaignBossAction.TideLanes || action == CampaignBossAction.TideFan;
            var cast = IndividualArt.Frames(Root + (lanes ? "lanes" : "pools"), CastFrames);
            // Wind up across the warning so the release pose lands on hazard activation.
            // Fan and surge share these clips, and their shorter clocks still reach every pose.
            if (phase == 0)
                return cast[Mathf.Clamp((int)(time / Mathf.Max(.01f, phaseDuration) * 12), 0, 11)];
            if (phase == 1)
                return cast[12 + Mathf.Clamp((int)(time / Mathf.Max(.01f, phaseDuration) * 4), 0, 3)];
            float recovery = lanes ? .3f : .42f;
            if (time < recovery)
                return cast[16 + Mathf.Clamp((int)(time / recovery * 8), 0, 7)];
            return Idle(time - recovery);
        }

        // Compatibility for the original two-pattern art review. Gameplay passes
        // the committed action and its actual phase duration through the overload above.
        public static Sprite Action(bool lanes, int phase, float time) =>
            Action(lanes ? CampaignBossAction.TideLanes : CampaignBossAction.TidePools, phase, time,
                phase == 0 ? (lanes ? TideLanePattern.WarningSeconds : .95f) : .5f);

        public const float RecoilDuration = .28f;
        // Visual recoil only: it does not grant immunity or cancel a boss attack.
        public static Sprite Recoil(float time) =>
            IndividualArt.Frames(Root + "hurt", HurtFrames)[Mathf.Clamp((int)(time / RecoilDuration * HurtFrames), 0, HurtFrames - 1)];
    }
}
