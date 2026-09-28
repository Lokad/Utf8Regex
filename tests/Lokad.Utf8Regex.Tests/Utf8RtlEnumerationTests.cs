using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R05 right-to-left enumeration: exact literals (plain or folded) enumerate
// through the reverse-search cursor, finding the last occurrence in the
// shrinking prefix on every move and truncating the bound to its start, which
// matches BCL runtextstart advancement exactly (overlaps and adjacent hits
// included). Reverse byte search never meets multibyte scalars, and ASCII
// lengths equal UTF-16 lengths, so coordinates project like the forward cursor.
// Slice-served shapes (alternations here) ride the owned slice program instead.
// Other families, boundaries, empty literals, and Kelvin-fenced folded shapes
// keep the BCL enumerator below.
public sealed class Utf8RtlEnumerationTests
{
    public static System.Collections.Generic.IEnumerable<object?[]> RtlLiteralEnumerationOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("abc", "xxabcxxabc"),
            ("abc", "abcabc"),
            ("abc", "abc"),
            ("abc", "ab"),
            ("abc", ""),
            ("a", "aaa"),
            ("aa", "aaaa"),
            ("ab", "xabxab"),
            ("abc", "éabcé"),
            ("abc", "a\U0001F600bc"),
            ("abc", "x\u212Ay"),
            ("(?i)abc", "xxABCxxabc"),
            ("(?i)a", "xAxAx"),
            ("(?i)abc", "éABCé"),
            ("(ab|cd)", "xxabcdyy"),
            ("caf\u00e9", "xcaf\u00e9ycaf\u00e9"),
            ("caf\u00e9", "cafe"),
            ("caf\u00e9", ""),
            ("\u00e9", "x\u00e9x\u00e9"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(RtlLiteralEnumerationOwnedCases))]
    public void RtlLiteralEnumerationStaysOwned(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((expectedMatch.Index, expectedMatch.Length));
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

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> RtlLiteralEnumerationDeclineCases()
    {
        (string pattern, string text)[] rows =
        [
            ("\\babc", "xabc"),
            ("(?i)k", "x\u212Ay"),
            ("", "abc"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(RtlLiteralEnumerationDeclineCases))]
    public void RtlLiteralEnumerationDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: literal families, boundary requirements, empty
        // literals, and Kelvin-fenced folded shapes keep BCL enumeration on the
        // right-to-left lane. Parity holds; construction is pinned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((expectedMatch.Index, expectedMatch.Length));
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

        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}
