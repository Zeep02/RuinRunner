using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Direct file lookup only. Slicing, chroma keying and pivot discovery belong to the Editor.</summary>
    public static class IndividualArt
    {
        public const string Root = "Art/Pixel/Separated/";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite[]> Sequences = new Dictionary<string, Sprite[]>();
        internal static void CachePreparedSprite(string id, Sprite sprite) => Sprites[id] = sprite;

        internal static async Task PrepareFramesAsync(string id, int count, CancellationToken cancellation)
        {
            if (Sequences.ContainsKey(id + ":" + count))
                return;
            var requests = new ResourceRequest[count];
            for (int i = 0; i < count; i++)
            {
                string path = id + "/" + i.ToString("D2");
                if (!Sprites.ContainsKey(path))
                    requests[i] = Resources.LoadAsync<Sprite>(Root + path);
            }
            for (int i = 0; i < count; i++)
            {
                var request = requests[i];
                if (request == null)
                    continue;
                while (!request.isDone)
                    await Awaitable.NextFrameAsync(cancellation);
                cancellation.ThrowIfCancellationRequested();
                var sprite = request.asset as Sprite;
                if (sprite == null)
                    throw new InvalidOperationException("Missing single-sprite import: " + Root + id + "/" + i.ToString("D2"));
                CachePreparedSprite(id + "/" + i.ToString("D2"), sprite);
            }
            Frames(id, count);
        }
        public static bool Exists(string id)
        {
            if (NativeHeroFrameCatalog.TryGetSprite(id, out var native))
                return native != null;
#if UNITY_EDITOR
            var path = NativeHeroFrameCatalog.EditorSourcePath(id);
            if (path != null) return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path) != null;
#endif
            return Resources.Load<Texture2D>(Root + id) != null;
        }
        public static Texture2D Texture(string id)
        {
            if (!Textures.TryGetValue(id, out var texture))
            {
                texture = Resources.Load<Texture2D>(Root + id);
                if (texture == null)
                    throw new InvalidOperationException("Missing individual art: " + Root + id);
                Textures.Add(id, texture);
            }
            return texture;
        }
        public static Sprite Sprite(string id)
        {
            if (!Sprites.TryGetValue(id, out var sprite))
            {
                if (!NativeHeroFrameCatalog.TryGetSprite(id, out sprite))
                {
#if UNITY_EDITOR
                    var path = NativeHeroFrameCatalog.EditorSourcePath(id);
                    if (path != null) sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    else
#endif
                    sprite = Resources.Load<Sprite>(Root + id);
                }
                if (sprite == null)
                    throw new InvalidOperationException("Missing single-sprite import: " + Root + id);
                Sprites.Add(id, sprite);
            }
            return sprite;
        }
        public static Sprite[] Frames(string id, int count)
        {
            var key = id + ":" + count;
            if (Sequences.TryGetValue(key, out var frames))
                return frames;
            frames = new Sprite[count];
            for (var i = 0; i < count; i++)
                frames[i] = Sprite(id + "/" + i.ToString("D2"));
            Sequences.Add(key, frames);
            return frames;
        }
    }
}
