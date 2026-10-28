using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2InvocationWorkspaceTests
{
    private readonly record struct Pcre2GraphState(object? Value, int Number);

    private static class Pcre2StackAccessor<T> where T : struct
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_items")]
        internal static extern ref T[]? Items(ref Pcre2InvocationStateStack<T> stack);
    }

    private static ref int[]? Items(ref Pcre2InvocationStateStack<int> stack) => ref Pcre2StackAccessor<int>.Items(ref stack);

    private static Pcre2ResourceBudget Budget(ulong bytes, ulong steps = 0) => new(
        new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = bytes, MatchLimit = checked((uint)steps) },
        Timeout.InfiniteTimeSpan, collectDiagnostics: true);

    private static Pcre2BacktrackingWorkspace Acquire(int repeats, int captures, bool restore, ref Pcre2ResourceBudget budget)
    {
        var workspace = new Pcre2BacktrackingWorkspace();
        try
        {
            workspace.EnsureInitialized(repeats, captures, restore, ref budget);
            return workspace;
        }
        catch { workspace.Dispose(ref budget); throw; }
    }

    private static ulong Bytes<T>(T[]? values) where T : struct => values is null ? 0 : Utf8ConstructionBudget.ArrayBytes<T>(values.Length);

    private static ulong Physical(ref Pcre2BacktrackingWorkspace workspace) => checked(
        Bytes(workspace.RepeatState) + Bytes(workspace.CaptureState) +
        Bytes(workspace.CaptureRestoreGenerations) + Bytes(workspace.CaptureRestoreSlots) +
        Bytes(workspace.CaptureRestoreSnapshots) + Bytes(workspace.RepeatRestoreGenerations) +
        Bytes(workspace.RepeatRestoreIds) + Bytes(workspace.RepeatRestoreSnapshots));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, true)]
    [InlineData(0, 2, true)]
    [InlineData(3, 17, true)]
    [InlineData(129, 257, true)]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 2, false)]
    [InlineData(3, 17, false)]
    [InlineData(129, 257, false)]
    public void FixedVectorsAdmitTheirPhysicalAggregateBeforeAcquisition(int repeats, int captures, bool restoreSubroutines)
    {
        var funded = Budget(1UL << 30);
        var measured = Acquire(repeats, captures, restoreSubroutines, ref funded);
        var required = Physical(ref measured);
        Assert.Equal(required, funded.ReservedWorkingBytes);
        measured.Dispose(ref funded);
        Assert.Equal(0UL, funded.ReservedWorkingBytes);
        const ulong parent = 7;
        foreach (var allow in new[] { false, true })
        {
            var budget = Budget(parent + required - (allow || required == 0 ? 0UL : 1UL));
            budget.ReserveWorking(parent);
            scoped var workspace = default(Pcre2BacktrackingWorkspace);
            try
            {
                if (!allow && required > 0)
                {
                    try { workspace = Acquire(repeats, captures, restoreSubroutines, ref budget); Assert.Fail("Expected aggregate heap denial."); }
                    catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind); }
                    Assert.Equal(parent, budget.ReservedWorkingBytes);
                }
                else
                {
                    workspace = Acquire(repeats, captures, restoreSubroutines, ref budget);
                    Assert.Equal(required, Physical(ref workspace));
                    Assert.Equal(parent + required, budget.ReservedWorkingBytes);
                    workspace.Dispose(ref budget);
                    Assert.Equal(parent, budget.ReservedWorkingBytes);
                }
            }
            finally { budget.ReleaseWorking(parent); }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void PartialFixedAcquisitionRestoresParentAndKeepsSpentWork(ulong steps)
    {
        var budget = Budget(1UL << 20, steps);
        budget.ReserveWorking(7);
        try
        {
            try { _ = Acquire(1, 2, true, ref budget); Assert.Fail("Expected acquisition work denial."); }
            catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind); }
            Assert.Equal(7UL, budget.ReservedWorkingBytes);
            try { budget.ChargeCandidate(); Assert.Fail("Spent work cannot be refunded."); }
            catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind); }
        }
        finally { budget.ReleaseWorking(7); }
    }

    [Fact]
    public void NestedWorkspaceAndDifferentStacksShareOneOwner()
    {
        var budget = Budget(1UL << 20);
        var parent = Acquire(1, 2, true, ref budget);
        var parentBytes = Physical(ref parent);
        var frames = new Pcre2InvocationStateStack<int>(1024, true);
        var controls = new Pcre2InvocationStateStack<Pcre2AtomicCheckpoint>(1024, true);
        try
        {
            Assert.True(frames.TryPush(42, ref budget));
            Assert.True(controls.TryPush(new(1, 2), ref budget));
            var outerBytes = parentBytes + Utf8ConstructionBudget.ArrayBytes<int>(16) + Utf8ConstructionBudget.ArrayBytes<Pcre2AtomicCheckpoint>(16);
            Assert.Equal(outerBytes, budget.ReservedWorkingBytes);
            var nested = Acquire(3, 4, true, ref budget);
            try { Assert.Equal(outerBytes + Physical(ref nested), budget.ReservedWorkingBytes); }
            finally { nested.Dispose(ref budget); }
            Assert.Equal(outerBytes, budget.ReservedWorkingBytes);
            Assert.Equal(42, frames.Pop());
            Assert.Equal(new Pcre2AtomicCheckpoint(1, 2), controls.Pop());
        }
        finally { controls.Dispose(ref budget); frames.Dispose(ref budget); parent.Dispose(ref budget); }
        Assert.Equal(0UL, budget.ReservedWorkingBytes);
    }

    [Fact]
    public void SecondStackCannotSpendAnotherStacksReservation()
    {
        var extent = Utf8ConstructionBudget.ArrayBytes<int>(16);
        var budget = Budget(7 + 2 * extent - 1);
        budget.ReserveWorking(7);
        var first = new Pcre2InvocationStateStack<int>(1024, true);
        var second = new Pcre2InvocationStateStack<int>(1024, true);
        try
        {
            Assert.True(first.TryPush(10, ref budget));
            try { second.TryPush(20, ref budget); Assert.Fail("Expected simultaneous capacity denial."); }
            catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind); }
            Assert.Equal(0, second.Count);
            Assert.Equal(0, second.RentCount);
            Assert.Equal(7 + extent, budget.ReservedWorkingBytes);
            Assert.Equal(10, first[0]);
        }
        finally { second.Dispose(ref budget); first.Dispose(ref budget); budget.ReleaseWorking(7); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GrowthAdmitsOldAndNewCapacityAlongsideParentWorkspace(bool allow)
    {
        var control = Budget(1UL << 20);
        var measured = Acquire(1, 2, true, ref control);
        var parentBytes = Physical(ref measured);
        measured.Dispose(ref control);
        var oldBytes = Utf8ConstructionBudget.ArrayBytes<int>(16);
        var newBytes = Utf8ConstructionBudget.ArrayBytes<int>(32);
        var maximum = parentBytes + oldBytes + newBytes;
        var budget = Budget(maximum - (allow ? 0UL : 1UL));
        var parent = Acquire(1, 2, true, ref budget);
        var stack = new Pcre2InvocationStateStack<int>(1024, true);
        try
        {
            for (var i = 0; i < 16; i++) Assert.True(stack.TryPush(i, ref budget));
            if (allow)
            {
                Assert.True(stack.TryPush(16, ref budget));
                Assert.Equal(maximum, budget.HeapBytes);
                Assert.Equal(32, Items(ref stack)!.Length);
            }
            else
            {
                try { stack.TryPush(16, ref budget); Assert.Fail("Expected old/new overlap denial."); }
                catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind); }
                Assert.Equal(16, Items(ref stack)!.Length);
            }
            Assert.Equal(allow ? 17 : 16, stack.Count);
            Assert.Equal(allow ? 2 : 1, stack.RentCount);
            Assert.Equal(parentBytes + (allow ? newBytes : oldBytes), budget.ReservedWorkingBytes);
            for (var i = 0; i < 16; i++) Assert.Equal(i, stack[i]);
        }
        finally { stack.Dispose(ref budget); parent.Dispose(ref budget); }
        Assert.Equal(0UL, budget.ReservedWorkingBytes);
    }

    [Fact]
    public void WorkFailureDuringGrowthCopyRetainsOriginalValuesAndCapacity()
    {
        var budget = Budget(1UL << 20, 2);
        var stack = new Pcre2InvocationStateStack<int>(1024, true);
        try
        {
            for (var i = 0; i < 16; i++) Assert.True(stack.TryPush(i, ref budget));
            try { stack.TryPush(16, ref budget); Assert.Fail("Expected copy work denial."); }
            catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind); }
            Assert.Equal(16, stack.Count);
            Assert.Equal(16, Items(ref stack)!.Length);
            Assert.Equal(2, stack.RentCount);
            Assert.Equal(Utf8ConstructionBudget.ArrayBytes<int>(16), budget.ReservedWorkingBytes);
            for (var i = 0; i < 16; i++) Assert.Equal(i, stack[i]);
        }
        finally { stack.Dispose(ref budget); }
        Assert.Equal(0UL, budget.ReservedWorkingBytes);
    }

    [Fact]
    public void MaximumCountAndNonGrowingPushesKeepTheirOriginalBehavior()
    {
        var budget = Budget(1UL << 20);
        var stack = new Pcre2InvocationStateStack<int>(2, true);
        try
        {
            Assert.True(stack.TryPush(1, ref budget));
            var peak = budget.HeapBytes;
            Assert.True(stack.TryPush(2, ref budget));
            Assert.Equal(peak, budget.HeapBytes);
            Assert.False(stack.TryPush(3, ref budget));
            Assert.Equal(2, stack.Count);
            Assert.Equal(2, Items(ref stack)!.Length);
            Assert.Equal(1, stack.RentCount);
            Assert.Equal(2, stack.Pop());
            Assert.Equal(1, stack.Pop());
        }
        finally { stack.Dispose(ref budget); }
    }

    [Fact]
    public void ColdFiniteWorkspacesDoNotGrowSharedRetention()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var budget = Budget(1UL << 20);
            var workspace = Acquire(1, 2, true, ref budget);
            var stack = new Pcre2InvocationStateStack<int>(1024, true);
            try { Assert.True(stack.TryPush(1, ref budget)); }
            finally { stack.Dispose(ref budget); workspace.Dispose(ref budget); }
            Assert.Equal(0, Utf8ExactStatePool.RetainedBytes);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void WarmPublicVmMatchingReusesWorkspaceWithoutPerCallAllocation()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var regex = new Utf8Pcre2Regex("(*MARK:tag)(a+)\\1");
            Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
            // Warm the same call site as the measured batch. A few hundred
            // fast calls can finish before the runtime's delayed tiering starts.
            var warmup = System.Diagnostics.Stopwatch.StartNew();
            do { _ = MeasureVmBatch(regex, out var warmedCorrect); Assert.True(warmedCorrect); }
            while (warmup.ElapsedMilliseconds < 250);
            var allocated = MeasureVmBatch(regex, out var correct);
            Assert.True(correct);
            Assert.Equal(0, allocated);
            Assert.True(Utf8ExactStatePool.RetainedBytes <= Utf8ExactStatePool.MaximumRetainedBytes);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureVmBatch(Utf8Pcre2Regex regex, out bool correct)
    {
        correct = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++) correct &= regex.IsMatch("xaaaa"u8);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void PopAndTruncateReleaseBorrowedGraphReferences()
    {
        var budget = Budget(1UL << 20);
        var stack = new Pcre2InvocationStateStack<Pcre2GraphState>(1024, true);
        var graph = new object();
        try
        {
            for (var i = 0; i < 3; i++) Assert.True(stack.TryPush(new(graph, i), ref budget));
            Assert.Same(graph, stack.Pop().Value);
            var items = Pcre2StackAccessor<Pcre2GraphState>.Items(ref stack)!;
            Assert.Null(items[2].Value);
            stack.Truncate(0);
            Assert.Null(items[0].Value);
            Assert.Null(items[1].Value);
        }
        finally { stack.Dispose(ref budget); }
    }

    [Theory]
    // Linear captures omit unused undo storage; the fixed vector still includes its header.
    [InlineData("(a)\\1", "aa", 80)]
    [InlineData("(*MARK:tag)(a+)\\1", "aa", 512)]
    public void PublicVmHeapLimitIncludesPhysicalHeadersAndAllFixedVectors(string pattern, string subject, ulong limit)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None,
            default, new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = limit }, Timeout.InfiniteTimeSpan);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(subject)));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }
}
