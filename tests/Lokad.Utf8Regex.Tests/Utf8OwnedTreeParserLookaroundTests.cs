using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser lookaround stage: the BCL-oracled battery pins positive and
// negative lookahead, lookbehind with forced direction options, atomic groups,
// punctuation escapes, and class escape tiers with BCL metadata,
// plus decline pins with construction errors on backreferences inside lookbehind, malformed openers,
// shorthand range ends, and undecodable escapes; captures in or after lookbehind
// serve BCL-exactly with detailed group-value agreement, and only the
// upper-bound shorthand plus single flip-decision residuals still route to the provider.
public sealed class Utf8OwnedTreeParserLookaroundTests
{
    public static IEnumerable<object?[]> LookaroundAcceptCases()
    {
        yield return new object?[] { "(?=a)", 512 };
        yield return new object?[] { "(?!a)", 512 };
        yield return new object?[] { "(?<=a)", 512 };
        yield return new object?[] { "(?<!a)", 512 };
        yield return new object?[] { "(?>a)", 512 };
        yield return new object?[] { "(?=a)b", 512 };
        yield return new object?[] { "a(?=b)", 512 };
        yield return new object?[] { "(?=(a))b", 512 };
        yield return new object?[] { "(?!(a|b))c", 512 };
        yield return new object?[] { "(?>a|b)c", 512 };
        yield return new object?[] { "(?>a)+", 512 };
        yield return new object?[] { "(?=a+)", 512 };
        yield return new object?[] { "(?!a+)", 512 };
        yield return new object?[] { "(?<=a+)b", 512 };
        yield return new object?[] { "(?<!a+)b", 512 };
        yield return new object?[] { "(?=(?<=a)b)c", 512 };
        yield return new object?[] { "((?=a)b)", 512 };
        yield return new object?[] { "(?=(a)(b))c", 512 };
        yield return new object?[] { "(?=a)(?=b)", 512 };
        yield return new object?[] { "(a)(?=b)", 512 };
        yield return new object?[] { "(?<x>(?=a)b)", 512 };
        yield return new object?[] { "(?=(?<x>a)b)\\k<x>", 512 };
        yield return new object?[] { "(?<=\\d)\\w", 512 };
        yield return new object?[] { "(?<!\\s).", 512 };
        yield return new object?[] { "(?>\\d+)", 512 };
        yield return new object?[] { "(?=)", 512 };
        yield return new object?[] { "(?<=)", 512 };
        yield return new object?[] { "(?=(?:a))", 512 };
        yield return new object?[] { "(?:(?=a)b)", 512 };
        yield return new object?[] { "(?=>a)", 512 };
        yield return new object?[] { "(?!>a)", 512 };
        yield return new object?[] { "(?<=(?:a))b", 512 };
        yield return new object?[] { "(?<=(?<=a)b)c", 512 };
        yield return new object?[] { "(?>((a)))", 512 };
        yield return new object?[] { "(?=>(a))", 512 };
        yield return new object?[] { "(?!>)", 512 };
        yield return new object?[] { "(?=a>b)c", 512 };
        yield return new object?[] { "(?>a>b)c", 512 };
        yield return new object?[] { "(a)(?<=b)", 512 };
        yield return new object?[] { "(?<x>a)(?<=b)", 512 };
        yield return new object?[] { "(?=a)(?<x>b)", 512 };
        yield return new object?[] { "(?<x>a)(?=b)", 512 };
        yield return new object?[] { "(?=a)b", 576 };
        yield return new object?[] { "(?<=a)b", 576 };
        yield return new object?[] { "(?>a)b", 576 };
        yield return new object?[] { "(?<=a)b", 768 };
        yield return new object?[] { "(?<=\\d)\\w", 513 };
        yield return new object?[] { "(?=\\d)\\w", 513 };
        yield return new object?[] { "(?>\\d+)", 513 };
        yield return new object?[] { "(?=>)", 512 };
        yield return new object?[] { "(?=>a)(b)", 512 };
        yield return new object?[] { "(?=a)b(?<x>c)", 512 };
        yield return new object?[] { "(?>a)b(?<x>c)", 512 };
        yield return new object?[] { "\\,", 512 };
        yield return new object?[] { "\\:", 512 };
        yield return new object?[] { "\\;", 512 };
        yield return new object?[] { "\\-", 512 };
        yield return new object?[] { "\\/", 512 };
        yield return new object?[] { "\\#", 512 };
        yield return new object?[] { "\\=", 512 };
        yield return new object?[] { "\\!", 512 };
        yield return new object?[] { "\\%", 512 };
        yield return new object?[] { "\\&", 512 };
        yield return new object?[] { "\\'", 512 };
        yield return new object?[] { "\\\"", 512 };
        yield return new object?[] { "\\<", 512 };
        yield return new object?[] { "\\>", 512 };
        yield return new object?[] { "\\`", 512 };
        yield return new object?[] { "\\~", 512 };
        yield return new object?[] { "\\@", 512 };
        yield return new object?[] { "\\ ", 512 };
        yield return new object?[] { "a\\,b", 512 };
        yield return new object?[] { "[\\,]", 512 };
        yield return new object?[] { "[\\:]", 512 };
        yield return new object?[] { "[\\>]", 512 };
        yield return new object?[] { "[\\@]", 512 };
        yield return new object?[] { "[\\,-a]", 512 };
        yield return new object?[] { "[\\n-a]", 512 };
        yield return new object?[] { "[\\x41-a]", 512 };
        yield return new object?[] { "[\\cA-a]", 512 };
        yield return new object?[] { "[\\0-a]", 512 };
        yield return new object?[] { "[\\b-a]", 512 };
        yield return new object?[] { "[\\--a]", 512 };
        yield return new object?[] { "[\\d-a]", 512 };
        yield return new object?[] { "[\\d-z]", 512 };
        yield return new object?[] { "[\\w-_]", 512 };
        yield return new object?[] { "[]-a]", 512 };
        yield return new object?[] { "(?<=a)b", 512 };
        yield return new object?[] { "((?<=a)b)", 512 };
        yield return new object?[] { "(?<=a{2})b", 512 };
        yield return new object?[] { "(?<=a*)b", 512 };
        yield return new object?[] { "(?<=ab+)c", 512 };
        yield return new object?[] { "a>(?<=b)c", 512 };
        yield return new object?[] { "(?<=(?i:a))b", 512 };
        yield return new object?[] { "(?<=ab|cd)e", 512 };
        yield return new object?[] { "(?<=(?:a)(?:b))c", 512 };
        yield return new object?[] { "(?<=a|b|c)d", 512 };
        yield return new object?[] { "(?<=(?i:a)(?m:b))c", 512 };
        yield return new object?[] { "(?<=a?)b", 512 };
        yield return new object?[] { "(?<=(?:a)+)b", 512 };
        yield return new object?[] { "(?<=^a)b", 512 };
        yield return new object?[] { "(?<=a$)b", 512 };
        yield return new object?[] { "(?<=\\ba)b", 512 };
    }

