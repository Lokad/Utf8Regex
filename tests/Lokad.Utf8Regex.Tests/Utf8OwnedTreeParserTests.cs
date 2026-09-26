using System.Text.RegularExpressions;
using System.Globalization;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser battery, BCL-oracled: the owned tree parser serves the
// literal/escape/concatenation/alternation/group/quantifier/dot/anchor/class/
// scoped-option/comment subset with capture metadata matching the .NET 10
// oracle (group numbers/names, construction verdicts with error/offset pins),
// and declines everything else. Fork-tree equality scaffolding was removed at
// the cutover; representation checks plus BCL behavioral expectations remain.
// Parent links, source positions, and find optimizations are construction
// details outside the comparison.
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
        yield return new object?[] { "[\\a]" };
        yield return new object?[] { "[\\e]" };
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

    public static IEnumerable<object?[]> ScopedAcceptCases()
    {
        yield return new object?[] { "(?m:^a)", 514 };
        yield return new object?[] { "(?s:.)", 528 };
        yield return new object?[] { "(?x:a)", 544 };
        yield return new object?[] { "(?-m:a)", 512 };
        yield return new object?[] { "(?m-:a)", 514 };
        yield return new object?[] { "(?i-i:a)", 512 };
        yield return new object?[] { "(?s-i:.)", 528 };
        yield return new object?[] { "(?m:^$)", 514 };
        yield return new object?[] { "(?i:^$)", 513 };
        yield return new object?[] { "(?i:^)", 513 };
        yield return new object?[] { "(?i:.)", 513 };
        yield return new object?[] { "(?i:.+)", 513 };
        yield return new object?[] { "(?i:\\b)", 513 };
        yield return new object?[] { "(?i:)", 513 };
        yield return new object?[] { "(?m:(a))", 514 };
        yield return new object?[] { "(?m:a+)", 514 };
        yield return new object?[] { "(?s:.+)", 528 };
        yield return new object?[] { "(?m:^a+)", 514 };
        yield return new object?[] { "(?i:(.+))", 513 };
        yield return new object?[] { "(?i:(?m:^))", 513 };
    }

    [Theory]
    [MemberData(nameof(AcceptCases))]
    public void OwnedParseServesWithBclMetadata(string pattern)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal(RegexOptions.CultureInvariant, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
    }

    [Theory]
    [MemberData(nameof(ScopedAcceptCases))]
    public void OwnedScopedOptionsFoldIntoTree(string pattern, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
    }

    public static IEnumerable<object?[]> DeclineCases()
    {
        yield return new object?[] { "a{2,1}", RegexParseError.ReversedQuantifierRange, 6 };
        yield return new object?[] { "a**", RegexParseError.NestedQuantifiersNotParenthesized, 3 };
        yield return new object?[] { "*a", RegexParseError.QuantifierAfterNothing, 1 };
        yield return new object?[] { "(a", RegexParseError.InsufficientClosingParentheses, 2 };
        yield return new object?[] { "a)", RegexParseError.InsufficientOpeningParentheses, 2 };
        yield return new object?[] { "\\q", RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "a{10000000000}", RegexParseError.QuantifierOrCaptureGroupOutOfRange, 13 };
        yield return new object?[] { "{2}", RegexParseError.QuantifierAfterNothing, 1 };
        yield return new object?[] { "\\c ", RegexParseError.UnrecognizedControlCharacter, 3 };
        yield return new object?[] { "\\c", RegexParseError.MissingControlCharacter, 2 };
        yield return new object?[] { "\\x4", RegexParseError.InsufficientOrInvalidHexDigits, 2 };
        yield return new object?[] { "\\u41", RegexParseError.InsufficientOrInvalidHexDigits, 2 };
        yield return new object?[] { "\\8", RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "\\k<w>", RegexParseError.UndefinedNamedReference, 5 };
        yield return new object?[] { "\\1", RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "[z-a]", RegexParseError.ReversedCharacterRange, 4 };
        yield return new object?[] { "[b-a]", RegexParseError.ReversedCharacterRange, 4 };
        yield return new object?[] { "[a", RegexParseError.UnterminatedBracket, 2 };
        yield return new object?[] { "[^]", RegexParseError.UnterminatedBracket, 3 };
        yield return new object?[] { "[\\q]", RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "*a)", RegexParseError.QuantifierAfterNothing, 1 };
        yield return new object?[] { "}a)", RegexParseError.InsufficientOpeningParentheses, 3 };
        yield return new object?[] { "]a)", RegexParseError.InsufficientOpeningParentheses, 3 };
        yield return new object?[] { "(?z:a)", RegexParseError.InvalidGroupingConstruct, 3 };
        yield return new object?[] { "(?x:a#b)", RegexParseError.InsufficientClosingParentheses, 8 };
    }

    [Theory]
    [MemberData(nameof(DeclineCases))]
    public void OwnedParserDeclinesInvalidShapes(string pattern, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

    [Fact]
    public void OwnedParserDeclinesButBclServesShorthandGap()
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse("(?I:a)", RegexOptions.CultureInvariant));
        var oracle = new Regex("(?I:a)", RegexOptions.CultureInvariant);
        var product = new Utf8Regex("(?I:a)", RegexOptions.CultureInvariant);
        Assert.Equal(oracle.ToString(), product.Pattern);
    }

    public static IEnumerable<object?[]> ClassAnchorEscapeCases()
    {
        yield return new object?[] { "[\\A]", 3 };
        yield return new object?[] { "[\\B]", 3 };
        yield return new object?[] { "[\\G]", 3 };
        yield return new object?[] { "[\\Z]", 3 };
        yield return new object?[] { "[\\z]", 3 };
        yield return new object?[] { "x[\\A]y", 4 };
    }

    [Theory]
    [MemberData(nameof(ClassAnchorEscapeCases))]
    public void OwnedParserDeclinesClassAnchorEscapes(string pattern, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Assert.Equal(RegexParseError.UnrecognizedEscape, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

    public static IEnumerable<object?[]> OptionAcceptCases()
    {
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.Multiline, 514 };
        yield return new object?[] { "$", RegexOptions.CultureInvariant | RegexOptions.Multiline, 514 };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.Singleline, 528 };
        yield return new object?[] { ".", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 513 };
        yield return new object?[] { "^", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 513 };
        yield return new object?[] { "", (RegexOptions)0, 0 };
        yield return new object?[] { "a", (RegexOptions)0, 0 };
        yield return new object?[] { "", RegexOptions.IgnoreCase, 1 };
    }

    [Theory]
    [MemberData(nameof(OptionAcceptCases))]
    public void OwnedParseServesWithBclMetadataAndOptions(string pattern, RegexOptions options, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal((options & RegexOptions.CultureInvariant) != 0 ? CultureInfo.InvariantCulture.Name : null, actual.Culture?.Name);
    }

}
