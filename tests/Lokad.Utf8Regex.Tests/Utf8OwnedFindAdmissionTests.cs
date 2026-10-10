using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedFindAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    private static Utf8TreeNode Wrap(Utf8TreeNode node, int depth)
    {
        for (var index = 0; index < depth; index++)
        {
            var parent = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.None);
            parent.WithChild(node);
            node = parent;
        }
        return node;
    }

    private static Utf8TreeNode Input(int route, int depth) => Wrap(route == 0
        ? Utf8TreeNode.Create(Utf8TreeNodeKind.End, RegexOptions.None)
        : Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a'), depth);

    private static object? Prepare(int route, Utf8TreeNode node) => route switch
    {
        0 => Utf8OwnedFindOptimizations.FindTrailingAnchor(node),
        1 => Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(node),
        _ => Utf8OwnedFindOptimizations.TryExtractLongestLiteral(node),
    };

    private static void AssertResult(int route, object? result)
    {
        if (route == 0) Assert.Equal(Utf8TreeNodeKind.End, Assert.IsType<Utf8TreeNodeKind>(result));
        else Assert.Equal("a", Assert.IsType<string>(result));
    }

    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 3; route++)
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }

    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootAdmissionPrecedesPayloadAcquisition(int route, int denial)
    {
        var input = Input(route, 0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 112ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, input));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ChildDepthDenialRestoresParentAndAllowsRecovery(int route)
    {
        var input = Input(route, 32);
        var shallow = Input(route, 0);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var work = owner.Budget.WorkSteps;
        using (var child = Utf8ConstructionScope.Enter()) AssertResult(route, Prepare(route, shallow));
        Assert.True(owner.Budget.WorkSteps > work);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void AncestorGrowthDenialReleasesScratchAndText(int route)
    {
        var input = Input(route, 128);
        using var owner = new Utf8ConstructionScope(0, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EveryWorkCutoffRestoresParentWithoutRefundingWork(int route)
    {
        var input = Input(route, 3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            AssertResult(route, Prepare(route, input));
            work = measure.Budget.WorkSteps;
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => AssertResult(route, Prepare(route, input)));
            if (cutoff == work) Assert.Null(failure);
            else Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
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
    public void EveryByteCutoffIncludesTransferredTextAndDescriptors(int route)
    {
        var input = Input(route, 3);
        ulong peak;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            AssertResult(route, Prepare(route, input));
            peak = measure.Budget.PeakBytes;
        }
        Assert.InRange(peak, 1ul, 10000ul);
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, 112 + allowance, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => AssertResult(route, Prepare(route, input)));
            if (allowance == peak) Assert.Null(failure);
            if (failure is not null) Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            Assert.True(owner.Budget.PeakBytes <= 112 + allowance);
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void LiteralRunByteAndWorkCutoffsRestoreEarlierAcquisitions(int route)
    {
        var input = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, RegexOptions.None);
        input.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a'));
        input.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, "😀"));
        input.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.End, RegexOptions.None));
        input.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, "longest"));
        var expected = route == 1 ? "a😀" : "longest";
        ulong peak, work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.Equal(expected, Prepare(route, input));
            peak = measure.Budget.PeakBytes;
            work = measure.Budget.WorkSteps;
        }
        Assert.InRange(peak, 1ul, 10000ul);
        Assert.InRange(work, 1ul, 10000ul);
        for (var mode = 0; mode < 2; mode++)
        for (var allowance = 0ul; allowance <= (mode == 0 ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, mode == 0 ? 112 + allowance : ulong.MaxValue,
                mode == 1 ? 2 + allowance : ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Assert.Equal(expected, Prepare(route, input)));
            if (allowance == (mode == 0 ? peak : work)) Assert.Null(failure);
            else Assert.Equal(mode == 0 ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void ReturnedLiteralLoansRemainOwnedUntilEnclosingScopeCloses(int route)
    {
        var input = Input(route, 3);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        using (var child = Utf8ConstructionScope.Enter())
        {
            AssertResult(route, Prepare(route, input));
            Assert.True(owner.Budget.ReservedBytes > 112);
            Assert.NotSame(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.Reserved);
        }
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void DeclinedLiteralAndPureAnchorReturnReleaseAllScratch(int route)
    {
        var input = Wrap(Utf8TreeNode.Create(Utf8TreeNodeKind.Nothing, RegexOptions.None), 65);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        if (route == 0) Assert.Equal(Utf8TreeNodeKind.Unknown, Prepare(route, input));
        else Assert.Null(Prepare(route, input));
        Assert.True(owner.Budget.PeakBytes > 112);
        Assert.True(owner.Budget.WorkSteps > 2);
        Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void ConcatenatedSupplementaryPatternTextPreservesBorrowedParents(int route)
    {
        var input = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, RegexOptions.None);
        var first = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, "😀");
        var last = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        input.WithChild(first); input.WithChild(last);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        Assert.Equal("😀a", Prepare(route, input));
        Assert.Same(input, first.Parent);
        Assert.Same(input, last.Parent);
        Assert.True(owner.Budget.ReservedBytes > 0);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void RemovedFindStateAndProducerAreAbsentFromShippedMetadata()
    {
        Assert.Null(typeof(Utf8OwnedFindOptimizations).GetMethod("FindLeadingAnchor", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(typeof(Utf8OwnedFindFacts).GetProperty("LeadingAnchor"));
        Assert.Null(typeof(Utf8OwnedFindFacts).GetProperty("Root"));
    }
}
