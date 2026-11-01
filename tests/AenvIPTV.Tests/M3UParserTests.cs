using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Aenv.Models;
using Aenv.Services;
using Xunit;

namespace Aenv.Tests;

public class M3UParserTests
{
    private static readonly Guid Pid = Guid.NewGuid();

    [Fact]
    public void Parses_extinf_attributes_and_name()
    {
        const string m3u = "#EXTM3U\n" +
            "#EXTINF:-1 tvg-id=\"cnn.us\" tvg-name=\"CNN\" tvg-logo=\"http://logo/cnn.png\" group-title=\"News\",CNN International\n" +
            "http://srv/live/1.ts\n";
        var ch = M3UParser.Parse(m3u, Pid).Channels.Single();

        Assert.Equal("CNN International", ch.Name);
        Assert.Equal("cnn.us", ch.TvgId);
        Assert.Equal("http://logo/cnn.png", ch.Logo);
        Assert.Equal("News", ch.Group);
        Assert.Equal("http://srv/live/1.ts", ch.StreamUrl);
        Assert.Equal(ContentKind.Live, ch.Kind);
        Assert.Equal(Pid, ch.PlaylistId);
    }

    [Fact]
    public void Comma_inside_quoted_attribute_does_not_break_name()
    {
        const string m3u = "#EXTINF:-1 group-title=\"Sport, HD\",Eurosport 1\nhttp://a/b.m3u8\n";
        var ch = M3UParser.Parse(m3u, Pid).Channels.Single();
        Assert.Equal("Eurosport 1", ch.Name);
        Assert.Equal("Sport, HD", ch.Group);
    }

    [Theory]
    [InlineData("#EXTINF:-1 group-title=\"Music\",Rock, Pop & Jazz", "Rock, Pop & Jazz")]
    [InlineData("#EXTINF:-1,News, Weather", "News, Weather")]
    [InlineData("#EXTINF:-1 tvg-name=\"A, B\" group-title=\"G, H\",Real, Name", "Real, Name")]
    public void Channel_names_may_contain_commas(string extinf, string expected)
    {
        var ch = M3UParser.Parse($"{extinf}\nhttp://x/1\n", Pid).Channels.Single();
        Assert.Equal(expected, ch.Name);
    }

    [Fact]
    public void Reads_epg_url_from_header_first_entry()
    {
        const string m3u = "#EXTM3U url-tvg=\"http://epg/a.xml.gz,http://epg/b.xml\"\n#EXTINF:-1,A\nhttp://x/1\n";
        Assert.Equal("http://epg/a.xml.gz", M3UParser.Parse(m3u, Pid).EpgUrl);
    }

    [Fact]
    public void Supports_x_tvg_url_alias()
    {
        const string m3u = "#EXTM3U x-tvg-url=\"http://epg/c.xml\"\n";
        Assert.Equal("http://epg/c.xml", M3UParser.Parse(m3u, Pid).EpgUrl);
    }

    [Fact]
    public void Uses_extgrp_when_group_title_missing()
    {
        const string m3u = "#EXTINF:-1,Kids One\n#EXTGRP:Kids\nhttp://x/k1\n";
        Assert.Equal("Kids", M3UParser.Parse(m3u, Pid).Channels.Single().Group);
    }

    [Fact]
    public void Missing_group_falls_back_to_localized_other()
    {
        var ch = M3UParser.Parse("#EXTINF:-1,Solo\nhttp://x/s\n", Pid).Channels.Single();
        Assert.Equal(Loc.T("other"), ch.Group);
    }

