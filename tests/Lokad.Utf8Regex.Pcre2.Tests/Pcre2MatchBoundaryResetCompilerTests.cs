using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2MatchBoundaryResetCompilerTests
{
    [Fact]
    public void ResetSeparatesConsumedAndReportedStartsWhilePreservingEarlierCaptures()
    {
        var regex = new Utf8Pcre2Regex("([a-z]+)-\\K[0-9]+");
        ReadOnlySpan<byte> match1Input = "xx name-123 yy"u8;
        var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match1Input, captures: match1Captures);

        Assert.True(match.Success);
        Assert.Equal("123", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal("name", System.Text.Encoding.UTF8.GetString((match1Captures[1].Success ? match1Input.Slice(match1Captures[1].StartOffsetInBytes, match1Captures[1].EndOffsetInBytes - match1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void BacktrackingRestoresTheReportedStartBeforeTryingAnotherAlternative()
    {
        var regex = new Utf8Pcre2Regex("a\\Kbz|ab");
        var match = regex.Match("ab"u8);

        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(0, match.StartOffsetInBytes);
    }

    [Fact]
    public void RepetitionRetainsTheLastSuccessfulReset()
    {
        var match = new Utf8Pcre2Regex("^(?:a\\Kb)+$").Match("abab"u8);

        Assert.True(match.Success);
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(3, match.StartOffsetInBytes);
        Assert.Equal(4, match.EndOffsetInBytes);
    }

    [Fact]
    public void GenericGlobalReplacementUsesConsumedProgressAndReportedSpans()
    {
        var regex = new Utf8Pcre2Regex("([a-z]+)-\\K([0-9]+)");
        var input = "a-12 b-345"u8;

        Assert.Equal(2, regex.Count(input));
        Assert.Equal(
            "a-<12/a/12> b-<345/b/345>",
            System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$0/$1/$2>")));
    }

    [Fact]
    public void ResetEmptyMatchesRemainAvailableButRespectNotEmpty()
    {
        var regex = new Utf8Pcre2Regex("^abc\\K");
        var match = regex.Match("abcdef"u8);
        var notEmpty = regex.Match("abcdef"u8, matchOptions: Pcre2MatchOptions.NotEmpty);

        Assert.True(match.Success);
        Assert.Equal(3, match.StartOffsetInBytes);
        Assert.Equal(3, match.EndOffsetInBytes);
        Assert.False(notEmpty.Success);
        Assert.Equal("abc<>def", System.Text.Encoding.UTF8.GetString(regex.Replace("abcdef"u8, "<$0>")));
    }

    [Fact]
    public void DirectLookaroundResetIsRejectedBySyntaxRatherThanPatternShape()
    {
        var exception = Assert.Throws<Pcre2CompileException>(() =>
            new Utf8Pcre2Regex("(?=(?:x|ab)\\K)"));

        Assert.Equal(Pcre2ErrorKind.LookaroundBackslashKDisabled, exception.ErrorKind);
    }

    [Fact]
    public void OptedInLookaroundResetPreservesRawOneShotOffsetsAndRejectsIteration()
    {
        var regex = new Utf8Pcre2Regex("(?=(?:a|aa)+\\K)", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true }, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        var match = regex.Match("aa"u8);
        var detailed = regex.Match("aa"u8);

        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(0, match.EndOffsetInBytes);
        Assert.Equal(2, detailed.StartOffsetInBytes);
        Assert.Equal(0, detailed.EndOffsetInBytes);
        Assert.Throws<NotSupportedException>(() => regex.Count("aa"u8));
        Assert.Throws<NotSupportedException>(() => regex.EnumerateMatches("aa"u8));
        Assert.Throws<NotSupportedException>(() => regex.Replace("aa"u8, "x"));


    }

    [Fact]
    public void ResetStateRemainsInvocationLocalUnderConcurrency()
    {
        var regex = new Utf8Pcre2Regex("(?:foo|bar)-\\K[0-9]+");
        Parallel.For(0, 256, index =>
        {
            var input = index % 2 == 0 ? "foo-12"u8 : "bar-345"u8;
            var expected = index % 2 == 0 ? "12" : "345";
            Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(regex.Match(input).GetValueBytes()));
        });
    }
}
