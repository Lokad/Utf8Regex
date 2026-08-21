using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2AssertionCompilerTests
{
    [Theory]
    [InlineData("foo(?=bar)", "xxfoobar", "foo")]
    [InlineData("foo(?!bar)", "xxfoobaz", "foo")]
    [InlineData("(?<=abc)def", "xxabcdef", "def")]
    [InlineData("(?<!abc)def", "xxzzzdef", "def")]
    [InlineData("(?=(?:ab|a))a", "xxab", "a")]
    [InlineData("(?=(?=ab))ab", "xxab", "ab")]
    public void LookaroundsComposeInTheGenericProgram(string pattern, string input, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void LookbehindUsesScalarLengthsAndPreservesUtf8Coordinates()
    {
        var regex = new Utf8Pcre2Regex("(?<=é{1,3})😀");
        var input = Encoding.UTF8.GetBytes("xéé😀z");

        var match = regex.Match(input);

        Assert.True(match.Success);
        Assert.Equal("😀", match.GetValueString());
        Assert.Equal(5, match.StartOffsetInBytes);
        Assert.Equal(9, match.EndOffsetInBytes);
        Assert.Equal(3, match.StartOffsetInUtf16);
        Assert.Equal(5, match.EndOffsetInUtf16);
    }

    [Fact]
    public void PositiveAssertionCapturesFlowToBackreferencesAndDetailedResults()
    {
        var lookahead = new Utf8Pcre2Regex("(?=(?<word>[a-z]+))\\k<word>")
            .MatchDetailed("abc!"u8);
        var lookbehind = new Utf8Pcre2Regex("(?<=(a|b))\\1")
            .MatchDetailed("xaa"u8);
        var outerToAssertion = new Utf8Pcre2Regex("(a)(?=\\1b)\\1b")
            .MatchDetailed("aab"u8);

        Assert.True(lookahead.Success);
        Assert.Equal("abc", lookahead.GetValueString());
        Assert.Equal("abc", lookahead.GetGroup(1).GetValueString());

        Assert.True(lookbehind.Success);
        Assert.Equal(2, lookbehind.Value.StartOffsetInBytes);
        Assert.Equal(1, lookbehind.GetGroup(1).StartOffsetInBytes);

        Assert.True(outerToAssertion.Success);
        Assert.Equal("aab", outerToAssertion.GetValueString());
    }

    [Fact]
    public void NegativeAssertionDiscardsCapturesFromItsFailedProbe()
    {
        var match = new Utf8Pcre2Regex("(?!(a))b").MatchDetailed("b"u8);

        Assert.True(match.Success);
        Assert.False(match.GetGroup(1).Success);
    }

    [Theory]
    [InlineData("(?i:ab)(?-i:CD)", "AbCD", true)]
    [InlineData("(?i:ab)(?-i:CD)", "AbCd", false)]
    [InlineData("(?s:a.b)", "a\nb", true)]
    [InlineData("(?-s:a.b)", "a\nb", false)]
    public void ScopedOptionsAreLoweredIntoTheirContainedTokens(string pattern, string input, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void ScopedNoAutoCaptureAndUngreedyRestoreAtGroupExit()
    {
        var noAutoCapture = new Utf8Pcre2Regex("(?n:(a))(?<x>b)").MatchDetailed("ab"u8);
        var ungreedy = new Utf8Pcre2Regex("(?U:a+)(a+)").MatchDetailed("aaaa"u8);

        Assert.True(noAutoCapture.Success);
        Assert.Equal(2, noAutoCapture.CaptureSlotCount);
        Assert.Equal("b", noAutoCapture.GetGroup(1).GetValueString());

        Assert.True(ungreedy.Success);
        Assert.Equal("aaa", ungreedy.GetGroup(1).GetValueString());
    }

    [Fact]
    public void ZeroWidthLookaheadUsesTheCanonicalGlobalProgressRule()
    {
        var regex = new Utf8Pcre2Regex("(?=a)");

        Assert.Equal(3, regex.Count("aaa"u8));
        Assert.Equal("|a|a|a", Encoding.UTF8.GetString(regex.Replace("aaa"u8, "|")));
    }

    [Fact]
    public void AssertionCapturesParticipateInGlobalReplacement()
    {
        var regex = new Utf8Pcre2Regex("(?=(?<x>a))a");

        Assert.Equal("[a] [a]", Encoding.UTF8.GetString(regex.Replace("a a"u8, "[${x}]")));
    }

    [Fact]
    public void ProbeReturnsGenericAssertionCapturesForAFullMatch()
    {
        var regex = new Utf8Pcre2Regex("(?=(?<x>a))a");

        var probe = regex.Probe("a"u8, Pcre2PartialMode.Soft);

        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        Assert.Equal("a", probe.GetMatch().GetGroup(1).GetValueString());
    }

    [Fact]
    public void AssertionCandidateSearchHonorsStartOffsetsAndNegativeContext()
    {
        var regex = new Utf8Pcre2Regex("(?<!x)(?=needle)needle");
        var input = "xneedle needle"u8;

        var match = regex.Match(input, 1);

        Assert.True(match.Success);
        Assert.Equal(8, match.StartOffsetInBytes);
    }

    [Fact]
    public void WarmedCaptureFreeLookaheadIsMatchDoesNotAllocate()
    {
        var regex = new Utf8Pcre2Regex("(?=ab|ac)ab");
        var input = "xxab"u8;
        for (var index = 0; index < 32; index++)
        {
            Assert.True(regex.IsMatch(input));
        }

        for (var index = 0; index < 256; index++)
        {
            Assert.True(regex.IsMatch(input));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
        {
            Assert.True(regex.IsMatch(input));
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void AssertionsShareTheInvocationLimitsAndRemainConcurrent()
    {
        var limited = new Utf8Pcre2Regex(
            "(?=(a|b))a",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { DepthLimit = 1 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        Assert.Equal(
            Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("a"u8)).ErrorKind);

        var regex = new Utf8Pcre2Regex("(?=(?<x>a|b))\\k<x>");
        Parallel.For(0, 256, index =>
        {
            var input = index % 2 == 0 ? "a"u8 : "b"u8;
            var match = regex.MatchDetailed(input);
            Assert.True(match.Success);
            Assert.Equal(0, match.GetGroup(1).StartOffsetInBytes);
        });
    }
}
