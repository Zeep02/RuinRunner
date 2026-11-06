using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Opt-in GPU source identity fixture; no normal run, rewards or user save access.</summary>
    public sealed class NativeHeroAtlasValidation : MonoBehaviour
    {
        private string directory;
        private readonly Proof proof = new Proof();
        private RenderTexture target;
        private Texture2D readback;
        private Camera capture;
        private SpriteRenderer[] cells;
        private const int Width = 1024, Height = 640, Columns = 16, Rows = 8;

        [Serializable]
        private sealed class Proof
        {
            public string result = "RUNNING", graphicsDevice, startedAtUtc, endedAtUtc, error;
            public int sourceFrames, comparedPixels, gpuPages, mismatches;
            public string scope = "Actual Windows SpriteRenderer/atlas UVs at native 1:1 pixels, every current individual frame; not gameplay/motion acceptance or performance.";
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--validation-capture-dir");
            if (index < 0 || index + 1 >= args.Length)
            {
                Debug.LogError("[Native Hero GPU] Missing capture directory");
                Application.Quit(3);
                yield break;
            }
            directory = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(directory);
            proof.graphicsDevice = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName;
            proof.startedAtUtc = DateTime.UtcNow.ToString("o");
            var run = RenderAll();
            while (true)
            {
                bool next;
                object current = null;
                try
                {
                    next = run.MoveNext();
                    if (next)
                        current = run.Current;
                }
                catch (Exception exception)
                {
                    proof.result = "FAIL";
                    proof.error = exception.ToString();
                    Save();
                    Debug.LogException(exception);
                    Application.Quit(1);
                    yield break;
                }
                if (!next)
                    break;
                yield return current;
            }
            proof.result = proof.mismatches == 0 ? "PASS" : "FAIL";
            Save();
            Debug.Log("[Native Hero GPU] " + proof.result + " " + proof.sourceFrames + " individual frames / " + proof.comparedPixels +
                " full RGBA source pixels / " + proof.gpuPages + " actual SpriteRenderer pages; mismatches=" + proof.mismatches);
            yield return null;
            Application.Quit(proof.mismatches == 0 ? 0 : 1);
        }

        private IEnumerator RenderAll()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new Exception("A real graphics device is required.");
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            capture = gameObject.AddComponent<Camera>();
            capture.enabled = false;
            capture.orthographic = true;
            capture.orthographicSize = Height / 64f;
            capture.aspect = (float)Width / Height;
            capture.transform.position = new Vector3(0, 0, -10);
            capture.cullingMask = 1 << 30;
            capture.clearFlags = CameraClearFlags.SolidColor;
            capture.backgroundColor = Color.clear;
            capture.allowHDR = false;
            capture.allowMSAA = false;
            target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { filterMode = FilterMode.Point, antiAliasing = 1, useMipMap = false };
            target.Create();
            capture.targetTexture = target;
            readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
            cells = new SpriteRenderer[Columns * Rows];
            for (int i = 0; i < cells.Length; i++)
            {
                var body = new GameObject("Native source " + i);
                body.layer = 30;
                body.transform.SetParent(transform, false);
                body.transform.localPosition = new Vector3(-Width / 64f + (i % Columns) * 2f + (8 + 24) / 32f,
                    -Height / 64f + (i / Columns) * 2.5f + (8 + 22) / 32f, 10);
                cells[i] = body.AddComponent<SpriteRenderer>();
            }
            for (int version = 0; version < NativeHeroFrameCatalog.CurrentVersions.Length; version++)
            {
                var catalog = NativeHeroFrameCatalog.Load(version);
                if (catalog == null)
                    throw new Exception("Missing native catalog " + version);
                for (int start = 0; start < catalog.frames.Length; start += cells.Length)
                {
                    int count = Mathf.Min(cells.Length, catalog.frames.Length - start);
                    for (int i = 0; i < cells.Length; i++)
                        cells[i].sprite = i < count ? catalog.frames[start + i] : null;
                    yield return null;
                    capture.Render();
                    var previous = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = target;
                        readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                    }
                    Color32[] pixels = readback.GetPixels32();
                    File.WriteAllBytes(Path.Combine(directory, "page-" + proof.gpuPages.ToString("D3") + "-" + NativeHeroFrameCatalog.CurrentVersions[version] + ".png"), readback.EncodeToPNG());
                    for (int i = 0; i < count; i++)
                    {
                        var sprite = cells[i].sprite;
                        if (!sprite.packed || sprite.packingRotation != SpritePackingRotation.None)
                            throw new Exception("Unpacked/rotated source " + start + "/" + i);
                        string rendered = NativeHeroFrameCatalog.VisiblePixelHash(pixels, Width, new RectInt((i % Columns) * 64 + 8, (i / Columns) * 80 + 8, 48, 64));
                        if (rendered != catalog.visiblePixelSha256[start + i])
                        {
                            proof.mismatches++;
                            Debug.LogError("[Native Hero GPU] Pixel mismatch " + catalog.logicalRoot + "/" + (start + i).ToString("D5"));
                        }
                        proof.sourceFrames++;
                        proof.comparedPixels += 48 * 64;
                    }
                    proof.gpuPages++;
                    Save();
                }
            }
        }

        private void Save()
        {
            proof.endedAtUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText(Path.Combine(directory, "gpu-source-result.json"), JsonUtility.ToJson(proof, true), new System.Text.UTF8Encoding(false));
        }
        private void OnDestroy()
        {
            if (capture != null)
                capture.targetTexture = null;
            if (target != null)
            {
                target.Release();
                Destroy(target);
            }
            if (readback != null)
                Destroy(readback);
        }
    }
}
