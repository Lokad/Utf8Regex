using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteValueSearchTests
{
    private static readonly Utf8ResourcePolicy Policy = new()
    { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };

    [Theory]
    [InlineData("needle", "π😀needle!", 6, 3)]
    [InlineData("Шерлок", "π😀Шерлок!", 6, 3)]
    [InlineData("needle", "Ватсон", -1, -1)]
    [InlineData("Шерлок", "Ватсон", -1, -1)]
    [InlineData("", "π😀", 0, 0)]
    [InlineData("", "", 0, 0)]
    public void ExactLiteralSearchKeepsCoordinatesUnderTinyStorageAndLiveToken(string pattern, string text, int start, int utf16)
    {
        var regex = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan, Policy);
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(text);
        var match = regex.Search(input, 0, token.Token);
        Assert.Equal(start >= 0, match.Success);
        if (start >= 0)
        {
            Assert.Equal((start, start + Encoding.UTF8.GetByteCount(pattern)), (match.StartOffsetInBytes, match.EndOffsetInBytes));
            Assert.Equal((utf16, utf16 + pattern.Length), (match.StartOffsetInUtf16, match.EndOffsetInUtf16));
            Assert.False(regex.Search("other"u8, 0, token.Token).Success && pattern.Length != 0);
            Assert.Equal(pattern, match.GetValueString());
        }
        byte[] invalid = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Search(invalid, 0, token.Token));
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Search(invalid, 0, token.Token)).CancellationToken);
    }

    [Fact]
    public void NonzeroFloorsAndUnprovedPatternsRetainScalarSemantics()
    {
        var literal = new Utf8PythonRegex("é", default, Timeout.InfiniteTimeSpan, Policy);
        Assert.Equal(3, literal.Search("éxé"u8, 2).StartOffsetInBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => literal.Search("éxé"u8, 1));
        Assert.False(new Utf8PythonRegex("needle(?=!)", default, Timeout.InfiniteTimeSpan,
            Policy with { MaxWorkingBytes = 1 << 20, MaxDepth = 128 }).Search("needle"u8).Success);
        var captured = new Utf8PythonRegex("(needle)", default, Timeout.InfiniteTimeSpan, Policy);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => captured.Search("needle"u8)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OriginalCancellationAndAbsoluteDeadlineInterruptWholeInputValidation(bool deadline)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var timeout = deadline ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var owner = new Utf8PythonRegex("Шерлок", default, timeout, Policy);
        var input = Encoding.UTF8.GetBytes(new string('x', 200000) + "Шерлок");
        var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ => { reached = true; if (deadline) ticks = 1000; else token.Cancel(); });
        Exception? failure = null;
        try { _ = owner.Search(input, 0, token.Token); } catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(reached);
        if (deadline) Assert.Equal("Шерлок", Assert.IsType<RegexMatchTimeoutException>(failure).Pattern);
        else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }

    [Fact]
    public void EveryWorkCutoffKeepsTheExistingParentLoanAndRefusesPartialSuccess()
    {
        var owner = new Utf8PythonRegex("Шерлок");
        var input = Encoding.UTF8.GetBytes(new string('x', 8192) + "Шерлок");
        var probe = new Utf8SliceInvocation(new(100000, 1, 13, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(probe.Ledger.TryReserve(13));
        Assert.True(owner.MaterializeNativeMatchValue(input, 0, 0, ref probe).Success);
        Assert.Equal(13UL, probe.Ledger.ReservedBytes);
        var needed = probe.Budget.Steps;
        for (ulong limit = 1; limit < needed; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 1, 13, 1), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Exception? failure = null;
            try { _ = owner.MaterializeNativeMatchValue(input, 0, 0, ref invocation); } catch (Exception error) { failure = error; }
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        var exact = new Utf8SliceInvocation(new(needed, 1, 13, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(exact.Ledger.TryReserve(13));
        Assert.True(owner.MaterializeNativeMatchValue(input, 0, 0, ref exact).Success);
        Assert.Equal(needed, exact.Budget.Steps); Assert.Equal(13UL, exact.Ledger.ReservedBytes);
    }
}
