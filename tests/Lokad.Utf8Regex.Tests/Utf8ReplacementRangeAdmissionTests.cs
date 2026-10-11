using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ReplacementRangeAdmissionTests
{
    // Independent complete modeled extent for a three-int range array.
    private static ulong Bytes(int count) => count == 0 ? 0 : 32UL + ((12UL * (ulong)count + 15) & ~15UL);

    [Theory]
    [InlineData(0)] [InlineData(16)] [InlineData(2048)]
    public void FullGrowthOverlapIsAdmittedBeforeRentAndDeniedGrowthKeepsOldRanges(int count)
    {
        var next = count == 0 ? 16 : count * 2;
        var peak = 7 + Bytes(count) + Bytes(next);
        foreach (var maximum in new[] { peak - 1, peak })
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, maximum), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(7));
            var control = new Utf8InvocationBufferControl(ref invocation);
            var ledger = new Utf8ReplacementRangeLedger();
            try
            {
                for (var i = 0; i < count; i++) ledger.Add(new(i, 1, i), control);
                Assert.Equal(7 + Bytes(count), invocation.Ledger.ReservedBytes);
                var before = GC.GetAllocatedBytesForCurrentThread();
                var breach = ledger.TryAdd(new(count, 1, count), control);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(maximum == peak ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, breach);
                Assert.Equal(maximum == peak ? count + 1 : count, ledger.Count);
                Assert.Equal(7 + Bytes(maximum == peak ? next : count), invocation.Ledger.ReservedBytes);
                if (maximum == peak) Assert.Equal(peak, invocation.Budget.PeakWorkingBytes);
                else Assert.Equal(0, allocated);
                for (var i = 0; i < count; i++) Assert.Equal(new(i, 1, i), ledger[i]);
            }
            finally { ledger.Dispose(control); }
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(7);
        }
    }

    [Fact]
    public void EveryCopyWorkCutoffKeepsOldStorageAndRecoversOnTheSameAuthority()
    {
        const int count = 4096;
        ulong seedSteps, totalSteps;
        var baseline = new Utf8SliceInvocation(new(0, 0, 1024 * 1024), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(baseline.Ledger.TryReserve(13));
        var baselineControl = new Utf8InvocationBufferControl(ref baseline);
        var baselineLedger = new Utf8ReplacementRangeLedger();
        for (var i = 0; i < count; i++) baselineLedger.Add(new(i, 1, i), baselineControl);
        seedSteps = baseline.Budget.Steps;
        baselineLedger.Add(new(count, 1, count), baselineControl);
        totalSteps = baseline.Budget.Steps;
        baselineLedger.Dispose(baselineControl);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        baseline.Ledger.Release(13);
        Assert.True(totalSteps > seedSteps + 2);
        for (var cutoff = seedSteps; cutoff <= totalSteps; cutoff++)
        {
            var invocation = new Utf8SliceInvocation(new(cutoff, 0, 1024 * 1024), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(13));
            var control = new Utf8InvocationBufferControl(ref invocation);
            var ledger = new Utf8ReplacementRangeLedger();
            try
            {
                for (var i = 0; i < count; i++) ledger.Add(new(i, 1, i), control);
                Assert.Equal(cutoff == totalSteps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps,
                    ledger.TryAdd(new(count, 1, count), control));
                Assert.Equal(cutoff == totalSteps ? count + 1 : count, ledger.Count);
                for (var i = 0; i < count; i++) Assert.Equal(new(i, 1, i), ledger[i]);
                Assert.Equal(13 + Bytes(cutoff == totalSteps ? count * 2 : count), invocation.Ledger.ReservedBytes);
            }
            finally { ledger.Dispose(control); }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(13);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationAndDeadlineDuringCopyRestoreTheOldArray(bool expire)
    {
        using var source = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(0, 0, 1024 * 1024),
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var ledger = new Utf8ReplacementRangeLedger();
        var control = new Utf8InvocationBufferControl(ref invocation);
        try
        {
            for (var i = 0; i < 4096; i++) ledger.Add(new(i, 1, i), control);
            while ((invocation.Budget.Steps & 63) != 61) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            var entered = false;
            invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else source.Cancel(); };
            // Ref-struct borrowed controls cannot be captured by an assertion lambda.
            Exception? failure = null;
            try { ledger.Add(new(4096, 1, 4096), control); }
            catch (Exception error) { failure = error; }
            if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.True(entered);
            Assert.Equal(4096, ledger.Count);
            Assert.Equal(13 + Bytes(4096), invocation.Ledger.ReservedBytes);
            for (var i = 0; i < 4096; i++) Assert.Equal(new(i, 1, i), ledger[i]);
        }
        finally { ledger.Dispose(control); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }
}
