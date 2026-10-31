using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiveTokenBooleanTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void AnchoredBooleanLiveTokenAvoidsScalarTemporaryStorage(bool compiled, bool hit)
    {
        using var construction = new CancellationTokenSource();
        var regex = Utf8Regex.Compile("^[A-Z]{3}[0-9]{4}$", RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None), Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, construction.Token);
        construction.Cancel(); using var operation = new CancellationTokenSource();
        var input = hit ? "ABC0123"u8.ToArray() : "ABC012x"u8.ToArray();
        for (var index = 0; index < 1000; index++) Assert.Equal(hit, regex.IsMatch(input, operation.Token));
        var start = GC.GetAllocatedBytesForCurrentThread(); var matches = 0;
        for (var index = 0; index < 10000; index++) if (regex.IsMatch(input, operation.Token)) matches++;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(hit ? 10000 : 0, matches);
        // The existing controlled invocation owner remains (currently120B).
        // This rejects the former ~1800B/call scalar capture/workspace path.
        Assert.InRange(allocated, 0, 160L * 10000);
        operation.Cancel();
        Assert.Equal(operation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(input, operation.Token)).CancellationToken);
    }

    [Theory]
    [InlineData("^[A-Z]{3}[0-9]{4}$", "ABC0123", true)]
    [InlineData("^[A-Z]{3}[0-9]{4}$", "ABC0123\n", true)]
    [InlineData("^[A-Z]{3}[0-9]{4}$", "ABC0123é", false)]
    [InlineData("[A-Z]{3}[0-9]{4}", "😀ABC0123é", true)]
    [InlineData("(ab[0-9]d)", "ab1d/ab2d", true)]
    [InlineData("^.$", "😀", true)]
    [InlineData("^.$", "😀é", false)]
    [InlineData("^\\p{L}+$", "éЖ文", true)]
    [InlineData("^\\p{L}+$", "éЖ😀", false)]
    public void BooleanTokenModesKeepScalarAndStrictTailSemantics(string pattern, string text, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text); using var live = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
            Assert.Equal(expected, regex.IsMatch(input)); Assert.Equal(expected, regex.IsMatch(input, live.Token));
            var malformed = input.Concat(new byte[] { 0xff }).ToArray();
            Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed, live.Token));
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ControlledBooleanInterruptsSubstantialMatchingAfterFullValidation(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var canceled = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ABC012x ", 8192)));
        var regex = new Utf8Regex("[A-Z]{3}[0-9]{4}", RegexOptions.CultureInvariant,
            timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan);
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref validation); var before = validation.Budget.Steps;
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (steps <= before + 64) return;
            reached = true; if (timeout) ticks = 100; else canceled.Cancel();
        });
        try
        {
            var error = Record.Exception(() => regex.IsMatch(input, canceled.Token)); Assert.True(reached);
            if (timeout) Assert.IsType<RegexMatchTimeoutException>(error);
            else Assert.Equal(canceled.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void ControlledBooleanStillAdmitsWorkBeforeNativeExecution()
    {
        using var live = new CancellationTokenSource();
        var regex = new Utf8Regex("^[A-Z]{3}[0-9]{4}$", RegexOptions.CultureInvariant,
            Timeout.InfiniteTimeSpan, new() { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            regex.IsMatch("ABC0123"u8, live.Token)).ResourceKind);
    }
}
