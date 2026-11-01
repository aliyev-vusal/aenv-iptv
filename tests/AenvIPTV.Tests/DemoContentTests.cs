using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Aenv.Models;
using Aenv.Services;
using Xunit;

namespace Aenv.Tests;

/// <summary>
/// The demo set in docs/demo is used for screenshots and Store certification.
/// A broken link there would mean a failed certification, so every reference is checked.
/// </summary>
public class DemoContentTests
{
    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static readonly string Root = FindRoot();
    private static readonly string Demo = Path.Combine(Root, "docs", "demo");
    private const string Base = "https://raw.githubusercontent.com/aliyev-vusal/aenv-iptv/main/docs/demo/";

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "windows", "AenvIPTV.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static M3UParser.Result Playlist() =>
        M3UParser.Parse(File.ReadAllText(Path.Combine(Demo, "aenv-demo.m3u")), Guid.NewGuid());

    private static string Local(string url)
    {
        Assert.StartsWith(Base, url);
        return Path.Combine(Demo, url.Substring(Base.Length).Replace('/', Path.DirectorySeparatorChar));
    }

    [Fact]
    public void App_button_points_to_the_demo_playlist_in_this_repository()
    {
        Assert.Equal(Base + "aenv-demo.m3u", Aenv.Services.Demo.PlaylistUrl);
        Assert.True(File.Exists(Local(Aenv.Services.Demo.PlaylistUrl)));
    }

    [Fact]
    public void Playlist_has_six_live_channels_and_three_movies()
    {
        var list = Playlist().Channels;
        Assert.Equal(6, list.Count(c => c.Kind == ContentKind.Live));
        Assert.Equal(3, list.Count(c => c.Kind == ContentKind.Movie));
        Assert.Equal(list.Count, list.Select(c => c.Id).Distinct().Count());
        Assert.All(list, c => Assert.False(string.IsNullOrEmpty(c.Logo)));
    }

    [Fact]
    public void Every_stream_logo_and_guide_link_points_to_a_file_in_the_repository()
    {
        var p = Playlist();
        var missing = new List<string>();
        foreach (var c in p.Channels)
        {
            if (!File.Exists(Local(c.StreamUrl!))) missing.Add(c.StreamUrl!);
            if (!File.Exists(Local(c.Logo!))) missing.Add(c.Logo!);
        }
        if (!File.Exists(Local(p.EpgUrl!))) missing.Add(p.EpgUrl!);
        Assert.Empty(missing);
    }

    [Fact]
    public void Hls_playlists_reference_existing_segments_and_are_complete()
    {
        foreach (var c in Playlist().Channels.Where(c => c.Kind == ContentKind.Live))
        {
            var m3u8 = Local(c.StreamUrl!);
            var lines = File.ReadAllLines(m3u8);
            Assert.Contains("#EXT-X-ENDLIST", lines);
            var segments = lines.Where(l => l.EndsWith(".ts")).ToList();
            Assert.True(segments.Count >= 5, $"{c.Name}: only {segments.Count} segments");
            Assert.All(segments, s => Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(m3u8)!, s)), s));
        }
    }

    [Fact]
    public void Guide_has_a_current_programme_for_every_channel_until_end_of_2027()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Demo, "aenv-demo-epg.xml.gz"));
        var guide = EpgService.Parse(bytes);
        foreach (var c in Playlist().Channels.Where(c => c.Kind == ContentKind.Live))
        {
            Assert.True(guide.ContainsKey(c.TvgId!), $"no guide for {c.TvgId}");
            if (DateTimeOffset.Now < new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero))
                Assert.Contains(guide[c.TvgId!], p => p.IsLive);
        }
    }

    [Fact]
    public void Demo_files_stay_small_enough_for_the_repository()
    {
        var total = Directory.GetFiles(Demo, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        Assert.True(total < 15 * 1024 * 1024, $"demo folder is {total / 1048576.0:0.0} MB");
    }

    [Fact]
    public void Store_guide_points_certification_to_the_demo_playlist()
    {
        var guide = File.ReadAllText(Path.Combine(Root, "windows", "STORE.md"));
        Assert.Contains(Base + "aenv-demo.m3u", guide);
    }

    [Theory]
    [InlineData("screenshots/01-library.png", 1920, 1080)]
    [InlineData("screenshots/02-player-epg.png", 1920, 1080)]
    [InlineData("screenshots/03-channel-list.png", 1920, 1080)]
    [InlineData("screenshots/04-movies.png", 1920, 1080)]
    [InlineData("screenshots/05-add-playlist.png", 1920, 1080)]
    [InlineData("hero-1920x1080.png", 1920, 1080)]
    [InlineData("poster-720x1080.png", 720, 1080)]
    [InlineData("boxart-1080x1080.png", 1080, 1080)]
    [InlineData("tile-300x300.png", 300, 300)]
    public void Store_artwork_has_the_required_size(string file, int w, int h)
    {
        var b = File.ReadAllBytes(Path.Combine(Root, "store-assets", file.Replace('/', Path.DirectorySeparatorChar)));
        int Be(int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        Assert.Equal((w, h), (Be(16), Be(20)));
    }
}
