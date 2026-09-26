using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser IgnoreCase stage: the differential battery pins case folding
// identical to the provider trees (single letters fold to two-pair sets from
// the owned fold table, classes append per-unit expansions, shorthands and
// categories pass through, groups and backreferences thread options, tree
// contributions come from leading constructs only), scoped shapes verified by
// metadata plus detailed behavior instead of tree equality, decline agreement on
// reversed ranges and astral escapes, and three folding-compound fork artifacts
// that vanish at the flip (the fork over-matches; the flip routes fallback BCL-exact).
public sealed class Utf8OwnedTreeParserIgnoreCaseTests
{
    public static IEnumerable<object?[]> IgnoreCaseAcceptCases()
    {
        yield return new object?[] { "a", 513 };
        yield return new object?[] { "a", 1 };
        yield return new object?[] { "a", 0 };
        yield return new object?[] { "A", 513 };
        yield return new object?[] { "z", 513 };
        yield return new object?[] { "k", 513 };
        yield return new object?[] { "K", 513 };
        yield return new object?[] { "S", 513 };
        yield return new object?[] { "s", 513 };
        yield return new object?[] { "ab", 513 };
        yield return new object?[] { "a1", 513 };
        yield return new object?[] { "1", 513 };
        yield return new object?[] { "1+", 513 };
        yield return new object?[] { "a", 577 };
        yield return new object?[] { "\\x41", 513 };
        yield return new object?[] { "\\cA", 513 };
        yield return new object?[] { "\\0", 513 };
        yield return new object?[] { "\\u00E9", 513 };
        yield return new object?[] { "\\u017F", 513 };
        yield return new object?[] { "\\u212A", 513 };
        yield return new object?[] { "\\u0131", 513 };
        yield return new object?[] { "\\u0130", 513 };
        yield return new object?[] { "[ab]", 513 };
        yield return new object?[] { "[a]", 513 };
        yield return new object?[] { "[a-z]", 513 };
        yield return new object?[] { "[^a]", 513 };
        yield return new object?[] { "[^a-z]", 513 };
        yield return new object?[] { "[A-Z]", 513 };
        yield return new object?[] { "[B-D]", 513 };
        yield return new object?[] { "[0-9]", 513 };
        yield return new object?[] { "[A-C]", 513 };
        yield return new object?[] { "[k-m]", 513 };
        yield return new object?[] { "[K-M]", 513 };
        yield return new object?[] { "[K-k]", 513 };
        yield return new object?[] { "[,-.]", 513 };
        yield return new object?[] { "[ab]", 577 };
        yield return new object?[] { "[\\da]", 513 };
        yield return new object?[] { "[a\\d]", 513 };
        yield return new object?[] { "[\\da-z]", 513 };
        yield return new object?[] { "[\\d]", 513 };
        yield return new object?[] { "[\\D]", 513 };
        yield return new object?[] { "[\\w]", 513 };
        yield return new object?[] { "[\\u00E9]", 513 };
        yield return new object?[] { "[\\u0000-\\u0FFF]", 513 };
        yield return new object?[] { "[a-[b]]", 513 };
        yield return new object?[] { "[abc-[b-d]]", 513 };
        yield return new object?[] { "[^a-[b]]", 513 };
        yield return new object?[] { "[\\d-[a]]", 513 };
        yield return new object?[] { "[\\da-[b]]", 513 };
        yield return new object?[] { "[abc-[b]]", 513 };
        yield return new object?[] { "[a-[b]]", 1 };
        yield return new object?[] { "\\d", 513 };
        yield return new object?[] { "\\w", 513 };
        yield return new object?[] { "\\p{L}", 513 };
        yield return new object?[] { ".", 513 };
        yield return new object?[] { "^", 513 };
        yield return new object?[] { "$", 513 };
        yield return new object?[] { "\\b", 513 };
        yield return new object?[] { "(?i:\\d)", 512 };
        yield return new object?[] { "(?i:a)", 512 };
        yield return new object?[] { "(?i:ab)", 512 };
        yield return new object?[] { "(?i:[ab])", 512 };
        yield return new object?[] { "(?i:a1)", 512 };
        yield return new object?[] { "(?i:12)", 512 };
        yield return new object?[] { "(?i:^)", 512 };
        yield return new object?[] { "(?i:.)", 512 };
        yield return new object?[] { "(?i:$)", 512 };
        yield return new object?[] { "(?i:\\w)", 512 };
        yield return new object?[] { "(?i:\\p{L})", 512 };
        yield return new object?[] { "(?i:[a-z])", 512 };
        yield return new object?[] { "(?i:[^a])", 512 };
        yield return new object?[] { "(?i:[a-[b]])", 512 };
        yield return new object?[] { "(?i:)", 512 };
        yield return new object?[] { "(?i:\\\\)", 512 };
        yield return new object?[] { "(?i:1)", 512 };
        yield return new object?[] { "(?i:a|b)", 512 };
        yield return new object?[] { "(?i:ab|cd)", 512 };
        yield return new object?[] { "(?i:\\x41)", 512 };
        yield return new object?[] { "(?i:K)", 512 };
        yield return new object?[] { "(?i:AB)", 512 };
        yield return new object?[] { "(?i:(a))", 512 };
        yield return new object?[] { "(?i:(?m:a))", 512 };
        yield return new object?[] { "(?ii:a)", 512 };
        yield return new object?[] { "(?mi-s:a)", 512 };
        yield return new object?[] { "(?is-m:a)", 512 };
        yield return new object?[] { "(?i-m:a)", 512 };
        yield return new object?[] { "(?im-s:a)", 512 };
        yield return new object?[] { "(?i:a)b", 513 };
        yield return new object?[] { "(?i:k)K", 513 };
        yield return new object?[] { "(?i:a)", 576 };
        yield return new object?[] { "(?<x>a)", 513 };
        yield return new object?[] { "(?<x>1)", 513 };
        yield return new object?[] { "(1)\\1", 513 };
        yield return new object?[] { "(a)\\1", 513 };
        yield return new object?[] { "(a\\1(?i:b))", 512 };
        yield return new object?[] { "(?<x>(?i:a))", 512 };
        yield return new object?[] { "(?<x>a)\\k<x>", 513 };
        yield return new object?[] { "(?<9>a)(b)", 513 };
        yield return new object?[] { "\\01", 513 };
        yield return new object?[] { "\\10", 513 };
        yield return new object?[] { "\\101", 513 };
        yield return new object?[] { "\\10{2}", 513 };
        yield return new object?[] { "\\101{2}", 512 };
        yield return new object?[] { "\\101{2}", 513 };
        yield return new object?[] { "\\x41{2}", 512 };
        yield return new object?[] { "\\x41{2}", 513 };
        yield return new object?[] { "a{0}", 513 };
        yield return new object?[] { "(a){0}", 513 };
        yield return new object?[] { "(a){0}", 512 };
        yield return new object?[] { "(?i:(a){0})", 512 };
        yield return new object?[] { "(?-i:a)b", 512 };
        yield return new object?[] { "(?m:a)", 512 };
        yield return new object?[] { "((?m:a))", 512 };
        yield return new object?[] { "cd(?m:ab)", 512 };
        yield return new object?[] { "a(?m:b)", 512 };
        yield return new object?[] { "(?:a)(?i:b)c", 512 };
        yield return new object?[] { "a(?i:b)(?m:c)", 512 };
        yield return new object?[] { "(?i:(?=a)b)", 512 };
        yield return new object?[] { "(?i:(?<=a)b)", 512 };
    }

