using System;
using UnityEngine;

namespace RuinRunner
{
    public sealed class PlayerProgression : MonoBehaviour
    {
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int RequiredExperience { get; private set; } = 28;
        public int PendingLevelRewards { get; private set; }

        private Action onLevelRewardEarned;

        public void Initialize(Action levelRewardCallback)
        {
            onLevelRewardEarned = levelRewardCallback;
        }

        public float ExperienceScale { get; set; } = 1;

        public void AddExperience(int amount)
        {
            Experience += Mathf.Max(0, Mathf.RoundToInt(amount * ExperienceScale));
            while (Experience >= RequiredExperience)
            {
                Experience -= RequiredExperience;
                Level++;
                PendingLevelRewards++;
                RequiredExperience = 28 + (Level - 1) * 16;
                GameAudio.Play(GameSound.Upgrade, AudioChannel.Interface, .55f);
                BossNativeEffect.CreateOverActors(null, "FxManaBurst", transform.position, 0f);
                if (onLevelRewardEarned != null)
                    onLevelRewardEarned();
            }
        }

        public bool SpendLevelReward()
        {
            if (PendingLevelRewards <= 0)
                return false;
            PendingLevelRewards--;
            return true;
        }
    }
}
