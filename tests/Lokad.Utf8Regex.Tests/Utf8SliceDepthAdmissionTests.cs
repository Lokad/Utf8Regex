using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8SliceDepthAdmissionTests
{
    private static Utf8SliceProgram Frames(int count, bool repeats, bool rtl)
    {
        Utf8SliceNode root = repeats
            ? new Utf8SliceNode.RepeatNode(new Utf8SliceNode.LiteralNode(0x1f600, false), 0, count, false)
            : new Utf8SliceNode.ConcatNode(Enumerable.Range(0, count).Select(_ => (Utf8SliceNode)
                new Utf8SliceNode.AlternationNode([[new Utf8SliceNode.LiteralNode(0x1f600, false)], [new Utf8SliceNode.LiteralNode(0xe9, false)]])).ToArray());
        return Utf8SliceLowerer.Lower(root, null, false, false, rtl)!;
    }

    private static Utf8SliceMatchOutcome Run(Utf8SliceProgram program, byte[] input, ref Utf8SliceInvocation invocation)
        => Utf8SliceRunner.TryMatchAt(program, input, program.RightToLeft ? input.Length : 0,
            [], [], true, ref invocation);

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void DeniedForkAndRepeatFramesNeverAcquireTheNextChunk(bool repeats, bool rtl)
    {
        var capacity = Utf8SliceRunner.FrameChunkCapacity;
        var exact = Frames(capacity, repeats, rtl);
        var extra = Frames(capacity + 1, repeats, rtl);
        var firstInput = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", capacity)));
        var secondInput = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", capacity + 1)));
        var admitted = new Utf8SliceInvocation(new(0, (uint)capacity, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(admitted.Ledger.TryReserve(7));
        Assert.True(Run(exact, firstInput, ref admitted).IsMatch);
        var peak = admitted.Budget.PeakWorkingBytes;
        Assert.Equal((uint)capacity, admitted.Budget.PeakDepth); Assert.Equal(7UL, admitted.Ledger.ReservedBytes);
        var denied = new Utf8SliceInvocation(new(0, (uint)capacity, peak), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(denied.Ledger.TryReserve(7));
        var failure = Run(extra, secondInput, ref denied);
        Assert.False(failure.IsMatch); Assert.Equal(Utf8ExecutionLimitKind.Depth, failure.Breach);
        Assert.Equal((uint)capacity + 1, denied.Budget.PeakDepth);
        Assert.Equal(peak, denied.Budget.PeakWorkingBytes); Assert.Equal(7UL, denied.Ledger.ReservedBytes);
        admitted.Ledger.Release(7); denied.Ledger.Release(7);
    }

    private static Utf8SliceProgram Assertions(int repeats, bool positive)
    {
        var body = new Utf8SliceNode.ConcatNode(Enumerable.Range(0, repeats).Select(_ => (Utf8SliceNode)
            new Utf8SliceNode.RepeatNode(new Utf8SliceNode.LiteralNode('a', false), 0, 1, false)).ToArray());
        var inner = new Utf8SliceNode.LookbehindNode(body, positive);
        return Utf8SliceLowerer.Lower(new Utf8SliceNode.LookbehindNode(inner, true), null, false, false, false)!;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AssertionDepthDenialPrecedesLargeRepeatAndContinuationAcquisition(bool positive)
    {
        var small = Assertions(0, positive); var large = Assertions(8192, positive);
        Assert.Equal(8192, large.Assertions[0].Body.Assertions[0].Body.RepeatCount);
        var baseline = new Utf8SliceInvocation(new(0, 1, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(baseline.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.Depth, Run(small, [], ref baseline).Breach);
        var peak = baseline.Budget.PeakWorkingBytes;
        var invocation = new Utf8SliceInvocation(new(0, 1, peak), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(7));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var outcome = Run(large, [], ref invocation);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.False(outcome.IsMatch); Assert.Equal(Utf8ExecutionLimitKind.Depth, outcome.Breach);
        Assert.Equal(2U, invocation.Budget.PeakDepth);
        Assert.Equal(peak, invocation.Budget.PeakWorkingBytes); Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.True(allocated < 32768, $"Denied assertion acquired large repeat arrays: {allocated} bytes.");
        baseline.Ledger.Release(7); invocation.Ledger.Release(7);
    }

    [Fact]
    public void OriginalCancellationAndDeadlineStillPrecedeDepthAdmission()
    {
        var program = Assertions(32, true);
        using var source = new CancellationTokenSource(); source.Cancel();
        var canceled = new Utf8SliceInvocation(new(0, 1, 0), Regex.InfiniteMatchTimeout, source.Token);
        var error = Assert.Throws<OperationCanceledException>(() => Run(program, [], ref canceled));
        Assert.Equal(source.Token, error.CancellationToken); Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var expired = new Utf8SliceInvocation(new(0, 1, 0), TimeSpan.FromMilliseconds(20), CancellationToken.None);
        ticks = 25;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => Run(program, [], ref expired));
        Assert.Equal(0UL, expired.Ledger.ReservedBytes);
    }
}
