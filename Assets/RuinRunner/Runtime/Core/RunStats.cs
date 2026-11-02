using UnityEngine;

namespace RuinRunner
{
    public sealed class RunStats
    {
        public int EnemiesDefeated { get; private set; }
        public float ElapsedSeconds { get; private set; }

        public void Reset()
        {
            EnemiesDefeated = 0;
            ElapsedSeconds = 0f;
        }

        public void Tick(float deltaTime)
        {
            ElapsedSeconds += Mathf.Max(0f, deltaTime);
        }

        public void RegisterEnemyDefeated()
        {
            EnemiesDefeated++;
        }
    }
}
