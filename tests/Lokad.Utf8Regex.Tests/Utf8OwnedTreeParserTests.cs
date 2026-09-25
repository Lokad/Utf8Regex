using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser strangler: the differential battery pins owned trees
// identical to provider trees (kinds, options, payloads, children, capture
// accounting) for the literal/escape/concatenation/alternation/group/
// quantifier/dot/anchor/class/scoped-option/comment subset, plus option-gated
// declines where the provider
// shape demonstrably differs. Parent links, source positions, and find
// optimizations are construction details outside the comparison.
public sealed class Utf8OwnedTreeParserTests
{
    public static IEnumerable<object?[]> AcceptCases()
    {
        yield return new object?[] { "" };
        yield return new object?[] { "a" };
        yield return new object?[] { "abc" };
        yield return new object?[] { "a|b" };
        yield return new object?[] { "(a)" };
        yield return new object?[] { "(?:ab)" };
        yield return new object?[] { "a*" };
        yield return new object?[] { "a+" };
        yield return new object?[] { "a?" };
        yield return new object?[] { "a{2}" };
        yield return new object?[] { "a{1}" };
        yield return new object?[] { "(a){1}" };
        yield return new object?[] { "a{1,1}" };
        yield return new object?[] { "a{1}?" };
        yield return new object?[] { "a{2,4}" };
        yield return new object?[] { "a{2,}" };
        yield return new object?[] { "(a+)(b+)" };
        yield return new object?[] { "()" };
        yield return new object?[] { "(a|)" };
        yield return new object?[] { "|" };
        yield return new object?[] { "ab|cd" };
        yield return new object?[] { "(ab)+" };
        yield return new object?[] { "(ab)" };
        yield return new object?[] { "a+?" };
        yield return new object?[] { "a*?" };
        yield return new object?[] { "a??" };
        yield return new object?[] { "a{2,4}?" };
        yield return new object?[] { "a{0}" };
        yield return new object?[] { "((a))" };
        yield return new object?[] { "(a)(b)" };
        yield return new object?[] { "aabaa" };
        yield return new object?[] { "ab+" };
        yield return new object?[] { "(a+)+" };
        yield return new object?[] { "(ab)+?" };
        yield return new object?[] { "(?:)" };
        yield return new object?[] { "(ab|cd)" };
        yield return new object?[] { "a|(b|c)" };
        yield return new object?[] { "((a))+" };
        yield return new object?[] { "(?:a)+" };
        yield return new object?[] { "()+" };
        yield return new object?[] { "a{007}" };
        yield return new object?[] { "(a)(b)(c)" };
        yield return new object?[] { "(ab){0}" };
        yield return new object?[] { "(a){0}" };
        yield return new object?[] { "x" };
        yield return new object?[] { "\\t" };
        yield return new object?[] { "\\a" };
        yield return new object?[] { "\\e" };
        yield return new object?[] { "\\n" };
        yield return new object?[] { "\\r" };
        yield return new object?[] { "\\f" };
        yield return new object?[] { "\\v" };
        yield return new object?[] { "\\cA" };
        yield return new object?[] { "\\cZ" };
        yield return new object?[] { "\\x41" };
        yield return new object?[] { "\\u0041" };
        yield return new object?[] { "\\0" };
        yield return new object?[] { "\\." };
        yield return new object?[] { "\\\\" };
        yield return new object?[] { "\\n+" };
        yield return new object?[] { "a\\nb" };
        yield return new object?[] { "\\cA\\cZ" };
        yield return new object?[] { "\\08" };
        yield return new object?[] { "." };
        yield return new object?[] { "^" };
        yield return new object?[] { "$" };
        yield return new object?[] { "^a" };
        yield return new object?[] { "a$" };
        yield return new object?[] { "\\b" };
        yield return new object?[] { "\\A" };
        yield return new object?[] { "a.b" };
        yield return new object?[] { "a.*b" };
        yield return new object?[] { ".+" };
        yield return new object?[] { ".?" };
        yield return new object?[] { "(a.b)" };
        yield return new object?[] { "(.)+" };
        yield return new object?[] { "(.)*?" };
        yield return new object?[] { "(.){2}" };
        yield return new object?[] { "(^)+" };
        yield return new object?[] { "(?:.)+" };
        yield return new object?[] { "^$" };
        yield return new object?[] { "$^" };
        yield return new object?[] { "(^a)+" };
        yield return new object?[] { ".." };
        yield return new object?[] { "^*" };
        yield return new object?[] { "^*?" };
        yield return new object?[] { "\\b+" };
        yield return new object?[] { "\\B" };
        yield return new object?[] { "\\G" };
        yield return new object?[] { "\\Z" };
        yield return new object?[] { "\\z" };
        yield return new object?[] { "\\bhi\\B" };
        yield return new object?[] { "(^a)" };
        yield return new object?[] { "a^b" };
        yield return new object?[] { "a$b" };
        yield return new object?[] { "^a|b$" };
        yield return new object?[] { "[a]" };
        yield return new object?[] { "[ab]" };
        yield return new object?[] { "[a-z]" };
        yield return new object?[] { "[^a]" };
        yield return new object?[] { "[^ab]" };
        yield return new object?[] { "[a-z0-9_]" };
        yield return new object?[] { "[-a]" };
        yield return new object?[] { "[a-]" };
        yield return new object?[] { "[a\\-z]" };
        yield return new object?[] { "[\\n]" };
        yield return new object?[] { "[a]+" };
        yield return new object?[] { "[a]{2}" };
        yield return new object?[] { "([ab])+" };
        yield return new object?[] { "[^a]+" };
        yield return new object?[] { "[.]" };
        yield return new object?[] { "[\\b]" };
        yield return new object?[] { "[\\A]" };
        yield return new object?[] { "[a[b]" };
        yield return new object?[] { "[ab-]+" };
        yield return new object?[] { "x[yz]" };
        yield return new object?[] { "[\\0]" };
        yield return new object?[] { "[\\x41]" };
        yield return new object?[] { "[\\cA]" };
        yield return new object?[] { "[é]" };
        yield return new object?[] { "[aé]" };
        yield return new object?[] { "[\\u0041-\\u0043]" };
        yield return new object?[] { "[A-z]" };
        yield return new object?[] { "[\\n-z]" };
        yield return new object?[] { "[aa]" };
        yield return new object?[] { "[ba]" };
        yield return new object?[] { "[a-a]" };
        yield return new object?[] { "[a-c-e]" };
        yield return new object?[] { "[a-b-c]" };
        yield return new object?[] { "[a-b-c-d]" };
        yield return new object?[] { "[-a-b]" };
        yield return new object?[] { "[a-b-]" };
        yield return new object?[] { "[---]" };
        yield return new object?[] { "[\\]]" };
        yield return new object?[] { "[a\\]]" };
        yield return new object?[] { "[--]" };
        yield return new object?[] { "[--0]" };
        yield return new object?[] { "[\\--0]" };
        yield return new object?[] { "[-]" };
        yield return new object?[] { "[ab-]" };
        yield return new object?[] { "[!-/]" };
        yield return new object?[] { "[ -~]" };
        yield return new object?[] { "[]a]" };
        yield return new object?[] { "[^]a]" };
        yield return new object?[] { "[(]" };
        yield return new object?[] { "[)]" };
        yield return new object?[] { "[a^b]" };
        yield return new object?[] { "[(a)]" };
        yield return new object?[] { "(?m:^a)" };
        yield return new object?[] { "(?s:.)" };
        yield return new object?[] { "(?x:a)" };
        yield return new object?[] { "(?-m:a)" };
        yield return new object?[] { "(?m-:a)" };
        yield return new object?[] { "(?i-i:a)" };
        yield return new object?[] { "(?s-i:.)" };
        yield return new object?[] { "(?m:^$)" };
        yield return new object?[] { "(?i:^$)" };
        yield return new object?[] { "(?i:^)" };
        yield return new object?[] { "(?i:.)" };
        yield return new object?[] { "(?i:.+)" };
        yield return new object?[] { "(?i:\\b)" };
        yield return new object?[] { "(?i:)" };
        yield return new object?[] { "(?m:(a))" };
        yield return new object?[] { "(?m:a+)" };
        yield return new object?[] { "(?s:.+)" };
        yield return new object?[] { "(?m:^a+)" };
        yield return new object?[] { "(?i:(.+))" };
        yield return new object?[] { "(?i:(?m:^))" };
        yield return new object?[] { "(?#c)a" };
        yield return new object?[] { "(?#c)" };
        yield return new object?[] { "(?#a)b" };
        yield return new object?[] { "é" };
        yield return new object?[] { "aéb" };
        yield return new object?[] { "(a)?" };
        yield return new object?[] { "(a)*" };
        yield return new object?[] { "(a)+" };
        yield return new object?[] { "(a)*?" };
        yield return new object?[] { "(a){2}" };
    }

