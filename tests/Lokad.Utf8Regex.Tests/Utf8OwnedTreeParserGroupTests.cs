using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser group stage: the BCL-oracled battery pins named groups,
// numeric and named backreferences, balance pops, and octal escapes with BCL
// metadata (capture accounting and name/number/sparse mappings checked against
// the group APIs), plus decline pins with construction errors on undefined
// references and malformed names, and the octal-rest residual that still
// routes to the provider.
public sealed class Utf8OwnedTreeParserGroupTests
{
    public static IEnumerable<object?[]> GroupAcceptCases()
    {
        yield return new object?[] { "(?<x>a)", 512 };
        yield return new object?[] { "(?'x'a)", 512 };
        yield return new object?[] { "(?<x>a)(?<y>b)", 512 };
        yield return new object?[] { "(?<ab12>c)", 512 };
        yield return new object?[] { "(?<9>a)", 512 };
        yield return new object?[] { "(a)(?<x>b)", 512 };
        yield return new object?[] { "(?<x>a)(b)", 512 };
        yield return new object?[] { "(?<x>a)(?<x>b)", 512 };
        yield return new object?[] { "(?<x>a)\\k<x>", 512 };
        yield return new object?[] { "(a)\\1", 512 };
        yield return new object?[] { "(a)(b)\\2", 512 };
        yield return new object?[] { "\\01", 512 };
        yield return new object?[] { "\\08", 512 };
        yield return new object?[] { "\\0", 512 };
        yield return new object?[] { "\\00", 512 };
        yield return new object?[] { "\\000", 512 };
        yield return new object?[] { "\\07", 512 };
        yield return new object?[] { "(a){2}\\1", 512 };
        yield return new object?[] { "(a\\1)", 512 };
        yield return new object?[] { "(\\1a)", 512 };
        yield return new object?[] { "(a)(b)\\1\\2", 512 };
        yield return new object?[] { "(?<name>a)\\k<name>", 512 };
        yield return new object?[] { "(?<name>a)(?<name>b)", 512 };
        yield return new object?[] { "(?'n'a)\\k'n'", 512 };
        yield return new object?[] { "(?<x>ab)+\\k<x>", 512 };
        yield return new object?[] { "((?<x>a)|(b))\\k<x>", 512 };
        yield return new object?[] { "(?<x>a)(?<-x>b)", 512 };
        yield return new object?[] { "(?<-0>a)", 512 };
        yield return new object?[] { "(?<x>a)(?<-0>b)", 512 };
        yield return new object?[] { "(?<x>)(?<-x>)", 512 };
        yield return new object?[] { "(?<a>)(?<-a>)(b)", 512 };
        yield return new object?[] { "(a)(?<x>b)(c)\\k<x>\\3\\1", 512 };
        yield return new object?[] { "(?<1>a)", 512 };
        yield return new object?[] { "(?<1>a)(b)", 512 };
        yield return new object?[] { "(a)(?<1>b)", 512 };
        yield return new object?[] { "(?<2>a)", 512 };
        yield return new object?[] { "(?<2>a)(b)", 512 };
        yield return new object?[] { "(a)(?<x>b)(?<y>c)(d)", 512 };
        yield return new object?[] { "(?<x>a)(b)(?<y>c)", 512 };
        yield return new object?[] { "(?<x>a)(?<y>b)(c)", 512 };
        yield return new object?[] { "(?<a>b>c)", 512 };
        yield return new object?[] { "(a\\2)(b)", 512 };
        yield return new object?[] { "(\\2a)(b)", 512 };
        yield return new object?[] { "(\\k<x>a)(?<x>b)", 512 };
        yield return new object?[] { "(a\\k<x>)(?<x>b)", 512 };
        yield return new object?[] { "(a)\\01", 512 };
        yield return new object?[] { "(a)(b)\\02", 512 };
        yield return new object?[] { "(a)\\001", 512 };
        yield return new object?[] { "(?<1>a)(?<x>b)", 512 };
        yield return new object?[] { "(?<9>a)\\9", 512 };
        yield return new object?[] { "(a)\\k<1>", 512 };
        yield return new object?[] { "(?<1>a)\\k<1>", 512 };
        yield return new object?[] { "(?<1>a)\\k'1'", 512 };
        yield return new object?[] { "(?<1>a)(?<1>b)", 512 };
        yield return new object?[] { "(?<10>a)", 512 };
        yield return new object?[] { "(?<10>a)\\k<10>", 512 };
        yield return new object?[] { "(a)\\1\\12", 512 };
        yield return new object?[] { "(a)(b)\\1\\12", 512 };
        yield return new object?[] { "(a)\\40", 512 };
        yield return new object?[] { "(?<9>a)(b)", 512 };
        yield return new object?[] { "(?<8>a)(?<9>b)", 512 };
        yield return new object?[] { "(?<9>a)(b)(?<8>c)", 512 };
        yield return new object?[] { "(?<9>a)(?<x>b)", 512 };
        yield return new object?[] { "(?<x>a)(?<9>b)", 512 };
        yield return new object?[] { "(?<1>a)(?<-01>b)", 512 };
        yield return new object?[] { "(?<x>a)(?<1>b)", 512 };
        yield return new object?[] { "(?<2>a)(?<x>b)(?<1>c)", 512 };
        yield return new object?[] { "(?<1>a)(?<x>b)(?<2>c)", 512 };
        yield return new object?[] { "(a)(?<2>b)(?<x>c)", 512 };
        yield return new object?[] { "(?<x>a)(?<2>b)", 512 };
        yield return new object?[] { "(?<2>a)(?<2>b)", 512 };
        yield return new object?[] { "(?<x>a)(?<x>b)(?<x>c)", 512 };
        yield return new object?[] { "(\\k<x>)(?<x>a)(?<x>b)", 512 };
        yield return new object?[] { "(?<\u00E9>x)\\k<\u00E9>", 512 };
        yield return new object?[] { "(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)\\11", 512 };
        yield return new object?[] { "\\11", 512 };
        yield return new object?[] { "\\111", 512 };
        yield return new object?[] { "\\101", 512 };
        yield return new object?[] { "\\177", 512 };
        yield return new object?[] { "\\200", 512 };
        yield return new object?[] { "\\377", 512 };
        yield return new object?[] { "\\12", 512 };
        yield return new object?[] { "\\001", 512 };
        yield return new object?[] { "\\055", 512 };
        yield return new object?[] { "\\060", 512 };
        yield return new object?[] { "\\010", 512 };
        yield return new object?[] { "\\040", 512 };
        yield return new object?[] { "\\40", 512 };
        yield return new object?[] { "\\10", 512 };
        yield return new object?[] { "\\077", 512 };
        yield return new object?[] { "\\10+", 512 };
        yield return new object?[] { "\\01+", 512 };
        yield return new object?[] { "(a)\\10+", 512 };
        yield return new object?[] { "(\\10)", 512 };
        yield return new object?[] { "(\\10)+", 512 };
        yield return new object?[] { "x\\10y", 512 };
        yield return new object?[] { "\\01a", 512 };
        yield return new object?[] { "\\10a", 512 };
        yield return new object?[] { "\\10|z", 512 };
        yield return new object?[] { "(a)\\01b", 512 };
        yield return new object?[] { "\\10*", 512 };
        yield return new object?[] { "\\10?", 512 };
        yield return new object?[] { "\\10+?", 512 };
        yield return new object?[] { "\\10{2}", 512 };
        yield return new object?[] { "\\01{2}", 512 };
        yield return new object?[] { "\\01?", 512 };
        yield return new object?[] { "\\12*", 512 };
        yield return new object?[] { "(a)\\1*?", 512 };
        yield return new object?[] { "(a)\\1+", 512 };
        yield return new object?[] { "(a)\\1?", 512 };
        yield return new object?[] { "(a)\\12+", 512 };
        yield return new object?[] { "(a)(b)\\12?", 512 };
        yield return new object?[] { "(?<1>a)(?<-1>b)", 512 };
        yield return new object?[] { "(a)(?<-1>b)", 512 };
        yield return new object?[] { "(?<X>a)", 512 };
        yield return new object?[] { "(?<x>ab)", 512 };
        yield return new object?[] { "(?<aBc123>x)", 512 };
        yield return new object?[] { "((?<x>a))", 512 };
        yield return new object?[] { "(?<x>a|b)", 512 };
        yield return new object?[] { "(?<xy>ab)", 512 };
        yield return new object?[] { "(?<x>a)\\k'x'", 512 };
        yield return new object?[] { "\\k<0>", 512 };
        yield return new object?[] { "(?<2147483647>a)", 512 };
        yield return new object?[] { "\\078", 512 };
        yield return new object?[] { "\\080", 512 };
        yield return new object?[] { "\\09", 512 };
        yield return new object?[] { "\\0a", 512 };
        yield return new object?[] { "\\00a", 512 };
        yield return new object?[] { "(a)\\078", 512 };
        yield return new object?[] { "[\\01]", 512 };
        yield return new object?[] { "[\\078]", 512 };
        yield return new object?[] { "[\\0]", 512 };
        yield return new object?[] { "[\\00]", 512 };
        yield return new object?[] { "[\\08]", 512 };
        yield return new object?[] { "[\\0-\\01]", 512 };
        yield return new object?[] { "(?<x>a)", 768 };
        yield return new object?[] { "(a)\\1", 768 };
        yield return new object?[] { "(?<x>a)", 576 };
        yield return new object?[] { "(a)\\1", 576 };
        yield return new object?[] { "(?<x>a)\\k<x>", 576 };
        yield return new object?[] { "\\01", 768 };
        yield return new object?[] { "(a)(b)\\2", 576 };
        yield return new object?[] { "^a$", 64 };
        yield return new object?[] { "a|b", 64 };
        yield return new object?[] { "(a)(b)", 520 };
    }

