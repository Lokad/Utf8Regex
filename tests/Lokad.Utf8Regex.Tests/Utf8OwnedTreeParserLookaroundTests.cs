using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser lookaround stage: the differential battery pins positive and
// negative lookahead, lookbehind with forced direction options, atomic groups,
// punctuation escapes, and class escape tiers identical to the provider trees,
// plus decline agreement on captures and backreferences inside lookbehind,
// malformed openers, shorthand range ends, and undecodable escapes, and the
// named-in-lookbehind plus phantom-> residuals that still route to the provider.
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
        yield return new object?[] { "\\_", 512 };
        yield return new object?[] { "\\ ", 512 };
        yield return new object?[] { "a\\,b", 512 };
        yield return new object?[] { "[\\,]", 512 };
        yield return new object?[] { "[\\:]", 512 };
        yield return new object?[] { "[\\>]", 512 };
        yield return new object?[] { "[\\@]", 512 };
        yield return new object?[] { "[\\_]", 512 };
        yield return new object?[] { "[\\,-a]", 512 };
        yield return new object?[] { "\\é", 512 };
        yield return new object?[] { "[\\n-a]", 512 };
        yield return new object?[] { "[\\x41-a]", 512 };
        yield return new object?[] { "[\\cA-a]", 512 };
        yield return new object?[] { "[\\0-a]", 512 };
        yield return new object?[] { "[\\b-a]", 512 };
        yield return new object?[] { "[\\A-a]", 512 };
        yield return new object?[] { "[\\B]", 512 };
        yield return new object?[] { "[\\G]", 512 };
        yield return new object?[] { "[\\Z]", 512 };
        yield return new object?[] { "[\\z]", 512 };
        yield return new object?[] { "[\\B-a]", 512 };
        yield return new object?[] { "[a-\\,]", 512 };
        yield return new object?[] { "[a-\\:]", 512 };
        yield return new object?[] { "[a-\\.]", 512 };
        yield return new object?[] { "[a-\\+]", 512 };
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

    public static IEnumerable<object?[]> LookaroundDeclineCases()
    {
        yield return new object?[] { "(?>a" };
        yield return new object?[] { "(?=a" };
        yield return new object?[] { "(?<=a" };
        yield return new object?[] { "(?=a))" };
        yield return new object?[] { "(?<=(a))b" };
        yield return new object?[] { "(?<=(a)(b))c" };
        yield return new object?[] { "(?<=(a|b))c" };
        yield return new object?[] { "(?<=((a)))b" };
        yield return new object?[] { "(?<=\\1)a" };
        yield return new object?[] { "(?<=(?:(a)))b" };
        yield return new object?[] { "[a-\\k]" };
        yield return new object?[] { "[a-\\8]" };
        yield return new object?[] { "[a-\\p]" };
        yield return new object?[] { "[a-\\q]" };
        yield return new object?[] { "[a-\\--]" };
        yield return new object?[] { "[\\8]" };
        yield return new object?[] { "[\\8-a]" };
        yield return new object?[] { "[\\C]" };
        yield return new object?[] { "[\\E]" };
        yield return new object?[] { "\\C" };
        yield return new object?[] { "\\E" };
        yield return new object?[] { "\\Q" };
    }

    public static IEnumerable<object?[]> LookaroundResidualCases()
    {
        // Fork-widening debt (no-vendor rule): BCL rejects upper-bound shorthand escapes
        // while the pristine fork accepts them; the provider gatekeeper still declines them.
        yield return new object?[] { "[a-\\d]", 512 };
        yield return new object?[] { "[a-\\p{L}]", 512 };
        yield return new object?[] { "[--\\d]", 512 };

        yield return new object?[] { "(?<=b)(?<x>a)", 512 };
        yield return new object?[] { "(?<=(?<x>a))b", 512 };
        yield return new object?[] { "(?<=a|(?<x>b))c", 512 };
        yield return new object?[] { "(?<=(?<1a>b))c", 512 };
        yield return new object?[] { "(?<=(?<-0>a)b)", 512 };
        yield return new object?[] { "(?<!>)", 512 };
        yield return new object?[] { "(?<!>a)b", 512 };
        yield return new object?[] { "(?<=a>b)c", 512 };
        yield return new object?[] { "(?<=(?>a))b", 512 };
        yield return new object?[] { "(?<=(?=>a)b)c", 512 };
        yield return new object?[] { "(?<=(?<x>(a))b)c", 512 };
        yield return new object?[] { "(?<=(?<1>a))b", 512 };
        yield return new object?[] { "(?<=a)b[>]", 512 };
        yield return new object?[] { "(?<=a)b\\>", 512 };
        yield return new object?[] { "(?<=a)b(?#>)c", 512 };
        yield return new object?[] { "(?<=a)b(?<x>c)", 512 };
        yield return new object?[] { "(?<x>a)(?<=b)(?<y>c)", 512 };
    }

    [Theory]
    [MemberData(nameof(LookaroundAcceptCases))]
    public void OwnedLookaroundMatchesProviderTree(string pattern, int options)
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
    [MemberData(nameof(LookaroundDeclineCases))]
    public void OwnedLookaroundDeclinesUnsupported(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            RegexOptions.CultureInvariant,
            RuntimeFrontEnd.RegexParser.GetTargetCulture(RegexOptions.CultureInvariant)));
    }

    [Theory]
    [MemberData(nameof(LookaroundResidualCases))]
    public void OwnedLookaroundDeclinesResidual(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.NotNull(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options));
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
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            (RegexOptions)options,
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options)));
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