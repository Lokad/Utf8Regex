using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RepeatedSegmentProofTests
{
    [Fact]
    public void DelimitedRunsRetainCaptureHistoryAfterRetry()
    {
        const string pattern = @"\A(?<label>[a-z]+\.)+(?<tail>[a-z]+\.end)$";
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["a.b.c.end", "a.b.c.end\n", "a.b.end", "a.b.c.end#", "a.b.é.end", "😀a.b.c.end"], options);
    }

    [Fact]
    public void DelimitedBooleanRunOnlyNeedsRetryStorage()
    {
        const string pattern = @"\A(?<label>[a-z]+\.)+[a-z]{2,12}$";
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 8, MaxWorkSteps = 1_000, MaxWorkingBytes = 800 }
        });
        Assert.True(regex.IsMatch("northwind.control.net"u8));
        Assert.True(regex.IsMatch("northwind.control.net\n"u8));
        Assert.False(regex.IsMatch("northwind.control.net#"u8));
    }

    [Fact]
    public void FixedCapturedRunsPreserveEveryCaptureAndTheFinalBackreference()
    {
        const string pattern = @"(?<part>\d{4}[- ]){3}\d{3,4}\k<part>";
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["1234-5678-9876-4569876-", "١٢٣٤ 5678-9876-4569876-", "1234-5678-9876-4561234-"], options);
    }

    [Fact]
    public void FixedCapturedRunsKeepSupplementaryDigitByteCoordinates()
    {
        using var regex = Utf8Regex.Compile(@"(?<part>\d{4}[- ]){3}\d{3,4}");
        var bytes = "é𝟙𝟚𝟛𝟜-𝟝𝟞𝟟𝟠 𝟡𝟘𝟙𝟚-𝟛𝟜𝟝"u8.ToArray();
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(bytes, startOffsetInBytes: 2));
        Assert.True(match.Success);
        Assert.Equal((2, 65), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal([(2, 19), (19, 36), (36, 53)], match.GetCaptures(1).ToArray()
            .Select(range => (range.StartOffsetInBytes, range.EndOffsetInBytes)).ToArray());
    }

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
    [InlineData(@"(?<all>(?:ab|a|bc)+)z", "abcz ababaz aaax")]
    [InlineData(@"(?:😀é|😀|é)+z", "😀é😀z 😀x éz")]
    [InlineData(@"(?:ab|a?)+z", "z abz xz")]
    [InlineData(@"(?:ab|a){1,3}z", "abababaz az aaaz")]
    [InlineData(@"(?:ab|a)+?b", "aab ab abab")]
    public void GreedinessRewindAndFailedSegmentEndpointsRemainScalar(string pattern, string subject)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options: mode); var oracle = new Regex(pattern, mode);
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
                var controlled = new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = new Utf8ResourcePolicy
                    {
                        MaxWorkSteps = 100000,
                        MaxDepth = 256,
                        MaxWorkingBytes = 1 << 20,
                        MaxResultBytes = 1 << 20
                    }
                });
                Assert.Equal(expected.Length, controlled.Count(input, token.Token));
                token.Cancel();
            }
        }
    }
}
