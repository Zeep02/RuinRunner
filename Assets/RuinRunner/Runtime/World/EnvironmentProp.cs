using UnityEngine;

namespace RuinRunner
{
    public enum EnvironmentPropKind { Bush, Grass, Pillar, Urn, Banner, Brazier, Tree }

    /// <summary>What a piece of room dressing does when something runs into it.
    /// Soft things are walked through and rustle. Everything solid can be broken: fragile at a touch, sturdy with a few hits, volatile on a short fuse.</summary>
    public enum DressingKind { Soft, Fragile, Sturdy, Volatile }

    [System.Serializable]
    public struct RoomDecorationData
    {
        public EnvironmentPropKind kind;
        public Vector2 position;
        [Range(.5f, 3f)] public float scale;
        public RoomDecorationData(EnvironmentPropKind type, float x, float y, float size = 1f)
        {
            kind = type;
            position = new Vector2(x, y);
            scale = size;
        }
    }

    public sealed class EnvironmentProp : MonoBehaviour
    {
        private EnvironmentPropKind kind;
        private SpriteRenderer visual;
        private int firstFrame;
        private bool animated;
        private bool broken;
        private bool dressing;
        private GameObject body;
        private float phase;
        private float rustleUntil;
        private PlayerController player;
        private float nextPlayerSearch;

        public static EnvironmentProp Create(Transform parent, Vector2 position, EnvironmentPropKind kind, float scale = 1f)
        {
            var root = new GameObject("Environment_" + kind);
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;
            var prop = root.AddComponent<EnvironmentProp>();
            prop.kind = kind;
            prop.firstFrame = kind == EnvironmentPropKind.Bush ? 0 : kind == EnvironmentPropKind.Grass ? 2 :
                kind == EnvironmentPropKind.Pillar ? 4 : kind == EnvironmentPropKind.Urn ? 5 : kind == EnvironmentPropKind.Banner ? 8 : 10;
            prop.animated = kind == EnvironmentPropKind.Bush || kind == EnvironmentPropKind.Grass || kind == EnvironmentPropKind.Banner || kind == EnvironmentPropKind.Brazier;
            prop.visual = root.AddComponent<SpriteRenderer>();
            prop.visual.sprite = kind == EnvironmentPropKind.Tree ? IndividualArt.Sprite("ContinuousWorld/MeadowTree") : EnvironmentArt.Prop(prop.firstFrame);
            prop.visual.sortingOrder = 900 - Mathf.RoundToInt(position.y * 100f);
            prop.phase = Mathf.Abs(position.x * .37f + position.y * .73f) % 1f;
            if (kind == EnvironmentPropKind.Urn || kind == EnvironmentPropKind.Grass)
            {
                root.layer = 10;
                var trigger = root.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(.7f, .75f);
                trigger.offset = new Vector2(0f, .32f);
            }
            return prop;
        }

        const int HitsToBreakSturdy = 3, ExplosionDamageToHero = 6;
        const float SturdyHitGap = .12f, FlinchSeconds = .16f, FlinchPixels = 2f / 32f, FuseSeconds = .6f, ExplosionRadius = 1.7f, ExplosionBaseDamage = 40f, PotManaChance = .3f, PotMana = 8f;
        const float ClipFramesPerSecond = 30f, IdleSway = 1.1f, RustleKick = 110f, SwaySpring = 60f, SwayDamping = 5f, RustleReach = .62f, LookAfterWithin = 16f, RustleLeafGap = .5f;
        private DressingKind dressingKind;
        private int hits;
        private float flinchUntil, nextHitAt, fuseEndsAt = -1f, swayAngle, swayVelocity, nextLeafAt;
        private Vector2 restingPlace;
        private Sprite[] clipFrames;
        private float clipRate = ClipFramesPerSecond;
        public string LivingClip { get; private set; }

        /// <summary>A looping effect that sits on this piece and goes when it goes: the embers over a candle, the glints on a crystal.</summary>
        public void AttachAmbient(string clip, Vector2 offset)
        {
            var fx = AmbientFx.Create(transform, (Vector2)transform.position + offset, clip);
            fx.GetComponent<SpriteRenderer>().sortingOrder = visual.sortingOrder + 1;
            if (LivingClip == null)
                LivingClip = clip;
        }

