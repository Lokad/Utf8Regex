using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WideRepeatStateTests
{
    [Theory]
    [InlineData(2147483647L)]
    [InlineData(2147483648L)]
    [InlineData(4294967294L)]
    [InlineData(long.MaxValue)]
    public void FiniteBoundsStayDistinctFromInfinityInCompactInstructions(long maximum)
    {
        var node = new Utf8SliceNode.RepeatNode(new Utf8SliceNode.LiteralNode('a', false), 0, maximum, false);
        var program = Assert.IsType<Utf8SliceProgram>(Utf8SliceLowerer.Lower(node, null, false, false, false));
        Assert.Equal(20, Unsafe.SizeOf<Utf8SliceInstruction>());
        Assert.Equal(0, program.Instructions[0].RepeatMinimum);
        Assert.Equal(maximum, program.Instructions[0].RepeatMaximum);
    }

    [Theory]
    [InlineData(2147483648L)]
    [InlineData(4294967294L)]
    [InlineData(4294967295L)] // The optional phase above the largest legal minimum.
    public void SavedRepeatCountsSurviveFrameAndJournalRollback(long count)
    {
        var invocation = new Utf8SliceInvocation(new(100000, 128, 1000000), Regex.InfiniteMatchTimeout, default);
        var stacks = new Utf8SliceRunner.ChunkedStacks();
        try
        {
            Assert.True(stacks.PushJournal(0, unchecked((int)count), 17, ref invocation));
            Assert.True(stacks.PushFrame(new() { Count = unchecked((int)count) }, ref invocation));
            Assert.True(stacks.PopFrame(out var frame));
            Assert.Equal(count, (long)unchecked((uint)frame.Count));
            int[] counts = [-1];
            int[] positions = [-1];
            stacks.TruncateJournal(0, counts, positions);
            Assert.Equal(count, (long)unchecked((uint)counts[0]));
            Assert.Equal(17, positions[0]);
        }
        finally { stacks.ReleaseAll(ref invocation); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    private delegate Utf8SliceMatchOutcome RunSeeded(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int start, int[] starts, int[] ends, int[] counts, int[] positions, int[] heads,
        bool history, bool nonempty, bool aligned, ref Utf8SliceRunner.ChunkedStacks stacks,
        ref Utf8SliceInvocation invocation);

    [Theory]
    [InlineData(4294967294L)] [InlineData(4294967295L)]
    public void LargestUnboundedMinimumFinishesOptionalEmptyPhaseWithoutReinitializing(long count)
    {
        // Seed the existing VM at the required/optional boundary, avoiding billions
        // of required iterations. UINT_MAX with a valid position is active state.
        var node = new Utf8SliceNode.RepeatNode(new Utf8SliceNode.EmptyNode(), 4294967294L, long.MaxValue, false)
            { GuardEmptyAfterMinimum = true };
        var outcome = RunAtCount(node, count, [], false);
        Assert.True(outcome.IsMatch);
        Assert.Equal(0, outcome.EndIndex);
    }

    [Fact]
    public void LazyFiniteRollbackKeepsUnsignedCountAndExactMaximum()
    {
        var node = new Utf8SliceNode.RepeatNode(new Utf8SliceNode.LiteralNode('a', false), 0, 4294967294L, true);
        var outcome = RunAtCount(node, 4294967293L, "aa"u8, true, 1);
        Assert.True(outcome.IsMatch);
        Assert.Equal(2, outcome.EndIndex);
    }

    private static Utf8SliceMatchOutcome RunAtCount(Utf8SliceNode node, long count, ReadOnlySpan<byte> input, bool nonempty, int start = 0)
    {
        var program = Assert.IsType<Utf8SliceProgram>(Utf8SliceLowerer.Lower(node, null, false, false, false));
        var run = typeof(Utf8SliceRunner).GetMethod("RunFrom", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<RunSeeded>();
        var invocation = new Utf8SliceInvocation(new(1000, 128, 1000000), Regex.InfiniteMatchTimeout, default);
        var stacks = new Utf8SliceRunner.ChunkedStacks();
        try
        {
            return run(program, input, start, [], [], [unchecked((int)count)], [0], [],
                false, nonempty, true, ref stacks, ref invocation);
        }
        finally
        {
            stacks.ReleaseAll(ref invocation);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }
}
