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
            var regex = new Utf8Regex(pattern, options);
            var controlled = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
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
                    Assert.Equal(Utf8ExecutionRoute.FallbackDirectAsciiDottedDecimalQuad, session.ExecutionRoute);
                    Assert.Equal(oracle.Count(text), controlled.Count(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Fact]
    public void DenseFalseCandidatesAndUnicodeTailsAgreeWithOrderedOracle()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.Compiled);
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
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void FullValidationPrecedesBothHitsAndMisses(RegexOptions options)
    {
        var regex = new Utf8Regex(Pattern, options);
        foreach (var prefix in new[] { "", "00.11.22.255", "😀..." })
        {
            var bytes = Encoding.UTF8.GetBytes(prefix).Concat(new byte[] { 0xF0, 0x9F, 0x98 }).ToArray();
            Assert.Throws<ArgumentException>(() => regex.Count(bytes));
        }
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void DirectionAndNonBacktrackingRetainTheirExistingExecutors(RegexOptions options)
    {
        var regex = new Utf8Regex(Pattern, options);
        const string text = "😀00.11.22.25500é255.249.199.000";
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.Equal(new Regex(Pattern, options).Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
            Assert.NotEqual(Utf8ExecutionRoute.FallbackDirectAsciiDottedDecimalQuad, session.ExecutionRoute);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void FiniteWorkAndOriginalTokenStillGovernThePublicCall()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', 65536) + "00.11.22.255");
        var denied = new Utf8Regex(Pattern, RegexOptions.Compiled, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(bytes));
        var regex = new Utf8Regex(Pattern, RegexOptions.Compiled);
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
        var regex = new Utf8Regex(Pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(10));
        var retained = regex.MatchDetailed("😀00.11.22.255é"u8);
        var bytes = Encoding.UTF8.GetBytes(new string('x', 65536));
        var session = Utf8SearchDiagnosticsSession.Start(_ => ticks = 20);
        try { Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(bytes)); }
        finally { session.Complete(); }
        Assert.Equal("00.11.22.255", retained.GetGroup(0).GetValueString());
    }

    [Fact]
    public void EveryCounterWorkCutoffPreservesBorrowedStorage()
    {
        var input = "é00.11.22.255😀255.249.199.000"u8.ToArray();
        var complete = new Utf8SliceInvocation(new(1000, 1, 0, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(complete.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None,
            Utf8AsciiIpv4TokenExecutor.TryCountAsciiIpv4Tokens(input, ref complete, out var count));
        Assert.Equal(2, count);
        Assert.Equal(13UL, complete.Ledger.ReservedBytes);
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 1, 0, 1), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var breach = Utf8AsciiIpv4TokenExecutor.TryCountAsciiIpv4Tokens(input, ref invocation, out _);
            Assert.Equal(cap < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
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
        var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromSeconds(1),
            new() { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(1, regex.Count("😀00.11.22.255é"u8, cancellation.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(cancellation.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count("😀00.11.22.255é"u8, cancellation.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
