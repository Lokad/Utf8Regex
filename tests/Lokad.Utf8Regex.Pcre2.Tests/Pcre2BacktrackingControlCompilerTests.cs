using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BacktrackingControlCompilerTests
{
    [Theory]
    [InlineData("^(*atomic:ab|a)b$", "ab", false)]
    [InlineData("^(?:ab|a)b$", "ab", true)]
    [InlineData("^a*+a$", "aaa", false)]
    [InlineData("^(a|)++\\w$", "a", false)]
    [InlineData("^(a|)+\\w$", "a", true)]
    public void AtomicGroupsAndPossessiveRepeatsDiscardOnlyInternalChoices(
        string pattern,
        string input,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Equal(expected, regex.IsMatch(bytes));
        Assert.Equal(expected, regex.Match(bytes).Success);
    }

    // R03 atomic slice: the core slice owns possessive groups, so the atomic
    // corner rides the shared slice runner with identical discarding behavior.
    [Fact]
    public void AtomicGroupsDiscardInternalChoicesOnSlice()
    {
        var regex = new Utf8Pcre2Regex("^(?>ab|a)b$");

        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("ab")));
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    // R03 anchor slice: the plain greedy-star control row keeps its
    // keep-choices result on the shared slice runner.
    [Theory]
    [InlineData("^a*a$", "aaa", true)]
    public void PlainGreedyStarKeepsChoicesOnSlice(
        string pattern,
        string input,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void AtomicCaptureStateIsRetainedOnSuccessAndRolledBackOnOuterFailure()
    {
        var success = new Utf8Pcre2Regex("^(?>(a|ab))c$").MatchDetailed("ac"u8);
        var fallback = new Utf8Pcre2Regex("^(?:(?>(a|ab))b|(ab)b)$").MatchDetailed("abb"u8);

        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(success.GetGroup(1).GetValueBytes()));
        Assert.True(fallback.Success);
        Assert.False(fallback.GetGroup(1).Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(fallback.GetGroup(2).GetValueBytes()));
    }

    [Fact]
    public void AcceptSucceedsImmediatelyAtTheCurrentInvocationBoundary()
    {
        var topLevel = new Utf8Pcre2Regex("a(*ACCEPT)b").Match("ac"u8);
        var subroutine = new Utf8Pcre2Regex(            "^(?&g)c(?(DEFINE)(?<g>a(*ACCEPT)b))$").MatchDetailed("ac"u8);

        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(topLevel.GetValueBytes()));
        Assert.True(subroutine.Success);
        Assert.Equal("ac", System.Text.Encoding.UTF8.GetString(subroutine.Value.GetValueBytes()));
        Assert.False(subroutine.GetGroup(1).Success);
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
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(match.Value.GetValueBytes()));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(match.GetGroup(1).GetValueBytes()));
    }

    [Fact]
    public void AtomicGroupsHideTheirInternalMarksFromNamedSkip()
    {
        var visible = new Utf8Pcre2Regex("a(?:(*MARK:X))(*SKIP:X)(*F)|(.)").Match("abc"u8);
        var hidden = new Utf8Pcre2Regex("a(?>(*MARK:X))(*SKIP:X)(*F)|(.)").Match("abc"u8);

        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(visible.GetValueBytes()));
        Assert.Equal(1, visible.StartOffsetInBytes);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(hidden.GetValueBytes()));
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
        var match = regex.Match("b"u8);
        var probe = regex.Probe("b"u8, Pcre2PartialMode.Soft);

        Assert.Equal("B", match.Mark);
        Assert.Equal("B", probe.Mark);
        Assert.Equal("<B:b>", System.Text.Encoding.UTF8.GetString(regex.Replace("b"u8, "<${*MARK}:$1>")));
    }

    [Fact]
    public void PrimaryFailuresReportTheLastEncounteredMark()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:A)a|b");
        var match = regex.Match("c"u8);
        var probe = regex.Probe("c"u8, Pcre2PartialMode.None);

        Assert.False(match.Success);
        Assert.Equal("A", match.Mark);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, probe.Kind);
        Assert.Equal("A", probe.Mark);
        // An ordinary marked miss cannot establish a partial-mode answer.
        Assert.Throws<NotSupportedException>(() =>
        { _ = regex.Probe("c"u8, Pcre2PartialMode.Soft); });
    }

    [Fact]
    public void SuccessfulAssertionsPropagateTheirMark()
    {
        var match = new Utf8Pcre2Regex("(?=(*MARK:A)a)a").Match("a"u8);

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
        var depthLimited = new Utf8Pcre2Regex(            "(?>(?>a|b)|c)", options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1
            },
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });
        var heapLimited = new Utf8Pcre2Regex(            "(*MARK:A)a", options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1
            },
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });

        Assert.Equal(
            Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => depthLimited.IsMatch("a"u8)).ErrorKind);
        Assert.Equal(
            Pcre2ErrorKind.HeapLimit,
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
            Assert.Equal(expectedMark, regex.Match(input).Mark);
        });
    }
}
