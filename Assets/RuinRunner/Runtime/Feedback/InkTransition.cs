using System;
using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The way between places: ink bleeds out from the hero until nothing shows, drains back to a puddle at their feet, and they stand up out of it.
    /// Drawn on top of the HUD while it moves; behind it while it is only the backdrop of the result screen.</summary>
    public sealed class InkTransition : MonoBehaviour
    {
        const float FramesPerSecond = 30f, RiseStartsIntoRecede = .38f, TitleFadeOut = .35f, TitleFadeIn = .4f, TitleAlpha = .5f, TitleHeight = .16f, LargestOverscan = 2.4f;
        const int OverHudDepth = -100, BehindHudDepth = 5;
        static readonly Color Ink = new Color32(8, 8, 14, 255);
        enum Phase { Clear, Cover, Hold, Recede }

        static InkTransition instance;
        Phase phase;
        Sprite[] frames;
        float age, titleSince, titleLeaving = -1f;
        Vector2 feet;
        string title = "";
        bool behindHud;
        Func<bool> keepWhile;
        GUIStyle titleStyle;

        public static bool LocksInput => instance != null && instance.phase != Phase.Clear && !instance.behindHud;

        static InkTransition Get()
        {
            if (instance != null)
                return instance;
            var root = new GameObject("InkTransition");
            DontDestroyOnLoad(root);
            return instance = root.AddComponent<InkTransition>();
        }

        void Begin(Phase next, string clip, Vector2 point)
        {
            phase = next;
            age = 0f;
            feet = point;
            frames = clip == null ? null : NativeCombatEffect.Frames(clip);
        }

        /// <summary>Bleeds out from these feet until the screen is gone, and stays that way. The title comes up once it is.</summary>
        public static IEnumerator Cover(Vector2 point, string titleAfter = "")
        {
            var ink = Get();
            ink.behindHud = false;
            ink.keepWhile = null;
            ink.title = titleAfter;
            ink.titleLeaving = -1f;
            ink.Begin(Phase.Cover, "InkCover", point);
            while (ink != null && ink.phase == Phase.Cover)
                yield return null;
        }

        /// <summary>Black right now, for when there was nothing on screen worth covering.</summary>
        public static void Hold(string titleText = "")
        {
            var ink = Get();
            ink.behindHud = false;
            ink.keepWhile = null;
            ink.title = titleText;
            ink.titleLeaving = -1f;
            if (ink.phase != Phase.Hold)
                ink.titleSince = Time.unscaledTime;
            ink.Begin(Phase.Hold, null, ink.feet);
        }

        /// <summary>Drains to a puddle at the hero's feet and lifts them out of it. Returns once the ink has run off them and they can move.</summary>
        public static IEnumerator Reveal(PlayerController hero)
        {
            var ink = Get();
            var point = hero.FeetPoint;
            hero.HideForInk(true, true);
            if (ink.phase == Phase.Hold && ink.title.Length > 0)
            {
                ink.titleLeaving = Time.unscaledTime;
                yield return new WaitForSecondsRealtime(TitleFadeOut);
            }
            ink.title = "";
            ink.Begin(Phase.Recede, "InkRecede", point);
            yield return new WaitForSecondsRealtime(RiseStartsIntoRecede);
            yield return InkRise.Lift(hero, point);
        }

        /// <summary>The ink behind the result screen. True once there is nothing left to see but it.</summary>
        public static bool CoverForResult(Vector2 point, Func<bool> whileTrue)
        {
            var ink = Get();
            if (ink.phase == Phase.Clear)
            {
                ink.title = "";
                ink.titleLeaving = -1f;
                ink.keepWhile = whileTrue;
                ink.behindHud = false;
                ink.Begin(Phase.Cover, "InkCover", point);
            }
            if (ink.phase != Phase.Hold)
                return false;
            ink.behindHud = true;
            return true;
        }

        public static void Clear()
        {
            if (instance == null)
                return;
            instance.phase = Phase.Clear;
            instance.behindHud = false;
            instance.keepWhile = null;
        }

        void Update()
        {
            if (phase == Phase.Clear)
                return;
            if (keepWhile != null && !keepWhile())
            {
                Clear();
                return;
            }
            age += Time.unscaledDeltaTime;
            if (frames == null || age * FramesPerSecond < frames.Length)
                return;
            if (phase == Phase.Cover)
            {
                phase = Phase.Hold;
                frames = null;
                titleSince = Time.unscaledTime;
            }
            else if (phase == Phase.Recede)
                Clear();
        }

        void OnGUI()
        {
            // Depth is read from every GUI event, not only the one that draws.
            GUI.depth = behindHud ? BehindHudDepth : OverHudDepth;
            if (phase == Phase.Clear || Event.current.type != EventType.Repaint)
                return;
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            if (frames == null)
            {
                Fill(screen);
                DrawTitle();
                return;
            }
            var frame = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(age * FramesPerSecond))];
            // Every frame is drawn around the hero, and big enough to reach whichever edge of the screen is further from them.
            var focus = screen.center;
            var camera = Camera.main;
            if (camera != null)
            {
                var point = camera.WorldToScreenPoint(feet);
                focus = new Vector2(point.x, Screen.height - point.y);
            }
            var over = Mathf.Min(LargestOverscan, 1f + 2f * Mathf.Max(Mathf.Abs(focus.x - screen.center.x) / screen.width, Mathf.Abs(focus.y - screen.center.y) / screen.height));
            var area = new Rect(focus.x - screen.width * over * .5f, focus.y - screen.height * over * .5f, screen.width * over, screen.height * over);
            var texture = frame.texture;
            var source = frame.textureRect;
            GUI.DrawTextureWithTexCoords(area, texture, new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
        }

        static void Fill(Rect area)
        {
            var old = GUI.color;
            GUI.color = Ink;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void DrawTitle()
        {
            if (title.Length == 0)
                return;
            if (titleStyle == null)
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = GameTypography.Regular };
            titleStyle.fontSize = Mathf.RoundToInt(26f * GameTypography.LayoutScale);
            var alpha = Mathf.Clamp01((Time.unscaledTime - titleSince) / TitleFadeIn);
            if (titleLeaving >= 0f)
                alpha = Mathf.Min(alpha, 1f - Mathf.Clamp01((Time.unscaledTime - titleLeaving) / TitleFadeOut));
            var old = GUI.color;
            GUI.color = new Color(.74f, .76f, .8f, alpha * TitleAlpha);
            GUI.Label(new Rect(0, Screen.height * TitleHeight - 30f, Screen.width, 60f), title, titleStyle);
            GUI.color = old;
        }
    }

    /// <summary>The ink that stands up round whoever is coming back. The puddle lies under them, the rest runs down over them.</summary>
    public sealed class InkRise : MonoBehaviour
    {
        const float FramesPerSecond = 30f;
        const float DrainStartsAfter = .7f, DrainedAfter = 1.02f;
        SpriteRenderer puddle, mound;
        Sprite[] puddleFrames, moundFrames;
        float age;

        // The hero comes back at the moment the top of the ink starts to come down off them, so it uncovers them from the head down.
        public static IEnumerator Lift(PlayerController hero, Vector2 feet)
        {
            Play(feet);
            yield return new WaitForSecondsRealtime(DrainStartsAfter);
            if (hero != null)
                hero.HideForInk(false, true);
            yield return new WaitForSecondsRealtime(DrainedAfter - DrainStartsAfter);
            if (hero != null)
                hero.HideForInk(false, false);
        }

        /// <summary>Death put off: the same ink closes over the hero where they fell and stands them back up.</summary>
        public static IEnumerator Revive(PlayerController hero)
        {
            hero.SetEntranceLock(true);
            hero.HideForInk(true, true);
            yield return Lift(hero, hero.FeetPoint);
            if (hero != null)
                hero.SetEntranceLock(false);
        }

        public static void Play(Vector2 feet, float scale = 1f)
        {
            var root = new GameObject("InkRise");
            root.transform.position = feet;
            root.transform.localScale = Vector3.one * scale;
            var rise = root.AddComponent<InkRise>();
            rise.puddleFrames = NativeCombatEffect.Frames("InkPuddle");
            rise.moundFrames = NativeCombatEffect.Frames("InkMound");
            rise.puddle = rise.Layer(root, "InkPuddle", rise.puddleFrames);
            rise.puddle.sortingLayerName = "WorldFloor";
            rise.puddle.sortingOrder = 13;
            rise.mound = rise.Layer(root, "InkMound", rise.moundFrames);
            rise.mound.sortingOrder = 2003;
        }

        SpriteRenderer Layer(GameObject root, string clip, Sprite[] frames)
        {
            var child = new GameObject(clip);
            child.transform.SetParent(root.transform, false);
            var view = child.AddComponent<SpriteRenderer>();
            view.sprite = frames[0];
            PixelFxMaterial.Apply(view, clip);
            return view;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            var index = Mathf.FloorToInt(age * FramesPerSecond);
            if (index >= puddleFrames.Length)
            {
                Destroy(gameObject);
                return;
            }
            puddle.sprite = puddleFrames[index];
            if (index < moundFrames.Length)
                mound.sprite = moundFrames[index];
            else
                mound.enabled = false;
        }
    }
}
