using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RepeatedSegmentProofTests
{
    [Theory]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,4}?", "Aa Bb Cc Dd Ee Ff Gg Hh", false)]
    [InlineData(@"(?:[A-Z][a-z]+?\s*){2,4}", "Aaaaa Bbbbb", false)]
    [InlineData(@"(?:[A-Z][a-z]+\s*?){2,4}", "Aa Bb Cc Dd   ", false)]
    [InlineData(@"(?:[a-z][a-z]+\s*){2,4}", "aaaa", false)]
    [InlineData(@"(?:[A-Z ][a-z]+\s*){2,4}", "Aa aa", false)]
    [InlineData(@"(?:[A-Z][a-z ]+\s+){2,4}", "Aa Bb ", false)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,4}", "Aa BbC Dd Ee", true)]
    [InlineData(@"(?:[A-Z][a-z]+\s+){2,4}", "Aa Bb Cc", true)]
    [InlineData(@"(?<all>(?:[A-Z][a-z]+\s*){2,4}?)", "Aa Bb Cc Dd Ee Ff Gg Hh", false)]
    public void NativeProofPreservesGreedinessRewindAndFailedSegmentEndpoints(string pattern, string subject, bool native)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, mode); var oracle = new Regex(pattern, mode);
            if (mode is RegexOptions.None or RegexOptions.Compiled)
                Assert.Equal(native, regex.Inspection.PreparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralRepeatedSegment);
            foreach (var text in new[] { subject, "😀" + subject + "é" })
            {
                var input = Encoding.UTF8.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var expected = oracle.Matches(text).Cast<Match>().ToArray();
                var actual = regex.Match(storage.AsSpan(1, input.Length)); var detailed = regex.MatchDetailed(input);
                Assert.Equal(expected.Length, regex.Count(input)); Assert.Equal(expected.Length != 0, regex.IsMatch(input));
                Assert.Equal(expected.Length != 0, actual.Success); Assert.Equal(actual.Success, detailed.Success);
                if (actual.Success)
                {
                    var first = expected[0];
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, first.Index)), actual.IndexInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(first.Value), actual.LengthInBytes);
                    Assert.Equal(first.Value, detailed.GetValueString());
                    if (first.Groups.Count > 1) Assert.Equal(first.Groups[1].Value, detailed.GetGroup(1).GetValueString());
                }
                var cursor = regex.EnumerateMatches(input); var alias = cursor; var count = 0;
                while (cursor.MoveNext())
                {
                    Assert.Equal(Encoding.UTF8.GetByteCount(expected[count].Value), cursor.Current.LengthInBytes);
                    count++;
                }
                Assert.Equal(expected.Length, count); count = 0;
                while (alias.MoveNext()) count++;
                Assert.Equal(expected.Length, count);
                using var token = new CancellationTokenSource();
                var controlled = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy
                { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
                Assert.Equal(expected.Length, controlled.Count(input, token.Token));
                byte[] malformed = [.. input, 0xff]; Assert.Throws<ArgumentException>(() => regex.Count(malformed));
                token.Cancel();
                Assert.Equal(token.Token,
                    Assert.Throws<OperationCanceledException>(() => controlled.Count(malformed, token.Token)).CancellationToken);
            }
        }
    }
}