    public static IEnumerable<object?[]> LookaroundClassAnchorDeclineCases()
    {
        // Anchor escapes are BCL-invalid inside a class (UnrecognizedEscape);
        // the owned parser rejects them with the same error as the oracle.
        yield return new object?[] { "[\\A-a]", 512, RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "[\\B]", 512, RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "[\\G]", 512, RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "[\\Z]", 512, RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "[\\z]", 512, RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "[\\B-a]", 512, RegexParseError.UnrecognizedEscape, 3 };
    }

    [Theory]
    [MemberData(nameof(LookaroundClassAnchorDeclineCases))]
    public void OwnedLookaroundDeclinesClassAnchorEscapes(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }
    public static IEnumerable<object?[]> LookaroundEscapeDeclineCases()
    {
        // Bare word-character escapes and reversed escaped-upper-bound ranges
        // are invalid; owned construction reports the pinned error.
        yield return new object?[] { "\\_", 512, RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "[\\_]", 512, RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "\\é", 512, RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "[a-\\,]", 512, RegexParseError.ReversedCharacterRange, 5 };
        yield return new object?[] { "[a-\\:]", 512, RegexParseError.ReversedCharacterRange, 5 };
        yield return new object?[] { "[a-\\.]", 512, RegexParseError.ReversedCharacterRange, 5 };
        yield return new object?[] { "[a-\\+]", 512, RegexParseError.ReversedCharacterRange, 5 };
    }

