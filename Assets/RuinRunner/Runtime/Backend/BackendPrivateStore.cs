using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace RuinRunner.Backend
{
    // Windows CurrentUser DPAPI. No server password, token or plaintext guest secret is written.
    internal sealed class BackendPrivateStore
    {
        readonly string root;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public BackendPrivateStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A private storage directory is required.");
            root = Path.GetFullPath(directory);
        }
        string FilePath(string category, string key) => Path.Combine(root, category, Hash(Encoding.UTF8.GetBytes(key)) + ".dpapi");
        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public T Read<T>(string category, string key) where T : class
        {
            var path = FilePath(category, key);
            if (!File.Exists(path))
                return null;
            return JsonConvert.DeserializeObject<T>(Utf8.GetString(Protect(File.ReadAllBytes(path), false)));
        }
        public void Write<T>(string category, string key, T value)
        {
            AtomicWrite(FilePath(category, key), Protect(Utf8.GetBytes(JsonConvert.SerializeObject(value)), true));
        }
        public GuestIdentity GetIdentity(string serverScope = "guest")
        {
            var existing = Read<GuestIdentity>("identity", serverScope);
            if (existing != null)
                return Validate(existing);
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            var identity = new GuestIdentity { installationId = Guid.NewGuid().ToString(), guestSecret = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') };
            var path = FilePath("identity", serverScope);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var encrypted = Protect(Utf8.GetBytes(JsonConvert.SerializeObject(identity)), true);
            // CreateNew makes concurrent first launches reuse the winning account, never overwrite it.
            try
            {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(encrypted, 0, encrypted.Length);
                    stream.Flush(true);
                }
            }
            catch (IOException) when (File.Exists(path))
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    try
                    {
                        return Validate(Read<GuestIdentity>("identity", serverScope));
                    }
                    catch (IOException) when (attempt < 29)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }
                throw;
            }
            return identity;
        }
        static GuestIdentity Validate(GuestIdentity identity)
        {
            if (identity == null || !Guid.TryParse(identity.installationId, out _) || identity.guestSecret == null || identity.guestSecret.Length != 43)
                throw new InvalidDataException("Guest credential recovery is required; refusing to create a replacement account.");
            return identity;
        }
        public PendingOperation[] Pending(string owner, string origin)
        {
            var directory = Path.Combine(root, "operations");
            if (!Directory.Exists(directory))
                return Array.Empty<PendingOperation>();
            return Directory.GetFiles(directory, "*.dpapi").Select(path => JsonConvert.DeserializeObject<PendingOperation>(Utf8.GetString(Protect(File.ReadAllBytes(path), false))))
                .Where(x => x != null && x.playerId == owner && x.apiOrigin == origin && x.status == "pending").OrderBy(x => x.createdUtc, StringComparer.Ordinal).ToArray();
        }
        public QueuedEconomyOperation[] EconomyQueue(string origin)
        {
            var directory = Path.Combine(root, "economy-queue");
            if (!Directory.Exists(directory))
                return Array.Empty<QueuedEconomyOperation>();
            return Directory.GetFiles(directory, "*.dpapi").Select(path => JsonConvert.DeserializeObject<QueuedEconomyOperation>(Utf8.GetString(Protect(File.ReadAllBytes(path), false))))
                .Where(x => x != null && x.apiOrigin == origin && (x.status == "pending" || x.status == "confirmed-unprojected")).OrderBy(x => x.createdUtc, StringComparer.Ordinal).ThenBy(x => x.operationId, StringComparer.Ordinal).ToArray();
        }
        public void SaveProfile(ProfileView profile, string origin)
        {
            if (profile == null)
                return;
            string key = origin + ":" + profile.profileId;
            var previous = Read<ProfileView>("profiles", key);
            if (previous != null && previous.revision > profile.revision)
                return;
            Write("profiles", key, profile);
        }
        public LegacyBackup BackupLegacy(string sourcePath)
        {
            var full = Path.GetFullPath(sourcePath);
            var raw = File.ReadAllBytes(full);
            if (raw.Length > 32768)
                throw new InvalidDataException("Legacy save exceeds the supported import size.");
            var source = Utf8.GetString(raw);
            if (source.Length > 0 && source[0] == '\ufeff')
                source = source.Substring(1);
            // Validate syntax before creating an import command, without rewriting original bytes.
            Newtonsoft.Json.Linq.JObject.Parse(source);
            var id = Guid.NewGuid().ToString("N");
            var directory = Path.Combine(root, "legacy", id);
            Directory.CreateDirectory(directory);
            var result = new LegacyBackup
            {
                backupId = id,
                originalSha256 = Hash(raw),
                importSha256 = Hash(Utf8.GetBytes(source)),
                sourceJson = source,
                encryptedOriginalPath = Path.Combine(directory, "original.dpapi"),
                createdUtc = DateTime.UtcNow.ToString("O")
            };
            AtomicWrite(result.encryptedOriginalPath, Protect(raw, true));
            if (File.Exists(full + ".bak"))
            {
                result.encryptedRecoveryPath = Path.Combine(directory, "recovery.dpapi");
                AtomicWrite(result.encryptedRecoveryPath, Protect(File.ReadAllBytes(full + ".bak"), true));
            }
            Write("legacy-receipts", id, result);
            return result;
        }
        static void AtomicWrite(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                    File.Replace(temporary, path, path + ".bak");
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Blob
        {
            public int size;
            public IntPtr data;
        }
        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
        static byte[] Protect(byte[] bytes, bool encrypt)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("The current credential store requires Windows CurrentUser DPAPI.");
            var input = new Blob { size = bytes.Length, data = Marshal.AllocHGlobal(bytes.Length) };
            Blob output = default;
            try
            {
                Marshal.Copy(bytes, 0, input.data, bytes.Length);
                var success = encrypt ? CryptProtectData(ref input, "RuinRunner guest and recovery state", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!success)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var result = new byte[output.size];
                Marshal.Copy(output.data, result, 0, result.Length);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.data);
                if (output.data != IntPtr.Zero)
                    LocalFree(output.data);
            }
        }
    }
}
