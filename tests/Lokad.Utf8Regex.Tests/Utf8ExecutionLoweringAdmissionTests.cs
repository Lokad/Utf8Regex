using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ExecutionLoweringAdmissionTests
{
    [Fact]
    public void CanonicalClassPayloadAndPreparedInstructionsKeepExactAsciiMembership()
    {
        foreach (var (pattern, accepted) in new[]
        {
            ("[a-c-[b]]", "ac"),
            ("[a-z-[b-y-[m]]]", "amz"),
            ("[\\t-\\r ]", "\t\n\v\f\r "),
            ("(?s:.)", new string(Enumerable.Range(0, 128).Select(static value => (char)value).ToArray())),
        })
        {
            var sourceAnalysis = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant);
            var analysis = Lokad.Utf8Regex.Internal.Planning.Utf8RegexPreparer.Prepare(sourceAnalysis);
            Assert.NotNull(analysis.ExecutionTree);
            Assert.NotNull(analysis.ExecutionProgram);
            var sourceNode = sourceAnalysis.SemanticRegex.RuntimeTree!.Root;
            while (sourceNode.ScalarClass is null && sourceNode.ChildCount == 1) sourceNode = sourceNode.Child(0);
            var source = sourceNode.ScalarClass;
            Assert.NotNull(source);
            var executionNode = analysis.ExecutionTree.Root;
            while (executionNode.Kind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group && executionNode.Children.Count == 1)
                executionNode = executionNode.Children[0];
            Assert.Same(source, executionNode.Payload);
            Assert.Equal(string.Empty, executionNode.Text);
            var atom = Assert.Single(analysis.ExecutionProgram.Instructions.Where(static instruction =>
                instruction.Kind == Utf8ExecutionInstructionKind.Enter && instruction.NodeKind == Utf8ExecutionNodeKind.Set));
            Assert.True(atom.CharClass.HasValue);
            Assert.Same(source, atom.Payload);
            for (var value = 0; value < 128; value++)
            {
                var expected = accepted.Contains((char)value);
                Assert.Equal(expected, atom.CharClass.Value.Contains((byte)value));
                Assert.Equal(expected, Utf8ExecutionInterpreter.TryMatchPrefix([(byte)value],
                    analysis.ExecutionProgram, 0, Utf8ExecutionDeadline.Infinite, out var length));
                Assert.Equal(expected ? 1 : 0, length);
            }
        }
        var dot = Utf8FrontEnd.Compile("(?s:.{1,2})", RegexOptions.CultureInvariant);
        Assert.NotNull(dot.ExecutionProgram);
        var compact = Assert.Single(dot.ExecutionProgram.Instructions.Where(static instruction =>
            instruction.Kind == Utf8ExecutionInstructionKind.Enter && instruction.NodeKind == Utf8ExecutionNodeKind.Loop));
        Assert.True(compact.CharClass.HasValue);
        Assert.Equal(string.Empty, compact.Text);
        Assert.True(Utf8ExecutionInterpreter.TryMatchPrefix("ab"u8, dot.ExecutionProgram, 0,
            Utf8ExecutionDeadline.Infinite, out var consumed));
        Assert.Equal(2, consumed);
        Assert.False(Utf8ExecutionInterpreter.TryMatchPrefix("😀"u8, dot.ExecutionProgram, 0,
            Utf8ExecutionDeadline.Infinite, out consumed));
        Assert.Equal(0, consumed);
        Assert.Equal(4, new Utf8Regex("(?s:.{1,2})", RegexOptions.CultureInvariant).Match("😀"u8).LengthInBytes);
        using var scope = Utf8ConstructionScope.Enter();
        using var data = Utf8ConstructionBuffer<int>.Create(scope.Budget);
        data.AddRange([0, 0, 0]);
        var empty = Utf8ScalarClass.Create(data);
        var emptyProgram = Utf8ExecutionProgramLowerer.Lower(new(new(Utf8ExecutionNodeKind.Set,
            RegexOptions.None, [], empty, '\0', 0, 1, 1)));
        Assert.NotNull(emptyProgram);
        Assert.True(emptyProgram.Instructions[0].CharClass.HasValue);
        Assert.True(emptyProgram.Instructions[0].CharClass.Value.IsEmpty);
        Assert.False(Utf8ExecutionInterpreter.TryMatchPrefix("a"u8, emptyProgram, 0,
            Utf8ExecutionDeadline.Infinite, out _));
        var literal = Utf8ExecutionProgramLowerer.Lower(new(new(Utf8ExecutionNodeKind.One,
            RegexOptions.None, [], string.Empty, 'a', 0, 1, 1)));
        Assert.NotNull(literal);
        Assert.False(literal.Instructions[0].CharClass.HasValue);
        var unproved = Utf8FrontEnd.Compile("\\p{L}+", RegexOptions.CultureInvariant);
        Assert.NotNull(unproved.ExecutionProgram);
        var unprovedClass = Assert.Single(unproved.ExecutionProgram.Instructions.Where(static instruction =>
            instruction.Kind == Utf8ExecutionInstructionKind.Enter && instruction.NodeKind == Utf8ExecutionNodeKind.Loop));
        Assert.NotNull(unprovedClass.ScalarClass);
        Assert.Null(unprovedClass.CharClass);
        Assert.False(Utf8ExecutionInterpreter.TryMatchPrefix("\0"u8, unproved.ExecutionProgram, 0,
            Utf8ExecutionDeadline.Infinite, out _));
    }

    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private sealed record Input(Utf8SemanticRegex Semantic, Utf8ExecutionTree Tree, Utf8ExecutionProgram Program);

    private static Input Make(int depth)
    {
        var source = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        var node = new Utf8ExecutionNode(Utf8ExecutionNodeKind.One, RegexOptions.None, [], "", 'a', 0, 0, 0);
        for (var index = 0; index < depth; index++)
        {
            var parent = Utf8TreeNode.Create(Utf8TreeNodeKind.Capture, RegexOptions.None, 1, -1);
            parent.WithChild(source);
            source = parent;
            node = new(Utf8ExecutionNodeKind.Capture, RegexOptions.None, [node], "", '\0', 1, 1, -1);
        }
        var owned = Utf8OwnedRegexTree.Create(source, 2, null, null, null, RegexOptions.None, CultureInfo.InvariantCulture, null, false, false);
        var semantic = new Utf8SemanticRegex("a", RegexOptions.None, "a", RegexOptions.None, owned);
        var tree = new Utf8ExecutionTree(node);
        var program = Utf8ExecutionProgramLowerer.Lower(tree);
        Assert.NotNull(program);
        return new(semantic, tree, program);
    }

    private static void Run(int route, Input input)
    {
        switch (route)
        {
            case 0: Assert.NotNull(Utf8ExecutionTreeLowerer.Lower(input.Semantic)); break;
            case 1: Assert.NotNull(Utf8ExecutionProgramLowerer.Lower(input.Tree)); break;
            default: throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 1)] [InlineData(0, 2)]
    [InlineData(1, 0)] [InlineData(1, 1)] [InlineData(1, 2)]
    public void RootAdmissionPrecedesTraversalAndLeavesParentIntact(int route, int denial)
    {
        var input = Make(0);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue,
            denial == 0 ? 128ul : ulong.MaxValue, denial == 2 ? 2ul : ulong.MaxValue,
            denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch
        {
            0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps,
        }, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ChildDepthDenialUnwindsAndAllowsRecovery(int route)
    {
        var input = Make(32);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var work = owner.Budget.WorkSteps;
        using (var recovery = Utf8ConstructionScope.Enter()) Run(route, Make(0));
        Assert.True(owner.Budget.WorkSteps > work);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void GrowingStorageDenialReleasesAllPayloadAndScratchLoans(int route)
    {
        var input = Make(128);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void EveryWorkCutoffRestoresParentWithoutRefundingSpentWork(int route)
    {
        var input = Make(3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Run(route, input);
            work = measure.Budget.WorkSteps;
            Assert.True(measure.Budget.ReservedBytes > 0);
            Assert.Equal(0ul, measure.Budget.Depth);
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Run(route, input));
            if (cutoff == work) Assert.Null(failure);
            else
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                Assert.True(breach.Requested > cutoff + 2);
            }
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void EveryByteCutoffAdmitsBeforeAcquisitionAndRestoresParent(int route)
    {
        var input = Make(3);
        ulong peak;
        using (var measure = Utf8ConstructionScope.Enter())
        {
            Run(route, input);
            peak = measure.Budget.PeakBytes;
        }
        Assert.InRange(peak, 1ul, 10000ul);
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 128 + allowance, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Run(route, input));
            if (allowance == peak) Assert.Null(failure);
            if (failure is not null)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                Assert.True(breach.Requested > 128 + allowance);
            }
            Assert.True(owner.Budget.PeakBytes <= 128 + allowance);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void SuccessfulResultsTransferPayloadLoansWhileScratchDepthCloses(int route)
    {
        var input = Make(3);
        using var owner = Utf8ConstructionScope.Enter();
        Run(route, input);
        Assert.True(owner.Budget.ReservedBytes > 0);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.ReservedBytes > 0);
        Assert.Equal(route == 1 ? (ulong)input.Program.Instructions.Count : 0ul, owner.Budget.Reserved);
    }

    [Fact]
    public void ProgramCapacityIncludesGrowthOverlapAndFinalInstructionArray()
    {
        var input = Make(3);
        ulong peak;
        using (var measure = Utf8ConstructionScope.Enter())
        {
            Run(1, input);
            peak = measure.Budget.Peak;
            Assert.Equal((ulong)input.Program.Instructions.Count, measure.Budget.Reserved);
        }
        for (var limit = 1ul; limit <= peak; limit++)
        {
            using var owner = new Utf8ConstructionScope(limit);
            var failure = Record.Exception(() => Run(1, input));
            if (failure is null) Assert.Equal((ulong)input.Program.Instructions.Count, owner.Budget.Reserved);
            else
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, breach.ResourceKind);
                Assert.True(breach.Requested > limit);
                Assert.Equal(0ul, owner.Budget.Reserved);
                Assert.Equal(0ul, owner.Budget.ReservedBytes);

            }
            Assert.True(owner.Budget.Peak <= limit);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
    }

    [Fact]
    public void ScalarSharedDeclarationsRetainEveryCaptureAndTheFinalByteRange()
    {
        var regex = new Utf8Regex("(?<x>a)(?<x>😀)", RegexOptions.CultureInvariant);
        var match = regex.MatchDetailed("a😀"u8);
        Assert.True(match.Success);
        var group = match.GetGroup("x");
        Assert.Equal(2, group.CaptureCount);
        Assert.Equal((1, 4), (group.IndexInBytes, group.LengthInBytes));
        Assert.Equal("😀", group.GetValueString());
    }
}
