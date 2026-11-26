using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Check parser metadata, scope, and folded compound behavior on syntax and
// subjects shared with the BCL comparator. Scalar equivalence tests pin the
// owned Unicode data independently of the comparator.
public sealed class Utf8OwnedTreeParserIgnoreCaseTests
{
    public static IEnumerable<object?[]> IgnoreCaseAcceptCases()
    {
        yield return new object?[] { "a", 513, 513 };
        yield return new object?[] { "a", 1, 1 };
        yield return new object?[] { "a", 0, 0 };
        yield return new object?[] { "A", 513, 513 };
        yield return new object?[] { "z", 513, 513 };
        yield return new object?[] { "k", 513, 513 };
        yield return new object?[] { "K", 513, 513 };
        yield return new object?[] { "S", 513, 513 };
        yield return new object?[] { "s", 513, 513 };
        yield return new object?[] { "ab", 513, 513 };
        yield return new object?[] { "a1", 513, 513 };
        yield return new object?[] { "1", 513, 513 };
        yield return new object?[] { "1+", 513, 513 };
        yield return new object?[] { "a", 577, 577 };
        yield return new object?[] { "\\x41", 513, 513 };
        yield return new object?[] { "\\cA", 513, 513 };
        yield return new object?[] { "\\0", 513, 513 };
        yield return new object?[] { "\\u00E9", 513, 513 };
        yield return new object?[] { "\\u017F", 513, 513 };
        yield return new object?[] { "\\u212A", 513, 513 };
        yield return new object?[] { "\\u0131", 513, 513 };
        yield return new object?[] { "\\u0130", 513, 513 };
        yield return new object?[] { "[ab]", 513, 513 };
        yield return new object?[] { "[a]", 513, 513 };
        yield return new object?[] { "[a-z]", 513, 513 };
        yield return new object?[] { "[^a]", 513, 513 };
        yield return new object?[] { "[^a-z]", 513, 513 };
        yield return new object?[] { "[A-Z]", 513, 513 };
        yield return new object?[] { "[B-D]", 513, 513 };
        yield return new object?[] { "[0-9]", 513, 513 };
        yield return new object?[] { "[A-C]", 513, 513 };
        yield return new object?[] { "[k-m]", 513, 513 };
        yield return new object?[] { "[K-M]", 513, 513 };
        yield return new object?[] { "[K-k]", 513, 513 };
        yield return new object?[] { "[,-.]", 513, 513 };
        yield return new object?[] { "[ab]", 577, 577 };
        yield return new object?[] { "[\\da]", 513, 513 };
        yield return new object?[] { "[a\\d]", 513, 513 };
        yield return new object?[] { "[\\da-z]", 513, 513 };
        yield return new object?[] { "[\\d]", 513, 513 };
        yield return new object?[] { "[\\D]", 513, 513 };
        yield return new object?[] { "[\\w]", 513, 513 };
        yield return new object?[] { "[\\u00E9]", 513, 513 };
        yield return new object?[] { "[\\u0000-\\u0FFF]", 513, 513 };
        yield return new object?[] { "[a-[b]]", 513, 513 };
        yield return new object?[] { "[abc-[b-d]]", 513, 513 };
        yield return new object?[] { "[^a-[b]]", 513, 513 };
        yield return new object?[] { "[\\d-[a]]", 513, 513 };
        yield return new object?[] { "[\\da-[b]]", 513, 513 };
        yield return new object?[] { "[abc-[b]]", 513, 513 };
        yield return new object?[] { "[a-[b]]", 1, 1 };
        yield return new object?[] { "\\d", 513, 513 };
        yield return new object?[] { "\\w", 513, 513 };
        yield return new object?[] { "\\p{L}", 513, 513 };
        yield return new object?[] { ".", 513, 513 };
        yield return new object?[] { "^", 513, 513 };
        yield return new object?[] { "$", 513, 513 };
        yield return new object?[] { "\\b", 513, 513 };
        yield return new object?[] { "(?i:\\d)", 512, 513 };
        yield return new object?[] { "(?i:a)", 512, 513 };
        yield return new object?[] { "(?i:ab)", 512, 513 };
        yield return new object?[] { "(?i:[ab])", 512, 513 };
        yield return new object?[] { "(?i:a1)", 512, 513 };
        yield return new object?[] { "(?i:12)", 512, 513 };
        yield return new object?[] { "(?i:^)", 512, 513 };
        yield return new object?[] { "(?i:.)", 512, 513 };
        yield return new object?[] { "(?i:$)", 512, 513 };
        yield return new object?[] { "(?i:\\w)", 512, 513 };
        yield return new object?[] { "(?i:\\p{L})", 512, 513 };
        yield return new object?[] { "(?i:[a-z])", 512, 513 };
        yield return new object?[] { "(?i:[^a])", 512, 513 };
        yield return new object?[] { "(?i:[a-[b]])", 512, 513 };
        yield return new object?[] { "(?i:)", 512, 513 };
        yield return new object?[] { "(?i:\\\\)", 512, 513 };
        yield return new object?[] { "(?i:1)", 512, 513 };
        yield return new object?[] { "(?i:a|b)", 512, 513 };
        yield return new object?[] { "(?i:ab|cd)", 512, 513 };
        yield return new object?[] { "(?i:\\x41)", 512, 513 };
        yield return new object?[] { "(?i:K)", 512, 513 };
        yield return new object?[] { "(?i:AB)", 512, 513 };
        yield return new object?[] { "(?i:(a))", 512, 513 };
        yield return new object?[] { "(?i:(?m:a))", 512, 513 };
        yield return new object?[] { "(?ii:a)", 512, 513 };
        yield return new object?[] { "(?mi-s:a)", 512, 515 };
        yield return new object?[] { "(?is-m:a)", 512, 529 };
        yield return new object?[] { "(?i-m:a)", 512, 513 };
        yield return new object?[] { "(?im-s:a)", 512, 515 };
        yield return new object?[] { "(?i:a)b", 513, 513 };
        yield return new object?[] { "(?i:k)K", 513, 513 };
        yield return new object?[] { "(?i:a)", 576, 577 };
        yield return new object?[] { "(?<x>a)", 513, 513 };
        yield return new object?[] { "(?<x>1)", 513, 513 };
        yield return new object?[] { "(1)\\1", 513, 513 };
        yield return new object?[] { "(a)\\1", 513, 513 };
        yield return new object?[] { "(a\\1(?i:b))", 512, 512 };
        yield return new object?[] { "(?<x>(?i:a))", 512, 512 };
        yield return new object?[] { "(?<x>a)\\k<x>", 513, 513 };
        yield return new object?[] { "(?<9>a)(b)", 513, 513 };
        yield return new object?[] { "\\01", 513, 513 };
        yield return new object?[] { "\\10", 513, 513 };
        yield return new object?[] { "\\101", 513, 513 };
        yield return new object?[] { "\\10{2}", 513, 513 };
        yield return new object?[] { "\\101{2}", 512, 512 };
        yield return new object?[] { "\\101{2}", 513, 513 };
        yield return new object?[] { "\\x41{2}", 512, 512 };
        yield return new object?[] { "\\x41{2}", 513, 513 };
        yield return new object?[] { "a{0}", 513, 513 };
        yield return new object?[] { "(a){0}", 513, 513 };
        yield return new object?[] { "(a){0}", 512, 512 };
        yield return new object?[] { "(?i:(a){0})", 512, 513 };
        yield return new object?[] { "(?-i:a)b", 512, 512 };
        yield return new object?[] { "(?m:a)", 512, 514 };
        yield return new object?[] { "((?m:a))", 512, 512 };
        yield return new object?[] { "cd(?m:ab)", 512, 512 };
        yield return new object?[] { "a(?m:b)", 512, 512 };
        yield return new object?[] { "(?:a)(?i:b)c", 512, 512 };
        yield return new object?[] { "a(?i:b)(?m:c)", 512, 512 };
        yield return new object?[] { "(?i:(?=a)b)", 512, 513 };
        yield return new object?[] { "(?i:(?<=a)b)", 512, 513 };
    }

