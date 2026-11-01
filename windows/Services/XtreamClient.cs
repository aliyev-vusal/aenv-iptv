using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aenv.Models;

namespace Aenv.Services;

/// <summary>Xtream Codes "player_api.php" client. Fields may be numbers or strings, so JsonNode is used.</summary>
public class XtreamClient
{
    private readonly string _server, _user, _pass;
    private readonly Guid _playlistId;

    public XtreamClient(string server, string user, string pass, Guid playlistId = default)
    {
        _server = server.TrimEnd('/');
        _user = user; _pass = pass; _playlistId = playlistId;
    }

    public string EpgUrl => $"{_server}/xmltv.php?username={Esc(_user)}&password={Esc(_pass)}";

    public async Task AuthenticateAsync()
    {
        var node = await RequestAsync(null);
        if (Int(node?["user_info"]?["auth"]) != 1)
            throw new Exception(Loc.T("auth_failed"));
    }

    public async Task<List<Channel>> LoadAllAsync()
    {
        // One request at a time: many providers allow a single connection per account
        // and answer parallel requests with 429 Too Many Requests.
        var all = new List<Channel>();
        foreach (var kind in new[] { ContentKind.Live, ContentKind.Movie, ContentKind.Series })
            all.AddRange(await LoadAsync(kind));
        return all;
    }

    public async Task<List<Channel>> LoadAsync(ContentKind kind)
    {
        var (catAction, streamAction) = kind switch
        {
            ContentKind.Live => ("get_live_categories", "get_live_streams"),
            ContentKind.Movie => ("get_vod_categories", "get_vod_streams"),
            _ => ("get_series_categories", "get_series")
        };
        var catsNode = await RequestAsync(catAction);
        var itemsNode = await RequestAsync(streamAction);

        var categories = new Dictionary<string, string>();
        if (catsNode is JsonArray cats)
            foreach (var c in cats)
                if (Str(c?["category_id"]) is { } id) categories[id] = Str(c?["category_name"]) ?? Loc.T("other");

        var list = new List<Channel>();
        if (itemsNode is not JsonArray items) return list;
        var host = new Uri(_server).Host;

        foreach (var item in items)
        {
            if (item is null) continue;
            var group = Str(item["category_id"]) is { } cid && categories.TryGetValue(cid, out var g) ? g : Loc.T("other");
            var name = Str(item["name"]) ?? "?";
            switch (kind)
            {
                case ContentKind.Live when Str(item["stream_id"]) is { } sid:
                    list.Add(new Channel
                    {
                        Id = $"xt-live-{host}-{sid}", Name = name, Group = group, Kind = kind,
                        Logo = Str(item["stream_icon"]), TvgId = Str(item["epg_channel_id"]),
                        // VLC plays MPEG-TS directly, the most stable option
                        StreamUrl = StreamUrl("live", sid, "ts"), PlaylistId = _playlistId
                    });
                    break;
                case ContentKind.Movie when Str(item["stream_id"]) is { } sid:
                    list.Add(new Channel
                    {
                        Id = $"xt-vod-{host}-{sid}", Name = name, Group = group, Kind = kind,
                        Logo = Str(item["stream_icon"]),
                        StreamUrl = StreamUrl("movie", sid, Str(item["container_extension"]) ?? "mp4"),
                        PlaylistId = _playlistId
                    });
                    break;
                case ContentKind.Series when Str(item["series_id"]) is { } sid:
                    list.Add(new Channel
                    {
                        Id = $"xt-series-{host}-{sid}", Name = name, Group = group, Kind = kind,
                        Logo = Str(item["cover"]), SeriesId = sid, PlaylistId = _playlistId
                    });
                    break;
            }
        }
        return list;
    }

    public async Task<List<Episode>> EpisodesAsync(string seriesId)
    {
        var node = await RequestAsync("get_series_info", $"&series_id={Esc(seriesId)}");
        var result = new List<Episode>();
        if (node?["episodes"] is not JsonObject seasons) return result;

        foreach (var (seasonKey, value) in seasons)
        {
            if (value is not JsonArray eps) continue;
            foreach (var ep in eps)
            {
                if (ep is null || Str(ep["id"]) is not { } id) continue;
                result.Add(new Episode
                {
                    Id = id,
                    Title = Str(ep["title"]) ?? Loc.T("episode"),
                    Season = int.TryParse(seasonKey, out var s) ? s : Int(ep["season"]) ?? 1,
                    Number = Int(ep["episode_num"]) ?? 0,
                    StreamUrl = StreamUrl("series", id, Str(ep["container_extension"]) ?? "mp4")
                });
            }
        }
        return result.OrderBy(e => e.Season).ThenBy(e => e.Number).ToList();
    }

    // ---- helpers ----

    private string StreamUrl(string type, string id, string ext) =>
        $"{_server}/{type}/{Uri.EscapeDataString(_user)}/{Uri.EscapeDataString(_pass)}/{id}.{ext}";

    private async Task<JsonNode?> RequestAsync(string? action, string extra = "")
    {
        var url = $"{_server}/player_api.php?username={Esc(_user)}&password={Esc(_pass)}";
        if (action != null) url += $"&action={action}";
        url += extra;
        var json = await Http.GetStringAsync(url);
        try { return JsonNode.Parse(json); }
        catch { throw new Exception(Loc.T("bad_response")); }
    }

    private static string Esc(string s) => Uri.EscapeDataString(s);

    private static string? Str(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        var s = v.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static int? Int(JsonNode? n) => int.TryParse(Str(n), out var i) ? i : null;
}
