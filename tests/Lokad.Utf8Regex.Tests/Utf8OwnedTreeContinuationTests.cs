using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedTreeContinuationTests
{
    private static string Nested(int depth, string opener, string body, string closer = ")")
        => string.Concat(Enumerable.Repeat(opener, depth)) + body +
            string.Concat(Enumerable.Repeat(closer, depth));

    private static string Pattern(int depth, string family) => family switch
    {
        "capture" => Nested(depth, "(", "(?<leaf>a)|(?<90000>b)"),
        "scope" => Nested(depth, "(?i:(?-i:", "(?i:(a|K)+)", "))"),
        "assertion" => Nested(depth, "(?=(?<!", "(?<leaf>a)", "))"),
        "atomic" => Nested(depth, "(?>", "(a|b)+") + "c",
        "expression" => Nested(depth, "(?(?=(?<probe>a))", "(?<leaf>a)", "|b)"),
        "reference" => "(?<x>a)" + Nested(depth, "(?(x)", @"\k<x>", "|b)"),
        "balance" => Nested(depth, "(?<x>", "a", ")(?<-x>b)"),
        "octal" => Nested(depth, "(?:", @"x\1012{2}y\08"),
        _ => throw new ArgumentOutOfRangeException(nameof(family)),
    };

    public static IEnumerable<object[]> DeepGrammarCases()
    {
        foreach (var depth in new[] { 8, 16, 31, 129 })
        foreach (var family in new[] { "capture", "scope", "assertion", "atomic", "expression", "reference", "balance", "octal" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [depth, family, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(DeepGrammarCases))]
    public void SuspendedGrammarKeepsOracleMetadataAndTreeOwnership(int depth, string family, RegexOptions options)
    {
        var pattern = Pattern(depth, family);
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        // Metadata does not depend on engine compilation. This oracle check
        // constructs the interpreted engine, without executing a deep subject.
        var oracle = new Regex(pattern, options & ~RegexOptions.Compiled);
        Assert.Equal(oracle.GetGroupNames(), tree.CaptureNames);
        Assert.Equal(oracle.GetGroupNumbers(), tree.CaptureCatalog!.GroupNumbers);
        Assert.Equal(oracle.GetGroupNumbers().Length, tree.CaptureCount);
        AssertDeepTree(tree, depth);
    }

    [Theory]
    [InlineData((int)Utf8TreeNodeKind.Capture)]
    [InlineData((int)Utf8TreeNodeKind.Atomic)]
    [InlineData((int)Utf8TreeNodeKind.Loop)]
    [InlineData((int)Utf8TreeNodeKind.PositiveLookaround)]
    public void ExtremeTreeDepthKeepsTreeOwnershipAndLiveFeatures(int nodeKind)
    {
        const int depth = 16385;
        var kind = (Utf8TreeNodeKind)nodeKind;
        // Analyze an independently built tree beyond normal pattern sizes.
        // No recursive test walker or .NET construction is required to exercise
        // the admitted feature cursor and complete tree ownership.
        var tree = Utf8OwnedTreeParser.TryParse("(?<x>a|b)+c", RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var body = tree.Root.Child(0);
        for (var level = 0; level < depth; level++)
        {
            var parent = new Utf8TreeNode(kind, RegexOptions.CultureInvariant);
            parent.M = 1;
            parent.N = kind == Utf8TreeNodeKind.Capture ? -1 : 2;
            parent.Children = body;
            body.Parent = parent;
            body = parent;
        }
        tree.Root.Children = body;
        body.Parent = tree.Root;
        AssertDeepTree(tree, depth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManySiblingGroupsKeepNumberingAndReferenceNormalization(bool compiled)
    {
        const int count = 4097;
        var pattern = string.Concat(Enumerable.Repeat("(?<x>a)", count)) + @"\1012{0}";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        var oracle = new Regex(pattern, options & ~RegexOptions.Compiled);
        Assert.Equal(oracle.GetGroupNames(), tree.CaptureNames);
        Assert.Equal(oracle.GetGroupNumbers(), tree.CaptureCatalog!.GroupNumbers);
        AssertDeepTree(tree, 3);
        var captures = new Stack<Utf8TreeNode>();
        captures.Push(tree.Root);
        var declarations = 0;
        while (captures.TryPop(out var node))
        {
            if (node.Kind == Utf8TreeNodeKind.Capture && node.M != 0)
            {
                Assert.Equal(1, node.M);
                declarations++;
            }
            for (var index = 0; index < node.ChildCount; index++) captures.Push(node.Child(index));
        }
        Assert.Equal(count, declarations);
    }

    private static void AssertDeepTree(Utf8OwnedRegexTree tree, int minimumDepth)
    {
        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(new("", tree.Options, "", tree.Options, tree));
        var nodes = new HashSet<Utf8TreeNode>();
        var pending = new Stack<(Utf8TreeNode Node, int Depth)>();
        pending.Push((tree.Root, 0));
        var maximumDepth = 0;
        var captures = 0;
        while (pending.TryPop(out var frame))
        {
            Assert.True(nodes.Add(frame.Node), "The owned tree must not contain cycles or shared children.");
            maximumDepth = Math.Max(maximumDepth, frame.Depth);
            if (frame.Node.Kind == Utf8TreeNodeKind.Capture) captures++;
            for (var index = 0; index < frame.Node.ChildCount; index++)
            {
                var child = frame.Node.Child(index);
                Assert.Same(frame.Node, child.Parent);
                pending.Push((child, frame.Depth + 1));
            }
        }
        Assert.True(maximumDepth >= minimumDepth);
        Assert.True(captures > 0);
        Assert.Equal(tree.CaptureCount, features.CaptureCount);
        Assert.Equal(tree.CaptureNameToNumberMapping is { Count: > 0 }, features.HasNamedCaptures);
    }

    public static IEnumerable<object[]> OwnedOperationCases()
    {
        foreach (var depth in new[] { 32, 62 })
        foreach (var family in new[] { "capture", "scope", "assertion", "expression" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [depth, family, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(OwnedOperationCases))]
    public void SuspendedGrammarKeepsAllOwnedOperations(int depth, string family, RegexOptions options)
    {
        var pattern = family switch
        {
            "capture" => Nested(depth, "(", "(?<x>a?)"),
            "scope" => Nested(depth, "(?i:", "(?<x>a?)"),
            "assertion" => Nested(depth, "(?=", "(?<x>a?)") + "a?",
            _ => Nested(depth, "(?(?=a)", "(?<x>a)", "|b)"),
        };
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "a", "b", "aAb"], options);
        if (family == "expression")
            Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, ["\U0001f600a\U0001f600"], options);
        else Utf8ScalarNullableOperationAssertions.AssertOperations(pattern, options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedInvalidGrammarKeepsConstructionFailures(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var valid = Nested(31, "(?:", "(?<x>a)");
        foreach (var pattern in new[] { valid[..^1], valid + ")", valid + @"\k<missing>", Nested(31, "(?:", "(?(1)a|b|c)") })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options));
            Assert.Equal(expected.Error, actual.Error);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, options));
        }
        Assert.NotNull(Utf8OwnedTreeParser.TryParse(valid, options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedCapturesRespectLimitsCancellationAndRecovery(bool compiled)
    {
        var pattern = Nested(257, "(", "a");
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var input = "a"u8.ToArray();
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.IsMatch(program, input, starts, ends, ref baseline).IsMatch);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var limited = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
                Utf8SliceRunner.IsMatch(program, input, starts, ends, ref limited).Breach);
            Assert.Equal(0UL, limited.Ledger.ReservedBytes);
        }
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var storage = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.TrailReserveBytes - 1),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8SliceRunner.IsMatch(program, input, starts, ends, ref storage).Breach);
        Assert.Equal(0UL, storage.Ledger.ReservedBytes);
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        canceled.CheckpointHook = steps => { if (steps >= 64) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.IsMatch(program, input, starts, ends, ref canceled));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.IsMatch(input));
        Assert.False(regex.IsMatch("b"u8));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
