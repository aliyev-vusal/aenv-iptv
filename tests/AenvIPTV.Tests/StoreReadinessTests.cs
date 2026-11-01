using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Aenv.Tests;

/// <summary>
/// Checks the requirements of the Microsoft Store (MSIX) and of a standard Windows installer
/// that would otherwise only be found during certification or on a user's PC.
/// </summary>
public class StoreReadinessTests
{
    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static readonly string Root = FindRoot();
    private static readonly string Win = Path.Combine(Root, "windows");
    private static readonly string Assets = Path.Combine(Win, "packaging", "Assets");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "windows", "AenvIPTV.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static XDocument Manifest()
    {
        var xml = File.ReadAllText(Path.Combine(Win, "packaging", "AppxManifest.xml"))
            .Replace("$IDENTITY_NAME$", "12345Test.AENVIPTV")
            .Replace("$PUBLISHER$", "CN=00000000-0000-0000-0000-000000000000")
            .Replace("$PUBLISHER_DISPLAY_NAME$", "Test")
            .Replace("$VERSION$", "1.0.0.0");
        return XDocument.Parse(xml);
    }

    private static (int w, int h) PngSize(string path)
    {
        var b = File.ReadAllBytes(path);
        Assert.True(b.Length > 24 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G', $"{Path.GetFileName(path)} is not a PNG");
        int Be(int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        return (Be(16), Be(20));
    }

    // ---------------- MSIX manifest ----------------

    [Fact]
    public void Manifest_has_only_known_tokens()
    {
        var raw = File.ReadAllText(Path.Combine(Win, "packaging", "AppxManifest.xml"));
        var tokens = Regex.Matches(raw, @"\$[A-Z_]+\$").Select(m => m.Value).Distinct().OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(new[] { "$IDENTITY_NAME$", "$PUBLISHER$", "$PUBLISHER_DISPLAY_NAME$", "$VERSION$" }, tokens);
        var script = File.ReadAllText(Path.Combine(Win, "build-msix.ps1"));
        Assert.All(tokens, t => Assert.Contains(t, script));
    }

    [Fact]
    public void Manifest_launches_the_real_executable_as_full_trust_desktop_app()
    {
        var doc = Manifest();
        var app = doc.Descendants().Single(e => e.Name.LocalName == "Application");
        var csproj = File.ReadAllText(Path.Combine(Win, "AenvIPTV.csproj"));
        var assembly = Regex.Match(csproj, "<AssemblyName>(.+?)</AssemblyName>").Groups[1].Value;

        Assert.Equal(assembly + ".exe", app.Attribute("Executable")!.Value);
        Assert.Equal("Windows.FullTrustApplication", app.Attribute("EntryPoint")!.Value);
        var caps = doc.Descendants().Where(e => e.Name.LocalName == "Capability").Select(e => e.Attribute("Name")!.Value).ToList();
        Assert.Contains("runFullTrust", caps);
        Assert.Contains("internetClient", caps);
    }

    [Fact]
    public void Manifest_targets_desktop_with_supported_minimum_version()
    {
        var tdf = Manifest().Descendants().Single(e => e.Name.LocalName == "TargetDeviceFamily");
        Assert.Equal("Windows.Desktop", tdf.Attribute("Name")!.Value);
        Assert.True(Version.Parse(tdf.Attribute("MinVersion")!.Value) >= new Version(10, 0, 17763, 0));
        Assert.Equal("x64", Manifest().Descendants().Single(e => e.Name.LocalName == "Identity").Attribute("ProcessorArchitecture")!.Value);
    }

    [Fact]
    public void Manifest_declares_all_app_languages()
    {
        var langs = Manifest().Descendants().Where(e => e.Name.LocalName == "Resource")
            .Select(e => e.Attribute("Language")!.Value.Split('-')[0].ToLowerInvariant()).ToHashSet();
        foreach (var (code, _) in Loc.Languages) Assert.Contains(code, langs);
    }

    [Fact]
    public void Display_names_and_descriptions_are_set()
    {
        var doc = Manifest();
        string Val(string local) => doc.Descendants().First(e => e.Name.LocalName == local).Value;
        Assert.Equal("AENV IPTV", Val("DisplayName"));
        Assert.False(string.IsNullOrWhiteSpace(Val("Description")));
        var ve = doc.Descendants().Single(e => e.Name.LocalName == "VisualElements");
        Assert.Equal("AENV IPTV", ve.Attribute("DisplayName")!.Value);
        Assert.False(string.IsNullOrWhiteSpace(ve.Attribute("Description")!.Value));
    }

    // ---------------- Visual assets ----------------

    public static IEnumerable<object[]> ManifestImages()
    {
        var doc = Manifest();
        return doc.Descendants().SelectMany(e => e.Attributes()).Select(a => a.Value)
            .Concat(doc.Descendants().Where(e => e.Name.LocalName == "Logo").Select(e => e.Value))
            .Where(v => v.StartsWith("Assets\\") && v.EndsWith(".png"))
            .Distinct().Select(v => new object[] { v });
    }

    [Theory, MemberData(nameof(ManifestImages))]
    public void Every_manifest_image_exists_in_all_scales(string image)
    {
        var name = Path.GetFileNameWithoutExtension(image.Replace('\\', Path.DirectorySeparatorChar));
        foreach (var scale in new[] { 100, 125, 150, 200, 400 })
            Assert.True(File.Exists(Path.Combine(Assets, $"{name}.scale-{scale}.png")), $"{name}.scale-{scale}.png missing");
    }

    [Theory]
    [InlineData("Square44x44Logo", 44, 44)]
    [InlineData("Square150x150Logo", 150, 150)]
    [InlineData("Wide310x150Logo", 310, 150)]
    [InlineData("StoreLogo", 50, 50)]
    [InlineData("SmallTile", 71, 71)]
    [InlineData("LargeTile", 310, 310)]
    [InlineData("SplashScreen", 620, 300)]
    public void Asset_dimensions_match_their_scale(string name, int w, int h)
    {
        foreach (var scale in new[] { 100, 125, 150, 200, 400 })
        {
            var (aw, ah) = PngSize(Path.Combine(Assets, $"{name}.scale-{scale}.png"));
            Assert.Equal((int)Math.Round(w * scale / 100.0), aw);
            Assert.Equal((int)Math.Round(h * scale / 100.0), ah);
        }
    }

    [Theory]
    [InlineData(16)] [InlineData(24)] [InlineData(32)] [InlineData(48)] [InlineData(256)]
    public void Taskbar_icons_exist_including_unplated(int size)
    {
        foreach (var suffix in new[] { "", "_altform-unplated", "_altform-lightunplated" })
        {
            var f = Path.Combine(Assets, $"Square44x44Logo.targetsize-{size}{suffix}.png");
            Assert.True(File.Exists(f), Path.GetFileName(f));
            Assert.Equal((size, size), PngSize(f));
        }
    }

    // ---------------- Standard installer ----------------

    private static string Iss() => File.ReadAllText(Path.Combine(Win, "installer", "AenvIPTV.iss"));

    [Theory]
    [InlineData("DisableWelcomePage=no")]
    [InlineData("DisableDirPage=no")]
    [InlineData("DisableProgramGroupPage=no")]
    [InlineData("DisableReadyPage=no")]
    [InlineData("DisableFinishedPage=no")]
    [InlineData("WizardStyle=modern")]
    [InlineData("PrivilegesRequiredOverridesAllowed=dialog")]
    [InlineData("CloseApplications=yes")]
    [InlineData("AppId={{85138C5C-CADA-431A-937F-39A2920022E4}")]   // never change: keeps upgrades working
    public void Installer_uses_standard_wizard_settings(string line)
    {
        Assert.Contains(line, Iss());
    }

    [Fact]
    public void Installer_files_exist()
    {
        var iss = Iss();
        var dir = Path.Combine(Win, "installer");
        string Rel(string p) => Path.GetFullPath(Path.Combine(dir, p.Trim().Replace('\\', Path.DirectorySeparatorChar)));

        Assert.True(File.Exists(Rel(Regex.Match(iss, @"LicenseFile=(.+)").Groups[1].Value)), "license");
        foreach (var key in new[] { "WizardImageFile", "WizardSmallImageFile" })
            foreach (var f in Regex.Match(iss, key + @"=(.+)").Groups[1].Value.Split(','))
                Assert.True(File.Exists(Rel(f)), f);
    }

    [Fact]
    public void Installer_has_start_menu_uninstall_and_launch_entries()
    {
        var iss = Iss();
        Assert.Contains("{cm:UninstallProgram,", iss);
        Assert.Contains("{cm:LaunchProgram,", iss);
        Assert.Contains("postinstall skipifsilent", iss);   // never auto-launches during silent installs
        Assert.Contains("UninstallDisplayIcon=", iss);
    }

    [Fact]
    public void Versions_are_consistent()
    {
        var csproj = File.ReadAllText(Path.Combine(Win, "AenvIPTV.csproj"));
        var v = Regex.Match(csproj, "<Version>(.+?)</Version>").Groups[1].Value;
        Assert.Contains($"#define MyAppVersion \"{v}\"", Iss());
        Assert.Contains($"[string]$Version = \"{v}\"", File.ReadAllText(Path.Combine(Win, "build-msix.ps1")));
        Assert.Contains($"[string]$Version = \"{v}\"", File.ReadAllText(Path.Combine(Win, "build-installer.ps1")));
    }

    // ---------------- Legal & accessibility ----------------

    [Theory]
    [InlineData("LICENSE.txt")]
    [InlineData("PRIVACY.txt")]
    [InlineData("THIRD-PARTY-NOTICES.txt")]
    public void Legal_documents_are_shipped(string file)
    {
        Assert.True(new FileInfo(Path.Combine(Win, "Legal", file)).Length > 200, file);
        Assert.Contains(@"Content Include=""Legal\*.txt""", File.ReadAllText(Path.Combine(Win, "AenvIPTV.csproj")));
        var about = File.ReadAllText(Path.Combine(Win, "Views", "AboutWindow.xaml.cs"));
        Assert.Contains($"\"{file}\"", about);
    }

    [Fact]
    public void Third_party_notices_cover_all_packages()
    {
        var notices = File.ReadAllText(Path.Combine(Win, "Legal", "THIRD-PARTY-NOTICES.txt"));
        var packages = Regex.Matches(File.ReadAllText(Path.Combine(Win, "AenvIPTV.csproj")), @"PackageReference Include=""([^""]+)""")
            .Select(m => m.Groups[1].Value);
        Assert.All(packages, p => Assert.Contains(p.Split('.')[0] == "VideoLAN" ? "LibVLC" : p.Replace(".WPF", ""), notices));
        Assert.Contains("LGPL", notices);
    }

    [Fact]
    public void Privacy_policy_is_published_and_matches_the_app_copy()
    {
        var app = File.ReadAllText(Path.Combine(Win, "Legal", "PRIVACY.txt"));
        var web = File.ReadAllText(Path.Combine(Root, "docs", "privacy-policy.md"));
        var date = Regex.Match(app, @"Last updated: (.+)").Groups[1].Value.Trim();
        Assert.Contains(date, web);
        Assert.Contains("no analytics", web);
    }

    [Fact]
    public void Icon_only_buttons_have_accessible_names()
    {
        var problems = new List<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(Win, "Views"), "*.xaml"))
        {
            var doc = XDocument.Load(f);
            foreach (var b in doc.Descendants().Where(e => e.Name.LocalName == "Button"))
            {
                var content = b.Attribute("Content")?.Value ?? "";
                var iconOnly = content.Length == 1 && content[0] >= '' && content[0] <= '';
                var hasName = b.Attributes().Any(a => a.Name.LocalName == "AutomationProperties.Name");
                if (iconOnly && !hasName) problems.Add($"{Path.GetFileName(f)}: {b.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value ?? content}");
            }
        }
        Assert.True(problems.Count == 0, "icon buttons without AutomationProperties.Name: " + string.Join(", ", problems));
    }
}
