using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexDefaultOptionsTests
{
    [Fact]
    public void DefaultConstructorUsesCultureInvariant()
    {
        var regex = new Utf8Regex("abc");

        Assert.Equal(RegexOptions.CultureInvariant, regex.Options);
    }

    [Fact]
    public void StaticHelpersDefaultToCultureInvariant()
    {
        Assert.True(Utf8Regex.IsMatch("xxabcxx"u8, "abc"));

        var match = Utf8Regex.Match("xxabcxx"u8, "abc");
        Assert.True(match.Success);

        var detailed = Utf8Regex.MatchDetailed("xxabcxx"u8, "(abc)");
        Assert.True(detailed.Success);

        var count = Utf8Regex.Count("abcabc"u8, "abc");
        Assert.Equal(2, count);
    }
}
