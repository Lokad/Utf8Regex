using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FrontEndScanAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, string text, params Utf8TreeNode[] children)
    {
        var node = Utf8TreeNode.Create(kind: kind, options: RegexOptions.None,
            ch: '\0', str: text, m: 1, n: 1,
            children: null, parent: null, matchCaseInsensitive: false);
        foreach (var child in children) node.WithChild(child);
        return node;
    }

    private static Utf8TreeNode Make(int depth)
    {
        var find = Node(Utf8TreeNodeKind.Multi, "ab");
        for (var index = 0; index < depth; index++)
            find = Node(Utf8TreeNodeKind.Concatenate, "", find, Node(Utf8TreeNodeKind.Multi, "c"));
        return find;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void RootDenialPrecedesFrameAcquisition(int denial)
    {
        var input = Make(0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Assert.Null(Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(input)));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth}, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void ChildDepthDenialRestoresParentAndAllowsRecovery()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var input = Make(32);
        var recovered = Make(0);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Assert.Null(Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(input)));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var spent = owner.Budget.DiagnosticCheckpoints;
        Assert.Null(Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(recovered));
        Assert.True(owner.Budget.DiagnosticCheckpoints > spent);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void ArrayGrowthDenialReleasesEveryScratchLoan()
    {
        var input = Make(128);
        using var owner = new Utf8ConstructionScope(0, 2048, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Assert.Null(Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(input)));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void EveryWorkCutoffRestoresScratchWithoutRefundingWork()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var input = Make(3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.Null(Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(input));
            work = measure.Budget.DiagnosticCheckpoints;
            Assert.Equal(0ul, measure.Budget.ReservedBytes);
        }
        Assert.InRange(work, 1ul, 10000ul);
    }

    [Fact]
    public void NonBacktrackingDiagnosticsRetainReverseSourcePrecedence()
    {
        var root = Node(Utf8TreeNodeKind.Concatenate, "", Node(Utf8TreeNodeKind.Backreference, ""), Node(Utf8TreeNodeKind.Atomic, ""));
        Assert.Equal("atomic groups", Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(root));
        root = Node(Utf8TreeNodeKind.Concatenate, "", Node(Utf8TreeNodeKind.Atomic, ""), Node(Utf8TreeNodeKind.Backreference, ""));
        Assert.Equal("backreferences", Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(root));
    }

    [Fact]
    public void NegatedQuotedScalarsPreserveTheByteRange()
    {
        var regex = new Utf8Regex("'[^a]{2}'", options: RegexOptions.CultureInvariant);
        var match = regex.Match("'😀b'"u8);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(7, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }
}
