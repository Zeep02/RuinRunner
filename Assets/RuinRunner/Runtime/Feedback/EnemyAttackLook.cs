using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Which pixelfx clips each enemy attacks with. Presentation only.</summary>
    public static class EnemyAttackLook
    {
        public static string Shot(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Shooter:
                case EnemyType.MossArcher: return "EShotEmber";
                case EnemyType.Leaper: return "EShotStone";
                case EnemyType.Tidecaller:
                case EnemyType.EelSpitter:
                case EnemyType.Drowned:
                case EnemyType.CoralGuard: return "EShotWater";
                case EnemyType.RuneTotem:
                case EnemyType.GildedBolter:
                case EnemyType.SealGolem:
                case EnemyType.SealPriest: return "EShotRune";
                case EnemyType.TideCrab:
                case EnemyType.JellyLamp:
                case EnemyType.TideSiren: return "EShotBubble";
                case EnemyType.Wisp:
                case EnemyType.InkShade:
                case EnemyType.ChainWarden: return "EShotSoul";
                case EnemyType.Hexer: return "EShotHex";
                case EnemyType.Boss: return "EShotBoulder";
                case EnemyType.Tidekeeper: return "EShotTide";
                case EnemyType.SealHeart: return "EShotSeal";
                default: return "EShotStone";
            }
        }
        public static string Flash(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Shooter:
                case EnemyType.MossArcher: return "EFlashEmber";
                case EnemyType.Tidecaller:
                case EnemyType.EelSpitter:
                case EnemyType.JellyLamp:
                case EnemyType.TideSiren:
                case EnemyType.TideCrab: return "EFlashWater";
                case EnemyType.RuneTotem:
                case EnemyType.GildedBolter:
                case EnemyType.SealPriest: return "EFlashRune";
                case EnemyType.Wisp:
                case EnemyType.InkShade:
                case EnemyType.ChainWarden: return "EFlashSoul";
                case EnemyType.Hexer: return "EFlashHex";
                default: return null;
            }
        }
        public static string Cast(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Shooter:
                case EnemyType.MossArcher: return "ECastEmber";
                case EnemyType.Tidecaller:
                case EnemyType.EelSpitter:
                case EnemyType.JellyLamp:
                case EnemyType.TideSiren:
                case EnemyType.TideCrab: return "ECastWater";
                case EnemyType.RuneTotem:
                case EnemyType.GildedBolter:
                case EnemyType.SealPriest: return "ECastRune";
                case EnemyType.Wisp:
                case EnemyType.InkShade:
                case EnemyType.ChainWarden: return "ECastSoul";
                case EnemyType.Hexer: return "ECastHex";
                case EnemyType.Boss: return "ECastStone";
                default: return null;
            }
        }
    }

    /// <summary>A thrown rock on a visible arc, with its shadow crossing the floor. The circle it lands on does the damage.</summary>
    public sealed class EnemyLobbedRock : MonoBehaviour
    {
        const float SpinPerSecond = 540f;
        EnemyController owner; SpriteRenderer rock; Transform shadow; Vector2 from, to; float age, duration, height;

        public static void Throw(EnemyController owner, Vector2 from, Vector2 to, float duration, float height)
        {
            var root = new GameObject("EnemyLobbedRock").AddComponent<EnemyLobbedRock>();
            root.owner = owner; root.from = from; root.to = to; root.duration = duration; root.height = height;
            root.rock = root.gameObject.AddComponent<SpriteRenderer>();
            root.rock.sprite = NativeCombatEffect.Frames("StoneChip")[0];
            root.rock.sortingOrder = 2000;
            root.transform.position = from;
            var ground = new GameObject("RockShadow");
            root.shadow = ground.transform; ground.transform.position = from;
            var shade = ground.AddComponent<SpriteRenderer>();
            shade.sprite = IndividualArt.Sprite(PlayerAnimationController.ArtRoot + "shadow");
            shade.sortingLayerName = "WorldFloor"; shade.sortingOrder = 12;
        }
        void Update()
        {
            if (owner == null || owner.IsDead) { Finish(); return; }
            if (owner.AnimationPaused) return;
            age += Time.deltaTime;
            var progress = Mathf.Clamp01(age / duration);
            var ground = Vector2.Lerp(from, to, progress);
            transform.position = ground + Vector2.up * (Mathf.Sin(progress * Mathf.PI) * height);
            transform.Rotate(0f, 0f, SpinPerSecond * Time.deltaTime);
            shadow.position = ground;
            if (progress >= 1f) Finish();
        }
        void Finish()
        {
            if (shadow != null) Destroy(shadow.gameObject);
            Destroy(gameObject);
        }
    }
}
