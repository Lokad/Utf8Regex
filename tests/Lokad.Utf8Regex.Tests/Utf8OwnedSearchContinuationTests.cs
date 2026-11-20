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
        return new Utf8SemanticRegex(pattern, options, pattern, options, tree);
    }

    private static string Snapshot(Utf8SearchFacts facts) => JsonSerializer.Serialize(new
    {
        facts.Kind, facts.LiteralUtf8, facts.AlternateLiteralsUtf8, facts.CanGuideFallbackStarts,
        facts.RequiredPrefilterLiteralUtf8, facts.RequiredPrefilterAlternateLiteralsUtf8,
        Sets = facts.FixedDistanceSets.Select(static set => new { set.Distance, Bytes = set.ByteSet.GetMatchBytes() }),
        facts.TrailingLiteralUtf8,
        facts.FallbackStartTransform, facts.Distance, facts.MinRequiredLength, facts.ExactRequiredLength,
        facts.MaxPossibleLength, facts.LeadingBoundary, facts.TrailingBoundary,
    });

    private static Utf8TreeNode Parent(Utf8TreeNodeKind kind, params Utf8TreeNode[] children)
    {
        var node = Utf8TreeNode.Create(kind, RegexOptions.CultureInvariant, 1, 1);
        node.Children = children.Length == 1 ? children[0] : children;
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
    [InlineData(65)]
    [InlineData(16385)]
    public void PrefixSetsKeepPartialInnerSequenceAndResumeOuterSibling(int depth)
    {
        var root = Parent(Utf8TreeNodeKind.Concatenate,
            Parent(Utf8TreeNodeKind.Concatenate, Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "ab"),
                Utf8TreeNode.Create(Utf8TreeNodeKind.PositiveLookaround, RegexOptions.CultureInvariant)),
            Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'c'));
        for (var index = 0; index < depth; index++) root = Parent(Utf8TreeNodeKind.Concatenate, root);
        object?[] args = [root, 0, null];
        Assert.True(Assert.IsType<bool>(Call("TryExtractDeterministicPrefixSets", args)));
        var sets = Assert.IsType<Utf8FixedDistanceSet[]>(args[2]);
        Assert.Equal(3, sets[^1].Distance + 1);
        Assert.Equal(new[] { 0, 1, 2 }, sets.Select(static set => set.Distance));
        Assert.Equal(new[] { "a", "b", "c" }, sets.Select(static set => System.Text.Encoding.ASCII.GetString(set.ByteSet.GetMatchBytes())));
        // A known assertion consumes zero bytes. An unknown inner atom has no
        // fixed width and cannot permit the same continuation at any depth.
        var opaque = Parent(Utf8TreeNodeKind.Concatenate,
            Parent(Utf8TreeNodeKind.Concatenate, Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, "ab"),
                Utf8TreeNode.Create(Utf8TreeNodeKind.Unknown, RegexOptions.CultureInvariant)),
            Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'c'));
        for (var index = 0; index < depth; index++) opaque = Parent(Utf8TreeNodeKind.Concatenate, opaque);
        args = [opaque, 0, null];
        Assert.True(Assert.IsType<bool>(Call("TryExtractDeterministicPrefixSets", args)));
        sets = Assert.IsType<Utf8FixedDistanceSet[]>(args[2]);
        Assert.Equal(new[] { 0, 1 }, sets.Select(static set => set.Distance));
        Assert.Equal(new[] { "a", "b" }, sets.Select(static set => System.Text.Encoding.ASCII.GetString(set.ByteSet.GetMatchBytes())));
    }
}
