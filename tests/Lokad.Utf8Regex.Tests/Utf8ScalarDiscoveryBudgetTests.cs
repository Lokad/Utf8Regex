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
        var regex = new Utf8Regex(reverse ? "(?<y>b)(?<x>a+)" : "(?<x>a+)(?<y>b)", options:             RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        AssertCutoffs(program, reverse ? "baa😀aa"u8.ToArray() : "aa😀aab"u8.ToArray(), histories,
            reverse ? 0 : 6, reverse ? 3 : 9, reverse ? [0] : [6], reverse ? [1] : [2]);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void CapturedCandidateCancellationReleasesReusedStorage(bool reverse, bool count)
    {
        var regex = new Utf8Regex("(a(.)c)", options: RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        // Exercise failed captured attempts in either direction. A subject
        // with no starting literal may finish in the prefix search alone.
        var input = System.Text.Encoding.UTF8.GetBytes(string.Concat(
            Enumerable.Repeat(reverse ? "xbc" : "abx", 1024)));
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
    public void ScratchCutoffsPreserveBorrowedStorage(bool reverse, bool histories)
    {
        var regex = new Utf8Regex("(.)b", options: RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        var input = "é😀b"u8.ToArray();
        AssertCutoffs(program, input, histories, 2, 7, [2], [4]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BalancedHistoriesAdmitArrayReplacementBeforeShrinking(bool reverse)
    {
        var regex = new Utf8Regex(reverse ? "(?<-x>.)(?<x>.)+" : "(?<x>.)+(?<-x>.)", options:             RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        AssertCutoffs(program, "é😀ab"u8.ToArray(), true, 0, 8, reverse ? [7, 6] : [0, 2], reverse ? [1, 1] : [2, 4]);
    }

    private static void AssertCutoffs(Utf8SliceProgram program, byte[] input, bool histories,
        int start, int end, int[] captureStarts, int[] captureLengths)
    {
        var baseline = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Find(program, input, histories, start, end, captureStarts, captureLengths, ref baseline));
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong cap = 13; cap <= baseline.Budget.PeakWorkingBytes; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(cap == baseline.Budget.PeakWorkingBytes ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes,
                Find(program, input, histories, start, end, captureStarts, captureLengths, ref invocation));
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    private static Utf8ExecutionLimitKind Find(Utf8SliceProgram program, byte[] input, bool histories,
        int start, int end, int[] captureStarts, int[] captureLengths, ref Utf8SliceInvocation invocation)
    {
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var results = new Utf8InvocationResultLedger();
        try
        {
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, program.RightToLeft ? input.Length : 0,
                new int[program.GroupCount], new int[program.GroupCount], histories, ref workspace, ref invocation);
            if (outcome.Breach == Utf8ExecutionLimitKind.None)
            {
                Assert.True(outcome.Found);
                Assert.Equal((start, end), (outcome.StartByte, outcome.EndByte));
                if (histories)
                {
                    var held = invocation.Ledger.ReservedBytes;
                    var snapshot = workspace.Stacks.SnapshotHistory(input, program.GroupNumbers, outcome.StartByte, outcome.EndByte,
                        ref results, ref invocation);
                    Assert.Equal(captureStarts.Zip(captureLengths, static (start, length) => (start, start + length)),
                        Utf8SliceHistoryAdmissionTests.CaptureEdges(snapshot, program.GroupNumbers[1]));
                    Assert.Equal(held + Utf8SliceHistoryAdmissionTests.HistoryBytes(snapshot), invocation.Ledger.ReservedBytes);
                }
            }
            else Assert.False(outcome.Found);
            return outcome.Breach;
        }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { return invocation.Budget.LimitBreach; }
        finally
        {
            results.Release(ref invocation);
            workspace.Release(ref invocation);
        }
    }

    [Fact]
    public void HistoryCancellationReleasesMaterializationAndPreservesItsToken()
    {
        var regex = new Utf8Regex("(?<x>.)+", options: RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Scalar program missing.");
        var input = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", 4096)));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var results = new Utf8InvocationResultLedger();
        var entered = false;
        try
        {
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, 0, new int[1], new int[1], true,
                ref workspace, ref invocation);
            Assert.True(outcome.Found);
            invocation.CheckpointHook = _ => { entered = true; source.Cancel(); };
            var error = Assert.Throws<OperationCanceledException>(() => workspace.Stacks.SnapshotHistory(input,
                program.GroupNumbers, outcome.StartByte, outcome.EndByte, ref results, ref invocation));
            Assert.Equal(source.Token, error.CancellationToken);
        }
        finally
        {
            results.Release(ref invocation);
            workspace.Release(ref invocation);
        }
        Assert.True(entered);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
