using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SliceCaptureProjectionBudgetTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_invocation")]
    private static extern ref Utf8SliceInvocation CursorInvocation(ref Pcre2SliceGlobalMatchCursor cursor);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(65)]
    public void SliceArraysShareExactAdmissionWithTheirParent(int slots)
    {
        var groupBytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(slots);
        var scratchBytes = slots <= 32 ? 0UL : Utf8ConstructionBudget.ArrayBytes<int>(slots * 4);
        var starts = Enumerable.Repeat(1, Math.Max(0, slots - 1)).ToArray();
        var ends = Enumerable.Repeat(5, Math.Max(0, slots - 1)).ToArray();
        foreach (var allow in new[] { false, true })
        {
            var bytes = 7 + groupBytes + scratchBytes - (allow || slots == 0 ? 0UL : 1UL);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, bytes), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var input = Utf8ValidatedInput.Create("x😀é"u8);
            if (!allow && slots != 0)
            {
                try { _ = Pcre2Runner.ProjectSliceCaptures(slots, 1, 7, starts, ends, ref input, ref invocation); Assert.Fail("Expected physical aggregate denial."); }
                catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind); }
            }
            else
            {
                var groups = Pcre2Runner.ProjectSliceCaptures(slots, 1, 7, starts, ends, ref input, ref invocation);
                Assert.Equal(slots, groups.Length);
                Assert.Equal(7 + groupBytes + scratchBytes, invocation.Budget.PeakWorkingBytes);
                Assert.Equal(7 + groupBytes, invocation.Ledger.ReservedBytes);
                if (slots > 0) Assert.Equal((1, 7, 1, 4), Edges(groups[0]));
                Pcre2SliceDriver.DetachProjectedGroups(groups, ref invocation);
                if (slots > 1) Assert.Equal((1, 5, 1, 3), Edges(groups[1]));
            }
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(7);
        }
    }

    [Fact]
    public void ReverseAndMissingEdgesRetainTheirOriginalPublicProjection()
    {
        var input = Utf8ValidatedInput.Create("x😀é"u8);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        int[] starts = [7, -1, 2, 5];
        int[] ends = [1, 7, 5, -1];
        var groups = Pcre2Runner.ProjectSliceCaptures(5, 1, 7, starts, ends, ref input, ref invocation);
        Assert.Equal(Pcre2GroupData.FromByteOffsets(input.Bytes, 1, 1, 7), groups[1]);
        Assert.False(groups[2].Success);
        Assert.Equal(Pcre2GroupData.FromByteOffsets(input.Bytes, 3, 2, 5), groups[3]);
        Assert.False(groups[3].HasUtf16Projection);
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
            var input = Utf8ValidatedInput.Create(bytes);
            try
            {
                var groups = Pcre2Runner.ProjectSliceCaptures(slots, 0, bytes.Length, starts, ends, ref input, ref invocation);
                Assert.Equal(6002, groups[0].EndOffsetInUtf16);
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
    [InlineData(65, 20)]
    [InlineData(1, 80000)]
    public void LiveCancellationDuringSortingAndWindowsUnwindsScratch(int slots, int scalars)
    {
        using var cancellation = new CancellationTokenSource();
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", scalars)));
        var starts = new int[Math.Max(0, slots - 1)];
        var ends = Enumerable.Repeat(bytes.Length, starts.Length).ToArray();
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 1UL << 20), Timeout.InfiniteTimeSpan, cancellation.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        invocation.CheckpointHook = _ => cancellation.Cancel();
        var input = Utf8ValidatedInput.Create(bytes);
        try { _ = Pcre2Runner.ProjectSliceCaptures(slots, 0, bytes.Length, starts, ends, ref input, ref invocation); Assert.Fail("Expected cancellation after entry."); }
        catch (OperationCanceledException failure) { Assert.Equal(cancellation.Token, failure.CancellationToken); }
        Assert.True(invocation.Budget.Steps >= 64);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void CursorCapturePublishesIndependentArraysAndRestoresParent()
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(é)(😀)");
        var compiled = regex.DebugCompiledProgram;
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(compiled.Operations.Match);
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram,
            Utf8ValidatedInput.Create("xé😀xé😀"u8), new Utf8BytePosition(0), compiled.Request, cancellation.Token);
        Assert.True(CursorInvocation(ref cursor).Ledger.TryReserve(7));
        Assert.True(cursor.MoveNext());
        var first = cursor.CaptureGroupsAt(cursor.Current.StartOffsetInBytes, true);
        Assert.Equal(7UL, CursorInvocation(ref cursor).Ledger.ReservedBytes);
        Assert.True(cursor.MoveNext());
        var second = cursor.CaptureGroupsAt(cursor.Current.StartOffsetInBytes, true);
        Assert.NotSame(first, second);
        Assert.Equal(7UL, CursorInvocation(ref cursor).Ledger.ReservedBytes);
        Assert.False(cursor.MoveNext());
        Assert.Equal(7UL, CursorInvocation(ref cursor).Ledger.ReservedBytes);
        Assert.Equal((3, 7, 2, 4), Edges(first[2]));
        Assert.Equal((10, 14, 6, 8), Edges(second[2]));
        CursorInvocation(ref cursor).Ledger.Release(7);
    }

    private static (int, int, int, int) Edges(Pcre2GroupData group)
        => (group.StartOffsetInBytes, group.EndOffsetInBytes, group.StartOffsetInUtf16, group.EndOffsetInUtf16);
}
