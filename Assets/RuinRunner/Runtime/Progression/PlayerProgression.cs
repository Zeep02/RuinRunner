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

        public void AddExperience(int amount)
        {
            Experience += Mathf.Max(0, amount);
            while (Experience >= RequiredExperience)
            {
                Experience -= RequiredExperience;
                Level++;
                PendingLevelRewards++;
                RequiredExperience = 28 + (Level - 1) * 16;
                PrototypeAudio.Play(PrototypeSound.Upgrade, AudioChannel.Interface, .7f);
                PrototypeVisuals.Burst(transform.position, new Color(.25f, .85f, 1f), 20);
                if (onLevelRewardEarned != null) onLevelRewardEarned();
            }
        }

        public bool SpendLevelReward()
        {
            if (PendingLevelRewards <= 0) return false;
            PendingLevelRewards--;
            return true;
        }
    }
}