    public static IEnumerable<object?[]> IgnoreCaseDeclineCases()
    {
        yield return new object?[] { "(?ix-a:b)", 512 };
        yield return new object?[] { "[a-Z]", 513 };
        yield return new object?[] { "(?i:\\U00010400)", 512 };
    }

    public static IEnumerable<object?[]> IgnoreCaseResidualCases()
    {
        yield return new object?[] { "(?i:[ab]){2}|(?:a.)", 512 };
        yield return new object?[] { "(?m:\\d){2}|(?:a.)", 512 };
        yield return new object?[] { "(?n:a){2}|(?:a.)", 512 };
    }

    public static IEnumerable<object?[]> IgnoreCaseScopeBehaviorCases()
    {
        // Graduated scope shapes the fork serves with divergent option marking
        // while BCL agrees with the owned per-node scoping: trailing content past
        // i/m/n-scopes, toggled scopes, quantified scopes, scoped dots, and
        // scope/capture interplay. They pin owned-accept plus fork-serves plus BCL
        // metadata and detailed behavior instead of tree equality.
        yield return new object?[] { "(?i:k)K", 512, new string[] { "kK", "KK", "k" } };
        yield return new object?[] { "(?i:a)+", 512, new string[] { "aa", "AA", "a" } };
        yield return new object?[] { "(?i:a)+?", 512, new string[] { "aa", "AA", "a" } };
        yield return new object?[] { "(?i:a)?", 512, new string[] { "a", "A", "" } };
        yield return new object?[] { "(?i:a){0}", 512, new string[] { "", "a" } };
        yield return new object?[] { "(?i:ab){2}", 512, new string[] { "abab", "ABAB" } };
        yield return new object?[] { "(?i:.){1,2}", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?i:.){2}", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?i:ab)cd", 512, new string[] { "abcd", "ABCD", "ABcd" } };
        yield return new object?[] { "(?i:a)(?-i:b)", 512, new string[] { "ab", "AB", "Ab" } };
        yield return new object?[] { "(?i:a)b(?i:c)", 512, new string[] { "abc", "ABC", "AbC" } };
        yield return new object?[] { "(?i:a)|b", 512, new string[] { "a", "A", "b", "c" } };
        yield return new object?[] { "(?i:a)+b", 512, new string[] { "aab", "AAB", "ab" } };
        yield return new object?[] { "(?i-m:a)b", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?m-i:a)b", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?m:a)b", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?s:a)b", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?i:a)(?m:b)", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?i:a)(?m:b)c", 512, new string[] { "abc", "ABC" } };
        yield return new object?[] { "(?i:a)(b(?m)c)", 512, new string[] { "abc", "ABC" } };
        yield return new object?[] { "(?n:a)b", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?m:a)$", 512, new string[] { "a", "A", "ab" } };
        yield return new object?[] { "(?s:a).", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?m:a)+", 512, new string[] { "aa", "AA", "a" } };
        yield return new object?[] { "(?s:a)+", 512, new string[] { "aa", "AA" } };
        yield return new object?[] { "(?n:a)(b)", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?m:a)(b)", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?s:a)(b)", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?i:a)(b)", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?x:a)b", 512, new string[] { "ab", "a b" } };
        yield return new object?[] { "(?x:a)+", 512, new string[] { "aa", "a a" } };
        yield return new object?[] { "(?x:a)(b)", 512, new string[] { "ab", "a b" } };
        yield return new object?[] { "(?m:)+", 512, new string[] { "", "a" } };
        yield return new object?[] { "(?i:a)$", 512, new string[] { "a", "A" } };
        yield return new object?[] { "(?i:a).", 512, new string[] { "ab", "AB" } };
        yield return new object?[] { "(?n:a).", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?m:.){1,2}", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?m:.){2}", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?n:.){1,2}", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?n:.){2}", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?i:[ab]){2}(?m:[ab]){2}", 512, new string[] { "abab", "ABAB" } };
        yield return new object?[] { "(?i:[ab]){2}([ab])", 512, new string[] { "aba", "ABA", "abb" } };
        yield return new object?[] { "(?i:[ab]){2}|([ab])", 512, new string[] { "ab", "a", "AB" } };
        yield return new object?[] { "(?i:[ab]){2}|a", 512, new string[] { "ab", "a", "A" } };
        yield return new object?[] { "(?i:[ab]){2}|.{1,2}", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?i:[ab]){2}(?n:.){1,2}", 512, new string[] { "abab", "aba" } };
        yield return new object?[] { "(?i:[ab]){2}|(?n:.){1,2}", 512, new string[] { "ab", "a" } };
        yield return new object?[] { "(?i:[ab]){2}.{1,2}", 512, new string[] { "abab", "aba" } };
        yield return new object?[] { "(?m:\\d){2}|.{1,2}", 512, new string[] { "12", "ab", "1" } };
        yield return new object?[] { "(?m:\\d){2}|(?n:.){1,2}", 512, new string[] { "12", "ab" } };
        yield return new object?[] { "(?n:a){2}|.{1,2}", 512, new string[] { "aa", "ab" } };
        yield return new object?[] { "(?n:a){2}|(?n:.){1,2}", 512, new string[] { "aa", "ab" } };
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseScopeBehaviorCases))]
    public void OwnedIgnoreCaseServesScopeShapes(string pattern, int options, string[] inputs)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.NotNull(Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options));
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNames(), product.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), product.GetGroupNumbers());
        foreach (var inputText in inputs)
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
    }
    [Theory]
    [MemberData(nameof(IgnoreCaseAcceptCases))]
    public void OwnedIgnoreCaseMatchesProviderTree(string pattern, int options)
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
    [MemberData(nameof(IgnoreCaseDeclineCases))]
    public void OwnedIgnoreCaseDeclinesUnsupported(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            (RegexOptions)options,
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options)));
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