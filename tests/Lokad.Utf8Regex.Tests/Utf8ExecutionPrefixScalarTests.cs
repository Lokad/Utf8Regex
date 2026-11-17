using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ExecutionPrefixScalarTests
{

    [Fact]
    public void LexicalFlagTransferPreservesEveryLoweredNode()
    {
        foreach (var pattern in new[] { "(?i:k)", "(?i:abc)", "a(?i:b)c", "(?i:é)" })
        {
            var semantic = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant).SemanticRegex;
            var execution = Utf8ExecutionTreeLowerer.Lower(semantic);
            Assert.NotNull(execution); Assert.NotNull(semantic.RuntimeTree);
            var pairs = new Stack<(Utf8TreeNode Owned, Utf8ExecutionNode Lowered)>();
            pairs.Push((semantic.RuntimeTree.Root, execution.Root));
            while (pairs.TryPop(out var pair))
            {
                Assert.Equal(pair.Owned.MatchCaseInsensitive, (pair.Lowered.Options & RegexOptions.IgnoreCase) != 0);
                Assert.Equal(pair.Owned.ChildCount, pair.Lowered.Children.Count);
                for (var index = 0; index < pair.Owned.ChildCount; index++) pairs.Push((pair.Owned.Child(index), pair.Lowered.Children[index]));
            }
        }
    }
}
