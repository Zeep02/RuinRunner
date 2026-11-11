using UnityEngine;

namespace RuinRunner
{
    public static class PixelArtAtlas
    {



        public static bool HasPlayerAtlas => IndividualArt.Exists(PlayerAnimationController.ArtRoot + "idle/down/00");
        public static bool HasEnemyAtlas => IndividualArt.Exists("Enemies/Grunt/idle/00") && IndividualArt.Exists("Boss/00/00");
        public static bool HasTerrainAtlas => IndividualArt.Exists("Terrain/Ruins/00");
        public static bool HasMeleeAttackAtlas => IndividualArt.Exists("External/NinjaAdventure/Slash/Arc/00");

        public static Sprite[] GetMeleeSlashFrames(WeaponFamily family, bool critical)
        {
            // One authored pack supplies the entire melee effect vocabulary.
            // Critical hits share the weapon's arc and receive a warm tint at
            // presentation time; they do not substitute an unrelated blast.
            var style = family == WeaponFamily.Dagger ? "Slash01" : family == WeaponFamily.Katana ? "Slash03" :
                family == WeaponFamily.Greatsword ? "Slash02" : family == WeaponFamily.Mace ? "Circular" : "Arc";
            return IndividualArt.Frames("External/NinjaAdventure/Slash/" + style, style == "Slash01" ? 5 : style == "Slash03" ? 4 : 6);
        }

        public static Sprite[] GetEnemyFrames(EnemyType type, int state)
        {
            if (type == EnemyType.Tidekeeper)
                return TidekeeperArt.Frames(state);
            if (type == EnemyType.SealHeart)
                return SealHeartArt.Frames(state);
            if (ExternalCreatureArt.Supports(type))
                return ExternalCreatureArt.Frames(type, state, 0);
            return GetBossFrames(Mathf.Clamp(state, 0, 5));
        }

        public static Sprite[] GetBossFrames(int row)
        {
            return GeneratedActorFrames.GetBoss(row);
        }

        public static Texture2D GetInventoryIcon(string id, InventoryIconKind kind)
        {
            return ItemIconLibrary.Get(id, kind);
        }

        public static Sprite GetHeldWeaponSprite(WeaponDefinition weapon)
        {
            if (weapon == null)
                return null;
            if (NativeWeaponArt.Supports(weapon.ArtId))
                return Resources.Load<Sprite>(NativeWeaponArt.Path(weapon.ArtId));
            return IndividualArt.Sprite("HeldWeapons/" + weapon.ArtId);
        }

        public static Sprite GetTerrainSprite(int index)
        {
            return IndividualArt.Sprite("Terrain/Ruins/" + Mathf.Clamp(index, 0, 15).ToString("D2"));
        }


    }
}

