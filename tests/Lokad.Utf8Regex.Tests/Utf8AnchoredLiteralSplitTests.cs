using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AnchoredLiteralSplitTests
{
    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredSplitCases()
    {
        string[] patterns = ["^ab", "\\Aab", "ab$", "ab\\z"];
        string[] texts = ["xxab", "abxx", "ab", "xxabxx", "ab\n", "ab\ncd", "", "éab", "abé"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AnchoredSplitCases))]
    public void AnchoredSplitOwnsInput(string pattern, RegexOptions options, string text)
    {
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], start, end - start));
        }

        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    [Theory]
    [InlineData("(?m:^ab)", "ab\ncd")]
    [InlineData("(?m:^ab)", "xxab")]
    [InlineData("(?m:ab$)", "ab\ncd")]
    [InlineData("(?m:ab$)", "xxab")]
    public void MultilineAnchoredSplitKeepsBclShaping(string pattern, string text)
    {
        // Declared R05 debt: line-anchored shapes are not absolute-anchored direct
        // families, so splits stay BCL-backed. Parity holds; construction is pinned.
        const int count = int.MaxValue;
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);

            var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
            foreach (var range in oracle.EnumerateSplits(text, count))
            {
                var start = range.Start.Value;
                var end = range.End.Value;
                expected.Add((text[start..end], start, end - start));
            }

            var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
            foreach (var split in regex.EnumerateSplits(input, count))
            {
                actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
            }

            Assert.Equal(expected, actual);
            Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }
}
