using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8Ipv4BooleanTests
{
    private const string Pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    private static bool Scalar(Utf8Regex regex, byte[] input)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
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

    [Theory]
    [InlineData(0)] [InlineData(2033)] [InlineData(2034)] [InlineData(2035)] [InlineData(4095)]
    public void BoundedDiscoveryUsesNoVmWorkspaceAcrossScalarPrefixesAndWindows(int boundary)
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var tail in new[] { "012.200.033.199", "99.99.99.99", "1.2.3.4", "99.99.99.999", "bad.address" })
        {
            var text = "é" + new string('x', boundary) + tail + "😀";
            var regex = new Utf8Regex(Pattern, mode, Timeout.InfiniteTimeSpan,
                Funded with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            var buffer = Encoding.UTF8.GetBytes("pad" + text + "tail");
            var input = buffer.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            var expected = Regex.IsMatch(text, Pattern);
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(expected, regex.IsMatch(input, token.Token));
                Assert.Equal(Utf8ExecutionRoute.FallbackDirectAsciiDottedDecimalQuad, session.ExecutionRoute);
                Assert.Equal(expected, regex.IsMatch(input));
            }
            finally { session.Complete(); }
        }
    }

    [Fact]
    public void AliasesCapturesAndExcludedModesKeepScalarSemantics()
    {
        foreach (var pattern in new[] { Pattern, "(" + Pattern + ")", Pattern.Replace("[0-9]", @"\d"),
            @"\A" + Pattern + @"\z", Pattern + @"\b" })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan, Funded);
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
        var retained = owner.MatchDetailed(bytes);
        Assert.True(owner.IsMatch(bytes)); Assert.Equal(1, owner.Count(bytes));
        Assert.True(owner.IsMatchFromUtf16Offset(bytes, 1));
        Assert.False(owner.IsMatchFromUtf16Offset(bytes, 5));
        Assert.Equal("012.200.033.199", retained.GetGroup(1).GetValueString());
    }

    [Fact]
    public void EveryWorkCutoffKeepsParentLoansAndPublishesNoPartialBoolean()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 4097) + "012.200.033.199");
        var complete = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8AsciiIpv4TokenExecutor.TryIsMatchAsciiIpv4Token(input, ref complete, out var found));
        Assert.True(found);
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 1, 13, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var breach = Utf8AsciiIpv4TokenExecutor.TryIsMatchAsciiIpv4Token(input, ref invocation, out found);
            Assert.Equal(cap == complete.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, breach);
            Assert.Equal(cap == complete.Budget.Steps, found); Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref validation);
        var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkSteps = validation.Budget.Steps });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionStillAppliesAfterWholeInputValidation(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(new string('x', 320000) + "012.200.033.199");
        var validation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref validation);
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var regex = new Utf8Regex(Pattern, RegexOptions.None, timeout, Funded);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validation.Budget.Steps) return;
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
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
    }

    [Fact]
    public void PublicationObservesCancellationFromItsLastDeadlinePoll()
    {
        var reads = 0; var cancelAt = int.MaxValue;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => { if (++reads == cancelAt) token.Cancel(); return 0; });
        var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromSeconds(1), Funded);
        Assert.True(regex.IsMatch("012.200.033.199"u8, token.Token));
        cancelAt = reads; reads = 0;
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch("012.200.033.199"u8, token.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }
}
