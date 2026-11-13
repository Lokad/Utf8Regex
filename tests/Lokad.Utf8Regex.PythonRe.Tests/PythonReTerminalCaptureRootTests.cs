using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTerminalCaptureRootTests
{
    [Fact]
    public void CompleteRootPreservesStringsBytesLastIndexAndRetainedCursorAliases()
    {
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        var input = Encoding.UTF8.GetBytes("é😀item-12 item-345!");
        Assert.Equal(new[] { "12", "345" }, regex.FindAllToStrings(input).ScalarValues);
        var bytes = regex.FindAllToUtf8(input).ScalarValues;
        Assert.Equal("12"u8.ToArray(), bytes[0]); Assert.Equal("345"u8.ToArray(), bytes[1]);
        var cursor = regex.CreateMatchCursor(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); var first = alias.Current;
        Assert.Equal(1, first.LastIndex); Assert.Equal("12", first.Groups[1].ValueText);
        Assert.Equal(11, first.Groups[1].StartOffsetInBytes);
        Assert.True(alias.MoveNext()); Assert.Equal("345", cursor.Current.Groups[1].ValueText);
        Assert.Equal(1, cursor.Current.LastIndex); Assert.False(cursor.MoveNext());
        alias.Dispose(); cursor.Dispose(); Assert.Equal("12", first.Groups[1].ValueText);
    }
    [Fact]
    public void SeparatedRootPreservesTuplesClosingOrderAndRetainedAliases()
    {
        using var regex = new Utf8PythonRegex("(?P<word>[a-z]+)-(?P<number>[0-9]+)");
        var input = Encoding.UTF8.GetBytes("é😀item-12 other-345!");
        var strings = regex.FindAllToStrings(input).TupleValues;
        Assert.Equal(new[] { "item", "12" }, strings[0]); Assert.Equal(new[] { "other", "345" }, strings[1]);
        var bytes = regex.FindAllToUtf8(input).TupleValues;
        Assert.Equal("item"u8.ToArray(), bytes[0][0]); Assert.Equal("12"u8.ToArray(), bytes[0][1]);
        var cursor = regex.CreateMatchCursor(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); var first = alias.Current;
        Assert.Equal(2, first.LastIndex); Assert.Equal("number", first.LastGroup);
        Assert.Equal(6, first.Groups[1].StartOffsetInBytes); Assert.Equal(11, first.Groups[2].StartOffsetInBytes);
        Assert.True(alias.MoveNext()); Assert.Equal("other", cursor.Current.Groups[1].ValueText);
        Assert.Equal("345", cursor.Current.Groups[2].ValueText); Assert.Equal(2, cursor.Current.LastIndex);
        alias.Dispose(); cursor.Dispose(); Assert.Equal("12", first.Groups[2].ValueText);
        Assert.Equal(2, first.LastIndex); Assert.Equal("number", first.LastGroup);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LongRootObservesOriginalCancellationAndDeadline(bool expire, bool separated)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8PythonRegex(separated ? "([a-z]+)-([0-9]+)" : "item-([0-9]+)", PythonReCompileOptions.None,
            TimeSpan.FromMilliseconds(100), new() { MaxWorkSteps = 1000000, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        var bytes = Encoding.ASCII.GetBytes("item-" + new string('1', 1 << 18));
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { reached = true; if (expire) ticks = 100; else token.Cancel(); });
        try
        {
            var failure = Record.Exception(() => regex.FindAllToStrings(bytes, 0, token.Token));
            Assert.True(reached);
            if (expire) Assert.IsType<System.Text.RegularExpressions.RegexMatchTimeoutException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
    }
}
