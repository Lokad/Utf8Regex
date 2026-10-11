using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeEligibilityAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static readonly Utf8OwnedRegexTree Seed = Utf8OwnedTreeParser.TryParse("a", RegexOptions.CultureInvariant)
        ?? throw new InvalidOperationException();
    private static Func<Utf8TreeNode, bool> Scan(Type type, string name) =>
        (type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException())
        .CreateDelegate<Func<Utf8TreeNode, bool>>();
    private static readonly Func<Utf8TreeNode, bool>[] Scans =
    [
        Scan(typeof(Utf8NativeExecutionAnalyzer), "ContainsImpossibleAtom"),
        Scan(typeof(Utf8NativeExecutionAnalyzer), "IsSupportedSimplePatternNode"),
        Scan(typeof(Utf8AsciiSimplePatternLowerer), "HasCaseClearingGroup"),
        Utf8AsciiSimplePatternLowerer.HasScopedIgnoreCase,
        Scan(typeof(Utf8AsciiSimplePatternLowerer), "AllowsTrailingNewlineBeforeEnd"),
    ];

    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, RegexOptions options, params Utf8TreeNode[] children)
    {
        var node = Utf8TreeNode.Create(kind, options);
        if (kind == Utf8TreeNodeKind.Capture) { node.M = 0; node.N = -1; }
        foreach (var child in children) node.WithChild(child);
        return node;
    }

    private static Utf8OwnedRegexTree Tree(Utf8TreeNode root) => Utf8OwnedRegexTree.Create(root, Seed.CaptureCount, Seed.CaptureNames, Seed.CaptureNameToNumberMapping, Seed.CaptureNumberSparseMapping, RegexOptions.CultureInvariant, CultureInfo.InvariantCulture, Seed.CaptureCatalog, false, false);

    private static Utf8OwnedRegexTree Make(int route, int depth)
    {
        var node = route switch
        {
            2 => Node(Utf8TreeNodeKind.Group, RegexOptions.None, Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a')),
            3 => Node(Utf8TreeNodeKind.Group, RegexOptions.IgnoreCase, Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.IgnoreCase, 'a')),
            4 => Node(Utf8TreeNodeKind.EndZ, RegexOptions.None),
            5 => Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, "😀"),
            _ => Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a'),
        };
        if (route == 5)
        {
            for (var index = 0; index < depth; index++) node = Node(Utf8TreeNodeKind.Group, RegexOptions.None, node);
            node = Node(Utf8TreeNodeKind.Capture, RegexOptions.None, node);
        }
        else
            for (var index = 0; index <= depth; index++) node = Node(Utf8TreeNodeKind.Capture, RegexOptions.None, node);
        return Tree(node);
    }

    private static object? Prepare(int route, Utf8OwnedRegexTree tree) => route == 5
        ? Utf8SliceParser.IsNonBacktrackingFlatGraph(tree)
        : Scans[route](tree.Root);

    private static void AssertResult(int route, object? result)
    {
        if (route == 5) Assert.True(Assert.IsType<bool>(result));
        else Assert.Equal(route != 0, Assert.IsType<bool>(result));
    }

    public static IEnumerable<object[]> RootDenials()
    {
        for (var route = 0; route < 6; route++)
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }

    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootAdmissionPrecedesScanningAndRestoresParent(int route, int denial)
    {
        var tree = Make(route, 0);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, tree));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void DeepChildDenialAndSameOwnerRecoveryRestoreLoans(int route)
    {
        var input = Make(route, 32);
        var shallow = Make(route, 0);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var work = owner.Budget.WorkSteps;
        using (var child = Utf8ConstructionScope.Enter()) AssertResult(route, Prepare(route, shallow));
        Assert.True(owner.Budget.WorkSteps > work);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void GrowthDenialReleasesFramesAndAnyLoweredPayload(int route)
    {
        var input = Make(route, 128);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(route, input));
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
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryWorkCutoffRestoresParentWithoutRefundingWork(int route)
    {
        var input = Make(route, 3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            AssertResult(route, Prepare(route, input));
            work = measure.Budget.WorkSteps;
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => AssertResult(route, Prepare(route, input)));
            // All eligibility routes now scan borrowed tree/catalog facts;
            // they do not compile a discarded scalar program or vary by map probes.
            if (cutoff == work) Assert.Null(failure);
            else Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
        if (route == 5)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 10000, ulong.MaxValue);
            AssertResult(route, Prepare(route, input));
            Assert.InRange(owner.Budget.WorkSteps, 1ul, 10000ul);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryByteCutoffRestoresTemporaryAndTransferredLoans(int route)
    {
        var input = Make(route, 3);
        ulong peak;
        using (var measure = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            AssertResult(route, Prepare(route, input));
            peak = measure.Budget.PeakBytes;
        }
        Assert.InRange(peak, 1ul, 30000ul);
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 128 + allowance, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => AssertResult(route, Prepare(route, input)));
            if (allowance == peak) Assert.Null(failure);
            if (failure is not null) Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            Assert.True(owner.Budget.PeakBytes <= 128 + allowance);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void WideBorrowedTreesUseAncestorCapacityAndRestoreParent(int route)
    {
        var children = Enumerable.Range(0, 4097).Select(_ => route == 4
            ? Node(Utf8TreeNodeKind.EndZ, RegexOptions.None)
            : Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a')).ToArray();
        var tree = Tree(Node(Utf8TreeNodeKind.Alternate, RegexOptions.None, children));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.Equal(route is 1 or 4, Assert.IsType<bool>(Prepare(route, tree)));
        Assert.Equal(2ul, owner.Budget.PeakDepth);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 1024ul);
        Assert.True(owner.Budget.WorkSteps >= 4097);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void BorrowedLiteralValidationAdmitsEveryScalarBeforeDecoding()
    {
        var text = string.Concat(Enumerable.Repeat("😀", 100000)) + '\ud800';
        var tree = Tree(Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, text));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 1002, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Prepare(0, tree));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(1002ul, owner.Budget.WorkSteps);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void FlatProofDeclinedForSharedCapturesRollsBackTheWholeAttempt()
    {
        var tree = Utf8OwnedTreeParser.TryParse("(?<x>a)(?<x>b)", RegexOptions.CultureInvariant) ?? throw new InvalidOperationException();
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.False(Assert.IsType<bool>(Prepare(5, tree)));
        Assert.True(owner.Budget.PeakBytes > 128);
        Assert.True(owner.Budget.WorkSteps > 2);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void UnusedLookaroundEligibilityHelperIsAbsentFromShippedMetadata()
    {
        Assert.Null(typeof(Utf8NativeExecutionAnalyzer).GetMethod("IsSupportedSimplePatternLookaroundNode", BindingFlags.Static | BindingFlags.NonPublic));
    }
}
