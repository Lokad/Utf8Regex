using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RepeatedSegmentProofTests
{
    [Theory]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,4}?", "Aa Bb Cc Dd Ee Ff Gg Hh")]
    [InlineData(@"(?:[A-Z][a-z]+?\s*){2,4}", "Aaaaa Bbbbb")]
    [InlineData(@"(?:[A-Z][a-z]+\s*?){2,4}", "Aa Bb Cc Dd   ")]
    [InlineData(@"(?:[a-z][a-z]+\s*){2,4}", "aaaa")]
    [InlineData(@"(?:[A-Z ][a-z]+\s*){2,4}", "Aa aa")]
    [InlineData(@"(?:[A-Z][a-z ]+\s+){2,4}", "Aa Bb ")]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,4}", "Aa BbC Dd Ee")]
    [InlineData(@"(?:[A-Z][a-z]+\s+){2,4}", "Aa Bb Cc")]
    [InlineData(@"(?<all>(?:[A-Z][a-z]+\s*){2,4}?)", "Aa Bb Cc Dd Ee Ff Gg Hh")]
    public void GreedinessRewindAndFailedSegmentEndpointsRemainScalar(string pattern, string subject)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, mode); var oracle = new Regex(pattern, mode);
            foreach (var text in new[] { subject, "😀" + subject + "é" })
            {
                var input = Encoding.UTF8.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var expected = oracle.Matches(text).Cast<Match>().ToArray();
                var actual = regex.Match(storage.AsSpan(1, input.Length)); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                Assert.Equal(expected.Length, regex.Count(input)); Assert.Equal(expected.Length != 0, regex.IsMatch(input));
                Assert.Equal(expected.Length != 0, actual.Success); Assert.Equal(actual.Success, detailed.Success);
                if (actual.Success)
                {
                    var first = expected[0];
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, first.Index)), actual.StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(first.Value), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
                    Assert.Equal(first.Value, System.Text.Encoding.UTF8.GetString(detailed.GetValueMemory().Span));
                    if (first.Groups.Count > 1) Assert.Equal(first.Groups[1].Value, System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
                }
                var cursor = regex.EnumerateMatches(input); var alias = cursor; var count = 0;
                while (cursor.MoveNext())
                {
                    Assert.Equal(Encoding.UTF8.GetByteCount(expected[count].Value), (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes));
                    count++;
                }
                Assert.Equal(expected.Length, count); count = 0;
                while (alias.MoveNext()) count++;
                Assert.Equal(expected.Length, count);
                using var token = new CancellationTokenSource();
                var controlled = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy
                { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
                Assert.Equal(expected.Length, controlled.Count(input, token.Token));
                token.Cancel();
            }
        }
    }
}
