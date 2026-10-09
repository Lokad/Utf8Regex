using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Retargeted grammar catalog: kind-order pins stay independent of production
// traversal. Feature pins describe the live census rather than BCL runtime facts.
public sealed class Utf8OwnedFeatureTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["", 512, "Capture,Concatenate", "000000000"];
        yield return ["abc", 512, "Capture,Multi", "000000000"];
        yield return ["a|b", 512, "Capture,Alternate,One,One", "000001000"];
        yield return ["(a|b)", 512, "Capture,Capture,Alternate,One,One", "000001000"];
        yield return ["(a|b|c)", 512, "Capture,Capture,Alternate,One,One,One", "000001000"];
        yield return ["a(b|c)d", 512, "Capture,Concatenate,One,Capture,Alternate,One,One,One", "000001000"];
        yield return ["(ab|cd)(ef|gh)", 512, "Capture,Concatenate,Capture,Alternate,Multi,Multi,Capture,Alternate,Multi,Multi", "000001000"];
        yield return ["(a)", 512, "Capture,Capture,One", "000000000"];
        yield return ["(a)(b)", 512, "Capture,Concatenate,Capture,One,Capture,One", "000000000"];
        yield return ["((a))", 512, "Capture,Capture,Capture,One", "000000000"];
        yield return ["(?<n>a)", 512, "Capture,Capture,One", "000000000"];
        yield return ["(?'n'a)", 512, "Capture,Capture,One", "000000000"];
        yield return ["(?<a>x)(?<b>y)", 512, "Capture,Concatenate,Capture,One,Capture,One", "000000000"];
        yield return ["(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)", 512, "Capture,Concatenate,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One", "000000000"];
        yield return ["(a)(?(1)b|c)", 512, "Capture,Concatenate,Capture,One,BackreferenceConditional,One,One", "000100000"];
        yield return ["(?<a>x)(?(a)b|c)", 512, "Capture,Concatenate,Capture,One,BackreferenceConditional,One,One", "000100000"];
        yield return ["(a)(?(1)b|)", 512, "Capture,Concatenate,Capture,One,BackreferenceConditional,One,Concatenate", "000100000"];
        yield return ["a*", 512, "Capture,Oneloop", "000010000"];
        yield return ["a+", 512, "Capture,Oneloop", "000010000"];
        yield return ["a?", 512, "Capture,Oneloop", "000010000"];
        yield return ["a{2}", 512, "Capture,Oneloop", "000010000"];
        yield return ["a{2,}", 512, "Capture,Oneloop", "000010000"];
        yield return ["a{2,4}", 512, "Capture,Oneloop", "000010000"];
        yield return ["a*?", 512, "Capture,Onelazy", "000010000"];
        yield return ["a{0}", 512, "Capture,Empty", "000000000"];
        yield return ["a{0,1}", 512, "Capture,Oneloop", "000010000"];
        yield return ["(ab)*", 512, "Capture,Loop,Capture,Multi", "000010000"];
        yield return ["(ab)+", 512, "Capture,Loop,Capture,Multi", "000010000"];
        yield return ["(a|b)*", 512, "Capture,Loop,Capture,Alternate,One,One", "000011000"];
        yield return ["(a|b)+", 512, "Capture,Loop,Capture,Alternate,One,One", "000011000"];
        yield return ["(a*)*", 512, "Capture,Loop,Capture,Oneloop", "000010000"];
        yield return ["(a+)+", 512, "Capture,Loop,Capture,Oneloop", "000010000"];
        yield return ["((a)*)*", 512, "Capture,Loop,Capture,Loop,Capture,One", "000010000"];
        yield return ["(a*)+?", 512, "Capture,Lazyloop,Capture,Oneloop", "000010000"];
        yield return ["(?>a)", 512, "Capture,Atomic,One", "001000000"];
        yield return ["(?>a|b)", 512, "Capture,Atomic,Alternate,One,One", "001001000"];
        yield return ["(?>a+)", 512, "Capture,Atomic,Oneloop", "001010000"];
        yield return ["(?>a)+", 512, "Capture,Loop,Atomic,One", "001010000"];
        yield return ["(?=a)", 512, "Capture,PositiveLookaround,One", "010000000"];
        yield return ["(?!a)", 512, "Capture,NegativeLookaround,One", "010000000"];
        yield return ["(?<=a)", 512, "Capture,PositiveLookaround,One", "010000001"];
        yield return ["(?<!a)", 512, "Capture,NegativeLookaround,One", "010000001"];
        yield return ["(?=(a))", 512, "Capture,PositiveLookaround,Capture,One", "010000000"];
        yield return ["(?!(a|b))", 512, "Capture,NegativeLookaround,Capture,Alternate,One,One", "010001000"];
        yield return ["(?<=a+)b", 512, "Capture,Concatenate,PositiveLookaround,Oneloop,One", "010010001"];
        yield return ["(?>(a))(b)", 512, "Capture,Concatenate,Atomic,Capture,One,Capture,One", "001000000"];
        yield return ["(?=a+)a", 512, "Capture,Concatenate,PositiveLookaround,Oneloop,One", "010010000"];
        yield return ["(?!a+)a", 512, "Capture,Concatenate,NegativeLookaround,Oneloop,One", "010010000"];
        yield return ["(a)\\1", 512, "Capture,Concatenate,Capture,One,Backreference", "100000000"];
        yield return ["(a)(b)\\2", 512, "Capture,Concatenate,Capture,One,Capture,One,Backreference", "100000000"];
        yield return ["(?<n>a)\\k<n>", 512, "Capture,Concatenate,Capture,One,Backreference", "100000000"];
        yield return ["^a", 512, "Capture,Concatenate,Beginning,One", "000000100"];
        yield return ["a$", 512, "Capture,Concatenate,One,EndZ", "000000000"];
        yield return ["\\Aa", 512, "Capture,Concatenate,Beginning,One", "000000100"];
        yield return ["a\\z", 512, "Capture,Concatenate,One,End", "000000000"];
        yield return ["a\\Z", 512, "Capture,Concatenate,One,EndZ", "000000000"];
        yield return ["\\Ga", 512, "Capture,Concatenate,Start,One", "000000000"];
        yield return ["\\ba\\b", 512, "Capture,Concatenate,Boundary,One,Boundary", "000000010"];
        yield return ["\\B", 512, "Capture,NonBoundary", "000000010"];
        yield return ["[ab]", 512, "Capture,Set", "000000000"];
        yield return ["[^ab]", 512, "Capture,Set", "000000000"];
        yield return [".", 512, "Capture,Set", "000000000"];
        yield return ["\\d", 512, "Capture,Set", "000000000"];
        yield return ["\\w", 512, "Capture,Set", "000000000"];
        yield return ["\\s", 512, "Capture,Set", "000000000"];
        yield return ["\\D", 512, "Capture,Set", "000000000"];
        yield return ["\\p{L}", 512, "Capture,Set", "000000000"];
        yield return ["\\P{L}", 512, "Capture,Set", "000000000"];
        yield return ["(?i:ab)", 512, "Capture,Group,Concatenate,Set,Set", "000000000"];
        yield return ["(?m:^a)", 512, "Capture,Group,Concatenate,Bol,One", "000000100"];
        yield return ["(?s:a.b)", 512, "Capture,Group,Concatenate,One,Set,One", "000000000"];
        yield return ["(?i:(a))", 512, "Capture,Group,Capture,Set", "000000000"];
        yield return ["a(?i:b)c", 512, "Capture,Concatenate,One,Group,Set,One", "000000000"];
        yield return ["ab", 513, "Capture,Concatenate,Set,Set", "000000000"];
        yield return ["ab", 576, "Capture,Multi", "000000000"];
        yield return ["ab", 514, "Capture,Multi", "000000000"];
        yield return ["ab", 528, "Capture,Multi", "000000000"];
        yield return ["(a)b", 513, "Capture,Concatenate,Capture,Set,Set", "000000000"];
        yield return ["(?<=a)b", 576, "Capture,Concatenate,PositiveLookaround,One,One", "010000001"];
        yield return ["(?-i:a)b", 513, "Capture,Concatenate,Group,One,Set", "000000000"];
        yield return ["a|b|c|d|e", 512, "Capture,Alternate,One,One,One,One,One", "000001000"];
        yield return ["(a(b(c)))", 512, "Capture,Capture,Concatenate,One,Capture,Concatenate,One,Capture,One", "000000000"];
        yield return ["(a(b))(c(d))", 512, "Capture,Concatenate,Capture,Concatenate,One,Capture,One,Capture,Concatenate,One,Capture,One", "000000000"];
        yield return ["(?:a)", 512, "Capture,Group,One", "000000000"];
        yield return ["(?:a|b)*c", 512, "Capture,Concatenate,Loop,Group,Alternate,One,One,One", "000011000"];
        yield return ["\\A(?:a|b)+$", 512, "Capture,Concatenate,Beginning,Loop,Group,Alternate,One,One,EndZ", "000011100"];
        yield return ["(a|ab|abc)", 512, "Capture,Capture,Alternate,One,Multi,Multi", "000001000"];
        yield return ["(abc|ab|a)", 512, "Capture,Capture,Alternate,Multi,Multi,One", "000001000"];
        yield return ["(a|b)(c|d)(e|f)", 512, "Capture,Concatenate,Capture,Alternate,One,One,Capture,Alternate,One,One,Capture,Alternate,One,One", "000001000"];
        yield return ["(ab)*cd", 512, "Capture,Concatenate,Loop,Capture,Multi,Multi", "000010000"];
        yield return ["ab(cd)*", 512, "Capture,Concatenate,Multi,Loop,Capture,Multi", "000010000"];
        yield return ["(ab|cd)+ef", 512, "Capture,Concatenate,Loop,Capture,Alternate,Multi,Multi,Multi", "000011000"];
        yield return ["(ab|cd)+?ef", 512, "Capture,Concatenate,Lazyloop,Capture,Alternate,Multi,Multi,Multi", "000011000"];
        yield return ["((((((a))))))", 512, "Capture,Capture,Capture,Capture,Capture,Capture,Capture,One", "000000000"];
        yield return ["(a|b|c|d|e|f|g)+", 512, "Capture,Loop,Capture,Alternate,One,One,One,One,One,One,One", "000011000"];
        yield return ["([ab]+|[cd]+)*", 512, "Capture,Loop,Capture,Alternate,Setloop,Setloop", "000011000"];
        yield return ["[ab]+", 512, "Capture,Setloop", "000010000"];
        yield return ["\\d+", 512, "Capture,Setloop", "000010000"];
        yield return ["a+?", 512, "Capture,Onelazy", "000010000"];
        yield return ["(a)(b|c)(d)", 512, "Capture,Concatenate,Capture,One,Capture,Alternate,One,One,Capture,One", "000001000"];
        yield return ["(?=(a)(b))c", 512, "Capture,Concatenate,PositiveLookaround,Concatenate,Capture,One,Capture,One,One", "010000000"];
        yield return ["(a)(?!(b))c", 512, "Capture,Concatenate,Capture,One,NegativeLookaround,Capture,One,One", "010000000"];
        yield return ["((a)|(b)|(c))d", 512, "Capture,Concatenate,Capture,Alternate,Capture,One,Capture,One,Capture,One,One", "000001000"];
        yield return ["a{3}b{2}c", 512, "Capture,Concatenate,Oneloop,Oneloop,One", "000010000"];
        yield return ["(a{2}b{3})+", 512, "Capture,Loop,Capture,Concatenate,Oneloop,Oneloop", "000010000"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void FeatureCensusKeepsPinnedGrammarAndCaptureMetadata(string pattern, int options, string expectedKinds, string expectedFeatures)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(tree);
        var kinds = new List<string>();
        var pending = new Stack<Utf8TreeNode>();
        pending.Push(tree.Root);
        while (pending.TryPop(out var node))
        {
            kinds.Add(node.Kind.ToString());
            for (var index = node.ChildCount - 1; index >= 0; index--)
            {
                Assert.Same(node, node.Child(index).Parent);
                pending.Push(node.Child(index));
            }
        }
        Assert.Equal(expectedKinds, string.Join(',', kinds));
        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(new(pattern, (RegexOptions)options, pattern, (RegexOptions)options, tree));
        var actual = new[] { features.HasBackreferences, features.HasLookarounds, features.HasAtomicGroups,
            features.HasConditionals, features.HasLoops, features.HasAlternation, features.HasAbsoluteStartAnchor,
            features.HasWordBoundary, features.HasLookbehind };
        Assert.Equal(expectedFeatures, string.Concat(actual.Select(static flag => flag ? '1' : '0')));
        // Capture declarations overlap the comparator's grammar; no subject or
        // code-unit matching is involved in this metadata check.
        var oracle = new Regex(pattern, (RegexOptions)options & ~RegexOptions.Compiled);
        Assert.Equal(oracle.GetGroupNumbers().Length, features.CaptureCount);
        Assert.Equal(oracle.GetGroupNames().Any(static name => !int.TryParse(name, out _)), features.HasNamedCaptures);
    }

    [Theory]
    [InlineData((int)Utf8TreeNodeKind.Loop)] [InlineData((int)Utf8TreeNodeKind.Lazyloop)]
    [InlineData((int)Utf8TreeNodeKind.Oneloop)] [InlineData((int)Utf8TreeNodeKind.Notoneloop)]
    [InlineData((int)Utf8TreeNodeKind.Setloop)] [InlineData((int)Utf8TreeNodeKind.Onelazy)]
    [InlineData((int)Utf8TreeNodeKind.Notonelazy)] [InlineData((int)Utf8TreeNodeKind.Setlazy)]
    [InlineData((int)Utf8TreeNodeKind.Oneloopatomic)] [InlineData((int)Utf8TreeNodeKind.Notoneloopatomic)]
    [InlineData((int)Utf8TreeNodeKind.Setloopatomic)]
    public void LoopKindsNeedNoAncestorFactSet(int kind)
    {
        var root = Utf8TreeNode.Create((Utf8TreeNodeKind)kind, RegexOptions.None);
        var tree = Utf8OwnedRegexTree.Create(root, 1, null, null, null, RegexOptions.None, null, null, false, false);
        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(new("", RegexOptions.None, "", RegexOptions.None, tree));
        Assert.True(features.HasLoops);
    }

    [Fact]
    public void ObsoleteTreeFactTypesStateAndPredicateStayAbsent()
    {
        var assembly = typeof(Utf8Regex).Assembly;
        Assert.Null(assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8OwnedTreeAnalyzer"));
        Assert.Null(assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8OwnedTreeAnalysis"));
        Assert.Null(typeof(Utf8SemanticRegex).GetProperty("RuntimeAnalysis"));
        Assert.Null(typeof(Utf8TreeNode).GetProperty("IsBacktrackingConstruct"));
    }
}
