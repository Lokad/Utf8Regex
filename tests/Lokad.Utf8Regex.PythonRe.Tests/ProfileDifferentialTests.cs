using Lokad.Utf8Regex.TestSupport;
using static Lokad.Utf8Regex.TestSupport.ProfileDifferential;

namespace Lokad.Utf8Regex.PythonRe.Tests;

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
            var regex = new Utf8PythonRegex(pattern, matchTimeout: timeout, resourcePolicy: policy);
            return (input, start, token) =>
            {
                var exists = regex.IsMatch(input, 0, token);
                var count = regex.Count(input, 0, token);
                var first = regex.Match(input, start, cancellationToken: token, mode: Utf8PythonMatchMode.Search);
                var firstRange = first.Success ? new ByteRange(true, first.StartOffsetInBytes, first.EndOffsetInBytes) : default;
                var matches = new List<ByteRange[]>();
                using var cursor = regex.CreateMatchCursor(input, start, token);
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
