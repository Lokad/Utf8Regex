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

    private static Pcre2ResourceBudget Budget(ulong bytes) => new(
        new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = bytes },
        Timeout.InfiniteTimeSpan, collectDiagnostics: true);

    private static Pcre2BacktrackingWorkspace Acquire(int repeats, int captures, bool restoreRepeats, bool restoreCaptures,
        ref Pcre2ResourceBudget budget)
    {
        var workspace = new Pcre2BacktrackingWorkspace();
        try
        {
            workspace.EnsureInitialized(repeats, captures, restoreRepeats, restoreCaptures, ref budget);
            return workspace;
        }
        catch { workspace.Dispose(ref budget); throw; }
    }

    private static ulong Bytes<T>(T[]? values) where T : struct => values is null ? 0 : Utf8ConstructionBudget.ArrayBytes<T>(values.Length);

    private static ulong Physical(ref Pcre2BacktrackingWorkspace workspace) => checked(
        Bytes(workspace.RepeatState) + Bytes(workspace.CaptureState) +
        Bytes(workspace.CaptureRestoreGenerations) + Bytes(workspace.RepeatRestoreGenerations));

    [Theory]
    [InlineData("cancellation")]
    [InlineData("deadline")]
    [InlineData("working denial")]
    public void EmptyWorkspaceKeepsOriginalAuthorityAndParentLoan(string interruption)
    {
        long now = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var token = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 8, 0),
            interruption == "deadline" ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan, token.Token);
        var budget = new Pcre2ResourceBudget(new() { HeapLimitInBytes = 8 }, invocation, false);
        budget.ReserveWorking(7);
        var workspace = new Pcre2BacktrackingWorkspace();
        try
        {
            workspace.EnsureInitialized(0, 0, true, true, ref budget);
            Assert.Equal(0UL, Physical(ref workspace));
            if (interruption == "cancellation") token.Cancel();
            else if (interruption == "deadline") now = 101;
            else
            {
                try { budget.ReserveWorking(2); Assert.Fail("Expected working denial."); }
                catch (Pcre2MatchException error) { Assert.Equal(Pcre2ErrorKind.HeapLimit, error.ErrorKind); }
            }
            Exception? failure = null;
            try { workspace.EnsureInitialized(0, 0, true, true, ref budget); }
            catch (Exception error) { failure = error; }
            if (interruption == "cancellation")
                Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            else Assert.Equal(interruption == "deadline" ? Pcre2ErrorKind.Timeout : Pcre2ErrorKind.HeapLimit,
                Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        }
        finally
        {
            workspace.Dispose(ref budget);
            Assert.Equal(7UL, budget.ReservedWorkingBytes);
            budget.ReleaseWorking(7);
        }
        Assert.Equal(0UL, budget.ReservedWorkingBytes);
    }

    [Theory]
    [InlineData(0, 0, true, true)]
    [InlineData(1, 0, true, true)]
    [InlineData(0, 2, true, false)]
    [InlineData(3, 17, true, true)]
    [InlineData(3, 17, true, false)]
    [InlineData(129, 257, true, true)]
    [InlineData(0, 0, false, false)]
    [InlineData(1, 0, false, false)]
    [InlineData(0, 2, false, false)]
    [InlineData(3, 17, false, false)]
    [InlineData(129, 257, false, false)]
    public void FixedVectorsAdmitTheirPhysicalAggregateBeforeAcquisition(int repeats, int captures,
        bool restoreRepeats, bool restoreCaptures)
    {
        var funded = Budget(1UL << 30);
        var measured = Acquire(repeats, captures, restoreRepeats, restoreCaptures, ref funded);
        Assert.Equal(restoreCaptures && captures != 0, measured.CaptureRestoreGenerations is not null);
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
                    try { workspace = Acquire(repeats, captures, restoreRepeats, restoreCaptures, ref budget); Assert.Fail("Expected aggregate heap denial."); }
                    catch (Pcre2MatchException failure) { Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind); }
                    Assert.Equal(parent, budget.ReservedWorkingBytes);
                }
                else
                {
                    workspace = Acquire(repeats, captures, restoreRepeats, restoreCaptures, ref budget);
                    Assert.Equal(required, Physical(ref workspace));
                    Assert.Equal(parent + required, budget.ReservedWorkingBytes);
                    workspace.Dispose(ref budget);
                    Assert.Equal(parent, budget.ReservedWorkingBytes);
                }
            }
            finally { budget.ReleaseWorking(parent); }
        }
    }

    [Fact]
    public void NestedWorkspaceAndDifferentStacksShareOneOwner()
    {
        var budget = Budget(1UL << 20);
        var parent = Acquire(1, 2, true, true, ref budget);
        var parentBytes = Physical(ref parent);
        var frames = new Pcre2InvocationStateStack<int>(1024, true);
        var controls = new Pcre2InvocationStateStack<Pcre2AtomicCheckpoint>(1024, true);
        try
        {
            Assert.True(frames.TryPush(42, ref budget));
            Assert.True(controls.TryPush(new(1, 2), ref budget));
            var outerBytes = parentBytes + Utf8ConstructionBudget.ArrayBytes<int>(16) + Utf8ConstructionBudget.ArrayBytes<Pcre2AtomicCheckpoint>(16);
            Assert.Equal(outerBytes, budget.ReservedWorkingBytes);
            var nested = Acquire(3, 4, true, true, ref budget);
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
        var measured = Acquire(1, 2, true, true, ref control);
        var parentBytes = Physical(ref measured);
        measured.Dispose(ref control);
        var oldBytes = Utf8ConstructionBudget.ArrayBytes<int>(16);
        var newBytes = Utf8ConstructionBudget.ArrayBytes<int>(32);
        var maximum = parentBytes + oldBytes + newBytes;
        var budget = Budget(maximum - (allow ? 0UL : 1UL));
        var parent = Acquire(1, 2, true, true, ref budget);
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
    public void ShallowRecursiveSearchFitsAFundedRetryBuffer()
    {
        using var regex = new Utf8Pcre2Regex(@"^((.)(?1)\2|.?)$", policy: new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1400 }
        });

        Assert.True(regex.IsMatch("redder"u8));
        Assert.False(regex.IsMatch("reddet"u8));
        Assert.True(regex.IsMatch("😀éxé😀"u8));
    }

    [Fact]
    public void ColdFiniteWorkspacesDoNotGrowSharedRetention()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var budget = Budget(1UL << 20);
            var workspace = Acquire(1, 2, true, true, ref budget);
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

    [Fact]
    public void SliceMemoryCursorReturnsItsFrameLeafAndKeepsPublishedCaptures()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            using var regex = new Utf8Pcre2Regex(@"\A(?<prefix>.*)(?<end>[bc])\z");
            Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
            using var cursor = regex.CreateMatchCursor("é😀b"u8.ToArray());
            var alias = cursor;
            Assert.True(cursor.MoveNext(out var result));

            alias.Dispose();

            Assert.Equal(Utf8SliceRunner.FrameReserveBytes, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);
            var end = result.Groups[2];
            Assert.Equal((6, 7), (end.StartOffsetInBytes, end.EndOffsetInBytes));
            Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void SliceMatchWithCallerSlotsReturnsItsFrameLeaf()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            using var regex = new Utf8Pcre2Regex(@"\A(?<prefix>.*)(?<end>[bc])\z");
            Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
            Span<Utf8Pcre2CaptureRange> slots = stackalloc Utf8Pcre2CaptureRange[3];

            var result = regex.Match("é😀b"u8, captures: slots);

            Assert.True(result.Success);
            Assert.Equal((6, 7), (slots[2].StartOffsetInBytes, slots[2].EndOffsetInBytes));
            Assert.Equal(Utf8SliceRunner.FrameReserveBytes, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);
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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = (limit) == 0 ? null : (limit)
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(subject)));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }
}
