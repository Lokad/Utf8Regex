using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BoundedDotCandidateTests
{
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 20000000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Theory]
    [InlineData("Watson")]
    [InlineData("W\nend")]
    [InlineData("Wé")]
    public void TerminalLiteralChoosesTheLastScalarEndpointAndKeepsCaptureEndings(string tail)
    {
        var literal = Regex.Escape(tail);
        foreach (var pattern in new[] { @"\AA(?<gap>.{0,25})" + literal,
            @"\A(?<outer>A(?<gap>.{0,25})" + literal + ")",
            @"\A(?:A(?<gap>.{0,25})" + literal + "|A(?<other>.{0,25})Z)" })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.Singleline })
        {
            var referencePattern = pattern.Replace(".", @"(?:(?=[\s\S]).)");
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Funded);
            var reference = new Utf8Regex(referencePattern, options, Timeout.InfiniteTimeSpan, Funded);
            foreach (var text in new[] { "Aé😀" + tail + "中" + tail, "A\n" + tail,
                "A" + new string('x', 25) + tail, "A" + new string('x', 26) + tail,
                "AéW-not-a-tail Z", "Aé" + tail[..^1], "Aé" + tail + "\n" + tail })
            {
                var input = Encoding.UTF8.GetBytes(text);
                var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                var expected = Utf8MatchSequence.First(reference.CreateMatchCursor(input));
                Utf8MatchSequence.EqualCaptures(expected, actual);
                Assert.Equal(reference.Count(input), regex.Count(input));
                Assert.Equal(reference.IsMatch(input), regex.IsMatch(input));

            }
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void TerminalTailProofIsBoundedWithoutChangingLongTailSemantics(int length)
    {
        var tail = "W" + new string('z', length - 1);
        var pattern = @"\AA(?<gap>.{0,25})" + tail;
        var input = Encoding.UTF8.GetBytes("Aé😀" + tail);
        var regex = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, Funded);
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(match.TryGetGroup("gap", out _));
        Assert.Equal("é😀", Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromName("gap")).Span));
        Assert.Equal(1, regex.Count(input));
    }

    [Fact]
    public void AChildRequiredEndCannotDiscardAnEarlierValidEndpoint()
    {
        foreach (var pattern in new[] { @"(?<=A(?<gap>.{0,25})Watson)X", @"(?=A(?<gap>.{0,25})Watson)A.*X" })
        {
            var referencePattern = pattern.Replace(".", @"(?:(?=[\s\S]).)");
            var input = "AéWatsonX中Watson"u8.ToArray();
            var regex = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, Funded);
            var reference = new Utf8Regex(referencePattern, RegexOptions.None, Timeout.InfiniteTimeSpan, Funded);
            Utf8MatchSequence.EqualCaptures(Utf8MatchSequence.First(reference.CreateMatchCursor(input)),
                Utf8MatchSequence.First(regex.CreateMatchCursor(input)));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteTailWorkCutoffsPreserveTheCallerLoan(bool hit)
    {
        var program = new Utf8Regex(@"\AA.{0,25}Watson").Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes("AéW-not-a-tail " + (hit ? "Watson" : "Watsxx"));
        var baseline = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var expected = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, expected.Breach);
        Assert.Equal(hit ? 1 : 0, expected.Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work <= baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            Assert.Equal(work == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteTailScanObservesOriginalInterruption(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var token = new CancellationTokenSource();
        var program = new Utf8Regex(@"\AA.{0,511}Watson").Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes("A" + new string('x', 100) + "Watson");
        var invocation = new Utf8SliceInvocation(new(100000, 1024, 1 << 20),
            expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) clock.Advance(50); else token.Cancel(); };
        Exception? error = null;
        try { _ = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation); }
        catch (Exception observed) { error = observed; }
        Assert.True(entered);
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ACompleteTailDenialNeverPublishesSuccessOrPartialCaptureSlots()
    {
        var program = new Utf8Regex(@"\AA(?<gap>.{0,25})Watson").Inspection.AvailableOwnedSliceProgram;
        var input = "AéWatson"u8.ToArray();
        var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, true, ref baseline).IsMatch);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            Array.Fill(starts, -1); Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new(work, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, true, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, result.Breach);
            Assert.False(result.IsMatch);
            Assert.All(starts, value => Assert.Equal(-1, value));
            Assert.All(ends, value => Assert.Equal(-1, value));
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("Holmes.{0,25}Watson|Watson.{0,25}Holmes")]
    [InlineData("A(?<gap>.{8,25})Z")]
    [InlineData("(?:A(?<gap>.{0,25})Z|A(?<other>.{0,25})Y)")]
    [InlineData("(?<outer>(?<gap>.{0,25}))Z")]
    [InlineData("A.{0,25}?Z")]
    [InlineData("A(?>.{0,25})Z")]
    public void PublicRangesCapturesAndGreedyOrderKeepOverlappingBclSemantics(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.Singleline,
            RegexOptions.IgnoreCase, RegexOptions.RightToLeft })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Funded);
            foreach (var text in new[] { "", "AxZ", "AxxxxxxxxZxxZ", "AxxxxxxxxY AxxxxxxxxZ", "Axxxxxxxxz",
                "Axxxxxxxx\nZ", "HolmesWatson", "Holmes" + new string('x', 25) + "Watson",
                "Holmes" + new string('x', 26) + "Watson", "HolmesxxxxW-not-a-tail", "Watson\nHolmes" })
            {
                var input = Encoding.UTF8.GetBytes(text); var expected = oracle.Match(text);
                Assert.Equal(expected.Success, regex.IsMatch(input));
                Assert.Equal(expected.Success, regex.Match(input).Success);
                Assert.Equal(oracle.Count(text), regex.Count(input));
                var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
            }
        }
    }

    [Theory]
    [InlineData(7)] [InlineData(8)] [InlineData(25)] [InlineData(511)] [InlineData(512)]
    public void ConservativeByteWindowIncludesTheLastAllowedScalarEndpoint(int maximum)
    {
        var pattern = @"\AA(?<gap>.{0," + maximum + "})Z";
        // The nonempty lookahead prevents the repeat's single-atom shortcut;
        // the following Dot still determines newline behavior and scalar width.
        var referencePattern = pattern.Replace(".", @"(?:(?=[\s\S]).)");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.Singleline })
        {
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, Funded);
            var reference = new Utf8Regex(referencePattern, options, Timeout.InfiniteTimeSpan, Funded);
            Assert.Contains(reference.Inspection.AvailableOwnedSliceProgram.Instructions,
                instruction => instruction.Kind == Utf8SliceInstructionKind.Fork && instruction.B == 1);
            foreach (var scalar in new[] { "x", "é", "中", "😀" })
            foreach (var length in new[] { maximum - 1, maximum, maximum + 1 })
            {
                var gap = string.Concat(Enumerable.Repeat(scalar, length));
                var input = Encoding.UTF8.GetBytes("A" + gap + "Z");
                var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                var expected = Utf8MatchSequence.First(reference.CreateMatchCursor(input));
                Assert.Equal(length <= maximum, actual.Success);
                Utf8MatchSequence.EqualCaptures(expected, actual);
                Assert.Equal(reference.Count(input), regex.Count(input));
                if (actual.Success) Assert.Equal(gap, Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromName("gap")).Span));
            }
        }
    }

    [Fact]
    public void LiteralPrefixProofKeepsFinalEndpointsAndTailInstructionBoundaries()
    {
        foreach (var tail in new[] { "Watson", "Watson123456789", "W(?<tail>at)son", "W(?i:AT)son" })
        foreach (var maximum in new[] { 8, 25, 511 })
        {
            var pattern = @"\AA(?<gap>.{0," + maximum + "})" + tail;
            var referencePattern = pattern.Replace(".", @"(?:(?=[\s\S]).)");
            var regex = new Utf8Regex(pattern, RegexOptions.Compiled, Timeout.InfiniteTimeSpan, Funded);
            var reference = new Utf8Regex(referencePattern, RegexOptions.Compiled, Timeout.InfiniteTimeSpan, Funded);
            foreach (var scalar in new[] { "x", "é", "中", "😀" })
            foreach (var length in new[] { maximum - 1, maximum, maximum + 1 })
            foreach (var suffix in new[] { tail == "Watson123456789" ? tail : "Watson", "Wats", "Wxxxxx" })
            {
                var gap = string.Concat(Enumerable.Repeat(scalar, length));
                var input = Encoding.UTF8.GetBytes("A" + gap + suffix);
                var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                var expected = Utf8MatchSequence.First(reference.CreateMatchCursor(input));
                Utf8MatchSequence.EqualCaptures(expected, actual);
                Assert.Equal(reference.Count(input), regex.Count(input));
                Assert.Equal(reference.IsMatch(input), regex.IsMatch(input));
                if (actual.Success)
                {
                    Assert.Equal(gap, Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromName("gap")).Span));
                    if (tail.Contains("?<tail>")) Assert.Equal("at", Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromName("tail")).Span));
                }
            }
        }
    }

    [Fact]
    public void NonBacktrackingAndNonzeroOriginsKeepTheirRanges()
    {
        var input = "😀xA😀😀Z xx AxxxxZ"u8.ToArray();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex("A.{0,25}Z", options, Timeout.InfiniteTimeSpan, Funded);
            Assert.Equal(1, regex.Count(input)); // Greedy selection reaches the final Z.
            Assert.Equal(5, regex.Match(input).StartOffsetInBytes);
            Assert.Equal(5, regex.Match(input, 5, []).StartOffsetInBytes);
            var next = regex.Match(input, 14, []);
            Assert.True(next.Success);
            Assert.Equal("AxxxxZ", Encoding.UTF8.GetString(input.AsSpan(next.StartOffsetInBytes, (next.EndOffsetInBytes - next.StartOffsetInBytes))));
        }
    }

    [Fact]
    public void EveryWorkCutoffPreservesTheBorrowedLoan()
    {
        var program = new Utf8Regex("A.{8,25}Z").Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.ASCII.GetBytes("A" + new string('x', 110));
        var baseline = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(0, Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline).Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work <= baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            Assert.Equal(work == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionIsObservedAfterValidation(bool expire)
    {
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("A" + new string('x', 110), 64)));
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8InputAnalyzer.Describe(input, ref validation);
        long ticks = 0; using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex("A.{8,25}Z", RegexOptions.None, TimeSpan.FromMilliseconds(20), Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validation.Budget.Steps) return;
            entered = true; if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void RetainedCapturesAliasesAdmissionAndMalformedTailsRemainValid()
    {
        const string pattern = "A(?<gap>.{8,25})Z";
        var regex = new Utf8Regex(pattern); var input = "AxxxxxxxxZ"u8.ToArray();
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        var group = retained.GetGroupMemory(regex.GroupNumberFromName("gap"));
        var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(cursor.Current, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal("xxxxxxxx", Encoding.UTF8.GetString(group.Span));
        foreach (var policy in new[] { Funded with { MaxWorkingBytes = 1 }, Funded with { MaxResultBytes = 1 } })
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(
                new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, policy).CreateMatchCursor(input)));
        byte[] malformed = [.. input, 0xff];
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, token.Token)).CancellationToken);
    }
}
