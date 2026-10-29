using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReRangeResultTests
{
    [Theory]
    [InlineData(@"(?P<word>\w+)(?P<tail>!)?", "😀é! two", 0)]
    [InlineData(@"(?P<outer>(?P<inner>a))", "😀a", 4)]
    [InlineData(@"(?<=(?P<prefix>é))a|(?P<other>🙂)", "éa🙂", 0)]
    [InlineData(@"(?P<empty>)|(?P<letter>a)", "a😀", 0)]
    [InlineData(@"(?P<none>z)", "é😀", 0)]
    public void RangesMatchOwnedCaptureMetadataAndScalarProgression(string pattern, string text, int start)
    {
        var regex = new Utf8PythonRegex(pattern); var bytes = Encoding.UTF8.GetBytes(text);
        byte[] padded = [0xff, .. bytes, 0xff]; var input = padded.AsMemory(1, bytes.Length);
        var expected = regex.FindIterDetailed(input.Span, start);
        var retained = new List<Utf8PythonRangeMatch>();
        using var cursor = regex.CreateMatchCursor(input, start); var alias = cursor;
        foreach (var match in expected)
        {
            Assert.True(alias.MoveNext(out var ranges)); retained.Add(ranges);
            Assert.False(cursor.HasCoordinateMap);
            Assert.Throws<InvalidOperationException>(() => cursor.Current);
            Assert.Equal(match.LastIndex, ranges.LastIndex); Assert.Equal(match.LastGroup, ranges.LastGroup);
            Assert.Equal(match.NameEntries, ranges.NameEntries.ToArray());
            Assert.Equal(match.Groups.Length, ranges.Groups.Length);
            for (var number = 0; number < match.Groups.Length; number++)
            {
                Assert.True(ranges.TryGetGroup(number, out var actual)); var wanted = match.Groups[number];
                Assert.Equal(wanted.Success, actual.Success); Assert.Equal(number, actual.Number);
                Assert.Equal(wanted.Success ? wanted.StartOffsetInBytes : 0, actual.StartOffsetInBytes);
                Assert.Equal(wanted.Success ? wanted.EndOffsetInBytes : 0, actual.EndOffsetInBytes);
                Assert.Equal(wanted.ValueText, Encoding.UTF8.GetString(ranges.GetGroupMemory(number).Span));
            }
            foreach (var name in ranges.NameEntries)
            {
                Assert.True(ranges.TryGetFirstSetGroup(name.Name, out var group)); Assert.Equal(name.Number, group.Number);
            }
        }
        Assert.False(cursor.MoveNext(out var none)); Assert.False(none.Success);
        alias.Dispose();
        for (var i = 0; i < retained.Count; i++)
            Assert.Equal(expected[i].Value.ValueText, Encoding.UTF8.GetString(retained[i].GetValueMemory().Span));
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
    }

    [Fact]
    public void OwnedAndRangeAdvancesShareOnePositionWithoutChangingRetainedValues()
    {
        using var cursor = new Utf8PythonRegex("(?P<x>.)").CreateMatchCursor("é😀a"u8.ToArray());
        Assert.True(cursor.MoveNext()); var owned = cursor.Current;
        Assert.True(cursor.MoveNext(out var ranges)); Assert.Equal("😀", Encoding.UTF8.GetString(ranges.GetValueMemory().Span));
        Assert.Throws<InvalidOperationException>(() => cursor.Current);
        Assert.True(cursor.MoveNext()); Assert.Equal("a", cursor.Current.Value.ValueText);
        cursor.Dispose(); Assert.Equal("é", owned.Value.ValueText);
        Assert.Equal("😀", Encoding.UTF8.GetString(ranges.GetGroupMemory(1).Span));
    }

    [Theory]
    [MemberData(nameof(PythonReSplitOwnershipTests.OwnedCases), MemberType = typeof(PythonReSplitOwnershipTests))]
    public void RangeSplitsFollowCpythonFixtures(string pattern, PythonReCompileOptions options, string text,
        int maxSplit, int start, string?[] expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern, options);
        var ranges = regex.SplitRanges(input, maxSplit, start, default);
        Assert.Equal(expected, ranges.Select(range => range.Success
            ? Encoding.UTF8.GetString(input.AsSpan(range.StartOffsetInBytes, range.EndOffsetInBytes - range.StartOffsetInBytes)) : null));
        var groupCount = regex.GetGroupNames().Length - 1;
        for (var i = 0; i < ranges.Length; i++) Assert.Equal(i % (groupCount + 1), ranges[i].Number);
    }

    [Fact]
    public void LargeCapturesAndNoSeparatorSplitsDoNotAllocateTextOrCoordinateMaps()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = 16 << 10, MaxResultBytes = 4 << 10 };
        var capture = new Utf8PythonRegex("(?P<all>.+)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
        var split = new Utf8PythonRegex("z", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
        var input = Encoding.UTF8.GetBytes(new string('é', 1 << 20) + "😀");
        using (var warm = capture.CreateMatchCursor("é😀"u8.ToArray())) Assert.True(warm.MoveNext(out _));
        _ = split.SplitRanges("é😀"u8);
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var cursor = capture.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var range)); Assert.False(cursor.HasCoordinateMap);
        var parts = split.SplitRanges(input);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 64 << 10);
        Assert.Equal(input.Length, range.GetGroupMemory(1).Length);
        Assert.Single(parts); Assert.True(parts[0].Success);
        Assert.Equal(0, parts[0].StartOffsetInBytes); Assert.Equal(input.Length, parts[0].EndOffsetInBytes);
    }

    [Fact]
    public void DiscardingHalfAMillionRangesKeepsActiveCapacityBounded()
    {
        var regex = new Utf8PythonRegex("(.)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 16 << 10 });
        using var cursor = regex.CreateMatchCursor(Enumerable.Repeat((byte)'a', 500000).ToArray());
        var count = 0;
        while (cursor.MoveNext(out var range)) { Assert.Equal(count, range.Value.StartOffsetInBytes); count++; }
        Assert.Equal(500000, count); Assert.False(cursor.HasCoordinateMap);
        var capped = new Utf8PythonRegex("(.)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 16 << 10, MaxResultBytes = 4096 });
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
        using var timed = new Utf8PythonRegex("(.)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(100)).CreateMatchCursor("ab"u8.ToArray());
        Assert.True(timed.MoveNext(out _)); ticks = 100;
        Assert.Throws<RegexMatchTimeoutException>(() => timed.MoveNext(out _));
    }

    [Fact]
    public void RangeEntryPointsKeepStrictValidationAndFailureSemantics()
    {
        var regex = new Utf8PythonRegex("a"); byte[] malformed = [(byte)'a', 0xff];
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(malformed));
        Assert.Throws<ArgumentException>(() => regex.SplitRanges(malformed));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.SplitRanges("😀a"u8, 0, 1, default));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.SplitRanges(malformed, 0, 0, token.Token)).CancellationToken);
        Assert.Single(regex.SplitRanges("é😀"u8, -1, 0, default));
        var none = default(Utf8PythonRangeMatch);
        Assert.False(none.Success); Assert.True(none.Groups.IsEmpty); Assert.True(none.NameEntries.IsEmpty);
        Assert.Null(none.LastIndex); Assert.Null(none.LastGroup); Assert.True(none.GetValueMemory().IsEmpty);
        Assert.False(none.TryGetGroup(0, out _)); Assert.False(none.TryGetFirstSetGroup("x", out _));
    }
}
