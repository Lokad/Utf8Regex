using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2RepeatJournalScopeTests
{
    [Fact]
    public void ControlledDenseCountDoesNotRerentRepeatStorageForEveryMatch()
    {
        var regex = Create("(?i)(a+)(b)");
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaab ", 1000)));
        for (var warm = 0; warm < 128; warm++) Assert.Equal(1000, regex.Count(bytes));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = regex.Count(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, count);
#if DEBUG
        // Unoptimized interpreter costs differ: original Debug ~897KB, reused ~465KB.
        const long allocationCeiling = 600000;
#else
        // Optimized public workload: original ~609KB, journal reuse stays below300KB.
        const long allocationCeiling = 300000;
#endif
        Assert.InRange(allocated, 0L, allocationCeiling);
    }

    [Theory]
    [InlineData("(?i)(a+)(b)", "😀aaac ab aaab", 2)]
    [InlineData(@"(?i)(a+)\1", "aaaa aaa aa", 3)]
    [InlineData("(?i)(a+?)(?=b)b", "aaab ab", 2)]
    [InlineData("(?i)(?>a+)(b)", "aaab ab", 2)]
    [InlineData("(?i)(?<pair>ab)(?&pair)", "abab xx abab", 2)]
    [InlineData("(?i)(a*)", "😀a", 3)]
    [InlineData("(?i)(|a)", "😀a", 4)]
    public void RepeatedAttemptsAndNestedContextsKeepIndependentRollback(string pattern, string text, int expected)
    {
        var regex = Create(pattern);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var bytes = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(bytes));
        Assert.Equal(expected, regex.Count(bytes, 0, Pcre2MatchOptions.None, token.Token));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void StickyDenialLeavesTheBorrowedParentCapacityIntact(int limit)
    {
        var regex = Create("(?i)(a+)(b)", new()
        {
            MatchLimit = limit == 0 ? 40U : 1000000U,
            DepthLimit = limit == 1 ? 1U : 1024U,
            HeapLimitInBytes = limit == 2 ? 256UL : 1048576UL,
            ResultLimitInBytes = 1,
        });
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(13));
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaab ", 100)));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes, ref invocation, out var input));
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Exception? failure = null;
                try { Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                    new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _); }
                catch (Exception error) { failure = error; }
                Assert.Equal(limit switch
                {
                    0 => Pcre2ErrorKind.MatchLimit, 1 => Pcre2ErrorKind.DepthLimit, _ => Pcre2ErrorKind.HeapLimit,
                }, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
                Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            }
        }
        finally { invocation.Ledger.Release(13); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ExistingRentalDiagnosticsCountAcquisitionsRatherThanReusedAttempts()
    {
        var regex = Create("(?i)(a+)(b)");
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaab ", 100)));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes, ref invocation, out var input));
        var budget = new Pcre2ResourceBudget(regex.DebugCompiledProgram.Request.DefaultLimits, invocation, true);
        Assert.True(Pcre2GlobalOperationDriver.TryCreateCursor(regex.DebugCompiledProgram, input,
            new Utf8BytePosition(0), Pcre2MatchOptions.None, out var cursor, budget));
        var frames = Pcre2BacktrackingRunner.CreateFrames(ref budget, 0);
        var repeats = new Pcre2InvocationStateStack<Pcre2RepeatMutation>(int.MaxValue, true);
        try
        {
            var count = 0;
            while (cursor.MoveNext(ref frames, ref repeats)) count++;
            Assert.Equal(100, count);
            Assert.Equal(1UL, budget.Diagnostics.WorkspaceRepeatMutationRents);
            Assert.True(budget.Diagnostics.WorkspaceFixedRents > 1);
        }
        finally { cursor.ReleaseBacktrackingStacks(ref frames, ref repeats); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits? limits = null) =>
        new(pattern, Pcre2CompileOptions.None, default, limits ?? new()
        {
            MatchLimit = 1000000, DepthLimit = 1024, HeapLimitInBytes = 1048576, ResultLimitInBytes = 1,
        }, Timeout.InfiniteTimeSpan);
}
