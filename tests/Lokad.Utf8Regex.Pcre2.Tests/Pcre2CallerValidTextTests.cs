using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CallerValidTextTests
{
    [Theory]
    [InlineData("needle")]
    [InlineData("(needle)")]
    [InlineData("foo[0-9]+bar")]
    public void EarlyMatchingDoesNotInspectAnUnconsumedSuffix(string pattern)
    {
        var hit = pattern.StartsWith("foo", StringComparison.Ordinal) ? "foo123bar" : "needle";
        var input = Encoding.UTF8.GetBytes(hit + " " + new string('é', 65536));
        var regex = new Utf8Pcre2Regex(pattern, defaultExecutionLimits: new() { MatchLimit = 64 });
        using var source = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, 0, default, source.Token));
        using var cursor = regex.CreateMatchCursor(input, cancellationToken: source.Token);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal((0, Encoding.UTF8.GetByteCount(hit)), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(@"\bneedle", " ")]
    [InlineData(@"\Bneedle", "x")]
    [InlineData(@"(?<=ab)needle", "ab")]
    [InlineData(@"(?<!zz)needle", "ab")]
    [InlineData(@"(?<=é😀)needle", "é😀")]
    public void ByteStartsRetainOriginalUnicodeContext(string pattern, string context)
    {
        var prefix = Encoding.UTF8.GetBytes("中" + context);
        byte[] input = [.. prefix, .. "needle"u8];
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.True(regex.IsMatch(input, prefix.Length));
        Assert.Equal(1, regex.Count(input, prefix.Length));
        using var cursor = regex.CreateMatchCursor(input, prefix.Length);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal((prefix.Length, input.Length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal("needle", Encoding.UTF8.GetString(match.GetGroupMemory(0).Span));
    }

    [Fact]
    public void PatternTemplateAndOriginalCancellationChecksRemain()
    {
        var regex = Utf8Pcre2Regex.Create("needle"u8, default, default, default, Timeout.InfiniteTimeSpan);
        Assert.Throws<ArgumentException>(() => new Utf8Pcre2Regex(new byte[] { 0xff }));
        Assert.Throws<ArgumentException>(() => regex.Replace("needle"u8, new byte[] { 0xff }));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch("needle"u8, 0, default, source.Token)).CancellationToken);
    }
}
