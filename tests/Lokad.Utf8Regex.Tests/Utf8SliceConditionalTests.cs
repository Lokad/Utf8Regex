using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// R03 conditionals: group tests read current capture state with committed
// branches (downstream failure never retries the other branch), expression
// tests desugar to lookahead-guarded alternations, and number tests denote
// final groups (validated post-parse since forward references read groups
// defined anywhere). Lookbehind bodies execute backward at their exact
// edge on the shared journal, including references and conditional tests.
// Bare shapes keep native precedence; quantified shapes route owned.
public sealed class Utf8SliceConditionalTests
{
    public static IEnumerable<object?[]> SliceConditionalShapes()
    {
        yield return new object?[] { "(a)?(?(1)b|c)" };
        yield return new object?[] { "(a)?(?(1)b)" };
        yield return new object?[] { "(?(?=a)b|c)" };
        yield return new object?[] { "(?(?!a)b|c)" };
        yield return new object?[] { "(?<n>a)?(?(n)b|c)" };
        yield return new object?[] { "(a)(?(1)b|c)(d)" };
        yield return new object?[] { "(a)?(?(1)(b)|(c))" };
        yield return new object?[] { "(?(1)(a)|(b))" };
        yield return new object?[] { "(\\p{L})(?(1)a|b)" };
        yield return new object?[] { "(\\d)(?(1)\\p{L}|x)" };
        yield return new object?[] { "(?(\\p{L}+)a|b)" };
    }

    [Theory]
    [MemberData(nameof(SliceConditionalShapes))]
    public void ConditionalShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> DeclinedShapes()
    {
        yield return new object?[] { "(?<=(a)\\1)b", "aab" };
        yield return new object?[] { "(?<=ab|(a)\\1)cd", "aacd" };
        yield return new object?[] { "(?<=(a)(?(1)b|c))d", "abd" };
        yield return new object?[] { "(?<=(a)(?(1)b|c))d", "acd" };
    }

