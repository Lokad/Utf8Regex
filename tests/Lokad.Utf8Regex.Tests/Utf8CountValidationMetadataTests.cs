using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8CountValidationMetadataTests
{
    [Theory]
    [InlineData("é")] [InlineData("😀")] [InlineData("夏洛克·福尔摩斯")]
    [InlineData("abcé")] [InlineData("needle")] [InlineData("é|a")]
    public void LiteralCountsPreserveBothTextPoliciesAtBorrowedAsciiAndUnicodeTails(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var reference = new Regex(pattern, options);
            var strict = new Utf8Regex(pattern, options: options);
            var trusted = new Utf8Regex(pattern, options: options);
            foreach (var length in new[] { 0, 1, 15, 16, 31, 32, 63, 64, 127, 128, 4095, 4096 })
            foreach (var tail in new[] { "", "é😀 夏洛克·福尔摩斯 abcé needle" })
            {
                var text = new string('a', length) + tail;
                var bytes = Encoding.UTF8.GetBytes(text);
                var storage = new byte[bytes.Length + 13];
                Array.Fill(storage, (byte)0xff);
                bytes.CopyTo(storage.AsSpan(5));
                var expected = reference.Count(text);
                Assert.Equal(expected, strict.Count(storage.AsSpan(5, bytes.Length)));
                Assert.Equal(expected, trusted.Count(storage.AsSpan(5, bytes.Length)));
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
                    MaxWorkSteps = 1
                }
            });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input)).ResourceKind);
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
    [InlineData(0)] [InlineData(1)] [InlineData(2045)] [InlineData(2046)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4095)]
    public void BorrowedAsciiProofKeepsByteLengthAndBoundedWork(int prefix)
    {
        foreach (var suffix in new[] { "", "é", "中", "😀", "é😀中" })
        {
            var text = new string('a', prefix) + suffix;
            var bytes = Encoding.UTF8.GetBytes(text);
            var backing = new byte[bytes.Length + 13];
            bytes.CopyTo(backing.AsSpan(5));
            var input = backing.AsSpan(5, bytes.Length);
            var full = new Utf8SliceInvocation(new(1_000_000, 0, 0), Timeout.InfiniteTimeSpan, default);
            var flags = new Utf8SliceInvocation(new(1_000_000, 0, 0), Timeout.InfiniteTimeSpan, default);
            var metadata = Utf8InputAnalyzer.Describe(input, ref full);
            Assert.Equal(suffix.Length == 0, Utf8InputAnalyzer.IsAscii(input, ref flags));
            Assert.Equal(bytes.Length, metadata.ByteLength);
            Assert.Equal(suffix.Length == 0, metadata.IsAscii);
            Assert.Equal(0UL, flags.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("")] [InlineData("ascii")] [InlineData("é")] [InlineData("😀")]
    public void UnmeteredValidationAlsoReturnsAnExactAsciiProof(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = new Utf8SliceInvocation(default, Timeout.InfiniteTimeSpan, default);
        Assert.Equal(System.Text.Ascii.IsValid(input), Utf8InputAnalyzer.IsAscii(input, ref invocation));
    }

    [Theory]
    [InlineData("abc[0-9]{2}", "abc12 abc34")]
    [InlineData("abc[0-9]{2}", "é😀abc12中abc34")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "class Alpha record Beta")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "é😀 class Alpha 中record Wrong record Good")]
    public void FinitePublicCountUsesTheSameScalarSemanticsAndValidatesTheWholeTail(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1_000_000,
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
                MaxWorkSteps = 1_000_000,
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
                MaxWorkSteps = 1,
                MaxWorkingBytes = 1 << 20
            }
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input)).ResourceKind);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(2047)]
    [InlineData(4095)]
    [InlineData(4096)]
    public void NonAsciiFactStopsInspectingTheUnusedSuffix(int prefix)
    {
        var bytes = Encoding.UTF8.GetBytes(new string('a', prefix) + "é😀" + new string('x', 1 << 20));
        var invocation = new Utf8SliceInvocation(new(8, 0, 0), Timeout.InfiniteTimeSpan, default);
        var description = Utf8InputAnalyzer.Describe(bytes, ref invocation);
        Assert.False(description.IsAscii);
        Assert.Equal(bytes.Length, description.ByteLength);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);

        // A true ASCII proof still has to inspect every byte under its allowance.
        bytes.AsSpan().Fill((byte)'a');
        invocation = new(new(8, 0, 0), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
            Utf8InputAnalyzer.TryDescribe(bytes, ref invocation, out _));
    }

}
