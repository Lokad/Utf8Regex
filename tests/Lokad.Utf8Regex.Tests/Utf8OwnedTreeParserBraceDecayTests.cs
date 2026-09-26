using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser lone bracket and brace decay: BCL spells unmatched closing
// brackets and uncompletable braces as literals, while only complete invalid
// quantifiers (reversed bounds, numeric overflow) reject. Narrowing rows pin
// owned-accept plus fork-decline plus BCL-constructs plus product IsMatch and
// Count agreement; differential rows pin owned trees identical to the fork
// (which decays unclosed digit tails exactly like BCL) plus the same product
// agreement. All expectations over self-text and discriminator inputs come
// live from the BCL oracle, so the rows are self-verifying.
public sealed class Utf8OwnedTreeParserBraceDecayTests
{
    public static IEnumerable<object?[]> BraceDecayNarrowingCases()
    {
        yield return new object?[] { "]", 512, new string[] { "", "]", "a]" } };
        yield return new object?[] { "a]", 512, new string[] { "a]", "a", "]" } };
        yield return new object?[] { "a]b", 512, new string[] { "a]b", "a]" } };
        yield return new object?[] { "}", 512, new string[] { "}", "a}" } };
        yield return new object?[] { "a}", 512, new string[] { "a}", "a" } };
        yield return new object?[] { "a{,3}", 512, new string[] { "a{,3}", "aaa", "a" } };
        yield return new object?[] { "{a}", 512, new string[] { "{a}", "a" } };
        yield return new object?[] { "a{}", 512, new string[] { "a{}", "a" } };
        yield return new object?[] { "a{,}", 512, new string[] { "a{,}", "a" } };
        yield return new object?[] { "(a){,3}", 512, new string[] { "(a){,3}", "a{,3}" } };
        yield return new object?[] { "a{2x}", 512, new string[] { "a{2x}", "aa" } };
        yield return new object?[] { "a{2,x}", 512, new string[] { "a{2,x}", "aa" } };
        yield return new object?[] { "{,3}", 512, new string[] { "{,3}", "a" } };
    }

    public static IEnumerable<object?[]> UnclosedBraceDifferentialCases()
    {
        yield return new object?[] { "a{2", 512, new string[] { "a{2}", "aa" } };
        yield return new object?[] { "a{12", 512, new string[] { "a{12}", "a" } };
        yield return new object?[] { "a{1,2", 512, new string[] { "a{1,2}", "a" } };
        yield return new object?[] { "a{2,", 512, new string[] { "a{2,}", "aa" } };
        yield return new object?[] { "a{2,1", 512, new string[] { "a{2,1}", "aa" } };
        yield return new object?[] { "(a){2", 512, new string[] { "(a){2}", "a{2}" } };
        yield return new object?[] { "{12", 512, new string[] { "{12}", "aa" } };
        yield return new object?[] { "{2,", 512, new string[] { "{2,}", "aa" } };
        yield return new object?[] { "a{10000000000", 512, new string[] { "a{10000000000", "aa" } };
        yield return new object?[] { "a{2,99999999999", 512, new string[] { "a{2,99999999999", "aa" } };
    }

    [Theory]
    [MemberData(nameof(BraceDecayNarrowingCases))]
    public void OwnedDecaysLoneBracketOrBrace(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Null(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options));
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        AssertProductMatchesOracle(oracle, product, inputs);
    }

    [Theory]
    [MemberData(nameof(UnclosedBraceDifferentialCases))]
    public void OwnedUnclosedBraceMatchesProviderTree(string pattern, int options, string[] inputs)
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
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        AssertProductMatchesOracle(oracle, product, inputs);
    }

    private static void AssertProductMatchesOracle(Regex oracle, Utf8Regex product, string[] inputs)
    {
        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
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