    [Theory]
    [MemberData(nameof(DeclinedShapes))]
    public void LookbehindOrderSensitiveShapesExecuteOwned(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> ParserDeclines()
    {
        yield return new object?[] { "(a)?(?(1)x|y|z)" };
        yield return new object?[] { "(?(?=a)x|y|z)" };
        yield return new object?[] { "(?(12)a|b)" };
        yield return new object?[] { "(?(1)a|b)" };
    }

    [Theory]
    [MemberData(nameof(ParserDeclines))]
    public void ConditionalOutOfRangeShapesDeclineAtParse(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [MemberData(nameof(ParserDeclines))]
    public void ConditionalOutOfRangeShapesFailOwnedConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> ParserShapes()
    {
        yield return new object?[] { "(a)?(?(1)b|c)" };
        yield return new object?[] { "(a)?(?(1)b)" };
        yield return new object?[] { "(a)?(?(1)|b)" };
        yield return new object?[] { "(a)?(?(1)b|)" };
        yield return new object?[] { "(?(?=a)b|c)" };
        yield return new object?[] { "(?(?!a)b|c)" };
        yield return new object?[] { "(?(nope)a|b)" };
        yield return new object?[] { "(?(-1)a|b)" };
        yield return new object?[] { "(?(DEFINE)a|b)" };
        yield return new object?[] { "(?()a|b)" };
        yield return new object?[] { "(?(0)a|b)" };
        yield return new object?[] { "(?<n>a)?(?(n)b|c)" };
        yield return new object?[] { "(?(1)(a)|(b))" };
        yield return new object?[] { "(\\p{L})(?(1)a|b)" };
        yield return new object?[] { "(\\d)(?(1)\\p{L}|x)" };
        yield return new object?[] { "(?(\\p{L}+)a|b)" };
        yield return new object?[] { "((?(1)a|b))" };
        yield return new object?[] { "(?((?<n>a))b|c)" };
        yield return new object?[] { "(?((?#c)a)b|d)" };
    }

    [Theory]
    [MemberData(nameof(ParserShapes))]
    public void ConditionalOrdinaryShapesParse(string pattern)
    {
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(a)?(?(1)b|c)", "ab", true };
        yield return new object?[] { "(a)?(?(1)b|c)", "c", true };
        yield return new object?[] { "(a)?(?(1)b|c)", "ac", true };
        yield return new object?[] { "(a)?(?(1)b)", "ab", true };
        yield return new object?[] { "(a)?(?(1)b)", "a", true };
        yield return new object?[] { "(?(?=a)b|c)", "ab", false };
        yield return new object?[] { "(?(?=a)b|c)", "c", true };
        yield return new object?[] { "(?(?!a)b|c)", "cb", true };
        yield return new object?[] { "(?<n>a)?(?(n)b|c)", "ab", true };
        yield return new object?[] { "(?(DEFINE)a)", "a", true };
        yield return new object?[] { "(?(DEFINE)a)", "b", true };
        yield return new object?[] { "(?(R)a|b)", "a", false };
        yield return new object?[] { "(?(R)a|b)", "b", true };
        yield return new object?[] { "(?(R1)a|b)", "a", false };
        yield return new object?[] { "(?(R1)a|b)", "b", true };
        yield return new object?[] { "(a)(?(1)b|c)(d)", "abd", true };
        yield return new object?[] { "(a)(?(1)b|c)(d)", "acd", false };
        yield return new object?[] { "(?(1)(a)|(b))", "a", false };
        yield return new object?[] { "(?(1)(a)|(b))", "b", true };
        yield return new object?[] { "(a)?(?(1)(b)|(c))", "ab", true };
        yield return new object?[] { "(a)?(?(1)(b)|(c))", "c", true };
        yield return new object?[] { "(a)?(?(1)b|a)b", "aab", true };
        yield return new object?[] { "(a)?(?(1)b|a)b", "abb", true };
        yield return new object?[] { "(x)?(?(1)y|z)", "z", true };
        yield return new object?[] { "(x)?(?(1)y|z)", "xy", true };
        yield return new object?[] { "(?(DEFINE)a|b)", "b", true };
        yield return new object?[] { "(?(DEFINE)a|b)", "a", false };
        yield return new object?[] { "(?()a|b)", "b", false };
        yield return new object?[] { "(?()a|b)", "a", true };
        yield return new object?[] { "(?(-1)a|b)", "b", true };
        yield return new object?[] { "(?(-1)a|b)", "a", false };
        yield return new object?[] { "(a*)?(?(1)b|B)", "b", true };
        yield return new object?[] { "(a*)?(?(1)b|B)", "B", true };
        yield return new object?[] { "(a*)?(?(1)B|b)", "b", true };
        yield return new object?[] { "((?(1)a|b))", "a", false };
        yield return new object?[] { "((?(1)a|b))", "b", true };
        yield return new object?[] { "(?<n>(?(1)a|b))", "b", true };
        yield return new object?[] { "(?(0)a|b)", "b", true };
        yield return new object?[] { "(?(0)a|b)", "a", false };
        yield return new object?[] { "(\\p{L})(?(1)a|b)", "xa", true };
        yield return new object?[] { "(\\p{L})(?(1)a|b)", "xb", false };
        yield return new object?[] { "(\\d)(?(1)\\p{L}|x)", "1a", true };
        yield return new object?[] { "(?(\\p{L}+)a|b)", "ab", true };
        yield return new object?[] { "((\\p{L}+))\\1", "abab", true };
        yield return new object?[] { "(?i:(\\p{Ll})(?(1)a|b))", "xA", true };
        yield return new object?[] { "(?i:(\\p{Ll})(?(1)a|b))", "xb", false };
        yield return new object?[] { "(?(?=a)a|b)+", "xaab\U0001F600", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedConditionalIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
        }
    }
}
public sealed class Utf8SliceConditionalOperationTests
{
    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "(a)?(?(1)b|c)", "abc", 2 };
        yield return new object?[] { "(a)?(?(1)b)", "ab", 2 };
        yield return new object?[] { "(?(?=a)b|c)", "abc", 1 };
        yield return new object?[] { "(a)(?(1)b|c)(d)", "abdabd", 2 };
        yield return new object?[] { "(?(?=a)a|b)+", "xaab\U0001F600", 1 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedConditionalCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        foreach (var options in new[]
        {
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.Count(input));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> FirstMatchCases()
    {
        yield return new object?[] { "(a)?(?(1)b|c)", "ac", 1, 1 };
        yield return new object?[] { "(a)(?(1)b|c)(d)", "abd", 0, 3 };
        yield return new object?[] { "(?(?=a)b|c)", "xc", 1, 1 };
        yield return new object?[] { "(?<n>a)?(?(n)b|c)", "ab", 0, 2 };
        yield return new object?[] { "(?(?=a)a|b)+", "xaab\U0001F600", 1, 3 };
    }

    [Theory]
    [MemberData(nameof(FirstMatchCases))]
    public void OwnedConditionalMatchMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expectedStart, expected.Index);
        Assert.Equal(expectedLength, expected.Length);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.Match(input);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.IndexInUtf16);
        Assert.Equal(expectedLength, found.LengthInUtf16);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> DetailedCases()
    {
        yield return new object?[] { "(a)(?(1)b|c)(d)", "abd", 0, 3, new int[] { 0, 2 }, new int[] { 1, 1 } };
        yield return new object?[] { "(a)?(?(1)(b)|(c))", "ab", 0, 2, new int[] { 0, 1, -1 }, new int[] { 1, 1, 0 } };
        yield return new object?[] { "(a)?(?(1)(b)|(c))", "c", 0, 1, new int[] { -1, -1, 0 }, new int[] { 0, 0, 1 } };
        yield return new object?[] { "(?(?=a)a|b)+", "xaab\U0001F600", 1, 3, new int[] { }, new int[] { } };
    }

    [Theory]
    [MemberData(nameof(DetailedCases))]
    public void OwnedConditionalDetailedMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength, int[] groupStarts, int[] groupLengths)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(expectedStart, detailed.IndexInUtf16);
        Assert.Equal(expectedLength, detailed.LengthInUtf16);
        Assert.Equal(groupStarts.Length, detailed.GroupCount - 1);
        for (var group = 1; group <= groupStarts.Length; group++)
        {
            var actual = detailed.GetGroup(group);
            var wanted = expected.Groups[group];
            Assert.Equal(wanted.Success, actual.Success);
            if (wanted.Success)
            {
                Assert.Equal(groupStarts[group - 1], actual.IndexInUtf16);
                Assert.Equal(groupLengths[group - 1], actual.LengthInUtf16);
            }

            Assert.Equal(wanted.Value, actual.GetValueString());
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> EnumerateCases()
    {
        yield return new object?[] { "(a)?(?(1)b)", "ab", new int[] { 0, 2, 2, 0 } };
        yield return new object?[] { "(?(?=a)b|c)", "acac", new int[] { 1, 1, 3, 1 } };
        yield return new object?[] { "(?(?=a)a|b)+", "xaab\U0001F600", new int[] { 1, 3 } };
    }

    [Theory]
    [MemberData(nameof(EnumerateCases))]
    public void OwnedConditionalEnumerateMatchesOracle(string pattern, string inputText, int[] expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var wanted = new List<string>();
        foreach (Match match in oracle.Matches(inputText))
        {
            wanted.Add(match.Index + ":" + match.Length);
        }

        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new List<string>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            actual.Add(match.IndexInUtf16 + ":" + match.LengthInUtf16);
        }

        Assert.Equal(wanted, actual);
        var flat = new List<int>();
        foreach (var item in actual)
        {
            var parts = item.Split(':');
            flat.Add(int.Parse(parts[0]));
            flat.Add(int.Parse(parts[1]));
        }

        Assert.Equal(expected, flat.ToArray());
    }