        // Room dressing. Soft pieces have no body at all. A clip is a piece that is never a still picture, like a burning brazier.
        public static EnvironmentProp CreateDressing(Transform parent, Vector2 position, Sprite sprite, DressingKind kind, Vector2 footSize, string clip = null, float clipFramesPerSecond = ClipFramesPerSecond)
        {
            var root = new GameObject(kind + " dressing");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var prop = root.AddComponent<EnvironmentProp>();
            prop.dressing = true;
            prop.dressingKind = kind;
            prop.restingPlace = position;
            prop.phase = Mathf.Abs(position.x * 7.31f + position.y * 3.17f);
            prop.kind = EnvironmentPropKind.Urn;
            prop.visual = root.AddComponent<SpriteRenderer>();
            prop.visual.sprite = sprite;
            prop.visual.sortingOrder = 900 - Mathf.RoundToInt(position.y * 100f);
            if (clip != null)
            {
                prop.clipFrames = NativeCombatEffect.Frames(clip);
                prop.clipRate = clipFramesPerSecond;
                prop.LivingClip = clip;
                prop.visual.sprite = prop.clipFrames[0];
                PixelFxMaterial.Apply(prop.visual, clip);
            }
            if (kind != DressingKind.Soft)
            {
                var solid = new GameObject("Dressing body");
                solid.transform.SetParent(root.transform, false);
                solid.layer = GameLayers.Ground;
                solid.AddComponent<BoxCollider2D>().size = footSize;
                prop.body = solid;
            }
            // Attacks find a prop by this trigger; the body above is only what stops people.
            root.layer = PropLayer;
            var trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(.7f, .75f);
            trigger.offset = new Vector2(0f, .32f);
            return prop;
        }

        const int PropLayer = 10;
        public static void HitCircle(Vector2 point, float radius)
        {
            foreach (var found in Physics2D.OverlapCircleAll(point, radius, 1 << PropLayer))
                HitBody(found);
        }
        public static void HitSegment(Vector2 from, Vector2 to, float width)
        {
            var path = to - from;
            if (path.sqrMagnitude < .0001f)
            {
                HitCircle(from, width * .5f);
                return;
            }
            foreach (var found in Physics2D.CircleCastAll(from, width * .5f, path.normalized, path.magnitude, 1 << PropLayer))
                HitBody(found.collider);
        }
        public static void HitBody(Collider2D found)
        {
            if (found == null)
                return;
            var prop = found.GetComponentInParent<EnvironmentProp>();
            if (prop != null)
                prop.Hit();
        }

        void Shatter(string clip, float scale)
        {
            if (broken)
                return;
            broken = true;
            if (StageValidationInput.Enabled)
                Debug.Log("[Prop] broke " + dressingKind + " with " + clip);
            var point = restingPlace;
            SigFx.Play(clip, point, 0, -1, true, false, scale);
            if (dressingKind != DressingKind.Soft)
                WeaponSecondaryParticles.Fan("StoneChip", point + Vector2.up * .25f, Vector2.up, 6, 300, 3.6f, .42f, .6f, visual.sortingOrder, Color.white, 6);
            GameAudio.Play(GameSound.Hit, AudioChannel.Ambience, dressingKind == DressingKind.Soft ? .12f : .35f);
            if (body != null)
                Destroy(body);
            Destroy(gameObject);
        }

        void Explode()
        {
            var point = restingPlace;
            Shatter("BreakBarrel", .62f);
            CombatStain.Place(point, "StainScorch", 1f);
            foreach (var enemy in CombatActionGeometry.Circle(point, ExplosionRadius))
                enemy.TakeDamage(ExplosionBaseDamage, ((Vector2)enemy.transform.position - point).normalized * 5f, false);
            foreach (var hero in PlayerController.ActiveCombatants)
                if (hero != null && Vector2.Distance(hero.transform.position, point) < ExplosionRadius)
                    hero.TakeDamage(ExplosionDamageToHero, ((Vector2)hero.transform.position - point).normalized);
            // One barrel beside another is a chain.
            HitCircle(point, ExplosionRadius);
        }

        void GiveManaFromPot()
        {
            if (Random.value >= PotManaChance)
                return;
            foreach (var hero in PlayerController.ActiveCombatants)
                if (hero != null)
                    hero.Stats.RestoreMana(PotMana);
            NativeCombatEffect.Create("RewardSpark", restingPlace + Vector2.up * .4f, 0, .5f, .5f, new Color(.45f, .7f, 1f), visual.sortingOrder + 1);
        }

