using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLiteralAtomRepeatTests
{
    [Theory]
    [InlineData("x*", "yy", new[] { "", "", "" })]
    [InlineData("x*", "xxyx", new[] { "xx", "", "x", "" })]
    [InlineData("(?:😀)*", "😀😀é😀", new[] { "😀😀", "", "😀", "" })]
    [InlineData("é{0,2}", "éééx", new[] { "éé", "é", "", "" })]
    [InlineData("x*", "yé😀x", new[] { "", "", "", "x", "" })]
    [InlineData("x{0,2}", "xxx", new[] { "xx", "x", "" })]
    [InlineData("x*", "", new[] { "" })]
    [InlineData("(x*)", "xxy", new[] { "xx", "", "" })]
    [InlineData("(x)*", "xxy", new[] { "x", "", "" })]
    [InlineData("x*?y", "xxy", new[] { "xxy" })]
    [InlineData("x*x", "xxx", new[] { "xxx" })]
    [InlineData("(?i)x*", "XXy", new[] { "XX", "", "" })]
    [InlineData("(?:x*|y)", "y", new[] { "", "y", "" })]
    [InlineData("(?<=x{2})y", "xxy", new[] { "y" })]
    [InlineData("x{2}x", "xxxx", new[] { "xxx" })]
    [InlineData("\0*", "\0\0x", new[] { "\0\0", "", "" })]
    public void PublicFindAllKeepsValuesCapturesAndEmptyProgression(string pattern, string text, string[] expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var policy in new[] { default(Utf8ResourcePolicy), new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000,MaxDepth = 256,            MaxWorkingBytes = 1 << 20,MaxResultBytes = 1 << 20        } })
        {
            var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = policy
            });
            using var token = new CancellationTokenSource();
            ReadOnlyMemory<byte> snapshots1Input = input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, token.Token))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            Assert.Equal(expected, snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
            ReadOnlyMemory<byte> snapshots2Input = input;
            var snapshots2 = new List<Utf8PythonRangeMatch>();
            using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, token.Token))
            {
                while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                    snapshots2.Add(snapshots2Match);
            }
            Assert.Equal(expected, snapshots2.Select(captureSnapshot2 => captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
            Assert.Equal(expected.Length, regex.Count(input, 0, token.Token));
            var collectedRanges1 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor1 = regex.CreateMatchCursor(input, 0, token.Token))
            {
                while (rangeCursor1.MoveNext(out var rangeMatch1))
                    collectedRanges1.Add(rangeMatch1);
            }
            Assert.Equal(expected.Length, collectedRanges1.ToArray().Length);
        }
    }

    [Fact]
    public void UnusedRepeatJournalsDoNotRemoveRequiredAdmission()
    {
        var input = Encoding.UTF8.GetBytes(new string('y', 512));
        var small = new Utf8PythonRegex("x*", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 1,
                MaxWorkingBytes = 256
            }
        });
        Assert.Equal(513, small.Count(input));
        Assert.Equal(513, new Utf8PythonRegex("x*", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        }).Count(input));
        var denied = new Utf8PythonRegex("(x*)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input)).ResourceKind);
        var result = new Utf8PythonRegex("x*", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxResultBytes = 1
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                ReadOnlyMemory<byte> snapshots3Input = input;
                var snapshots3 = new List<Utf8PythonRangeMatch>();
                using (var snapshots3Cursor = result.CreateMatchCursor(snapshots3Input, 0, default))
                {
                    while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                        snapshots3.Add(snapshots3Match);
                }
                return snapshots3;
            }).ResourceKind);
        Assert.Throws<ArgumentException>(() => small.Count(new byte[] { (byte)'y', 255 }));
    }
}
