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
        Assert.Equal(expected, regex.FullMatch(input).Success);
        Assert.Equal(expected, regex.FullMatchDetailedData(input).Success);
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void CapturesAndRetainedCursorSnapshotsSurviveOffsetsAndTails(int length)
    {
        var value = new string('x', length) + "é😀";
        var input = Encoding.UTF8.GetBytes("ab" + value + "a𝒜a");
        using var regex = new Utf8PythonRegex(@"(?P<x>[^ab]+)a");
        using var cursor = regex.CreateMatchCursor(input, 2);
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        Assert.Equal(value, retained.Groups[1].ValueText);
        Assert.True(alias.MoveNext());
        Assert.Equal("𝒜", alias.Current.Groups[1].ValueText);
        cursor.Dispose();
        Assert.Equal(value, retained.Groups[1].ValueText);
        Assert.Equal(2, regex.Count(input, 2));
        Assert.Equal(value + "a", regex.SearchDetailedData(input, 2).Value.ValueText);
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

    [Fact]
    public void EarlyMatchesDoNotAdmitMalformedSuffixes()
    {
        using var regex = new Utf8PythonRegex(@"[^ab]+a");
        var input = new byte[] { (byte)'x', (byte)'a', 0xFF };
        Assert.Throws<ArgumentException>(() => regex.IsMatch(input));
        Assert.Throws<ArgumentException>(() => regex.Search(input));
        Assert.Throws<ArgumentException>(() => regex.Count(input));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptedDiscoveryKeepsOriginalTokenDeadlineAndRetainedResult(bool expire)
    {
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
            Assert.True(cursor.MoveNext());
            var retained = cursor.Current;
            armed = true;
            var failure = Record.Exception(() => cursor.MoveNext());
            Assert.True(interrupted);
            if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            cursor.Dispose();
            Assert.Equal("xa", retained.Value.ValueText);
        }
        finally { session.Complete(); }
    }
}
