using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Aenv.Models;
using Aenv.Services;
using Xunit;

namespace Aenv.Tests;

public class EpgTests
{
    private static string Ts(DateTimeOffset t) => t.ToUniversalTime().ToString("yyyyMMddHHmmss") + " +0000";

    private static string Guide(params (string ch, DateTimeOffset start, DateTimeOffset stop, string title, string? desc)[] items)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE tv SYSTEM \"xmltv.dtd\">\n<tv>\n<channel id=\"a\"><display-name>A</display-name></channel>\n");
        foreach (var p in items)
        {
            sb.Append($"<programme start=\"{Ts(p.start)}\" stop=\"{Ts(p.stop)}\" channel=\"{p.ch}\">");
            sb.Append($"<title lang=\"en\">{p.title}</title>");
            if (p.desc != null) sb.Append($"<desc lang=\"en\">{p.desc}</desc>");
            sb.Append("<category>News</category></programme>\n");
        }
        return sb.Append("</tv>").ToString();
    }

    [Fact]
    public void Parses_titles_descriptions_and_sorts()
    {
        var now = DateTimeOffset.Now;
        var xml = Guide(
            ("a", now.AddHours(1), now.AddHours(2), "Later", "L desc"),
            ("a", now.AddMinutes(-30), now.AddMinutes(30), "Now on", "Now desc"));
        var g = EpgService.Parse(Encoding.UTF8.GetBytes(xml));

        var list = g["a"];
        Assert.Equal(new[] { "Now on", "Later" }, list.Select(p => p.Title));
        Assert.Equal("Now desc", list[0].Desc);
        Assert.True(list[0].IsLive);
        Assert.False(list[1].IsLive);
        Assert.InRange(list[0].Progress, 0.4, 0.6);
    }

    [Fact]
    public void Keeps_only_the_minus3h_plus24h_window()
    {
        var now = DateTimeOffset.Now;
        var xml = Guide(
            ("a", now.AddHours(-10), now.AddHours(-9), "Old", null),
            ("a", now.AddHours(30), now.AddHours(31), "Far future", null),
            ("a", now.AddHours(5), now.AddHours(6), "Kept", null));
        Assert.Equal(new[] { "Kept" }, EpgService.Parse(Encoding.UTF8.GetBytes(xml))["a"].Select(p => p.Title));
    }

    [Fact]
    public void Reads_gzip_compressed_guides()
    {
        var now = DateTimeOffset.Now;
        var raw = Encoding.UTF8.GetBytes(Guide(("b", now, now.AddHours(1), "Zipped", null)));
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(raw);
        Assert.Equal("Zipped", EpgService.Parse(ms.ToArray())["b"].Single().Title);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-5)]
    [InlineData(0)]
    public void Applies_timezone_offsets(int offsetHours)
    {
        var start = DateTimeOffset.UtcNow.AddHours(1);
        start = new DateTimeOffset(start.Year, start.Month, start.Day, start.Hour, start.Minute, start.Second, TimeSpan.Zero);
        var local = start.ToOffset(TimeSpan.FromHours(offsetHours));
        var sign = offsetHours < 0 ? "-" : "+";
        var off = $"{sign}{Math.Abs(offsetHours):00}00";
        var xml = $"<tv><programme start=\"{local:yyyyMMddHHmmss} {off}\" stop=\"{local.AddHours(1):yyyyMMddHHmmss} {off}\" channel=\"c\"><title>T</title></programme></tv>";

        var p = EpgService.Parse(Encoding.UTF8.GetBytes(xml))["c"].Single();
        Assert.Equal(start.UtcDateTime, p.Start.UtcDateTime);
        Assert.Equal(TimeSpan.FromHours(1), p.Stop - p.Start);
    }

    [Fact]
    public void Skips_programmes_with_invalid_dates()
    {
        var xml = "<tv><programme start=\"garbage\" stop=\"20991231000000 +0000\" channel=\"c\"><title>X</title></programme></tv>";
        Assert.False(EpgService.Parse(Encoding.UTF8.GetBytes(xml)).ContainsKey("c"));
    }

    [Fact]
    public void Programme_progress_is_clamped()
    {
        var p = new Programme { Start = DateTimeOffset.Now.AddHours(-2), Stop = DateTimeOffset.Now.AddHours(-1) };
        Assert.Equal(1, p.Progress);
        var t = DateTimeOffset.Now;
        var q = new Programme { Start = t, Stop = t };
        Assert.Equal(0, q.Progress);
    }
}
