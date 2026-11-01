using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiWordBoundaryTests
{
    [Theory]
    [InlineData(Pcre2CompileOptions.None)]
    [InlineData(Pcre2CompileOptions.Ucp)]
    public void PublicAnchoredBoundaryAgreesWithAsciiWordAlphabet(Pcre2CompileOptions options)
    {
        var boundary = new Utf8Pcre2Regex(@"\b", options);
        var nonBoundary = new Utf8Pcre2Regex(@"\B", options);
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";
        byte[] pair = new byte[2];
        for (var left = 0; left < 128; left++)
        for (var right = 0; right < 128; right++)
        {
            pair[0] = (byte)left; pair[1] = (byte)right;
            var expected = alphabet.Contains((char)left) != alphabet.Contains((char)right);
            Assert.Equal(expected, boundary.IsMatch(pair, 1, Pcre2MatchOptions.Anchored));
            Assert.Equal(!expected, nonBoundary.IsMatch(pair, 1, Pcre2MatchOptions.Anchored));
        }
        Assert.False(boundary.IsMatch([])); Assert.True(nonBoundary.IsMatch([]));
        Assert.True(boundary.IsMatch("a"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.True(boundary.IsMatch("a"u8, 1, Pcre2MatchOptions.Anchored));
        Assert.False(boundary.IsMatch("!"u8));
    }

    [Theory]
    [InlineData("éabc!", true, false)]
    [InlineData("\u0301abc!", true, false)]
    [InlineData("١abc!", true, false)]
    [InlineData("\U00010400abc!", true, false)]
    [InlineData("😀abc!", true, true)]
    [InlineData("\u200Dabc\u200D", true, true)]
    [InlineData("\u200Cabc\u200C", true, true)]
    public void UnicodeMembershipAndRetainedCapturesKeepPcre2Policy(string text, bool asciiExpected, bool ucpExpected)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { Pcre2CompileOptions.None, Pcre2CompileOptions.Ucp })
        {
            var regex = new Utf8Pcre2Regex(@"\b(?<word>abc)\b", options);
            var expected = options == Pcre2CompileOptions.None ? asciiExpected : ucpExpected;
            var startOfWord = bytes.AsSpan().IndexOf("abc"u8);
            var retained = regex.MatchDetailed(bytes);
            Assert.Equal(expected, retained.Success);
            Assert.Equal(expected ? 1 : 0, regex.Count(bytes));
            for (var offset = 0; offset <= bytes.Length; offset++)
            {
                if (offset < bytes.Length && (bytes[offset] & 0xc0) == 0x80) continue;
                Assert.Equal(expected && offset == startOfWord,
                    regex.IsMatch(bytes, offset, Pcre2MatchOptions.Anchored));
            }
            _ = regex.IsMatch("other"u8);
            if (expected) Assert.Equal("abc", retained.GetGroup(1).GetValueString());
        }
    }

    [Theory]
    [InlineData(Pcre2CompileOptions.None, 4)]
    [InlineData(Pcre2CompileOptions.Ucp, 6)]
    public void EmptyProgressionUsesScalarPositions(Pcre2CompileOptions options, int expected)
    {
        var boundary = new Utf8Pcre2Regex(@"\b", options);
        var nonBoundary = new Utf8Pcre2Regex(@"\B", options);
        var input = "a é _\0😀"u8.ToArray();
        Assert.Equal(expected, boundary.Count(input));
        Assert.Equal(8 - expected, nonBoundary.Count(input));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProductionHelperStillRejectsInvalidOffsets(bool ucp)
    {
        var bytes = "éa"u8.ToArray();
        Assert.Throws<InvalidOperationException>(() => Pcre2CharacterSemantics.IsWordBoundary(bytes, 1, ucp));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pcre2CharacterSemantics.IsWordBoundary(bytes, -1, ucp));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pcre2CharacterSemantics.IsWordBoundary(bytes, bytes.Length + 1, ucp));
    }

    [Fact]
    public void ValidationOriginalTokenAndFiniteAdmissionRemainAuthoritative()
    {
        var regex = new Utf8Pcre2Regex(@"\babc\b");
        byte[] invalid = [.. "abc!"u8, 0xff];
        Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(invalid, 0, Pcre2MatchOptions.None, canceled.Token));
        Assert.Equal(canceled.Token, failure.CancellationToken);
        var limited = new Utf8Pcre2Regex(@"\babc\b", Pcre2CompileOptions.None, default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("abc!"u8)).ErrorKind);
    }
}
