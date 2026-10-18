using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WideRepeatStateTests
{
    [Theory]
    [InlineData("a{0,2147483647}", 2147483647L)]
    [InlineData("a{2147483647}", 2147483647L)]
    [InlineData("[a-z]{0,2147483647}?", 2147483647L)]
    [InlineData("(ab){0,2147483647}", 2147483647L)]
    [InlineData("😀{0,2147483647}", 2147483647L)]
    [InlineData(@"(?i)\265{0,2147483647}", 2147483647L)]
    [InlineData(@"(?i)\178{0,2147483647}", 2147483647L)]
    [InlineData(@"(?i)\128{0,2147483647}", 2147483647L)]
    [InlineData("a{0,}", long.MaxValue)]
    [InlineData("a{2147483647,}", long.MaxValue)]
    [InlineData("[a-z]*?", long.MaxValue)]
    [InlineData("(ab)+", long.MaxValue)]
    [InlineData(@"(?i)\265+", long.MaxValue)]
    [InlineData(@"(?i)\178+", long.MaxValue)]
    [InlineData("(?:a*){1}", long.MaxValue)]
    [InlineData("(?:a*){1,1}?", long.MaxValue)]
    public void CoreGrammarKeepsFiniteMaximumDistinctFromInfinity(string pattern, long maximum)
    {
        var tree = Assert.IsType<Utf8OwnedRegexTree>(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None));
        var program = Assert.IsType<Utf8SliceProgram>(Utf8OwnedTreeSliceLowerer.TryLower(tree, RegexOptions.None));
        var repeat = Assert.Single(program.Instructions, static instruction => instruction.Kind is
            Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy);
        Assert.Equal(maximum, repeat.RepeatMaximum);
    }

    [Theory]
    [InlineData("a{0,2147483647}", false)]
    [InlineData("a{0,2147483647}?", false)]
    [InlineData("a{0,}", true)]
    [InlineData("a{0,}?", true)]
    public void CoreFiniteMaximumStopsAtItsBoundary(string pattern, bool expected)
    {
        var tree = Assert.IsType<Utf8OwnedRegexTree>(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None));
        var program = Assert.IsType<Utf8SliceProgram>(Utf8OwnedTreeSliceLowerer.TryLower(tree, RegexOptions.None));
        // Seed a completed consuming iteration. Unbounded counts retain only
        // the optional phase (min+1); finite counts retain the exact maximum.
        Assert.Equal(expected, RunAtCount(program, expected ? 1 : int.MaxValue, "aa"u8, true, 1).IsMatch);
    }

    [Theory]
    [InlineData("a{0,2147483647}", false)]
    [InlineData("[a-z]{0,2147483647}", false)]
    [InlineData("(?:a){0,2147483647}", false)]
    [InlineData("a*", true)]
    [InlineData("[a-z]*", true)]
    [InlineData("(?:a)*", true)]
    public void SentinelBasedDirectQueriesDeclineFiniteMaximum(string pattern, bool expected)
    {
        var tree = Assert.IsType<Utf8OwnedRegexTree>(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None));
        Assert.Equal(expected, Utf8FallbackTreeQuery.TryRepeat(tree.Root, out _, out _, out _));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.RightToLeft | RegexOptions.Compiled)]
    public void FiniteMaximumKeepsPublicScalarCapturesAndResourceControls(RegexOptions options)
    {
        const string pattern = "(?<x>😀|a){0,2147483647}";
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(2, regex.Count("😀a"u8));
        var retained = regex.MatchDetailed("😀a"u8);
        Assert.Equal((0, 5), (retained.IndexInBytes, retained.LengthInBytes));
        Assert.Equal((options & RegexOptions.RightToLeft) == 0 ? "a" : "😀", retained.GetGroup("x").GetValueString());
        Assert.Equal("XX", regex.ReplaceToString("😀a"u8, "X"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => regex.Count("😀a"u8, cancellation.Token));
        var denied = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count("😀a"u8)).ResourceKind);
        Assert.Equal(2, regex.Count("😀a"u8));
        Assert.Equal("😀a", retained.GetValueString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiniteNonBacktrackingExpansionRetainsConstructionAdmission(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        // A finite consuming repeat requires expansion in this engine. It must
        // fail admission promptly, rather than silently become a compact '*'.
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            new Utf8Regex("(?<x>😀|a){0,2147483647}", options));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, failure.ResourceKind);
        var unbounded = new Utf8Regex("(?<x>😀|a)*", options);
        Assert.Equal(2, unbounded.Count("😀a"u8));
        // Existing compact zero-width finite repeats still need no expansion.
        var empty = new Utf8Regex("(?<x>){0,2147483647}", options);
        Assert.Equal(3, empty.Count("😀a"u8));
        Assert.True(empty.MatchDetailed("😀a"u8).GetGroup("x").Success);
    }

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
        return RunAtCount(program, count, input, nonempty, start);
    }

    private static Utf8SliceMatchOutcome RunAtCount(Utf8SliceProgram program, long count, ReadOnlySpan<byte> input, bool nonempty, int start = 0)
    {
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
