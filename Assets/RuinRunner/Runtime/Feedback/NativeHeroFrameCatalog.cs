using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Sprite references into lossless built-in atlases, authored from individual PNGs.</summary>
    public sealed class NativeHeroFrameCatalog : ScriptableObject
    {
        public const string CatalogRoot = "Art/NativeHero/";
        public const string SourceRoot = "Assets/RuinRunner/ArtSources/Hero/";
        public const string ArchiveRoot = "Assets/RuinRunner/ArtSources/Archive/SscaryMaleCombat/";
        public const string LogicalRoot = "External/SscaryMaleCombat/";
        public static readonly string[] CurrentVersions = { "ArmedGripV3", "WeaponStandingV4", "WeaponReloadV3", "WeaponHurtV1" };
        public static readonly string[] ArchivedVersions = { "Melee", "ShortBlade", "Thrust", "Defense", "RangedV7", "WeaponStandingV1", "WeaponStandingV2" };

        [HideInInspector] public string logicalRoot;
        [HideInInspector] public Sprite[] frames;
        [HideInInspector] public string[] pngSha256;
        [HideInInspector] public string[] visiblePixelSha256;

        private static readonly NativeHeroFrameCatalog[] loaded = new NativeHeroFrameCatalog[CurrentVersions.Length];
        // Preparation runs before creating the first player. Keep disk reads and
        // lazy atlas resolution out of a single Town frame; later pose lookups
        // still use the same authored sprites and synchronous cache fallback.
        public static async Task PrepareAsync(CancellationToken cancellation)
        {
            for (int version = 0; version < CurrentVersions.Length; version++)
            {
                cancellation.ThrowIfCancellationRequested();
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                if (loaded[version] == null)
                {
                    var request = Resources.LoadAsync<NativeHeroFrameCatalog>(CatalogRoot + CurrentVersions[version]);
                    while (!request.isDone)
                        await Awaitable.NextFrameAsync(cancellation);
                    loaded[version] = request.asset as NativeHeroFrameCatalog;
                }
                var catalog = loaded[version];
                if (catalog == null || catalog.frames == null)
                    throw new InvalidOperationException("Missing native hero catalog: " + CurrentVersions[version]);
                double requestSeconds = elapsed.Elapsed.TotalSeconds;
                var textures = new System.Collections.Generic.HashSet<Texture2D>();
                var batch = System.Diagnostics.Stopwatch.StartNew();
                double longestBatch = 0;
                for (int frame = 0; frame < catalog.frames.Length; frame++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var sprite = catalog.frames[frame];
                    if (sprite == null)
                        throw new InvalidOperationException("Missing native hero frame: " + catalog.logicalRoot + "/" + frame);
                    var texture = sprite.texture;
                    if (texture != null && textures.Add(texture))
                        _ = texture.width;
                    IndividualArt.CachePreparedSprite(catalog.logicalRoot + "/" + frame.ToString("D5"), sprite);
                    if (batch.Elapsed.TotalMilliseconds < 4)
                        continue;
                    longestBatch = Math.Max(longestBatch, batch.Elapsed.TotalMilliseconds);
                    await Awaitable.NextFrameAsync(cancellation);
                    batch.Restart();
                }
                longestBatch = Math.Max(longestBatch, batch.Elapsed.TotalMilliseconds);
                if (StageValidationInput.Enabled)
                    Debug.Log("[Town Preparation] " + CurrentVersions[version] + " request=" + requestSeconds.ToString("F3") +
                        "s total=" + elapsed.Elapsed.TotalSeconds.ToString("F3") + "s longestResolveBatch=" + longestBatch.ToString("F3") + "ms textures=" + textures.Count);
                await Awaitable.NextFrameAsync(cancellation);
            }
        }
        public static int ExpectedFrames(int version)
        {
            switch (CurrentVersions[version])
            {
                case "ArmedGripV3":
                    return SscaryArmedFrames.UniqueSpriteCount;
                case "WeaponStandingV4":
                    return WeaponStandingFrames.UniqueSpriteCount;
                case "WeaponReloadV3":
                    return WeaponReloadFrames.UniqueSpriteCount;
                case "WeaponHurtV1":
                    return SscaryHurtFrames.UniqueSpriteCount;
                default:
                    throw new InvalidOperationException("Unknown native hero catalog: " + CurrentVersions[version]);
            }
        }
        public static NativeHeroFrameCatalog Load(int version)
        {
            if (loaded[version] == null)
            {
                loaded[version] = Resources.Load<NativeHeroFrameCatalog>(CatalogRoot + CurrentVersions[version]);
                // Sprite references alone leave their atlas textures lazy. Resolve
                // each atlas during character preparation, before a short combat
                // pose needs it for the first time.
                var catalog = loaded[version];
                if (catalog != null)
                {
                    var textures = new System.Collections.Generic.HashSet<Texture2D>();
                    foreach (var sprite in catalog.frames)
                    {
                        if (sprite == null)
                            continue;
                        var texture = sprite.texture;
                        if (texture != null && textures.Add(texture))
                            _ = texture.width;
                    }
                }
            }
            return loaded[version];
        }

        public static bool TryGetSprite(string id, out Sprite sprite)
        {
            sprite = null;
            for (int version = 0; version < CurrentVersions.Length; version++)
            {
                string prefix = LogicalRoot + CurrentVersions[version] + "/";
                if (!id.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                var catalog = Load(version);
#if UNITY_EDITOR
                // Authoring tools may run before a new catalog is generated.
                if (catalog == null) return false;
#endif
                if (catalog == null || !int.TryParse(id.Substring(prefix.Length), out int index) ||
                    index < 0 || index >= catalog.frames.Length || catalog.frames[index] == null)
                    throw new InvalidOperationException("Missing native hero frame: " + id);
                sprite = catalog.frames[index];
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        // Retired poses remain available to original authoring/inspection tools,
        // without being packed or shipped in the Player.
        public static string EditorSourcePath(string id)
        {
            foreach (string version in CurrentVersions)
            {
                string prefix = LogicalRoot + version + "/";
                if (id.StartsWith(prefix, StringComparison.Ordinal)) return SourceRoot + version + "/" + id.Substring(prefix.Length) + ".png";
            }
            foreach (string version in ArchivedVersions)
            {
                string prefix = LogicalRoot + version + "/";
                if (id.StartsWith(prefix, StringComparison.Ordinal)) return ArchiveRoot + version + "/" + id.Substring(prefix.Length) + ".png";
            }
            return null;
        }
#endif

        // Transparent RGB has no displayed meaning and may be alpha-dilated by
        // an importer. All alpha and every nontransparent RGB byte must match.
        public static string VisiblePixelHash(Color32[] pixels, int stride, RectInt rect)
        {
            var bytes = new byte[rect.width * rect.height * 4];
            int offset = 0;
            for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                {
                    Color32 color = pixels[(rect.y + y) * stride + rect.x + x];
                    bytes[offset++] = color.a == 0 ? (byte)0 : color.r;
                    bytes[offset++] = color.a == 0 ? (byte)0 : color.g;
                    bytes[offset++] = color.a == 0 ? (byte)0 : color.b;
                    bytes[offset++] = color.a;
                }
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
