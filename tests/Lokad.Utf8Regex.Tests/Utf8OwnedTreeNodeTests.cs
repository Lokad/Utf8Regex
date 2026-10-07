using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned tree-node structural checks: kind values round-trip, constructors store
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
        var node = new Utf8TreeNode((Utf8TreeNodeKind)value, RegexOptions.CultureInvariant);
        Assert.Equal(value, (int)node.Kind);
        Assert.Equal(RegexOptions.CultureInvariant, node.Options);
        Assert.Equal(0, node.ChildCount);
        Assert.Empty(node.ChildList);
    }

    [Fact]
    public void OwnedConstructorsStorePayloads()
    {
        var charNode = new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        Assert.Equal('a', charNode.Ch);
        Assert.Equal(0, charNode.M);
        Assert.Equal(0, charNode.N);

        var textNode = new Utf8TreeNode(Utf8TreeNodeKind.Multi, RegexOptions.None, "ab");
        Assert.Equal("ab", textNode.Str);

        var rangeNode = new Utf8TreeNode(Utf8TreeNodeKind.Loop, RegexOptions.None, 1, 3);
        Assert.Equal(1, rangeNode.M);
        Assert.Equal(3, rangeNode.N);

        var slotNode = new Utf8TreeNode(Utf8TreeNodeKind.Capture, RegexOptions.None, 2);
        Assert.Equal(2, slotNode.M);

        textNode.Str2 = "witness";
        Assert.Equal("witness", textNode.Str2);
    }

    [Fact]
    public void OwnedChildStorageKeepsParentLinks()
    {
        var parent = new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, RegexOptions.None);
        Assert.Equal(0, parent.ChildCount);
        Assert.Empty(parent.ChildList);

        for (var i = 0; i < 3; i++)
        {
            var child = new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.None, (char)('a' + i));
            parent.AddChild(child);
            Assert.Equal(i + 1, parent.ChildCount);
            Assert.Same(parent, parent.Child(i).Parent);
            Assert.Equal((char)('a' + i), parent.Child(i).Ch);
        }

        Assert.Equal(3, parent.ChildList.Count);
    }
}