    [Theory]
    [MemberData(nameof(AcceptCases))]
    public void OwnedParseMatchesVendoredTree(string pattern)
    {
        var expected = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(expected);
        var actual = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(actual);
        Assert.Equal(expected.CaptureCount, actual.CaptureCount);
        Assert.Equal(expected.CaptureNames ?? [], actual.CaptureNames ?? []);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Culture, actual.Culture);
        Assert.True(TreesEqual(expected.Root, actual.Root));
    }

    public static IEnumerable<object?[]> DeclineCases()
    {

        yield return new object?[] { "a{,3}" };
        yield return new object?[] { "a{2,1}" };
        yield return new object?[] { "a**" };
        yield return new object?[] { "*a" };
        yield return new object?[] { "(a" };
        yield return new object?[] { "a)" };
        yield return new object?[] { "\\q" };

        yield return new object?[] { "a{10000000000}" };
        yield return new object?[] { "{2}" };
        yield return new object?[] { "\\c " };
        yield return new object?[] { "\\c" };
        yield return new object?[] { "\\x4" };
        yield return new object?[] { "\\u41" };
        yield return new object?[] { "\\8" };
        yield return new object?[] { "\\k<w>" };
        yield return new object?[] { "\\1" };

        yield return new object?[] { "[z-a]" };
        yield return new object?[] { "[b-a]" };
        yield return new object?[] { "[a" };
        yield return new object?[] { "[^]" };
        yield return new object?[] { "[\\q]" };
        yield return new object?[] { "]" };
        yield return new object?[] { "a]" };
        yield return new object?[] { "a}" };
        yield return new object?[] { "*a)" };
        yield return new object?[] { "}a)" };
        yield return new object?[] { "]a)" };
        yield return new object?[] { "(?-:a)" };
        yield return new object?[] { "(?z:a)" };
        yield return new object?[] { "(?I:a)" };
        yield return new object?[] { "(?x:a#b)" };
    }

    public static IEnumerable<object?[]> OptionResidualCases()
    {
        yield return new object?[] { "(?i--m:a)" };
    }

    [Theory]
    [MemberData(nameof(OptionResidualCases))]
    public void OwnedParserDeclinesResidualOptionShapes(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.NotNull(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [MemberData(nameof(DeclineCases))]
    public void OwnedParserDeclinesBeyondSubset(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> OptionAcceptCases()
    {
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.Multiline };
        yield return new object?[] { "$", RegexOptions.CultureInvariant | RegexOptions.Multiline };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.Singleline };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase };
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase };
        yield return new object?[] { "", (RegexOptions)0 };
        yield return new object?[] { "a", (RegexOptions)0 };
        yield return new object?[] { "", RegexOptions.IgnoreCase };
    }

    [Theory]
    [MemberData(nameof(OptionAcceptCases))]
    public void OwnedParseMatchesVendoredTreeWithOptions(string pattern, RegexOptions options)
    {
        var expected = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, options);
        Assert.NotNull(expected);
        var actual = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(actual);
        Assert.Equal(expected.CaptureCount, actual.CaptureCount);
        Assert.Equal(expected.CaptureNames ?? [], actual.CaptureNames ?? []);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Culture, actual.Culture);
        Assert.True(TreesEqual(expected.Root, actual.Root));
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