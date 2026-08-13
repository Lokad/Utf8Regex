using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2Utf8BoundaryTests
{
    [Fact]
    public void ValidPrefixCannotHideMalformedBytesInSubjectTail()
    {
        var regex = new Utf8Pcre2Regex("a");
        var input = new byte[] { (byte)'a', (byte)'b', 0xE2, 0x82 };

        Assert.Throws<ArgumentException>(() => regex.IsMatch(input, 1));
        Assert.Throws<ArgumentException>(() => regex.Match(input, 1));
        Assert.Throws<ArgumentException>(() => regex.Count(input, 1));
        Assert.Throws<ArgumentException>(() =>
        {
            _ = regex.EnumerateMatches(input, 1);
        });
    }

    [Fact]
    public void StartOffsetMustBeAScalarBoundary()
    {
        var regex = new Utf8Pcre2Regex(".");
        var input = "a😀b"u8.ToArray();

        for (var offset = 2; offset <= 4; offset++)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, offset));
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(input, offset));
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, offset));
        }
    }

    [Fact]
    public void Utf8PatternAndReplacementSpansRejectMalformedInputBeforeDecoding()
    {
        var invalid = new byte[] { 0xE2, 0x82 };

        Assert.Throws<ArgumentException>(() => new Utf8Pcre2Regex(invalid));

        var regex = new Utf8Pcre2Regex("a");
        Assert.Throws<ArgumentException>(() => regex.Replace("a"u8, invalid));
        Assert.Throws<ArgumentException>(() => regex.TryReplace("a"u8, invalid, new byte[16], out _));
    }
}
