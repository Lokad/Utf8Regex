using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R05 astral routing audit: BCL matches astral halves (one UTF-16 unit per dot,
// negated class, or negated shorthand) while owned walkers consume whole UTF-8
// scalars, so width-sensitive programs decline astral inputs at routing while
// literals, positive classes, and positive shorthands stay owned (their ASCII
// checks never meet astral bytes, and the owned tables ship no astral ranges).
// Every operation must agree with its oracle lane regardless of which route
// serves it; any owned route consuming whole astral scalars here trips parity.
public sealed class Utf8AstralSemanticsTests
{
    public static System.Collections.Generic.IEnumerable<object?[]> AstralCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a.c", "a\U0001F600c"),
            ("a.c", "\U0001F600"),
            ("..", "\U0001F600"),
            ("...", "\U0001F600"),
            (".*", "\U0001F600"),
            ("a.*b", "a\U0001F600b"),
            (".{2}", "\U0001F600"),
            (".{2,4}", "\U0001F600\U0001F600"),
            ("(a.c)+", "xa\U0001F600c"),
            ("(a.c)", "a\U0001F600c"),
            ("^.$", "\U0001F600"),
            ("((a|.)c)", "a\U0001F600c"),
            ("[^x]", "\U0001F600"),
            ("[^x]+", "\U0001F600"),
            ("[^x][^y]", "\U0001F600"),
            ("a[^x]d", "a\U0001F600d"),
            ("[^\\d]", "\U0001F600"),
            ("[^\\w]", "\U0001F600"),
            ("[^a\\d]", "\U0001F600"),
            ("[abc]+", "a\U0001F600bc\U0001F600d"),
            ("[abc]", "\U0001F600"),
            ("[\\d]", "\U0001D7CE"),
            ("[\\d]", "1"),
            ("[a\\d]", "\U0001D7CE"),
            ("[a\\d]", "a1"),
            ("\\d+", "1\U0001D7CE2"),
            ("\\d", "\U0001D7CE"),
            ("\\D+", "\U0001F600"),
            ("\\w+", "a\U00010000b"),
            ("\\w+", "\U00010000_\U00010000"),
            ("\\W+", "\U0001F600"),
            ("\\p{L}", "\U0001F600"),
            ("^a", "a\U0001F600"),
            ("c$", "a\U0001F600c"),
            ("\\ba\\b", "a \U0001F600"),
            ("(?i)a[^k]b", "a\U0001F600b"),
            ("[abc]*", "a\U0001F600"),
            ("[abc]*", "\U0001F600"),
            ("a*", "a\U0001F600"),
            ("x*", "\U0001F600"),
            (".*?", "\U0001F600"),
            ("(a|b)*", "a\U0001F600b"),
            ("^", "\U0001F600"),
            ("a|", "a\U0001F600"),
            ("^a.*$", "a\U0001F600b"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(AstralCases))]
    public void AstralInputMatchesOracleAcrossOperations(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(text).Count, regex.Count(input));

        var expectedMatch = oracle.Match(text);
        var actualMatch = regex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);

        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatchItem in oracle.Matches(text))
        {
            expected.Add((expectedMatchItem.Index, expectedMatchItem.Length));
        }

        var actual = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in regex.EnumerateMatches(input))
        {
            actual.Add((valueMatch.IndexInUtf16, valueMatch.LengthInUtf16));
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].Index);
            Assert.Equal(expected[i].Length, actual[i].Length);
        }

        const int maxSplits = int.MaxValue;
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, maxSplits))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], start, end - start));
        }

        var splitRegex = new Utf8Regex(pattern, options);
        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, maxSplits))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expectedSplits, actualSplits);

        var replaceRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "x"), replaceRegex.ReplaceToString(input, "x"));
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralEvaluatorCases()
    {
        // Evaluator matches must stay scalar-aligned by design (both the owned
        // groupless arm and the BCL-backed grouped loop fail closed otherwise), so
        // empty-capable and half-matching shapes on astral input stay out of scope
        // here; only scalar-aligned shapes are pinned.
        (string pattern, string text)[] rows =
        [
            ("^", "\U0001F600"),
            ("[abc]+", "a\U0001F600bc\U0001F600d"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(AstralEvaluatorCases))]
    public void AstralEvaluatorMatchesOracle(string pattern, RegexOptions options, string text)
    {
        // Evaluator callbacks observe enumeration (proven scalar-aligned above),
        // so they need no astral fence on any core.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }
}
