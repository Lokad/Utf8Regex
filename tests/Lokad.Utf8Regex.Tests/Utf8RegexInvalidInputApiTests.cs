using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexInvalidInputApiTests
{
    private static readonly byte[] s_invalidUtf8 = new byte[] { 0x61, 0xE2, 0x28, 0xA1 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstanceMatchApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() => regex.IsMatch(s_invalidUtf8));
        Assert.Throws<ArgumentException>(() => regex.Match(s_invalidUtf8));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailed(s_invalidUtf8));
        Assert.Throws<ArgumentException>(() => regex.Count(s_invalidUtf8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstanceEnumerationApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateMatches(s_invalidUtf8);
            _ = enumerator.MoveNext();
        });

        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateSplits(s_invalidUtf8);
            _ = enumerator.MoveNext();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstanceReplaceApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() => regex.Replace(s_invalidUtf8, "z"));
        Assert.Throws<ArgumentException>(() => regex.ReplaceToString(s_invalidUtf8, "z"));
        Assert.Throws<ArgumentException>(() => regex.Replace(s_invalidUtf8, "z"u8));
    }

    [Fact]
    public void StaticApisRejectInvalidUtf8()
    {
        Assert.Throws<ArgumentException>(() => Utf8Regex.IsMatch(s_invalidUtf8, "abc", RegexOptions.CultureInvariant));
        Assert.Throws<ArgumentException>(() => Utf8Regex.Match(s_invalidUtf8, "abc", RegexOptions.CultureInvariant));
        Assert.Throws<ArgumentException>(() => Utf8Regex.MatchDetailed(s_invalidUtf8, "abc", RegexOptions.CultureInvariant));
        Assert.Throws<ArgumentException>(() => Utf8Regex.Count(s_invalidUtf8, "abc", RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartAtApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() => regex.IsMatchFromUtf16Offset(s_invalidUtf8, 0));
        Assert.Throws<ArgumentException>(() => regex.MatchFromUtf16Offset(s_invalidUtf8, 0));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailedFromUtf16Offset(s_invalidUtf8, 0));
        Assert.Throws<ArgumentException>(() => regex.CountFromUtf16Offset(s_invalidUtf8, 0));
        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateMatchesFromUtf16Offset(s_invalidUtf8, 0);
            _ = enumerator.MoveNext();
        });
    }

    private static Utf8Regex CreateRegex(bool compiled)
        => new("abc", compiled ? RegexOptions.Compiled | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant);
}
