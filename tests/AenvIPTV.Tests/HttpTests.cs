using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aenv.Models;
using Aenv.Services;
using Xunit;

namespace Aenv.Tests;

/// <summary>Rate limiting (429), retries and user-facing error messages.</summary>
[Collection("Http")]
public class HttpTests : IDisposable
{
    private readonly FakeXtreamServer _srv = new();
    private readonly List<TimeSpan> _waits = new();

    public HttpTests()
    {
        Http.Delay = (t, _) => { lock (_waits) _waits.Add(t); return Task.CompletedTask; };   // don't really wait
        _srv.Responses[""] = "{\"user_info\":{\"auth\":1}}";
        _srv.Responses["ok"] = "fine";
    }

    public void Dispose()
    {
        Http.Delay = (t, ct) => Task.Delay(t, ct);
        _srv.Dispose();
    }

    private string Url(string action) => $"{_srv.BaseUrl}/player_api.php?action={action}";

    [Fact]
    public async Task Retries_after_429_and_succeeds()
    {
        _srv.Fail("ok", 429, times: 2);
        Assert.Equal("fine", await Http.GetStringAsync(Url("ok")));
        Assert.Equal(3, _srv.Count("ok"));
        Assert.Equal(2, _waits.Count);
    }

    [Fact]
    public async Task Honours_retry_after_header()
    {
        _srv.Fail("ok", 429, times: 1, retryAfter: "7");
        await Http.GetStringAsync(Url("ok"));
        Assert.Equal(TimeSpan.FromSeconds(7), _waits.Single());
    }

    [Fact]
    public async Task Caps_very_long_retry_after()
    {
        _srv.Fail("ok", 503, times: 1, retryAfter: "3600");
        await Http.GetStringAsync(Url("ok"));
        Assert.Equal(Http.MaxRetryDelay, _waits.Single());
    }

    [Fact]
    public async Task Gives_up_with_a_helpful_message_when_still_rate_limited()
    {
        _srv.Fail("ok", 429, times: 10);
        var ex = await Assert.ThrowsAnyAsync<HttpFailureException>(() => Http.GetStringAsync(Url("ok")));
        Assert.Equal(Loc.T("http_too_many"), ex.Message);
        Assert.DoesNotContain("Response status code", ex.Message);
        Assert.Equal(Http.MaxAttempts, _srv.Count("ok"));
    }

    [Fact]
    public async Task Does_not_retry_wrong_credentials()
    {
        _srv.Fail("ok", 401, times: 5);
        var ex = await Assert.ThrowsAnyAsync<HttpFailureException>(() => Http.GetStringAsync(Url("ok")));
        Assert.Equal(Loc.T("http_denied"), ex.Message);
        Assert.Equal(1, _srv.Count("ok"));
    }

    [Theory]
    [InlineData(404, "http_not_found")]
    [InlineData(403, "http_denied")]
    public async Task Maps_status_codes_to_messages(int status, string key)
    {
        _srv.Fail("ok", status, times: 5);
        var ex = await Assert.ThrowsAnyAsync<HttpFailureException>(() => Http.GetStringAsync(Url("ok")));
        Assert.Equal(Loc.T(key), ex.Message);
    }

    [Fact]
    public async Task Server_errors_mention_the_code()
    {
        _srv.Fail("ok", 500, times: 5);
        var ex = await Assert.ThrowsAnyAsync<HttpFailureException>(() => Http.GetStringAsync(Url("ok")));
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task Unreachable_server_gives_network_message()
    {
        var url = $"http://127.0.0.1:{FakeXtreamServer.FreePort()}/player_api.php";
        var ex = await Assert.ThrowsAnyAsync<HttpFailureException>(() => Http.GetStringAsync(url));
        Assert.Equal(Loc.T("http_network"), ex.Message);
    }

    [Fact]
    public async Task Xtream_loading_never_sends_parallel_requests()
    {
        var all = await new XtreamClient(_srv.BaseUrl, "u", "p").LoadAllAsync();
        Assert.Equal(6, _srv.Requests.Count);          // 3 × (categories + streams)
        Assert.Equal(1, _srv.MaxConcurrent);
        Assert.Empty(all);
    }

    [Fact]
    public async Task Xtream_loading_survives_a_temporary_429()
    {
        _srv.Responses["get_live_streams"] = "[{\"stream_id\":1,\"name\":\"A\",\"category_id\":\"1\"}]";
        _srv.Fail("get_live_streams", 429, times: 1, retryAfter: "1");
        var all = await new XtreamClient(_srv.BaseUrl, "u", "p").LoadAllAsync();
        Assert.Single(all.Where(c => c.Kind == ContentKind.Live));
    }
}
