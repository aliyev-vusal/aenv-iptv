using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml;
using Aenv.Models;

namespace Aenv.Services;

/// <summary>XMLTV programme guide (.xml and .xml.gz). Only [now-3 h, now+24 h] is kept.</summary>
public static class EpgService
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, List<Programme>>> Cache = new();

    /// <summary>Guides are big and providers rate-limit them, so a downloaded guide is reused for this long.</summary>
    public static TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(12);

    /// <summary>
    /// Loads a guide: memory → fresh disk copy → download. If the download fails, an older disk copy
    /// is used rather than showing no guide at all.
    /// </summary>
    public static async Task<Dictionary<string, List<Programme>>> LoadAsync(string url, bool force = false)
    {
        if (!force && Cache.TryGetValue(url, out var cached)) return cached;

        var key = "epg:" + url;
        var bytes = force ? null : DiskCache.Read(key, MaxAge);
        if (bytes == null)
        {
            try
            {
                bytes = await Http.GetBytesAsync(url);
                DiskCache.Write(key, bytes);
            }
            catch
            {
                bytes = DiskCache.ReadAny(key);
                if (bytes == null) throw;
            }
        }

        var result = await Task.Run(() => Parse(bytes));
        Cache[url] = result;
        return result;
    }

    /// <summary>Drops the in-memory copy (used by tests and when a playlist is removed).</summary>
    public static void Forget(string url) => Cache.TryRemove(url, out _);

    internal static Dictionary<string, List<Programme>> Parse(byte[] bytes)
    {
        Stream stream = new MemoryStream(bytes);
        if (bytes.Length > 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
            stream = new GZipStream(stream, CompressionMode.Decompress);

        var from = DateTimeOffset.Now.AddHours(-3);
        var to = DateTimeOffset.Now.AddHours(24);
        var result = new Dictionary<string, List<Programme>>();

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, Async = false };
        using var reader = XmlReader.Create(stream, settings);
        while (reader.ReadToFollowing("programme"))
        {
            var ch = reader.GetAttribute("channel");
            var start = ParseDate(reader.GetAttribute("start"));
            var stop = ParseDate(reader.GetAttribute("stop"));
            if (ch == null || start == null || stop == null || stop <= from || start >= to) continue;

            string title = "", desc = "";
            using (var sub = reader.ReadSubtree())
            {
                sub.Read(); // <programme>
                sub.Read(); // first child element
                while (!sub.EOF)
                {
                    if (sub.NodeType == XmlNodeType.Element && sub.Name == "title" && title.Length == 0)
                        title = sub.ReadElementContentAsString();      // advances the reader itself
                    else if (sub.NodeType == XmlNodeType.Element && sub.Name == "desc" && desc.Length == 0)
                        desc = sub.ReadElementContentAsString();
                    else
                        sub.Read();
                }
            }

            if (!result.TryGetValue(ch, out var list)) result[ch] = list = new List<Programme>();
            list.Add(new Programme { ChannelId = ch, Title = title, Desc = desc, Start = start.Value, Stop = stop.Value });
        }
        foreach (var l in result.Values) l.Sort((a, b) => a.Start.CompareTo(b.Start));
        return result;
    }

    /// <summary>XMLTV timestamp: "20261101183000 +0100"</summary>
    private static DateTimeOffset? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 14) return null;
        if (!DateTime.TryParseExact(s[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt)) return null;
        var offset = TimeSpan.Zero;
        var rest = s[14..].Trim();
        if (rest.Length >= 5 && (rest[0] == '+' || rest[0] == '-') &&
            int.TryParse(rest.AsSpan(1, 2), out var h) && int.TryParse(rest.AsSpan(3, 2), out var m))
        {
            offset = new TimeSpan(h, m, 0);
            if (rest[0] == '-') offset = -offset;
        }
        return new DateTimeOffset(dt, offset);
    }
}
