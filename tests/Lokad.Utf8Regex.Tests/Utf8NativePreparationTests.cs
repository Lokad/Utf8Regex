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

    [Theory]
    [InlineData("(?:😀é){3}x|a🙏")]
    [InlineData("(ab|cd)+")]
    [InlineData("(?:😀é){100}x")]
    [InlineData("x(?i:ab)é|y🙏")]
    public void NativeCoveringPrefixesPreserveScalarClippingAndBranchCoverage(string pattern)
    {
        const RegexOptions options = RegexOptions.CultureInvariant;
        var native = Parse(pattern, options);
        var text = Assert.IsType<Utf8OwnedRegexTree>(Utf8OwnedTreeParser.TryParse(pattern, options));
        var expected = Utf8OwnedFindOptimizations.FindAlternatePrefixes(text.Root, options);
        var actual = Utf8OwnedFindOptimizations.FindAlternatePrefixes(native.Root, options);
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MixedSourceRopesEmitOneCompleteUtf8Prefix()
    {
        var root = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, RegexOptions.CultureInvariant);
        root.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'x'));
        root.WithChild(Utf8TreeNode.CreateUtf8Literal(RegexOptions.CultureInvariant, "é😀"u8.ToArray()));
        root.WithChild(Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "yz"));
        Assert.Equal(new[] { "xé😀yz"u8.ToArray() },
            Utf8OwnedFindOptimizations.FindAlternatePrefixes(root, RegexOptions.CultureInvariant));
    }
}
