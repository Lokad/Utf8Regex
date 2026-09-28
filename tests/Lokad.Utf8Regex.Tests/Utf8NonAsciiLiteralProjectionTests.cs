using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 projection correctness: ASCII-literal-family patterns on non-ASCII input must
/// report UTF-16 coordinates through the boundary map (or take BCL verification),
/// never raw byte offsets. Covers single and alternation literals, ignore-case
/// literals, empty matches, splits, and from-offset enumeration on BMP and
/// supplementary text, with an ASCII control row.
/// </summary>
public sealed class Utf8NonAsciiLiteralProjectionTests
{
    public static IEnumerable<object?[]> LiteralCases()
    {
        yield return new object?[] { "y", RegexOptions.CultureInvariant, "x\U0001F600y" };
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, "xxabc\U0001F600abcxx" };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, "абаб" };
        yield return new object?[] { "ab|cd", RegexOptions.CultureInvariant, "x\U0001F600abxcd" };
        yield return new object?[] { "ABC", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxab\U0001F600cxx" };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, "b\U0001F600b" };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, "11ab\U0001F600cd22" };
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, "xxabcxx" };
    }

    [Theory]
    [MemberData(nameof(LiteralCases))]
    public void EnumerateMatchesProjectsCoordinatesLikeRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options);
        var runtime = new Regex(pattern, options);

        var expected = new List<(int Index, int Length)>();
        foreach (Match match in runtime.Matches(text))
        {
            expected.Add((match.Index, match.Length));
        }

        var actual = new List<Utf8ValueMatch>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            actual.Add(match);
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].IndexInUtf16);
            Assert.Equal(expected[i].Length, actual[i].LengthInUtf16);
            var aligned = IsScalarBoundary(text, expected[i].Index) &&
                IsScalarBoundary(text, expected[i].Index + expected[i].Length);
            Assert.Equal(aligned, actual[i].IsByteAligned);
            if (aligned)
            {
                Assert.Equal(
                    Encoding.UTF8.GetByteCount(text.Substring(0, expected[i].Index)),
                    actual[i].IndexInBytes);
                Assert.Equal(
                    Encoding.UTF8.GetByteCount(text.Substring(expected[i].Index, expected[i].Length)),
                    actual[i].LengthInBytes);
            }
        }
    }

    [Theory]
    [MemberData(nameof(LiteralCases))]
    public void EnumerateSplitsMatchRuntimeValues(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options);
        var runtime = new Regex(pattern, options);

        var expected = runtime.Split(text);
        var actual = new List<string>();
        foreach (var split in regex.EnumerateSplits(input))
        {
            actual.Add(split.GetValueString());
        }

        Assert.Equal(expected, actual.ToArray());
    }

    [Fact]
    public void EnumerateMatchesFromUtf16OffsetProjectsLikeRuntime()
    {
        const string text = "x\U0001F600abxcd";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("ab|cd", RegexOptions.CultureInvariant);
        var runtime = new Regex("ab|cd", RegexOptions.CultureInvariant);

        foreach (var offset in new[] { 0, 1, 3, 4 })
        {
            var collected = new List<(int Index, int Length)>();
            foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, offset))
            {
                collected.Add((match.IndexInUtf16, match.LengthInUtf16));
            }

            var expected = new List<(int Index, int Length)>();
            foreach (Match match in runtime.Matches(text.Substring(offset)))
            {
                expected.Add((match.Index + offset, match.Length));
            }

            Assert.Equal(expected.Count, collected.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Index, collected[i].Index);
                Assert.Equal(expected[i].Length, collected[i].Length);
            }
        }
    }

    public static IEnumerable<object?[]> LightMatchCases()
    {
        // Lightweight `Match` (and detailed group 0 shaped from it) must report
        // UTF-16 coordinates on non-ASCII input, never raw byte offsets. Byte and
        // UTF-16 offsets diverge past non-ASCII content, in both directions.
        yield return new object?[] { "\\bab\\b", RegexOptions.CultureInvariant, "x\U0001F600ab" };
        yield return new object?[] { "(?i)ab", RegexOptions.CultureInvariant, "x\u00E9ab" };
        yield return new object?[] { "(?i)ab", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "x\u00E9ab" };
        yield return new object?[] { "ab", RegexOptions.CultureInvariant, "x\U0001F600ab" };
        yield return new object?[] { "\\bab\\b", RegexOptions.CultureInvariant, "ab\U0001F600" };
        yield return new object?[] { "ab", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "x\U0001F600ab" };
        yield return new object?[] { "\\bab\\b", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "x\U0001F600ab" };
        yield return new object?[] { "(?i)ab", RegexOptions.CultureInvariant, "ab\u00E9" };
    }

    [Theory]
    [MemberData(nameof(LightMatchCases))]
    public void LightMatchProjectsCoordinatesLikeRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var lane in new[] { options, options | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, lane, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, lane);

            var expected = oracle.Match(text);
            var actual = regex.Match(input);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Index, actual.IndexInUtf16);
                Assert.Equal(expected.Length, actual.LengthInUtf16);
            }

            var detailed = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, detailed.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Index, detailed.GetGroup(0).IndexInUtf16);
                Assert.Equal(expected.Length, detailed.GetGroup(0).LengthInUtf16);
            }

            // The mapping stays owned: no fallback engine is constructed.
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }

    public static IEnumerable<object?[]> NonAsciiSplitCases()
    {
        string[] patterns = ["abc", "ab", "(ab)", "a{1}"];
        string[] texts = ["xxab12cdxx", "ééabcé", "xaby", "nomatchhere", ""];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }

        yield return new object?[] { "ab", RegexOptions.CultureInvariant, "x\U0001F600abx" };
        yield return new object?[] { "ab", RegexOptions.Compiled | RegexOptions.CultureInvariant, "x\U0001F600abx" };
    }

    [Theory]
    [MemberData(nameof(NonAsciiSplitCases))]
    public void EnumerateSplitsOwnsNonAsciiSingleLiterals(string pattern, RegexOptions options, string text)
    {
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], start, end - start));
        }

        var actual = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("a.c", "ééabcé")]
    [InlineData("a.c", "x\U0001F600abc")]
    public void NonByteSafeSimpleKeepsBclShaping(string pattern, string text)
    {
        // Declared R05 debt: non-byte-safe simple shapes verify candidates through
        // BCL engines on non-ASCII input (main lazy for discovery, anchored lazy for
        // bounded-slice detailed shaping). Parity holds; construction is pinned per op.
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);

            var enumRegex = new Utf8Regex(pattern, options);
            var expected = new System.Collections.Generic.List<(int Index, int Length)>();
            foreach (Match expectedMatch in oracle.Matches(text))
            {
                expected.Add((expectedMatch.Index, expectedMatch.Length));
            }

            var actual = new System.Collections.Generic.List<(int Index, int Length)>();
            foreach (var valueMatch in enumRegex.EnumerateMatches(input))
            {
                actual.Add((valueMatch.IndexInUtf16, valueMatch.LengthInUtf16));
            }

            Assert.Equal(expected.Count, actual.Count);
            Assert.True(enumRegex.Inspection.DebugFallbackRegexIsCreated);

            var detailedRegex = new Utf8Regex(pattern, options);
            var expectedFirst = oracle.Match(text);
            var actualFirst = detailedRegex.MatchDetailed(input);
            Assert.Equal(expectedFirst.Success, actualFirst.Success);
            Assert.Equal(expectedFirst.Index, actualFirst.IndexInUtf16);
            Assert.True(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
    }

    private static bool IsScalarBoundary(string text, int utf16Offset)
    {
        if ((uint)utf16Offset > (uint)text.Length)
        {
            return false;
        }

        if (utf16Offset == 0 || utf16Offset == text.Length)
        {
            return true;
        }

        return !(char.IsHighSurrogate(text[utf16Offset - 1]) && char.IsLowSurrogate(text[utf16Offset]));
    }
}
