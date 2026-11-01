using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aenv.Models;
using Aenv.Services;
using Xunit;

namespace Aenv.Tests;

/// <summary>Disk cache for channel lists and guides: fewer requests to the provider, instant start.</summary>
[Collection("Http")]
public class CacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aenv-cache-" + Guid.NewGuid().ToString("N"));
    private readonly FakeXtreamServer _srv = new();
    private readonly string _previousRoot = DiskCache.Root;

    public CacheTests()
    {
        DiskCache.Root = _dir;
        Http.Delay = (_, _) => Task.CompletedTask;
        var t = DateTimeOffset.UtcNow.AddMinutes(-10);
        var xml = $"<tv><programme start=\"{t:yyyyMMddHHmmss} +0000\" stop=\"{t.AddHours(1):yyyyMMddHHmmss} +0000\" channel=\"c\"><title>Live now</title></programme></tv>";
        _srv.Files["/epg.xml"] = Encoding.UTF8.GetBytes(xml);
    }

    public void Dispose()
    {
        DiskCache.Root = _previousRoot;
        Http.Delay = (t, ct) => Task.Delay(t, ct);
        ChannelCache.MaxAge = TimeSpan.FromHours(6);
        _srv.Dispose();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string EpgUrl => _srv.BaseUrl + "/epg.xml";

    [Fact]
    public async Task Guide_is_downloaded_once_and_then_read_from_disk()
    {
        await EpgService.LoadAsync(EpgUrl);
        EpgService.Forget(EpgUrl);                     // simulate an app restart
        var g = await EpgService.LoadAsync(EpgUrl);
        Assert.Equal(1, _srv.Count("/epg.xml"));
        Assert.Equal("Live now", g["c"].Single().Title);
    }

    [Fact]
    public async Task Forced_refresh_downloads_the_guide_again()
    {
        await EpgService.LoadAsync(EpgUrl);
        await EpgService.LoadAsync(EpgUrl, force: true);
        Assert.Equal(2, _srv.Count("/epg.xml"));
    }

    [Fact]
    public async Task Old_guide_is_used_when_the_server_is_rate_limiting()
    {
        await EpgService.LoadAsync(EpgUrl);
        _srv.Fail("/epg.xml", 429, times: 10);
        var g = await EpgService.LoadAsync(EpgUrl, force: true);
        Assert.Equal("Live now", g["c"].Single().Title);
    }

    [Fact]
    public async Task Guide_error_is_reported_when_nothing_is_cached()
    {
        _srv.Fail("/epg.xml", 429, times: 10);
        await Assert.ThrowsAnyAsync<HttpFailureException>(() => EpgService.LoadAsync(EpgUrl, force: true));
    }

    [Fact]
    public void Channel_list_round_trips_and_ages()
    {
        var id = Guid.NewGuid();
        var list = new List<Channel>
        {
            new() { Id = "1", Name = "Lumora Nature", Group = "Nature", StreamUrl = "http://x/1.m3u8", Kind = ContentKind.Live, TvgId = "lumora.aenv" },
            new() { Id = "2", Name = "Tide Pool", Group = "Movies", StreamUrl = "http://x/2.mp4", Kind = ContentKind.Movie },
        };
        ChannelCache.Save(id, list, "http://x/epg.xml.gz");

        var back = ChannelCache.Load(id)!;
        Assert.Equal(list.Select(c => c.Name), back.Channels.Select(c => c.Name));
        Assert.Equal(ContentKind.Movie, back.Channels[1].Kind);
        Assert.Equal("lumora.aenv", back.Channels[0].TvgId);
        Assert.Equal("http://x/epg.xml.gz", back.EpgUrl);
        Assert.True(ChannelCache.IsFresh(id));

        ChannelCache.MaxAge = TimeSpan.Zero;
        Assert.False(ChannelCache.IsFresh(id));        // still loadable, but due for a refresh
        Assert.NotNull(ChannelCache.Load(id));

        ChannelCache.Remove(id);
        Assert.Null(ChannelCache.Load(id));
    }

    [Fact]
    public void Corrupt_cache_file_is_ignored()
    {
        var id = Guid.NewGuid();
        DiskCache.Write("channels:" + id, Encoding.UTF8.GetBytes("{ not json"));
        Assert.Null(ChannelCache.Load(id));
    }
}
