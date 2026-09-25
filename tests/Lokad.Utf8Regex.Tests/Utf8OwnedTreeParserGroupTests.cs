using System.Collections;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser group stage: the differential battery pins named groups,
// numeric and named backreferences, balance pops, and octal escapes identical
// to the provider trees (kinds, options, payloads, capture accounting, and
// name/number/sparse metadata), plus decline agreement on undefined
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
        yield return new object?[] { "(?<0>a)", 512 };
        yield return new object?[] { "(?<a b>c)", 512 };
        yield return new object?[] { "(?<,>a)", 512 };
        yield return new object?[] { "(?<9>a)", 512 };
        yield return new object?[] { "(?<007>a)", 512 };
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
        yield return new object?[] { "(?<00>a)", 512 };
        yield return new object?[] { "(?<01>a)", 512 };
        yield return new object?[] { "(a)(?<x>b)(?<y>c)(d)", 512 };
        yield return new object?[] { "(?<x>a)(b)(?<y>c)", 512 };
        yield return new object?[] { "(?<x>a)(?<y>b)(c)", 512 };
        yield return new object?[] { "(?<a>b>c)", 512 };
        yield return new object?[] { "(?<a'b>c)", 512 };
        yield return new object?[] { "(?'a>b'c)", 512 };
        yield return new object?[] { "(?<a\\)b>c)", 512 };
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
        yield return new object?[] { "(?<0>a)(b)", 512 };
        yield return new object?[] { "(?<00>a)(?<0>b)", 512 };
        yield return new object?[] { "(?<x>a)(?<007>b)", 512 };
        yield return new object?[] { "(a)(?<007>b)(c)", 512 };
        yield return new object?[] { "(?<1>a)(?<1>b)", 512 };
        yield return new object?[] { "(?<000>a)", 512 };
        yield return new object?[] { "(?<01>a)(b)", 512 };
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
        yield return new object?[] { "(?<007>a)(?<-007>b)", 512 };
        yield return new object?[] { "(?<1>a)(?<-01>b)", 512 };
        yield return new object?[] { "(?<01>a)(?<-1>b)", 512 };
        yield return new object?[] { "(?<x>a)(?<1>b)", 512 };
        yield return new object?[] { "(?<2>a)(?<x>b)(?<1>c)", 512 };
        yield return new object?[] { "(?<1>a)(?<x>b)(?<2>c)", 512 };
        yield return new object?[] { "(a)(?<2>b)(?<x>c)", 512 };
        yield return new object?[] { "(?<x>a)(?<2>b)", 512 };
        yield return new object?[] { "(?<2>a)(?<2>b)", 512 };
        yield return new object?[] { "(?<+1>a)", 512 };
        yield return new object?[] { "(?<1a>b)", 512 };
        yield return new object?[] { "(?'1a'b)", 512 };
        yield return new object?[] { "(?<a+>b)", 512 };
        yield return new object?[] { "(?<.>b)", 512 };
        yield return new object?[] { "(?<\\\\>b>c)", 512 };
        yield return new object?[] { "(?<x>a)(?<x>b)(?<x>c)", 512 };
        yield return new object?[] { "(\\k<x>)(?<x>a)(?<x>b)", 512 };
        yield return new object?[] { "(?<01>a)(?<1>b)", 512 };
        yield return new object?[] { "(?<a+>b)\\k<a+>", 512 };
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
        yield return new object?[] { "(?<007>a)(?<-7>b)", 512 };
        yield return new object?[] { "(a)(?<-1>b)", 512 };
        yield return new object?[] { "(?<X>a)", 512 };
        yield return new object?[] { "(?<x>ab)", 512 };
        yield return new object?[] { "(?<aBc123>x)", 512 };
        yield return new object?[] { "((?<x>a))", 512 };
        yield return new object?[] { "(?<x>a|b)", 512 };
        yield return new object?[] { "(?<xy>ab)", 512 };
        yield return new object?[] { "(?<x>a)\\k'x'", 512 };
        yield return new object?[] { "\\k<0>", 512 };
        yield return new object?[] { "(?<0>a)\\k<0>", 512 };
        yield return new object?[] { "(?<0a>b)", 512 };
        yield return new object?[] { "(?<99999999999999999999>a)", 512 };
        yield return new object?[] { "(?<2147483647>a)", 512 };
        yield return new object?[] { "(?<2147483648>a)", 512 };
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
    }

    public static IEnumerable<object?[]> GroupDeclineCases()
    {
        yield return new object?[] { "(?<a-b>c)" };
        yield return new object?[] { "(?<>a)" };
        yield return new object?[] { "(a)\\2" };
        yield return new object?[] { "\\1" };
        yield return new object?[] { "(?<x>a)\\k<y>" };
        yield return new object?[] { "\\k<x>" };
        yield return new object?[] { "\\k<>" };
        yield return new object?[] { "\\k" };
        yield return new object?[] { "(?<-x>a)" };
        yield return new object?[] { "(?<a-b>c)(d)" };
        yield return new object?[] { "(?<9>a)\\1" };
        yield return new object?[] { "(?<9>a)(b)\\2" };
        yield return new object?[] { "(?<7>a)\\k<007>" };
        yield return new object?[] { "\\k<10>" };
        yield return new object?[] { "\\k'1'" };
        yield return new object?[] { "(?<-1>b)" };
        yield return new object?[] { "(?<-007>b)" };
        yield return new object?[] { "(a)\\4" };
        yield return new object?[] { "\\4" };
        yield return new object?[] { "(?<-a>b)" };
        yield return new object?[] { "(?<a->b)" };
        yield return new object?[] { "(?<--a>b)" };
        yield return new object?[] { "(?'a-b'c)" };
        yield return new object?[] { "(?<a-b-c>d)" };
        yield return new object?[] { "(?<a>b" };
        yield return new object?[] { "(?'a>b)" };
        yield return new object?[] { "\\k<x" };
        yield return new object?[] { "\\k'x" };
        yield return new object?[] { "(?<x>" };
        yield return new object?[] { "(a)\\k<2>" };
        yield return new object?[] { "(?<-1a>b)" };
        yield return new object?[] { "\\999" };
        yield return new object?[] { "\\99999999999999999999" };
        yield return new object?[] { "(?P<x>a)" };
        yield return new object?[] { "\\k<1>" };
        yield return new object?[] { "(?<01>a)\\k<01>" };
        yield return new object?[] { "(a)\\9" };
        yield return new object?[] { "(?<->b)" };
    }

    [Theory]
    [MemberData(nameof(GroupAcceptCases))]
    public void OwnedGroupsMatchProviderTree(string pattern, int options)
    {
        var expected = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        Assert.NotNull(expected);
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Equal(expected.CaptureCount, actual.CaptureCount);
        Assert.Equal(expected.CaptureNames ?? [], actual.CaptureNames ?? []);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Culture, actual.Culture);
        AssertHashtableEqual(expected.CaptureNameToNumberMapping, actual.CaptureNameToNumberMapping);
        AssertHashtableEqual(expected.CaptureNumberSparseMapping, actual.CaptureNumberSparseMapping);
        Assert.True(TreesEqual(expected.Root, actual.Root));
    }

    [Theory]
    [MemberData(nameof(GroupDeclineCases))]
    public void OwnedGroupsDeclineInvalidReference(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            RegexOptions.CultureInvariant,
            RuntimeFrontEnd.RegexParser.GetTargetCulture(RegexOptions.CultureInvariant)));
    }

    private static void AssertHashtableEqual(Hashtable? expected, Hashtable? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.Equal(expected.Count, actual.Count);
        foreach (DictionaryEntry entry in expected)
        {
            Assert.True(actual.ContainsKey(entry.Key));
            Assert.Equal(entry.Value, actual[entry.Key]);
        }
    }

    private static bool TreesEqual(RuntimeFrontEnd.RegexNode left, RuntimeFrontEnd.RegexNode right)
    {
        return right.Kind == left.Kind &&
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