using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceLoweringContinuationTests
{
    private static Utf8SliceNode Deep(int depth, int shape, Utf8SliceNode leaf)
    {
        var node = leaf;
        for (var index = 0; index < depth; index++)
            node = shape switch
            {
                0 => new Utf8SliceNode.CaptureNode(0, node),
                1 => new Utf8SliceNode.ConcatNode([node]),
                2 => new Utf8SliceNode.AtomicNode(node),
                3 => new Utf8SliceNode.RepeatNode(node, 1, 1, false),
                4 => new Utf8SliceNode.LookaheadNode(node, true),
                _ => new Utf8SliceNode.LookbehindNode(node, true),
            };
        return node;
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var depth in new[] { 65, 16385 })
        foreach (var shape in Enumerable.Range(0, 6))
        foreach (var rtl in new[] { false, true })
        foreach (var empty in new[] { false, true })
            yield return [depth, shape, rtl, empty];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeepConstructionKeepsInstructionCountsEmptyFactsAndAssertionDirections(int depth, int shape, bool rtl, bool empty)
    {
        // Build trees directly: the retained public grammar bound is separate.
        // Record nodes must never require recursive structural hashing here.
        Utf8SliceNode leaf = empty ? new Utf8SliceNode.EmptyNode() : new Utf8SliceNode.LiteralNode('é', false);
        var program = Utf8SliceLowerer.Lower(Deep(depth, shape, leaf), null, false, false, rtl);
        Assert.NotNull(program);
        Assert.Equal(empty || shape >= 4, program.CanMatchEmpty);
        Assert.Equal(rtl, program.RightToLeft);
        var pending = new Stack<Utf8SliceProgram>();
        pending.Push(program);
        var programs = 0;
        var totalInstructions = 0;
        while (pending.TryPop(out var current))
        {
            programs++;
            totalInstructions += current.Instructions.Length;
            Assert.Equal(Utf8SliceInstructionKind.Accept, current.Instructions[^1].Kind);
            for (var index = 0; index < current.Instructions.Length; index++)
            {
                var instruction = current.Instructions[index];
                if (instruction.Kind is Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or
                    Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy or Utf8SliceInstructionKind.RepeatEnd)
                    Assert.InRange(instruction.A, 0, current.Instructions.Length - 1);
            }
            foreach (var assertion in current.Assertions)
            {
                Assert.Equal(shape == 5, assertion.IsLookbehind);
                Assert.Equal(assertion.IsLookbehind, assertion.Body.RightToLeft);
                Assert.Equal(assertion.MinLengthBytes, assertion.MaxLengthBytes);
                Assert.Equal(assertion.Body.CanMatchEmpty ? 0 : empty ? 0 : 2, assertion.MinLengthBytes);
                pending.Push(assertion.Body);
            }
        }
        var leafCount = empty ? 1 : 2; // Accept, and a literal when nonempty.
        var expectedInstructions = shape switch
        {
            0 => depth * 2 + leafCount,
            1 => leafCount,
            2 => depth * 4 + leafCount,
            3 => depth * 3 + leafCount,
            4 => rtl ? (depth - 1) * 4 + 2 + leafCount : depth * 4 + leafCount,
            _ => depth * 2 + leafCount,
        };
        Assert.Equal(expectedInstructions, totalInstructions);
        Assert.Equal(shape == 5 ? depth + 1 : shape == 4 && rtl ? 2 : 1, programs);
        Assert.Equal(shape == 0 ? 1 : 0, program.GroupCount);
        Assert.Equal(shape == 3 ? depth : 0, program.RepeatCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeepLookbehindWidthFactsSaturateAndRetainCaptureMarkers(bool capture)
    {
        Utf8SliceNode node = new Utf8SliceNode.LiteralNode('é', false);
        for (var index = 0; index < 16385; index++) node = new Utf8SliceNode.RepeatNode(node, 2, 2, false);
        if (capture) node = new Utf8SliceNode.CaptureNode(0, node);
        var program = Utf8SliceLowerer.Lower(new Utf8SliceNode.LookbehindNode(node, true), null, false, false, false);
        Assert.NotNull(program);
        var assertion = Assert.Single(program.Assertions);
        Assert.Equal(int.MaxValue, assertion.MinLengthBytes);
        Assert.Equal(int.MaxValue, assertion.MaxLengthBytes);
        Assert.Equal(capture, program.SawCapturingLookbehind);
        Assert.False(program.HasNonFixedLookbehind);
        Assert.Equal(capture ? 1 : 0, program.GroupCount);
    }

    [Theory]
    [InlineData(65, 0)]
    [InlineData(4097, 0)]
    [InlineData(65, 2)]
    [InlineData(4097, 2)]
    [InlineData(65, 3)]
    [InlineData(4097, 3)]
    [InlineData(65, 4)]
    [InlineData(4097, 4)]
    public void DeepFlatProgramsRespectWorkStorageCancellationAndRecovery(int depth, int shape)
    {
        var root = Deep(depth, shape, new Utf8SliceNode.LiteralNode('é', false));
        // Assertions are zero-width; a following literal consumes the subject.
        if (shape == 4) root = new Utf8SliceNode.ConcatNode([root, new Utf8SliceNode.LiteralNode('é', false)]);
        var program = Utf8SliceLowerer.Lower(root, null, false, false, false);
        Assert.NotNull(program);
        var input = "é"u8.ToArray();
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.IsMatch(program, input, starts, ends, ref baseline).IsMatch);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        if (shape == 0)
        {
            Assert.Equal(new[] { 0 }, starts);
            Assert.Equal(new[] { 2 }, ends);
        }
        var cutoffs = depth == 65 ? Enumerable.Range(1, checked((int)baseline.Budget.Steps - 1)).Select(static value => (ulong)value)
            : new[] { 1UL, 2UL, 63UL, 64UL, 127UL, 128UL, 4095UL, 4096UL, baseline.Budget.Steps - 1 };
        foreach (var work in cutoffs)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var limited = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SliceRunner.IsMatch(program, input, starts, ends, ref limited).Breach);
            Assert.Equal(0UL, limited.Ledger.ReservedBytes);
        }
        var storage = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.TrailReserveBytes - 1),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, Utf8SliceRunner.IsMatch(program, input, starts, ends, ref storage).Breach);
        Assert.Equal(0UL, storage.Ledger.ReservedBytes);
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        canceled.CheckpointHook = steps => { if (steps >= 64) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() => Utf8SliceRunner.IsMatch(program, input, starts, ends, ref canceled));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        var recovered = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.IsMatch(program, input, starts, ends, ref recovered).IsMatch);
        Assert.Equal(0UL, recovered.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AdmittedCanonicalNestingRetainsCompletePublicOperations(bool compiled, bool rtl)
    {
        var pattern = string.Concat(Enumerable.Repeat("(?:", 60)) + @"(?<x>a+?)(?=b)b" + new string(')', 60);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "ab", "aab", "xxaabb", "\U0001f600ab\U0001f600", "aa", "bb", "ababa"], options);
    }
}
