using System.Text.RegularExpressions;
using System.Text;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser conditional stage: the differential battery pins conditional
// alternation identical to the provider trees (numeric and named conditions
// resolving at the end pass with backreferences, empty branches lowering to
// empty concatenations, single-kid form for bare defined closes), plus decline
// agreement on undefined references, missing branches, and DEFINE/R conditions
// the fork itself rejects while BCL serves through the fallback.
public sealed class Utf8OwnedTreeParserConditionalTests
{
    public static IEnumerable<object?[]> ConditionalAcceptCases()
    {
        yield return new object?[] { "(?<x>a)(?(x)b|c)", 512 };
        yield return new object?[] { "(a)(?(1)b|c)", 512 };
        yield return new object?[] { "(?<x>a)(?(x)b|)", 512 };
        yield return new object?[] { "(a)(?(1)b|)", 512 };
        yield return new object?[] { "(?<x>a)(?(x))", 512 };
        yield return new object?[] { "(a)(?(1)b|c)(d)", 512 };
        yield return new object?[] { "(?(1)a|b)(c)", 512 };
        yield return new object?[] { "(?(x)a|b)(?<x>c)", 512 };
        yield return new object?[] { "(a)(?(1)|b)", 512 };
        yield return new object?[] { "(a)(?(1)|)", 512 };
        yield return new object?[] { "(a)(?(1))", 512 };
        yield return new object?[] { "(a)(b)(?(1)(?(2)x|y)|z)", 512 };
        yield return new object?[] { "(a(?(1)b|c))", 512 };
        yield return new object?[] { "(?<x>a(?(x)b|c))", 512 };
        yield return new object?[] { "(?<x>a)(?(x)b|c)", 516 };
        yield return new object?[] { "(a)(?(1)b|c)", 1024 };
        yield return new object?[] { "(?i:(a)(?(1)b|c))", 512 };
        yield return new object?[] { "(a)(?(1)b|c)+", 512 };
        yield return new object?[] { "((a)(?(1)b|c))", 512 };
    }

    public static IEnumerable<object?[]> ConditionalDeclineCases()
    {
        yield return new object?[] { "(?(1)a|b)", 512 };
        yield return new object?[] { "(?(1)a)", 512 };
        yield return new object?[] { "(?(1))", 512 };
        yield return new object?[] { "(?(1)|b)", 512 };
        yield return new object?[] { "(a)(?(1)b|c)", 516 };
    }

    [Theory]
    [MemberData(nameof(ConditionalAcceptCases))]
    public void OwnedConditionalMatchesProviderTree(string pattern, int options)
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

    [Theory]
    [MemberData(nameof(ConditionalDeclineCases))]
    public void OwnedConditionalDeclinesUnsupported(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            (RegexOptions)options,
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options)));
    }

    public static IEnumerable<object?[]> ConditionalUndefinedNameCases()
    {
        yield return new object?[] { "(?(x)a|b)", 512, new string[] { "b", "a", "" } };
        yield return new object?[] { "(?(x)a|b)", 516, new string[] { "b", "a", "" } };
        yield return new object?[] { "(?(y))", 512, new string[] { "", "a" } };
        yield return new object?[] { "(?(x)a)", 512, new string[] { "", "a" } };
        yield return new object?[] { "(?(DEFINE)a|b)", 512, new string[] { "b", "a" } };
        yield return new object?[] { "(?(R)a|b)", 512, new string[] { "b", "a" } };
        yield return new object?[] { "(?(R1)a|b)", 512, new string[] { "b", "a" } };
        yield return new object?[] { "(?(undefined)ab|cd)", 512, new string[] { "cd", "ab", "" } };
    }

    [Theory]
    [MemberData(nameof(ConditionalUndefinedNameCases))]
    public void OwnedConditionalServesUndefinedName(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            (RegexOptions)options,
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options)));
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
    }

    public static IEnumerable<object?[]> ConditionalSingleBranchCases()
    {
        // Single non-empty branches without else lower with an implicit empty else.
        // The fork serves these with a different (single-kid) tree shape, so they pin
        // owned-accept plus fork-serves plus BCL behavior instead of tree equality.
        yield return new object?[] { "(a)(?(1)a)", 512, new string[] { "aa", "a" } };
        yield return new object?[] { "(?<x>a)(?(x)a)", 512, new string[] { "aa", "a" } };
    }

    [Theory]
    [MemberData(nameof(ConditionalSingleBranchCases))]
    public void OwnedConditionalServesSingleBranch(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.NotNull(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options));
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
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