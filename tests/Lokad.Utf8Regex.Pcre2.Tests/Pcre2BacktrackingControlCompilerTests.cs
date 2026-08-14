using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BacktrackingControlCompilerTests
{
    [Theory]
    [InlineData("^(?>ab|a)b$", "ab", false)]
    [InlineData("^(*atomic:ab|a)b$", "ab", false)]
    [InlineData("^(?:ab|a)b$", "ab", true)]
    [InlineData("^a*+a$", "aaa", false)]
    [InlineData("^a*a$", "aaa", true)]
    [InlineData("^(a|)++\\w$", "a", false)]
    [InlineData("^(a|)+\\w$", "a", true)]
    public void AtomicGroupsAndPossessiveRepeatsDiscardOnlyInternalChoices(
        string pattern,
        string input,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void AtomicCaptureStateIsRetainedOnSuccessAndRolledBackOnOuterFailure()
    {
        var success = new Utf8Pcre2Regex("^(?>(a|ab))c$").MatchDetailed("ac"u8);
        var fallback = new Utf8Pcre2Regex("^(?:(?>(a|ab))b|(ab)b)$").MatchDetailed("abb"u8);

        Assert.Equal("a", success.GetGroup(1).GetValueString());
        Assert.True(fallback.Success);
        Assert.False(fallback.GetGroup(1).Success);
        Assert.Equal("ab", fallback.GetGroup(2).GetValueString());
    }

    [Fact]
    public void AcceptSucceedsImmediatelyAtTheCurrentInvocationBoundary()
    {
        var topLevel = new Utf8Pcre2Regex("a(*ACCEPT)b").Match("ac"u8);
        var subroutine = new Utf8Pcre2Regex(
            "^(?&g)c(?(DEFINE)(?<g>a(*ACCEPT)b))$").MatchDetailed("ac"u8);

        Assert.Equal("a", topLevel.GetValueString());
        Assert.True(subroutine.Success);
        Assert.Equal("a", subroutine.GetGroup(1).GetValueString());
    }

    [Fact]
    public void FailForcesImmediateBacktracking()
    {
        var regex = new Utf8Pcre2Regex("^(?:a(*FAIL)b|ac)$");

        Assert.True(regex.IsMatch("ac"u8));
        Assert.False(regex.IsMatch("ab"u8));
    }

    [Fact]
    public void CommitStopsCandidateRestartOnceBacktrackingReachesIt()
    {
        var regex = new Utf8Pcre2Regex("a(*COMMIT)b|ac");

        Assert.False(regex.IsMatch("xac"u8));
        Assert.True(regex.IsMatch("xab"u8));
    }

    [Fact]
    public void PruneFailsOneCandidateButAllowsTheNextCandidate()
    {
        var regex = new Utf8Pcre2Regex("a(*PRUNE)b|c");
        var match = regex.Match("ac"u8);

        Assert.True(match.Success);
        Assert.Equal(1, match.StartOffsetInBytes);
    }

    [Fact]
    public void UnnamedSkipRestartsAtItsSubjectPosition()
    {
        var regex = new Utf8Pcre2Regex("a+(*SKIP)b|a");

        Assert.False(regex.IsMatch("aaa"u8));
        Assert.True(regex.IsMatch("aaab"u8));
    }

    [Fact]
    public void NamedSkipUsesTheMostRecentVisibleMark()
    {
        var regex = new Utf8Pcre2Regex("a(*MARK:X)b(*SKIP:X)(*F)|(.)");
        var match = regex.MatchDetailed("abc"u8);

        Assert.True(match.Success);
        Assert.Equal("b", match.GetValueString());
        Assert.Equal("b", match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void AtomicGroupsHideTheirInternalMarksFromNamedSkip()
    {
        var visible = new Utf8Pcre2Regex("a(?:(*MARK:X))(*SKIP:X)(*F)|(.)").Match("abc"u8);
        var hidden = new Utf8Pcre2Regex("a(?>(*MARK:X))(*SKIP:X)(*F)|(.)").Match("abc"u8);

        Assert.Equal("b", visible.GetValueString());
        Assert.Equal(1, visible.StartOffsetInBytes);
        Assert.Equal("a", hidden.GetValueString());
        Assert.Equal(0, hidden.StartOffsetInBytes);
    }

    [Fact]
    public void ThenMovesToTheNextInnermostAlternative()
    {
        var regex = new Utf8Pcre2Regex("^((a|ab)(*THEN)c|abd)$");

        Assert.True(regex.IsMatch("abd"u8));
        Assert.False(regex.IsMatch("abc"u8));
    }

    [Fact]
    public void MarkFollowsTheSuccessfulPathAndFeedsProbeAndReplacement()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:A)ac|(*MARK:B)(b)");
        var match = regex.MatchDetailed("b"u8);
        var probe = regex.Probe("b"u8, Pcre2PartialMode.Soft);

        Assert.Equal("B", match.Mark);
        Assert.Equal("B", probe.Mark);
        Assert.Equal("<B:b>", regex.ReplaceToString("b"u8, "<${*MARK}:$1>"));
    }

    [Fact]
    public void DetailedFailuresReportTheLastEncounteredMark()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:A)a|b");
        var match = regex.MatchDetailed("c"u8);
        var probe = regex.Probe("c"u8, Pcre2PartialMode.Soft);

        Assert.False(match.Success);
        Assert.Equal("A", match.Mark);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, probe.Kind);
        Assert.Equal("A", probe.Mark);
    }

    [Fact]
    public void SuccessfulAssertionsPropagateTheirMark()
    {
        var match = new Utf8Pcre2Regex("(?=(*MARK:A)a)a").MatchDetailed("a"u8);

        Assert.True(match.Success);
        Assert.Equal("A", match.Mark);
    }

    [Fact]
    public void DeferredControlVerbsStayWithinSubroutineInvocationBoundaries()
    {
        var regex = new Utf8Pcre2Regex("(?1)(A(*COMMIT)|B)D");

        Assert.True(regex.IsMatch("ABAD"u8));
    }

    [Fact]
    public void ControlInstructionsChargeDepthAndHeapLimits()
    {
        var depthLimited = new Utf8Pcre2Regex(
            "(?>(?>a|b)|c)",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { DepthLimit = 1 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        var heapLimited = new Utf8Pcre2Regex(
            "(*MARK:A)a",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);

        Assert.Equal(
            "DepthLimit",
            Assert.Throws<Pcre2MatchException>(() => depthLimited.IsMatch("a"u8)).ErrorKind);
        Assert.Equal(
            "HeapLimit",
            Assert.Throws<Pcre2MatchException>(() => heapLimited.IsMatch("a"u8)).ErrorKind);
    }

    [Fact]
    public void AnalysisReportsGenericControlVerbPrograms()
    {
        var analysis = new Utf8Pcre2Regex("(?>a)(*MARK:A)b++(*PRUNE)").Analyze();

        Assert.True(analysis.IsFullyNative);
        Assert.True(analysis.UsesBacktrackingControlVerbs);
    }

    [Fact]
    public void ControlStateRemainsInvocationLocalUnderConcurrency()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:A)(?>a+)|(*MARK:B)b++");
        Parallel.For(0, 256, index =>
        {
            var input = index % 2 == 0 ? "aaa"u8 : "b"u8;
            var expectedMark = index % 2 == 0 ? "A" : "B";
            Assert.Equal(expectedMark, regex.MatchDetailed(input).Mark);
        });
    }
}
