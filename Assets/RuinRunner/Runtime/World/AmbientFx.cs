using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Something in a room that keeps moving on its own: a flame, a drip, a bat. Each starts at its own point in the loop so no two beat together,
    /// and draws nothing while it is off screen.</summary>
    public sealed class AmbientFx : MonoBehaviour
    {
        const float FramesPerSecond = 30f, OffScreenMargin = 3f;
        SpriteRenderer view;
        Sprite[] frames;
        float phase;
        Camera viewCamera;

        public static AmbientFx Create(Transform parent, Vector2 position, string clip, float scale = 1f, bool onFloor = false)
        {
            var root = new GameObject("Ambient_" + clip);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;
            var fx = root.AddComponent<AmbientFx>();
            fx.frames = NativeCombatEffect.Frames(clip);
            fx.view = root.AddComponent<SpriteRenderer>();
            fx.view.sprite = fx.frames[0];
            PixelFxMaterial.Apply(fx.view, clip);
            if (onFloor) { fx.view.sortingLayerName = "WorldFloor"; fx.view.sortingOrder = 4; }
            else fx.view.sortingOrder = 900 - Mathf.RoundToInt(position.y * 100f);
            // Spread by position, so a stage comes out the same every time on the same seed.
            fx.phase = Mathf.Abs(position.x * 7.31f + position.y * 3.17f) % (fx.frames.Length / FramesPerSecond);
            return fx;
        }

        void Update()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera != null)
            {
                var offset = (Vector2)(transform.position - viewCamera.transform.position);
                var visible = Mathf.Abs(offset.y) < viewCamera.orthographicSize + OffScreenMargin && Mathf.Abs(offset.x) < viewCamera.orthographicSize * viewCamera.aspect + OffScreenMargin;
                view.enabled = visible;
                if (!visible) return;
            }
            view.sprite = frames[Mathf.FloorToInt((Time.unscaledTime + phase) * FramesPerSecond) % frames.Length];
        }
    }

    /// <summary>Dust in the air of a stage. They keep to the screen by wrapping round it, so there are always a few and they are never many.</summary>
    public sealed class AmbientMotes : MonoBehaviour
    {
        struct Mote { public Transform view; public SpriteRenderer sprite; public Vector2 offset, drift; public float phase, life; }
        const int Count = 26;
        static Sprite dot;
        readonly List<Mote> motes = new List<Mote>();
        Camera viewCamera;
        Color tint;
        Vector2 flow;

        public static AmbientMotes Create(Transform parent, int stage)
        {
            var root = new GameObject("Ambient motes");
            root.transform.SetParent(parent, false);
            var field = root.AddComponent<AmbientMotes>();
            field.tint = stage == 0 ? new Color(1f, .9f, .62f) : stage == 1 ? new Color(.62f, .94f, 1f) : new Color(.82f, .68f, 1f);
            field.flow = stage == 1 ? new Vector2(.05f, .34f) : stage == 0 ? new Vector2(.16f, .1f) : new Vector2(0f, .22f);
            if (dot == null)
            {
                var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                pixel.SetPixel(0, 0, Color.white); pixel.Apply();
                dot = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 32f);
            }
            for (var i = 0; i < Count; i++)
            {
                var view = new GameObject("Mote");
                view.transform.SetParent(root.transform, false);
                var sprite = view.AddComponent<SpriteRenderer>();
                sprite.sprite = dot; sprite.sortingOrder = 2002;
                if (i % 5 == 0) view.transform.localScale = Vector3.one * 2f;
                field.motes.Add(new Mote
                {
                    view = view.transform, sprite = sprite, offset = new Vector2(Random.value, Random.value),
                    drift = new Vector2(Random.Range(-.06f, .06f), Random.Range(-.04f, .08f)), phase = Random.value * 10f, life = Random.Range(2.5f, 6f)
                });
            }
            return field;
        }

        void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;
            var center = (Vector2)viewCamera.transform.position;
            var size = new Vector2(viewCamera.orthographicSize * viewCamera.aspect * 2f + 1f, viewCamera.orthographicSize * 2f + 1f);
            var time = Time.time;
            for (var i = 0; i < motes.Count; i++)
            {
                var mote = motes[i];
                var fraction = mote.offset + (flow + mote.drift) * time / size;
                var point = new Vector2((Mathf.Repeat(fraction.x, 1f) - .5f) * size.x, (Mathf.Repeat(fraction.y, 1f) - .5f) * size.y);
                point.x += Mathf.Sin(time * .7f + mote.phase) * .22f;
                // Whole pixels, like everything else in the world.
                mote.view.position = new Vector3(Mathf.Round((center.x + point.x) * 32f) / 32f, Mathf.Round((center.y + point.y) * 32f) / 32f);
                var color = tint;
                color.a = Mathf.Max(0f, Mathf.Sin((time + mote.phase) / mote.life * Mathf.PI * 2f)) * .55f;
                mote.sprite.color = color;
            }
        }
    }

    /// <summary>The edges of the screen fall away into the dark, and redden while the hero is nearly dead. Under the HUD, over the world.</summary>
    public sealed class ScreenVignette : MonoBehaviour
    {
        const int Width = 320, Height = 180, Depth = 8, Bands = 8;
        const float LowHealth = .3f, Inner = .62f, Outer = 1.38f, DarkEdge = .62f, BloodEdge = .6f;
        static Texture2D ring;
        GameController game;
        float blood;

        public static void Ensure(GameController owner)
        {
            var existing = FindAnyObjectByType<ScreenVignette>();
            if (existing != null) { existing.game = owner; return; }
            var root = new GameObject("ScreenVignette");
            DontDestroyOnLoad(root);
            root.AddComponent<ScreenVignette>().game = owner;
        }

        static Texture2D Ring()
        {
            if (ring != null) return ring;
            ring = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            // Stepped and dithered, so the falloff is drawn in the same pixels as everything else instead of a smooth photo gradient.
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                {
                    var dx = (x + .5f) / Width * 2f - 1f; var dy = (y + .5f) / Height * 2f - 1f;
                    var value = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - Inner) / (Outer - Inner));
                    value *= value;
                    var scaled = value * Bands; var band = Mathf.Floor(scaled);
                    if (scaled - band > (((x ^ y) & 1) == 0 ? .35f : .7f)) band += 1f;
                    ring.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Min(1f, band / Bands)));
                }
            ring.Apply();
            return ring;
        }

        void OnGUI()
        {
            // Depth is read from every GUI event, not only the one that draws.
            GUI.depth = Depth;
            if (Event.current.type != EventType.Repaint || game == null || !game.IsRunActive || game.IsTown) return;
            var area = new Rect(0, 0, Screen.width, Screen.height);
            var old = GUI.color;
            GUI.color = new Color(.012f, .012f, .03f, DarkEdge);
            GUI.DrawTexture(area, Ring());
            var player = game.Player;
            var wanted = player != null && !player.IsDead && player.MaxHealth > 0f && player.Health / player.MaxHealth < LowHealth ? 1f : 0f;
            blood = Mathf.MoveTowards(blood, wanted, Time.unscaledDeltaTime * 2.5f);
            if (blood > 0f)
            {
                var beat = .72f + .28f * Mathf.Sin(Time.unscaledTime * 5.2f);
                GUI.color = new Color(.5f, .03f, .04f, BloodEdge * blood * beat);
                GUI.DrawTexture(area, Ring());
            }
            GUI.color = old;
        }
    }
}
