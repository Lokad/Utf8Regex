using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Split segments project the owned scalar match cursor. Captured groups do
// not appear as segments. Zero count is unlimited, one returns the whole
// subject, and negative counts fail before any matching begins. Comparators
// below use subjects and patterns whose code-unit behavior overlaps scalars.
public sealed class Utf8SliceSplitTests
{
    public static TheoryData<string, string, int> SplitCases => new()
    {
        { ",", "a,b,,c", int.MaxValue },
        { ",", "a,b,c,d", 3 },
        { ",", "a,b,c,d", 0 },
        { ",", "abc", int.MaxValue },
        { ",", string.Empty, int.MaxValue },
        { ",", "a\U0001F600,b", int.MaxValue },
        { "a+", "aaabaa", int.MaxValue },
        { "a+", "aaabaa", 2 },
        { "a+", "xyz", int.MaxValue },
        { "a+", "axyza", int.MaxValue },
        { "a+", "\U0001F600aa\U0001F600", int.MaxValue },
        { "x*|y", "axbyc", int.MaxValue },
        { "x*|y", "abc", int.MaxValue },
        { "(a)", "baac", int.MaxValue },
        { "(a)+", "baac", int.MaxValue },
        { "(?:cat|horse)", "cat horse cathorse", int.MaxValue },
        { "(?:cat|horse)", "cat horse cathorse", 2 },
        { "(?:cat|horse)+", "cat horse cathorse", int.MaxValue },
        { "(?:cat|horse)+", "cat horse cathorse", 2 },
        { "(cat)|(horse)", "cat horse fish horse", int.MaxValue },
        { @"\d+", "a1b22c", int.MaxValue },
        { "(a)(?:b)(c)", "xabcxabc", int.MaxValue },
        { "(a)(?:b)(c)+", "xabcxabc", int.MaxValue },
        { @"\p{L}*", "123", int.MaxValue },
        { @"[\p{L}a]*", "1a", int.MaxValue },
        { @"\P{L}*", "ab", int.MaxValue },
        { @"x*|\p{L}", "ax", int.MaxValue },
        { @"(?:\p{L}|)*", "ab", int.MaxValue },
        { @"\t+", "a\t\tb\tc", int.MaxValue },
        { @"\n+", "a\nb", int.MaxValue },
        { @"\cA+", "a\u0001b", int.MaxValue },
    };

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void SplitSegmentsMatchOracle(string pattern, string inputText, int count)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = new List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(inputText, count))
        {
            var offset = range.Start.GetOffset(inputText.Length);
            var value = inputText[range];
            expected.Add((value, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), Encoding.UTF8.GetByteCount(value)));
        }

        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            using var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(expected, ReadSegments(regex, input, count));
        }
    }

    [Theory]
    [InlineData(",", "a,b")]
    [InlineData("a+", "aaabaa")]
    public void CountOneReturnsWholeInput(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        using var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.Equal([(inputText, 0, input.Length)], ReadSegments(regex, input, 1));
    }

    [Fact]
    public void NegativeCountFailsAtEntry()
    {
        using var regex = new Utf8Regex(",", options: RegexOptions.CultureInvariant);
        Assert.Throws<ArgumentOutOfRangeException>("count", () => { _ = regex.EnumerateSplits("a,b"u8, -1); });
    }

    private static List<(string Value, int Index, int Length)> ReadSegments(
        Utf8Regex regex, ReadOnlySpan<byte> input, int count)
    {
        var segments = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            segments.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }
        return segments;
    }
}
