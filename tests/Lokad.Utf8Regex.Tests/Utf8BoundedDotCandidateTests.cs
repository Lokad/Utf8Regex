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
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            var reference = new Utf8Regex(referencePattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Funded
        });
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(match.TryGetGroup(1, out _));
        Assert.Equal("é😀", Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromUtf8Name("gap"u8)).Span));
        Assert.Equal(1, regex.Count(input));
    }

    [Fact]
    public void GreedyTerminalLiteralCanCrossAReverseSearchBlock()
    {
        const string pattern = @"\A(?<gap>.*)(?<tail>😀é)";
        // The final reverse block starts at byte 2050. The literal at 2049
        // is visible only if the next block includes bytes beyond its last start.
        var input = Encoding.UTF8.GetBytes(new string('a', 2049) + "😀é" + new string('x', 2043));
        using var token = new CancellationTokenSource();
        using var limited = Utf8Regex.Compile(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy { MaxWorkSteps = 128, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        Assert.True(limited.IsMatch(input, token.Token));

        using var regex = new Utf8Regex(pattern);
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token));
        Assert.Equal(2055, match.Value.EndOffsetInBytes);
        Assert.Equal(2049, match.GetGroupMemory(1).Length);
        Assert.True(match.GetGroupMemory(2).Span.SequenceEqual("😀é"u8));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void GreedyClassTerminalLiteralRespectsScalarMinimum(int minimum, bool hit)
    {
        using var regex = new Utf8Regex(@"\A(?<gap>[a😀]{" + minimum + ",4})😀a");
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor("a😀😀a"u8.ToArray()));
        Assert.Equal(hit, match.Success);
        if (hit) Assert.True(match.GetGroupMemory(1).Span.SequenceEqual("a😀"u8));
    }

    [Fact]
    public void AChildRequiredEndCannotDiscardAnEarlierValidEndpoint()
    {
        foreach (var pattern in new[] { @"(?<=A(?<gap>.{0,25})Watson)X", @"(?=A(?<gap>.{0,25})Watson)A.*X" })
        {
            var referencePattern = pattern.Replace(".", @"(?:(?=[\s\S]).)");
            var input = "AéWatsonX中Watson"u8.ToArray();
            var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            var reference = new Utf8Regex(referencePattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
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
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void CompleteTailScanObservesOriginalInterruption(bool expire, bool reverse)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var token = new CancellationTokenSource();
        using var regex = new Utf8Regex(reverse ? @"\AA.*Watson" : @"\AA.{0,511}Watson");
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes(reverse ? "AWatson" + new string('x', 250000) : "A" + new string('x', 100) + "Watson");
        var invocation = new Utf8SliceInvocation(new(100000, 1024, 1 << 20),
            expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var entered = false;
        // The forward dot takes about 123 blocks for the long subject. The
        // next selected checkpoint interrupts the descending literal search.
        invocation.CheckpointHook = steps =>
        {
            if (reverse && steps < 192) return;
            entered = true;
            if (expire) clock.Advance(50); else token.Cancel();
        };
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
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
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
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            var reference = new Utf8Regex(referencePattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
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
                if (actual.Success) Assert.Equal(gap, Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromUtf8Name("gap"u8)).Span));
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
            var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            var reference = new Utf8Regex(referencePattern, options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
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
                    Assert.Equal(gap, Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromUtf8Name("gap"u8)).Span));
                    if (tail.Contains("?<tail>")) Assert.Equal("at", Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromUtf8Name("tail"u8)).Span));
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
            var regex = new Utf8Regex("A.{0,25}Z", options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
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
    public void OriginalInterruptionIsObservedDuringSearch(bool expire)
    {
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("A" + new string('x', 110), 64)));
        long ticks = 0; using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex("A.{8,25}Z", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Funded
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (entered) return;
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
        var group = retained.GetGroupMemory(regex.GroupNumberFromUtf8Name("gap"u8));
        var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(cursor.Current, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal("xxxxxxxx", Encoding.UTF8.GetString(group.Span));
        foreach (var policy in new[] { Funded with { MaxWorkingBytes = 1 }, Funded with { MaxResultBytes = 1 } })
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(
                new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = policy
                }).CreateMatchCursor(input)));
        byte[] malformed = [.. input, 0xff];
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, token.Token)).CancellationToken);
    }
}
