using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarDiscoveryBudgetTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void FailedCapturedCandidatesResetTrailsRepeatsAndHeads(bool reverse, bool histories)
    {
        var regex = new Utf8Regex(reverse ? "(?<y>b)(?<x>a+)" : "(?<x>a+)(?<y>b)",
            RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        AssertCutoffs(program, reverse ? "baa😀aa"u8.ToArray() : "aa😀aab"u8.ToArray(), histories,
            reverse ? 0 : 6, reverse ? 3 : 9, reverse ? [0] : [6], reverse ? [1] : [2]);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void CapturedCandidateCancellationReleasesReusedStorage(bool reverse, bool count)
    {
        var regex = new Utf8Regex("(a(.)c)", RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        var input = System.Text.Encoding.UTF8.GetBytes(new string('x', 4096));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var seenAfterMultipleCandidates = false;
        invocation.CheckpointHook = steps =>
        {
            if (steps < 256) return;
            seenAfterMultipleCandidates = true;
            source.Cancel();
        };
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var error = Assert.Throws<OperationCanceledException>(() =>
        {
            if (count) _ = Utf8SliceRunner.CountFromBytes(program, input, reverse ? input.Length : 0, starts, ends, ref invocation);
            else _ = Utf8SliceRunner.TryFindFirst(program, input, reverse ? input.Length : 0, starts, ends, true, ref invocation);
        });
        Assert.True(seenAfterMultipleCandidates);
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.All(starts, value => Assert.Equal(-1, value));
        Assert.All(ends, value => Assert.Equal(-1, value));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void EveryWorkAndScratchCutoffPreservesBorrowedStorage(bool reverse, bool histories)
    {
        var regex = new Utf8Regex("(.)b", RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        var input = "é😀b"u8.ToArray();
        AssertCutoffs(program, input, histories, 2, 7, [2], [4]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BalancedHistoriesAdmitArrayReplacementBeforeShrinking(bool reverse)
    {
        var regex = new Utf8Regex(reverse ? "(?<-x>.)(?<x>.)+" : "(?<x>.)+(?<-x>.)",
            RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        AssertCutoffs(program, "é😀ab"u8.ToArray(), true, 0, 8, reverse ? [7, 6] : [0, 2], reverse ? [1, 1] : [2, 4]);
    }

    private static void AssertCutoffs(Utf8SliceProgram program, byte[] input, bool histories,
        int start, int end, int[] captureStarts, int[] captureLengths)
    {
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Find(program, input, histories, start, end, captureStarts, captureLengths, ref baseline));
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong steps = 1; steps <= baseline.Budget.Steps; steps++)
        {
            var invocation = new Utf8SliceInvocation(new(steps, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(steps == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps,
                Find(program, input, histories, start, end, captureStarts, captureLengths, ref invocation));
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        for (ulong cap = 13; cap <= baseline.Budget.PeakWorkingBytes; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(cap == baseline.Budget.PeakWorkingBytes ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes,
                Find(program, input, histories, start, end, captureStarts, captureLengths, ref invocation));
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    private static Utf8ExecutionLimitKind Find(Utf8SliceProgram program, byte[] input, bool histories,
        int start, int end, int[] captureStarts, int[] captureLengths, ref Utf8SliceInvocation invocation)
    {
        var outcome = Utf8SliceRunner.TryFindFirst(program, input, program.RightToLeft ? input.Length : 0,
            new int[program.GroupCount], new int[program.GroupCount], histories, ref invocation);
        try
        {
            if (outcome.Breach == Utf8ExecutionLimitKind.None)
            {
                Assert.True(outcome.Found);
                Assert.Equal((start, end), (outcome.StartByte, outcome.EndByte));
                if (histories)
                {
                    Assert.Equal(captureStarts, outcome.CaptureStarts?[0]);
                    Assert.Equal(captureLengths, outcome.CaptureLengths?[0]);
                    Assert.Equal(13UL + Utf8SliceHistoryAdmissionTests.HistoryBytes(
                        outcome.CaptureStarts ?? throw new InvalidOperationException(),
                        outcome.CaptureLengths ?? throw new InvalidOperationException()), invocation.Ledger.ReservedBytes);
                }
            }
            else
            {
                Assert.False(outcome.Found);
                Assert.Null(outcome.CaptureStarts);
                Assert.Null(outcome.CaptureLengths);
            }
            return outcome.Breach;
        }
        finally { Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref invocation); }
    }

    [Fact]
    public void HistoryCancellationReleasesMaterializationAndPreservesItsToken()
    {
        var regex = new Utf8Regex("(?<x>.)+", RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        var input = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", 4096)));
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(Utf8SliceRunner.TryFindFirst(program, input, 0, new int[1], new int[1], false, ref baseline).Found);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var entered = false;
        invocation.CheckpointHook = steps =>
        {
            // The identical VM without materialization has already completed.
            if (steps <= baseline.Budget.Steps) return;
            entered = true;
            source.Cancel();
        };
        var error = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.TryFindFirst(program, input, 0, new int[1], new int[1], true, ref invocation));
        Assert.True(entered);
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
