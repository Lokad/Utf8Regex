using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedSearchContinuationTests
{
    public static IEnumerable<object[]> Cases()
    {
        var patterns = new[] { "(?:cat|dog)\\r?\\n\"[A-F0-9]{8}\"", @"\b(?:KEY|LOCK)\b[^\r\n]{1,8}tail",
            @"foo\s+bar[^\n]{2,5}end", @"^\s*\b(?:cat|dog)\b$", "ab[cd]ef", "(?:ab(?=c)|cd(?=e))f",
            "café.{0,4}fin", @"a\r\nb" };
        for (var index = 0; index < patterns.Length; index++)
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.RightToLeft })
        foreach (var depth in new[] { 65, 16385 })
            yield return [patterns[index], options, index % 3, depth];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CompleteSearchAnalysisRetainsAllFactsAcrossDeepWrappers(string pattern, RegexOptions options, int wrapperKind, int depth)
    {
        var kind = wrapperKind switch { 0 => Utf8TreeNodeKind.Capture, 1 => Utf8TreeNodeKind.Group, _ => Utf8TreeNodeKind.Atomic };
        var shallow = Semantic(pattern, options, kind, 1);
        var deep = Semantic(pattern, options, kind, depth);
        // Existing independent shallow fact pins define selection semantics.
        // Extra wrappers exercise the complete analyzer and required families.
        Assert.Equal(Snapshot(Utf8FrontEndSearchAnalyzer.Analyze(shallow)), Snapshot(Utf8FrontEndSearchAnalyzer.Analyze(deep)));
        Assert.Equal(Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(shallow.RuntimeTree!.Root),
            Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(deep.RuntimeTree!.Root));
    }

    private static Utf8SemanticRegex Semantic(string pattern, RegexOptions options, Utf8TreeNodeKind kind, int depth)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        var body = tree.Root.Child(0);
        for (var index = 0; index < depth; index++) body = Parent(kind, body);
        tree.Root.Children = body;
        body.Parent = tree.Root;
        return new Utf8SemanticRegex(pattern, options, pattern, options, tree, Utf8OwnedTreeAnalyzer.Analyze(tree));
    }

    private static string Snapshot(Utf8SearchFacts facts) => JsonSerializer.Serialize(new
    {
        facts.Kind, facts.LiteralUtf8, facts.AlternateLiteralsUtf8, facts.CanGuideFallbackStarts,
        facts.RequiredPrefilterLiteralUtf8, facts.RequiredPrefilterAlternateLiteralsUtf8,
        facts.SecondaryRequiredPrefilterQuotedAsciiSet, facts.SecondaryRequiredPrefilterQuotedAsciiLength,
        Sets = facts.FixedDistanceSets.Select(static set => new { set.Distance, Bytes = set.ByteSet.GetMatchBytes() }),
        facts.TrailingLiteralUtf8, facts.OrderedWindowLeadingLiteralsUtf8, facts.OrderedWindowTrailingLiteralUtf8,
        facts.RequiredWindowPrefilters, facts.OrderedWindowMaxGap, facts.OrderedWindowSameLine,
        facts.FallbackStartTransform, facts.Distance, facts.MinRequiredLength, facts.ExactRequiredLength,
        facts.MaxPossibleLength, facts.LeadingBoundary, facts.TrailingBoundary,
    });

    private static Utf8TreeNode Parent(Utf8TreeNodeKind kind, params Utf8TreeNode[] children)
    {
        var node = new Utf8TreeNode(kind, RegexOptions.CultureInvariant, 1, 1);
        node.Children = children.Length == 1 ? children[0] : new List<Utf8TreeNode>(children);
        foreach (var child in children) child.Parent = node;
        return node;
    }

    private static object? Call(string name, params object?[] args)
    {
        var method = typeof(Utf8FrontEndSearchAnalyzer).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method.Invoke(null, args);
    }

    [Theory]
    [InlineData(65, 0)]
    [InlineData(16385, 0)]
    [InlineData(65, 1)]
    [InlineData(16385, 1)]
    [InlineData(65, 2)]
    [InlineData(16385, 2)]
    [InlineData(65, 3)]
    [InlineData(16385, 3)]
    public void DeepWidthAndLineFactsUseSequenceSumBranchMaximumAndFiniteRepeat(int depth, int shape)
    {
        var node = new Utf8TreeNode(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "a\r\né");
        for (var index = 0; index < depth; index++)
            node = shape switch
            {
                0 => Parent(Utf8TreeNodeKind.Concatenate, node),
                1 => Parent(Utf8TreeNodeKind.Concatenate, node, new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'x')),
                2 => Parent(Utf8TreeNodeKind.Alternate, node, new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'x')),
                _ => Parent(Utf8TreeNodeKind.Loop, node),
            };
        object?[] width = [node, 0, false];
        Assert.True(Assert.IsType<bool>(Call("TryGetFiniteAsciiWindowWidth", width)));
        Assert.Equal(5 + (shape == 1 ? depth : 0), Assert.IsType<int>(width[1]));
        Assert.False(Assert.IsType<bool>(width[2]));
        object?[] lines = [node, 0];
        Assert.True(Assert.IsType<bool>(Call("TryGetFiniteLineBreakCount", lines)));
        Assert.Equal(2, Assert.IsType<int>(lines[1]));
    }

    [Fact]
    public void FailedFirstChildDoesNotEvaluateLaterOverflowingLineBranch()
    {
        var overflow = Parent(Utf8TreeNodeKind.Loop, new Utf8TreeNode(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "\n\n"));
        overflow.N = int.MaxValue - 1;
        var root = Parent(Utf8TreeNodeKind.Concatenate, new Utf8TreeNode(Utf8TreeNodeKind.Backreference, RegexOptions.CultureInvariant), overflow);
        object?[] lines = [root, 0];
        Assert.False(Assert.IsType<bool>(Call("TryGetFiniteLineBreakCount", lines)));
        Assert.Equal(0, Assert.IsType<int>(lines[1]));
        object?[] width = [overflow, 0, true];
        Assert.False(Assert.IsType<bool>(Call("TryGetFiniteAsciiWindowWidth", width)));
        Assert.Equal(0, Assert.IsType<int>(width[1]));
        Assert.False(Assert.IsType<bool>(width[2]));
        var thrown = Assert.Throws<TargetInvocationException>(() => Call("TryGetFiniteLineBreakCount", overflow, 0));
        Assert.IsType<OverflowException>(thrown.InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyNestedPhraseFailsWhileDirectAnchorsAreSkipped(bool nested)
    {
        var anchor = new Utf8TreeNode(Utf8TreeNodeKind.Bol, RegexOptions.CultureInvariant);
        var root = Parent(Utf8TreeNodeKind.Concatenate, new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'a'),
            nested ? Parent(Utf8TreeNodeKind.Concatenate, anchor) : anchor);
        object?[] args = [root, ""];
        Assert.Equal(!nested, Assert.IsType<bool>(Call("TryExtractAsciiWindowPhrase", args)));
        Assert.Equal(nested ? "" : "a", Assert.IsType<string>(args[1]));
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepPhraseAndTrailingLiteralRetainForwardAndReverseOrder(int depth)
    {
        var phrase = new Utf8TreeNode(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "ab");
        for (var index = 0; index < depth; index++)
            phrase = Parent(Utf8TreeNodeKind.Concatenate, phrase, new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'c'));
        object?[] args = [phrase, ""];
        Assert.True(Assert.IsType<bool>(Call("TryExtractAsciiWindowPhrase", args)));
        Assert.Equal("ab" + new string('c', depth), Assert.IsType<string>(args[1]));
        args = [phrase, ""];
        Assert.True(Assert.IsType<bool>(Call("TryExtractTrailingLiteral", args)));
        Assert.Equal("c", Assert.IsType<string>(args[1]));
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void PrefixSetsKeepPartialInnerSequenceAndResumeOuterSibling(int depth)
    {
        var root = Parent(Utf8TreeNodeKind.Concatenate,
            Parent(Utf8TreeNodeKind.Concatenate, new Utf8TreeNode(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "ab"),
                new Utf8TreeNode(Utf8TreeNodeKind.PositiveLookaround, RegexOptions.CultureInvariant)),
            new Utf8TreeNode(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'c'));
        for (var index = 0; index < depth; index++) root = Parent(Utf8TreeNodeKind.Concatenate, root);
        var sets = new List<Utf8FixedDistanceSet>();
        object?[] args = [root, sets, 0, 0];
        Assert.True(Assert.IsType<bool>(Call("TryAppendDeterministicPrefixSets", args)));
        Assert.Equal(3, Assert.IsType<int>(args[2]));
        Assert.Equal(new[] { 0, 1, 2 }, sets.Select(static set => set.Distance));
        Assert.Equal(new[] { "a", "b", "c" }, sets.Select(static set => System.Text.Encoding.ASCII.GetString(set.ByteSet.GetMatchBytes())));
    }
}
