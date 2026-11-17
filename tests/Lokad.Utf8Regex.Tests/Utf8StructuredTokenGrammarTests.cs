using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuredTokenGrammarTests
{

    [Fact]
    public void ScalarNulIsARealOptionalDelimiterAndEmptyTailStillConsumesItsDelimiter()
    {
        var input = "😀 a:ab\0xx Ω"u8;
        var regex = new Utf8Regex("[ab]+:[ab]+[ab12]+(?:\0[x]*)?");
        var match = regex.Match(input);
        Assert.Equal((5, 12), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(1, regex.Count(input));
        var emptyTail = new Utf8Regex("[ab]+:[ab]+[ab12]+(?:\0)?").Match(input);
        Assert.Equal((5, 10), (emptyTail.StartOffsetInBytes, emptyTail.EndOffsetInBytes));
        Assert.False(regex.Match(input, 6, Span<Utf8CaptureRange>.Empty).Success);
    }

    [Fact]
    public void FirstAndSecondBodyRunsPreserveGreedyScalarRegexOutcomes()
    {
        foreach (var first in new[] { "a", "b", "ab" })
        foreach (var second in new[] { "a", "b", "ab" })
        {
            var pattern = "[a]+:[" + first + "]+[" + second + "]+";
            var regex = new Utf8Regex(pattern);
            var oracle = new Regex(pattern);
            foreach (var text in Subjects(5))
            {
                var actual = regex.Match(Encoding.UTF8.GetBytes(text));
                var expected = oracle.Match(text);
                Assert.Equal(expected.Success, actual.Success);
                if (actual.Success) Assert.Equal((expected.Index, expected.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            }
        }
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("[a-z]+:[a-z]+[0-9]+", options);
            Assert.True(regex.IsMatch("😀 a:abc12 é"u8));
            Assert.Equal(1, regex.Count("😀 a:abc12 é"u8));
            var match = regex.Match("😀 a:abc12 é"u8);
            Assert.Equal((5, 12), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        }
    }private static IEnumerable<string> Subjects(int remaining, string prefix)
    {
        yield return prefix;
        if (remaining == 0) yield break;
        foreach (var value in "ab:") foreach (var text in Subjects(remaining - 1, prefix + value)) yield return text;
    }
    private static IEnumerable<string> Subjects(int maximum) => Subjects(maximum, "");
}
