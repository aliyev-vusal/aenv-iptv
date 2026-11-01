using System;
using System.Collections.Generic;
using System.Text.Json;
using Aenv.Models;

namespace Aenv.Services;

/// <summary>
/// Last successfully loaded channel list of each playlist. Lets the app show channels immediately
/// on start and avoid contacting the provider again while the list is still fresh.
/// </summary>
public static class ChannelCache
{
    /// <summary>Lists younger than this are not reloaded automatically (manual refresh always reloads).</summary>
    public static TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(6);

    public record Entry(List<Channel> Channels, string? EpgUrl);

    private static string Key(Guid playlistId) => "channels:" + playlistId;

    public static void Save(Guid playlistId, List<Channel> channels, string? epgUrl) =>
        DiskCache.Write(Key(playlistId), JsonSerializer.SerializeToUtf8Bytes(new Entry(channels, epgUrl)));

    /// <summary>Returns the cached list regardless of age, or null.</summary>
    public static Entry? Load(Guid playlistId)
    {
        var bytes = DiskCache.ReadAny(Key(playlistId));
        if (bytes == null) return null;
        try { return JsonSerializer.Deserialize<Entry>(bytes); }
        catch { return null; }
    }

    public static bool IsFresh(Guid playlistId) =>
        DiskCache.WrittenAtUtc(Key(playlistId)) is { } t && DateTime.UtcNow - t < MaxAge;

    public static void Remove(Guid playlistId) => DiskCache.Remove(Key(playlistId));
}
