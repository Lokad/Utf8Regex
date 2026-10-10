using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned tree-node structural checks: kind values round-trip, factories store
// their payloads, and child storage transitions (empty to single to list) keep
// parent links. Former fork-equivalence pins were removed at the cutover; these
// pin the owned representation directly.
public sealed class Utf8OwnedTreeNodeTests
{
    public static IEnumerable<object?[]> KindCases()
    {
        yield return new object?[] { 0 };
        yield return new object?[] { 3 };
        yield return new object?[] { 4 };
        yield return new object?[] { 5 };
        yield return new object?[] { 6 };
        yield return new object?[] { 7 };
        yield return new object?[] { 8 };
        yield return new object?[] { 9 };
        yield return new object?[] { 10 };
        yield return new object?[] { 11 };
        yield return new object?[] { 12 };
        yield return new object?[] { 13 };
        yield return new object?[] { 14 };
        yield return new object?[] { 15 };
        yield return new object?[] { 16 };
        yield return new object?[] { 17 };
        yield return new object?[] { 18 };
        yield return new object?[] { 19 };
        yield return new object?[] { 20 };
        yield return new object?[] { 21 };
        yield return new object?[] { 22 };
        yield return new object?[] { 23 };
        yield return new object?[] { 24 };
        yield return new object?[] { 25 };
        yield return new object?[] { 26 };
        yield return new object?[] { 27 };
        yield return new object?[] { 28 };
        yield return new object?[] { 29 };
        yield return new object?[] { 30 };
        yield return new object?[] { 31 };
        yield return new object?[] { 32 };
        yield return new object?[] { 33 };
        yield return new object?[] { 34 };
        yield return new object?[] { 41 };
        yield return new object?[] { 42 };
        yield return new object?[] { 43 };
        yield return new object?[] { 44 };
        yield return new object?[] { 45 };
        yield return new object?[] { 46 };
    }

    [Theory]
    [MemberData(nameof(KindCases))]
    public void OwnedKindRoundTripsValue(int value)
    {
        var node = Utf8TreeNode.Create((Utf8TreeNodeKind)value, RegexOptions.CultureInvariant);
        Assert.Equal(value, (int)node.Kind);
        Assert.Equal(RegexOptions.CultureInvariant, node.Options);
        Assert.Equal(0, node.ChildCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => node.Child(0));
    }

    [Fact]
    public void OwnedFactoriesStorePayloads()
    {
        var charNode = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        Assert.Equal('a', charNode.Ch);
        Assert.Equal(0, charNode.M);
        Assert.Equal(0, charNode.N);

        var textNode = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, "ab");
        Assert.Equal("ab", textNode.Str);

        var rangeNode = Utf8TreeNode.Create(Utf8TreeNodeKind.Loop, RegexOptions.None, 1, 3);
        Assert.Equal(1, rangeNode.M);
        Assert.Equal(3, rangeNode.N);

        var slotNode = Utf8TreeNode.Create(Utf8TreeNodeKind.Capture, RegexOptions.None, 2, 0);
        Assert.Equal(2, slotNode.M);

    }

    [Fact]
    public void OwnedChildStorageKeepsParentLinks()
    {
        var empty = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, RegexOptions.None);
        Assert.Equal(0, empty.ChildCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.Child(0));
        var parsed = Utf8OwnedTreeParser.TryParse("(a)(b)(c)", RegexOptions.None);
        Assert.NotNull(parsed);
        var parent = parsed.Root.Child(0);
        Assert.Equal(3, parent.ChildCount);
        Assert.IsType<Utf8TreeNode[]>(parent.Children);
        for (var index = 0; index < parent.ChildCount; index++)
        {
            var child = parent.Child(index);
            Assert.Same(parent, child.Parent);
            Assert.Equal((char)('a' + index), child.Child(0).Ch);
        }
    }
}
