using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ExecutionLoweringAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private sealed record Input(Utf8SemanticRegex Semantic, Utf8ExecutionTree Tree, Utf8ExecutionProgram Program);

    private static Input Make(int depth)
    {
        var source = new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        var node = new Utf8ExecutionNode(Utf8ExecutionNodeKind.One, RegexOptions.None, [], "", 'a', 0, 0, 0);
        for (var index = 0; index < depth; index++)
        {
            var parent = new Utf8TreeNode(Utf8TreeNodeKind.Capture, RegexOptions.None, 1, -1);
            parent.AddChild(source);
            source = parent;
            node = new(Utf8ExecutionNodeKind.Capture, RegexOptions.None, [node], "", '\0', 1, 1, -1);
        }
        var owned = new Utf8OwnedRegexTree(source, 2, null, null, null, RegexOptions.None, CultureInfo.InvariantCulture);
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
            case 2: Assert.NotNull(Utf8ExecutionInterpreter.CreateDeterministicCaptureLayout(input.Program)); break;
            default: throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 1)] [InlineData(0, 2)]
    [InlineData(1, 0)] [InlineData(1, 1)] [InlineData(1, 2)]
    [InlineData(2, 0)] [InlineData(2, 1)] [InlineData(2, 2)]
    public void RootAdmissionPrecedesTraversalAndLeavesParentIntact(int route, int denial)
    {
        var input = Make(0);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue,
            denial == 0 ? 112ul : ulong.MaxValue, denial == 2 ? 2ul : ulong.MaxValue,
            denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch
        {
            0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps,
        }, failure.ResourceKind);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ChildDepthDenialUnwindsAndAllowsRecovery(int route)
    {
        var input = Make(32);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var work = owner.Budget.WorkSteps;
        using (var recovery = Utf8ConstructionScope.Enter()) Run(route, Make(0));
        Assert.True(owner.Budget.WorkSteps > work);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void GrowingStorageDenialReleasesAllPayloadAndScratchLoans(int route)
    {
        var input = Make(128);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
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
            var checkpoint = owner.Budget.ObjectCheckpoint;
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
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
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
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 112 + allowance, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Run(route, input));
            if (allowance == peak) Assert.Null(failure);
            if (failure is not null)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                Assert.True(breach.Requested > 112 + allowance);
            }
            Assert.True(owner.Budget.PeakBytes <= 112 + allowance);
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void SuccessfulResultsTransferPayloadLoansWhileScratchDepthCloses(int route)
    {
        var input = Make(3);
        using var owner = Utf8ConstructionScope.Enter();
        Run(route, input);
        Assert.True(owner.Budget.ReservedBytes > 0);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.NotNull(owner.Budget.ObjectCheckpoint);
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
                Assert.Null(owner.Budget.ObjectCheckpoint);
            }
            Assert.True(owner.Budget.Peak <= limit);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
    }

    [Fact]
    public void CaptureLayoutKeepsLastSharedDeclarationWithoutRetainingHashTable()
    {
        var prepared = Utf8FrontEnd.Compile("(?<x>a)(?<x>bc)", RegexOptions.CultureInvariant);
        Assert.NotNull(prepared.ExecutionProgram);
        var layout = Assert.IsType<Utf8DeterministicCaptureLayout>(prepared.ExecutionProgram.DeterministicCaptureLayout);
        var captures = Assert.IsType<KeyValuePair<int, Utf8DeterministicCaptureSlice>[]>(layout.Captures);
        Assert.Equal(3, layout.MatchLength);
        Assert.Equal(new Utf8DeterministicCaptureSlice(1, 2), captures.Single(static pair => pair.Key == 1).Value);
        Assert.Equal(new Utf8DeterministicCaptureSlice(0, 3), captures.Single(static pair => pair.Key == 0).Value);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0;
        for (var repeat = 0; repeat < 1000; repeat++) foreach (var pair in captures) sum += pair.Value.Length;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(5000, sum);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void UnsupportedCaptureLayoutDeclinesWithoutRetainingScratch()
    {
        var input = new Utf8ExecutionTree(new(Utf8ExecutionNodeKind.Alternate, RegexOptions.None, [], "", '\0', 0, 0, 0));
        var program = Utf8ExecutionProgramLowerer.Lower(input);
        Assert.NotNull(program);
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Null(Utf8ExecutionInterpreter.CreateDeterministicCaptureLayout(program));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Null(owner.Budget.ObjectCheckpoint);
        Assert.True(owner.Budget.WorkSteps > 0);
    }
}
