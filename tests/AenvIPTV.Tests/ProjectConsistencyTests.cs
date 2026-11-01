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
/// Static checks over the WPF sources. They catch mistakes that otherwise only show up
/// as a crash when a window opens on Windows (missing handlers, resources, translations).
/// </summary>
public class ProjectConsistencyTests
{
    private static readonly string Win = Path.Combine(RepoRoot(), "windows");

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static string RepoRoot()
    {
        // Walk up from this source file: works from any output folder and in CI
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "windows", "AenvIPTV.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static IEnumerable<string> XamlFiles() =>
        Directory.GetFiles(Win, "*.xaml", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static IEnumerable<string> CsFiles() =>
        Directory.GetFiles(Win, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    public static IEnumerable<object[]> Xaml() => XamlFiles().Select(f => new object[] { Path.GetRelativePath(Win, f) });

    [Theory, MemberData(nameof(Xaml))]
    public void Xaml_is_well_formed(string file)
    {
        XDocument.Load(Path.Combine(Win, file));
    }

    private static readonly string[] Events =
    {
        "Click", "Checked", "SelectionChanged", "TextChanged", "ValueChanged", "KeyDown", "PreviewKeyDown",
        "MouseDoubleClick", "MouseMove", "MouseLeftButtonDown", "PreviewMouseDown", "PreviewMouseUp",
    };

    [Theory, MemberData(nameof(Xaml))]
    public void Every_event_handler_exists_in_code_behind(string file)
    {
        var path = Path.Combine(Win, file);
        var xaml = File.ReadAllText(path);
        var cs = File.Exists(path + ".cs") ? File.ReadAllText(path + ".cs") : "";
        var handlers = Regex.Matches(xaml, $@"\b(?:{string.Join('|', Events)})=""(\w+)""").Select(m => m.Groups[1].Value)
            .Concat(Regex.Matches(xaml, @"<EventSetter[^>]*Handler=""(\w+)""").Select(m => m.Groups[1].Value))
            .Distinct();
        var missing = handlers.Where(h => !Regex.IsMatch(cs, $@"\bvoid\s+{h}\s*\(")).ToList();
        Assert.True(missing.Count == 0, $"{file}: missing handlers {string.Join(", ", missing)}");
    }

    [Theory, MemberData(nameof(Xaml))]
    public void Event_handlers_are_not_inside_styles_or_templates(string file)
    {
        // Handlers inside Setter values are wired to the wrong element at runtime (InvalidCastException)
        var doc = XDocument.Load(Path.Combine(Win, file));
        var bad = doc.Descendants()
            .Where(e => e.Ancestors().Any(a => a.Name.LocalName is "Setter" or "DataTemplate" or "ControlTemplate"))
            .Where(e => e.Attributes().Any(a => Events.Contains(a.Name.LocalName)))
            .Select(e => e.Name.LocalName).ToList();
        Assert.True(bad.Count == 0, $"{file}: event handlers inside templates/styles on {string.Join(", ", bad)}");
    }

    [Theory, MemberData(nameof(Xaml))]
    public void Every_static_resource_is_defined(string file)
    {
        var xaml = File.ReadAllText(Path.Combine(Win, file));
        var app = File.ReadAllText(Path.Combine(Win, "App.xaml"));
        var defined = Regex.Matches(xaml + app, @"x:Key=""([^""]+)""").Select(m => m.Groups[1].Value).ToHashSet();
        var used = Regex.Matches(xaml, @"\{StaticResource ([^}\s]+)\}").Select(m => m.Groups[1].Value).Distinct();
        var missing = used.Where(u => !defined.Contains(u)).ToList();
        Assert.True(missing.Count == 0, $"{file}: undefined resources {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_translation_key_used_in_xaml_and_code_exists()
    {
        var used = XamlFiles().SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\{l:T (\w+)\}").Select(m => m.Groups[1].Value))
            .Concat(CsFiles().SelectMany(f => Regex.Matches(File.ReadAllText(f), @"Loc\.[TF]\(""(\w+)""").Select(m => m.Groups[1].Value)))
            .Distinct();
        var missing = used.Where(k => !Loc.AllStrings.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, $"missing translations: {string.Join(", ", missing)}");
    }

    private static readonly HashSet<string> FrameworkTypes = new() { "MessageBox", "MessageBoxButton", "MessageBoxImage", "ListBox", "TextBox", "ComboBox" };

    [Fact]
    public void Named_elements_used_in_code_behind_exist_in_xaml()
    {
        var problems = new List<string>();
        foreach (var xamlPath in XamlFiles().Where(f => File.Exists(f + ".cs")))
        {
            var xaml = File.ReadAllText(xamlPath);
            var names = Regex.Matches(xaml, @"x:Name=""(\w+)""").Select(m => m.Groups[1].Value).ToHashSet();
            var cs = File.ReadAllText(xamlPath + ".cs");
            // PascalCase identifiers followed by a member access that look like controls (e.g. SearchBox.Text)
            foreach (Match m in Regex.Matches(cs, @"(?<![\w.])([A-Z]\w*(?:Box|List|Text|Button|Panel|Bar|Host|Image|Slider|Progress|Row|Video|Overlay|State|Placeholder))\."))
                if (!names.Contains(m.Groups[1].Value) && !FrameworkTypes.Contains(m.Groups[1].Value)
                    && !Regex.IsMatch(cs, $@"\b{m.Groups[1].Value}\s*="))
                    problems.Add($"{Path.GetFileName(xamlPath)}: {m.Groups[1].Value}");
        }
        Assert.True(problems.Count == 0, string.Join("; ", problems.Distinct()));
    }

    [Fact]
    public void Dispatcher_BeginInvoke_is_not_given_a_lambda()
    {
        // WPF has no BeginInvoke(Action) overload; a lambda there is compile error CS1660. Use InvokeAsync.
        var hits = CsFiles().Where(f => Regex.IsMatch(File.ReadAllText(f), @"BeginInvoke\(\s*(\(|async\b|\w+\s*=>)"))
            .Select(Path.GetFileName).ToList();
        Assert.Empty(hits);
    }

    [Fact]
    public void No_references_to_removed_windows()
    {
        var hits = CsFiles().Concat(XamlFiles())
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\b(PlayerWindow|SeriesWindow)\b"))
            .Select(Path.GetFileName).ToList();
        Assert.Empty(hits);
    }

    [Fact]
    public void Project_and_installer_reference_existing_files()
    {
        var csproj = File.ReadAllText(Path.Combine(Win, "AenvIPTV.csproj"));
        foreach (Match m in Regex.Matches(csproj, @"(?:<ApplicationIcon>|<Resource Include="")([^<""]+)"))
        {
            var rel = m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
            Assert.True(File.Exists(Path.Combine(Win, rel)), $"csproj references missing file '{rel}'");
        }

        var iss = File.ReadAllText(Path.Combine(Win, "installer", "AenvIPTV.iss"));
        var icon = Regex.Match(iss, @"SetupIconFile=(.+)").Groups[1].Value.Trim().Replace('\\', Path.DirectorySeparatorChar);
        Assert.True(File.Exists(Path.Combine(Win, "installer", icon)), $"installer icon missing: '{icon}'");
        Assert.Contains("#define MyAppExeName \"AenvIPTV.exe\"", iss);
        Assert.Contains("<AssemblyName>AenvIPTV</AssemblyName>", csproj);
    }

    [Fact]
    public void Window_icons_point_to_existing_resource()
    {
        foreach (var f in XamlFiles())
        {
            var m = Regex.Match(File.ReadAllText(f), @"Icon=""/([^""]+)""");
            if (m.Success) Assert.True(File.Exists(Path.Combine(Win, m.Groups[1].Value)), $"{Path.GetFileName(f)} → {m.Groups[1].Value}");
        }
    }

    [Fact]
    public void Old_product_names_are_gone_from_user_facing_code()
    {
        var hits = CsFiles().Concat(XamlFiles())
            .Where(f => !f.EndsWith("AppPaths.cs"))                       // legacy folder names for migration
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"Ekran|VAEN|Vaen"))
            .Select(Path.GetFileName).ToList();
        Assert.Empty(hits);
    }
}