    public static IEnumerable<object?[]> GroupInvalidNameDeclineCases()
    {
        // BCL capture-name rule (machine-probed): names with non-word characters
        // decline to the BCL fallback with the pinned construction error.
        yield return new object?[] { "(?<a b>c)", 512, RegexParseError.CaptureGroupNameInvalid, 4 };
        yield return new object?[] { "(?<,>a)", 512, RegexParseError.CaptureGroupNameInvalid, 3 };
        yield return new object?[] { "(?<a'b>c)", 512, RegexParseError.CaptureGroupNameInvalid, 4 };
        yield return new object?[] { "(?'a>b'c)", 512, RegexParseError.CaptureGroupNameInvalid, 4 };
        yield return new object?[] { "(?<a\\)b>c)", 512, RegexParseError.CaptureGroupNameInvalid, 4 };
        yield return new object?[] { "(?<+1>a)", 512, RegexParseError.CaptureGroupNameInvalid, 3 };
        yield return new object?[] { "(?<a+>b)", 512, RegexParseError.CaptureGroupNameInvalid, 4 };
        yield return new object?[] { "(?<.>b)", 512, RegexParseError.CaptureGroupNameInvalid, 3 };
        yield return new object?[] { "(?<\\\\>b>c)", 512, RegexParseError.CaptureGroupNameInvalid, 3 };
        yield return new object?[] { "(?<a+>b)\\k<a+>", 512, RegexParseError.UnrecognizedEscape, 10 };
    }

