using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// The grammar catalog pins parsed node order and capture metadata.
public sealed class Utf8OwnedFeatureTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["", 512, "Capture,Concatenate"];
        yield return ["abc", 512, "Capture,Multi"];
        yield return ["a|b", 512, "Capture,Alternate,One,One"];
        yield return ["(a|b)", 512, "Capture,Capture,Alternate,One,One"];
        yield return ["(a|b|c)", 512, "Capture,Capture,Alternate,One,One,One"];
        yield return ["a(b|c)d", 512, "Capture,Concatenate,One,Capture,Alternate,One,One,One"];
        yield return ["(ab|cd)(ef|gh)", 512, "Capture,Concatenate,Capture,Alternate,Multi,Multi,Capture,Alternate,Multi,Multi"];
        yield return ["(a)", 512, "Capture,Capture,One"];
        yield return ["(a)(b)", 512, "Capture,Concatenate,Capture,One,Capture,One"];
        yield return ["((a))", 512, "Capture,Capture,Capture,One"];
        yield return ["(?<n>a)", 512, "Capture,Capture,One"];
        yield return ["(?'n'a)", 512, "Capture,Capture,One"];
        yield return ["(?<a>x)(?<b>y)", 512, "Capture,Concatenate,Capture,One,Capture,One"];
        yield return ["(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)", 512, "Capture,Concatenate,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One,Capture,One"];
        yield return ["(a)(?(1)b|c)", 512, "Capture,Concatenate,Capture,One,BackreferenceConditional,One,One"];
        yield return ["(?<a>x)(?(a)b|c)", 512, "Capture,Concatenate,Capture,One,BackreferenceConditional,One,One"];
        yield return ["(a)(?(1)b|)", 512, "Capture,Concatenate,Capture,One,BackreferenceConditional,One,Concatenate"];
        yield return ["a*", 512, "Capture,Oneloop"];
        yield return ["a+", 512, "Capture,Oneloop"];
        yield return ["a?", 512, "Capture,Oneloop"];
        yield return ["a{2}", 512, "Capture,Oneloop"];
        yield return ["a{2,}", 512, "Capture,Oneloop"];
        yield return ["a{2,4}", 512, "Capture,Oneloop"];
        yield return ["a*?", 512, "Capture,Onelazy"];
        yield return ["a{0}", 512, "Capture,Empty"];
        yield return ["a{0,1}", 512, "Capture,Oneloop"];
        yield return ["(ab)*", 512, "Capture,Loop,Capture,Multi"];
        yield return ["(ab)+", 512, "Capture,Loop,Capture,Multi"];
        yield return ["(a|b)*", 512, "Capture,Loop,Capture,Alternate,One,One"];
        yield return ["(a|b)+", 512, "Capture,Loop,Capture,Alternate,One,One"];
        yield return ["(a*)*", 512, "Capture,Loop,Capture,Oneloop"];
        yield return ["(a+)+", 512, "Capture,Loop,Capture,Oneloop"];
        yield return ["((a)*)*", 512, "Capture,Loop,Capture,Loop,Capture,One"];
        yield return ["(a*)+?", 512, "Capture,Lazyloop,Capture,Oneloop"];
        yield return ["(?>a)", 512, "Capture,Atomic,One"];
        yield return ["(?>a|b)", 512, "Capture,Atomic,Alternate,One,One"];
        yield return ["(?>a+)", 512, "Capture,Atomic,Oneloop"];
        yield return ["(?>a)+", 512, "Capture,Loop,Atomic,One"];
        yield return ["(?=a)", 512, "Capture,PositiveLookaround,One"];
        yield return ["(?!a)", 512, "Capture,NegativeLookaround,One"];
        yield return ["(?<=a)", 512, "Capture,PositiveLookaround,One"];
        yield return ["(?<!a)", 512, "Capture,NegativeLookaround,One"];
        yield return ["(?=(a))", 512, "Capture,PositiveLookaround,Capture,One"];
        yield return ["(?!(a|b))", 512, "Capture,NegativeLookaround,Capture,Alternate,One,One"];
        yield return ["(?<=a+)b", 512, "Capture,Concatenate,PositiveLookaround,Oneloop,One"];
        yield return ["(?>(a))(b)", 512, "Capture,Concatenate,Atomic,Capture,One,Capture,One"];
        yield return ["(?=a+)a", 512, "Capture,Concatenate,PositiveLookaround,Oneloop,One"];
        yield return ["(?!a+)a", 512, "Capture,Concatenate,NegativeLookaround,Oneloop,One"];
        yield return ["(a)\\1", 512, "Capture,Concatenate,Capture,One,Backreference"];
        yield return ["(a)(b)\\2", 512, "Capture,Concatenate,Capture,One,Capture,One,Backreference"];
        yield return ["(?<n>a)\\k<n>", 512, "Capture,Concatenate,Capture,One,Backreference"];
        yield return ["^a", 512, "Capture,Concatenate,Beginning,One"];
        yield return ["a$", 512, "Capture,Concatenate,One,EndZ"];
        yield return ["\\Aa", 512, "Capture,Concatenate,Beginning,One"];
        yield return ["a\\z", 512, "Capture,Concatenate,One,End"];
        yield return ["a\\Z", 512, "Capture,Concatenate,One,EndZ"];
        yield return ["\\Ga", 512, "Capture,Concatenate,Start,One"];
        yield return ["\\ba\\b", 512, "Capture,Concatenate,Boundary,One,Boundary"];
        yield return ["\\B", 512, "Capture,NonBoundary"];
        yield return ["[ab]", 512, "Capture,Set"];
        yield return ["[^ab]", 512, "Capture,Set"];
        yield return [".", 512, "Capture,Set"];
        yield return ["\\d", 512, "Capture,Set"];
        yield return ["\\w", 512, "Capture,Set"];
        yield return ["\\s", 512, "Capture,Set"];
        yield return ["\\D", 512, "Capture,Set"];
        yield return ["\\p{L}", 512, "Capture,Set"];
        yield return ["\\P{L}", 512, "Capture,Set"];
        yield return ["(?i:ab)", 512, "Capture,Group,Concatenate,Set,Set"];
        yield return ["(?m:^a)", 512, "Capture,Group,Concatenate,Bol,One"];
        yield return ["(?s:a.b)", 512, "Capture,Group,Concatenate,One,Set,One"];
        yield return ["(?i:(a))", 512, "Capture,Group,Capture,Set"];
        yield return ["a(?i:b)c", 512, "Capture,Concatenate,One,Group,Set,One"];
        yield return ["ab", 513, "Capture,Concatenate,Set,Set"];
        yield return ["ab", 576, "Capture,Multi"];
        yield return ["ab", 514, "Capture,Multi"];
        yield return ["ab", 528, "Capture,Multi"];
        yield return ["(a)b", 513, "Capture,Concatenate,Capture,Set,Set"];
        yield return ["(?<=a)b", 576, "Capture,Concatenate,PositiveLookaround,One,One"];
        yield return ["(?-i:a)b", 513, "Capture,Concatenate,Group,One,Set"];
        yield return ["a|b|c|d|e", 512, "Capture,Alternate,One,One,One,One,One"];
        yield return ["(a(b(c)))", 512, "Capture,Capture,Concatenate,One,Capture,Concatenate,One,Capture,One"];
        yield return ["(a(b))(c(d))", 512, "Capture,Concatenate,Capture,Concatenate,One,Capture,One,Capture,Concatenate,One,Capture,One"];
        yield return ["(?:a)", 512, "Capture,Group,One"];
        yield return ["(?:a|b)*c", 512, "Capture,Concatenate,Loop,Group,Alternate,One,One,One"];
        yield return ["\\A(?:a|b)+$", 512, "Capture,Concatenate,Beginning,Loop,Group,Alternate,One,One,EndZ"];
        yield return ["(a|ab|abc)", 512, "Capture,Capture,Alternate,One,Multi,Multi"];
        yield return ["(abc|ab|a)", 512, "Capture,Capture,Alternate,Multi,Multi,One"];
        yield return ["(a|b)(c|d)(e|f)", 512, "Capture,Concatenate,Capture,Alternate,One,One,Capture,Alternate,One,One,Capture,Alternate,One,One"];
        yield return ["(ab)*cd", 512, "Capture,Concatenate,Loop,Capture,Multi,Multi"];
        yield return ["ab(cd)*", 512, "Capture,Concatenate,Multi,Loop,Capture,Multi"];
        yield return ["(ab|cd)+ef", 512, "Capture,Concatenate,Loop,Capture,Alternate,Multi,Multi,Multi"];
        yield return ["(ab|cd)+?ef", 512, "Capture,Concatenate,Lazyloop,Capture,Alternate,Multi,Multi,Multi"];
        yield return ["((((((a))))))", 512, "Capture,Capture,Capture,Capture,Capture,Capture,Capture,One"];
        yield return ["(a|b|c|d|e|f|g)+", 512, "Capture,Loop,Capture,Alternate,One,One,One,One,One,One,One"];
        yield return ["([ab]+|[cd]+)*", 512, "Capture,Loop,Capture,Alternate,Setloop,Setloop"];
        yield return ["[ab]+", 512, "Capture,Setloop"];
        yield return ["\\d+", 512, "Capture,Setloop"];
        yield return ["a+?", 512, "Capture,Onelazy"];
        yield return ["(a)(b|c)(d)", 512, "Capture,Concatenate,Capture,One,Capture,Alternate,One,One,Capture,One"];
        yield return ["(?=(a)(b))c", 512, "Capture,Concatenate,PositiveLookaround,Concatenate,Capture,One,Capture,One,One"];
        yield return ["(a)(?!(b))c", 512, "Capture,Concatenate,Capture,One,NegativeLookaround,Capture,One,One"];
        yield return ["((a)|(b)|(c))d", 512, "Capture,Concatenate,Capture,Alternate,Capture,One,Capture,One,Capture,One,One"];
        yield return ["a{3}b{2}c", 512, "Capture,Concatenate,Oneloop,Oneloop,One"];
        yield return ["(a{2}b{3})+", 512, "Capture,Loop,Capture,Concatenate,Oneloop,Oneloop"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void KeepsPinnedGrammarAndCaptureMetadata(string pattern, int options, string expectedKinds)
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
        // Capture declarations overlap the comparator's grammar; no subject or
        // code-unit matching is involved in this metadata check.
        var oracle = new Regex(pattern, (RegexOptions)options & ~RegexOptions.Compiled);
        Assert.Equal(oracle.GetGroupNumbers().Length, tree.CaptureCount);
        Assert.Equal(oracle.GetGroupNames().Any(static name => !int.TryParse(name, out _)), tree.CaptureNameToNumberMapping is { Count: > 0 });
    }

    [Theory]
    [InlineData(@"^a", true, false)]
    [InlineData(@"(?m:^a)", true, false)]
    [InlineData(@"((a))$", false, true)]
    [InlineData(@"(?<x>a){0}b", false, false)]
    [InlineData(@"(?:^){0}b", false, false)]
    [InlineData(@"^(?<x>a)", true, true)]
    public void PublishesOnlyLiveAnchorsAndCaptureNodes(string pattern, bool hasStartAnchor, bool hasCapture)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        Assert.Equal(new Utf8RegexFeatures(hasStartAnchor, hasCapture), tree.Features);
    }
}
