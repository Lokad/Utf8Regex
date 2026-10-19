using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LiteralCountBridgeTests
{
    [Theory]
    [InlineData("(a)(b)", "π😀ab abxab", 6, 3)]
    [InlineData("(cat|horse|dog)", "π😀horse catdog", 6, 3)]
    [InlineData("(a|aa)", "aaaa", 0, 4)]
    [InlineData("(aa|a)", "aaaa", 0, 2)]
    [InlineData("(😀)(é)", "π😀é x😀é", 2, 2)]
    [InlineData("(a)(b)", "π😀xx", 2, 0)]
    public void FiniteCountKeepsOrderedScalarsAndBorrowedOffsets(string pattern, string text, int start, int expected)
    {
        var regex = Create(pattern);
        Assert.IsType<Pcre2FiniteLiteralLanguageDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, start));
        Assert.Equal(expected, regex.Count(input, start, Pcre2MatchOptions.None, token.Token));
    }

    [Fact]
    public void PureCountUsesItsSmallInvocationWithoutVmScratch()
    {
        var regex = Create("(a)(b)", new()
        {
            MatchLimit = 5000, DepthLimit = 1, HeapLimitInBytes = 1, ResultLimitInBytes = 1,
        });
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 1000)));
        Assert.Equal(1000, regex.Count(bytes));
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(bytes)).ErrorKind);
    }

    [Fact]
    public void ExplicitOptionsMalformedTailsAndRetainedCapturesKeepTheirContracts()
    {
        var regex = Create("(a)(b)");
        var retained = regex.MatchDetailed("😀ab ab"u8, 4);
        Assert.Equal(1, regex.Count("😀ab ab"u8, 4, Pcre2MatchOptions.Anchored));
        Assert.Equal(2, regex.Count("😀ab ab"u8, 4));
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.Equal("b", retained.GetGroup(2).GetValueString());
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("😀ab"u8, 1));
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', (byte)'b', 255 }));
    }

    [Theory]
    [InlineData("(a)(b)", "ab Xab ab", 3)]
    [InlineData("(a|aa)", "aaaa", 4)]
    [InlineData("(😀)(é)", "😀é x😀é", 2)]
    public void EverySmallWorkCutoffEitherDeniesOrReturnsTheCompleteCount(string pattern, string text, int expected)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var admitted = false;
        var denied = false;
        for (uint work = 1; work <= 96; work++)
        {
            var regex = Create(pattern, new() { MatchLimit = work, DepthLimit = 1024,
                HeapLimitInBytes = 1048576, ResultLimitInBytes = 1048576 });
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
        var control = Create("(a)(b)", new() { MatchLimit = uint.MaxValue, DepthLimit = 1,
            HeapLimitInBytes = 1048576, ResultLimitInBytes = 1 });
        var funded = Pcre2SliceDriver.CreateInvocation(control.DebugCompiledProgram.Request, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, funded.TryReserveWorking(13));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate("ab ab ab ab"u8,
            ref funded, out var controlInput));
        ulong requiredWork;
        try
        {
            Assert.True(Pcre2GlobalOperationDriver.TryCount(control.DebugCompiledProgram, ref controlInput,
                new Utf8BytePosition(0), Pcre2MatchOptions.None, ref funded, out var count));
            Assert.Equal(4, count);
            requiredWork = funded.Budget.Steps;
            Assert.InRange(requiredWork, 2UL, uint.MaxValue);
        }
        finally { funded.Ledger.Release(13); }

        var regex = Create("(a)(b)", new() { MatchLimit = checked((uint)(requiredWork - 1)), DepthLimit = 1,
            HeapLimitInBytes = 1048576, ResultLimitInBytes = 1 });
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate("ab ab ab ab"u8,
            ref invocation, out var input));
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Exception? failure = null;
                var count = -1;
                try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out count); }
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
    [InlineData(false)] [InlineData(true)]
    public void InterruptionAfterValidationKeepsOriginalAuthorityAndRetainedCaptures(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(a)(b)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000, HeapLimitInBytes = 1048576,
                ResultLimitInBytes = 1048576 }, TimeSpan.FromMilliseconds(20));
        var retained = regex.MatchDetailed("ab"u8);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, token.Token);
        var bytes = Encoding.UTF8.GetBytes("π😀" + string.Concat(Enumerable.Repeat("ab ", 1000)));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes, ref invocation, out var input));
        var validated = invocation.Budget.Steps;
        var checkpoints = 0;
        invocation.CheckpointHook = _ => { checkpoints++; if (cancel) token.Cancel(); else ticks = 25; };
        Exception? failure = null;
        try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
            new Utf8BytePosition(6), Pcre2MatchOptions.None, ref invocation, out _); }
        catch (Exception error) { failure = error; }
        Assert.True(checkpoints > 0 && invocation.Budget.Steps > validated);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        if (cancel) Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.Equal("b", retained.GetGroup(2).GetValueString());
    }

    [Fact]
    public void PublicTimeoutKeepsTheProfileFailureVocabulary()
    {
        long ticks = 0;
        var armed = false;
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (armed && ++reads == 100) ticks = 25;
            return ticks;
        });
        var regex = new Utf8Pcre2Regex("(a)(b)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 1000000 }, TimeSpan.FromMilliseconds(20));
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab ", 1000)));
        armed = true;
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

    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits? limits = null) =>
        new(pattern, Pcre2CompileOptions.None, default, limits ?? new()
        {
            MatchLimit = 1000000, DepthLimit = 1024, HeapLimitInBytes = 1048576, ResultLimitInBytes = 1048576,
        }, Timeout.InfiniteTimeSpan);
}
