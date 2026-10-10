using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Fabricated test input graphs borrow their child arrays. The operation under
// test admits its own acquisitions, independently of fixture preparation.
internal static class Utf8BorrowedTreeTestInputs
{
    internal static Utf8TreeNode WithChild(this Utf8TreeNode parent, Utf8TreeNode child)
    {
        var children = new Utf8TreeNode[parent.ChildCount + 1];
        for (var index = 0; index < parent.ChildCount; index++) children[index] = parent.Child(index);
        children[^1] = child;
        parent.Children = children.Length == 1 ? child : children;
        child.Parent = parent;
        return parent;
    }
}
