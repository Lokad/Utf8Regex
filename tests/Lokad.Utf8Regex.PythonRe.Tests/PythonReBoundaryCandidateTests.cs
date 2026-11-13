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
    [InlineData(@"(\d{2,4})\b", "123A 1234 12 1", 0, "1234", 2)]
    [InlineData(@"\d{0,4}\b", "201A", 0, "", 2)]
    [InlineData(@"(?a:\d{2,4}\b)", "١٢٣ 123A 12", 0, "12", 1)]
    [InlineData(@"\d{2,4}\b", "١٢٣ 123A 12", 0, "١٢٣", 2)]
    [InlineData(@".{1,4}\b", "abc !", 0, "abc", 1)]
    [InlineData(@"[a-z]{1,4}\B", "abcd!", 0, "abc", 1)]
    [InlineData(@"\d{2,4}\b", "😀12 34", 4, "12", 2)]
    [InlineData(@"[a-z]{1,8}\b", "alpha beta gamma", 0, "alpha", 3)]
    [InlineData(@"[a-z ]{1,8}\b", "alpha beta gamma", 0, "alpha ", 3)]
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

    [Fact]
    public void BoundedWordRunsNeedNoUselessRewindDepth()
    {
        using var regex = new Utf8PythonRegex(@"(\d{2,4})\b", default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
        Assert.Equal(1, regex.Count("1234"u8));
        Assert.Equal(0, regex.Count("123A"u8));
        Assert.Equal(2, regex.Count("12 567"u8));
        using var words = new Utf8PythonRegex(@"[a-z]{1,8}\b", default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
        Assert.Equal(3, words.Count("alpha beta gamma"u8));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void BoundedWordRunsPreserveOriginalInterruptionAndRetainedResult(bool timeout, bool interiorRun)
    {
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var armed = false;
        var interrupted = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (!armed) return;
            interrupted = true;
            if (timeout) ticks = 25; else cancellation.Cancel();
        });
        try
        {
            var pattern = interiorRun ? @"\b[0-9]+X\b" : @"\d{2,4}\b";
            using var regex = new Utf8PythonRegex(pattern, default, TimeSpan.FromMilliseconds(20),
                new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
            var input = Encoding.UTF8.GetBytes(interiorRun
                ? "22X a" + new string('1', 200000) + "X 33X"
                : "1234 " + string.Concat(Enumerable.Repeat("123A ", 4096)) + "5678");
            using var cursor = regex.CreateMatchCursor(input, 0, cancellation.Token);
            Assert.True(cursor.MoveNext());
            var retained = cursor.Current;
            armed = true;
            var failure = Record.Exception(() => cursor.MoveNext());
            Assert.True(interrupted);
            if (timeout) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            cursor.Dispose();
            Assert.Equal(interiorRun ? "22X" : "1234", retained.Value.ValueText);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4096)]
    public void WordInteriorBlocksKeepUnicodeEdgesCapturesAndEofBoundaries(int count)
    {
        var input = Encoding.UTF8.GetBytes("a" + new string('1', count) + "X\u0301 22X😀 33Xé");
        foreach (var pattern in new[] { @"\b", @"(?a:\b)", @"\b[0-9]+X\b",
            @"(?a:\b[0-9]+X\b)", @"\b(?P<n>[0-9]+)X\b", @"\B[0-9]+X" })
        {
            using var regex = new Utf8PythonRegex(pattern);
            // The leading assertion leaves this equivalent reference on its
            // original per-candidate verifier, rather than the interior proof.
            using var reference = new Utf8PythonRegex("(?=)(?:" + pattern + ")");
            foreach (var start in new[] { 0, 1, count })
            {
                var expected = reference.FindIterDetailed(input, start);
                var actual = regex.FindIterDetailed(input, start);
                Assert.Equal(expected.Length, regex.Count(input, start));
                Assert.Equal(expected.Length, actual.Length);
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(expected[i].Groups, actual[i].Groups);
                    Assert.Equal(expected[i].LastIndex, actual[i].LastIndex);
                }
            }
        }
        using var boundary = new Utf8PythonRegex(@"\b");
        var eof = Encoding.UTF8.GetBytes("a" + new string('1', count));
        Assert.Equal(new[] { 0, eof.Length },
            boundary.FindIterDetailed(eof).Select(m => m.Value.StartOffsetInBytes));
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

    }

    [Theory]
    [InlineData(@"\b[0-9]+X\b", "a111X", 0, false)]
    [InlineData(@"\b", "alpha beta", 1, false)]
    [InlineData(@"(?P<x>a+)(?P=x)", "aaaa", 0, true)]
    public void InteriorStartsKeepActualWorkingAdmission(string pattern, string text, int offset, bool requiresStorage)
    {
        using var regex = new Utf8PythonRegex(pattern, default, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 });
        var input = Encoding.UTF8.GetBytes(text);
        if (requiresStorage)
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, Assert.Throws<Utf8ResourceLimitExceededException>(
                () => regex.IsMatch(input, offset)).ResourceKind);
        else
            Assert.Equal(new Utf8PythonRegex(pattern).IsMatch(input, offset), regex.IsMatch(input, offset));
    }

    [Theory]
    [InlineData(@"\b[0-9]+X\b", 1)]
    [InlineData(@"\b", 4)]
    public void EveryWorkCutoffRefundsOnlyOwnedScratch(string pattern, int expectedCount)
    {
        var program = PythonReScalarCompiler.Compile(new PythonReParser(pattern).Parse(default));
        var input = "a111A 22X"u8.ToArray();
        ulong required;
        var probe = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan,
            null, pattern, new(new(1_000_000, 1024, 1 << 20), Timeout.InfiniteTimeSpan, default));
        try
        {
            var count = 0;
            while (probe.MoveNext()) count++;
            Assert.Equal(expectedCount, count);
            required = probe.WorkingInvocation.Budget.Steps;
        }
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
                var count = 0;
                while (cursor.MoveNext()) count++;
                Assert.Equal(expectedCount, count);
                Assert.Equal(required, cap);
            }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.True(cap < required); Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
            finally { if (constructed) cursor.Dispose(); Assert.Equal(13UL, invocation.Ledger.ReservedBytes); invocation.Ledger.Release(13); }
        }
    }

    [Theory]
    [InlineData(false, @"\b[0-9]+X\b")] [InlineData(true, @"\b[0-9]+X\b")]
    [InlineData(false, @"\b")] [InlineData(true, @"\b")]
    public void WordInteriorScanObservesOriginalInterruption(bool expire, string pattern)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(pattern, default, TimeSpan.FromMilliseconds(20));
        // Bounded ASCII blocks need enough input to reach the diagnostic
        // hook's unchanged 64-work-unit cadence after construction.
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("a" + new string('1', 200000)), 0, token.Token);
        if (pattern == @"\b") Assert.True(cursor.MoveNext());
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
