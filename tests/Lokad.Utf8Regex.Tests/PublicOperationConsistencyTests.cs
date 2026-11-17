using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class PublicOperationConsistencyTests
{
    public static IEnumerable<object?[]> Cases()
    {
        yield return new object?[] { "exact-ascii-literal", "abc", RegexOptions.CultureInvariant, "xxabcxx" };
        yield return new object?[] { "exact-unicode-ru", "Шерлок", RegexOptions.None, "prefix Шерлок suffix" };
        yield return new object?[] { "exact-unicode-zh", "夏洛克", RegexOptions.None, "prefix夏洛克suffix" };
        yield return new object?[] { "literal-alternation-en", "Sherlock Holmes|John Watson|Irene Adler", RegexOptions.None, "John Watson esq" };
        yield return new object?[] { "case-pair-class-family", "[Aa][Bb][Cc]", RegexOptions.CultureInvariant, "xxABCxx" };
        yield return new object?[] { "single-upper-class-literal", "[A]", RegexOptions.CultureInvariant, "A" };
        yield return new object?[] { "case-pair-class-ignorecase", "[Aa][Bb][Cc]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxABCxx" };
        yield return new object?[] { "structural-identifier-family", "\\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\\b", RegexOptions.CultureInvariant, "logger.LogError(\"bad\");" };
        yield return new object?[] { "structural-ordered-window", "Holmes.{0,25}Watson|Watson.{0,25}Holmes", RegexOptions.None, "Holmes and Watson" };
        yield return new object?[] { "structural-token-window", "[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}", RegexOptions.None, "abcdefghij Result klmnopqrst" };
        yield return new object?[] { "ignorecase-literals-abd", "a[bc]d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxabdxx" };
        yield return new object?[] { "structural-repeated-segment", "(?:[A-Z][a-z]+\\s*){10,100}", RegexOptions.CultureInvariant, "Alpha Beta Gamma Delta Epsilon Zeta Eta Theta Iota Kappa " };
        yield return new object?[] { "structural-quoted-relation-aws", "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+", RegexOptions.Multiline, "\"AIDAABCDEFGHIJKLMNOP\"\nctx = 1\nctx = 2\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n\n\"AIDAABCDEFGHIJKLMNOP\"\nctx = 3\nctx = 4\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n" };
        yield return new object?[] { "char-class-plus", "[a-z]+", RegexOptions.CultureInvariant, "xxabcxx" };
        yield return new object?[] { "negated-class", "[^0-9]+", RegexOptions.CultureInvariant, "xxabcxx" };
        yield return new object?[] { "bounded-repeat", "[A-Za-z]{8,13}", RegexOptions.None, "xxabcdefghxx" };
        yield return new object?[] { "digit-range", "\\d{3}-\\d{4}", RegexOptions.CultureInvariant, "call 555-1234 now" };
        yield return new object?[] { "start-anchor", "^abc", RegexOptions.CultureInvariant, "abcdef" };
        yield return new object?[] { "end-anchor", "abc$", RegexOptions.CultureInvariant, "xxabc" };
        yield return new object?[] { "word-boundary", "\\bword\\b", RegexOptions.CultureInvariant, "a word here" };
        yield return new object?[] { "multiline-anchor", "^abc", RegexOptions.Multiline | RegexOptions.CultureInvariant, "xx\nabc" };
        yield return new object?[] { "lookahead-pos", "a(?=b)", RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "lookahead-neg", "a(?!b)", RegexOptions.CultureInvariant, "ac" };
        yield return new object?[] { "lookbehind-pos", "(?<=a)b", RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "lookbehind-neg", "(?<!a)b", RegexOptions.CultureInvariant, "cb" };
        yield return new object?[] { "backreference", "(a)\\1", RegexOptions.CultureInvariant, "aa" };
        yield return new object?[] { "named-backreference", "(?<w>a)\\k<w>", RegexOptions.CultureInvariant, "aa" };
        yield return new object?[] { "repeated-capture", "(a)+", RegexOptions.CultureInvariant, "aaa" };
        yield return new object?[] { "named-capture", "(?<word>\\w+)", RegexOptions.CultureInvariant, "hello" };
        yield return new object?[] { "right-to-left", "abc", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, "xxabcxx" };
        yield return new object?[] { "ignorecase-invariant", "abc", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxABCxx" };
        yield return new object?[] { "ignorecase-culture", "abc", RegexOptions.IgnoreCase, "xxABCxx" };
        yield return new object?[] { "explicit-capture", "(a)(b)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, "xxabxx" };
        yield return new object?[] { "whitespace-mode", "a b c", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant, "abc" };
        yield return new object?[] { "singleline-dot", "a.*b", RegexOptions.Singleline | RegexOptions.CultureInvariant, "a\nb" };
        yield return new object?[] { "ecmascript", "abc", RegexOptions.ECMAScript, "xxabcxx" };
        yield return new object?[] { "nonbacktracking", "abc", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, "xxabcxx" };
        yield return new object?[] { "empty-star", "a*", RegexOptions.CultureInvariant, "bbb" };
        yield return new object?[] { "astral-literal", "\U0001F600", RegexOptions.CultureInvariant, "\U0001F600" };
        yield return new object?[] { "conditional", "(a)?(?(1)ab|cd)", RegexOptions.CultureInvariant, "aab" };
        yield return new object?[] { "balancing", "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))", RegexOptions.CultureInvariant, "BEGINEND" };
        yield return new object?[] { "word-email-shape", "\\w+@\\w+\\.\\w+", RegexOptions.CultureInvariant, "mail me a@b.cd ok" };
        yield return new object?[] { "compiled-literal", "abc", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xxabcxx" };
        yield return new object?[] { "negated-shorthand-window-gap-miss", "a\\D{2,5}b", RegexOptions.CultureInvariant, "xa12b" };
        yield return new object?[] { "negated-shorthand-window-gap-hit", "a\\D{2,5}b", RegexOptions.CultureInvariant, "xayzb" };
        yield return new object?[] { "negated-space-window-gap-hit", "a[^\\s]{2,5}b", RegexOptions.CultureInvariant, "xayzb" };
        yield return new object?[] { "divergent-ranged-count", "(?:a|ab){2,3}", RegexOptions.CultureInvariant, "xaabab" };
        yield return new object?[] { "folded-shorthand-range-astral", "(?i)A\\D{2,}?B", RegexOptions.CultureInvariant, "xa\U0001F600b" };
        yield return new object?[] { "negated-property-range-astral", "a\\P{L}{2,3}b", RegexOptions.CultureInvariant, "xa\U0001F600b" };
        yield return new object?[] { "negated-shorthand-plus-astral", "a\\P{Nd}+b", RegexOptions.CultureInvariant, "xa\U0001F600b" };
        yield return new object?[] { "word-range-astral", "a\\W{2,}?b", RegexOptions.CultureInvariant, "xa\U0001F600b" };
        yield return new object?[] { "negated-shorthand-window-gap-miss-compiled", "a\\D{2,5}b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xa12b" };
        yield return new object?[] { "negated-shorthand-window-gap-hit-compiled", "a\\D{2,5}b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xayzb" };
        yield return new object?[] { "negated-space-window-gap-hit-compiled", "a[^\\s]{2,5}b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xayzb" };
        yield return new object?[] { "divergent-ranged-count-compiled", "(?:a|ab){2,3}", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xaabab" };
        yield return new object?[] { "negated-property-range-astral-compiled", "a\\P{L}{2,3}b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xa\U0001F600b" };
        yield return new object?[] { "word-range-astral-compiled", "a\\W{2,}?b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xa\U0001F600b" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OperationsAgreeOnWholeSubjectByteRanges(string name, string pattern,
        RegexOptions options, string inputText)
    {
        Assert.NotEmpty(name);
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var ranges = new List<(int Start, int Length)>();
        foreach (var match in regex.EnumerateMatches(input))
            ranges.Add((match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(ranges.Count, regex.Count(input));
        Assert.Equal(ranges.Count != 0, regex.IsMatch(input));
        var first = regex.Match(input);
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(ranges.Count != 0, first.Success);
        Assert.Equal(first.Success, detailed.Success);
        if (first.Success)
        {
            Assert.Equal(ranges[0], (first.IndexInBytes, first.LengthInBytes));
            Assert.Equal(ranges[0], (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
        }
        using var cursor = regex.CreateMatchCursor(input,
            (options & RegexOptions.RightToLeft) != 0 ? input.Length : 0);
        foreach (var expected in ranges)
        {
            Assert.True(cursor.MoveNext(out var match));
            Assert.Equal(expected, (match.Value.StartOffsetInBytes,
                match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));
        }
        Assert.False(cursor.MoveNext(out _));
    }
}
