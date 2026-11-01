using System;
using System.IO;

namespace Aenv.Services;

/// <summary>
/// Central place for all on-disk locations. Data written by earlier app names
/// ("VaenIPTV", "Ekran") is migrated once, so users keep their playlists after updating.
/// </summary>
public static class AppPaths
{
    public const string FolderName = "AenvIPTV";
    private static readonly string[] LegacyFolderNames = { "VaenIPTV", "Ekran" };

    /// <summary>%AppData%\AenvIPTV – settings and logs (local to this PC).</summary>
    public static string LocalDir { get; } = Prepare(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    /// <summary>OneDrive\AenvIPTV if OneDrive is available (synced between PCs), otherwise LocalDir.</summary>
    public static string DataDir { get; } = Prepare(OneDriveRoot()
        ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string LibraryFile => Path.Combine(DataDir, "library.json");
    public static string SettingsFile => Path.Combine(LocalDir, "settings.json");
    public static string CrashLog => Path.Combine(LocalDir, "crash.log");

    private static string? OneDriveRoot()
    {
        var root = Environment.GetEnvironmentVariable("OneDrive");
        return !string.IsNullOrEmpty(root) && Directory.Exists(root) ? root : null;
    }

    /// <summary>
    /// Creates root\AenvIPTV and, once per folder, copies files from the newest legacy folder
    /// that are missing here. Per-file copying matters because the installer may already have
    /// written settings.json before the first start.
    /// </summary>
    internal static string Prepare(string root)
    {
        var dir = Path.Combine(root, FolderName);
        try
        {
            Directory.CreateDirectory(dir);
            var marker = Path.Combine(dir, ".migrated");
            if (File.Exists(marker)) return dir;

            foreach (var legacy in LegacyFolderNames)
            {
                var old = Path.Combine(root, legacy);
                if (!Directory.Exists(old)) continue;
                foreach (var file in Directory.GetFiles(old))
                {
                    var target = Path.Combine(dir, Path.GetFileName(file));
                    if (!File.Exists(target)) File.Copy(file, target);
                }
                break;
            }
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
        }
        catch { /* migration is best-effort; the app still starts with an empty library */ }
        return dir;
    }
}