    [Theory]
    [MemberData(nameof(GroupInvalidNameDeclineCases))]
    public void OwnedGroupDeclinesInvalidName(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }
    public static IEnumerable<object?[]> GroupDeclineCases()
    {
        yield return new object?[] { "(?<a-b>c)", RegexParseError.UndefinedNamedReference, 6 };
        yield return new object?[] { "(?<>a)", RegexParseError.CaptureGroupNameInvalid, 3 };
        yield return new object?[] { "(a)\\2", RegexParseError.UndefinedNumberedReference, 5 };
        yield return new object?[] { "\\1", RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "(?<x>a)\\k<y>", RegexParseError.UndefinedNamedReference, 12 };
        yield return new object?[] { "\\k<x>", RegexParseError.UndefinedNamedReference, 5 };
        yield return new object?[] { "\\k<>", RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "\\k", RegexParseError.MalformedNamedReference, 1 };
        yield return new object?[] { "(?<-x>a)", RegexParseError.UndefinedNamedReference, 5 };
        yield return new object?[] { "(?<a-b>c)(d)", RegexParseError.UndefinedNamedReference, 6 };
        yield return new object?[] { "(?<9>a)\\1", RegexParseError.UndefinedNumberedReference, 9 };
        yield return new object?[] { "(?<9>a)(b)\\2", RegexParseError.UndefinedNumberedReference, 12 };
        yield return new object?[] { "\\k<10>", RegexParseError.UndefinedNumberedReference, 6 };
        yield return new object?[] { "\\k'1'", RegexParseError.UndefinedNumberedReference, 5 };
        yield return new object?[] { "(?<-1>b)", RegexParseError.UndefinedNumberedReference, 5 };
        yield return new object?[] { "(?<-007>b)", RegexParseError.UndefinedNumberedReference, 7 };
        yield return new object?[] { "(a)\\4", RegexParseError.UndefinedNumberedReference, 5 };
        yield return new object?[] { "\\4", RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "(?<-a>b)", RegexParseError.UndefinedNamedReference, 5 };
        yield return new object?[] { "(?<a->b)", RegexParseError.CaptureGroupNameInvalid, 5 };
        yield return new object?[] { "(?<--a>b)", RegexParseError.CaptureGroupNameInvalid, 4 };
        yield return new object?[] { "(?'a-b'c)", RegexParseError.UndefinedNamedReference, 6 };
        yield return new object?[] { "(?<a-b-c>d)", RegexParseError.UndefinedNamedReference, 6 };
        yield return new object?[] { "(?<a>b", RegexParseError.InsufficientClosingParentheses, 6 };
        yield return new object?[] { "(?'a>b)", RegexParseError.CaptureGroupNameInvalid, 4 };
        yield return new object?[] { "\\k<x", RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "\\k'x", RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "(?<x>", RegexParseError.InsufficientClosingParentheses, 5 };
        yield return new object?[] { "(a)\\k<2>", RegexParseError.UndefinedNumberedReference, 8 };
        yield return new object?[] { "(?<-1a>b)", RegexParseError.UndefinedNumberedReference, 5 };
        yield return new object?[] { "\\999", RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "\\99999999999999999999", RegexParseError.QuantifierOrCaptureGroupOutOfRange, 11 };
        yield return new object?[] { "(?P<x>a)", RegexParseError.InvalidGroupingConstruct, 3 };
        yield return new object?[] { "\\k<1>", RegexParseError.UndefinedNumberedReference, 5 };
        yield return new object?[] { "(?<01>a)\\k<01>", RegexParseError.InvalidGroupingConstruct, 5 };
        yield return new object?[] { "(a)\\9", RegexParseError.UndefinedNumberedReference, 5 };
        yield return new object?[] { "(?<->b)", RegexParseError.CaptureGroupNameInvalid, 4 };
    }

