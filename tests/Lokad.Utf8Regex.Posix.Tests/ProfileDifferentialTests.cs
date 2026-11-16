using Lokad.Utf8Regex.TestSupport;
using static Lokad.Utf8Regex.TestSupport.ProfileDifferential;

namespace Lokad.Utf8Regex.Posix.Tests;

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
            // REG_NEWLINE and explicit-LF negated sets match this oracle's dot.
            var regex = new Utf8PosixRegex(pattern, PosixCompileOptions.Newline,
                matchTimeout: timeout, resourcePolicy: policy);
            return (input, start, token) =>
            {
                var exists = regex.IsMatch(input, token);
                var count = regex.Count(input, cancellationToken: token);
                var captures = new Utf8CaptureRange[groups];
                var first = regex.Match(input, captures, start, token);
                var firstRange = Range(first);
                var matches = new List<ByteRange[]>();
                using var cursor = regex.CreateMatchCursor(input, start, token);
                while (cursor.MoveNext(captures, out _)) matches.Add(captures.Select(Range).ToArray());
                return new(exists, count, firstRange, matches);
            };
        });

    private static ByteRange Range(Utf8CaptureRange value) => value.Success
        ? new(true, value.StartOffsetInBytes, value.StartOffsetInBytes + (value.EndOffsetInBytes - value.StartOffsetInBytes)) : default;
}
