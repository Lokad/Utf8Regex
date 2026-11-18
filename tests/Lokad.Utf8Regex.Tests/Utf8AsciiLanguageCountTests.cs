using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8AsciiLanguageCountTests
{
    private const string Octet = @"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
    private const string Pattern = "(?:" + Octet + @"\.){3}" + Octet;

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void ProvedAsciiLanguageCountsWithinScalarSubjectsAndOffsetSpans(RegexOptions options)
    {
        foreach (var pattern in new[] { Pattern, Pattern.Replace("25", @"\x32\x35").Replace(@"\.", @"\x2e") })
        {
            var regex = new Utf8Regex(pattern, options: options);
            var controlled = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1000000,
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            var oracle = new Regex(pattern, options);
            foreach (var text in new[] { "", "1.2.3.4", "..😀..", "😀00.11.22.25500é",
                "٣٣.44.55.66 00.11.22.255😀255.249.199.000é", "999.999.999.999", "00.00.00.00" })
            {
                var padded = Encoding.UTF8.GetBytes("pad" + text + "pad");
                var input = padded.AsSpan(3, Encoding.UTF8.GetByteCount(text));
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    Assert.Equal(oracle.Count(text), regex.Count(input));
                    Assert.Equal(oracle.Count(text), controlled.Count(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Fact]
    public void DenseFalseCandidatesAndUnicodeTailsAgreeWithOrderedOracle()
    {
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Compiled);
        var oracle = new Regex(Pattern);
        string[] alphabet = ["0", "1", "2", "5", "9", ".", ".", " ", "é", "😀", "٣"];
        var random = new Random(0x49507634);
        for (var sample = 0; sample < 256; sample++)
        {
            var text = string.Concat(Enumerable.Range(0, 96).Select(_ => alphabet[random.Next(alphabet.Length)]));
            if ((sample & 3) == 0) text += "00.11.22.25500😀255.249.199.000";
            Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
        const string tail = "😀00.11.22.255é";
        for (var length = 0; length <= tail.Length; length++)
        {
            if (length > 0 && char.IsHighSurrogate(tail[length - 1])) continue;
            var text = tail[..length];
            Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void DirectionAndNonBacktrackingRetainTheirExistingExecutors(RegexOptions options)
    {
        var regex = new Utf8Regex(Pattern, options: options);
        const string text = "😀00.11.22.25500é255.249.199.000";
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.Equal(new Regex(Pattern, options).Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void FiniteWorkAndOriginalTokenStillGovernThePublicCall()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', 1 << 20) + "00.11.22.255");
        var denied = new Utf8Regex(Pattern, options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            }
        });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(bytes));
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Compiled);
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, source.Token)).CancellationToken);
        using var live = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => live.Cancel());
        try
        {
            Assert.Equal(live.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, live.Token)).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void DeadlineAndRetainedResultsKeepTheirOriginalAuthority()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(10)
        });
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor("😀00.11.22.255é"u8.ToArray()));
        var bytes = Encoding.UTF8.GetBytes(new string('x', 1 << 20));
        var session = Utf8SearchDiagnosticsSession.Start(_ => ticks = 20);
        try { Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(bytes)); }
        finally { session.Complete(); }
        Assert.Equal("00.11.22.255", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(0).Span));
    }

    [Fact]
    public void CounterPublicationObservesCancellationFromItsLastDeadlinePoll()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var cancellation = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) cancellation.Cancel();
            return 0;
        });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1),
            ResourcePolicy = new()
            {
                MaxWorkSteps = 100000,
                MaxDepth = 8192,
                MaxWorkingBytes = 8 << 20,
                MaxResultBytes = 1
            }
        });
        Assert.Equal(1, regex.Count("😀00.11.22.255é"u8, cancellation.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(cancellation.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count("😀00.11.22.255é"u8, cancellation.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