    public static IEnumerable<object?[]> ReplaceCases()
    {
        yield return new object?[] { "(a)?(?(1)b|c)", "ac", "a<c>" };
        yield return new object?[] { "(?(?=a)b|c)", "ac", "a<c>" };
        yield return new object?[] { "(?(?=a)a|b)+", "xaab\U0001F600", "x<aab>\U0001F600" };
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void OwnedConditionalReplaceMatchesOracle(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Replace(inputText, "<$&>"));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(input, "<$&>")));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> AstralConditionalStringReplaceCases()
    {
        yield return new object?[] { "(?(?=a)a|b)+", "xaab\U0001F600", "Q" };
        yield return new object?[] { "(?(?=a)a|b)+", "xaab\U0001F600", "<$&>" };
        yield return new object?[] { "(?(a)a|b)+", "xaab\U0001F600", "Q" };
        yield return new object?[] { "(?(a)a|b)+", "xaab\U0001F600", "<$&>" };
    }

    [Theory]
    [MemberData(nameof(AstralConditionalStringReplaceCases))]
    public void OwnedConditionalStringReplaceMatchesOracle(string pattern, string inputText, string template)
    {
        // R05: the string core admits LTR slice shapes past scalar-start alignment
        // exactly like the byte cores, so conditional shapes replace owned here with
        // oracle parity and zero fallback construction on both templates.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var stringRegex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(stringRegex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.Replace(inputText, template), stringRegex.ReplaceToString(input, template));
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?(?=a)a|b)+", "xaab")]
    [InlineData("(?(?=a)a|b)+", "xaab\U0001F600")]
    [InlineData("(?(a)a|b)+", "xaab")]
    [InlineData("(?(a)a|b)+", "xaab\U0001F600")]
    public void OwnedConditionalEvaluatorMatchesOracle(string pattern, string inputText)
    {
        // R05: conditional shapes project every callback argument from the
        // deterministic owned re-query at each enumerated hit start, so both
        // evaluator cores serve owned with zero fallback construction.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(inputText, static match => "[" + match.Value + "]");
        var byteRegex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.True(byteRegex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        var stringRegex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(stringRegex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]"));
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?(?=a)a|b)+", "xaab\U0001F600", 0)]
    [InlineData("(?(?=a)a|b)+", "xaab\U0001F600", 2)]
    [InlineData("(a)?(?(1)b|c)", "aab", 1)]
    public void OwnedConditionalFromOffsetDetailedMatchesOracle(string pattern, string inputText, int startAt)
    {
        // R05: FromOffset Detailed keeps absolute-origin semantics through the
        // shared first-of-enumeration arm and projects group 0 the same way.
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var expected = oracle.Match(inputText, startAt);
        var actual = regex.MatchDetailedFromUtf16Offset(input, startAt);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Groups[0].Index, actual.GetGroup(0).IndexInUtf16);
            Assert.Equal(expected.Groups[0].Length, actual.GetGroup(0).LengthInUtf16);
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?(?=a)a|b)+", "xaab")]
    [InlineData("(?(?=a)a|b)+", "xaab\U0001F600")]
    [InlineData("(a)?(?(1)b|c)", "acab")]
    public void OwnedConditionalSplitsMatchOracle(string pattern, string inputText)
    {
        // R05: conditional enumeration already serves owned, so the shared split
        // creation projects the same coordinates with zero fallback construction.
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(inputText, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((inputText[start..end], start, end - start));
        }
        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(?(11)x|y)", "abcdefghijkx", true)]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(?(11)x|y)", "abcdefghijky", false)]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(?(10)x|y)", "abcdefghijkx", true)]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(?(10)x|y)", "abcdefghijky", false)]
    public void OwnedConditionalMultiDigitMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<x>a)(?<y>b)(?(1)a|b)", "aba", true)]
    [InlineData("(a)?(?(1)(?>b+)|c)", "abbb", true)]
    [InlineData("(?<o>a)(?<-o>b)(?(o)x|y)", "aby", true)]
    [InlineData("(AB)?(?(1)cd|ef)", "ABcd", true)]
    public void OwnedConditionalSpecialShapesMatchOracle(string pattern, string inputText, bool expected)
    {
        var options = pattern == "(AB)?(?(1)cd|ef)"
            ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            : RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void ConditionalExplicitCaptureMismatchThrowsLikeRuntime()
    {
        Assert.Throws<RegexParseException>(() => new Regex("(?n:(a)(b))(?(1)x|y)", RegexOptions.CultureInvariant));
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex("(?n:(a)(b))(?(1)x|y)", RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("(a)?(?(1)b|c)", "aab", 1, true, 1, 2)]
    [InlineData("(?(?=a)b|c)", "xac", 2, true, 2, 1)]
    public void OwnedConditionalFromOffsetMatchesOracle(string pattern, string inputText, int startAt, bool expectedSuccess, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var wanted = oracle.Match(inputText, startAt);
        Assert.Equal(expectedSuccess, wanted.Success);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.MatchFromUtf16Offset(input, startAt);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, wanted.Index);
            Assert.Equal(expectedLength, wanted.Length);
            Assert.Equal(expectedStart, found.IndexInUtf16);
            Assert.Equal(expectedLength, found.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a)?(?(1)b|c)", "cb", true)]
    [InlineData("(a)?(?(1)b|c)", "ab", false)]
    public void OwnedConditionalRightToLeftMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}