    [Theory]
    [InlineData("http://srv/movie/u/p/10.mkv", ContentKind.Movie)]
    [InlineData("http://srv/series/u/p/11.mp4", ContentKind.Movie)]
    [InlineData("http://cdn/file.MKV", ContentKind.Movie)]
    [InlineData("http://cdn/live/stream.m3u8", ContentKind.Live)]
    [InlineData("udp://@239.0.0.1:1234", ContentKind.Live)]
    [InlineData("rtmp://srv/app/stream", ContentKind.Live)]
    public void Detects_content_kind(string url, ContentKind expected)
    {
        var ch = M3UParser.Parse($"#EXTINF:-1,X\n{url}\n", Pid).Channels.Single();
        Assert.Equal(expected, ch.Kind);
    }

    [Fact]
    public void Handles_crlf_bom_blank_lines_and_vlc_options()
    {
        var m3u = "﻿#EXTM3U\r\n\r\n#EXTINF:-1 group-title=\"G\",One\r\n#EXTVLCOPT:http-user-agent=X\r\nhttp://x/1\r\n\r\n#EXTINF:-1,Two\r\nhttp://x/2\r\n";
        var list = M3UParser.Parse(m3u, Pid).Channels;
        Assert.Equal(new[] { "One", "Two" }, list.Select(c => c.Name));
        Assert.Equal("G", list[0].Group);
    }

    [Fact]
    public void Url_without_extinf_uses_file_name()
    {
        var ch = M3UParser.Parse("http://x/path/news-hd.m3u8\n", Pid).Channels.Single();
        Assert.Equal("news-hd.m3u8", ch.Name);
    }

    [Fact]
    public void Attributes_do_not_leak_to_next_entry()
    {
        const string m3u = "#EXTINF:-1 tvg-logo=\"http://l/1.png\" group-title=\"A\",First\nhttp://x/1\nhttp://x/2\n";
        var list = M3UParser.Parse(m3u, Pid).Channels;
        Assert.Null(list[1].Logo);
        Assert.Equal(Loc.T("other"), list[1].Group);
    }

    [Fact]
    public void Ignores_garbage_lines()
    {
        var list = M3UParser.Parse("#EXTM3U\nnot a url\n#EXTINF:-1,Ok\nhttp://x/ok\n", Pid).Channels;
        Assert.Single(list);
    }

    [Fact]
    public void Parses_xspf()
    {
        const string xspf = "<?xml version=\"1.0\"?><playlist version=\"1\" xmlns=\"http://xspf.org/ns/0/\"><trackList>" +
            "<track><location>http://x/a.ts</location><title>Alpha</title><image>http://l/a.png</image><album>Music</album></track>" +
            "<track><location>http://x/b.ts</location></track></trackList></playlist>";
        var list = M3UParser.Parse(xspf, Pid).Channels;
        Assert.Equal(2, list.Count);
        Assert.Equal("Alpha", list[0].Name);
        Assert.Equal("Music", list[0].Group);
        Assert.Equal("http://l/a.png", list[0].Logo);
        Assert.Equal("http://x/b.ts", list[1].Name);
    }

    [Fact]
    public void Ids_are_stable_for_favorites()
    {
        const string m3u = "#EXTINF:-1,A\nhttp://x/1\n";
        Assert.Equal(M3UParser.Parse(m3u, Pid).Channels[0].Id, M3UParser.Parse(m3u, Guid.NewGuid()).Channels[0].Id);
    }

    [Fact]
    public void Parses_50000_entries_quickly()
    {
        var sb = new StringBuilder("#EXTM3U\n");
        for (int i = 0; i < 50_000; i++)
            sb.Append($"#EXTINF:-1 tvg-id=\"c{i}\" tvg-logo=\"http://l/{i}.png\" group-title=\"G{i % 40}\",Channel {i}\nhttp://srv/live/u/p/{i}.ts\n");
        var text = sb.ToString();

        var sw = Stopwatch.StartNew();
        var list = M3UParser.Parse(text, Pid).Channels;
        sw.Stop();

        Assert.Equal(50_000, list.Count);
        Assert.True(sw.ElapsedMilliseconds < 3000, $"parsing took {sw.ElapsedMilliseconds} ms");
    }
}
