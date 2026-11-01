using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Aenv.Services;

/// <summary>
/// Small file cache for channel lists and programme guides, so the app starts instantly and does not
/// hit the provider on every launch. Entries are keyed by any string (URL, playlist id) and stored in
/// %AppData%\AenvIPTV\cache. Freshness is decided by the file's write time.
/// </summary>
public static class DiskCache
{
    private static string? _root;

    /// <summary>Cache folder; tests point it to a temporary directory.</summary>
    public static string Root
    {
        get => _root ??= Path.Combine(AppPaths.LocalDir, "cache");
        set => _root = value;
    }

    private static string PathFor(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32].ToLowerInvariant();
        return Path.Combine(Root, hash + ".bin");
    }

    /// <summary>Returns the cached bytes if present and younger than <paramref name="maxAge"/>.</summary>
    public static byte[]? Read(string key, TimeSpan maxAge)
    {
        var f = new FileInfo(PathFor(key));
        if (!f.Exists || DateTime.UtcNow - f.LastWriteTimeUtc > maxAge) return null;
        try { return File.ReadAllBytes(f.FullName); } catch { return null; }
    }

    /// <summary>Returns cached bytes regardless of age (fallback when the server is unavailable).</summary>
    public static byte[]? ReadAny(string key) => Read(key, TimeSpan.MaxValue);

    public static DateTime? WrittenAtUtc(string key)
    {
        var f = new FileInfo(PathFor(key));
        return f.Exists ? f.LastWriteTimeUtc : null;
    }

    public static void Write(string key, byte[] data)
    {
        try
        {
            Directory.CreateDirectory(Root);
            var path = PathFor(key);
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            File.Move(tmp, path, overwrite: true);
        }
        catch { /* caching is best-effort */ }
    }

    public static void Remove(string key)
    {
        try { File.Delete(PathFor(key)); } catch { }
    }
}