    public static IEnumerable<object?[]> IgnoreCaseDeclineCases()
    {
        yield return new object?[] { "(?ix-a:b)", 512, RegexParseError.InvalidGroupingConstruct, 6 };
        yield return new object?[] { "[a-Z]", 513, RegexParseError.ReversedCharacterRange, 4 };
        yield return new object?[] { "(?i:\\U00010400)", 512, RegexParseError.UnrecognizedEscape, 6 };
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
        var oracle = new Regex(pattern, (RegexOptions)options);
        var product = new Utf8Regex(pattern, options: (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNames(), Utf8GroupNames.Read(product));
        Assert.Equal(oracle.GetGroupNumbers(), Enumerable.Range(0, product.CaptureGroupCount + 1).Select(product.GroupNumberFromCaptureSlot).ToArray());
        foreach (var inputText in inputs)
        {
            var input = System.Text.Encoding.UTF8.GetBytes(inputText);
            var expected = oracle.Match(inputText);
            var match = product.Match(input);
            Assert.Equal(expected.Success, match.Success);
            if (expected.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expected.Index)), match.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(expected.Index, expected.Length)), (match.EndOffsetInBytes - match.StartOffsetInBytes));
                var actualDetailed = Utf8MatchSequence.First(product.CreateMatchCursor(input));
                for (var g = 0; g < expected.Groups.Count; g++)
                {
                    var expectedGroup = expected.Groups[g];
                    Assert.True(actualDetailed.TryGetGroup(g, out var actualGroup));
                    Assert.Equal(expectedGroup.Success, actualGroup.Success);
                    if (expectedGroup.Success)
                    {
                        Assert.Equal(expectedGroup.Value, System.Text.Encoding.UTF8.GetString(actualDetailed.GetGroupMemory(g).Span));
                    }
                }
            }

            Assert.Equal(oracle.Matches(inputText).Count, product.Count(input));
        }
    }

    [Theory]
    [InlineData("(?i)a(?-i)bx", "é ABx Abx abx aBx", 7, 3, 2)]
    [InlineData("(?i)a(?-i)b|xy", "é AB Ab xy XY", 6, 2, 2)]
    [InlineData("(?i)a(?-i)b{1,2}", "é AB Abb ab aBB", 6, 3, 2)]
    [InlineData("(?i)[a](?-i)[b]|xy", "é AB Ab xy XY", 6, 2, 2)]
    public void FiniteLiteralExecutionHonorsUnscopedCaseToggles(string pattern, string subject,
        int firstStart, int firstLength, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(subject);
        var patternUtf8 = Encoding.UTF8.GetBytes(pattern);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = RegexOptions.CultureInvariant | mode;
            foreach (var regex in new[] { new Utf8Regex(pattern, options), Utf8Regex.Compile(patternUtf8, options) })
            {
                Assert.False(regex.IsMatch("ABX"u8));
                Assert.Equal(expectedCount, regex.Count(input));
                var match = regex.Match(input);
                Assert.True(match.Success);
                Assert.Equal(firstStart, match.StartOffsetInBytes);
                Assert.Equal(firstStart + firstLength, match.EndOffsetInBytes);
            }
        }
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseAcceptCases))]
    public void OwnedIgnoreCaseServesWithBclMetadata(string pattern, int options, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal((options & (int)RegexOptions.CultureInvariant) != 0 ? CultureInfo.InvariantCulture.Name : CultureInfo.CurrentCulture.Name, actual.Culture?.Name);
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseDeclineCases))]
    public void OwnedIgnoreCaseDeclinesUnsupported(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }
}
