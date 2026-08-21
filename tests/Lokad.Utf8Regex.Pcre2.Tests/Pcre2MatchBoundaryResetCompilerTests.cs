using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2MatchBoundaryResetCompilerTests
{
    [Fact]
    public void ResetSeparatesConsumedAndReportedStartsWhilePreservingEarlierCaptures()
    {
        var regex = new Utf8Pcre2Regex("([a-z]+)-\\K[0-9]+");
        var match = regex.MatchDetailed("xx name-123 yy"u8);

        Assert.True(match.Success);
        Assert.Equal("123", match.Value.GetValueString());
        Assert.Equal("name", match.GetGroup(1).GetValueString());
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void BacktrackingRestoresTheReportedStartBeforeTryingAnotherAlternative()
    {
        var regex = new Utf8Pcre2Regex("a\\Kbz|ab");
        var match = regex.Match("ab"u8);

        Assert.Equal("ab", match.GetValueString());
        Assert.Equal(0, match.StartOffsetInBytes);
    }

    [Fact]
    public void RepetitionRetainsTheLastSuccessfulReset()
    {
        var match = new Utf8Pcre2Regex("^(?:a\\Kb)+$").Match("abab"u8);

        Assert.True(match.Success);
        Assert.Equal("b", match.GetValueString());
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
            regex.ReplaceToString(input, "<$0/$1/$2>"));
    }

    [Fact]
    public void ResetEmptyMatchesRemainAvailableButRespectNotEmpty()
    {
        var regex = new Utf8Pcre2Regex("^abc\\K");
        var match = regex.Match("abcdef"u8);
        var notEmpty = regex.Match("abcdef"u8, 0, Pcre2MatchOptions.NotEmpty);

        Assert.True(match.Success);
        Assert.Equal(3, match.StartOffsetInBytes);
        Assert.Equal(3, match.EndOffsetInBytes);
        Assert.False(notEmpty.Success);
        Assert.Equal("abc<>def", regex.ReplaceToString("abcdef"u8, "<$0>"));
    }

    [Fact]
    public void DirectLookaroundResetIsRejectedBySyntaxRatherThanPatternShape()
    {
        var exception = Assert.Throws<Pcre2CompileException>(() =>
            new Utf8Pcre2Regex("(?=(?:x|ab)\\K)"));

        Assert.Equal(Pcre2ErrorKinds.LookaroundBackslashKDisabled, exception.ErrorKind);
    }

    [Fact]
    public void OptedInLookaroundResetPreservesRawOneShotOffsetsAndRejectsIteration()
    {
        var regex = new Utf8Pcre2Regex(
            "(?=(?:a|aa)+\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true },
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);
        var match = regex.Match("aa"u8);
        var detailed = regex.MatchDetailed("aa"u8);

        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(0, match.EndOffsetInBytes);
        Assert.Equal(2, detailed.Value.StartOffsetInBytes);
        Assert.Equal(0, detailed.Value.EndOffsetInBytes);
        Assert.Throws<NotSupportedException>(() => regex.Count("aa"u8));
        Assert.Throws<NotSupportedException>(() => regex.EnumerateMatches("aa"u8));
        Assert.Throws<NotSupportedException>(() => regex.Replace("aa"u8, "x"));

        var destination = new Utf8Pcre2MatchData[1];
        Assert.Throws<NotSupportedException>(() => regex.MatchMany("aa"u8, destination, out _));
    }

    [Fact]
    public void AnalysisUsesGenericResetSemantics()
    {
        var regex = new Utf8Pcre2Regex(
            "(?=(?:a|aa)+\\K)",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowLookaroundBackslashK = true },
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);
        var analysis = regex.Analyze();

        Assert.True(analysis.IsFullyNative);
        Assert.True(analysis.MayReportNonMonotoneMatchOffsets);
        Assert.True(analysis.RejectsNonMonotoneIterativeMatches);
    }

    [Fact]
    public void ResetStateRemainsInvocationLocalUnderConcurrency()
    {
        var regex = new Utf8Pcre2Regex("(?:foo|bar)-\\K[0-9]+");
        Parallel.For(0, 256, index =>
        {
            var input = index % 2 == 0 ? "foo-12"u8 : "bar-345"u8;
            var expected = index % 2 == 0 ? "12" : "345";
            Assert.Equal(expected, regex.Match(input).GetValueString());
        });
    }
}
