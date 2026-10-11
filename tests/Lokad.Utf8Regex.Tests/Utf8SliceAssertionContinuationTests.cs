using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8SliceAssertionContinuationTests
{
    private static Utf8SliceProgram Deep(int depth, bool negative = false, bool empty = false,
        bool rtl = false, bool repeats = false)
    {
        Utf8SliceNode node = empty ? new Utf8SliceNode.EmptyNode()
            : new Utf8SliceNode.CaptureNode(0, new Utf8SliceNode.LiteralNode('a', false));
        for (var index = 0; index < depth; index++)
        {
            if (repeats) node = new Utf8SliceNode.RepeatNode(node, 1, 1, false);
            node = new Utf8SliceNode.LookbehindNode(node, !negative);
        }
        return Utf8SliceLowerer.Lower(node, null, false, false, rtl)!;
    }

    public static IEnumerable<object[]> DeepCases()
    {
        foreach (var depth in new[] { 65, 4097, 16385 })
        foreach (var negative in new[] { false, true })
        foreach (var empty in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [depth, negative, empty, rtl];
    }

    [Theory]
    [MemberData(nameof(DeepCases))]
    public void DeepAssertionProgramsKeepEdgesCaptureCommitAndNegativeRollback(
        int depth, bool negative, bool empty, bool rtl)
    {
        // Private program graphs exercise the actual runner without widening
        // the separately retained public grammar admission.
        var program = Deep(depth, negative, empty, rtl);
        foreach (var edge in new[] { 0, 1 })
        {
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
                Regex.InfiniteMatchTimeout, CancellationToken.None);
            var result = Utf8SliceRunner.TryMatchAt(program, "a"u8, edge, starts, ends, true, ref invocation);
            var leafHit = empty || edge == 1;
            var expected = negative && (depth & 1) != 0 ? !leafHit : leafHit;
            Assert.Equal(expected, result.IsMatch);
            Assert.Equal(expected ? edge : -1, result.EndIndex);
            Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            Assert.True(invocation.Budget.PeakDepth >= depth);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
            if (!empty)
            {
                if (!negative && expected)
                {
                    Assert.Equal(0, Math.Min(starts[0], ends[0]));
                    Assert.Equal(1, Math.Max(starts[0], ends[0]));
                }
                else
                {
                    Assert.Equal(-1, starts[0]);
                    Assert.Equal(-1, ends[0]);
                }
            }
        }
    }

    [Theory]
    [InlineData(65)]
    [InlineData(4097)]
    public void WorkDepthAndAggregateStorageDenialsReleaseEveryChildAndKeepCallerLeases(int depth)
    {
        var program = Deep(depth, repeats: true);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.TryMatchAt(program, "a"u8, 1, starts, ends, true, ref baseline).IsMatch);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        var work = depth == 65 ? Enumerable.Range(1, checked((int)baseline.Budget.Steps - 1)).Select(static x => (ulong)x)
            : new[] { 1UL, 63UL, 64UL, 4095UL, 4096UL, baseline.Budget.Steps / 2, baseline.Budget.Steps - 1 };
        var depths = depth == 65 ? Enumerable.Range(1, checked((int)baseline.Budget.PeakDepth - 1)).Select(static x => (uint)x)
            : new[] { 1U, 63U, 64U, 4096U, baseline.Budget.PeakDepth - 1 };
        var limits = work.Select(static x => new Utf8ExecutionLimits(x, 0, 0))
            .Concat(depths.Select(static x => new Utf8ExecutionLimits(0, x, 0)))
            .Concat(new[] { new Utf8ExecutionLimits(0, 0, 1),
                new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.FrameReserveBytes - 1),
                new Utf8ExecutionLimits(0, 0, baseline.Budget.PeakWorkingBytes - 1) });
        foreach (var limit in limits)
        {
            var denied = new Utf8SliceInvocation(limit, Regex.InfiniteMatchTimeout, CancellationToken.None);
            var result = Utf8SliceRunner.TryMatchAt(program, "a"u8, 1, starts, ends, true, ref denied);
            Assert.False(result.IsMatch);
            Assert.Equal(limit.MaxWorkSteps != 0 ? Utf8ExecutionLimitKind.WorkSteps : limit.MaxDepth != 0
                ? Utf8ExecutionLimitKind.Depth : Utf8ExecutionLimitKind.WorkingBytes, result.Breach);
            Assert.Equal(0UL, denied.Ledger.ReservedBytes);
            Assert.All(starts, static value => Assert.Equal(-1, value));
            Assert.All(ends, static value => Assert.Equal(-1, value));
        }
        var recovery = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(recovery.Ledger.TryReserve(7));
        Assert.True(Utf8SliceRunner.TryMatchAt(program, "a"u8, 1, starts, ends, true, ref recovery).IsMatch);
        Assert.Equal(7UL, recovery.Ledger.ReservedBytes);
        recovery.Ledger.Release(7);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringDescentAndUnwindRestoresCaptureAndRepeatState(bool unwind)
    {
        var program = Deep(4097, repeats: true);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.TryMatchAt(program, "a"u8, 1, starts, ends, true, ref baseline).IsMatch);
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        var entered = false;
        canceled.CheckpointHook = _ =>
        {
            if (!unwind || canceled.Budget.PeakDepth >= baseline.Budget.PeakDepth)
            {
                entered = true;
                source.Cancel();
            }
        };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.TryMatchAt(program, "a"u8, 1, starts, ends, true, ref canceled));
        Assert.True(entered);
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        Assert.All(starts, static value => Assert.Equal(-1, value));
        Assert.All(ends, static value => Assert.Equal(-1, value));
    }

    [Fact]
    public void DeadlineDuringUnwindKeepsItsAuthorityAndReturnsOwnedArrays()
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var program = Deep(65, repeats: true);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.TryMatchAt(program, "a"u8, 1, starts, ends, true, ref baseline).IsMatch);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, TimeSpan.FromMilliseconds(200), CancellationToken.None);
        var entered = false;
        invocation.CheckpointHook = _ =>
        {
            if (!entered && invocation.Budget.PeakDepth >= baseline.Budget.PeakDepth)
            {
                entered = true;
                clock.Advance(250);
            }
        };
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8SliceRunner.TryMatchAt(program, "a"u8, 1, starts, ends, true, ref invocation));
        Assert.True(entered);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.All(starts, static value => Assert.Equal(-1, value));
        Assert.All(ends, static value => Assert.Equal(-1, value));
    }

    private delegate bool PushFrame(ref Utf8SliceFrame[][] tables, ref int count, int chunkCapacity,
        Utf8SliceFrame value, ref Utf8SliceInvocation invocation, ref ulong reservedBytes, out Utf8ExecutionLimitKind breach);

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 64)]
    [InlineData(true, 64)]
    public void StackTableGrowthAdmitsOverlapBeforeAllocationAndRecordsItsPeak(bool allow, int chunkCapacity)
    {
        var method = typeof(Utf8SliceRunner).GetNestedType("ChunkedStacks", BindingFlags.NonPublic)!
            .GetMethod("Push", BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(typeof(Utf8SliceFrame));
        var push = method.CreateDelegate<PushFrame>();
        // The small-chunk control makes old/new table overlap the actual peak;
        // the 64-frame row also exercises the production chunk extent.
        var chunkBytes = 32UL + ((ulong)(28 * chunkCapacity) + 15) / 16 * 16;
        var tables = Enumerable.Range(0, 4).Select(_ => new Utf8SliceFrame[chunkCapacity]).ToArray();
        var original = tables;
        var count = 4 * chunkCapacity;
        var existingBytes = chunkBytes * 4 + 64;
        var peak = existingBytes + 96 + chunkBytes;
        var limit = allow ? peak : peak - 1;
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, limit), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(invocation.Ledger.TryReserve(existingBytes));
        var reserved = existingBytes;
        // Warm reflection/delegate dispatch before measuring an allocation denial.
        var warmTables = Array.Empty<Utf8SliceFrame[]>();
        var warmCount = 0;
        var warmReserved = 0UL;
        var warm = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.False(push(ref warmTables, ref warmCount, 64, default, ref warm, ref warmReserved, out _));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var accepted = push(ref tables, ref count, chunkCapacity, default, ref invocation, ref reserved, out var breach);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(allow, accepted);
        Assert.Equal(allow ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, breach);
        Assert.Equal(4 * chunkCapacity + (allow ? 1 : 0), count);
        if (allow)
        {
            Assert.Equal(8, tables.Length);
            Assert.Equal(peak, invocation.Budget.PeakWorkingBytes);
            Assert.Equal(existingBytes + 32 + chunkBytes, reserved);
        }
        else
        {
            Assert.Same(original, tables);
            Assert.Equal(existingBytes, reserved);
            Assert.Equal(0, allocated);
        }
        Assert.Equal(reserved, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(reserved);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    private static PushFrame GrowthPush() => (typeof(Utf8SliceRunner).GetNestedType("ChunkedStacks", BindingFlags.NonPublic)
        ?.GetMethod("Push", BindingFlags.NonPublic | BindingFlags.Static)?.MakeGenericMethod(typeof(Utf8SliceFrame))
        ?? throw new InvalidOperationException()).CreateDelegate<PushFrame>();

    [Fact]
    public void EveryTableCopyWorkCutoffLeavesTheOriginalStackAndLoanIntact()
    {
        var push = GrowthPush();
        // 512 references take two bounded copy checkpoints, plus acquisition
        // checkpoints before the new table and before its new leaf.
        for (ulong cap = 1; cap <= 4; cap++)
        {
            var tables = Enumerable.Range(0, 512).Select(_ => new Utf8SliceFrame[1]).ToArray();
            var original = tables;
            var count = 512;
            var reserved = 13UL + 4128 + 512 * 64;
            var parent = reserved;
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(parent));
            Assert.Equal(cap == 4, push(ref tables, ref count, 1, default, ref invocation, ref reserved, out var breach));
            Assert.Equal(cap == 4 ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, breach);
            if (cap != 4)
            {
                Assert.Same(original, tables);
                Assert.Equal(512, count);
                Assert.Equal(parent, reserved);
                Assert.Equal(parent, invocation.Ledger.ReservedBytes);
            }
            else
            {
                Assert.Equal(513, count);
                Assert.Equal(parent + 8224 + 64 - 4128, reserved);
                Assert.Equal(parent + 8224 + 64, invocation.Budget.PeakWorkingBytes);
                for (var index = 0; index < original.Length; index++) Assert.Same(original[index], tables[index]);
            }
            invocation.Ledger.Release(reserved);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TableCopyCancellationAndDeadlineRollBackGrowth(bool deadline)
    {
        var push = GrowthPush();
        var tables = Enumerable.Range(0, 512).Select(_ => new Utf8SliceFrame[1]).ToArray();
        var original = tables;
        var count = 512;
        var reserved = 13UL + 4128 + 512 * 64;
        var parent = reserved;
        using var source = new CancellationTokenSource();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads < 3 ? 0 : 1);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0),
            deadline ? TimeSpan.FromMilliseconds(1) : Regex.InfiniteMatchTimeout, source.Token);
        if (!deadline)
        {
            for (var padding = 0; padding < 63; padding++) invocation.TryChargeStep();
            invocation.CheckpointHook = _ => source.Cancel();
        }
        Assert.True(invocation.Ledger.TryReserve(parent));
        if (deadline) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            push(ref tables, ref count, 1, default, ref invocation, ref reserved, out _));
        else
        {
            var error = Assert.Throws<OperationCanceledException>(() =>
                push(ref tables, ref count, 1, default, ref invocation, ref reserved, out _));
            Assert.Equal(source.Token, error.CancellationToken);
        }
        Assert.Same(original, tables);
        Assert.Equal(512, count);
        Assert.Equal(parent, reserved);
        Assert.Equal(parent, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(reserved);
    }

    [Fact]
    public void StackCountExhaustionRejectsBeforeAcquisition()
    {
        var push = GrowthPush();
        var tables = Array.Empty<Utf8SliceFrame[]>();
        var count = int.MaxValue;
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var reserved = 13UL;
        Assert.False(push(ref tables, ref count, 1, default, ref invocation, ref reserved, out var breach));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, breach);
        Assert.Equal(int.MaxValue, count);
        Assert.Empty(tables);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0UL, invocation.Budget.Steps);
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var pattern in new[] { @"(?<=a(?=(?<x>b)))b", @"(?<=a(?<!b))b", @"(?<=(?<=a)b)c",
            @"(?<!a(?<=(?<x>a)))b", @"(?<=(?<x>a+))b\k<x>", @"(?<x>a)(?<=\k<x>)b",
            @"(?<x>a)+(?<=(?<-x>a))b", @"(?<=(a|ab))b", @"(?<=(?<x>a{1,3}))b",
            @"(?<!(?<x>a{1,3}))b", @"(?<=a(?=b))(?=b)b", @"(?m)(?<=^a)b$" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicOperationsKeepNestedDirectionsChoicesCapturesBalancingAndUnicode(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "a", "b", "ab", "abc", "aaba", "aaabaaa", "xab ab abc", "ab\n", "ab\nrest",
             "\U0001f600abc\U0001f600", "éabc中"], options);
}
