using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CallerValidTextTests
{
    [Theory]
    [InlineData("needle")]
    [InlineData("(?<word>needle)")]
    [InlineData("(?<=é)needle")]
    public void EarlyPublicResultsNeedNoUnconsumedSuffixPass(string pattern)
    {
        var input = Encoding.UTF8.GetBytes("éneedle " + new string('中', 1 << 18));
        using var source = new CancellationTokenSource();
        foreach (var finite in new[] { false, true })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = finite ? new()
                {
                    MaxDepth = 4096,
                    MaxWorkingBytes = 1 << 24,
                    MaxResultBytes = 1 << 20
                }

                : default
            });
            var session = Utf8SearchDiagnosticsSession.Start(step => Assert.True(step < 128, "An early result must not scan the suffix."));
            try
            {
            Assert.True(regex.IsMatch(input, source.Token));
            var whole = regex.Match(input, cancellationToken: source.Token);
            Assert.Equal((2, 8), (whole.StartOffsetInBytes, whole.EndOffsetInBytes));
            var slots = new Utf8CaptureRange[regex.CaptureGroupCount + 1];
            Assert.Equal(whole, regex.Match(input, 0, slots, source.Token));
            using (var cursor = regex.CreateMatchCursor(input, cancellationToken: source.Token))
            {
                Assert.True(cursor.MoveNext(out var first));
                Assert.Equal(whole, first.Value);
            }
            var matches = regex.EnumerateMatches(input, cancellationToken: source.Token);
            Assert.True(matches.MoveNext());
            Assert.Equal(whole, matches.Current);
            var offsetMatches = regex.EnumerateMatches(input, 0, source.Token);
            Assert.True(offsetMatches.MoveNext());
            Assert.Equal(whole, offsetMatches.Current);
            var splits = regex.EnumerateSplits(input, cancellationToken: source.Token);
            Assert.True(splits.MoveNext());
            Assert.Equal("é", Encoding.UTF8.GetString(splits.Current.GetValueBytes()));
            }
            finally { session.Complete(); }
        }
    }

    [Fact]
    public void PatternAndTemplateChecksRemainIndependentOfCallerValidSubjects()
    {
        var regex = Utf8Regex.Compile("needle"u8);
        Assert.Throws<ArgumentException>(() => Utf8Regex.Compile(new byte[] { 0xff }));
        Assert.Throws<ArgumentException>(() => regex.Replace("needle"u8, new byte[] { 0xff }));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch("needle"u8, source.Token)).CancellationToken);
    }

    [Fact]
    public void FundedFactoryKeepsCallerTextAcrossCountCursorAndReplacement()
    {
        var account = new Account();
        using (var owner = Utf8Regex.Compile("needle", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = account
        }, cancellationToken: default))
        {
            Assert.Equal(2, owner.Count("needle é😀 needle"u8));
            using (var cursor = owner.CreateMatchCursor("needle é😀 needle"u8.ToArray()))
            {
                Assert.True(cursor.MoveNext(out var retained));
                Assert.True(cursor.MoveNext(out _));
                Assert.False(cursor.MoveNext(out _));
                Assert.True(retained.Success);
            }
            Span<byte> destination = stackalloc byte[64];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                owner.TryReplace("needle é😀 needle"u8, "X"u8, destination, out var written));
            Assert.Equal("X é😀 X", Encoding.UTF8.GetString(destination[..written]));
        }
        Assert.Equal(0UL, account.Used);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { Used = checked(Used + bytes); return true; }
        public void Release(ulong bytes) { Assert.True(bytes <= Used); Used -= bytes; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallerTextRetainsInterruptionDuringMatching(bool deadline)
    {
        long now = 0;
        using var clock = Internal.Execution.Utf8ExecutionDeadline.UseClockForDiagnostics(() => now);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("needle", policy: new Utf8RegexPolicy
        {
            MatchTimeout = deadline ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan
        });
        var bytes = Encoding.UTF8.GetBytes(new string('x', 256 * 1024) + "needle");
        var triggered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { triggered = true; if (deadline) now = 101; else source.Cancel(); });
        try
        {
            if (deadline) Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(bytes, source.Token));
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, source.Token)).CancellationToken);
            Assert.True(triggered);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void CallerTextRetainsWorkingAdmission()
    {
        var memory = new Utf8Regex(@"(a+)\1", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(memory.CreateMatchCursor("aaaa"u8.ToArray())));
    }

}
