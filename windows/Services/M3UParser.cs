using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aenv.Models;

namespace Aenv.Services;

/// <summary>M3U / M3U8 (EXTINF) and XSPF playlist parser.</summary>
public static class M3UParser
{
    private static readonly Regex AttrRegex = new(@"([A-Za-z0-9\-_]+)=""([^""]*)""", RegexOptions.Compiled);

    public record Result(List<Channel> Channels, string? EpgUrl);

    public static Result Parse(string text, Guid playlistId)
    {
        if (text.Contains("<playlist") && text.Contains("xspf"))
            return new Result(ParseXspf(text, playlistId), null);

        var channels = new List<Channel>();
        string? epg = null, pendingName = null, pendingGroup = null;
        var attrs = new Dictionary<string, string>();

        using var reader = new StringReader(text);
        string? raw;
        while ((raw = reader.ReadLine()) != null)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXTM3U"))
            {
                var a = Attributes(line);
                if (a.TryGetValue("url-tvg", out var u) || a.TryGetValue("x-tvg-url", out u))
                    epg = u.Split(',')[0].Trim();
            }
            else if (line.StartsWith("#EXTINF"))
            {
                attrs = Attributes(line);
                pendingName = DisplayName(line);
            }
            else if (line.StartsWith("#EXTGRP:"))
            {
                pendingGroup = line["#EXTGRP:".Length..];
            }
            else if (line.StartsWith("#"))
            {
                continue;
            }
            else if (Uri.TryCreate(line, UriKind.Absolute, out var uri))
            {
                attrs.TryGetValue("tvg-name", out var tvgName);
                attrs.TryGetValue("group-title", out var group);
                attrs.TryGetValue("tvg-logo", out var logo);
                attrs.TryGetValue("tvg-id", out var tvgId);
                group ??= pendingGroup;

                channels.Add(new Channel
                {
                    Id = line,
                    Name = pendingName ?? tvgName ?? Path.GetFileName(uri.AbsolutePath),
                    Logo = string.IsNullOrWhiteSpace(logo) ? null : logo,
                    Group = string.IsNullOrWhiteSpace(group) ? Loc.T("other") : group,
                    StreamUrl = line,
                    TvgId = string.IsNullOrWhiteSpace(tvgId) ? null : tvgId,
                    Kind = GuessKind(uri),
                    PlaylistId = playlistId
                });
                pendingName = null; pendingGroup = null; attrs = new();
            }
        }
        return new Result(channels, epg);
    }

    private static Dictionary<string, string> Attributes(string line) =>
        AttrRegex.Matches(line)
            .GroupBy(m => m.Groups[1].Value.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Groups[2].Value);

    /// <summary>The channel name is everything after the first comma outside quotes (names may contain commas).</summary>
    private static string? DisplayName(string line)
    {
        bool inQuotes = false; int comma = -1;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            else if (line[i] == ',' && !inQuotes) { comma = i; break; }
        }
        if (comma < 0) return null;
        var name = line[(comma + 1)..].Trim();
        return name.Length == 0 ? null : name;
    }

    private static ContentKind GuessKind(Uri uri)
    {
        var p = uri.AbsolutePath.ToLowerInvariant();
        if (p.Contains("/movie/") || p.Contains("/series/")) return ContentKind.Movie;
        var ext = Path.GetExtension(p).TrimStart('.');
        return ext is "mp4" or "mkv" or "avi" or "mov" ? ContentKind.Movie : ContentKind.Live;
    }

    private static List<Channel> ParseXspf(string xml, Guid playlistId)
    {
        var doc = XDocument.Parse(xml);
        return doc.Descendants().Where(e => e.Name.LocalName == "track").Select(t =>
        {
            string? Get(string n) => t.Elements().FirstOrDefault(e => e.Name.LocalName == n)?.Value.Trim();
            var loc = Get("location");
            return loc == null ? null : new Channel
            {
                Id = loc, StreamUrl = loc,
                Name = Get("title") ?? loc,
                Logo = Get("image"),
                Group = Get("album") ?? Loc.T("other"),
                Kind = ContentKind.Live,
                PlaylistId = playlistId
            };
        }).Where(c => c != null).Cast<Channel>().ToList();
    }
}
