using UnityEngine;

namespace RuinRunner
{
    internal static class RuntimeSceneObjects
    {
        public static void ClearSession()
        {
            foreach (var effects in FindAll<ItemCombatEffects>())
                effects.CancelTransient();
            foreach (var echoes in FindAll<DashAfterimageEffect>())
                echoes.CancelTransient();
            DestroyAll<EnemyController>();
            DestroyAll<EnemySpawnTelegraph>();
            DestroyAll<RewardChest>();
            DestroyAll<ExperienceOrb>();
            DestroyAll<GoldCoin>();
            DestroyAll<FieldLootPickup>();
            DestroyAll<EnemyProjectile>();
            DestroyAll<PlayerProjectile>();
            DestroyAll<TransientSpriteEffect>();
            DestroyAll<SignatureSlashEffect>();
            DestroyAll<ExternalCombatEffect>();
            DestroyAll<NativeCombatEffect>();
            DestroyAll<WeaponSecondaryParticles>();
            CombatAudio.StopCombat();
            DestroyAll<ArenaHazard>();
        }

        public static void ClearProjectiles()
        {
            DestroyAll<EnemyProjectile>();
            DestroyAll<PlayerProjectile>();
            DestroyAll<ArenaHazard>();
        }

        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_6000_6_OR_NEWER
            return Object.FindObjectsByType<T>();
#elif UNITY_6000_0_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        private static void DestroyAll<T>() where T : Component
        {
            foreach (var component in FindAll<T>())
                Object.Destroy(component.gameObject);
        }
    }
}
