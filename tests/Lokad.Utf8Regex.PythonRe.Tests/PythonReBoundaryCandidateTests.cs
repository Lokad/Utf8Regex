using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBoundaryCandidateTests
{
    [Fact]
    public void IsMatchTiersUpThePythonBranchProgram()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        using var regex = new Utf8PythonRegex(pattern);
        var program = regex.ScalarProgram;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.True(program.HasCaptureFreeAsciiBranch);
        Assert.Equal(dynamicCode, program.ObservesProofWork);
        Assert.Null(program.TieredProof);

        using var source = new CancellationTokenSource();
        var miss = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀é 012.200.033.1AA!", 12)));
        byte[] later = [.. miss, .. "012.200.033.199"u8];
        for (var i = 0; i < 65_536; i++)
            if (!regex.IsMatch("012.200.033.199"u8)) throw new InvalidOperationException("Missing single-candidate hit.");
        Assert.Null(program.TieredProof);
        for (var i = 0; i < 4_096; i++)
            if (!regex.IsMatch(later, cancellationToken: source.Token)) throw new InvalidOperationException("Missing controlled hit.");
        Assert.Null(program.TieredProof);

        for (var i = 0; i < 131_072 && program.ObservesProofWork; i++)
            if (!regex.IsMatch(later)) throw new InvalidOperationException("Missing tier-up hit.");
        var proof = program.TieredProof;
        Assert.Equal(dynamicCode, proof is not null);
        Assert.True(regex.IsMatch(later, miss.Length));
        Assert.True(regex.IsMatch(later, miss.Length, source.Token));
        Assert.False(regex.IsMatch(miss));
        Assert.False(regex.IsMatch(later, later.Length));
        Assert.Same(proof, program.TieredProof);

        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(later, cancellationToken: source.Token)).CancellationToken);
        using var funded = new Utf8PythonRegex(pattern, policy: new() { ProgramMemoryAllowance = new Allowance(1 << 24) });
        using var limited = new Utf8PythonRegex(pattern, policy: new() { ResourcePolicy = new() { MaxWorkSteps = 1 } });
        using var timed = new Utf8PythonRegex(pattern, policy: new() { MatchTimeout = TimeSpan.FromMinutes(1) });
        using var constructionLimited = new Utf8PythonRegex(pattern,
            policy: new() { ConstructionPolicy = new() { MaxWorkSteps = 1_000_000 } });
        Assert.False(funded.ScalarProgram.ObservesProofWork);
        Assert.False(limited.ScalarProgram.ObservesProofWork);
        Assert.False(timed.ScalarProgram.ObservesProofWork);
        Assert.False(constructionLimited.ScalarProgram.ObservesProofWork);
        Assert.True(funded.IsMatch(later));
        Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.IsMatch(later));
    }

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
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "é11/18/2019 😀١١/١٨/٢٠١٩", 0, "١١/١٨/٢٠١٩", 1)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "😀𐒡/2/٣٤ !11/18/2019", 4, "𐒡/2/٣٤", 2)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "11/18/201A; next 11/𝟙/2026", 0, "11/𝟙/2026", 1)]
    [InlineData(@"\b[0-9/]{1,3}/[0-9]{1,2}/[0-9]{2,4}\b", "prefix 9/44/201x then 1//2/2026.", 0, "1//2/2026", 1)]
    [InlineData(@"\b(?P<n>\d{1,2})/X", "café11/X 22/X suffix", 0, "22/X", 1)]
    [InlineData(@"\b[01]{1,3}1X", "prefix é01X 0011X after", 0, "0011X", 1)]
    public void SearchKeepsBoundaryPredicatesCapturesAndOffsets(string pattern, string text, int offset, string? expected, int expectedCount)
    {
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected is not null, regex.IsMatch(input, offset));
        Assert.Equal(expected is not null, regex.IsMatch(input, offset, token.Token));
        Utf8PythonRangeMatch held = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, offset, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out held);
        }
        Assert.Equal(expected is not null, held.Success);
        if (expected is not null) Assert.Equal(expected, System.Text.Encoding.UTF8.GetString((held.Groups[0].Success ? held.GetGroupMemory(held.Groups[0].Number).Span : ReadOnlySpan<byte>.Empty)));
        if (pattern.Contains("?P<")) Assert.Equal("22", System.Text.Encoding.UTF8.GetString((held.Groups[1].Success ? held.GetGroupMemory(held.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(expectedCount, regex.Count(input, offset));
        if (expected is not null) Assert.Equal(expected, System.Text.Encoding.UTF8.GetString((held.Groups[0].Success ? held.GetGroupMemory(held.Groups[0].Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void RequiredDelimiterCanRejectTheInputUnderSmallWorkLimit()
    {
        using var regex = new Utf8PythonRegex(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 16, MaxWorkingBytes = 1 }
        });
        using var source = new CancellationTokenSource();
        Assert.False(regex.IsMatch("Today is 11 18 2019 and tomorrow is 11 19 2019."u8, cancellationToken: source.Token));
    }

    [Fact]
    public void RequiredNextDelimiterCanRejectAWindowBeforeVerification()
    {
        using var regex = new Utf8PythonRegex(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 85, MaxWorkingBytes = 1 }
        });
        using var source = new CancellationTokenSource();
        Assert.False(regex.IsMatch("Today is 11/18/201A and tomorrow is 11/19/201A."u8, cancellationToken: source.Token));
    }

    [Fact]
    public void BoundedWordRunsNeedNoUselessRewindDepth()
    {
        using var regex = new Utf8PythonRegex(@"(\d{2,4})\b", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 1,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }
        });
        Assert.Equal(1, regex.Count("1234"u8));
        Assert.Equal(0, regex.Count("123A"u8));
        Assert.Equal(2, regex.Count("12 567"u8));
        using var words = new Utf8PythonRegex(@"[a-z]{1,8}\b", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 1,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }
        });
        Assert.Equal(3, words.Count("alpha beta gamma"u8));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void BoundedWordRunsPreserveOriginalInterruptionAndRetainedResult(bool timeout, bool interiorRun)
    {
        Utf8PythonRangeMatch cursorRange = default;
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
            using var regex = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = TimeSpan.FromMilliseconds(20),
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1000000,
                    MaxDepth = 1024,
                    MaxWorkingBytes = 1 << 20,
                    MaxResultBytes = 1 << 20
                }
            });
            var input = Encoding.UTF8.GetBytes(interiorRun
                ? "22X a" + new string('1', 200000) + "X 33X"
                : "1234 " + string.Concat(Enumerable.Repeat("123A ", 4096)) + "5678");
            using var cursor = regex.CreateMatchCursor(input, 0, cancellation.Token);
            Assert.True(cursor.MoveNext(out cursorRange));
            var retained = cursorRange;
            armed = true;
            var failure = Record.Exception(() => cursor.MoveNext(out _));
            Assert.True(interrupted);
            if (timeout) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            cursor.Dispose();
            Assert.Equal(interiorRun ? "22X" : "1234", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4096)]
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
                var collectedRanges1 = new List<Utf8PythonRangeMatch>();
                using (var rangeCursor1 = reference.CreateMatchCursor(input, start, default))
                {
                    while (rangeCursor1.MoveNext(out var rangeMatch1))
                        collectedRanges1.Add(rangeMatch1);
                }
                var expected = collectedRanges1.ToArray();
                var collectedRanges2 = new List<Utf8PythonRangeMatch>();
                using (var rangeCursor2 = regex.CreateMatchCursor(input, start, default))
                {
                    while (rangeCursor2.MoveNext(out var rangeMatch2))
                        collectedRanges2.Add(rangeMatch2);
                }
                var actual = collectedRanges2.ToArray();
                Assert.Equal(expected.Length, regex.Count(input, start));
                Assert.Equal(expected.Length, actual.Length);
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(expected[i].Groups.ToArray(), actual[i].Groups.ToArray());
                    Assert.Equal(expected[i].LastIndex, actual[i].LastIndex);
                }
            }
        }
        using var boundary = new Utf8PythonRegex(@"\b");
        var eof = Encoding.UTF8.GetBytes("a" + new string('1', count));
        var collectedRanges3 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor3 = boundary.CreateMatchCursor(eof, 0, default))
        {
            while (rangeCursor3.MoveNext(out var rangeMatch3))
                collectedRanges3.Add(rangeMatch3);
        }
        Assert.Equal(new[] { 0, eof.Length },
            collectedRanges3.ToArray().Select(m => m.Value.StartOffsetInBytes));
    }

    [Theory]
    [InlineData(2033)]
    [InlineData(2047)]
    [InlineData(4095)]
    public void WordInteriorsAndTailsKeepOriginalScalarFloors(int count)
    {
        using var regex = new Utf8PythonRegex(@"\b[0-9]+X\b");
        var input = Encoding.UTF8.GetBytes("a" + new string('1', count) + "X 😀 22X");
        PythonReMatchAssert.Equal("22X", regex.Match(input, mode: Utf8PythonMatchMode.Search));
        PythonReMatchAssert.Equal("22X", regex.Match(input, count, mode: Utf8PythonMatchMode.Search));
    }

    [Theory]
    [InlineData(@"\b[0-9]+X\b", "a111X", 0, false)]
    [InlineData(@"\b", "alpha beta", 1, false)]
    [InlineData(@"(?P<x>a+)(?P=x)", "aaaa", 0, true)]
    public void InteriorStartsKeepActualWorkingAdmission(string pattern, string text, int offset, bool requiresStorage)
    {
        using var regex = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1_000_000,
                MaxWorkingBytes = 1
            }
        });
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
    [InlineData(false, @"\b[0-9]+X\b")]
    [InlineData(true, @"\b[0-9]+X\b")]
    [InlineData(false, @"\b\d{1,2}X")]
    [InlineData(true, @"\b\d{1,2}X")]
    [InlineData(false, @"\b")]
    [InlineData(true, @"\b")]
    public void WordInteriorScanObservesOriginalInterruption(bool expire, string pattern)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var regex = new Utf8PythonRegex(pattern, options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        // Bounded ASCII blocks need enough input to reach the diagnostic
        // hook's unchanged 64-work-unit cadence after construction.
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("a" + new string('1', 200000)), 0, token.Token);
        if (pattern == @"\b") Assert.True(cursor.MoveNext(out _));
        var interrupted = false;
        cursor.WorkingInvocation.CheckpointHook = _ => { interrupted = true; if (expire) ticks = 30; else token.Cancel(); };
        if (expire) Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _)).CancellationToken);
        Assert.True(interrupted);
    }

    [Fact]
    public void FundedAliasesAndCapturesSurviveProgramClose()
    {
        var account = new Allowance(16 << 20);
        using var regex = Utf8PythonRegex.Compile(@"\b(?P<n>[0-9]+)X\b", options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = account
        }, cancellationToken: default);
        using var cursor = regex.CreateMatchCursor("a111A 22X 33X"u8.ToArray()); var alias = cursor;
        regex.Dispose(); Assert.True(account.Used > 0);
        Assert.True(alias.MoveNext(out var first));
        Assert.True(cursor.MoveNext(out _)); Assert.False(alias.MoveNext(out _));
        alias.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal("22X", Encoding.UTF8.GetString(first.GetValueMemory().Span));
    }
}
