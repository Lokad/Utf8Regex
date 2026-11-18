using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReByteOffsetCollectionTests
{
    [Theory]
    [InlineData("foo", "é😀")]
    [InlineData("foo|bar", "é😀")]
    [InlineData("foo", "ascii ")]
    [InlineData("foo|bar", "ascii ")]
    public void CursorRangesPreserveOriginalOffsets(string pattern, string prefix)
    {
        var input = Encoding.UTF8.GetBytes(prefix + " foo bar");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var expected = pattern == "foo" ? new[] { "foo" } : new[] { "foo", "bar" };
        using var live = new CancellationTokenSource();
        foreach (var mode in Enumerable.Range(0, 3))
        {
            var regex = Create(pattern, mode == 2);
            Assert.True(regex.DebugUsesUtf8RegexBackend);
            var token = mode == 1 ? live.Token : default;
            var wholeMatches3 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor3 = regex.CreateMatchCursor(input, start, token))
            {
                while (rangeCursor3.MoveNext(out var rangeMatch3))
                {
                    wholeMatches3.Add(rangeMatch3);
                }
            }
            var matches = wholeMatches3.ToArray();
            Assert.Equal(expected, matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)));
            Assert.Equal(start + 1, matches[0].Value.StartOffsetInBytes);
            Assert.Equal(start + 4, matches[0].Value.EndOffsetInBytes);
            Assert.True(regex.Match("later foo"u8, mode: Utf8PythonMatchMode.Search).Success);
            Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(matches[0].GetValueMemory().Span));
        }
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("foo|bar")]
    public void CursorChecksScalarBoundariesAndOriginalAuthority(string pattern)
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        foreach (var finite in new[] { false, true })
        {
            var regex = Create(pattern, finite);
            for (var interior = 1; interior < 4; interior++)
            {
                var offset = interior;
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    ReadOnlyMemory<byte> snapshots4Input = ("😀 foo"u8).ToArray();
                    var snapshots4 = new List<Utf8PythonRangeMatch>();
                    using (var snapshots4Cursor = regex.CreateMatchCursor(snapshots4Input, offset, default))
                    {
                        while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                            snapshots4.Add(snapshots4Match);
                    }
                    return snapshots4;
                });
            }
            ReadOnlyMemory<byte> snapshots5Input = ("😀 foo"u8).ToArray();
            int snapshots5Start = "😀 foo"u8.Length;
            var snapshots5 = new List<Utf8PythonRangeMatch>();
            using (var snapshots5Cursor = regex.CreateMatchCursor(snapshots5Input, snapshots5Start, default))
            {
                while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                    snapshots5.Add(snapshots5Match);
            }
            Assert.Empty(snapshots5.Select(captureSnapshot3 => captureSnapshot3.GetGroupMemory(captureSnapshot3.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray());

            var failure = Assert.Throws<OperationCanceledException>(() =>
            {
                ReadOnlyMemory<byte> snapshots6Input = ("😀 foo"u8).ToArray();
                var snapshots6 = new List<Utf8PythonRangeMatch>();
                using (var snapshots6Cursor = regex.CreateMatchCursor(snapshots6Input, 4, canceled.Token))
                {
                    while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                        snapshots6.Add(snapshots6Match);
                }
                return snapshots6;
            });
            Assert.Equal(canceled.Token, failure.CancellationToken);
        }
        var limited = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            }
        });
        Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            ReadOnlyMemory<byte> snapshots7Input = ("😀 foo"u8).ToArray();
            var snapshots7 = new List<Utf8PythonRangeMatch>();
            using (var snapshots7Cursor = limited.CreateMatchCursor(snapshots7Input, 4, default))
            {
                while (snapshots7Cursor.MoveNext(out var snapshots7Match))
                    snapshots7.Add(snapshots7Match);
            }
            return snapshots7;
        });
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("foo|bar")]
    public void ByteOffsetCursorDoesNotAllocateASubjectSizedMap(string pattern)
    {
        var regex = Create(pattern, false);
        var small = Encoding.UTF8.GetBytes("é" + new string('a', 63) + " foo");
        var large = Encoding.UTF8.GetBytes("é" + new string('a', 65_535) + " foo");
        for (var i = 0; i < 16; i++)
        {
            ReadOnlyMemory<byte> snapshots8Input = small;
            int snapshots8Start = small.Length - 4;
            var snapshots8 = new List<Utf8PythonRangeMatch>();
            using (var snapshots8Cursor = regex.CreateMatchCursor(snapshots8Input, snapshots8Start, default))
            {
                while (snapshots8Cursor.MoveNext(out var snapshots8Match))
                    snapshots8.Add(snapshots8Match);
            }
            _ = snapshots8;
            ReadOnlyMemory<byte> snapshots9Input = large;
            int snapshots9Start = large.Length - 4;
            var snapshots9 = new List<Utf8PythonRangeMatch>();
            using (var snapshots9Cursor = regex.CreateMatchCursor(snapshots9Input, snapshots9Start, default))
            {
                while (snapshots9Cursor.MoveNext(out var snapshots9Match))
                    snapshots9.Add(snapshots9Match);
            }
            _ = snapshots9;
        }
        var smallBytes = Measure(small);
        var largeBytes = Measure(large);
        // One result has the same payload; allow incidental metadata variation,
        // while rejecting a UTF-16 boundary map proportional to the whole input.
        Assert.InRange(largeBytes, 0, smallBytes + 4096);

        long Measure(byte[] input)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            ReadOnlyMemory<byte> snapshots10Input = input;
            int snapshots10Start = input.Length - 4;
            var snapshots10 = new List<Utf8PythonRangeMatch>();
            using (var snapshots10Cursor = regex.CreateMatchCursor(snapshots10Input, snapshots10Start, default))
            {
                while (snapshots10Cursor.MoveNext(out var snapshots10Match))
                    snapshots10.Add(snapshots10Match);
            }
            var values = snapshots10;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal("foo"u8.ToArray(), Assert.Single(values.Select(captureSnapshot4 => captureSnapshot4.GetGroupMemory(captureSnapshot4.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray()));
            return allocated;
        }
    }

    private static Utf8PythonRegex Create(string pattern, bool finite) => new(pattern, options: default, policy: new Utf8RegexPolicy()
    {
        MatchTimeout = Timeout.InfiniteTimeSpan,
        ResourcePolicy = finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000,
            MaxDepth = 1024,
            MaxWorkingBytes = 16 << 20,
            MaxResultBytes = 16 << 20
        }

    : default
    });
}
