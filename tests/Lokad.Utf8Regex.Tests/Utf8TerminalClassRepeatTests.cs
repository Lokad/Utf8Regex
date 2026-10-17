using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TerminalClassRepeatTests
{
    [Theory]
    [InlineData(@"\w+", "é²a\u0301𐐀", 2)]
    [InlineData(@"\d+", "\U000104A0\U000104A1!12", 2)]
    [InlineData(@"\D{2,3}", "😀éab12😀Z", 2)]
    [InlineData(@"\p{L}{2,3}", "éééé!AB", 2)]
    [InlineData(@"[^\p{L}]+", "ab😀²!Z", 1)]
    [InlineData(@"(?i)\p{Ll}+", "aÉ𐐀!11", 1)]
    [InlineData(@"[\uD800-\uDFFF]+", "😀", 0)]
    [InlineData(@"[^\uD800]+", "😀é", 1)]
    public void CompleteScalarsKeepCoreClassSemantics(string pattern, string text, int expected)
    {
        var regex = Create(pattern, 256);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.Count(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, token.Token));
    }

    [Theory]
    [InlineData(@"(\w+)", "abc def", 2)]
    [InlineData(@"\w+?", "abc def", 6)]
    [InlineData(@"\w*", "a😀", 3)]
    [InlineData(@"\w+Z", "abcZ", 1)]
    [InlineData(@"(?:\w+|a)b", "ab", 1)]
    [InlineData(@"(?<=\w{2})\w+", "abXYZ", 1)]
    [InlineData(@"(?=\w+)\w+Z", "abcZ", 1)]
    public void GeneralRewindCaptureLazyEmptyAndAssertionPathsRemainValid(string pattern, string text, int expected)
        => Assert.Equal(expected, Create(pattern, 256).Count(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void TerminalRunsElideUselessDepthWhileCapturesInsideTheLoopKeepTheirLimit()
    {
        Assert.Equal(2, Create(@"\w+", 1).Count("abc ééé"u8));
        Assert.Equal(2, Create(@"(\w+)", 1).Count("abc ééé"u8));
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(@"(\w)+", 1).Count("abc ééé"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, failure.ResourceKind);
        var reverse = new Utf8Regex(@"\w+", RegexOptions.CultureInvariant | RegexOptions.RightToLeft);
        Assert.Equal(2, reverse.Count("abc ééé"u8));
    }

    [Fact]
    public void EveryWorkCutoffPreservesTheBorrowedLoanAndKeepsScalarWorkMetered()
    {
        var regex = new Utf8Regex(@"\p{L}+");
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Missing scalar program.");
        var input = "éé A𐐀!"u8.ToArray();
        var baseline = new Utf8SliceInvocation(new(100000, 1, 1 << 20, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(2, result.Count);
        Assert.Equal(0U, baseline.Budget.PeakDepth);
        Assert.InRange(baseline.Budget.Steps, 8UL, 100UL);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong limit = 1; limit <= baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 1, 1 << 20, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var actual = Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation);
            Assert.Equal(limit == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, actual.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void APlainRunBorrowsNoWorkingCapacityFromItsParent()
    {
        var regex = new Utf8Regex(@"\p{L}+");
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Missing scalar program.");
        var invocation = new Utf8SliceInvocation(new(100000, 1, 13, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, "éé A𐐀!"u8, 0, [], [], ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(2, result.Count);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(13UL, invocation.Budget.PeakWorkingBytes);
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 10000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };
        var plain = new Utf8Regex(@"\w+", RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, policy);
        Assert.Equal(2, plain.Count("abc ééé"u8));
        var captured = new Utf8Regex(@"(\w+)", RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, policy);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => captured.Count("abc ééé"u8)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalInterruptionDuringTheRunReleasesScratch(bool timeout)
    {
        var regex = new Utf8Regex(@"\p{L}+");
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Missing scalar program.");
        var input = Encoding.UTF8.GetBytes(new string('é', 2048));
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (timeout) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var failure = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0, [], [], ref invocation));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            Assert.True(invocation.Budget.Steps >= 64);
        }
        finally { session.Complete(); }
    }

    private static Utf8Regex Create(string pattern, uint depth) => new(pattern, RegexOptions.CultureInvariant,
        Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy { MaxDepth = depth, MaxWorkSteps = 1000000,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
}
