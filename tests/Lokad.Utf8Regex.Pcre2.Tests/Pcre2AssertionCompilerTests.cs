using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2AssertionCompilerTests
{
    private const string ImpliedLookahead = "(?=(?<token>[a-z]{3}[0-9]{3}))abc123";

    [Theory]
    [InlineData("(?=(?<token>[a-z]{4}[0-9]{2}))abc123", "abc123", false)]
    [InlineData("(?=(?<token>abc123x))abc123", "abc123", false)]
    [InlineData("(?=(?<token>abc123x))abc123", "abc123x", true)]
    [InlineData("(?=(?<token>[a-z]{3}))\\k<token>!", "abc!", true)]
    [InlineData("(?=(?<token>[a-z]{3}))\\k<token>!", "ab!", false)]
    public void UnprovedLookaheadKeepsItsOwnConstraint(string pattern, string input, bool expected)
    {
        using var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImpliedLookaheadSearchObservesOriginalInterruption(bool expire)
    {
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var regex = new Utf8Pcre2Regex(ImpliedLookahead, policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan,
        });
        var input = new byte[256 * 1024];
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.IsMatch(input, cancellationToken: cancellation.Token));
            if (expire) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData("foo(?!bar)", "xxfoobaz", "foo")]
    [InlineData("(?<=abc)def", "xxabcdef", "def")]
    [InlineData("(?<!abc)def", "xxzzzdef", "def")]
    [InlineData("(?=(?=ab))ab", "xxab", "ab")]
    public void LookaroundsComposeThroughSlice(string pattern, string input, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        // R03 lookaround slice: fixed-shape assertions run first-match on the shared slice runner instead of the generic VM.
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("foo(?=bar)", "xxfoobar", "foo")]
    // The transparent lookahead body now rides the slice, so the VM-routing row spells its group Python-style (the core slice declines it).
    [InlineData("(?=(?P<g>ab|a))a", "xxab", "a")]
    [InlineData("(?<=(a)(?(1)b|c))d", "abd", "d")]
    public void NonSliceLookaroundsStayInTheGenericProgram(string pattern, string input, string expected)
    {
        // Positive lookahead behind a leading literal keeps native precedence, and the Python-spelled body declines the core slice: both stay on the generic VM.
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void LookbehindUsesScalarLengthsAndPreservesUtf8Coordinates()
    {
        var regex = new Utf8Pcre2Regex("(?<=é{1,3})😀");
        var input = Encoding.UTF8.GetBytes("xéé😀z");

        var match = regex.Match(input);

        Assert.True(match.Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(5, match.StartOffsetInBytes);
        Assert.Equal(9, match.EndOffsetInBytes);
    }

    [Theory]
    [InlineData("xéé😀", 5, 9)]
    [InlineData("xβ!", 3, 4)]
    public void ConditionalLookbehindKeepsItsScalarBounds(string text, int start, int end)
    {
        using var regex = new Utf8Pcre2Regex("(?(?<=é{1,2})😀|!)");
        var match = regex.Match(Encoding.UTF8.GetBytes(text), startOffsetInBytes: start,
            matchOptions: Pcre2MatchOptions.Anchored);

        Assert.True(match.Success);
        Assert.Equal((start, end), (match.StartOffsetInBytes, match.EndOffsetInBytes));
    }

    [Fact]
    public void PositiveAssertionCapturesFlowToBackreferencesAndCallerSlots()
    {
        var lookahead1Regex = new Utf8Pcre2Regex("(?=(?<word>[a-z]+))\\k<word>");
        ReadOnlySpan<byte> lookahead1Input = "abc!"u8;
        var lookahead1Captures = new Utf8Pcre2CaptureRange[lookahead1Regex.CaptureGroupCount + 1];
        var lookahead = lookahead1Regex.Match(lookahead1Input, captures: lookahead1Captures);
        var lookbehind2Regex = new Utf8Pcre2Regex("(?<=(a|b))\\1");
        ReadOnlySpan<byte> lookbehind2Input = "xaa"u8;
        var lookbehind2Captures = new Utf8Pcre2CaptureRange[lookbehind2Regex.CaptureGroupCount + 1];
        var lookbehind = lookbehind2Regex.Match(lookbehind2Input, captures: lookbehind2Captures);
        var outerToAssertion = new Utf8Pcre2Regex("(a)(?=\\1b)\\1b").Match("aab"u8);

        Assert.True(lookahead.Success);
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(lookahead.GetValueBytes()));
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString((lookahead1Captures[1].Success ? lookahead1Input.Slice(lookahead1Captures[1].StartOffsetInBytes, lookahead1Captures[1].EndOffsetInBytes - lookahead1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));

        Assert.True(lookbehind.Success);
        Assert.Equal(2, lookbehind.StartOffsetInBytes);
        Assert.Equal(1, lookbehind2Captures[1].StartOffsetInBytes);

        Assert.True(outerToAssertion.Success);
        Assert.Equal("aab", System.Text.Encoding.UTF8.GetString(outerToAssertion.GetValueBytes()));
    }

    [Fact]
    public void NegativeAssertionDiscardsCapturesFromItsFailedProbe()
    {
        var match4Regex = new Utf8Pcre2Regex("(?!(a))b");
        ReadOnlySpan<byte> match4Input = "b"u8;
        var match4Captures = new Utf8Pcre2CaptureRange[match4Regex.CaptureGroupCount + 1];
        var match = match4Regex.Match(match4Input, captures: match4Captures);

        Assert.True(match.Success);
        Assert.False(match4Captures[1].Success);
    }

    [Theory]
    [InlineData("(?i:ab)(?-i:CD)", "AbCD", true)]
    [InlineData("(?i:ab)(?-i:CD)", "AbCd", false)]
    [InlineData("(?s:a.b)", "a\nb", true)]
    [InlineData("(?-s:a.b)", "a\nb", false)]
    public void ScopedOptionsAreLoweredIntoTheirContainedTokens(string pattern, string input, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Equal(expected, regex.IsMatch(bytes));
        Assert.Equal(expected, regex.Match(bytes).Success);
    }

    [Fact]
    public void ScopedNoAutoCaptureAndUngreedyRestoreAtGroupExit()
    {
        var noAutoCapture5Regex = new Utf8Pcre2Regex("(?n:(a))(?<x>b)");
        ReadOnlySpan<byte> noAutoCapture5Input = "ab"u8;
        var noAutoCapture5Captures = new Utf8Pcre2CaptureRange[noAutoCapture5Regex.CaptureGroupCount + 1];
        var noAutoCapture = noAutoCapture5Regex.Match(noAutoCapture5Input, captures: noAutoCapture5Captures);
        var ungreedy6Regex = new Utf8Pcre2Regex("(?U:a+)(a+)");
        ReadOnlySpan<byte> ungreedy6Input = "aaaa"u8;
        var ungreedy6Captures = new Utf8Pcre2CaptureRange[ungreedy6Regex.CaptureGroupCount + 1];
        var ungreedy = ungreedy6Regex.Match(ungreedy6Input, captures: ungreedy6Captures);

        Assert.True(noAutoCapture.Success);
        Assert.Equal(2, (noAutoCapture.Success ? noAutoCapture5Captures.Length : 0));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((noAutoCapture5Captures[1].Success ? noAutoCapture5Input.Slice(noAutoCapture5Captures[1].StartOffsetInBytes, noAutoCapture5Captures[1].EndOffsetInBytes - noAutoCapture5Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));

        Assert.True(ungreedy.Success);
        Assert.Equal("aaa", System.Text.Encoding.UTF8.GetString((ungreedy6Captures[1].Success ? ungreedy6Input.Slice(ungreedy6Captures[1].StartOffsetInBytes, ungreedy6Captures[1].EndOffsetInBytes - ungreedy6Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void ZeroWidthLookaheadUsesTheCanonicalGlobalProgressRule()
    {
        var regex = new Utf8Pcre2Regex("(?=a)");

        Assert.Equal(3, regex.Count("aaa"u8));
        Assert.Equal("|a|a|a", Encoding.UTF8.GetString(regex.Replace("aaa"u8, "|"u8)));
    }

    [Fact]
    public void AssertionCapturesParticipateInGlobalReplacement()
    {
        var regex = new Utf8Pcre2Regex("(?=(?<x>a))a");

        Assert.Equal("[a] [a]", Encoding.UTF8.GetString(regex.Replace("a a"u8, "[${x}]"u8)));
    }

    [Fact]
    public void ProbeReturnsGenericAssertionCapturesForAFullMatch()
    {
        var regex = new Utf8Pcre2Regex("(?=(?<x>a))a");

        var probe = regex.Probe("a"u8, Pcre2PartialMode.Soft);

        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(probe.GetMatch().GetGroup(1).GetValueBytes()));
    }

    [Fact]
    public void AssertionCandidateSearchHonorsStartOffsetsAndNegativeContext()
    {
        var regex = new Utf8Pcre2Regex("(?<!x)(?=needle)needle");
        var input = "xneedle needle"u8;

        var match = regex.Match(input, startOffsetInBytes: 1);

        Assert.True(match.Success);
        Assert.Equal(8, match.StartOffsetInBytes);
    }

    [Fact]
    public void WarmedCaptureFreeLookaheadIsMatchDoesNotAllocate()
    {
        // Earlier tests can fill the retention allowance with other buffer sizes.
        // Warm this VM shape from an empty pool in the isolated collection.
        Utf8ExactStatePool.Clear();
        try
        {
            using var regex = new Utf8Pcre2Regex("(?=(?:ab|ac)++)a");
            Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
            // Warm the measured call site through delayed tiering.
            var warmup = Stopwatch.StartNew();
            do { _ = MeasureLookaheadBatch(regex, out _); }
            while (warmup.ElapsedMilliseconds < 1_000);

            var allocated = MeasureLookaheadBatch(regex, out var matches);
            Assert.Equal(256, matches);
            Assert.Equal(0, allocated);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureLookaheadBatch(Utf8Pcre2Regex regex, out int matches)
    {
        var input = "xxab"u8;
        matches = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
        {
            if (regex.IsMatch(input)) matches++;
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void AssertionsShareTheInvocationLimitsAndRemainConcurrent()
    {
        // Both repeat branches remain viable; the assertion still needs a
        // pending alternative in addition to its own logical depth.
        var limited = new Utf8Pcre2Regex("(?=(a+|a+b))a", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1
            },
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });
        Assert.Equal(
            Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("a"u8)).ErrorKind);

        var regex = new Utf8Pcre2Regex("(?=(?<x>a|b))\\k<x>");
        Parallel.For(0, 256, index =>
        {
            var input = index % 2 == 0 ? "a"u8 : "b"u8;
            ReadOnlySpan<byte> match7Input = input;
            var match7Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var match = regex.Match(match7Input, captures: match7Captures);
            Assert.True(match.Success);
            Assert.Equal(0, match7Captures[1].StartOffsetInBytes);
        });
    }
}