        public void Hit()
        {
            if (broken)
                return;
            if (dressing)
            {
                switch (dressingKind)
                {
                    case DressingKind.Soft:
                        Shatter("BreakPlant", .5f);
                        break;
                    case DressingKind.Fragile:
                        GiveManaFromPot();
                        Shatter(clipFrames != null ? "BreakBarrel" : "BreakPot", clipFrames != null ? .3f : .34f);
                        break;
                    case DressingKind.Volatile:
                        if (fuseEndsAt < 0f)
                        {
                            fuseEndsAt = Time.time + FuseSeconds;
                            flinchUntil = fuseEndsAt;
                        }
                        break;
                    default:
                        // A sweep that touches it for several frames is still one blow.
                        if (Time.time < nextHitAt)
                            break;
                        nextHitAt = Time.time + SturdyHitGap;
                        flinchUntil = Time.time + FlinchSeconds;
                        if (++hits >= HitsToBreakSturdy)
                        {
                            Shatter("BreakStone", .4f);
                            break;
                        }
                        WeaponSecondaryParticles.Fan("StoneChip", restingPlace + Vector2.up * .25f, Vector2.up, 3, 300, 3f, .3f, .5f, visual.sortingOrder, Color.white, 6);
                        GameAudio.Play(GameSound.Hit, AudioChannel.Ambience, .2f);
                        break;
                }
                return;
            }
            if (kind == EnvironmentPropKind.Grass)
            {
                rustleUntil = Time.time + .6f;
                return;
            }
            if (kind != EnvironmentPropKind.Urn)
                return;
            broken = true;
            visual.sprite = EnvironmentArt.Prop(6);
            var trigger = GetComponent<Collider2D>();
            if (trigger != null)
                trigger.enabled = false;
            WeaponSecondaryParticles.Fan("StoneChip", (Vector2)transform.position + Vector2.up * .3f, Vector2.up, 6, 300, 3.6f, .42f, .6f, visual.sortingOrder, Color.white, 6);
            GameAudio.Play(GameSound.Hit, AudioChannel.Ambience, .35f);
        }

        void UpdateDressing()
        {
            PlayerController hero = null;
            foreach (var combatant in PlayerController.ActiveCombatants)
            {
                hero = combatant;
                break;
            }
            if (hero == null || (hero.transform.position - transform.position).sqrMagnitude > LookAfterWithin * LookAfterWithin)
                return;
            if (clipFrames != null)
                visual.sprite = clipFrames[Mathf.FloorToInt((Time.unscaledTime + phase) * clipRate) % clipFrames.Length];
            if (fuseEndsAt >= 0f)
            {
                visual.color = Mathf.Sin(Time.time * 42f) > 0f ? new Color(1f, .45f, .3f) : Color.white;
                if (Time.time >= fuseEndsAt)
                {
                    Explode();
                    return;
                }
            }
            if (Time.time < flinchUntil)
                transform.position = restingPlace + Vector2.right * (Mathf.Sin(Time.time * 70f) > 0f ? FlinchPixels : -FlinchPixels);
            else if ((Vector2)transform.position != restingPlace)
                transform.position = restingPlace;
            if (dressingKind != DressingKind.Soft)
                return;
            // Plants lean away as the hero pushes through, spring back, and otherwise just stir.
            var away = (Vector2)transform.position - (Vector2)hero.transform.position;
            if (away.sqrMagnitude < RustleReach * RustleReach && hero.MovementInput.sqrMagnitude > .1f)
            {
                swayVelocity += (away.x >= 0f ? -1f : 1f) * RustleKick * Time.deltaTime;
                if (Time.time >= nextLeafAt)
                {
                    nextLeafAt = Time.time + RustleLeafGap;
                    SigFx.Play("BreakPlant", restingPlace, 0, -1, true, false, .22f);
                }
            }
            swayVelocity += (-swayAngle * SwaySpring - swayVelocity * SwayDamping) * Time.deltaTime;
            swayAngle += swayVelocity * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, swayAngle + Mathf.Sin(Time.time * 1.7f + phase) * IdleSway);
        }

        private void Update()
        {
            if (dressing)
            {
                if (!broken)
                    UpdateDressing();
                return;
            }
            if (broken || !animated)
                return;
            if (player == null && Time.time >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.time + 1f;
                player = Object.FindAnyObjectByType<PlayerController>();
            }
            if ((kind == EnvironmentPropKind.Grass || kind == EnvironmentPropKind.Bush) && player != null &&
                Vector2.Distance(player.transform.position, transform.position) < .8f && player.MovementInput.sqrMagnitude > .1f)
                rustleUntil = Time.time + .35f;
            var rate = kind == EnvironmentPropKind.Brazier ? 7f : Time.time < rustleUntil ? 10f : 2f;
            visual.sprite = EnvironmentArt.Prop(firstFrame + (Mathf.FloorToInt((Time.time + phase) * rate) & 1));
        }
    }
}