    [Theory]
    [MemberData(nameof(LookaroundEscapeDeclineCases))]
    public void OwnedLookaroundDeclinesInvalidEscape(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }
    public static IEnumerable<object?[]> LookaroundDeclineCases()
    {
        yield return new object?[] { "(?>a", RegexParseError.InsufficientClosingParentheses, 4 };
        yield return new object?[] { "(?=a", RegexParseError.InsufficientClosingParentheses, 4 };
        yield return new object?[] { "(?<=a", RegexParseError.InsufficientClosingParentheses, 5 };
        yield return new object?[] { "(?=a))", RegexParseError.InsufficientOpeningParentheses, 6 };
        yield return new object?[] { "(?<=\\1)a", RegexParseError.UndefinedNumberedReference, 6 };
        yield return new object?[] { "[a-\\k]", RegexParseError.UnrecognizedEscape, 5 };
        yield return new object?[] { "[a-\\8]", RegexParseError.UnrecognizedEscape, 5 };
        yield return new object?[] { "[a-\\p]", RegexParseError.InvalidUnicodePropertyEscape, 5 };
        yield return new object?[] { "[a-\\q]", RegexParseError.UnrecognizedEscape, 5 };
        yield return new object?[] { "[a-\\--]", RegexParseError.ReversedCharacterRange, 5 };
        yield return new object?[] { "[\\8]", RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "[\\8-a]", RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "[\\C]", RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "[\\E]", RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "\\C", RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "\\E", RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "\\Q", RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "(?<=(?<1a>b))c", RegexParseError.CaptureGroupNameInvalid, 8 };
    }

