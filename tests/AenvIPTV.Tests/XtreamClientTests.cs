using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Aenv.Models;
using Aenv.Services;
using Xunit;

namespace Aenv.Tests;

public class XtreamClientTests : IDisposable
{
    private readonly FakeXtreamServer _srv = new();
    private readonly Guid _pid = Guid.NewGuid();

    public XtreamClientTests()
    {
        _srv.Responses[""] = "{\"user_info\":{\"auth\":1,\"status\":\"Active\"},\"server_info\":{}}";
        // Real servers mix numbers and strings for ids – both must work
        _srv.Responses["get_live_categories"] = "[{\"category_id\":\"1\",\"category_name\":\"News\"},{\"category_id\":2,\"category_name\":\"Sport\"}]";
        _srv.Responses["get_live_streams"] =
            "[{\"stream_id\":101,\"name\":\"CNN\",\"stream_icon\":\"http://l/cnn.png\",\"category_id\":\"1\",\"epg_channel_id\":\"cnn.us\"}," +
            " {\"stream_id\":\"102\",\"name\":\"ESPN\",\"stream_icon\":\"\",\"category_id\":2,\"epg_channel_id\":null}," +
            " {\"name\":\"Broken (no id)\",\"category_id\":\"1\"}," +
            " {\"stream_id\":103,\"name\":\"Orphan\",\"category_id\":\"999\"}]";
        _srv.Responses["get_vod_categories"] = "[{\"category_id\":\"5\",\"category_name\":\"Action\"}]";
        _srv.Responses["get_vod_streams"] = "[{\"stream_id\":201,\"name\":\"Movie A\",\"stream_icon\":\"http://l/a.jpg\",\"category_id\":\"5\",\"container_extension\":\"mkv\"}]";
        _srv.Responses["get_series_categories"] = "[{\"category_id\":\"7\",\"category_name\":\"Drama\"}]";
        _srv.Responses["get_series"] = "[{\"series_id\":301,\"name\":\"Show X\",\"cover\":\"http://l/x.jpg\",\"category_id\":\"7\"}]";
        _srv.Responses["get_series_info"] =
            "{\"episodes\":{\"2\":[{\"id\":\"402\",\"episode_num\":1,\"title\":\"S2E1\",\"container_extension\":\"mp4\"}]," +
            "\"1\":[{\"id\":\"401\",\"episode_num\":\"2\",\"title\":\"S1E2\",\"container_extension\":\"mkv\"},{\"id\":400,\"episode_num\":1,\"title\":\"S1E1\"}]}}";
    }

    public void Dispose() => _srv.Dispose();

    private XtreamClient Client(string user = "user", string pass = "pass") => new(_srv.BaseUrl + "/", user, pass, _pid);

    [Fact]
    public async Task Authenticates()
    {
        await Client().AuthenticateAsync();
    }

    [Fact]
    public async Task Rejects_wrong_credentials_with_localized_message()
    {
        _srv.Responses[""] = "{\"user_info\":{\"auth\":0}}";
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => Client().AuthenticateAsync());
        Assert.Equal(Loc.T("auth_failed"), ex.Message);
    }

    [Fact]
    public async Task Rejects_non_json_response()
    {
        _srv.Responses[""] = "<html>Not an Xtream server</html>";
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => Client().AuthenticateAsync());
        Assert.Equal(Loc.T("bad_response"), ex.Message);
    }

    [Fact]
    public async Task Loads_live_vod_and_series_with_categories()
    {
        var all = await Client().LoadAllAsync();

        var live = all.Where(c => c.Kind == ContentKind.Live).ToList();
        Assert.Equal(new[] { "CNN", "ESPN", "Orphan" }, live.Select(c => c.Name));     // entry without id skipped
        Assert.Equal("News", live[0].Group);
        Assert.Equal("Sport", live[1].Group);                                          // numeric category id
        Assert.Equal(Loc.T("other"), live[2].Group);                                    // unknown category
        Assert.Equal("cnn.us", live[0].TvgId);
        Assert.Null(live[1].Logo);                                                     // empty icon → null
        Assert.Equal(_srv.BaseUrl + "/live/user/pass/101.ts", live[0].StreamUrl);
        Assert.All(all, c => Assert.Equal(_pid, c.PlaylistId));

        var movie = all.Single(c => c.Kind == ContentKind.Movie);
        Assert.Equal(_srv.BaseUrl + "/movie/user/pass/201.mkv", movie.StreamUrl);
        Assert.Equal("Action", movie.Group);

        var series = all.Single(c => c.Kind == ContentKind.Series);
        Assert.Equal("301", series.SeriesId);
        Assert.Equal("http://l/x.jpg", series.Logo);
        Assert.Null(series.StreamUrl);
    }

    [Fact]
    public async Task Channel_ids_are_unique_and_stable()
    {
        var a = await Client().LoadAllAsync();
        var b = await Client().LoadAllAsync();
        Assert.Equal(a.Count, a.Select(c => c.Id).Distinct().Count());
        Assert.Equal(a.Select(c => c.Id), b.Select(c => c.Id));
    }

    [Fact]
    public async Task Episodes_are_sorted_by_season_and_number()
    {
        var eps = await Client().EpisodesAsync("301");
        Assert.Equal(new[] { "S1E1", "S1E2", "S2E1" }, eps.Select(e => e.Title));
        Assert.Equal(_srv.BaseUrl + "/series/user/pass/400.mp4", eps[0].StreamUrl);   // default extension
        Assert.Equal(_srv.BaseUrl + "/series/user/pass/401.mkv", eps[1].StreamUrl);
        Assert.Equal(2, eps[2].Season);
    }

    [Fact]
    public async Task Escapes_special_characters_in_credentials()
    {
        var c = Client("ali@mail.com", "p&ss w/rd");
        await c.AuthenticateAsync();
        var q = HttpUtility.ParseQueryString(_srv.Requests.Last().Query);
        Assert.Equal("ali@mail.com", q["username"]);
        Assert.Equal("p&ss w/rd", q["password"]);
        var live = (await c.LoadAsync(ContentKind.Live)).First();
        Assert.Contains("/live/ali%40mail.com/p%26ss%20w%2Frd/101.ts", live.StreamUrl);
    }

    [Fact]
    public void Builds_epg_url()
    {
        Assert.Equal(_srv.BaseUrl + "/xmltv.php?username=user&password=pass", Client().EpgUrl);
    }
}
