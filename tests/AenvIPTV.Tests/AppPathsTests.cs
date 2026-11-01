using System;
using System.IO;
using Aenv.Services;
using Xunit;

namespace Aenv.Tests;

public class AppPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aenv-test-" + Guid.NewGuid().ToString("N"));

    public AppPathsTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Migrates_library_from_previous_app_name()
    {
        var old = Directory.CreateDirectory(Path.Combine(_root, "VaenIPTV")).FullName;
        File.WriteAllText(Path.Combine(old, "library.json"), "{\"old\":true}");

        var dir = AppPaths.Prepare(_root);

        Assert.Equal(Path.Combine(_root, "AenvIPTV"), dir);
        Assert.Equal("{\"old\":true}", File.ReadAllText(Path.Combine(dir, "library.json")));
    }

    [Fact]
    public void Does_not_overwrite_files_written_by_the_installer()
    {
        var old = Directory.CreateDirectory(Path.Combine(_root, "VaenIPTV")).FullName;
        File.WriteAllText(Path.Combine(old, "settings.json"), "{\"lang\":\"az\"}");
        File.WriteAllText(Path.Combine(old, "library.json"), "LIB");
        var dir = Directory.CreateDirectory(Path.Combine(_root, "AenvIPTV")).FullName;
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"lang\":\"de\"}");   // installer ran first

        AppPaths.Prepare(_root);

        Assert.Equal("{\"lang\":\"de\"}", File.ReadAllText(Path.Combine(dir, "settings.json")));
        Assert.Equal("LIB", File.ReadAllText(Path.Combine(dir, "library.json")));   // still migrated
    }

    [Fact]
    public void Migrates_only_once()
    {
        var old = Directory.CreateDirectory(Path.Combine(_root, "Ekran")).FullName;
        File.WriteAllText(Path.Combine(old, "library.json"), "LIB");
        var dir = AppPaths.Prepare(_root);
        File.Delete(Path.Combine(dir, "library.json"));    // user cleared the library

        AppPaths.Prepare(_root);

        Assert.False(File.Exists(Path.Combine(dir, "library.json")));
    }

    [Fact]
    public void Prefers_newest_legacy_folder()
    {
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(_root, "Ekran")).FullName, "library.json"), "OLDEST");
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(_root, "VaenIPTV")).FullName, "library.json"), "NEWER");
        Assert.Equal("NEWER", File.ReadAllText(Path.Combine(AppPaths.Prepare(_root), "library.json")));
    }

    [Fact]
    public void Fresh_install_creates_empty_folder()
    {
        var dir = AppPaths.Prepare(_root);
        Assert.True(Directory.Exists(dir));
        Assert.False(File.Exists(Path.Combine(dir, "library.json")));
    }
}
