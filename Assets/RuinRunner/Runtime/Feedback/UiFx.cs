using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Effects drawn on the interface itself rather than in the world: bursts over a chosen item, a full-screen flash, film bars.
    /// They keep running while the world is paused behind a menu, and outlive the panel that started them.</summary>
    public sealed class UiFx : MonoBehaviour
    {
        const float FramesPerSecond = 30f;
        const int Depth = -60;
        struct Playing
        {
            public Sprite[] frames;
            public Rect area;
            public float since;
        }
        static UiFx instance;
        readonly List<Playing> playing = new List<Playing>();
        float flashUntil, flashSeconds, barsWanted, bars;
        Color flashColor;

        static UiFx Get()
        {
            if (instance != null)
                return instance;
            var root = new GameObject("UiFx");
            DontDestroyOnLoad(root);
            return instance = root.AddComponent<UiFx>();
        }

        /// <summary>Plays a clip once, centred on a point in the HUD's layout coordinates.</summary>
        public static void Play(string clip, Vector2 layoutCentre, float layoutSize)
        {
            var frames = NativeCombatEffect.Frames(clip);
            var scale = GameTypography.LayoutScale;
            var first = frames[0].rect;
            var height = layoutSize * first.height / Mathf.Max(first.width, first.height) * scale;
            var width = layoutSize * first.width / Mathf.Max(first.width, first.height) * scale;
            Get().playing.Add(new Playing
            {
                frames = frames,
                since = Time.unscaledTime,
                area = new Rect(layoutCentre.x * scale - width * .5f, layoutCentre.y * scale - height * .5f, width, height)
            });
        }

        public static void Flash(Color color, float seconds)
        {
            var fx = Get();
            fx.flashColor = color;
            fx.flashSeconds = seconds;
            fx.flashUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Black bars closing in from the top and bottom of the screen, for a moment that is out of the player's hands.</summary>
        public static void FilmBars(bool shown) => Get().barsWanted = shown ? 1f : 0f;

        /// <summary>A dry brush stroke behind a line of words: drags in, holds, breaks up. Age and time left are in seconds. Draw it first, then the words.</summary>
        public static void BrushStroke(Rect area, float age, float left)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var enter = NativeCombatEffect.Frames("BannerIn");
            var leave = NativeCombatEffect.Frames("BannerOut");
            Sprite frame;
            if (left * FramesPerSecond < leave.Length)
                frame = leave[Mathf.Clamp(leave.Length - 1 - Mathf.FloorToInt(left * FramesPerSecond), 0, leave.Length - 1)];
            else
                frame = enter[Mathf.Clamp(Mathf.FloorToInt(age * FramesPerSecond), 0, enter.Length - 1)];
            Draw(frame, area);
        }

        static void Draw(Sprite frame, Rect area)
        {
            var texture = frame.texture;
            var source = frame.textureRect;
            var coordinates = new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
            // The material is what lets a clip's light add to what is under it, as it does in the world.
            if (Event.current.type == EventType.Repaint)
                Graphics.DrawTexture(area, texture, coordinates, 0, 0, 0, 0, GUI.color, PixelFxMaterial.Shared);
        }

        void OnGUI()
        {
            GUI.depth = Depth;
            if (Event.current.type != EventType.Repaint)
                return;
            bars = Mathf.MoveTowards(bars, barsWanted, Time.unscaledDeltaTime * 3.2f);
            var old = GUI.color;
            if (bars > 0f)
            {
                var height = Screen.height * .11f * bars * (2f - bars);
                GUI.color = new Color(.02f, .02f, .035f, 1f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - height, Screen.width, height), Texture2D.whiteTexture);
            }
            GUI.color = old;
            for (var i = playing.Count - 1; i >= 0; i--)
            {
                var item = playing[i];
                var index = Mathf.FloorToInt((Time.unscaledTime - item.since) * FramesPerSecond);
                if (index >= item.frames.Length)
                {
                    playing.RemoveAt(i);
                    continue;
                }
                Draw(item.frames[index], item.area);
            }
            if (Time.unscaledTime < flashUntil)
            {
                var color = flashColor;
                color.a *= (flashUntil - Time.unscaledTime) / flashSeconds;
                GUI.color = color;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }
    }

    /// <summary>Which stage's colours the small effects should use.</summary>
    public static class StageFx
    {
        static GameController game;
        public static string Theme
        {
            get
            {
                if (game == null)
                    game = Object.FindAnyObjectByType<GameController>();
                return StageTerrainArt.Theme(game != null && game.IsRunActive && !game.IsTown ? game.CurrentStageIndex : 0);
            }
        }
    }

    /// <summary>The mark something leaves on the floor where it fell. Rooms end up showing where the fight was.</summary>
    public sealed class CombatStain : MonoBehaviour
    {
        const int MostAtOnce = 40;
        const float FramesPerSecond = 30f, Life = 14f, FadeSeconds = 3f, Strength = .82f;
        static readonly List<CombatStain> stains = new List<CombatStain>();
        SpriteRenderer view;
        Sprite[] frames;
        float age;

        public static void Place(Vector2 point, string clip, float scale)
        {
            if (stains.Count >= MostAtOnce && stains[0] != null)
                Destroy(stains[0].gameObject);
            var root = new GameObject("Stain");
            root.transform.position = new Vector3(Mathf.Round(point.x * 32f) / 32f, Mathf.Round(point.y * 32f) / 32f);
            root.transform.localScale = Vector3.one * scale;
            var stain = root.AddComponent<CombatStain>();
            stain.frames = NativeCombatEffect.Frames(clip);
            stain.view = root.AddComponent<SpriteRenderer>();
            stain.view.sprite = stain.frames[0];
            stain.view.flipX = Random.value < .5f;
            stain.view.flipY = Random.value < .5f;
            stain.view.sortingLayerName = "WorldFloor";
            stain.view.sortingOrder = 5;
            PixelFxMaterial.Apply(stain.view, clip);
            stain.Shade(Strength);
            stains.Add(stain);
        }

        public static void ClearAll()
        {
            foreach (var stain in stains)
                if (stain != null)
                    Destroy(stain.gameObject);
            stains.Clear();
        }

        void Shade(float amount) => view.color = new Color(amount, amount, amount, amount);

        void Update()
        {
            age += Time.deltaTime;
            view.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(age * FramesPerSecond))];
            if (age > Life - FadeSeconds)
                Shade(Strength * Mathf.Clamp01((Life - age) / FadeSeconds));
            if (age >= Life)
                Destroy(gameObject);
        }

        void OnDestroy() => stains.Remove(this);
    }

    /// <summary>A small scuff on every other footfall, in whatever the stage's floor is made of.</summary>
    public sealed class PlayerFootfalls : MonoBehaviour
    {
        const float StepSeconds = .27f, MinimumSpeed = 1f, SideStep = .07f;
        PlayerController player;
        float nextStep;
        bool left;
        Vector2 lastPoint;

        public void Initialize(PlayerController owner)
        {
            player = owner;
            lastPoint = owner.transform.position;
        }

        void Update()
        {
            if (Time.deltaTime <= 0f)
                return;
            var point = (Vector2)transform.position;
            var speed = (point - lastPoint).magnitude / Time.deltaTime;
            lastPoint = point;
            if (player.IsDead || player.IsDashing || speed < MinimumSpeed || Time.time < nextStep)
                return;
            nextStep = Time.time + StepSeconds;
            left = !left;
            SigFx.Play("Step" + StageFx.Theme, player.FeetPoint + Vector2.right * (left ? -SideStep : SideStep), 0, -1, true);
        }
    }

    /// <summary>A guardian going down: film bars, explosions walking across it in the slowed time, and one last blast with a flash.</summary>
    public sealed class BossFallPresentation : MonoBehaviour
    {
        static readonly float[] Bursts = { .05f, .3f, .5f, .7f, .85f };
        const float FinalAt = 1.05f, BarsOffAt = 2f, Spread = 1.1f;
        Vector2 centre;
        CameraFollow view;
        float age;
        int next;
        bool finished;

        public static void Play(Vector2 point, CameraFollow camera)
        {
            var presentation = new GameObject("BossFallPresentation").AddComponent<BossFallPresentation>();
            presentation.centre = point;
            presentation.view = camera;
            UiFx.FilmBars(true);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            while (next < Bursts.Length && age >= Bursts[next])
            {
                var offset = new Vector2(Random.Range(-Spread, Spread), Random.Range(-.3f, Spread));
                SigFx.Play("BreakBarrel", centre + offset, 0, -1, true, false, .55f);
                if (view != null)
                    view.Shake(.12f, .18f);
                next++;
            }
            if (!finished && age >= FinalAt)
            {
                finished = true;
                SigFx.Play("BossFall", centre, 0, -1, true, false, .85f);
                CombatStain.Place(centre, "StainScorch", 2.2f);
                UiFx.Flash(new Color(1f, .96f, .86f, .75f), .28f);
                if (view != null)
                    view.Shake(.5f, .45f);
            }
            if (age >= BarsOffAt)
            {
                UiFx.FilmBars(false);
                Destroy(gameObject);
            }
        }

        void OnDestroy() => UiFx.FilmBars(false);
    }

    /// <summary>The title screen's air: leaves coming down through the light. Drawn in the same pixels as the rest of the menu.</summary>
    public static class MenuAmbience
    {
        const int Leaves = 26, Motes = 34;
        static readonly Color[] LeafColours = { new Color(.42f, .62f, .2f), new Color(.62f, .72f, .24f), new Color(.86f, .66f, .22f) };

        public static void Draw(float width, float height)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var time = Time.unscaledTime;
            var old = GUI.color;
            for (var i = 0; i < Motes; i++)
            {
                var seed = i * 12.9898f;
                var x = Mathf.Repeat(Frac(seed) * width + Mathf.Sin(time * .3f + seed) * 34f + time * 5f, width);
                var y = Mathf.Repeat(Frac(seed * 3.1f) * height - time * (6f + Frac(seed * 7f) * 10f), height);
                var alpha = Mathf.Max(0f, Mathf.Sin(time * (.5f + Frac(seed * 5f)) + seed)) * .6f;
                GUI.color = new Color(1f, .94f, .62f, alpha);
                GUI.DrawTexture(new Rect(Mathf.Round(x), Mathf.Round(y), 3f, 3f), Texture2D.whiteTexture);
            }
            for (var i = 0; i < Leaves; i++)
            {
                var seed = i * 78.233f;
                var fall = 22f + Frac(seed) * 26f;
                var x = Mathf.Repeat(Frac(seed * 2.3f) * width + Mathf.Sin(time * .8f + seed) * 46f + time * 16f, width + 40f) - 20f;
                var y = Mathf.Repeat(Frac(seed * 4.1f) * height + time * fall, height + 40f) - 20f;
                // A leaf turning over is wide, then thin, then wide, in whole steps of the pixel grid.
                var turn = Mathf.Abs(Mathf.Sin(time * (1.2f + Frac(seed * 6f)) + seed));
                var size = 6f + Frac(seed * 9f) * 3f;
                var leaf = new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Max(3f, Mathf.Round(size * turn / 3f) * 3f), 3f * (turn > .5f ? 2f : 1f));
                var colour = LeafColours[i % LeafColours.Length];
                GUI.color = new Color(colour.r * .55f, colour.g * .55f, colour.b * .55f, .9f);
                GUI.DrawTexture(new Rect(leaf.x, leaf.y + 3f, leaf.width, leaf.height), Texture2D.whiteTexture);
                GUI.color = new Color(colour.r, colour.g, colour.b, .95f);
                GUI.DrawTexture(leaf, Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        static float Frac(float value) => Mathf.Repeat(Mathf.Abs(Mathf.Sin(value) * 43758.547f), 1f);
    }
}
