using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTerminalCaptureRootTests
{
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(65536)]
    public void FirstSeparatedRootPreservesLongRunsAndFailedCandidates(int length)
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var regex = new Utf8PythonRegex("([a-z]+)-([0-9]+)");
        var word = new string('a', length);
        var text = "é😀bad-! " + word + "-12 tail-345";
        var input = Encoding.UTF8.GetBytes(text);
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        var values = snapshots1.Select(captureSnapshot1 => Enumerable.Range(1, captureSnapshot1.Groups.Length - 1).Select(captureNumber1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureNumber1).Span)).ToArray()).ToArray();
        Assert.Equal(2, values.Length);
        Assert.Equal(new[] { word, "12" }, values[0]);
        Assert.Equal(new[] { "tail", "345" }, values[1]);
        ReadOnlyMemory<byte> snapshots2Input = Encoding.UTF8.GetBytes(word + "!");
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        Assert.Empty(snapshots2.Select(captureSnapshot2 => Enumerable.Range(1, captureSnapshot2.Groups.Length - 1).Select(captureNumber2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureNumber2).Span)).ToArray()).ToArray());
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out cursorRange));
        var first = cursorRange;
        Assert.Equal(word, System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal(Encoding.UTF8.GetByteCount("é😀bad-! "), first.Groups[1].StartOffsetInBytes);
        Assert.Equal(2, first.LastIndex);
        Assert.True(cursor.MoveNext(out cursorRange));
        Assert.Equal("tail", System.Text.Encoding.UTF8.GetString(cursorRange.GetGroupMemory(1).Span));
        Assert.False(cursor.MoveNext(out cursorRange));
        Assert.Equal(word, System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }

    [Fact]
    public void CompleteRootPreservesStringsBytesLastIndexAndRetainedCursorAliases()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        var input = Encoding.UTF8.GetBytes("é😀item-12 item-345!");
        ReadOnlyMemory<byte> snapshots3Input = input;
        var snapshots3 = new List<Utf8PythonRangeMatch>();
        using (var snapshots3Cursor = regex.CreateMatchCursor(snapshots3Input, 0, default))
        {
            while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                snapshots3.Add(snapshots3Match);
        }
        Assert.Equal(new[] { "12", "345" }, snapshots3.Select(captureSnapshot3 => System.Text.Encoding.UTF8.GetString(captureSnapshot3.GetGroupMemory(captureSnapshot3.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots4Input = input;
        var snapshots4 = new List<Utf8PythonRangeMatch>();
        using (var snapshots4Cursor = regex.CreateMatchCursor(snapshots4Input, 0, default))
        {
            while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                snapshots4.Add(snapshots4Match);
        }
        var bytes = snapshots4.Select(captureSnapshot4 => captureSnapshot4.GetGroupMemory(captureSnapshot4.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray();
        Assert.Equal("12"u8.ToArray(), bytes[0]);
        Assert.Equal("345"u8.ToArray(), bytes[1]);
        var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var first = cursorRange;
        Assert.Equal(1, first.LastIndex);
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal(11, first.Groups[1].StartOffsetInBytes);
        Assert.True(alias.MoveNext(out cursorRange));
        Assert.Equal("345", System.Text.Encoding.UTF8.GetString(cursorRange.GetGroupMemory(1).Span));
        Assert.Equal(1, cursorRange.LastIndex);
        Assert.False(cursor.MoveNext(out cursorRange));
        alias.Dispose();
        cursor.Dispose();
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }
    [Fact]
    public void SeparatedRootPreservesTuplesClosingOrderAndRetainedAliases()
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var regex = new Utf8PythonRegex("(?P<word>[a-z]+)-(?P<number>[0-9]+)");
        var input = Encoding.UTF8.GetBytes("é😀item-12 other-345!");
        ReadOnlyMemory<byte> snapshots5Input = input;
        var snapshots5 = new List<Utf8PythonRangeMatch>();
        using (var snapshots5Cursor = regex.CreateMatchCursor(snapshots5Input, 0, default))
        {
            while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                snapshots5.Add(snapshots5Match);
        }
        var strings = snapshots5.Select(captureSnapshot5 => Enumerable.Range(1, captureSnapshot5.Groups.Length - 1).Select(captureNumber5 => System.Text.Encoding.UTF8.GetString(captureSnapshot5.GetGroupMemory(captureNumber5).Span)).ToArray()).ToArray();
        Assert.Equal(new[] { "item", "12" }, strings[0]);
        Assert.Equal(new[] { "other", "345" }, strings[1]);
        ReadOnlyMemory<byte> snapshots6Input = input;
        var snapshots6 = new List<Utf8PythonRangeMatch>();
        using (var snapshots6Cursor = regex.CreateMatchCursor(snapshots6Input, 0, default))
        {
            while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                snapshots6.Add(snapshots6Match);
        }
        var bytes = snapshots6.Select(captureSnapshot6 => Enumerable.Range(1, captureSnapshot6.Groups.Length - 1).Select(captureNumber6 => captureSnapshot6.GetGroupMemory(captureNumber6).ToArray()).ToArray()).ToArray();
        Assert.Equal("item"u8.ToArray(), bytes[0][0]);
        Assert.Equal("12"u8.ToArray(), bytes[0][1]);
        var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var first = cursorRange;
        Assert.Equal(2, first.LastIndex);
        Assert.Equal(6, first.Groups[1].StartOffsetInBytes);
        Assert.Equal(11, first.Groups[2].StartOffsetInBytes);
        Assert.True(alias.MoveNext(out cursorRange));
        Assert.Equal("other", System.Text.Encoding.UTF8.GetString(cursorRange.GetGroupMemory(1).Span));
        Assert.Equal("345", System.Text.Encoding.UTF8.GetString(cursorRange.GetGroupMemory(2).Span));
        Assert.Equal(2, cursorRange.LastIndex);
        alias.Dispose();
        cursor.Dispose();
        Assert.Equal("12", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(2).Span));
        Assert.Equal(2, first.LastIndex);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LongRootObservesOriginalCancellationAndDeadline(bool expire, bool separated)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8PythonRegex(separated ? "([a-z]+)-([0-9]+)" : "item-([0-9]+)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(100),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        var bytes = Encoding.ASCII.GetBytes("item-" + new string('1', 1 << 18));
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            reached = true;
            if (expire)
            {
                ticks = 100;
            }
            else
            {
                token.Cancel();
            }
        });
        try
        {
            var failure = Record.Exception(() =>
            {
                ReadOnlyMemory<byte> snapshots7Input = bytes;
                var snapshots7 = new List<Utf8PythonRangeMatch>();
                using (var snapshots7Cursor = regex.CreateMatchCursor(snapshots7Input, 0, token.Token))
                {
                    while (snapshots7Cursor.MoveNext(out var snapshots7Match))
                        snapshots7.Add(snapshots7Match);
                }
                return snapshots7;
            });
            Assert.True(reached);
            if (expire)
            {
                Assert.IsType<System.Text.RegularExpressions.RegexMatchTimeoutException>(failure);
            }
            else
            {
                Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            }
        }
        finally
        {
            session.Complete();
        }
    }
}
