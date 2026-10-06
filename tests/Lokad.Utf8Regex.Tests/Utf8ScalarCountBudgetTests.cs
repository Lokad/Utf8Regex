using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCountBudgetTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryWorkCutoffKeepsCallerStorageAndStopsCounting(bool reverse)
    {
        var regex = new Utf8Regex("(?:😀|a)*", RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        var input = Encoding.UTF8.GetBytes("😀a😀");
        var start = reverse ? input.Length : 0;
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var complete = Utf8SliceRunner.CountFromBytes(program, input, start, [], [], ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, complete.Breach);
        Assert.Equal(2, complete.Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong limit = 1; limit <= baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, start, [], [], ref invocation);
            Assert.Equal(limit == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, result.Breach);
            Assert.InRange(result.Count, 0, 2);
            if (result.Breach == Utf8ExecutionLimitKind.None) Assert.Equal(2, result.Count);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryScratchCutoffKeepsTheBorrowedReservation(bool reverse)
    {
        var regex = new Utf8Regex("(?:😀|a)*", RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        var input = Encoding.UTF8.GetBytes("😀a😀");
        var start = reverse ? input.Length : 0;
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(2, Utf8SliceRunner.CountFromBytes(program, input, start, [], [], ref baseline).Count);
        var peak = baseline.Budget.PeakWorkingBytes;
        Assert.True(peak > 13);
        for (ulong cap = 13; cap <= peak; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, start, [], [], ref invocation);
            Assert.Equal(cap == peak ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationReleasesOnlyOwnedScratch(bool reverse)
    {
        var regex = new Utf8Regex("(?:😀|a)*", RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀a", 512)));
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() =>
                Utf8SliceRunner.CountFromBytes(program, input, reverse ? input.Length : 0, [], [], ref invocation));
            Assert.Equal(source.Token, error.CancellationToken);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }
}
