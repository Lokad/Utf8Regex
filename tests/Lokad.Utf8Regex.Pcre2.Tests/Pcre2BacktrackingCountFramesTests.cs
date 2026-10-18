using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2BacktrackingCountFramesTests
{
    [Fact]
    public void FixedWorkspaceAllocationDoesNotScaleWithCandidateAttempts()
    {
        var regex = Create(@"(?<s>\p{Xps}+)(?<x>\p{Xuc}{1,2})", new()
        {
            MatchLimit = 10000000, DepthLimit = 256,
            HeapLimitInBytes = 1UL << 20, ResultLimitInBytes = 1,
        });
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\u0085\u180E😀😀!", 1000)));
        using var token = new CancellationTokenSource();
        for (var index = 0; index < 3; index++)
            Assert.Equal(1000, regex.Count(bytes, 0, Pcre2MatchOptions.None, token.Token));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = regex.Count(bytes, 0, Pcre2MatchOptions.None, token.Token);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, count);
        Assert.True(allocated < 16384, $"Count allocated {allocated} bytes for fixed candidate scratch.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void ImpossibleShortSubjectDoesNotAcquireFixedWorkspace(string text)
    {
        var regex = Create(@"(?<x>\p{Xuc}){3}!", new()
        {
            MatchLimit = 10000, DepthLimit = 1, HeapLimitInBytes = 1, ResultLimitInBytes = 1,
        });
        using var token = new CancellationTokenSource();
        Assert.Equal(0, regex.Count(Encoding.UTF8.GetBytes(text), 0, Pcre2MatchOptions.None, token.Token));
    }

    [Theory]
    [InlineData("(?i)(a+)(b)", "😀aaabéab", 2)]
    [InlineData(@"(?i)(a+)\1", "aaaa aaa aa", 3)]
    [InlineData("(?i)(a+?)(?=b)b", "aaab ab", 2)]
    [InlineData("(?i)(?<=(a))(b+)", "abbb abb", 2)]
    [InlineData("(?i)(?>a+)(b)", "aaab ab", 2)]
    [InlineData("(?i)(?:(a+)|b+)(?(1)c|d)", "aac bbd ac bd", 4)]
    [InlineData("(?i)(?<pair>ab)(?&pair)", "abab xx abab", 2)]
    [InlineData("(?i)(a*)", "😀a", 3)]
    [InlineData("(?i)(|a)", "😀a", 4)]
    [InlineData("(*MARK:tag)(😀+)(é+)", "😀😀ééx😀é", 2)]
    public void AttemptsAndGlobalAdvancesKeepIndependentState(string pattern, string text, int expected)
    {
        var regex = Create(pattern);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, regex.Count(input, 0, Pcre2MatchOptions.None, token.Token));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void DenialReleasesExclusiveCapacityWithoutRefundingWork(int deniedLimit)
    {
        var regex = Create("(?i)(a+)(b)", new()
        {
            MatchLimit = deniedLimit == 0 ? 30U : 1000000U,
            DepthLimit = deniedLimit == 1 ? 1U : 256U,
            HeapLimitInBytes = deniedLimit == 2 ? 1UL : 1UL << 20,
            ResultLimitInBytes = deniedLimit == 3 ? 1UL : 1UL << 20,
        });
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaab ", 100)));
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, default);
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes, ref invocation, out var input));
        var validated = invocation.Budget.Steps;
        Exception? failure = null;
        try
        {
            Assert.True(Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out var count));
            Assert.Equal(100, count);
        }
        catch (Exception error) { failure = error; }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.True(invocation.Budget.Steps > validated);
        if (deniedLimit == 3) Assert.Null(failure); // Count retains no result payload.
        else Assert.Equal(deniedLimit switch
        {
            0 => Pcre2ErrorKind.MatchLimit,
            1 => Pcre2ErrorKind.DepthLimit,
            _ => Pcre2ErrorKind.HeapLimit,
        }, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(1, Create("(?i)(a+)(b)").Count("ab"u8));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptionAndRetainedCapturesKeepTheirOriginalAuthority(bool cancel)
    {
        long ticks = 0;
        using var token = new CancellationTokenSource();
        var armed = false;
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (armed && ++reads == 100) { if (cancel) token.Cancel(); else ticks = 25; }
            return ticks;
        });
        var regex = Create("(?i)(a+)(?=b)b", timeout: TimeSpan.FromMilliseconds(20));
        var retained = regex.MatchDetailed("aaab"u8);
        var invocation = Pcre2SliceDriver.CreateInvocation(regex.DebugCompiledProgram.Request, token.Token);
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aaab ", 3000)));
        Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.TryCreate(bytes, ref invocation, out var input));
        var validated = invocation.Budget.Steps;
        armed = true;
        Exception? failure = null;
        try
        {
            Pcre2GlobalOperationDriver.TryCount(regex.DebugCompiledProgram, ref input,
                new Utf8BytePosition(0), Pcre2MatchOptions.None, ref invocation, out _);
        }
        catch (Exception error) { failure = error; }
        Assert.True(reads >= 100 && invocation.Budget.Steps > validated);
        Assert.True(invocation.Budget.PeakWorkingBytes > 0);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        if (cancel) Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.True(failure is Utf8ExecutionDeadlineExpiredException or Pcre2MatchException { ErrorKind: Pcre2ErrorKind.Timeout });
        Assert.Equal("aaa", retained.GetGroup(1).GetValueString());
        armed = false;
        ticks = 0;
        Assert.Equal(1, regex.Count("ab"u8));
    }

    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits? limits = null,
        TimeSpan? timeout = null) => new(pattern, Pcre2CompileOptions.None, default,
            limits ?? new() { MatchLimit = 1000000, DepthLimit = 256,
                HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 },
            timeout ?? Timeout.InfiniteTimeSpan);
}
