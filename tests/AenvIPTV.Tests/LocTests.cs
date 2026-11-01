using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Aenv.Tests;

public class LocTests
{
    [Fact]
    public void Every_key_has_all_five_languages()
    {
        Assert.Empty(Loc.BrokenKeys());
        Assert.Equal(5, Loc.Languages.Length);
    }

    [Fact]
    public void No_translation_is_empty()
    {
        var empty = Loc.AllStrings.Where(kv => kv.Value.Any(string.IsNullOrWhiteSpace)).Select(kv => kv.Key);
        Assert.Empty(empty);
    }

    [Fact]
    public void Placeholders_match_across_languages()
    {
        var bad = Loc.AllStrings.Where(kv =>
        {
            var sets = kv.Value.Select(v => string.Join(",", Regex.Matches(v, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x))).Distinct();
            return sets.Count() > 1;
        }).Select(kv => kv.Key);
        Assert.Empty(bad);
    }

    [Fact]
    public void Unknown_key_returns_the_key()
    {
        Assert.Equal("no_such_key", Loc.T("no_such_key"));
    }

    [Fact]
    public void Format_inserts_arguments()
    {
        Assert.Contains("42", Loc.F("episodes_n", 42));
    }
}
