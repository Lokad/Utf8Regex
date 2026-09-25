using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned tree analysis: the differential battery pins per-node atomicity,
// capture containment, backtracking, and loop facts plus tree-wide flags
// identical to the legacy instance (oracle first, then owned) across the
// probed pattern catalog.
public sealed class Utf8OwnedTreeAnalysisTests
{
    public static IEnumerable<object?[]> AnalysisCases()
    {        yield return new object?[] { "", 512, "100", "Capture:1100;Concatenate:1000" };
        yield return new object?[] { "abc", 512, "100", "Capture:1100;Multi:1000" };
        yield return new object?[] { "a|b", 512, "100", "Capture:1100;Alternate:1000;One:1000;One:1000" };
        yield return new object?[] { "(a|b)", 512, "100", "Capture:1100;Capture:1100;Alternate:1000;One:1000;One:1000" };
        yield return new object?[] { "(a|b|c)", 512, "100", "Capture:1100;Capture:1100;Alternate:1000;One:1000;One:1000;One:1000" };
        yield return new object?[] { "a(b|c)d", 512, "100", "Capture:1110;Concatenate:1110;One:0000;Capture:0110;Alternate:0010;One:0000;One:0000;One:1000" };
        yield return new object?[] { "(ab|cd)(ef|gh)", 512, "100", "Capture:1110;Concatenate:1110;Capture:0110;Alternate:0010;Multi:0000;Multi:0000;Capture:1100;Alternate:1000;Multi:1000;Multi:1000" };
        yield return new object?[] { "(a)", 512, "100", "Capture:1100;Capture:1100;One:1000" };
        yield return new object?[] { "(a)(b)", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;Capture:1100;One:1000" };
        yield return new object?[] { "((a))", 512, "100", "Capture:1100;Capture:1100;Capture:1100;One:1000" };
        yield return new object?[] { "(?<n>a)", 512, "100", "Capture:1100;Capture:1100;One:1000" };
        yield return new object?[] { "(?'n'a)", 512, "100", "Capture:1100;Capture:1100;One:1000" };
        yield return new object?[] { "(?<a>x)(?<b>y)", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;Capture:1100;One:1000" };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;Capture:0100;One:0000;Capture:0100;One:0000;Capture:0100;One:0000;Capture:0100;One:0000;Capture:0100;One:0000;Capture:0100;One:0000;Capture:0100;One:0000;Capture:0100;One:0000;Capture:1100;One:1000" };
        yield return new object?[] { "(a)(?(1)b|c)", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;BackreferenceConditional:1000;One:1000;One:1000" };
        yield return new object?[] { "(?<a>x)(?(a)b|c)", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;BackreferenceConditional:1000;One:1000;One:1000" };
        yield return new object?[] { "(?(?=a)b|c)", 512, "100", "Capture:1100;ExpressionConditional:1000;PositiveLookaround:1000;One:1000;One:1000;One:1000" };
        yield return new object?[] { "(?(?!a)b|c)", 512, "100", "Capture:1100;ExpressionConditional:1000;NegativeLookaround:1000;One:1000;One:1000;One:1000" };
        yield return new object?[] { "(a)(?(1)b|)", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;BackreferenceConditional:1000;One:1000;Concatenate:1000" };
        yield return new object?[] { "a*", 512, "100", "Capture:1100;Oneloop:1000" };
        yield return new object?[] { "a+", 512, "100", "Capture:1100;Oneloop:1000" };
        yield return new object?[] { "a?", 512, "100", "Capture:1100;Oneloop:1000" };
        yield return new object?[] { "a{2}", 512, "100", "Capture:1100;Oneloop:1000" };
        yield return new object?[] { "a{2,}", 512, "100", "Capture:1100;Oneloop:1000" };
        yield return new object?[] { "a{2,4}", 512, "100", "Capture:1100;Oneloop:1000" };
        yield return new object?[] { "a*?", 512, "100", "Capture:1100;Onelazy:1000" };
        yield return new object?[] { "a{0}", 512, "100", "Capture:1100;Empty:1000" };
        yield return new object?[] { "a{0,1}", 512, "100", "Capture:1100;Oneloop:1000" };
        yield return new object?[] { "(ab)*", 512, "100", "Capture:1100;Loop:1100;Capture:0101;Multi:0001" };
        yield return new object?[] { "(ab)+", 512, "100", "Capture:1100;Loop:1100;Capture:0101;Multi:0001" };
        yield return new object?[] { "(a|b)*", 512, "100", "Capture:1110;Loop:1110;Capture:0111;Alternate:0011;One:0001;One:0001" };
        yield return new object?[] { "(a|b)+", 512, "100", "Capture:1110;Loop:1110;Capture:0111;Alternate:0011;One:0001;One:0001" };
        yield return new object?[] { "(a*)*", 512, "100", "Capture:1110;Loop:1110;Capture:0111;Oneloop:0011" };
        yield return new object?[] { "(a+)+", 512, "100", "Capture:1110;Loop:1110;Capture:0111;Oneloop:0011" };
        yield return new object?[] { "((a)*)*", 512, "100", "Capture:1110;Loop:1110;Capture:0111;Loop:0111;Capture:0101;One:0001" };
        yield return new object?[] { "(a*)+?", 512, "100", "Capture:1110;Lazyloop:1110;Capture:0111;Oneloop:0011" };
        yield return new object?[] { "(?>a)", 512, "100", "Capture:1100;Atomic:1000;One:1000" };
        yield return new object?[] { "(?>a|b)", 512, "100", "Capture:1100;Atomic:1000;Alternate:1000;One:1000;One:1000" };
        yield return new object?[] { "(?>a+)", 512, "100", "Capture:1100;Atomic:1000;Oneloop:1000" };
        yield return new object?[] { "(?>a)+", 512, "100", "Capture:1100;Loop:1000;Atomic:0001;One:1001" };
        yield return new object?[] { "(?=a)", 512, "100", "Capture:1100;PositiveLookaround:1000;One:1000" };
        yield return new object?[] { "(?!a)", 512, "100", "Capture:1100;NegativeLookaround:1000;One:1000" };
        yield return new object?[] { "(?<=a)", 512, "101", "Capture:1100;PositiveLookaround:1000;One:1000" };
        yield return new object?[] { "(?<!a)", 512, "101", "Capture:1100;NegativeLookaround:1000;One:1000" };
        yield return new object?[] { "(?=(a))", 512, "100", "Capture:1100;PositiveLookaround:1100;Capture:1100;One:1000" };
        yield return new object?[] { "(?!(a|b))", 512, "100", "Capture:1100;NegativeLookaround:1100;Capture:1100;Alternate:1000;One:1000;One:1000" };
        yield return new object?[] { "(?<=a+)b", 512, "101", "Capture:1100;Concatenate:1000;PositiveLookaround:0000;Oneloop:1000;One:1000" };
        yield return new object?[] { "(?>(a))(b)", 512, "100", "Capture:1100;Concatenate:1100;Atomic:0100;Capture:1100;One:1000;Capture:1100;One:1000" };
        yield return new object?[] { "(?=a+)a", 512, "100", "Capture:1100;Concatenate:1000;PositiveLookaround:0000;Oneloop:1000;One:1000" };
        yield return new object?[] { "(?!a+)a", 512, "100", "Capture:1100;Concatenate:1000;NegativeLookaround:0000;Oneloop:1000;One:1000" };
        yield return new object?[] { "(a)\\1", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;Backreference:1000" };
        yield return new object?[] { "(a)(b)\\2", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;Capture:0100;One:0000;Backreference:1000" };
        yield return new object?[] { "(?<n>a)\\k<n>", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;Backreference:1000" };
        yield return new object?[] { "^a", 512, "100", "Capture:1100;Concatenate:1000;Beginning:0000;One:1000" };
        yield return new object?[] { "a$", 512, "100", "Capture:1100;Concatenate:1000;One:0000;EndZ:1000" };
        yield return new object?[] { "\\Aa", 512, "100", "Capture:1100;Concatenate:1000;Beginning:0000;One:1000" };
        yield return new object?[] { "a\\z", 512, "100", "Capture:1100;Concatenate:1000;One:0000;End:1000" };
        yield return new object?[] { "a\\Z", 512, "100", "Capture:1100;Concatenate:1000;One:0000;EndZ:1000" };
        yield return new object?[] { "\\Ga", 512, "100", "Capture:1100;Concatenate:1000;Start:0000;One:1000" };
        yield return new object?[] { "\\ba\\b", 512, "100", "Capture:1100;Concatenate:1000;Boundary:0000;One:0000;Boundary:1000" };
        yield return new object?[] { "\\B", 512, "100", "Capture:1100;NonBoundary:1000" };
        yield return new object?[] { "[ab]", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { "[^ab]", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { ".", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { "\\d", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { "\\w", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { "\\s", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { "\\D", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { "\\p{L}", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { "\\P{L}", 512, "100", "Capture:1100;Set:1000" };
        yield return new object?[] { "(?i:ab)", 512, "110", "Capture:1100;Group:1000;Concatenate:0000;Set:0000;Set:0000" };
        yield return new object?[] { "(?m:^a)", 512, "100", "Capture:1100;Group:1000;Concatenate:0000;Bol:0000;One:0000" };
        yield return new object?[] { "(?s:a.b)", 512, "100", "Capture:1100;Group:1000;Concatenate:0000;One:0000;Set:0000;One:0000" };
        yield return new object?[] { "(?i:(a))", 512, "110", "Capture:1100;Group:1100;Capture:0100;Set:0000" };
        yield return new object?[] { "a(?i:b)c", 512, "110", "Capture:1100;Concatenate:1000;One:0000;Group:0000;Set:0000;One:1000" };
        yield return new object?[] { "ab", 513, "110", "Capture:1100;Concatenate:1000;Set:0000;Set:1000" };
        yield return new object?[] { "ab", 576, "101", "Capture:1100;Multi:1000" };
        yield return new object?[] { "ab", 514, "100", "Capture:1100;Multi:1000" };
        yield return new object?[] { "ab", 528, "100", "Capture:1100;Multi:1000" };
        yield return new object?[] { "(a)b", 513, "110", "Capture:1100;Concatenate:1100;Capture:0100;Set:0000;Set:1000" };
        yield return new object?[] { "(?<=a)b", 576, "101", "Capture:1100;Concatenate:1000;PositiveLookaround:0000;One:1000;One:1000" };
        yield return new object?[] { "(?-i:a)b", 513, "100", "Capture:1100;Concatenate:1000;Group:0000;One:0000;One:1000" };
        yield return new object?[] { "a|b|c|d|e", 512, "100", "Capture:1100;Alternate:1000;One:1000;One:1000;One:1000;One:1000;One:1000" };
        yield return new object?[] { "(a(b(c)))", 512, "100", "Capture:1100;Capture:1100;Concatenate:1100;One:0000;Capture:1100;Concatenate:1100;One:0000;Capture:1100;One:1000" };
        yield return new object?[] { "(a(b))(c(d))", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;Concatenate:0100;One:0000;Capture:0100;One:0000;Capture:1100;Concatenate:1100;One:0000;Capture:1100;One:1000" };
        yield return new object?[] { "(?:a)", 512, "100", "Capture:1100;Group:1000;One:0000" };
        yield return new object?[] { "(?:a|b)*c", 512, "100", "Capture:1110;Concatenate:1010;Loop:0010;Group:0011;Alternate:0011;One:0001;One:0001;One:1000" };
        yield return new object?[] { "\\A(?:a|b)+$", 512, "100", "Capture:1110;Concatenate:1010;Beginning:0000;Loop:0010;Group:0011;Alternate:0011;One:0001;One:0001;EndZ:1000" };
        yield return new object?[] { "(a|ab|abc)", 512, "100", "Capture:1100;Capture:1100;Alternate:1000;One:1000;Multi:1000;Multi:1000" };
        yield return new object?[] { "(abc|ab|a)", 512, "100", "Capture:1100;Capture:1100;Alternate:1000;Multi:1000;Multi:1000;One:1000" };
        yield return new object?[] { "(a|b)(c|d)(e|f)", 512, "100", "Capture:1110;Concatenate:1110;Capture:0110;Alternate:0010;One:0000;One:0000;Capture:0110;Alternate:0010;One:0000;One:0000;Capture:1100;Alternate:1000;One:1000;One:1000" };
        yield return new object?[] { "(ab)*cd", 512, "100", "Capture:1110;Concatenate:1110;Loop:0110;Capture:0101;Multi:0001;Multi:1000" };
        yield return new object?[] { "ab(cd)*", 512, "100", "Capture:1100;Concatenate:1100;Multi:0000;Loop:1100;Capture:0101;Multi:0001" };
        yield return new object?[] { "(ab|cd)+ef", 512, "100", "Capture:1110;Concatenate:1110;Loop:0110;Capture:0111;Alternate:0011;Multi:0001;Multi:0001;Multi:1000" };
        yield return new object?[] { "(ab|cd)+?ef", 512, "100", "Capture:1110;Concatenate:1110;Lazyloop:0110;Capture:0111;Alternate:0011;Multi:0001;Multi:0001;Multi:1000" };
        yield return new object?[] { "((((((a))))))", 512, "100", "Capture:1100;Capture:1100;Capture:1100;Capture:1100;Capture:1100;Capture:1100;Capture:1100;One:1000" };
        yield return new object?[] { "(a|b|c|d|e|f|g)+", 512, "100", "Capture:1110;Loop:1110;Capture:0111;Alternate:0011;One:0001;One:0001;One:0001;One:0001;One:0001;One:0001;One:0001" };
        yield return new object?[] { "([ab]+|[cd]+)*", 512, "100", "Capture:1110;Loop:1110;Capture:0111;Alternate:0011;Setloop:0011;Setloop:0011" };
        yield return new object?[] { "[ab]+", 512, "100", "Capture:1100;Setloop:1000" };
        yield return new object?[] { "\\d+", 512, "100", "Capture:1100;Setloop:1000" };
        yield return new object?[] { "a+?", 512, "100", "Capture:1100;Onelazy:1000" };
        yield return new object?[] { "(a)(b|c)(d)", 512, "100", "Capture:1110;Concatenate:1110;Capture:0100;One:0000;Capture:0110;Alternate:0010;One:0000;One:0000;Capture:1100;One:1000" };
        yield return new object?[] { "(?=(a)(b))c", 512, "100", "Capture:1100;Concatenate:1100;PositiveLookaround:0100;Concatenate:1100;Capture:0100;One:0000;Capture:1100;One:1000;One:1000" };
        yield return new object?[] { "(a)(?!(b))c", 512, "100", "Capture:1100;Concatenate:1100;Capture:0100;One:0000;NegativeLookaround:0100;Capture:1100;One:1000;One:1000" };
        yield return new object?[] { "((a)|(b)|(c))d", 512, "100", "Capture:1110;Concatenate:1110;Capture:0110;Alternate:0110;Capture:0100;One:0000;Capture:0100;One:0000;Capture:0100;One:0000;One:1000" };
        yield return new object?[] { "a{3}b{2}c", 512, "100", "Capture:1110;Concatenate:1010;Oneloop:0010;Oneloop:0010;One:1000" };
        yield return new object?[] { "(a{2}b{3})+", 512, "100", "Capture:1110;Loop:1110;Capture:0111;Concatenate:0011;Oneloop:0011;Oneloop:0011" };
    }

    [Theory]
    [MemberData(nameof(AnalysisCases))]
    public void OwnedTreeAnalysisMatchesLegacy(string pattern, int options, string expectedFlags, string expectedNodes)
    {
        var tree = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        Assert.NotNull(tree);
        Assert.NotNull(tree.Root);
        var legacy = RuntimeFrontEnd.RegexTreeAnalyzer.Analyze(tree);
        Assert.Equal(expectedFlags, DumpFlags(legacy.Complete, legacy.HasIgnoreCase, legacy.HasRightToLeft));
        Assert.Equal(expectedNodes, DumpLegacyNodes(tree, legacy));
        var ownedTree = Utf8OwnedRegexTree.FromLegacy(tree);
        Assert.NotNull(ownedTree);
        var owned = Utf8OwnedTreeAnalyzer.Analyze(ownedTree);
        Assert.Equal(expectedFlags, DumpFlags(owned.Complete, owned.HasIgnoreCase, owned.HasRightToLeft));
        Assert.Equal(expectedNodes, DumpOwnedNodes(ownedTree, owned));
    }

    private static string DumpFlags(bool complete, bool hasIgnoreCase, bool hasRightToLeft)
    {
        return string.Concat(complete ? '1' : '0', hasIgnoreCase ? '1' : '0', hasRightToLeft ? '1' : '0');
    }

    private static void CollectNodes(RuntimeFrontEnd.RegexNode node, List<RuntimeFrontEnd.RegexNode> nodes)
    {
        nodes.Add(node);
        for (var i = 0; i < node.ChildCount; i++)
        {
            CollectNodes(node.Child(i), nodes);
        }
    }

    private static string DumpLegacyNodes(RuntimeFrontEnd.RegexTree tree, RuntimeFrontEnd.AnalysisResults analysis)
    {
        Assert.NotNull(tree.Root);
        var nodes = new List<RuntimeFrontEnd.RegexNode>();
        CollectNodes(tree.Root, nodes);
        var builder = new StringBuilder();
        var first = true;
        foreach (var node in nodes)
        {
            if (!first)
            {
                builder.Append(';');
            }

            first = false;
            builder.Append(node.Kind.ToString()).Append(':');
            builder.Append(analysis.IsAtomicByAncestor(node) ? '1' : '0');
            builder.Append(analysis.MayContainCapture(node) ? '1' : '0');
            builder.Append(analysis.MayBacktrack(node) ? '1' : '0');
            builder.Append(analysis.IsInLoop(node) ? '1' : '0');
        }

        return builder.ToString();
    }

    private static void CollectOwnedNodes(Utf8TreeNode node, List<Utf8TreeNode> nodes)
    {
        nodes.Add(node);
        for (var i = 0; i < node.ChildCount; i++)
        {
            CollectOwnedNodes(node.Child(i), nodes);
        }
    }

    private static string DumpOwnedNodes(Utf8OwnedRegexTree tree, Utf8OwnedTreeAnalysis analysis)
    {
        Assert.NotNull(tree.Root);
        var nodes = new List<Utf8TreeNode>();
        CollectOwnedNodes(tree.Root, nodes);
        var builder = new StringBuilder();
        var first = true;
        foreach (var node in nodes)
        {
            if (!first)
            {
                builder.Append(';');
            }

            first = false;
            builder.Append(node.Kind.ToString()).Append(':');
            builder.Append(analysis.IsAtomicByAncestor(node) ? '1' : '0');
            builder.Append(analysis.MayContainCapture(node) ? '1' : '0');
            builder.Append(analysis.MayBacktrack(node) ? '1' : '0');
            builder.Append(analysis.IsInLoop(node) ? '1' : '0');
        }

        return builder.ToString();
    }
}