    [Theory]
    [MemberData(nameof(LookaroundAcceptCases))]
    public void OwnedLookaroundServesWithBclMetadata(string pattern, int options)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)options, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
    }

    [Theory]
    [MemberData(nameof(LookaroundDeclineCases))]
    public void OwnedLookaroundDeclinesUnsupported(string pattern, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

    public static IEnumerable<object?[]> LookaroundNarrowingCases()
    {
        // Fork-narrowing debt: the fork rejects a lookbehind followed by a group
        // with InvalidPattern while BCL accepts it. The owned parser serves these with
        // BCL-agreeing behavior, machine-verified row by row below.
        yield return new object?[] { "(?<=.)([ab])", 512 };
        yield return new object?[] { "(?<=.)|([ab])", 512 };
        yield return new object?[] { "(?<=.)(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=.)|(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=.)(^)?", 512 };
        yield return new object?[] { "(?<=.)|(^)?", 512 };
        yield return new object?[] { "(?<=a)*([ab])", 512 };
        yield return new object?[] { "(?<=a)*|([ab])", 512 };
        yield return new object?[] { "(?<=a)*(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=a)*|(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=a)*(^)?", 512 };
        yield return new object?[] { "(?<=a)*|(^)?", 512 };
        yield return new object?[] { "(?<=\\d)?([ab])", 512 };
        yield return new object?[] { "(?<=\\d)?|([ab])", 512 };
        yield return new object?[] { "(?<=\\d)?(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=\\d)?|(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=\\d)?(^)?", 512 };
        yield return new object?[] { "(?<=\\d)?|(^)?", 512 };
        yield return new object?[] { "(?<=[ab]){1,2}([ab])", 512 };
        yield return new object?[] { "(?<=[ab]){1,2}|([ab])", 512 };
        yield return new object?[] { "(?<=[ab]){1,2}(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=[ab]){1,2}|(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=[ab]){1,2}(^)?", 512 };
        yield return new object?[] { "(?<=[ab]){1,2}|(^)?", 512 };
        yield return new object?[] { "(?<=.)+?([ab])", 512 };
        yield return new object?[] { "(?<=.)+?|([ab])", 512 };
        yield return new object?[] { "(?<=.)+?(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=.)+?|(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=.)+?(^)?", 512 };
        yield return new object?[] { "(?<=.)+?|(^)?", 512 };
        yield return new object?[] { "(?<=$)+([ab])", 512 };
        yield return new object?[] { "(?<=$)+|([ab])", 512 };
        yield return new object?[] { "(?<=$)+(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=$)+|(\\d){1,2}", 512 };
        yield return new object?[] { "(?<=$)+(^)?", 512 };
        yield return new object?[] { "(?<=$)+|(^)?", 512 };
    }

    [Theory]
    [MemberData(nameof(LookaroundNarrowingCases))]
    public void OwnedLookaroundServesForkNarrowedShapes(string pattern, int options)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new System.Text.RegularExpressions.Regex(pattern, (RegexOptions)options, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        foreach (var input in new[] { "", "a", "ab", "aab", "xaab", "b", "aaab", "abb", "aabb", "1", "11", "a1" })
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var expected = oracle.Match(input);
            var match = product.Match(bytes);
            Assert.Equal(expected.Success, match.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Index, match.IndexInUtf16);
                Assert.Equal(expected.Length, match.LengthInUtf16);
            }

            Assert.Equal(oracle.Count(input), product.Count(bytes));
        }
    }

    public static IEnumerable<object?[]> LookbehindCaptureNarrowingCases()
    {
        // Fork-narrowing debt: the fork rejects captures inside lookbehind while
        // BCL serves them with left-to-right numbering and populated values. The
        // owned parser serves these; group values are pinned per group below.
        yield return new object?[] { "(?<=(a))b", 512, new string[] { "ab", "b", "a", "aab" } };
        yield return new object?[] { "(?<=(a)(b))c", 512, new string[] { "abc", "ac", "ab" } };
        yield return new object?[] { "(?<=(a|b))c", 512, new string[] { "ac", "bc", "c" } };
        yield return new object?[] { "(?<=((a)))b", 512, new string[] { "ab", "b", "a" } };
        yield return new object?[] { "(?<=(?:(a)))b", 512, new string[] { "ab", "b", "a" } };
        yield return new object?[] { "(?<=(a))", 512, new string[] { "a", "", "b" } };
        yield return new object?[] { "(?<=((a)(b)))c", 512, new string[] { "abc", "ac" } };
    }

    [Theory]
    [MemberData(nameof(LookbehindCaptureNarrowingCases))]
    public void OwnedLookbehindServesInnerCaptures(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new System.Text.RegularExpressions.Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNames(), product.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), product.GetGroupNumbers());
        foreach (var inputText in inputs)
        {
            AssertDetailedAgreement(oracle, product, inputText);
        }
    }

    public static IEnumerable<object?[]> LookbehindNamedBehaviorCases()
    {
        // Graduated lookbehind shapes the fork serves with corrupt name tables
        // (phantom names) while BCL agrees with the owned numbering: named groups
        // in or after lookbehind, numeric groups after lookbehind, greater-than
        // literals, and nested assertions. They pin owned-accept plus fork-serves
        // plus BCL metadata and detailed behavior instead of tree equality.
        yield return new object?[] { "(?<=(?<x>a))b", 512, new string[] { "ab", "b", "a" } };
        yield return new object?[] { "(?<=a|(?<x>b))c", 512, new string[] { "ac", "bc", "c" } };
        yield return new object?[] { "(?<=(?<x>(a))b)c", 512, new string[] { "abc", "ac" } };
        yield return new object?[] { "(?<=a)b(?<x>c)", 512, new string[] { "abc", "ab" } };
        yield return new object?[] { "(?<x>a)(?<=b)(?<y>c)", 512, new string[] { "abc", "bc" } };
        yield return new object?[] { "(?<=b)(?<x>a)", 512, new string[] { "ba", "a", "b" } };
        yield return new object?[] { "(?<=(?<1>a))b", 512, new string[] { "ab", "b" } };
        yield return new object?[] { "(?<!>)", 512, new string[] { "a", ">", "" } };
        yield return new object?[] { "(?<!>a)b", 512, new string[] { "ab", "b" } };
        yield return new object?[] { "(?<=a>b)c", 512, new string[] { "a>bc", "abc" } };
        yield return new object?[] { "(?<=(?>a))b", 512, new string[] { "ab", "b" } };
        yield return new object?[] { "(?<=(?=>a)b)c", 512, new string[] { "abc", "ac" } };
        yield return new object?[] { "(?<=(?<-0>a)b)", 512, new string[] { "ab", "b" } };
        yield return new object?[] { "(?<=a)b[>]", 512, new string[] { "ab>", "ab" } };
        yield return new object?[] { "(?<=a)b(?#>)c", 512, new string[] { "abc", "ab" } };
        yield return new object?[] { "(?<=.)(?<1>.)+?", 512, new string[] { "abc", "ab", "a" } };
        yield return new object?[] { "(?<=.)(?<1>\\d){2}", 512, new string[] { "a12", "12", "a1" } };
        yield return new object?[] { "(?<=.)(?<1>^)+", 512, new string[] { "abc", "a" } };
        yield return new object?[] { "(?<=.)+?(?<1>.)+?", 512, new string[] { "abc", "ab", "a" } };
        yield return new object?[] { "(?<=.)+?(?<1>\\d){2}", 512, new string[] { "a12", "12", "a1" } };
        yield return new object?[] { "(?<=.)+?(?<1>^)+", 512, new string[] { "abc", "a" } };
        yield return new object?[] { "(?<=.)+?|(?<1>.)+?", 512, new string[] { "abc", "ab", "a" } };
        yield return new object?[] { "(?<=.)+?|(?<1>\\d){2}", 512, new string[] { "a12", "12", "a1" } };
        yield return new object?[] { "(?<=.)+?|(?<1>^)+", 512, new string[] { "abc", "a" } };
        yield return new object?[] { "(?<=.)|(?<1>.)+?", 512, new string[] { "abc", "ab", "a" } };
        yield return new object?[] { "(?<=.)|(?<1>\\d){2}", 512, new string[] { "a12", "12", "a1" } };
        yield return new object?[] { "(?<=.)|(?<1>^)+", 512, new string[] { "abc", "a" } };
        yield return new object?[] { "(?<=[ab]){1,2}(?<1>.)+?", 512, new string[] { "aab", "ab", "a" } };
        yield return new object?[] { "(?<=[ab]){1,2}(?<1>\\d){2}", 512, new string[] { "a12", "12" } };
        yield return new object?[] { "(?<=[ab]){1,2}(?<1>^)+", 512, new string[] { "aab", "ab" } };
        yield return new object?[] { "(?<=[ab]){1,2}|(?<1>.)+?", 512, new string[] { "aab", "ab", "a" } };
        yield return new object?[] { "(?<=[ab]){1,2}|(?<1>\\d){2}", 512, new string[] { "a12", "12" } };
        yield return new object?[] { "(?<=[ab]){1,2}|(?<1>^)+", 512, new string[] { "aab", "ab" } };
        yield return new object?[] { "(?<=\\d)?(?<1>.)+?", 512, new string[] { "12", "1" } };
        yield return new object?[] { "(?<=\\d)?(?<1>\\d){2}", 512, new string[] { "12", "1" } };
        yield return new object?[] { "(?<=\\d)?(?<1>^)+", 512, new string[] { "12", "1" } };
        yield return new object?[] { "(?<=\\d)?|(?<1>.)+?", 512, new string[] { "12", "1" } };
        yield return new object?[] { "(?<=\\d)?|(?<1>\\d){2}", 512, new string[] { "12", "1" } };
        yield return new object?[] { "(?<=\\d)?|(?<1>^)+", 512, new string[] { "12", "1" } };
        yield return new object?[] { "(?<=$)+(?<1>.)+?", 512, new string[] { "a", "ab" } };
        yield return new object?[] { "(?<=$)+(?<1>\\d){2}", 512, new string[] { "a12", "12" } };
        yield return new object?[] { "(?<=$)+(?<1>^)+", 512, new string[] { "a", "ab" } };
        yield return new object?[] { "(?<=$)+|(?<1>.)+?", 512, new string[] { "a", "ab" } };
        yield return new object?[] { "(?<=$)+|(?<1>\\d){2}", 512, new string[] { "a12", "12" } };
        yield return new object?[] { "(?<=$)+|(?<1>^)+", 512, new string[] { "a", "ab" } };
        yield return new object?[] { "(?<=a)*(?<1>.)+?", 512, new string[] { "aab", "ab" } };
        yield return new object?[] { "(?<=a)*(?<1>\\d){2}", 512, new string[] { "aa12", "a12" } };
        yield return new object?[] { "(?<=a)*(?<1>^)+", 512, new string[] { "aab", "ab" } };
        yield return new object?[] { "(?<=a)*|(?<1>.)+?", 512, new string[] { "aab", "ab" } };
        yield return new object?[] { "(?<=a)*|(?<1>\\d){2}", 512, new string[] { "aa12", "a12" } };
        yield return new object?[] { "(?<=a)*|(?<1>^)+", 512, new string[] { "aab", "ab" } };
    }

    [Theory]
    [MemberData(nameof(LookbehindNamedBehaviorCases))]
    public void OwnedLookbehindServesNamedShapes(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new System.Text.RegularExpressions.Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNames(), product.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), product.GetGroupNumbers());
        foreach (var inputText in inputs)
        {
            AssertDetailedAgreement(oracle, product, inputText);
        }
    }

    private static void AssertDetailedAgreement(System.Text.RegularExpressions.Regex oracle, Utf8Regex product, string inputText)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        var expected = oracle.Match(inputText);
        var match = product.Match(input);
        Assert.Equal(expected.Success, match.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, match.IndexInUtf16);
            Assert.Equal(expected.Length, match.LengthInUtf16);
            var actualDetailed = product.MatchDetailed(input);
            for (var g = 0; g < expected.Groups.Count; g++)
            {
                var expectedGroup = expected.Groups[g];
                var actualGroup = actualDetailed.GetGroup(g);
                Assert.Equal(expectedGroup.Success, actualGroup.Success);
                if (expectedGroup.Success)
                {
                    Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
                    Assert.Equal(expectedGroup.Index, actualGroup.IndexInUtf16);
                    Assert.Equal(expectedGroup.Length, actualGroup.LengthInUtf16);
                }
            }
        }

        Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
    }
    public static IEnumerable<object?[]> EscapedGreaterThanCases()
    {
        // Escaped greater-thans are ordinary literals BCL serves, while the fork
        // corrupts every post-opener greater-than shape: no fork assertion here,
        // only owned-accept plus BCL-constructs plus detailed product agreement.
        yield return new object?[] { "(?<=a)b\\>", 512, new string[] { "a>b", "ab", "a>", ">", "b", "xa>by" } };
    }

    [Theory]
    [MemberData(nameof(EscapedGreaterThanCases))]
    public void OwnedServesEscapedGreaterThan(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new System.Text.RegularExpressions.Regex(pattern, (RegexOptions)options, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        foreach (var input in inputs)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var expected = oracle.Match(input);
            var match = product.Match(bytes);
            Assert.Equal(expected.Success, match.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Index, match.IndexInUtf16);
                Assert.Equal(expected.Length, match.LengthInUtf16);
            }

            Assert.Equal(oracle.Count(input), product.Count(bytes));
        }
    }

}
