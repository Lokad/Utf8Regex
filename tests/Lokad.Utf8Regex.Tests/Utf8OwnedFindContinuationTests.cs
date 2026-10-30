using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedFindContinuationTests
{
    private static Utf8OwnedRegexTree Wrapped(string pattern, RegexOptions options, Utf8TreeNodeKind kind, int depth)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        var body = tree.Root.Child(0);
        for (var level = 0; level < depth; level++)
        {
            var wrapper = Utf8TreeNode.Create(kind, tree.Root.Options);
            wrapper.M = 1;
            wrapper.N = -1;
            wrapper.Children = body;
            body.Parent = wrapper;
            body = wrapper;
        }
        tree.Root.Children = body;
        body.Parent = tree.Root;
        return tree;
    }

    public static IEnumerable<object[]> DeepFactsCases()
    {
        foreach (var pattern in new[] { @"\Aab[cd]ef\z", "ab|cd", @"ab\d|cd|ef", @"^ab|cd\d|ef",
            "(?:ab){2,3}cd", @"(?<x>ab)\k<x>cd", "ab(?=c)d", "K[ab]CD" })
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.RightToLeft })
        foreach (var kind in new[] { Utf8TreeNodeKind.Capture, Utf8TreeNodeKind.Group, Utf8TreeNodeKind.Atomic })
        foreach (var depth in new[] { 65, 16385 })
            yield return [pattern, options, (int)kind, depth];
    }

    [Theory]
    [MemberData(nameof(DeepFactsCases))]
    public void EntireFindFactsPipelineKeepsWrapperSemantics(string pattern, RegexOptions options, int wrapperKind, int depth)
    {
        var kind = (Utf8TreeNodeKind)wrapperKind;
        var shallow = Wrapped(pattern, options, kind, 1);
        var deep = Wrapped(pattern, options, kind, depth);
        // Existing pinned tests independently define shallow semantics. Extra
        // identical transparent wrappers must preserve the whole facts object;
        // one atomic wrapper also preserves its established literal exclusions.
        var expected = Utf8OwnedFindFacts.FromTree(shallow);
        var actual = Utf8OwnedFindFacts.FromTree(deep);
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.MinRequiredLength, actual.MinRequiredLength);
        Assert.Equal(expected.MaxPossibleLength, actual.MaxPossibleLength);
        Assert.Equal(expected.IsFixedLength, actual.IsFixedLength);
        Assert.Equal(expected.TrailingAnchor, actual.TrailingAnchor);
        Assert.Equal(expected.LeadingLiteral, actual.LeadingLiteral);
        Assert.Equal(expected.LongestLiteral, actual.LongestLiteral);
        Assert.Equal(expected.AlternatePrefixes, actual.AlternatePrefixes);
        Assert.Equal(expected.FixedDistanceLiteral, actual.FixedDistanceLiteral);
        Assert.Equal(Sets(expected.FixedDistanceSets), Sets(actual.FixedDistanceSets));
    }

    private static string[]? Sets(Utf8OwnedFindOptimizations.OwnedSet[]? sets)
        => sets?.Select(static set => string.Join("|", set.Distance, set.CharClass.ActualLowMask,
            set.CharClass.ActualHighMask)).ToArray();

    [Theory]
    [InlineData(65)]
    [InlineData(4097)]
    public void NestedConcatenationKeepsWidthsPrefixAndLastLiteralTie(int depth)
    {
        Utf8TreeNode body = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'a');
        for (var level = 0; level < depth; level++)
        {
            var suffix = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'b');
            var concat = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, RegexOptions.CultureInvariant);
            concat.Children = new Utf8TreeNode[] { body, suffix };
            body.Parent = concat;
            suffix.Parent = concat;
            body = concat;
        }
        Assert.Equal(depth + 1, Utf8OwnedFindOptimizations.ComputeByteWidth(body).Minimum);
        Assert.Equal(depth + 1, Utf8OwnedFindOptimizations.ComputeByteWidth(body).Maximum);
        Assert.Null(Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(body));
        Assert.Equal("b", Utf8OwnedFindOptimizations.TryExtractLongestLiteral(body));
        Assert.Equal(new[] { "a" + new string('b', depth) },
            Utf8OwnedFindOptimizations.FindAlternatePrefixes(body, RegexOptions.CultureInvariant)?.Select(Encoding.UTF8.GetString).ToArray());
        Assert.Equal(((string Literal, int Distance)?)("b", depth),
            Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(body));
        Assert.Null(Utf8OwnedFindOptimizations.FindFixedDistanceSets(body));
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepBoundedRepeatsKeepSaturatingWidthsAndCoveringPrefixes(int depth)
    {
        Utf8TreeNode body = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "ab");
        for (var level = 0; level < depth; level++)
        {
            var loop = Utf8TreeNode.Create(Utf8TreeNodeKind.Loop, RegexOptions.CultureInvariant, 2, 2);
            loop.Children = body;
            body.Parent = loop;
            body = loop;
        }
        Assert.Equal(int.MaxValue, Utf8OwnedFindOptimizations.ComputeByteWidth(body).Minimum);
        Assert.Null(Utf8OwnedFindOptimizations.ComputeByteWidth(body).Maximum);
        Assert.Null(Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(body));
        Assert.Null(Utf8OwnedFindOptimizations.TryExtractLongestLiteral(body));
        Assert.Equal(new[] { "abababab" }, Utf8OwnedFindOptimizations.FindAlternatePrefixes(body, RegexOptions.CultureInvariant)?.Select(Encoding.UTF8.GetString).ToArray());
        Assert.Null(Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(body));
        Assert.Null(Utf8OwnedFindOptimizations.FindFixedDistanceSets(body));
    }

    [Theory]
    [InlineData(@"ab|cd\d|ef", "ab")]
    [InlineData(@"^ab|cd\d|ef", "ab")]
    [InlineData(@"a|^bc\d|de", "a")]
    public void StoppedAlternativesKeepAllBranchPrefixes(string pattern, string prefix)
    {
        var tree = Wrapped(pattern, RegexOptions.CultureInvariant, Utf8TreeNodeKind.Group, 16385);
        var expected = pattern.StartsWith("a|", StringComparison.Ordinal)
            ? new[] { "a", "bc", "de" } : new[] { "ab", "cd", "ef" };
        Assert.Equal(prefix, expected[0]);
        Assert.Equal(expected, Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, tree.Options)?.Select(Encoding.UTF8.GetString).ToArray());
    }

    [Fact]
    public void StoppedFirstAlternativeKeepsItsPrefixAndLaterBranches()
    {
        var tree = Wrapped(@"^ab\d|cd", RegexOptions.CultureInvariant, Utf8TreeNodeKind.Group, 16385);
        var prefixes = Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, tree.Options)?.Select(Encoding.UTF8.GetString).ToArray();
        Assert.NotNull(prefixes);
        Assert.Equal(new[] { "ab", "cd" }, prefixes);
    }
}
