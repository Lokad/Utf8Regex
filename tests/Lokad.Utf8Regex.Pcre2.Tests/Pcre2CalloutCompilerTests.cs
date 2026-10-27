using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// callout rejection used a raw (?C substring scan, so callout-looking text inside a class, a quoted literal, a comment, or behind an escape was rejected although native PCRE2 reads it literally. The same audit covers the sibling raw scans and the compatible-backend routing.
public sealed class Pcre2CalloutCompilerTests
{
    [Fact]
    public void CalloutTextInCharacterClassMatchesLiterally()
    {
        var match = new Utf8Pcre2Regex("[(?C]").Match("x?y"u8);

        Assert.True(match.Success);
        Assert.Equal("?", match.GetValueString());
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(2, match.EndOffsetInBytes);
    }

    [Fact]
    public void CalloutTextInQuotedLiteralMatchesLiterally()
    {
        var match = new Utf8Pcre2Regex("\\Q(?C\\E").Match("a(?Cb"u8);

        Assert.True(match.Success);
        Assert.Equal("(?C", match.GetValueString());
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(4, match.EndOffsetInBytes);
    }

    [Fact]
    public void CalloutTextInCommentIsIgnored()
    {
        var match = new Utf8Pcre2Regex("(?#(?C)ab").Match("xxab"u8);

        Assert.True(match.Success);
        Assert.Equal("ab", match.GetValueString());
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(4, match.EndOffsetInBytes);
    }

    [Fact]
    public void CalloutTextBehindEscapesMatchesLiterally()
    {
        var match = new Utf8Pcre2Regex("\\(\\?C\\)").Match("a(?C)b"u8);

        Assert.True(match.Success);
        Assert.Equal("(?C)", match.GetValueString());
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(5, match.EndOffsetInBytes);
    }

    [Theory]
    [InlineData("a(?C)b")]
    [InlineData("a(?C1)b")]
    public void RealCalloutsStayRejected(string pattern)
    {
        var exception = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern));

        Assert.Equal(Pcre2ErrorKind.CalloutUnsupported, exception.ErrorKind);
    }

    [Theory]
    [InlineData("[{5,4}]", "a5b", "5", 1, 2)]
    [InlineData("\\Q{5,4}\\E", "x{5,4}y", "{5,4}", 1, 6)]
    [InlineData("a\\{5,4\\}", "a{5,4}", "a{5,4}", 0, 6)]
    [InlineData("[{65536}]", "a6b", "6", 1, 2)]
    [InlineData("[(?X)]", "aXb", "X", 1, 2)]
    [InlineData("\\Q(?X)\\E", "(?X)", "(?X)", 0, 4)]
    [InlineData("(?#(?z)ab", "xxab", "ab", 2, 4)]
    [InlineData("\\\\idef", "\\idef", "\\idef", 0, 5)]
    public void RawScanSentinelsInLiteralContextMatchLiterally(string pattern, string input, string expectedValue, int expectedStart, int expectedEnd)
    {
        var match = new Utf8Pcre2Regex(pattern).Match(System.Text.Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(expectedValue, match.GetValueString());
        Assert.Equal(expectedStart, match.StartOffsetInBytes);
        Assert.Equal(expectedEnd, match.EndOffsetInBytes);
    }

    [Fact]
    public void QuotedResetInLookaroundIsLiteral()
    {
        var match = new Utf8Pcre2Regex("(?=x\\Q\\K\\E)x\\K").Match("x\\K"u8);

        Assert.True(match.Success);
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(1, match.EndOffsetInBytes);
    }

    [Fact]
    public void UnrecognizedEscapeInClassStaysRejected()
    {
        var exception = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex("[\\idef]"));

        Assert.Equal(Pcre2ErrorKind.UnrecognizedEscape, exception.ErrorKind);
    }
}
