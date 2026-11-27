using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8LiteralExistenceTests
{
    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void CompleteSuffixExistencePreservesScalarsOffsetsAndOtherOperations(RegexOptions options)
    {
        foreach (var pattern in new[] { ".*(?<x>ss)", @"[^\n]*(?<x>\x73s)", ".*(?<x>a\nb)", ".*(a)(b)", ".*(?<x>ab)(?<y>cd)" })
        {
            var regex = new Utf8Regex(pattern, options: options);
            var oracle = new Regex(pattern, options);
            foreach (var text in new[] { "", "é😀", "prefixss\nss", "éss😀", "a\nb", "a", "b", "ab", "cd", "abcd" })
            {
                var padded = Encoding.UTF8.GetBytes("pad" + text + "pad");
                var input = padded.AsSpan(3, Encoding.UTF8.GetByteCount(text));
                Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
                Assert.Equal(oracle.Count(text), regex.Count(input));
                var expected = oracle.Match(text);
                var actual = regex.Match(input);
                Assert.Equal(expected.Success, actual.Success);
                if (expected.Success)
                    Assert.Equal(expected.Value, Encoding.UTF8.GetString(input.Slice(actual.StartOffsetInBytes, (actual.EndOffsetInBytes - actual.StartOffsetInBytes))));
            }
        }
        var suffix = new Utf8Regex(".*(ss)", options: options);
        Assert.Equal(1, suffix.Count("ssxxss"u8));
        Assert.False(suffix.Match("éss😀x"u8, 4, []).Success);
        Assert.True(suffix.Match("éss😀xss"u8, 4, []).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => suffix.Match("😀ss"u8, 1, []).Success);
    }

    [Fact]
    public void BooleanSearchPreservesScalarSemantics()
    {
        var regex = Limited();
        Assert.True(regex.IsMatch("é😀ss"u8));
        Assert.False(regex.IsMatch("é😀no"u8));
        foreach (var options in new[] { RegexOptions.RightToLeft, RegexOptions.NonBacktracking, RegexOptions.IgnoreCase })
            Assert.True(Limited(options).IsMatch("ss"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedSearchPollsOriginalCancellationAndDeadline(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', (1 << 20)) + "ss");
        const ulong threshold = 64;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = Limited(timeout: expire ? TimeSpan.FromMilliseconds(20) : null);
        var retained = Utf8MatchSequence.First(new Utf8Regex(".*(?<x>ss)").CreateMatchCursor("éss"u8.ToArray()));
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else source.Cancel();
        });
        Exception? failure = null;
        try { regex.IsMatch(input, source.Token); }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal("ss", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, canceled.Token)).CancellationToken);
    }

    private static Utf8Regex Limited(RegexOptions options = RegexOptions.None, TimeSpan? timeout = null) =>
        new(".*(ss)", options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout ?? Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 8192,
                MaxWorkingBytes = 8 << 20,
                MaxResultBytes = 1
            }
        });

    [Fact]
    public void CancellationFromTheLastObservedDeadlinePollCannotPublishSuccess()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) source.Cancel();
            return 0;
        });
        var regex = Limited(timeout: TimeSpan.FromSeconds(1));
        Assert.True(regex.IsMatch("ss"u8, source.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch("ss"u8, source.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
