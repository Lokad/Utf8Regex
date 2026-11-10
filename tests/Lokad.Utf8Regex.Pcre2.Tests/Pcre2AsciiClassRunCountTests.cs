using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiClassRunCountTests
{
    [Theory]
    [InlineData(@"\w{10,}", "short words only", 0)]
    [InlineData(@"\w{3,}", "ab abc abcd x", 2)]
    [InlineData(@"[a-z]{2,}", "abc!!def!g", 2)]
    [InlineData(@"[^a]{2,}", "bbaccaad", 2)]
    [InlineData(@"[a\0]{2,}", "a\0!!aa", 2)]
    [InlineData(@"[a-z]*", "ab!", 3)]
    [InlineData(@"[a-z]{2,}?", "abcdef", 3)]
    [InlineData(@"[a-z]{2,3}", "abcdefg", 2)]
    [InlineData(@"(?i)[a-z]{3,}", "ABC xyz", 2)]
    [InlineData(@"\w{3,}", "éabcd😀xyz", 2)]
    public void CompleteClassRunsAndExcludedFormsKeepTheirPublicCounts(string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(expected, regex.Count(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, 0, Pcre2MatchOptions.None, token.Token));
    }

    [Fact]
    public void MemberRunsCrossBlocksAndRespectOffsetsWithoutPublishingRanges()
    {
        var regex = new Utf8Pcre2Regex(@"\w{10,}");
        var text = new string('a', 4095) + " " + new string('b', 9) + " " +
            new string('c', 10) + "\0" + new string('d', 8200);
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, text.Length);
        Assert.Equal(3, regex.Count(input));
        Assert.Equal(2, regex.Count(input, 4095));
        Assert.Equal(0, regex.Count(input, input.Length - 9));
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', 255 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("éabcd"u8, 1));
    }

    [Fact]
    public void UnicodePropertiesAndAnchoringKeepTheirOwnRules()
    {
        var unicode = new Utf8Pcre2Regex(@"\w{3,}", Pcre2CompileOptions.Ucp);
        Assert.Equal(1, unicode.Count("ééé"u8));
        Assert.Equal(2, unicode.Count("abc def"u8));
        var plain = new Utf8Pcre2Regex(@"[a-z]{2,}");
        Assert.Equal(0, plain.Count("0abc xyz"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, plain.Count("abc zz"u8, 0, Pcre2MatchOptions.EndAnchored));
        var anchored = new Utf8Pcre2Regex(@"[a-z]{2,}", Pcre2CompileOptions.Anchored);
        Assert.Equal(0, anchored.Count("0abc xyz"u8));
        var crlf = new Utf8Pcre2Regex(@"[a-z]{2,}", default,
            new() { Newline = Pcre2NewlineConvention.Crlf }, default, Timeout.InfiniteTimeSpan);
        Assert.Equal(2, crlf.Count("\r\nabc def\r\n"u8));
    }
}
