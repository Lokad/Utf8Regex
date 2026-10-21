using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser conditional stage: the BCL-oracled battery pins conditional
// alternation identical to the provider trees (numeric and named conditions
// resolving at the end pass with backreferences, empty branches lowering to
// empty concatenations, single-kid form for bare defined closes), plus decline
// agreement on undefined references, missing branches, and DEFINE/R conditions
// rejected by the owned parser and checked against the BCL oracle.
public sealed class Utf8OwnedTreeParserConditionalTests
{
    public static IEnumerable<object?[]> ConditionalAcceptCases()
    {
        yield return new object?[] { "(?<x>a)(?(x)b|c)", 512, 512 };
        yield return new object?[] { "(a)(?(1)b|c)", 512, 512 };
        yield return new object?[] { "(?<x>a)(?(x)b|)", 512, 512 };
        yield return new object?[] { "(a)(?(1)b|)", 512, 512 };
        yield return new object?[] { "(?<x>a)(?(x))", 512, 512 };
        yield return new object?[] { "(a)(?(1)b|c)(d)", 512, 512 };
        yield return new object?[] { "(?(1)a|b)(c)", 512, 512 };
        yield return new object?[] { "(?(x)a|b)(?<x>c)", 512, 512 };
        yield return new object?[] { "(a)(?(1)|b)", 512, 512 };
        yield return new object?[] { "(a)(?(1)|)", 512, 512 };
        yield return new object?[] { "(a)(?(1))", 512, 512 };
        yield return new object?[] { "(a)(b)(?(1)(?(2)x|y)|z)", 512, 512 };
        yield return new object?[] { "(a(?(1)b|c))", 512, 512 };
        yield return new object?[] { "(?<x>a(?(x)b|c))", 512, 512 };
        yield return new object?[] { "(?<x>a)(?(x)b|c)", 516, 516 };
        yield return new object?[] { "(?i:(a)(?(1)b|c))", 512, 513 };
        yield return new object?[] { "(a)(?(1)b|c)+", 512, 512 };
        yield return new object?[] { "((a)(?(1)b|c))", 512, 512 };
        yield return new object?[] { "(a)(?(01)b|c)", 512, 512 };
        yield return new object?[] { "(b)(?(01)c|d)", 512, 512 };
        yield return new object?[] { "(?(007)a|b)(?<7>c)", 512, 512 };
        yield return new object?[] { "(?(01)a|b)(a)", 512, 512 };
    }

    public static IEnumerable<object?[]> ConditionalDeclineCases()
    {
        yield return new object?[] { "(?(1)a|b)", 512, RegexParseError.AlternationHasUndefinedReference, 5 };
        yield return new object?[] { "(?(1)a)", 512, RegexParseError.AlternationHasUndefinedReference, 5 };
        yield return new object?[] { "(?(1))", 512, RegexParseError.AlternationHasUndefinedReference, 5 };
        yield return new object?[] { "(?(1)|b)", 512, RegexParseError.AlternationHasUndefinedReference, 5 };
        yield return new object?[] { "(a)(?(1)b|c)", 516, RegexParseError.AlternationHasUndefinedReference, 8 };
        yield return new object?[] { "(a)?(?(1)b|c)", 518, RegexParseError.AlternationHasUndefinedReference, 9 };
        yield return new object?[] { "(a)?(?(1)b|c)", 532, RegexParseError.AlternationHasUndefinedReference, 9 };
        yield return new object?[] { "(a)?(?(1)b|c)", 596, RegexParseError.AlternationHasUndefinedReference, 9 };
        yield return new object?[] { "(a)?(?(1)b|c)", 597, RegexParseError.AlternationHasUndefinedReference, 9 };
        yield return new object?[] { "(?(01)a|b)", 512, RegexParseError.AlternationHasUndefinedReference, 6 };
        yield return new object?[] { "(?<x>a)(?(02)b|c)", 512, RegexParseError.AlternationHasUndefinedReference, 13 };
    }

    [Theory]
    [MemberData(nameof(ConditionalAcceptCases))]
    public void OwnedConditionalServesWithBclMetadata(string pattern, int options, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
    }

    [Fact]
    public void OwnedNonBacktrackingConditionalFailsLikeBcl()
    {
        // BCL NonBacktracking rejects conditionals at construction; the owned
        // parser still serves the shape while the product rejects it through
        // the owned NonBacktracking grammar admission.
        var actual = Utf8OwnedTreeParser.TryParse("(a)(?(1)b|c)", RegexOptions.NonBacktracking);
        Assert.NotNull(actual);
        Assert.Equal(2, actual.CaptureCount);
        Assert.Throws<NotSupportedException>(() => new Regex("(a)(?(1)b|c)", RegexOptions.NonBacktracking));
        Assert.Throws<NotSupportedException>(() => new Utf8Regex("(a)(?(1)b|c)", RegexOptions.NonBacktracking));
    }

    [Theory]
    [MemberData(nameof(ConditionalDeclineCases))]
    public void OwnedConditionalDeclinesUnsupported(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
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
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
    }

    public static IEnumerable<object?[]> ConditionalZeroDigitCases()
    {
        // BCL serves (?(0)...)/(?(00)...) as never-matching tests while nonzero
        // zero-padded numbers resolve strictly by slot. The fork serves these with
        // a different tree shape, so they pin owned-accept plus fork-serves plus
        // BCL behavior instead of tree equality.
        yield return new object?[] { "(?(00)a|b)", 512, new string[] { "b", "a", "" } };
        yield return new object?[] { "(?(0)a|b)", 512, new string[] { "b", "a", "" } };
        yield return new object?[] { "(a)(?(00)b|c)", 512, new string[] { "ac", "a", "c" } };
    }

    [Theory]
    [MemberData(nameof(ConditionalZeroDigitCases))]
    public void OwnedConditionalServesZeroDigit(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
    }
}
