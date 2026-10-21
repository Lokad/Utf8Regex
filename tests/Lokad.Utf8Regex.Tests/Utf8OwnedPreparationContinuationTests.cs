using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedPreparationContinuationTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var depth in new[] { 65, 16385 })
        foreach (var kind in new[] { Utf8TreeNodeKind.Capture, Utf8TreeNodeKind.Group, Utf8TreeNodeKind.Atomic })
        foreach (var scoped in new[] { false, true })
            yield return [depth, (int)kind, scoped];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeepPreparationKeepsChildOrderAndPayloads(int depth, int wrapperKind, bool scoped)
    {
        var options = RegexOptions.CultureInvariant;
        var pattern = scoped ? "(?<x>ab)(?i:[cd]+)(?=e)" : "(?<x>ab)[cd]+(?=e)";
        var owned = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(owned);
        var body = owned.Root.Child(0);
        for (var level = 0; level < depth; level++)
        {
            var wrapper = Utf8TreeNode.Create((Utf8TreeNodeKind)wrapperKind, options, 1, -1);
            wrapper.Children = body;
            body.Parent = wrapper;
            body = wrapper;
        }
        owned.Root.Children = body;
        body.Parent = owned.Root;
        Assert.Equal(scoped, Utf8AsciiSimplePatternLowerer.HasScopedIgnoreCase(owned.Root));
        var semantic = new Utf8SemanticRegex(pattern, options, pattern, options, owned);
        var tree = Utf8ExecutionTreeLowerer.Lower(semantic);
        Assert.NotNull(tree);
        var pending = new Stack<(Utf8TreeNode Source, Utf8ExecutionNode Copy)>();
        pending.Push((owned.Root, tree.Root));
        var nodeCount = 0;
        while (pending.TryPop(out var pair))
        {
            nodeCount++;
            Assert.Equal(pair.Source.Options & ~RegexOptions.IgnoreCase, pair.Copy.Options & ~RegexOptions.IgnoreCase);
            Assert.Equal(pair.Source.MatchCaseInsensitive, (pair.Copy.Options & RegexOptions.IgnoreCase) != 0);
            if (pair.Source.ScalarClass is { } scalarClass)
            {
                Assert.Same(scalarClass, pair.Copy.Payload);
                Assert.Equal(string.Empty, pair.Copy.Text);
            }
            else Assert.Equal(pair.Source.Str ?? "", pair.Copy.Text);
            Assert.Equal(pair.Source.Ch, pair.Copy.Ch);
            Assert.Equal(pair.Source.M, pair.Copy.Min);
            Assert.Equal(pair.Source.N, pair.Copy.Max);
            Assert.Equal(pair.Source.ChildCount, pair.Copy.Children.Count);
            for (var index = pair.Source.ChildCount - 1; index >= 0; index--)
                pending.Push((pair.Source.Child(index), pair.Copy.Children[index]));
        }
        Assert.True(nodeCount > depth);
    }
}
