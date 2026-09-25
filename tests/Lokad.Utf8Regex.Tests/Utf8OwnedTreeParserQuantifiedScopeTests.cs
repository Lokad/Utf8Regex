using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser quantified-scope stage: the differential battery pins loops
// directly over scoped groups identical to the provider trees. Loop wrappers
// carry the atom options (so a wrapped scope marks the loop), which the merge
// pass and the leading fence leave alone; behavior is unchanged because bodies
// carry the effective scoping either way.
public sealed class Utf8OwnedTreeParserQuantifiedScopeTests
{
    public static IEnumerable<object?[]> QuantifiedScopeAcceptCases()
    {
        yield return new object?[] { "a(?m:b)+", 512 };
        yield return new object?[] { "a(?i:\\d)?", 512 };
        yield return new object?[] { "a(?m:[ab]){1,2}", 512 };
        yield return new object?[] { "a(?m:.)*", 512 };
        yield return new object?[] { "a(?i:^)+", 512 };
        yield return new object?[] { "a(?m:$){2}", 512 };
        yield return new object?[] { "((?m:a)b)+", 512 };
        yield return new object?[] { "(a(?m:b))+", 512 };
        yield return new object?[] { "(?i:(?m:a)+)", 512 };
        yield return new object?[] { "x(?s:y)*", 512 };
        yield return new object?[] { "a(?i:b)+c", 512 };
        yield return new object?[] { "(a(?i)b)+", 512 };
        yield return new object?[] { "(a(?i)b)*", 512 };
        yield return new object?[] { "((?i)a)+", 512 };
        yield return new object?[] { "(x(?i)y)+", 512 };
    }

    [Theory]
    [MemberData(nameof(QuantifiedScopeAcceptCases))]
    public void OwnedQuantifiedScopeMatchesProviderTree(string pattern, int options)
    {
        var expected = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        Assert.NotNull(expected);
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Equal(expected.CaptureCount, actual.CaptureCount);
        Assert.Equal(expected.CaptureNames ?? [], actual.CaptureNames ?? []);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Culture, actual.Culture);
        Assert.True(TreesEqual(expected.Root, actual.Root));
    }

    private static bool TreesEqual(RuntimeFrontEnd.RegexNode left, Utf8TreeNode right)
    {
        return (int)right.Kind == (int)left.Kind &&
            right.Options == left.Options &&
            right.Ch == left.Ch &&
            right.Str == left.Str &&
            right.Str2 == left.Str2 &&
            right.M == left.M &&
            right.N == left.N &&
            right.ChildCount == left.ChildCount &&
            Enumerable.Range(0, left.ChildCount).All(index => TreesEqual(left.Child(index), right.Child(index)));
    }
}