    [Theory]
    [MemberData(nameof(GroupAcceptCases))]
    public void OwnedGroupsServeWithBclMetadata(string pattern, int options)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)options, actual.Options);
        Assert.Equal((options & (int)RegexOptions.CultureInvariant) != 0 ? CultureInfo.InvariantCulture.Name : null, actual.Culture?.Name);
        AssertGroupNameMapping(oracle, actual.CaptureNameToNumberMapping);
        AssertGroupNumberMapping(oracle, actual.CaptureNumberSparseMapping);
    }

    [Fact]
    public void OwnedNonBacktrackingRtlRowServesPinnedMetadata()
    {
        // NonBacktracking plus RightToLeft is BCL-rejected at the options layer,
        // so no BCL construction oracle exists for this row: pin the owned values.
        var actual = Utf8OwnedTreeParser.TryParse("(a)(b)", RegexOptions.NonBacktracking | RegexOptions.RightToLeft);
        Assert.NotNull(actual);
        Assert.Equal(3, actual.CaptureCount);
        Assert.Equal(new[] { "0", "1", "2" }, actual.CaptureNames ?? []);
        Assert.Equal(RegexOptions.NonBacktracking | RegexOptions.RightToLeft, actual.Options);
        Assert.Null(actual.Culture);
        Assert.Null(actual.CaptureNameToNumberMapping);
        Assert.Null(actual.CaptureNumberSparseMapping);
    }

    [Theory]
    [MemberData(nameof(GroupDeclineCases))]
    public void OwnedGroupsDeclineInvalidReference(string pattern, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

    public static IEnumerable<object?[]> GroupLeadingZeroDeclineCases()
    {
        yield return new object?[] { "(?<0>a)", 512 };
        yield return new object?[] { "(?<007>a)", 512 };
        yield return new object?[] { "(?<00>a)", 512 };
        yield return new object?[] { "(?<01>a)", 512 };
        yield return new object?[] { "(?<00>a)(?<0>b)", 512 };
        yield return new object?[] { "(?<x>a)(?<007>b)", 512 };
        yield return new object?[] { "(a)(?<007>b)(c)", 512 };
        yield return new object?[] { "(?<000>a)", 512 };
        yield return new object?[] { "(?<007>a)(?<-007>b)", 512 };
        yield return new object?[] { "(?<01>a)(?<-1>b)", 512 };
        yield return new object?[] { "(?<007>a)(?<-7>b)", 512 };
        yield return new object?[] { "(?<007>a)", 513 };
        yield return new object?[] { "(?<0>a)(b)", 512 };
        yield return new object?[] { "(?<0>a)\\k<0>", 512 };
        yield return new object?[] { "(?<1a>b)", 512 };
        yield return new object?[] { "(?<0a>b)", 512 };
        yield return new object?[] { "(?<01>a)(?(01)b|c)", 512 };
        yield return new object?[] { "(?<007>a)(b)(c)(d)(e)(f)(g)", 512 };
        yield return new object?[] { "(?n)(?<01>a)(b)", 512 };
        yield return new object?[] { "(?n)(a)(?<01>b)", 512 };
        yield return new object?[] { "(?<01>a)(?<01>b)", 512 };
        yield return new object?[] { "(?'1a'b)", 512 };
        yield return new object?[] { "(?<99999999999999999999>a)", 512 };
        yield return new object?[] { "(?<2147483648>a)", 512 };
    }

    [Theory]
    [MemberData(nameof(GroupLeadingZeroDeclineCases))]
    public void OwnedGroupDeclinesLeadingZeroDefinition(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.Throws<System.Text.RegularExpressions.RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
    }

    public static IEnumerable<object?[]> GroupZeroPaddedRefCases()
    {
        yield return new object?[] { "(?<7>a)\\k<007>", 512, new string[] { "aa", "ab", "a" } };
        yield return new object?[] { "(?<7>a)\\k<07>", 512, new string[] { "aa", "ab", "a" } };
        yield return new object?[] { "(?<1>a)\\k<01>", 512, new string[] { "aa", "ab", "a" } };
        yield return new object?[] { "(?<01>a)(b)\\k<01>", 512, new string[] { "abb", "ab", "a" } };
        yield return new object?[] { "(?<01>a)(?<1>b)\\k<01>", 512, new string[] { "aba", "ab", "a" } };
        yield return new object?[] { "(?<1>a)(?<01>b)\\k<01>", 512, new string[] { "aba", "ab", "a" } };
        yield return new object?[] { "(?<7>a)\\k'007'", 512, new string[] { "aa", "ab", "a" } };
        yield return new object?[] { "(b)\\k<01>", 512, new string[] { "bb", "ab", "b" } };
    }
    [Theory]
    [MemberData(nameof(GroupZeroPaddedRefCases))]
    public void OwnedGroupResolvesZeroPaddedRef(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNames(), product.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), product.GetGroupNumbers());
        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
    }

    public static IEnumerable<object?[]> GroupExistingNumberAliasCases()
    {
        // Leading-zero declarations capture into an existing public number.
        // They declare no additional number or name; a canonical, unnamed or
        // named declaration elsewhere must provide the referenced identity.
        yield return new object?[] { "(?<01>a)(b)", 512, new string[] { "ab", "a", "b" } };
        yield return new object?[] { "(?<01>a)(?<1>b)", 512, new string[] { "ab", "a", "b" } };
        yield return new object?[] { "(?<1>a)(?<01>b)", 512, new string[] { "ab", "a", "b" } };
        yield return new object?[] { "(a)(?<01>x)", 512, new string[] { "a", "b", "" } };
        yield return new object?[] { "(a)(?<01>x)(b)", 512, new string[] { "ab", "a", "b" } };
        yield return new object?[] { "(b)(?<01>a)", 512, new string[] { "ba", "a", "b" } };
        yield return new object?[] { "(?<01>a)(b)(c)", 512, new string[] { "abc", "ab", "a" } };
        yield return new object?[] { "(?<01>ab)(b)", 512, new string[] { "abb", "ab", "a" } };
        yield return new object?[] { "(?<01>(a))", 512, new string[] { "a", "b", "" } };
        yield return new object?[] { "(?<01>a)(?=(b))", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?<01>a)(?:(b))", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?<01>a)(?<x>b)", 512, new string[] { "ab", "a", "b" } };
        yield return new object?[] { "(?<01>a)(?'x'b)", 512, new string[] { "ab", "a", "b" } };
        yield return new object?[] { "(?<01>a)(b)\\1", 512, new string[] { "abb", "ab", "a" } };
        yield return new object?[] { "(?<01>a)(?<01>b)(c)", 512, new string[] { "abc", "ab" } };
        yield return new object?[] { "(?<01>a)(?<02>b)(c)(d)", 512, new string[] { "abcd", "abc" } };
        yield return new object?[] { "(a)(?<02>x)(b)", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?<02>a)(b)(c)", 512, new string[] { "abc", "ab" } };
        yield return new object?[] { "(?<00000000000000000000001>a)(b)", 512, new string[] { "ab", "a" } };
    }

    [Theory]
    [MemberData(nameof(GroupExistingNumberAliasCases))]
    public void OwnedGroupServesExistingNumberAlias(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNames(), product.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), product.GetGroupNumbers());
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        foreach (var inputText in inputs)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(oracle.IsMatch(inputText), product.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
            var expected = oracle.Match(inputText);
            var context = product.MatchDetailed(input);
            Assert.Equal(expected.Value, context.GetValueString());
            foreach (var number in oracle.GetGroupNumbers())
            {
                var group = context.GetGroup(number);
                Assert.Equal(expected.Groups[number].Value, group.GetValueString());
                Assert.Equal(expected.Groups[number].Captures.Count, group.CaptureCount);
                for (var index = 0; index < group.CaptureCount; index++)
                {
                    var capture = group.GetCapture(index);
                    Assert.Equal(expected.Groups[number].Captures[index].Index, capture.IndexInUtf16);
                    Assert.Equal(expected.Groups[number].Captures[index].Length, capture.LengthInUtf16);
                    Assert.Equal(expected.Groups[number].Captures[index].Value, capture.GetValueString());
                }
            }
            Assert.False(product.Inspection.DebugFallbackRegexIsCreated);
        }
    }
    private static void AssertGroupNameMapping(Regex oracle, Hashtable? actual)
    {
        var names = oracle.GetGroupNames();
        if (actual is null)
        {
            Assert.All(names, static name => Assert.True(int.TryParse(name, out _), "Non-numeric group name without a mapping: " + name));
            return;
        }

        Assert.Equal(names.Length, actual.Count);
        foreach (var name in names)
        {
            Assert.True(actual.ContainsKey(name), "Missing name mapping: " + name);
            Assert.Equal(oracle.GroupNumberFromName(name), (int)actual[name]!);
        }
    }

    private static void AssertGroupNumberMapping(Regex oracle, Hashtable? actual)
    {
        var numbers = oracle.GetGroupNumbers();
        if (actual is null)
        {
            for (var index = 0; index < numbers.Length; index++)
            {
                Assert.Equal(index, numbers[index]);
            }

            return;
        }

        Assert.Equal(numbers.Length, actual.Count);
        foreach (DictionaryEntry entry in actual)
        {
            Assert.Equal((int)entry.Key, numbers[(int)entry.Value!]);
        }
    }
}
