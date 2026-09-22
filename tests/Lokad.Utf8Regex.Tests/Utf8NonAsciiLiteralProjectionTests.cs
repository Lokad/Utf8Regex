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
