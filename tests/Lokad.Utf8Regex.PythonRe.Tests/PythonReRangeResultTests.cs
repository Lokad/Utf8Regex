using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReRangeResultTests
{

    [Theory]
    [InlineData(0, 0)]
    [InlineData(253, 1)]
    [InlineData(254, 7)]
    [InlineData(255, 0)]
    [InlineData(256, 1)]
    [InlineData(257, 7)]
    [InlineData(511, 1)]
    public void CaptureByteEdgesFollowIndependentRunesAcrossBlocksAndBorrowedOffsets(int prefixLength, int borrowedOffset)
    {
        var text = new string('x', prefixLength) + "é😀a\u0301" + new string('y', 260) + "😀é\u0800\uD7FF\U0010ffff";
        var input = Encoding.UTF8.GetBytes(text);
        byte[] storage = [.. Enumerable.Repeat((byte)0xff, borrowedOffset), .. input, 0xff];
        using var cursor = new Utf8PythonRegex("(?P<x>.)").CreateMatchCursor(storage.AsMemory(borrowedOffset, input.Length));
        var position = 0;
        Utf8PythonRangeMatch retained = default;
        foreach (var rune in text.EnumerateRunes())
        {
            Assert.True(cursor.MoveNext(out var range));
            for (var number = 0; number < 2; number++)
            {
                Assert.True(range.TryGetGroup(number, out var capture));
                Assert.True(capture.Success);
                Assert.Equal((position, position + rune.Utf8SequenceLength), (capture.StartOffsetInBytes, capture.EndOffsetInBytes));
            }
            retained = range;
            position += rune.Utf8SequenceLength;
        }
        Assert.Equal(input.Length, position);
        Assert.False(cursor.MoveNext(out _));
        cursor.Dispose();
        Assert.True(retained.GetValueMemory().Span.SequenceEqual(input.AsSpan(input.Length - 4)));
    }

    [Fact]
    public void RangeAdvancesShareOnePositionWithoutChangingEarlierResults()
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var cursor = new Utf8PythonRegex("(?P<x>.)").CreateMatchCursor("é😀a"u8.ToArray());
        Assert.True(cursor.MoveNext(out cursorRange)); var earlier = cursorRange;
        Assert.True(cursor.MoveNext(out var ranges)); Assert.Equal("😀", Encoding.UTF8.GetString(ranges.GetValueMemory().Span));
        Assert.True(cursor.MoveNext(out cursorRange)); Assert.Equal("a", System.Text.Encoding.UTF8.GetString(cursorRange.GetValueMemory().Span));
        cursor.Dispose(); Assert.Equal("é", System.Text.Encoding.UTF8.GetString(earlier.GetValueMemory().Span));
        Assert.Equal("😀", Encoding.UTF8.GetString(ranges.GetGroupMemory(1).Span));
    }

    [Theory]
    [MemberData(nameof(PythonReSplitOwnershipTests.Cases), MemberType = typeof(PythonReSplitOwnershipTests))]
    public void RangeSplitsFollowCpythonFixtures(string pattern, PythonReCompileOptions options, string text,
        int maxSplit, int start, string?[] expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern, options: options);
        var ranges = regex.SplitRanges(input, maxSplit, start, default);
        Assert.Equal(expected, ranges.Select(range => range.Success
            ? Encoding.UTF8.GetString(input.AsSpan(range.StartOffsetInBytes, range.EndOffsetInBytes - range.StartOffsetInBytes)) : null));
        var groupCount = regex.CaptureGroupCount;
        for (var i = 0; i < ranges.Length; i++) Assert.Equal(i % (groupCount + 1), ranges[i].Number);
    }

    [Fact]
    public void LargeCapturesAndNoSeparatorSplitsDoNotAllocateTextOrCoordinateMaps()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = 16 << 10, MaxResultBytes = 4 << 10 };
        var capture = new Utf8PythonRegex("(?P<all>.+)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var split = new Utf8PythonRegex("z", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var input = Encoding.UTF8.GetBytes(new string('é', 1 << 20) + "😀");
        using (var warm = capture.CreateMatchCursor("é😀"u8.ToArray())) Assert.True(warm.MoveNext(out _));
        _ = split.SplitRanges("é😀"u8);
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var cursor = capture.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var range));
        var parts = split.SplitRanges(input);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 64 << 10);
        Assert.Equal(input.Length, range.GetGroupMemory(1).Length);
        Assert.Single(parts); Assert.True(parts[0].Success);
        Assert.Equal(0, parts[0].StartOffsetInBytes); Assert.Equal(input.Length, parts[0].EndOffsetInBytes);
    }

    [Fact]
    public void DiscardingHalfAMillionRangesKeepsActiveCapacityBounded()
    {
        var regex = new Utf8PythonRegex("(.)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 16 << 10
            }
        });
        using var cursor = regex.CreateMatchCursor(Enumerable.Repeat((byte)'a', 500000).ToArray());
        var count = 0;
        while (cursor.MoveNext(out var range)) { Assert.Equal(count, range.Value.StartOffsetInBytes); count++; }
        Assert.Equal(500000, count);
        var capped = new Utf8PythonRegex("(.)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 16 << 10,
                MaxResultBytes = 4096
            }
        });
        using var denied = capped.CreateMatchCursor(Enumerable.Repeat((byte)'a', 128).ToArray());
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => { while (denied.MoveNext(out _)) { } });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind);
    }

    [Fact]
    public void RangeAdvancesUseOnlyTheActiveMoveTokenAndOriginalDeadline()
    {
        using var first = new CancellationTokenSource(); using var second = new CancellationTokenSource();
        using var cursor = new Utf8PythonRegex("(z)").CreateMatchCursor(Encoding.UTF8.GetBytes("z" + new string('a', 262144)));
        Assert.True(cursor.MoveNext(out var retained, first.Token)); first.Cancel();
        var entered = false; cursor.WorkingInvocation.CheckpointHook = _ => { entered = true; second.Cancel(); };
        Assert.Equal(second.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _, second.Token)).CancellationToken);
        Assert.True(entered); Assert.Equal("z", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));

        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var timed = new Utf8PythonRegex("(.)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(100)
        }).CreateMatchCursor("ab"u8.ToArray());
        Assert.True(timed.MoveNext(out _)); ticks = 100;
        Assert.Throws<RegexMatchTimeoutException>(() => timed.MoveNext(out _));
    }

    [Fact]
    public void RangeEntryPointsPreserveScalarStartsCancellationAndDefaults()
    {
        var regex = new Utf8PythonRegex("a");
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.SplitRanges("😀a"u8, 0, 1, default));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.SplitRanges("a"u8, 0, 0, token.Token)).CancellationToken);
        Assert.Single(regex.SplitRanges("é😀"u8, -1, 0, default));
        var none = default(Utf8PythonRangeMatch);
        Assert.False(none.Success); Assert.True(none.Groups.IsEmpty);
        Assert.Null(none.LastIndex);  Assert.True(none.GetValueMemory().IsEmpty);
        Assert.False(none.TryGetGroup(0, out _));
    }
}
