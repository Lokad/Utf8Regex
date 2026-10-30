using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativePreparationTests
{
    public static IEnumerable<object?[]> WidthCases() => Utf8OwnedFindOptimizationsTests.FactsCases()
        .Select(row => new object?[] { row[0], row[1], row[2], row[3], (int)(Utf8TreeNodeKind)row[4]! });

    [Theory]
    [MemberData(nameof(WidthCases))]
    public void NativeByteWidthsKeepExistingPinnedBounds(string pattern, RegexOptions options,
        int minimum, int? maximum, int anchor)
    {
        var tree = Parse(pattern, options);
        var width = Utf8OwnedFindOptimizations.ComputeByteWidth(tree.Root);
        Assert.Equal(minimum, width.Minimum); Assert.Equal(maximum, width.Maximum);
        Assert.Equal((Utf8TreeNodeKind)anchor, Utf8OwnedFindOptimizations.FindTrailingAnchor(tree.Root));
    }

    [Theory]
    [InlineData("é😀e\u0301", RegexOptions.CultureInvariant, 9, 9)]
    [InlineData("é😀\0", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, 7, 7)]
    [InlineData("(?i:kK)", RegexOptions.CultureInvariant, 2, 6)]
    [InlineData("(?i:😀)", RegexOptions.CultureInvariant, 1, 4)]
    [InlineData("(?<=😀)é{2}", RegexOptions.CultureInvariant, 4, 4)]
    public void ScalarAndFoldedBoundsDoNotBecomeByteCounts(string pattern, RegexOptions options, int minimum, int maximum)
    {
        var native = Utf8OwnedFindOptimizations.ComputeByteWidth(Parse(pattern, options).Root);
        var text = Utf8OwnedFindOptimizations.ComputeByteWidth(Utf8OwnedTreeParser.TryParse(pattern, options)!.Root);
        Assert.Equal(text, native);
        Assert.Equal(minimum, native.Minimum); Assert.Equal(maximum, native.Maximum);
    }

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("(?<x>foo)bar", "foobar")]
    [InlineData("(?:a{1})bc", "abc")]
    [InlineData("\0-42", "\0-42")]
    public void BorrowedAsciiQueriesCopyNativeLiteralSlices(string pattern, string expected)
    {
        var root = Parse(pattern).Root;
        using var scope = Utf8ConstructionScope.Enter();
        using var cursor = new Utf8FallbackTreeQuery.Cursor(root);
        Assert.True(cursor.ReadLiteral(root, out var slice));
        var bytes = Encoding.UTF8.GetBytes(expected);
        Assert.Equal(bytes.Length, slice.Length);
        Assert.Equal(bytes[0], Utf8FallbackTreeQuery.First(slice));
        Assert.Equal(bytes, Utf8FallbackTreeQuery.CopySlice(slice, scope.Budget));
        Assert.False(cursor.Next(out _));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("(?:abc)")]
    [InlineData("(?<x>abc)")]
    public void QualifiedNativeLiteralCopyRemainsOwned(string pattern)
    {
        var input = Encoding.UTF8.GetBytes(pattern);
        var tree = Utf8OwnedTreeParser.TryParse(input, RegexOptions.CultureInvariant, out _)!;
        Array.Fill(input, (byte)0xff);
        using var scope = Utf8ConstructionScope.Enter();
        Assert.True(Utf8FallbackTreeQuery.TryLiteral(tree.Root, out var literal));
        Assert.Equal(3, Utf8FallbackTreeQuery.LiteralLength(literal));
        Assert.Equal("abc"u8.ToArray(), Utf8FallbackTreeQuery.CopyLiteral(literal, scope.Budget));
    }

    [Theory]
    [InlineData("é")]
    [InlineData("abc😀")]
    [InlineData("a\u0080")]
    [InlineData("K")]
    public void AsciiProofDoesNotAdmitNativeUnicode(string pattern)
    {
        var root = Parse(pattern).Root;
        using var scope = Utf8ConstructionScope.Enter();
        Assert.False(Utf8FallbackTreeQuery.TryLiteral(root, out _));
        using var cursor = new Utf8FallbackTreeQuery.Cursor(root);
        Assert.False(cursor.ReadLiteral(root, out _));
    }

    [Fact]
    public void NativeQueryShapesRetainWildcardPositionsAndCheckedTails()
    {
        var root = Parse("(?i:[aA].\\-42)").Root;
        using var scope = Utf8ConstructionScope.Enter();
        using var cursor = new Utf8FallbackTreeQuery.Cursor(root);
        Assert.True(cursor.ReadShape(root, Utf8ScalarCaseBehavior.Invariant, true, out var slice, out var wildcards));
        Assert.Equal(5, slice.Length); Assert.Equal((UInt128)2, wildcards);
        var bytes = new byte[5];
        Utf8FallbackTreeQuery.AppendShape(slice, bytes, 0, Utf8ScalarCaseBehavior.Invariant, true);
        Assert.Equal("a.-42"u8.ToArray(), bytes);
        for (var index = 0; index < bytes.Length; index++)
            Assert.Equal(bytes[index], Utf8FallbackTreeQuery.ShapeByte(slice, index, Utf8ScalarCaseBehavior.Invariant, true));
        var tail = new byte[3];
        Utf8FallbackTreeQuery.AppendShape(slice, tail, 2, Utf8ScalarCaseBehavior.Invariant, true);
        Assert.Equal("-42"u8.ToArray(), tail);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RequiredFamiliesUseByteOrderAndByteScoresWithoutChangingMatches(bool native)
    {
        static Utf8OwnedRegexTree Tree(string pattern, bool bytes) => bytes ? Parse(pattern)
            : Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant)!;
        var alternatives = Tree("[ab](😀|\ue000)|[cd](😀|\ue000)", native);
        var family = Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(alternatives.Root);
        Assert.Equal(new[] { "\ue000"u8.ToArray(), "😀"u8.ToArray() }, family);
        var scored = Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(Tree("abc[0]😀", native).Root);
        Assert.Equal(new[] { "😀"u8.ToArray() }, scored);

        var expression = new Utf8Regex("[ab](😀|\ue000)|[cd](😀|\ue000)", RegexOptions.CultureInvariant);
        var input = "a😀 b\ue000 c😀 d\ue000"u8.ToArray();
        Assert.Equal(4, expression.Count(input));
        using var cursor = expression.CreateMatchCursor(input);
        var ranges = new List<(int, int)>();
        while (cursor.MoveNext(out var match)) ranges.Add((match.Value.StartOffsetInBytes,
            match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));
        Assert.Equal(new[] { (0, 5), (6, 4), (11, 5), (17, 4) }, ranges);
    }

    [Theory]
    [InlineData("é😀x", 7, 7)]
    [InlineData("(?i:k)x", 2, 4)]
    [InlineData("(?:😀|é){2}x", 5, 9)]
    public void ExecutionTreeWidthRetainsNativeLiteralPayloads(string pattern, int minimum, int maximum)
    {
        var options = RegexOptions.CultureInvariant;
        var native = ExecutionTree(Parse(pattern, options), options);
        var text = ExecutionTree(Utf8OwnedTreeParser.TryParse(pattern, options)!, options);
        var width = Utf8ExecutionPrefixFacts.Width(native.Root);
        Assert.Equal(Utf8ExecutionPrefixFacts.Width(text.Root), width);
        Assert.Equal(minimum, width.Minimum); Assert.Equal(maximum, width.Maximum);
    }

    [Fact]
    public void NativeUnicodePrefixGuardsUseActualByteDistances()
    {
        var source = Parse("é😀x");
        var tree = ExecutionTree(source, RegexOptions.CultureInvariant);
        Assert.Same(source.Root.Child(0).LiteralUtf8, tree.Root.Children[0].LiteralUtf8);
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None));
        var guards = Utf8DeterministicVerifierGuards.Create(tree, plan);
        Assert.True(guards.HasValue); Assert.Single(guards.PrefixGuards!);
        Assert.True(guards.Matches("é😀x"u8, 0));
        Assert.True(guards.Matches("aé😀x"u8, 1));
        Assert.False(guards.Matches("é😀y"u8, 0));
        Assert.False(guards.Matches("éx"u8, 0));
    }

    [Theory]
    [InlineData("(?i:k)x", "Kx")]
    [InlineData("(?i:😀)x", "😀x")]
    [InlineData("(?:é|😀)x", "😀x")]
    public void VariableNativeWidthCannotInventAConflictingFixedGuard(string pattern, string subject)
    {
        var tree = ExecutionTree(Parse(pattern), RegexOptions.CultureInvariant);
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None));
        var guards = Utf8DeterministicVerifierGuards.Create(tree, plan);
        Assert.True(guards.Matches(Encoding.UTF8.GetBytes(subject), 0));
    }

    private static Utf8ExecutionTree ExecutionTree(Utf8OwnedRegexTree tree, RegexOptions options)
        => Utf8ExecutionTreeLowerer.Lower(new Utf8SemanticRegex(string.Empty, options, string.Empty, options, tree))!;

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TextFactsCannotSilentlyJoinAcrossAnUnrepresentedNativeFragment(bool leadingAscii)
    {
        var root = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, RegexOptions.CultureInvariant);
        if (leadingAscii) root.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'x'));
        root.WithChild(Utf8TreeNode.CreateUtf8Literal(RegexOptions.CultureInvariant, "é"u8.ToArray()));
        root.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'y'));
        Assert.Equal(leadingAscii ? "x" : null, Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(root));
        Assert.Equal(leadingAscii ? "x" : "y", Utf8OwnedFindOptimizations.TryExtractLongestLiteral(root));
    }

    private static Utf8OwnedRegexTree Parse(string pattern, RegexOptions options = RegexOptions.CultureInvariant)
    {
        var tree = Utf8OwnedTreeParser.TryParse(Encoding.UTF8.GetBytes(pattern), options, out var diagnostic);
        Assert.False(diagnostic.IsSyntaxError, diagnostic.ToString());
        return Assert.IsType<Utf8OwnedRegexTree>(tree);
    }
}
