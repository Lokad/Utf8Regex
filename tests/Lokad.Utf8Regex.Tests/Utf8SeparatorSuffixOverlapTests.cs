using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SeparatorSuffixOverlapTests
{
    [Theory]
    [InlineData(@"\b(?:foo|bar)\s* ", "foo   bar   ")]
    [InlineData(@"\b(?:foo|bar)\s+ ", "foo   bar   ")]
    [InlineData(@"\b(?:foo|bar)\s+ ", "foo bar ")]
    [InlineData(@"\b(?:foo|bar)[\t ]* ", "foo \t bar\t ")]
    [InlineData(@"\b(?:foo|bar)\s*\t", "foo \t\tbar\t ")]
    [InlineData(@"\b(?:foo|bar)\s* ", "😀foo \u2003 bar   ")]
    public void SeparatorRewindsForItsLiteralSuffixInEveryMatchingLane(string pattern, string text)
    {
        var expected = new Regex(pattern).Matches(text);
        var input = Encoding.UTF8.GetBytes(text);
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var finite in new[] { false, true })
        {
            var policy = finite
                ? new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 }
                : default;
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, policy);
            Assert.Equal(expected.Count, regex.Count(input));
            Assert.Equal(expected.Count, regex.Count(input, cancellation.Token));
            Assert.Equal(expected.Count != 0, regex.IsMatch(input));
            var first = regex.Match(input);
            Assert.Equal(expected.Count != 0, first.Success);
            if (first.Success)
            {
                Assert.Equal(expected[0].Value, Encoding.UTF8.GetString(input.AsSpan(first.IndexInBytes, first.LengthInBytes)));
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[0].Index)), first.IndexInBytes);
            }
            var actual = regex.EnumerateMatches(input, cancellation.Token);
            var index = 0;
            while (actual.MoveNext())
            {
                Assert.True(index < expected.Count);
                var match = expected[index++];
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)), actual.Current.IndexInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(match.Value), actual.Current.LengthInBytes);
            }
            Assert.Equal(expected.Count, index);
        }
    }
}
