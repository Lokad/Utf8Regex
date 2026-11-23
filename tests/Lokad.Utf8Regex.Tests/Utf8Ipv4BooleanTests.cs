using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8Ipv4BooleanTests
{
    private const string Pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    private static bool Scalar(Utf8Regex regex, byte[] input)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        invocation.AnchorOriginByte = program.RightToLeft ? input.Length : 0;
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var match = Utf8SliceRunner.TryFindFirst(program, input, invocation.AnchorOriginByte,
                slots.Starts, slots.Ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach); return match.Found;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Fact]
    public void BranchedSearchStopsAtTheMinimumRemainingByteLength()
    {
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(Pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 12, MaxWorkingBytes = 1 }
        });
        Assert.False(regex.IsMatch("012.200.033.1AA"u8, token.Token));
        Assert.True(regex.IsMatch("😀99.99.99.99"u8, token.Token));

        using var ranges = new Utf8Regex(Pattern);
        Assert.Equal(new Utf8CaptureRange(4, 15), ranges.Match("😀99.99.99.99"u8));
        Assert.False(ranges.Match("😀99.99.99.99"u8, startOffsetInBytes: 5).Success);
    }

    [Theory]
    [InlineData("2345.200.033.1AA")]
    [InlineData("2345.200.033.199")]
    [InlineData("256.200.033.199")]
    [InlineData("12..23.34.45.56")]
    [InlineData("😀2345.200.033.1AA 23.34.45.56")]
    public void BranchedSearchReusesOnlyConclusiveSuffixRejections(string text)
    {
        using var source = new CancellationTokenSource();
        using var ordinary = new Utf8Regex(Pattern, RegexOptions.Compiled);
        using var limited = new Utf8Regex(Pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 24, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        var expected = Regex.IsMatch(text, Pattern);
        Assert.Equal(expected, ordinary.IsMatch(bytes));
        Assert.Equal(expected, limited.IsMatch(bytes, source.Token));
    }

    [Theory]
    [InlineData("012.200.033.199", false)]
    [InlineData("no leading digit", true)]
    public void BranchedCandidateSearchChecksInterruptionWithAdmittedStorage(string text, bool expire)
    {
        using var regex = new Utf8Regex(Pattern, RegexOptions.Compiled);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(64, 1, 13),
            expire ? TimeSpan.FromMilliseconds(200) : Timeout.InfiniteTimeSpan, source.Token);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        for (var i = 0; i < 63; i++)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeStep());
        invocation.CheckpointHook = _ =>
        {
            if (expire) clock.Advance(250);
            else source.Cancel();
        };

        var input = Encoding.UTF8.GetBytes(text);
        var error = Record.Exception(() => Utf8SliceRunner.TryFindFirst(program, input, 0,
            [], [], false, ref invocation, existenceOnly: true));

        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Theory]
    [InlineData(0)] [InlineData(2033)] [InlineData(2034)] [InlineData(2035)] [InlineData(4095)]
    public void ScalarDiscoveryPreservesPrefixesAndWindowBoundaries(int boundary)
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var tail in new[] { "012.200.033.199", "99.99.99.99", "1.2.3.4", "99.99.99.999", "bad.address" })
        {
            var text = "é" + new string('x', boundary) + tail + "😀";
            var regex = new Utf8Regex(Pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded with
                {
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            var buffer = Encoding.UTF8.GetBytes("pad" + text + "tail");
            var input = buffer.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            var expected = Regex.IsMatch(text, Pattern);
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(expected, regex.IsMatch(input, token.Token));
                Assert.Equal(expected, regex.IsMatch(input));
            }
            finally { session.Complete(); }
        }
    }

    [Theory]
    [InlineData("012.200.033.199")]
    [InlineData("012.29.033.199")]
    public void DisprovedOctetAlternativesRequireNoWorkingStorage(string text)
    {
        var expected = Regex.Match(text, Pattern);
        Assert.True(expected.Success);
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(Pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 100, MaxWorkingBytes = 1 }
        });
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(1, regex.Count(input, token.Token));
        Utf8MatchSequence.AssertRange(regex.Match(input, cancellationToken: token.Token), text, expected.Index, expected.Length);
    }

    [Theory]
    [InlineData("012.200.033.199")]
    [InlineData("250.250.250.250")]
    [InlineData("😀012.200.033.199")]
    public void CompleteBranchProofFitsBooleanRangeAndCountLimits(string text)
    {
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(Pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 8, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        var input = Encoding.UTF8.GetBytes(text);
        var expected = Regex.Match(text, Pattern);
        Assert.True(regex.IsMatch(input, token.Token));
        Utf8MatchSequence.AssertRange(regex.Match(input, cancellationToken: token.Token), text, expected.Index, expected.Length);
        Assert.Equal(1, regex.Count(input, token.Token));
    }

    [Fact]
    public void CompleteBranchCountContinuesFromTheOrderedEndWithinItsWorkingLimit()
    {
        var text = "😀" + string.Concat(Enumerable.Repeat("012.200.033.199 250.255.251.254 ", 24));
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(Pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 256, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        Assert.Equal(Regex.Count(text, Pattern), regex.Count(Encoding.UTF8.GetBytes(text), token.Token));
    }

    [Theory]
    [InlineData("200X")]
    [InlineData("200Y")]
    public void BoundedAlternationProofLeavesViableCaptureChoicesInOrder(string text)
    {
        const string pattern = @"\A(?:(?<first>2[0-4][0-9])(?=X)|(?<retry>2[0-9][0-9]))[XY]\z";
        using var regex = Utf8Regex.Compile(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = Regex.Match(text, pattern);
        Assert.True(expected.Success);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
    }

    [Fact]
    public void AliasesCapturesAndExcludedModesKeepScalarSemantics()
    {
        foreach (var pattern in new[] { Pattern, "(" + Pattern + ")", Pattern.Replace("[0-9]", @"\d"),
            @"\A" + Pattern + @"\z", Pattern + @"\b" })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options: mode, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            using var token = new CancellationTokenSource();
            foreach (var text in new[] { "", "012.200.033.199", "256.200.033.199", "123.234.345.456",
                "é99.99.99.999😀", "0١2.2٠٠.0٣٣.1٩9", "0𝟙2.2𝟘𝟘.0𝟛𝟛.1𝟡9", "1.2.3.4", "..99.99.99.99.99" })
            {
                var input = Encoding.UTF8.GetBytes(text);
                var expected = Scalar(regex, input);
                Assert.Equal((pattern, mode, text, expected), (pattern, mode, text, regex.IsMatch(input)));
                Assert.Equal(expected, regex.IsMatch(input, token.Token));
            }
        }
        var owner = new Utf8Regex("(" + Pattern + ")");
        var bytes = "é012.200.033.199😀"u8.ToArray();
        var retained = Utf8MatchSequence.First(owner.CreateMatchCursor(bytes));
        Assert.True(owner.IsMatch(bytes)); Assert.Equal(1, owner.Count(bytes));
        Assert.True(owner.Match(bytes, 2, []).Success);
        Assert.False(owner.Match(bytes, 6, []).Success);
        Assert.Equal("012.200.033.199", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionReachesLongCandidateSearch(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(new string('x', 320000) + "012.200.033.199");
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout,
            ResourcePolicy = Funded
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps < 128) return;
            entered = true;
            if (expire) ticks += 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(timeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, token.Token)).MatchTimeout);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.True(regex.IsMatch("012.200.033.199"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicationObservesCancellationFromItsLastDeadlinePoll(bool count)
    {
        var reads = 0; var cancelAt = int.MaxValue;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => { if (++reads == cancelAt) token.Cancel(); return 0; });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1),
            ResourcePolicy = Funded
        });
        int Run() => count ? regex.Count("012.200.033.199"u8, token.Token)
            : regex.IsMatch("012.200.033.199"u8, token.Token) ? 1 : 0;
        Assert.Equal(1, Run());
        cancelAt = reads; reads = 0;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => Run()).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
