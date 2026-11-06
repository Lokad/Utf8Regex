using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NegatedPairRewindTests
{
    [Theory]
    [InlineData("[^ab]+a", "é😀b𝒜a")]
    [InlineData("[^ b]+a", "xxx ééa")]
    [InlineData("[^ b]+ ", "xxx éé ")]
    [InlineData("[^ab]+x", "xxxbxxx")]
    [InlineData("[^ab]+aZ", "ééa?éaZ")]
    [InlineData("[^ab]+a(?:Z|Y)", "éa?éaY")]
    [InlineData("[^AB]+A", "ééB𝒜A")]
    [InlineData("[^Aa]+a", "ééA𝒜a")]
    [InlineData("[^abc]+a", "ééa")]
    [InlineData("[^é]+a", "xxa éxxa")]
    [InlineData("[^ab]{2,}a", "xxa xba")]
    [InlineData("[^ab]{1,3}a", "xxxa xba")]
    [InlineData("[^ab]*a", "a éa")]
    [InlineData("(?i:[^ab]+)a", "ééA𝒜a")]
    [InlineData("([^ab]+)a", "ééa 𝒜a")]
    [InlineData(@"[^ab]+\s*a", "éé a")]
    public void DirectWindowsAndExcludedPathsKeepOverlappingBclResults(string pattern, string text)
    {
        // Excluded prefix blocks force the delimiter window to find a later
        // scalar start. The long suffix also exposes absent/dense tail searches.
        text = new string('b', 2049) + text + new string('b', 2049);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(oracle.Count(text), regex.Count(input));
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Success, actual.Success);
        if (actual.Success) Assert.Equal(expected.Value, actual.GetValueString());
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    [InlineData(4095)] [InlineData(4096)]
    public void RewindStopsAndFloorsRemainScalarAligned(int length)
    {
        var text = "😀" + new string('é', length) + "b𝒜a";
        var input = Encoding.UTF8.GetBytes(text);
        var program = new Utf8Regex("[^ab]+a").Inspection.AvailableOwnedSliceProgram!;
        var invocation = new Utf8SliceInvocation(new(1000, 1, 1 << 20), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.CountFromBytes(program, input, 4, [], [], ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        Assert.Equal(1, outcome.Count);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void EveryWorkCutoffRefundsTheEnclosingLoan()
    {
        var program = new Utf8Regex("[^ab]+a").Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('é', 10000) + "a");
        var baseline = new Utf8SliceInvocation(new(1000, 1, 1 << 20), Regex.InfiniteMatchTimeout, default);
        var complete = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline);
        Assert.Equal(1, complete.Count);
        Assert.Equal(Utf8ExecutionLimitKind.None, complete.Breach);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 1, 1 << 20), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BackwardBlocksObserveOriginalInterruption(bool expire)
    {
        var program = new Utf8Regex("[^ab]+a").Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes(new string('é', 131072) + "a");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var seekBlocks = (input.Length + 2047) / 2048;
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= (ulong)seekBlocks) return;
            entered = true;
            if (expire) ticks = 25; else token.Cancel();
        });
        var invocation = new Utf8SliceInvocation(new(1000000, 1, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var failure = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation));
            Assert.True(entered);
            if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }
}
