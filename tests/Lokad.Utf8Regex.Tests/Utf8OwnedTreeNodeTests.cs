using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned tree-node equivalence: every kind, constructor shape, and storage
// transition must match the fork node member-for-member, since consumers
// migrate by renaming. Expected values come from the fork live at run time;
// the input rows below only enumerate the vocabulary and shapes.
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
    public void OwnedKindMatchesFork(int value)
    {
        var expected = new RuntimeFrontEnd.RegexNode((RuntimeFrontEnd.RegexNodeKind)value, RegexOptions.CultureInvariant);
        var actual = new Utf8TreeNode((Utf8TreeNodeKind)value, RegexOptions.CultureInvariant);
        Assert.Equal((int)expected.Kind, (int)actual.Kind);
        Assert.Equal(value, (int)actual.Kind);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.IsBacktrackingConstruct, actual.IsBacktrackingConstruct);
        Assert.Equal(expected.ChildCount, actual.ChildCount);
    }

    [Fact]
    public void OwnedConstructorsMatchFork()
    {
        var forkChar = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, RegexOptions.None, 'a');
        var ownedChar = new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        Assert.Equal(forkChar.Ch, ownedChar.Ch);
        Assert.Equal(forkChar.M, ownedChar.M);
        Assert.Equal(forkChar.N, ownedChar.N);

        var forkText = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Multi, RegexOptions.None, "ab");
        var ownedText = new Utf8TreeNode(Utf8TreeNodeKind.Multi, RegexOptions.None, "ab");
        Assert.Equal(forkText.Str, ownedText.Str);

        var forkRange = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Loop, RegexOptions.None, 1, 3);
        var ownedRange = new Utf8TreeNode(Utf8TreeNodeKind.Loop, RegexOptions.None, 1, 3);
        Assert.Equal(forkRange.M, ownedRange.M);
        Assert.Equal(forkRange.N, ownedRange.N);

        var forkSlot = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Capture, RegexOptions.None, 2);
        var ownedSlot = new Utf8TreeNode(Utf8TreeNodeKind.Capture, RegexOptions.None, 2);
        Assert.Equal(forkSlot.M, ownedSlot.M);

        forkText.Str2 = "witness";
        ownedText.Str2 = "witness";
        Assert.Equal(forkText.Str2, ownedText.Str2);
    }

    [Fact]
    public void OwnedChildStorageMatchesFork()
    {
        var forkParent = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.Concatenate, RegexOptions.None);
        var ownedParent = new Utf8TreeNode(Utf8TreeNodeKind.Concatenate, RegexOptions.None);
        Assert.Equal(forkParent.ChildCount, ownedParent.ChildCount);
        Assert.Equal(forkParent.ChildList.Count, ownedParent.ChildList.Count);

        for (var i = 0; i < 3; i++)
        {
            var forkChild = new RuntimeFrontEnd.RegexNode(RuntimeFrontEnd.RegexNodeKind.One, RegexOptions.None, (char)('a' + i));
            var ownedChild = new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.None, (char)('a' + i));
            forkParent.AddChild(forkChild);
            ownedParent.AddChild(ownedChild);
            Assert.Equal(forkParent.ChildCount, ownedParent.ChildCount);
            Assert.Same(forkParent, forkParent.Child(i).Parent);
            Assert.Same(ownedParent, ownedParent.Child(i).Parent);
            Assert.Equal(forkParent.Child(i).Ch, ownedParent.Child(i).Ch);
        }

        Assert.Equal(forkParent.ChildList.Count, ownedParent.ChildList.Count);
    }
}