using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// R03 split on the slice: segments project the owned match cursor with the
// shared segment/tail/count shaping. Rows cover separators, quantified and
// empty-capable separators, captures (never segments), counts including the
// remainder rule, misses, empty inputs, and astral content.
public sealed class Utf8RegexSliceSplitDiffTests
{
    public static TheoryData<string, string, int> SliceSplitCases => new()
    {
        { ",", "a,b,,c", int.MaxValue },
        { ",", "a,b,c,d", 3 },
        { ",", "abc", int.MaxValue },
        { ",", string.Empty, int.MaxValue },
        { "a+", "aaabaa", int.MaxValue },
        { "a+", "aaabaa", 2 },
        { "a+", "xyz", int.MaxValue },
        { "x*|y", "axbyc", int.MaxValue },
        { "x*|y", "abc", int.MaxValue },
        { "(a)", "baac", int.MaxValue },
        { "(a)+", "baac", int.MaxValue },
        { "(?:cat|horse)+", "cat horse cathorse", int.MaxValue },
        { "(?:cat|horse)+", "cat horse cathorse", 2 },
        { "(cat)|(horse)", "cat horse fish horse", int.MaxValue },
        { @"\d+", "a1b22c", int.MaxValue },
        { "(a)(?:b)(c)+", "xabcxabc", int.MaxValue },
        { ",", "a,b,c,d", 0 },
    };

    [Theory]
    [MemberData(nameof(SliceSplitCases))]
    public void SliceSplitMatchesRuntime(string pattern, string input, int count)
    {
        var options = RegexOptions.CultureInvariant;
        // Bare shapes keep native precedence; quantified shapes ride the owned slice arm. Either way no BCL fallback may appear: native split shaping uses owned kernels.
        var utf8Regex = new Utf8Regex(pattern, options);
        var expected = EnumerateRuntimeSplits(input, pattern, count, options);
        var actual = EnumerateUtf8Splits(utf8Regex, input, count);
        Assert.Equal(expected, actual);
        if (utf8Regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.False(utf8Regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    [Theory]
    [InlineData(",", "a\U0001F600,b")]
    [InlineData("a+", "\U0001F600aa\U0001F600")]
    [InlineData("x*|y", "a\U0001F600b")]
    public void SliceSplitMatchesRuntimeOnAstralInput(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);
        var expected = EnumerateRuntimeSplits(input, pattern, int.MaxValue, options);
        var actual = EnumerateUtf8Splits(utf8Regex, input, int.MaxValue);
        Assert.Equal(expected, actual);
        if (utf8Regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.False(utf8Regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    private static List<(string Value, int Index, int Length)> EnumerateRuntimeSplits(
        string input,
        string pattern,
        int count,
        RegexOptions options)
    {
        var regex = new Regex(pattern, options);
        var values = new List<(string Value, int Index, int Length)>();
        foreach (var range in regex.EnumerateSplits(input, count))
        {
            var offset = range.Start.GetOffset(input.Length);
            var value = input[range];
            values.Add((value, offset, value.Length));
        }

        return values;
    }

    private static List<(string Value, int Index, int Length)> EnumerateUtf8Splits(Utf8Regex utf8Regex, string input, int count)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var values = new List<(string Value, int Index, int Length)>();
        foreach (var split in utf8Regex.EnumerateSplits(bytes, count))
        {
            values.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        return values;
    }
}