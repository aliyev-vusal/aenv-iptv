using System;
using System.Text.Json.Serialization;

namespace Aenv.Models;

public enum SourceType { M3U, Xtream }

public enum ContentKind { Live, Movie, Series }

public class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public SourceType Type { get; set; }
    /// <summary>Playlist URL for M3U, server address for Xtream.</summary>
    public string Url { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }
    /// <summary>Optional XMLTV URL (.xml / .xml.gz).</summary>
    public string? EpgUrl { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;

    public override string ToString() => Name;
}

public class Channel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Logo { get; set; }
    public string Group { get; set; } = "";
    public string? StreamUrl { get; set; }
    public string? TvgId { get; set; }
    public ContentKind Kind { get; set; }
    public string? SeriesId { get; set; }
    public Guid PlaylistId { get; set; }

    // UI only, filled in while filtering
    [JsonIgnore] public string Subtitle { get; set; } = "";
    [JsonIgnore] public double Progress { get; set; }
    [JsonIgnore] public bool HasProgress => Progress > 0;
    [JsonIgnore] public bool IsFavorite { get; set; }
    [JsonIgnore] public bool IsLive => Kind == ContentKind.Live;
}

public class Episode
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int Season { get; set; }
    public int Number { get; set; }
    public string StreamUrl { get; set; } = "";
    public string Display => $"{Number}. {Title}";
}

public class Programme
{
    public string ChannelId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Desc { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset Stop { get; set; }

    public bool IsLive => DateTimeOffset.Now >= Start && DateTimeOffset.Now < Stop;
    public double Progress
    {
        get
        {
            var total = (Stop - Start).TotalSeconds;
            if (total <= 0) return 0;
            return Math.Clamp((DateTimeOffset.Now - Start).TotalSeconds / total, 0, 1);
        }
    }
}
