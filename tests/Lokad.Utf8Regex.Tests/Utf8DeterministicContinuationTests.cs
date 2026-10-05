using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeterministicContinuationTests
{
    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, string text, params Utf8ExecutionNode[] children)
        => new(kind, RegexOptions.CultureInvariant, children, text, '\0', 0, 1, 1);

    private static object? Call(Type type, string name, params object?[] args)
    {
        var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method.Invoke(null, args);
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepDuplicateUnionsRetainLiteralFamilyAndByteMask(int depth)
    {
        var root = Node(Utf8ExecutionNodeKind.Multi, "cat");
        for (var index = 0; index < depth; index++)
            root = Node(Utf8ExecutionNodeKind.Alternate, "", root, Node(Utf8ExecutionNodeKind.Multi, "dog"));
        var tree = new Utf8ExecutionTree(root);
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None));
        var anchor = Utf8DeterministicAnchorSearch.Create(tree, plan);
        Assert.True(anchor.HasValue);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, anchor.Searcher.Kind);
        Assert.Equal(2, anchor.Searcher.FindFirst("zzdogcat"u8));
        Assert.True(Utf8DeterministicAnchorSearch.CanUseForByteSafeLinear(tree, anchor, plan.StructuralSearchPlan));
        object?[] args = [root, 0UL, 0UL];
        Assert.True(Assert.IsType<bool>(Call(typeof(Utf8DeterministicAnchorSearch), "TryGetLeadingAsciiByteMask", args)));
        Assert.Equal(0UL, Assert.IsType<ulong>(args[1]));
        Assert.Equal((1UL << ('c' - 64)) | (1UL << ('d' - 64)), Assert.IsType<ulong>(args[2]));
    }

    [Theory]
    [InlineData(65)]
    [InlineData(16385)]
    public void DeepSequencesPreservePartialPrefixAndOuterContinuation(int depth)
    {
        var root = Node(Utf8ExecutionNodeKind.Concatenate, "",
            Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Multi, "ab"), Node(Utf8ExecutionNodeKind.PositiveLookaround, "")),
            Node(Utf8ExecutionNodeKind.Multi, "c"));
        for (var index = 0; index < depth; index++) root = Node(Utf8ExecutionNodeKind.Concatenate, "", root);
        var tree = new Utf8ExecutionTree(root);
        var plan = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.FixedDistanceAsciiLiteral,
            new Utf8SearchFactData { LiteralUtf8 = "c"u8.ToArray(), Distance = 2 }));
        var anchor = Utf8DeterministicAnchorSearch.Create(tree, plan);
        Assert.True(anchor.HasValue);
        Assert.Equal(2, anchor.Offset);
        Assert.Equal(PreparedSearcherKind.ExactLiteral, anchor.Searcher.Kind);
        var guards = Utf8DeterministicVerifierGuards.Create(tree, plan);
        Assert.NotNull(guards.PrefixGuards);
        Assert.Equal(new[] { 0, 1, 2 }, guards.PrefixGuards.Select(static guard => guard.Offset));
        Assert.Equal(new byte?[] { (byte)'a', (byte)'b', (byte)'c' }, guards.PrefixGuards.Select(static guard => guard.Literal));
        Assert.True(guards.Matches("abc"u8, 0));
        Assert.False(guards.Matches("abx"u8, 0));
    }

    [Theory]
    [InlineData(65, false)]
    [InlineData(16385, false)]
    [InlineData(65, true)]
    [InlineData(16385, true)]
    public void DeepLoopByteMaskKeepsAsciiGateWhileWhitespaceUnwraps(int depth, bool ascii)
    {
        var root = Node(Utf8ExecutionNodeKind.Set, ascii ? Utf8OwnedCharClass.ECMASpaceClass : Utf8OwnedCharClass.SpaceClass);
        for (var index = 0; index < depth; index++)
            root = Node(Utf8ExecutionNodeKind.Loop, "", Node(Utf8ExecutionNodeKind.Group, "", root));
        Assert.True(Assert.IsType<bool>(Call(typeof(Utf8DeterministicVerifierGuards), "IsAsciiWhitespaceLoop", root)));
        object?[] args = [root, 0UL, 0UL];
        Assert.Equal(ascii, Assert.IsType<bool>(Call(typeof(Utf8DeterministicAnchorSearch), "TryGetLeadingAsciiByteMask", args)));
        var expected = new[] { (byte)' ', (byte)'\t', (byte)'\n', (byte)'\r', (byte)'\v', (byte)'\f' }
            .Aggregate(0UL, static (mask, value) => mask | (1UL << value));
        Assert.Equal(ascii ? expected : 0UL, Assert.IsType<ulong>(args[1]));
        Assert.Equal(0UL, Assert.IsType<ulong>(args[2]));
    }

    [Fact]
    public void FailedUnionKeepsCompletedMasksButRejectsLiteralFamily()
    {
        var root = Node(Utf8ExecutionNodeKind.Alternate, "", Node(Utf8ExecutionNodeKind.Multi, "cat"),
            Node(Utf8ExecutionNodeKind.PositiveLookaround, ""), Node(Utf8ExecutionNodeKind.Multi, "dog"));
        object?[] args = [root, 0UL, 0UL];
        Assert.False(Assert.IsType<bool>(Call(typeof(Utf8DeterministicAnchorSearch), "TryGetLeadingAsciiByteMask", args)));
        Assert.Equal(0UL, Assert.IsType<ulong>(args[1]));
        Assert.Equal(1UL << ('c' - 64), Assert.IsType<ulong>(args[2]));
        Assert.Null(Call(typeof(Utf8DeterministicAnchorSearch), "TryExtractLeadingAsciiLiteralsCore", root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralFamilyCapsAndDeduplicationPreserveConcatenationFallback(bool overCap)
    {
        var branches = Enumerable.Range(0, overCap ? 33 : 32)
            .Select(static index => Node(Utf8ExecutionNodeKind.Multi, "x" + index)).ToArray();
        var family = Node(Utf8ExecutionNodeKind.Alternate, "", branches);
        var root = Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Multi, "pre"), family,
            Node(Utf8ExecutionNodeKind.Multi, "post"));
        var result = Assert.IsType<List<string>>(Call(typeof(Utf8DeterministicAnchorSearch), "TryExtractLeadingAsciiLiteralsCore", root));
        Assert.Equal(overCap ? new[] { "pre" } : Enumerable.Range(0, 32).Select(static index => "prex" + index + "post"), result);
    }

    [Fact]
    public void EmptyChildStopsAsciiAppendLocallyButParentRetainsProgress()
    {
        var root = Node(Utf8ExecutionNodeKind.Concatenate, "",
            Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Multi, "ab"),
                Node(Utf8ExecutionNodeKind.Empty, ""), Node(Utf8ExecutionNodeKind.Multi, "unused")),
            Node(Utf8ExecutionNodeKind.Multi, "c"));
        var prefix = new StringBuilder();
        Call(typeof(Utf8DeterministicAnchorSearch), "AppendDeterministicLeadingAscii", root, prefix);
        Assert.Equal("abc", prefix.ToString());
    }

    [Fact]
    public void ProductOverflowDiscardsItsOwnPrefixBeforeOuterSequenceRecovers()
    {
        var left = Node(Utf8ExecutionNodeKind.Alternate, "", Enumerable.Range(0, 32)
            .Select(static index => Node(Utf8ExecutionNodeKind.Multi, "x" + index)).ToArray());
        var right = Node(Utf8ExecutionNodeKind.Alternate, "", Node(Utf8ExecutionNodeKind.Multi, "a"), Node(Utf8ExecutionNodeKind.Multi, "b"));
        var product = Node(Utf8ExecutionNodeKind.Concatenate, "", left, right);
        Assert.Null(Call(typeof(Utf8DeterministicAnchorSearch), "TryExtractLeadingAsciiLiteralsCore", product));
        var root = Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Multi, "pre"), product,
            Node(Utf8ExecutionNodeKind.Multi, "unused"));
        Assert.Equal(new[] { "pre" }, Assert.IsType<List<string>>(Call(typeof(Utf8DeterministicAnchorSearch), "TryExtractLeadingAsciiLiteralsCore", root)));
    }
}
