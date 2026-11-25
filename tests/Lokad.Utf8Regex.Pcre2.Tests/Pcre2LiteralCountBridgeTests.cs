using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LiteralCountBridgeTests
{
    [Fact]
    public void FoldedCountKeepsByteOffsetsNonoverlapAndMatchFlags()
    {
        using var regex = new Utf8Pcre2Regex("Aa", Pcre2CompileOptions.Caseless);
        using var token = new CancellationTokenSource();
        Assert.Equal(3, regex.Count("😀aAaAa\nAa"u8, 4));
        Assert.Equal(2, regex.Count("😀aAaAa\nAa"u8, 6, cancellationToken: token.Token));
        Assert.Equal(0, regex.Count("😀aAaAa\nAa"u8, 12));
        Assert.Equal(0, regex.Count([]));
        Assert.Equal(2, regex.Count("😀aAaAa\nAa"u8, 4, Pcre2MatchOptions.Anchored));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab"u8, 1));
    }

    [Theory]
    [InlineData(Pcre2MatchOptions.None, 4, 4, 7, 10)]
    [InlineData(Pcre2MatchOptions.Anchored, 4, 4)]
    [InlineData(Pcre2MatchOptions.EndAnchored, 4, 10)]
    [InlineData(Pcre2MatchOptions.Anchored | Pcre2MatchOptions.EndAnchored, 10, 10)]
    [InlineData(Pcre2MatchOptions.Anchored | Pcre2MatchOptions.EndAnchored, 4)]
    public void FoldedEnumerationKeepsByteOffsetsAndMatchFlags(Pcre2MatchOptions flags, int start,
        params int[] expected)
    {
        using var regex = new Utf8Pcre2Regex("Aa", Pcre2CompileOptions.Caseless);
        using var token = new CancellationTokenSource();
        var cursor = regex.EnumerateMatches("😀aA aaXAA"u8, start, flags, token.Token);
        foreach (var offset in expected)
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal(offset, cursor.Current.StartOffsetInBytes);
            Assert.Equal(offset + 2, cursor.Current.EndOffsetInBytes);
        }
        Assert.False(cursor.MoveNext());
        Assert.Equal(expected.Length, regex.Count("😀aA aaXAA"u8, start, flags, token.Token));
    }

    [Fact]
    public void FoldedEnumerationChargesWorkAcrossALongMiss()
    {
        using var regex = new Utf8Pcre2Regex("Aa", Pcre2CompileOptions.Caseless, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 2 }
        });
        var cursor = regex.EnumerateMatches(new byte[3 * 4096]);
        Exception? failure = null;
        try { _ = cursor.MoveNext(); }
        catch (Exception error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

    [Theory]
    [InlineData("ki", "Ki Ki kİ", 2, false)]
    [InlineData("si", "ſI Si sı", 2, false)]
    [InlineData("ii", "İi Ii ıi", 1, true)]
    [InlineData("(?-i:a)B", "AB aB ab", 2, false)]
    public void FoldedCountRequiresTheCompletePcre2CaseFiber(string pattern, string text, int expected, bool prepared)
    {
        using var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Caseless);
        Assert.Equal(prepared, regex.DebugCompiledProgram.Operations.Count is Pcre2LiteralDirectProgram);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.Count(input));
        var found = 0;
        var cursor = regex.EnumerateMatches(input);
        while (cursor.MoveNext()) found++;
        Assert.Equal(expected, found);
    }

    [Theory]
    [InlineData("(a)(b)", "π😀ab abxab", 6, 3)]
    [InlineData("(cat|horse|dog)", "π😀horse catdog", 6, 3)]
    [InlineData("(a|aa)", "aaaa", 0, 4)]
    [InlineData("(aa|a)", "aaaa", 0, 2)]
    [InlineData("(😀)(é)", "π😀é x😀é", 2, 2)]
    [InlineData("(a)(b)", "π😀xx", 2, 0)]
    public void FiniteCountKeepsOrderedScalarsAndBorrowedOffsets(string pattern, string text, int start, int expected)
    {
        using var ordinary = new Utf8Pcre2Regex(pattern);
        var regex = Create(pattern);
        Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, ordinary.Count(input, start));
        Assert.Equal(expected, regex.Count(input, start));
        Assert.Equal(expected, regex.Count(input, start, Pcre2MatchOptions.None, token.Token));
    }

    [Fact]
    public void PureCountUsesItsSmallInvocationWithoutVmScratch()
    {
        var regex = Create("(a)(b)", new()
        {
            MatchLimit = 5000,
            DepthLimit = 1,
            HeapLimitInBytes = 1,
            ResultLimitInBytes = 1
        });
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 1000)));
        Assert.Equal(1000, regex.Count(bytes));
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() =>
            {
                ReadOnlyMemory<byte> result1Input = bytes;
                using var result1Cursor = regex.CreateMatchCursor(result1Input);
                result1Cursor.MoveNext(out var result1Result);
                result1Cursor.Dispose();
                _ = result1Result;
            }).ErrorKind);
    }

    [Fact]
    public void ExplicitOptionsAndByteOffsetsPreserveRetainedCaptures()
    {
        var regex = Create("(a)(b)");
        ReadOnlySpan<byte> input = "😀ab ab"u8;
        var captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        Assert.True(regex.Match(input, startOffsetInBytes: 4, captures: captures).Success);
        Assert.Equal(1, regex.Count("😀ab ab"u8, 4, Pcre2MatchOptions.Anchored));
        Assert.Equal(2, regex.Count("😀ab ab"u8, 4));
        Assert.Equal((4, 5), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.Equal((5, 6), (captures[2].StartOffsetInBytes, captures[2].EndOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab"u8, 1));
    }

    [Theory]
    [InlineData("(a)(b)", "ab Xab ab", 3, Pcre2CompileOptions.None)]
    [InlineData("(a|aa)", "aaaa", 4, Pcre2CompileOptions.None)]
    [InlineData("(😀)(é)", "😀é x😀é", 2, Pcre2CompileOptions.None)]
    [InlineData("Ab", "ab XAB aB", 3, Pcre2CompileOptions.Caseless)]
    public void EverySmallWorkCutoffEitherDeniesOrReturnsTheCompleteCount(string pattern, string text, int expected,
        Pcre2CompileOptions options)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var admitted = false;
        var denied = false;
        for (uint work = 1; work <= 96; work++)
        {
            using var regex = Create(pattern, new() { MatchLimit = work, DepthLimit = 1024, HeapLimitInBytes = 1048576, ResultLimitInBytes = 1048576 }, options);
            try { Assert.Equal(expected, regex.Count(bytes)); admitted = true; }
            catch (Pcre2MatchException error)
            {
                Assert.Equal(Pcre2ErrorKind.MatchLimit, error.ErrorKind);
                denied = true;
            }
        }
        Assert.True(admitted && denied);
    }

    [Fact]
    public void BorrowedParentCapacityAndStickyDenialSurviveTheCount()
    {
        // Work units are versioned. Fund all but the final required step,
        // rather than pinning the old per-candidate accounting threshold.
        var control = Create("(a)(b)", new() { MatchLimit = uint.MaxValue, DepthLimit = 1, HeapLimitInBytes = 1048576, ResultLimitInBytes = 1 });
        var funded = Pcre2SliceDriver.CreateInvocation(control.DebugCompiledProgram.Request, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, funded.TryReserveWorking(13));
        Pcre2Runner.AdmitStart("ab ab ab ab"u8, 0, ref funded);
        ReadOnlySpan<byte> controlInput = "ab ab ab ab"u8;
        ulong requiredWork;
        try
        {
            Assert.True(Pcre2GlobalOperationDriver.TryCount(control.DebugCompiledProgram, controlInput,
                new Utf8BytePosition(0), Pcre2MatchOptions.None, ref funded, out var count));
            Assert.Equal(4, count);
            requiredWork = funded.Budget.Steps;
            Assert.InRange(requiredWork, 2UL, uint.MaxValue);
        }
        finally { funded.Ledger.Release(13); }

        var regex = Create("(a)(b)", new() { MatchLimit = checked((uint)(requiredWork - 1)), DepthLimit = 1, HeapLimitInBytes = 1048576, ResultLimitInBytes = 1 });
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        Pcre2Runner.AdmitStart("ab ab ab ab"u8, 0, ref invocation);
        ReadOnlySpan<byte> input = "ab ab ab ab"u8;
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Exception? failure = null;
                var count = -1;
                try
                {
                    Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out count);
                }
                catch (Exception error) { failure = error; }
                Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
                Assert.Equal(0, count);
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, invocation.Budget.LimitBreach);
                Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
                Assert.Equal(0UL, invocation.Budget.ResultBytes);
            }
        }
        finally { invocation.Ledger.Release(13); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptionAfterValidationKeepsOriginalAuthorityAndRetainedCaptures(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(a)(b)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        ReadOnlySpan<byte> retained3Input = "ab"u8;
        var retained3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained3Input, captures: retained3Captures);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, token.Token);
        var bytes = Encoding.UTF8.GetBytes("π😀" + string.Concat(Enumerable.Repeat("ab ", 1000)));
        Pcre2Runner.AdmitStart(bytes, 0, ref invocation);
        ReadOnlySpan<byte> input = bytes;
        var validated = invocation.Budget.Steps;
        var checkpoints = 0;
        invocation.CheckpointHook = _ => { checkpoints++; if (cancel) token.Cancel(); else ticks = 25; };
        Exception? failure = null;
        try
        {
            Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, input,
            new Utf8BytePosition(6), Pcre2MatchOptions.None, ref invocation, out _);
        }
        catch (Exception error) { failure = error; }
        Assert.True(checkpoints > 0 && invocation.Budget.Steps > validated);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        if (cancel) Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained3Captures[1].Success ? retained3Input.Slice(retained3Captures[1].StartOffsetInBytes, retained3Captures[1].EndOffsetInBytes - retained3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((retained3Captures[2].Success ? retained3Input.Slice(retained3Captures[2].StartOffsetInBytes, retained3Captures[2].EndOffsetInBytes - retained3Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("(a)(b)", Pcre2CompileOptions.None, false)]
    [InlineData("Ab", Pcre2CompileOptions.Caseless, false)]
    [InlineData("Ab", Pcre2CompileOptions.Caseless, true)]
    public void PublicInterruptionKeepsTheProfileFailureVocabulary(string pattern, Pcre2CompileOptions options,
        bool cancel)
    {
        long ticks = 0;
        var armed = false;
        var reads = 0;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (armed && ++reads == 100)
            {
                if (cancel) token.Cancel();
                else ticks = 25;
            }
            return ticks;
        });
        using var regex = new Utf8Pcre2Regex(pattern, options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 1000)));
        armed = true;
        if (cancel)
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(bytes, cancellationToken: token.Token)).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() => regex.Count(bytes)).ErrorKind);
        Assert.True(reads >= 100);
    }

    [Theory]
    [InlineData("(ab|cd|ef|gh)", "ab cd ef gh", 4)]
    [InlineData(@"(a)\K(b)", "abab", 2)]
    [InlineData("(a?)", "😀a", 3)]
    [InlineData("(?i)(a)(b)", "AB ab", 2)]
    [InlineData("(^a)(b)", "ab ab", 1)]
    public void BroaderLanguagesAndContextualPatternsKeepTheirExistingCounts(string pattern, string text, int expected)
    {
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, Create(pattern).Count(Encoding.UTF8.GetBytes(text), 0,
            Pcre2MatchOptions.None, token.Token));
    }

    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits? limits = null,
        Pcre2CompileOptions options = Pcre2CompileOptions.None) =>
        new(pattern, options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits ?? new()
                {
                    MatchLimit = 1000000,
                    DepthLimit = 1024,
                    HeapLimitInBytes = 1048576,
                    ResultLimitInBytes = 1048576
                }

                )).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits ?? new()
                {
                    MatchLimit = 1000000,
                    DepthLimit = 1024,
                    HeapLimitInBytes = 1048576,
                    ResultLimitInBytes = 1048576
                }

                )).MatchLimit,
                MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits ?? new()
                {
                    MatchLimit = 1000000,
                    DepthLimit = 1024,
                    HeapLimitInBytes = 1048576,
                    ResultLimitInBytes = 1048576
                }

                )).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits ?? new()
                {
                    MatchLimit = 1000000,
                    DepthLimit = 1024,
                    HeapLimitInBytes = 1048576,
                    ResultLimitInBytes = 1048576
                }

                )).DepthLimit,
                MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits ?? new()
                {
                    MatchLimit = 1000000,
                    DepthLimit = 1024,
                    HeapLimitInBytes = 1048576,
                    ResultLimitInBytes = 1048576
                }

                )).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits ?? new()
                {
                    MatchLimit = 1000000,
                    DepthLimit = 1024,
                    HeapLimitInBytes = 1048576,
                    ResultLimitInBytes = 1048576
                }

                )).HeapLimitInBytes,
                MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits ?? new()
                {
                    MatchLimit = 1000000,
                    DepthLimit = 1024,
                    HeapLimitInBytes = 1048576,
                    ResultLimitInBytes = 1048576
                }

                )).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits ?? new()
                {
                    MatchLimit = 1000000,
                    DepthLimit = 1024,
                    HeapLimitInBytes = 1048576,
                    ResultLimitInBytes = 1048576
                }

                )).ResultLimitInBytes
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
}
