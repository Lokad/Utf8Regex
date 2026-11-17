using Lokad.Utf8Regex.TestSupport;
using System.Threading;
using static Lokad.Utf8Regex.TestSupport.ProfileDifferential;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class ProfileDifferentialTests
{
    [Theory]
    [InlineData("default")]
    [InlineData("token")]
    [InlineData("timeout")]
    [InlineData("budget")]
    [InlineData("all")]
    public void SharedLanguagePreservesByteMatchesAndCaptures(string mode)
        => ProfileDifferential.Run(mode, (pattern, timeout, policy, groups) =>
        {
            var regex = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default, policy, timeout ?? Timeout.InfiniteTimeSpan);
            return (input, start, token) =>
            {
                var exists = regex.IsMatch(input, 0, Pcre2MatchOptions.None, token);
                var count = regex.Count(input, 0, Pcre2MatchOptions.None, token);
                var first = regex.Match(input, start, Pcre2MatchOptions.None, token);
                var firstRange = first.Success ? new ByteRange(true, first.StartOffsetInBytes, first.EndOffsetInBytes) : default;
                var matches = new List<ByteRange[]>();
                using var cursor = regex.CreateMatchCursor(input, start, cancellationToken: token);
                while (cursor.MoveNext(out var match))
                {
                    var ranges = new ByteRange[groups];
                    for (var i = 0; i < groups; i++)
                    {
                        Assert.True(match.TryGetGroup(i, out var capture));
                        ranges[i] = capture.Success ? new(true, capture.StartOffsetInBytes, capture.EndOffsetInBytes) : default;
                    }
                    matches.Add(ranges);
                }
                return new(exists, count, firstRange, matches);
            };
        });
}
