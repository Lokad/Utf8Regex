using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FrontEndScanAdmissionTests
{
    private const string Digits = "[0-9]";
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private sealed record Input(Utf8TreeNode Find, Utf8SemanticRegex Semantic);

    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, string text, params Utf8TreeNode[] children)
    {
        Utf8TreeNode node;
        if (kind is Utf8TreeNodeKind.Set or Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy)
        {
            Assert.Equal(Digits, text);
            using var scope = Utf8ConstructionScope.Enter();
            using var data = Utf8ConstructionBuffer<int>.Create(scope.Budget);
            data.AddRange([0, 2, 0, '0', ':']);
            node = Utf8TreeNode.Create(kind, RegexOptions.None, Utf8ScalarClass.Create(data));
            node.M = 1; node.N = 1;
            scope.Complete();
        }
        else node = Utf8TreeNode.Create(kind: kind, options: RegexOptions.None,
            ch: '\0', str: text, m: 1, n: 1,
            children: null, parent: null, matchCaseInsensitive: false);
        foreach (var child in children) node.WithChild(child);
        return node;
    }

    private static Utf8SemanticRegex Semantic(Utf8TreeNode root)
        => new("a", RegexOptions.None, 0, RegexOptions.None,
            Utf8OwnedRegexTree.Create(root, 1, null, null, null, RegexOptions.None, CultureInfo.InvariantCulture, null, false, false));

    private static Input Make(int depth)
    {
        var find = Node(Utf8TreeNodeKind.Multi, "ab");
        for (var index = 0; index < depth; index++)
            find = Node(Utf8TreeNodeKind.Concatenate, "", find, Node(Utf8TreeNodeKind.Multi, "c"));
        return new(find, Semantic(find));
    }

    private static void Run(int route, Input input)
    {
        // Discard the scan results and release their construction loans.
        using var discarded = Utf8ConstructionScope.Enter();
        switch (route)
        {
            case 6:
                Span<(int, AsciiCharClass)> sets = stackalloc (int, AsciiCharClass)[Utf8FrontEndSearchAnalyzer.MaxDeterministicPrefixSets];
                Assert.True(Utf8FrontEndSearchAnalyzer.ExtractDeterministicPrefixSets(input.Find, 0, sets) > 0);
                break;
            case 9: Assert.Equal(1, Utf8RuntimeTreeFeatureAnalyzer.Analyze(input.Semantic).CaptureCount); break;
            case 10: Assert.Null(Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(input.Find)); break;
        }
    }

    public static IEnumerable<object[]> RootDenials()
    {
        foreach (var route in new[] { 6, 9, 10 })
        for (var denial = 0; denial < 3; denial++) yield return [route, denial];
    }

    [Theory]
    [MemberData(nameof(RootDenials))]
    public void RootDenialPrecedesFrameAcquisition(int route, int denial)
    {
        var input = Make(0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(6)] [InlineData(9)] [InlineData(10)]
    public void ChildDepthDenialRestoresParentAndAllowsRecovery(int route)
    {
        var input = Make(32);
        var recovered = Make(0);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var spent = owner.Budget.WorkSteps;
        Run(route, recovered);
        Assert.True(owner.Budget.WorkSteps > spent);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(6)] [InlineData(9)] [InlineData(10)]
    public void ArrayGrowthDenialReleasesEveryScratchLoan(int route)
    {
        var input = Make(128);
        using var owner = new Utf8ConstructionScope(0, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Run(route, input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(6)] [InlineData(9)] [InlineData(10)]
    public void EveryWorkCutoffRestoresScratchWithoutRefundingWork(int route)
    {
        var input = Make(3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Run(route, input);
            work = measure.Budget.WorkSteps;
            Assert.Equal(0ul, measure.Budget.ReservedBytes);
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            var failure = Record.Exception(() => Run(route, input));
            if (cutoff < work)
            {
                var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                Assert.True(breach.Requested > cutoff + 2);
            }
            else Assert.Null(failure);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void WhitespaceSuffixBorrowsChildrenWithoutReparentingTheOwnedTree()
    {
        var whitespaceTree = Utf8OwnedTreeParser.TryParse(@"\s", RegexOptions.CultureInvariant);
        Assert.NotNull(whitespaceTree);
        var whitespaceAtom = whitespaceTree.Root;
        while (whitespaceAtom.ChildCount == 1) whitespaceAtom = whitespaceAtom.Child(0);
        Assert.NotNull(whitespaceAtom.ScalarClass);
        var whitespace = Utf8TreeNode.CreateSetRepeat(whitespaceAtom, RegexOptions.CultureInvariant, 0, int.MaxValue, false);
        whitespace.M = 0;
        whitespace.N = int.MaxValue;
        var root = Node(Utf8TreeNodeKind.Concatenate, "", Node(Utf8TreeNodeKind.Beginning, ""), whitespace,
            Node(Utf8TreeNodeKind.Boundary, ""), Node(Utf8TreeNodeKind.Multi, "ab"), Node(Utf8TreeNodeKind.Set, Digits), Node(Utf8TreeNodeKind.Multi, "c"));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var work = owner.Budget.WorkSteps;
        var (node, startIndex) = Utf8FrontEndSearchAnalyzer.SkipLeadingAsciiWhitespacePrefix(root);
        Assert.Same(root, node);
        Assert.Equal(3, startIndex);
        Assert.Equal(checkpoint, owner.Budget.PeakBytes);
        Assert.True(owner.Budget.WorkSteps > work);
        Span<(int Distance, AsciiCharClass ByteSet)> scratch = stackalloc (int, AsciiCharClass)[Utf8FrontEndSearchAnalyzer.MaxDeterministicPrefixSets];
        var count = Utf8FrontEndSearchAnalyzer.ExtractDeterministicPrefixSets(node!, startIndex, scratch);
        var sets = scratch[..count].ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, sets.Select(static set => set.Distance));
        Assert.Equal(new[] { "a", "b", "0123456789", "c" }, sets.Select(static set =>
        {
            Span<byte> bytes = stackalloc byte[128];
            var length = set.ByteSet.CopyMatchBytes(bytes);
            return System.Text.Encoding.ASCII.GetString(bytes[..length]);
        }));
        for (var index = 0; index < root.ChildCount; index++) Assert.Same(root, root.Child(index).Parent);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
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
    public void FeatureFlagsRetainTheCompleteGrammarCensus()
    {
        var lookbehind = Utf8TreeNode.Create(Utf8TreeNodeKind.PositiveLookaround, RegexOptions.RightToLeft);
        var root = Node(Utf8TreeNodeKind.Concatenate, "", Node(Utf8TreeNodeKind.Beginning, ""), Node(Utf8TreeNodeKind.Boundary, ""),
            Node(Utf8TreeNodeKind.Backreference, ""), lookbehind, Node(Utf8TreeNodeKind.Atomic, ""),
            Node(Utf8TreeNodeKind.ExpressionConditional, ""), Node(Utf8TreeNodeKind.Setloop, Digits), Node(Utf8TreeNodeKind.Alternate, ""));
        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(Semantic(root));
        Assert.Equal(new Utf8RegexFeatures(1, false, true, true, true, true, true, true, true, true, true, false), features);
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
