using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeEligibilityAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static readonly Utf8OwnedRegexTree Seed = Utf8OwnedTreeParser.TryParse("a", RegexOptions.CultureInvariant)
        ?? throw new InvalidOperationException();

    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, RegexOptions options, params Utf8TreeNode[] children)
    {
        var node = Utf8TreeNode.Create(kind, options);
        if (kind == Utf8TreeNodeKind.Capture) { node.M = 0; node.N = -1; }
        foreach (var child in children) node.WithChild(child);
        return node;
    }

    private static Utf8OwnedRegexTree Tree(Utf8TreeNode root) => Utf8OwnedRegexTree.Create(root, Seed.CaptureCount, Seed.CaptureNames, Seed.CaptureNameToNumberMapping, Seed.CaptureNumberSparseMapping, RegexOptions.CultureInvariant, CultureInfo.InvariantCulture, Seed.CaptureCatalog, false, false, default, default);

    private static Utf8OwnedRegexTree Make(int depth)
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, "😀");
        for (var index = 0; index < depth; index++) node = Node(Utf8TreeNodeKind.Group, RegexOptions.None, node);
        return Tree(Node(Utf8TreeNodeKind.Capture, RegexOptions.None, node));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void RootAdmissionPrecedesScanningAndRestoresParent(int denial)
    {
        var tree = Make(0);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceParser.IsNonBacktrackingFlatGraph(tree));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth}, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void DeepChildDenialAndSameOwnerRecoveryRestoreLoans()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var input = Make(32);
        var shallow = Make(0);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceParser.IsNonBacktrackingFlatGraph(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var work = owner.Budget.DiagnosticCheckpoints;
        using (var child = Utf8ConstructionScope.Enter()) Assert.True(Utf8SliceParser.IsNonBacktrackingFlatGraph(shallow));
        Assert.True(owner.Budget.DiagnosticCheckpoints > work);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void GrowthDenialReleasesFramesAndAnyLoweredPayload()
    {
        var input = Make(128);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 2048, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceParser.IsNonBacktrackingFlatGraph(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void EveryWorkCutoffRestoresParentWithoutRefundingWork()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var input = Make(3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.True(Utf8SliceParser.IsNonBacktrackingFlatGraph(input));
            work = measure.Budget.DiagnosticCheckpoints;
        }
        Assert.InRange(work, 1ul, 10000ul);
    }

    [Fact]
    public void EveryByteCutoffRestoresTemporaryAndTransferredLoans()
    {
        var input = Make(3);
        ulong peak;
        using (var measure = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.True(Utf8SliceParser.IsNonBacktrackingFlatGraph(input));
            peak = measure.Budget.PeakBytes;
        }
        Assert.InRange(peak, 1ul, 30000ul);
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 128 + allowance, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Assert.True(Utf8SliceParser.IsNonBacktrackingFlatGraph(input)));
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
    [InlineData(@"a$|b", false)]
    [InlineData(@"a$|\uD800", true)]
    public void FinalizationChecksImpossibleAtomsAfterDecliningFiniteExpansion(string pattern, bool impossible)
    {
        var text = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        var bytes = Utf8OwnedTreeParser.TryParse(System.Text.Encoding.UTF8.GetBytes(pattern), RegexOptions.CultureInvariant, out _);
        Assert.NotNull(text);
        Assert.NotNull(bytes);
        Assert.Equal((impossible, false), text.LiteralEligibility);
        Assert.Equal(text.LiteralEligibility, bytes.LiteralEligibility);
    }

    [Fact]
    public void FlatProofDeclinedForSharedCapturesRollsBackTheWholeAttempt()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var tree = Utf8OwnedTreeParser.TryParse("(?<x>a)(?<x>b)", RegexOptions.CultureInvariant) ?? throw new InvalidOperationException();
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.False(Utf8SliceParser.IsNonBacktrackingFlatGraph(tree));
        Assert.True(owner.Budget.PeakBytes > 128);
        Assert.True(owner.Budget.DiagnosticCheckpoints > 2);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }
}
