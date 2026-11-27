using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8CountSubjectTests
{
    [Theory]
    [InlineData("é")] [InlineData("😀")] [InlineData("夏洛克·福尔摩斯")]
    [InlineData("abcé")] [InlineData("needle")] [InlineData("é|a")]
    public void LiteralCountsUseBorrowedAsciiAndUnicodeTails(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var reference = new Regex(pattern, options);
            using var regex = new Utf8Regex(pattern, options: options);
            foreach (var length in new[] { 0, 1, 15, 16, 31, 32, 63, 64, 127, 128, 4095, 4096 })
            foreach (var tail in new[] { "", "é😀 夏洛克·福尔摩斯 abcé needle" })
            {
                var text = new string('a', length) + tail;
                var bytes = Encoding.UTF8.GetBytes(text);
                var storage = new byte[bytes.Length + 13];
                Array.Fill(storage, (byte)0xff);
                bytes.CopyTo(storage.AsSpan(5));
                var expected = reference.Count(text);
                Assert.Equal(expected, regex.Count(storage.AsSpan(5, bytes.Length)));
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnicodeLiteralMissKeepsOriginalWorkAndInterruption(bool expire)
    {
        var input = Encoding.ASCII.GetBytes(new string('a', 262144));
        {
            var limited = new Utf8Regex("夏洛克·福尔摩斯", policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new()
                {
                    MaxDepth = 4096
                }
            });
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            using var source = new CancellationTokenSource();
            var regex = new Utf8Regex("夏洛克·福尔摩斯", policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromMilliseconds(20)
            });
            var entered = false;
            var session = Utf8SearchDiagnosticsSession.Start(_ =>
            {
                entered = true;
                if (expire) ticks = 50;
                else source.Cancel();
            });
            try
            {
                if (expire) Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, source.Token));
                else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
                Assert.True(entered);
            }
            finally { session.Complete(); }
        }
    }

    [Theory]
    [InlineData("abc[0-9]{2}", "abc12 abc34")]
    [InlineData("abc[0-9]{2}", "é😀abc12中abc34")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "class Alpha record Beta")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "é😀 class Alpha 中record Wrong record Good")]
    public void FinitePublicCountUsesTheSameScalarSemantics(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new()
                {
                    MaxDepth = 10000,
                    MaxWorkingBytes = 1 << 24,
                    MaxResultBytes = 1 << 24
                }
            });
            using var live = new CancellationTokenSource();
            Assert.Equal(new Regex(pattern, mode).Count(text), regex.Count(input, live.Token));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicBlockInterruptionKeepsTheOriginalTokenAndDeadline(bool expire)
    {
        var input = Encoding.ASCII.GetBytes(new string('x', 262144) + "abc12");
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Regex("abc[0-9]{2}", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1 << 20
            }
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= 2) return;
            entered = true;
            if (expire) ticks = 50;
            else cancellation.Cancel();
        });
        try
        {
            if (expire) Assert.Equal("abc[0-9]{2}", Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input)).Pattern);
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
        ticks = 0;
        Assert.Equal(1, regex.Count("abc12"u8));
        var denied = new Utf8Regex("abc[0-9]{2}", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1 << 20
            }
        });
    }
}
