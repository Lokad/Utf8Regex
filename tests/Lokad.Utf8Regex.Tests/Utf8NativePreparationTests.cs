using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Execution;

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

    private static Utf8OwnedRegexTree Parse(string pattern, RegexOptions options = RegexOptions.CultureInvariant)
    {
        var tree = Utf8OwnedTreeParser.TryParse(Encoding.UTF8.GetBytes(pattern), options, out var diagnostic);
        Assert.False(diagnostic.IsSyntaxError, diagnostic.ToString());
        return Assert.IsType<Utf8OwnedRegexTree>(tree);
    }
}
