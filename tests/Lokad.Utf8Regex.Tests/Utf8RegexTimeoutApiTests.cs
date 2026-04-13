using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexTimeoutApiTests
{
    [Fact]
    public void StaticHelpersUseDefaultTimeoutWithoutExplicitParameter()
    {
        var match = Utf8Regex.Match("abc"u8, "abc");

        Assert.True(match.Success);
    }

    [Fact]
    public void StaticHelpersDoNotTreatZeroTimeoutAsDefaultSentinel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Utf8Regex.IsMatch("abc"u8, "abc", RegexOptions.CultureInvariant, TimeSpan.Zero));
    }
}
