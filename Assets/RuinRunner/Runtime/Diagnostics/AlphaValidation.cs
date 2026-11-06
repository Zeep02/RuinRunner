using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Rendered atlas QA, explicitly isolated from player saves and normal play.</summary>
    public sealed class AlphaValidation : MonoBehaviour
    {
        private struct Entry { public string name; public Texture2D texture; public Rect rect; }
        private readonly List<Entry> entries = new List<Entry>();
        private readonly HashSet<Texture2D> checkedTextures = new HashSet<Texture2D>();
        private readonly Color[] backgrounds = { new Color(.28f,.48f,.26f), new Color(.08f,.1f,.14f), new Color(.78f,.72f,.55f) };
        private int page;
        private string exportDirectory;
        private const int PerPage = 24;

        private void OnEnable() => Application.logMessageReceived += FailOnException;
        private void OnDisable() => Application.logMessageReceived -= FailOnException;
        private static void FailOnException(string message, string trace, LogType type)
        { if (type == LogType.Exception) Application.Quit(3); }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--validation-capture-dir");
            if (index < 0 || index + 1 >= args.Length) { Debug.LogError("[Alpha QA] capture path required"); Application.Quit(3); yield break; }
            var directory = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(directory);
            exportDirectory = Path.Combine(directory, "RuntimeTextures");
            Directory.CreateDirectory(exportDirectory);
            var camera = new GameObject("AlphaValidationCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            for (var type = 0; type < 4; type++)
            for (var state = 0; state < 3; state++)
                AddFrames(((EnemyType)type) + "/" + state, PixelArtAtlas.GetEnemyFrames((EnemyType)type, state), true);
            for (var state = 0; state < 8; state++) AddFrames(BossSpriteAnimator.RowId(state), PixelArtAtlas.GetBossFrames(state), true);
            AddFrames(BossSpriteAnimator.EntranceId, IndividualArt.Frames(BossSpriteAnimator.EntranceId, BossSpriteAnimator.FrameCount), true);
            foreach (var family in new[] { WeaponFamily.Dagger, WeaponFamily.Longsword, WeaponFamily.Mace, WeaponFamily.Katana, WeaponFamily.Greatsword })
                AddFrames("Slash/" + family, PixelArtAtlas.GetMeleeSlashFrames(family, false), true);
            AddFrames("Slash/Critical", PixelArtAtlas.GetMeleeSlashFrames(WeaponFamily.Longsword, true), true);
            for (var cell = 0; cell < 12; cell++) Add("Environment/" + cell, EnvironmentArt.Prop(cell), true);
            foreach (WorldInteractionArt art in Enum.GetValues(typeof(WorldInteractionArt))) Add("World/" + art, PixelWorldArt.GetSprite(art), false);
            foreach (PixelHudBarType type in Enum.GetValues(typeof(PixelHudBarType)))
            { AddTexture("HUD/" + type + "/empty", PixelHudSkin.GetEmpty(type), true); AddTexture("HUD/" + type + "/filled", PixelHudSkin.GetFilled(type), true); }
            foreach (var weapon in WeaponLootTable.All) Add("Held/" + weapon.Id, PixelArtAtlas.GetHeldWeaponSprite(weapon), false);
            foreach (var accessory in AccessoryLootTable.All) AddTexture("Accessory/" + accessory.Id, PixelUiSkin.GetAccessoryIcon(accessory.Id), false);
            var capture = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            if (capture == null) throw new InvalidOperationException("Screen capture unavailable");
            for (page = 0; page * PerPage < entries.Count; page++)
            {
                yield return new WaitForSeconds(.15f);
                yield return new WaitForEndOfFrame();
                capture.Invoke(null, new object[] { Path.Combine(directory, "alpha-page-" + (page + 1).ToString("00") + ".png"), 1 });
                yield return new WaitForSeconds(.2f);
            }
            Debug.Log("[Alpha QA] PASS " + entries.Count + " runtime sprites/textures; " + checkedTextures.Count + " keyed textures contain zero matte candidates. White foreground is preserved, not RGB-keyed. Inspect captured pages for visual approval.");
            Application.Quit(0);
        }

        private void AddFrames(string name, Sprite[] frames, bool keyed)
        { for (var i = 0; i < frames.Length; i++) Add(name + "/" + i, frames[i], keyed); }

        private void Add(string name, Sprite sprite, bool keyed)
        {
            if (sprite == null) throw new InvalidOperationException("Missing " + name);
            CheckKey(sprite.texture, keyed);
            entries.Add(new Entry { name = name, texture = sprite.texture, rect = sprite.rect });
        }

        private void AddTexture(string name, Texture2D texture, bool keyed)
        {
            if (texture == null) throw new InvalidOperationException("Missing " + name);
            CheckKey(texture, keyed);
            entries.Add(new Entry { name = name, texture = texture, rect = new Rect(0, 0, texture.width, texture.height) });
        }

        private void CheckKey(Texture2D texture, bool keyed)
        {
            if (!keyed || !checkedTextures.Add(texture)) return;
            var pixels=SpriteVisibleBounds.ReadPixels(texture);
            foreach (var pixel in pixels)
                if (pixel.a > 0 && SpriteMatte.IsMagentaMatte(pixel)) throw new InvalidOperationException("Remaining matte in " + texture.name);
            var inspected=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try
            {
                inspected.SetPixels32(pixels);inspected.Apply();
                File.WriteAllBytes(Path.Combine(exportDirectory, checkedTextures.Count.ToString("00") + "-" + texture.name + ".png"), inspected.EncodeToPNG());
            }
            finally{Destroy(inspected);}
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1f));
            var label = new GUIStyle(GUI.skin.label) { font = GameTypography.Regular, fontSize = 12 };
            label.normal.textColor = Color.white;
            GUI.Label(new Rect(12, 8, 1256, 32), "Runtime alpha QA — page " + (page + 1) + " / green · dark · light backgrounds; original white highlights must remain", label);
            for (var slot = 0; slot < PerPage && page * PerPage + slot < entries.Count; slot++)
            {
                var entry = entries[page * PerPage + slot];
                var cell = new Rect(12 + slot % 6 * 210, 46 + slot / 6 * 164, 202, 154);
                var old = GUI.color;
                GUI.color = backgrounds[slot % 3]; GUI.DrawTexture(cell, Texture2D.whiteTexture); GUI.color = old;
                var scale = Mathf.Min(180f / entry.rect.width, 122f / entry.rect.height);
                if (scale >= 1f) scale = Mathf.Floor(scale);
                var rect = new Rect(cell.center.x - entry.rect.width * scale * .5f, cell.y + 6, entry.rect.width * scale, entry.rect.height * scale);
                var uv = new Rect(entry.rect.x / entry.texture.width, entry.rect.y / entry.texture.height, entry.rect.width / entry.texture.width, entry.rect.height / entry.texture.height);
                GUI.DrawTextureWithTexCoords(rect, entry.texture, uv, true);
                GUI.Label(new Rect(cell.x + 4, cell.yMax - 22, cell.width - 8, 22), entry.name, label);
            }
        }
    }
}
