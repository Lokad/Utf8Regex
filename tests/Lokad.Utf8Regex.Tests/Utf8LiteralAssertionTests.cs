using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8LiteralAssertionTests
{
    [Theory]
    [InlineData("(?<=foo)bar")]
    [InlineData("(?<!foo)bar")]
    [InlineData("bar(?=foo)")]
    [InlineData("bar(?!foo)")]
    [InlineData("bar(?=foo|go|baz)")]
    [InlineData("bar(?!foo|go|baz)")]
    [InlineData("bar(?=(foo|go))")]
    [InlineData("(?:foo|fo)(?<=foo)bar")]
    [InlineData("(?<=(?<x>foo))bar")]
    [InlineData("(?<=foo|goo)bar")]
    [InlineData("(?<=fo{1,2})bar")]
    [InlineData("(?<=f(?=oo)oo)bar")]
    [InlineData("(?<=foo)bar(?<=bar)")]
    [InlineData("(?:(?<x>foo)(?=bar)baz|(?<x>foo)bar)")]
    [InlineData("(?:(?<x>foo)(?!bar)bar|(?<x>foo)bar)")]
    [InlineData("(?<=😀)bar")]
    public void PublicOperationsPreserveAssertionsAndCaptures(string pattern)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant })
        {
            var regex = new Utf8Regex(pattern, options: mode); var oracle = new Regex(pattern, mode);
            foreach (var text in new[] { "", "bar", "foobar", "fooxbar", "FOObar", "goobar", "barfoo",
                "éfoobar😀", "😀bar", "foobarbazfoobar", "fobar", "foo", "bar\n" })
            {
                var input = Encoding.UTF8.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var expected = oracle.Match(text); var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(storage.AsMemory(1, input.Length)));
                Assert.Equal(expected.Success, actual.Success);
                Assert.Equal(expected.Success, regex.IsMatch(input));
                Assert.Equal(oracle.Count(text), regex.Count(input));
                if (!actual.Success) continue;
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.Value.StartOffsetInBytes);
                Assert.Equal(expected.Value, System.Text.Encoding.UTF8.GetString(actual.GetValueMemory().Span));
                if (expected.Groups.Count > 1) Assert.Equal(expected.Groups[1].Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(1).Span));
            }
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(31)] [InlineData(32)] [InlineData(33)]
    public void LiteralBodyBoundsPreserveFullContextAndOffsetFloors(int length)
    {
        var prefix = new string('a', length); var regex = new Utf8Regex("(?<=" + prefix + ")bar");
        var input = Encoding.UTF8.GetBytes("😀" + prefix + "bar");
        var match = regex.Match(input, 4 + length, []);
        Assert.True(match.Success); Assert.Equal(4 + length, match.StartOffsetInBytes);
        Assert.False(regex.Match(input, 5 + length, []).Success);
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes("😀" + prefix[..^1] + "bar")));
    }

    [Theory]
    [InlineData("(?=foo|bar)bar", "bar", 0)]
    [InlineData("(?!foo|goo)bar", "bar", 0)]
    [InlineData("(?<=foo|goo)bar", "goobar", 3)]
    public void LiteralChoicesNeedOnlyLogicalAssertionDepth(string pattern, string text, int start)
    {
        using var regex = Utf8Regex.Compile(pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1), Timeout.InfiniteTimeSpan, default);
        var result = Utf8SliceRunner.TryMatchAt(program, Encoding.UTF8.GetBytes(text), start, [], [], true, ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.True(result.IsMatch);
        Assert.Equal(1u, invocation.Budget.PeakDepth);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void LogicalAssertionDepthAndOriginalLoansSurviveTheShortcut()
    {
        var plain = new Utf8Regex("(?<=foo)bar").Inspection.AvailableOwnedSliceProgram!;
        var tiny = new Utf8SliceInvocation(new(100000, 1, 1), Timeout.InfiniteTimeSpan, default);
        var literal = Utf8SliceRunner.TryMatchAt(plain, "foobar"u8, 3, [], [], true, ref tiny);
        Assert.Equal(Utf8ExecutionLimitKind.None, literal.Breach); Assert.True(literal.IsMatch);
        Assert.Equal(1u, tiny.Budget.PeakDepth); Assert.Equal(0UL, tiny.Ledger.ReservedBytes);
        var program = new Utf8Regex("(?:foo|fo)(?<=foo)bar").Inspection.AvailableOwnedSliceProgram!;
        var complete = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.TryMatchAt(program, "foobar"u8, 0, [], [], true, ref complete);
        Assert.True(outcome.IsMatch); Assert.True(complete.Budget.PeakDepth >= 2);
        Assert.Equal(13UL, complete.Ledger.ReservedBytes);
        var denied = new Utf8SliceInvocation(new(100000, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(denied.Ledger.TryReserve(13));
        outcome = Utf8SliceRunner.TryMatchAt(program, "foobar"u8, 0, [], [], true, ref denied);
        Assert.Equal(Utf8ExecutionLimitKind.Depth, outcome.Breach);
        Assert.Equal(13UL, denied.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("(?<=foo)bar", true)]
    [InlineData("(?<!foo)bar", false)]
    [InlineData("(?<=goo|foo)bar", true)]
    [InlineData("(?<!goo|foo)bar", false)]
    public void EveryWorkCutoffKeepsTheExistingLoan(string pattern, bool expected)
    {
        var program = new Utf8Regex(pattern).Inspection.AvailableOwnedSliceProgram!;
        var complete = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.TryMatchAt(program, "foobar"u8, 3, [], [], true, ref complete);
        Assert.Equal(expected, outcome.IsMatch); Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        for (ulong work = 1; work <= complete.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            outcome = Utf8SliceRunner.TryMatchAt(program, "foobar"u8, 3, [], [], true, ref invocation);
            Assert.Equal(work < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, outcome.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("(?:foo(?<=foo)){1000}bar", false)]
    [InlineData("(?:foo(?<=foo)){1000}bar", true)]
    [InlineData("(?:(?=foo|goo)foo){1000}bar", false)]
    [InlineData("(?:(?=foo|goo)foo){1000}bar", true)]
    public void RepeatedAssertionsKeepOriginalInterruption(string pattern, bool expire)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("foo", 1000)) + "bar");
        using var token = new CancellationTokenSource(); long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000
            }
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { entered = true; if (expire) ticks = 50; else token.Cancel(); });
        try
        {
            if (expire) Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, cancellationToken: token.Token));
            else Assert.Equal(token.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Match(input, cancellationToken: token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void GuardedRepeatChecksItsDeadlineBeforeLeavingTheLoop()
    {
        using var regex = new Utf8Regex(@"\A(?:(?!foo|goo)[a-z]){1,512}foo\z", policy: new()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("gox", 96)) + "foo");
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        Assert.True(regex.IsMatch(input));
        var session = Utf8SearchDiagnosticsSession.Start(_ => ticks = 50);
        try { Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input)); }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData("(?=b)b", "b", true, false)]
    [InlineData("(?=b)b", "c", false, false)]
    [InlineData("(?=b)(?=(?<next>b))b", "b", true, false)]
    [InlineData("(?=b)b", "b", true, true)]
    public void InlineLookaheadChecksInterruptionBeforeLeavingItsSegment(
        string suffix, string tail, bool expected, bool cancelAtClockRead)
    {
        var prefix = new string('a', 96);
        using var regex = new Utf8Regex(@"\A" + prefix + suffix, RegexOptions.Compiled, new()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        Assert.Contains(regex.Inspection.AvailableOwnedSliceProgram!.Instructions,
            op => op is { Kind: Utf8SliceInstructionKind.Fork, B: 1, C: > 0 });
        var input = Encoding.UTF8.GetBytes(prefix + tail);
        using var cancel = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (!cancelAtClockRead) return ticks;
            if (ticks != 0) cancel.Cancel();
            return 0;
        });
        Assert.Equal(expected, regex.IsMatch(input, cancel.Token));

        var session = Utf8SearchDiagnosticsSession.Start(_ => ticks = 50);
        try
        {
            if (cancelAtClockRead)
                Assert.Equal(cancel.Token,
                    Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, cancel.Token)).CancellationToken);
            else
                Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, cancel.Token));
        }
        finally { session.Complete(); }
    }
}
