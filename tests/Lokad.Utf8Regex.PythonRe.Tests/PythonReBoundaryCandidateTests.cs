using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBoundaryCandidateTests
{
    [Theory]
    [InlineData(@"\b[0-9]+X\b", "111 22X 333", 0, "22X", 1)]
    [InlineData(@"\b[0-9]+X\b", "a11X_ 22X", 0, "22X", 1)]
    [InlineData(@"\b[0-9]+X\b", "é11X 22X", 0, "22X", 1)]
    [InlineData(@"(?a:\b[0-9]+X\b)", "é11X 22X", 0, "11X", 2)]
    [InlineData(@"\b[0-9]+X\b", "á22X 33X", 0, "22X", 2)]
    [InlineData(@"\b(?P<n>[0-9]+)X\b", "a11X_ 22X", 0, "22X", 1)]
    [InlineData(@"\B[0-9]+X", "a22X", 0, "22X", 1)]
    [InlineData(@"\b[0-9]+X\b", "a11X 22X", 2, "22X", 1)]
    [InlineData(@"\b[0-9]+X\b", "a11X 22X", 8, null, 0)]
    public void SearchKeepsBoundaryPredicatesCapturesAndOffsets(string pattern, string text, int offset, string? expected, int expectedCount)
    {
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected is not null, regex.IsMatch(input, offset));
        Assert.Equal(expected is not null, regex.IsMatch(input, offset, token.Token));
        var held = regex.SearchDetailedData(input, offset);
        Assert.Equal(expected is not null, held.Success);
        if (expected is not null) Assert.Equal(expected, held.Groups[0].ValueText);
        if (pattern.Contains("?P<")) Assert.Equal("22", held.Groups[1].ValueText);
        Assert.Equal(expectedCount, regex.Count(input, offset));
        if (expected is not null) Assert.Equal(expected, held.Groups[0].ValueText);
    }

    [Theory]
    [InlineData(2033)] [InlineData(2047)] [InlineData(4095)]
    public void WordInteriorsAndTailsKeepOriginalScalarFloors(int count)
    {
        using var regex = new Utf8PythonRegex(@"\b[0-9]+X\b");
        var input = Encoding.UTF8.GetBytes("a" + new string('1', count) + "X 😀 22X");
        Assert.Equal("22X", regex.SearchToString(input));
        Assert.Equal("22X", regex.SearchToString(input, count));
        var invalid = input.Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid));
    }

    [Fact]
    public void FirstImpossibleCandidateStillAdmitsVmStorage()
    {
        using var regex = new Utf8PythonRegex(@"\b[0-9]+X\b", default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, Assert.Throws<Utf8ResourceLimitExceededException>(
            () => regex.IsMatch("a111X"u8)).ResourceKind);
    }

    [Fact]
    public void EveryWorkCutoffRefundsOnlyOwnedScratch()
    {
        const string pattern = @"\b[0-9]+X\b";
        var program = PythonReScalarCompiler.Compile(new PythonReParser(pattern).Parse(default));
        var input = "a111A 22X"u8.ToArray();
        ulong required;
        var probe = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan,
            null, pattern, new(new(1_000_000, 1024, 1 << 20), Timeout.InfiniteTimeSpan, default));
        try { Assert.True(probe.MoveNext()); Assert.False(probe.MoveNext()); required = probe.WorkingInvocation.Budget.Steps; }
        finally { probe.Dispose(); }
        for (ulong cap = 1; cap <= required; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 1024, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            PythonReScalarMatchCursor cursor = default;
            var constructed = false;
            try
            {
                cursor = new(input, program, 0, Timeout.InfiniteTimeSpan, null, pattern, invocation);
                constructed = true;
                Assert.True(cursor.MoveNext()); Assert.False(cursor.MoveNext()); Assert.Equal(required, cap);
            }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.True(cap < required); Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
            finally { if (constructed) cursor.Dispose(); Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13); }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WordInteriorScanObservesOriginalInterruption(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(@"\b[0-9]+X\b", default, TimeSpan.FromMilliseconds(20));
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("a" + new string('1', 4096)), 0, token.Token);
        var interrupted = false;
        cursor.WorkingInvocation.CheckpointHook = _ => { interrupted = true; if (expire) ticks = 30; else token.Cancel(); };
        if (expire) Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext());
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()).CancellationToken);
        Assert.True(interrupted);
    }

    [Fact]
    public void FundedAliasesAndCapturesSurviveProgramClose()
    {
        var account = new Allowance(16 << 20);
        using var regex = Utf8PythonRegex.Compile(@"\b(?P<n>[0-9]+)X\b", default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, account);
        using var cursor = regex.CreateMatchCursor("a111A 22X 33X"u8.ToArray()); var alias = cursor;
        regex.Dispose(); Assert.True(account.Used > 0);
        Assert.True(alias.MoveNext(out var first));
        Assert.True(cursor.MoveNext(out _)); Assert.False(alias.MoveNext(out _));
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal("22X", Encoding.UTF8.GetString(first.GetValueMemory().Span));
    }
}
