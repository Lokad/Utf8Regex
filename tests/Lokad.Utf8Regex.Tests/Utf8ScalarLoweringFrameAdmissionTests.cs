using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarLoweringFrameAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static readonly Utf8SliceNode Leaf = new Utf8SliceNode.LiteralNode('a', false);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void AnalysisRootDenialPrecedesFrameStorageAndFactMapAcquisition(int denial)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceLowerer.Lower(Leaf, null, false, false, false));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, 1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void DeepFrameByteDenialRestoresTheParentAndTheSameOwnerRecovers(int shape)
    {
        var root = Leaf;
        for (var index = 0; index < 32; index++)
            root = shape switch
            {
                0 => new Utf8SliceNode.CaptureNode(0, root),
                1 => new Utf8SliceNode.ConcatNode([root]),
                2 => new Utf8SliceNode.AlternationNode([[root], []]),
                3 => new Utf8SliceNode.RepeatNode(root, 1, 1, false),
                4 => new Utf8SliceNode.LookaheadNode(root, true),
                _ => new Utf8SliceNode.LookbehindNode(root, true),
            };
        // Preserve the original graph/frame allowance; separately admit the
        // finalized program with a conservative helper charge.
        var maximumBytes = 1024ul + Math.Max(128ul, Utf8SliceProgram.ObjectBytes);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, maximumBytes, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceLowerer.Lower(root, null, false, false, false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(maximumBytes, failure.Limit);
        Assert.True(failure.Requested > failure.Limit);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, maximumBytes);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var spent = owner.Budget.WorkSteps;
        using (var recovery = Utf8ConstructionScope.Enter())
            Assert.NotNull(Utf8SliceLowerer.Lower(Leaf, null, false, false, false));
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void FormerGraphOnlyAllowanceCannotAcquireFinalProgramMetadata()
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 1024, ulong.MaxValue, ulong.MaxValue);
        _ = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceLowerer.Lower(Leaf, null, false, false, false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        // The retained numeric group-zero name now owns its 48-byte text loan.
        Assert.Equal(1024ul, failure.Limit); Assert.Equal(1152ul, failure.Requested);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.InRange(owner.Budget.PeakBytes, 128ul, 1024ul); Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryWorkCutoffRestoresAnalysisEmissionAndBranchScratch(bool nonBacktracking)
    {
        // Borrow one immutable DAG throughout replay so reference-identity hash
        // probes remain the same. Include shared nodes, empty branches, finite
        // optional copies, a nullable loop, assertions and a conditional test.
        Utf8SliceNode root = new Utf8SliceNode.ConcatNode([
            new Utf8SliceNode.AlternationNode([[], [new Utf8SliceNode.RepeatNode(Leaf, 1, 3, true)], [Leaf, Leaf]]),
            new Utf8SliceNode.RepeatNode(new Utf8SliceNode.AlternationNode([[], [Leaf]]), 0, int.MaxValue, false),
            new Utf8SliceNode.ConditionalNode(-1, Leaf, new Utf8SliceNode.EmptyNode(), new Utf8SliceNode.LookaheadNode(Leaf, true)),
            new Utf8SliceNode.LookbehindNode(Leaf, true),
        ]);
        ulong work;
        using (var measure = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.NotNull(Utf8SliceLowerer.Lower(root, null, false, false, false, null, false, false,
                Utf8ScalarCaseBehavior.Invariant, nonBacktracking));
            work = measure.Budget.WorkSteps;
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var speculative = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Utf8SliceLowerer.Lower(root, null, false, false, false, null,
                    false, false, Utf8ScalarCaseBehavior.Invariant, nonBacktracking));
                if (cutoff < work)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                    Assert.Equal(cutoff + 2, breach.Limit);
                    Assert.True(breach.Requested > breach.Limit);
                    Assert.Equal(128ul, owner.Budget.ReservedBytes);
                    Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                }
                else Assert.Null(failure);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.True(owner.Budget.WorkSteps <= cutoff + 2);
            }
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void BorrowedAnalysisDepthCannotBypassTheEnclosingOwner()
    {
        Utf8SliceNode root = Leaf;
        for (var index = 0; index < 32; index++) root = new Utf8SliceNode.LookbehindNode(root, true);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceLowerer.Lower(root, null, false, false, false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        using (var recovery = Utf8ConstructionScope.Enter())
            Assert.NotNull(Utf8SliceLowerer.Lower(Leaf, null, false, false, false));
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }
}
