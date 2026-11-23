using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SliceCaptureProjectionBudgetTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(65)]
    public void SliceArraysShareExactAdmissionWithTheirParent(int slots)
    {
        var groupBytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(slots);
        var starts = Enumerable.Repeat(1, Math.Max(0, slots - 1)).ToArray();
        var ends = Enumerable.Repeat(5, Math.Max(0, slots - 1)).ToArray();
        foreach (var allow in new[] { false, true })
        {
            var bytes = 7 + groupBytes - (allow || slots == 0 ? 0UL : 1UL);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, bytes), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            if (!allow && slots != 0)
            {
                try { _ = Pcre2Runner.PublishSliceCaptures(slots, 1, 7, starts, ends, ref invocation); Assert.Fail("Expected physical aggregate denial."); }
                catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind); }
            }
            else
            {
                var groups = Pcre2Runner.PublishSliceCaptures(slots, 1, 7, starts, ends, ref invocation);
                Assert.Equal(slots, groups.Length);
                Assert.Equal(7 + groupBytes, invocation.Budget.PeakWorkingBytes);
                Assert.Equal(7 + groupBytes, invocation.Ledger.ReservedBytes);
                if (slots > 0) Assert.Equal((1, 7), Edges(groups[0]));
                Pcre2SliceDriver.DetachProjectedGroups(groups, ref invocation);
                if (slots > 1) Assert.Equal((1, 5), Edges(groups[1]));
            }
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(7);
        }
    }

    [Fact]
    public void ScalarProjectionNormalizesReverseEdgesAndKeepsMissingGroups()
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        int[] starts = [7, -1, 5, 5];
        int[] ends = [1, 7, 7, -1];
        var groups = Pcre2Runner.PublishSliceCaptures(5, 1, 7, starts, ends, ref invocation);
        Assert.Equal(new Pcre2GroupData { Number = 1,Success = true,StartOffsetInBytes = 1,EndOffsetInBytes = 7,Utf8SliceIsWellFormed = true}, groups[1]);
        Assert.False(groups[2].Success);
        Assert.Equal(new Pcre2GroupData { Number = 3,Success = true,StartOffsetInBytes = 5,EndOffsetInBytes = 7,Utf8SliceIsWellFormed = true}, groups[3]);
        Assert.False(groups[4].Success);
        Pcre2SliceDriver.DetachProjectedGroups(groups, ref invocation);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(33)]
    public void EveryWorkCutoffRestoresLoansWithoutRefundingSteps(int slots)
    {
        var bytes = Encoding.UTF8.GetBytes("a" + string.Concat(Enumerable.Repeat("😀", 3000)) + "é");
        var starts = Enumerable.Repeat(1, slots - 1).ToArray();
        var ends = Enumerable.Repeat(bytes.Length - 2, slots - 1).ToArray();
        var completed = false;
        for (ulong steps = 1; steps <= 4096; steps++)
        {
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(steps, 0, 1UL << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            try
            {
                var groups = Pcre2Runner.PublishSliceCaptures(slots, 0, bytes.Length, starts, ends, ref invocation);
                Pcre2SliceDriver.DetachProjectedGroups(groups, ref invocation);
                completed = true;
            }
            catch (Pcre2MatchException failure)
            {
                Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind);
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, invocation.TryChargeStep());
            }
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(7);
            if (completed) break;
        }
        Assert.True(completed);
    }

    [Theory]
    [InlineData(4097)]
    [InlineData(8193)]
    public void LiveCancellationDuringPublicationUnwindsCaptureStorage(int slots)
    {
        using var cancellation = new CancellationTokenSource();
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", 20)));
        var starts = new int[Math.Max(0, slots - 1)];
        var ends = Enumerable.Repeat(bytes.Length, starts.Length).ToArray();
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 1UL << 20), Timeout.InfiniteTimeSpan, cancellation.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        invocation.CheckpointHook = _ => cancellation.Cancel();
        try { _ = Pcre2Runner.PublishSliceCaptures(slots, 0, bytes.Length, starts, ends, ref invocation); Assert.Fail("Expected cancellation after entry."); }
        catch (OperationCanceledException failure) { Assert.Equal(cancellation.Token, failure.CancellationToken); }
        Assert.True(invocation.Budget.Steps > 0);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void CursorCaptureReusesAdmittedBufferAndRestoresParent()
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(é)(😀)");
        var compiled = regex.DebugCompiledProgram;
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(compiled.Operations.Match);
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram,
            Utf8ValidatedInput.BorrowBytes("xé😀xé😀"u8), new Utf8BytePosition(0), compiled.Request, cancellation.Token);
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(7));
        var slots = Pcre2SliceDriver.RentProgramSlots(direct.SliceProgram, ref cursor.WorkingInvocation);
        var held = cursor.WorkingInvocation.Ledger.ReservedBytes;
        Pcre2GroupData[]? groups = null;
        try
        {
            Assert.True(cursor.MoveNext(slots));
            groups = cursor.ProjectCurrentCaptures(slots);
            var projectedHeld = held + Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(3);
            Assert.Equal(projectedHeld, cursor.WorkingInvocation.Ledger.ReservedBytes);
            Assert.Equal((3, 7), Edges(groups[2]));
            Assert.True(cursor.MoveNext(slots));
            Assert.Same(groups, cursor.ProjectCurrentCaptures(slots, groups));
            Assert.Equal(projectedHeld, cursor.WorkingInvocation.Ledger.ReservedBytes);
            Assert.Equal((10, 14), Edges(groups[2]));
            Assert.False(cursor.MoveNext(slots));
            Assert.Equal(projectedHeld, cursor.WorkingInvocation.Ledger.ReservedBytes);
        }
        finally
        {
            if (groups is not null) Pcre2SliceDriver.DetachProjectedGroups(groups, ref cursor.WorkingInvocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref cursor.WorkingInvocation);
        }
        Assert.Equal(7UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
        cursor.WorkingInvocation.Ledger.Release(7);
    }

    private static (int, int) Edges(Pcre2GroupData group)
        => (group.StartOffsetInBytes, group.EndOffsetInBytes);
}
