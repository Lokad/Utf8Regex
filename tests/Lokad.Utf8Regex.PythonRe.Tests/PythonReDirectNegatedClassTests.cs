using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReDirectNegatedClassTests
{
    [Theory]
    [InlineData("[^ab]+", "é😀\n", true)]
    [InlineData("[^ab]+", "éa😀", false)]
    [InlineData("[^ab]{2}", "😀", false)]
    [InlineData("[^ab]{2}", "é😀", true)]
    [InlineData("[^é😀]", "𝒜", true)]
    [InlineData("[^é😀]", "😀", false)]
    [InlineData(@"[^\uD800-\uDFFF]", "😀", true)]
    [InlineData(@"[^\uD800]", "a", true)]
    [InlineData(@"[^\x00-\U0010ffff]", "😀", false)]
    [InlineData(@"[^\x00-\U0010ffff]", "", false)]
    [InlineData(@"[^\U00010000-\U0010ffff]+", "é\n", true)]
    [InlineData(@"[^\U00010000-\U0010ffff]+", "😀", false)]
    [InlineData(@"[^\d_]", "é", true)]
    [InlineData(@"[^\d_]", "١", false)]
    [InlineData(@"[^\D_]", "١", true)]
    [InlineData(@"[^\D_]", "é", false)]
    [InlineData(@"(?a:[^\d_])", "١", true)]
    [InlineData(@"(?i:[^k])", "K", false)]
    [InlineData(@"(?ai:[^k])", "K", true)]
    [InlineData(@"[^\^\]\\]", "^", false)]
    [InlineData(@"[^\^\]\\]", "😀", true)]
    public void ComplementUsesCompleteScalarsAndProfilePredicates(string pattern, string text, bool expected)
    {
        using var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.Match(input,mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot1 = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out snapshot1);
        }
        Assert.Equal(expected, snapshot1.Success);
    }

    [Theory]
    [InlineData(2046)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(4095)]
    public void CapturesAndRetainedCursorSnapshotsSurviveOffsetsAndTails(int length)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var value = new string('x', length) + "é😀";
        var input = Encoding.UTF8.GetBytes("ab" + value + "a𝒜a");
        using var regex = new Utf8PythonRegex(@"(?P<x>[^ab]+)a");
        using var cursor = regex.CreateMatchCursor(input, 2);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        Assert.Equal(value, System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        Assert.True(alias.MoveNext(out cursorRange));
        Assert.Equal("𝒜", System.Text.Encoding.UTF8.GetString(cursorRange.GetGroupMemory(1).Span));
        cursor.Dispose();
        Assert.Equal(value, System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        Assert.Equal(2, regex.Count(input, 2));
        Utf8PythonRangeMatch snapshot2 = default;
        using (var captureCursor2 = regex.CreateMatchCursor(input, 2, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor2.MoveNext(out snapshot2);
        }
        Assert.Equal(value + "a", System.Text.Encoding.UTF8.GetString(snapshot2.GetValueMemory().Span));
    }

    [Fact]
    public void SimpleComplementNeedsNoBacktrackingCapacity()
    {
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(@"[^ab]+a", default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 10000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(new string('é', 10000) + "a");
        Assert.True(regex.IsMatch(input, 0, token.Token));
        Assert.Equal(1, regex.Count(input, 0, token.Token));
    }



    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedDiscoveryKeepsOriginalTokenDeadlineAndRetainedResult(bool expire)
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var armed = false;
        var interrupted = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (!armed) return;
            interrupted = true;
            if (expire) ticks = 25; else cancellation.Cancel();
        });
        try
        {
            using var regex = new Utf8PythonRegex(@"[^ab]+a", default, TimeSpan.FromMilliseconds(20));
            var input = Encoding.UTF8.GetBytes("xa" + new string('é', 100000) + "a");
            using var cursor = regex.CreateMatchCursor(input, 0, cancellation.Token);
            Assert.True(cursor.MoveNext(out cursorRange));
            var retained = cursorRange;
            armed = true;
            var failure = Record.Exception(() => cursor.MoveNext(out _));
            Assert.True(interrupted);
            if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            cursor.Dispose();
            Assert.Equal("xa", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
        finally { session.Complete(); }
    }
}
