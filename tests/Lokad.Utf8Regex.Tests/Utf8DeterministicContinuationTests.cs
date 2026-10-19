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
    private static Utf8ScalarClass Class(string pattern)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var scalarClass = tree.Root.Child(0).ScalarClass;
        Assert.NotNull(scalarClass);
        return scalarClass;
    }

    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, string text, params Utf8ExecutionNode[] children)
        => new(kind, RegexOptions.CultureInvariant, children, kind == Utf8ExecutionNodeKind.Set ? Class(text) : text, '\0', 0, 1, 1);

    private static object? Call(Type type, string name, params object?[] args)
    {
        var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method.Invoke(null, args);
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
        var root = Node(Utf8ExecutionNodeKind.Set, ascii ? @"[\t-\r ]" : @"\s");
        for (var index = 0; index < depth; index++)
            root = Node(Utf8ExecutionNodeKind.Loop, "", Node(Utf8ExecutionNodeKind.Group, "", root));
        Assert.Equal(ascii, Assert.IsType<bool>(Call(typeof(Utf8DeterministicVerifierGuards), "IsAsciiWhitespaceLoop", root)));

    }

}
