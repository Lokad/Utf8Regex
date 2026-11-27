using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using static Lokad.Utf8Regex.Tests.Utf8CoreOperationTests;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CoreWorkingPolicyTests
{
    private static Utf8Regex Create(string pattern, ulong bytes, RegexOptions options)
        => new(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = bytes
            }
        });

    private static void Denied(Utf8ResourceLimitExceededException error, ulong bytes)
    {
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        Assert.Equal(bytes, error.Limit);
        Assert.True(error.Requested > bytes);
    }

    private static void Denied(Utf8ResourceLimitExceededException error, ulong bytes, string operation)
    {
        if (operation is "replace" or "destination" &&
            error.ResourceKind == Utf8ResourceLimitKind.ConstructionBytes)
        {
            // Template preparation has its own additional-capacity ceiling
            // before execution; diagnostics include its construction parent.
            Assert.True(error.Limit >= bytes);
            Assert.True(error.Requested > error.Limit);
        }
        else Denied(error, bytes);
    }

    [Theory]
    [MemberData(nameof(OperationModes), MemberType = typeof(Utf8CoreOperationTests))]
    public void AllPublicOperationsHaveFiniteWorkingSuccessAndExactCutoff(string operation, bool compiled, bool live)
    {
        const string pattern = "(?<x>😀)+(?<y>😀ab)";
        var input = "😀😀😀ab"u8.ToArray();
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        var expected = Utf8CoreOperationTests.Run(new Utf8Regex(pattern, options: options), operation, input, token, 0);
        Assert.Equal(expected, Utf8CoreOperationTests.Run(Create(pattern, ulong.MaxValue, options), operation, input, token, 0));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8CoreOperationTests.Run(Create(pattern, 1, options), operation, input, token, 0)), 1, operation);
        ulong low = 1, high = 1_048_576;
        while (high - low > 1)
        {
            var middle = low + (high - low) / 2;
            try
            {
                Assert.Equal(expected, Utf8CoreOperationTests.Run(Create(pattern, middle, options), operation, input, token, 0));
                high = middle;
            }
            catch (Utf8ResourceLimitExceededException error) { Denied(error, middle, operation); low = middle; }
        }
        Assert.Equal(expected, Utf8CoreOperationTests.Run(Create(pattern, high, options), operation, input, token, 0));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8CoreOperationTests.Run(Create(pattern, high - 1, options), operation, input, token, 0)), high - 1, operation);
    }

    [Theory]
    [InlineData("abc", "xxabc", RegexOptions.None)]
    [InlineData("😀", "é😀", RegexOptions.Compiled)]
    [InlineData("[a-z]+", "abc", RegexOptions.RightToLeft)]
    [InlineData("(?<=😀)(?<n>é+)\\k<n>", "😀éé", RegexOptions.Compiled)]
    [InlineData(".*?", "😀a", RegexOptions.None)]
    [InlineData("(😀|😀😀)+", "😀😀😀", RegexOptions.NonBacktracking | RegexOptions.Compiled)]
    public void NativeShapesAssertionsReferencesRtlAndLinearRoutesStayScalar(string pattern, string text, RegexOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var operation in Operations)
            Assert.Equal(Utf8CoreOperationTests.Run(new Utf8Regex(pattern, options: options), operation, input, default, 0),
                Utf8CoreOperationTests.Run(Create(pattern, 1_048_576, options), operation, input, default, 0));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void SmallFixedCompoundRepeatFitsWithoutACounterJournal(RegexOptions options)
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        using var regex = Create(pattern, 600, options);
        Assert.True(regex.IsMatch("012.200.033.199"u8));
    }

    [Fact]
    public void FixedCompoundWithShortDecimalRunsFitsOneWorkingByte()
    {
        using var regex = Create(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", 1, RegexOptions.None);
        using var source = new CancellationTokenSource();
        Assert.True(regex.IsMatch("Ω 12.3.45.6"u8, source.Token));
        Assert.True(regex.IsMatch("𐐀 𝟘.١.2.３"u8, source.Token));
    }

    [Fact]
    public void SmallFixedCapturedRepeatNeedsNoStorageForBooleanMatch()
    {
        using var regex = Create(@"(?<part>\d{4}[- ]){3}\d{3,4}", 1, RegexOptions.Compiled);
        Assert.True(regex.IsMatch("1234-5678-1234-456"u8));
        Assert.False(regex.IsMatch("1234-5678-1234-45X"u8));
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth)]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkingBytes)]
    [InlineData(Utf8ResourceLimitKind.ExecutionResultBytes)]
    public void CombinedPolicyPreservesEachPublicFailureKind(Utf8ResourceLimitKind kind)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxDepth = kind == Utf8ResourceLimitKind.ExecutionDepth ? 1 : uint.MaxValue,
            MaxWorkingBytes = kind == Utf8ResourceLimitKind.ExecutionWorkingBytes ? 1 : ulong.MaxValue,
            MaxResultBytes = kind == Utf8ResourceLimitKind.ExecutionResultBytes ? 1 : ulong.MaxValue
        };
        var regex = new Utf8Regex("(?<x>😀)+(?<y>😀ab)", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor("😀😀😀ab"u8.ToArray())));
        Assert.Equal(kind, error.ResourceKind); Assert.Equal(1UL, error.Limit); Assert.True(error.Requested > 1);
    }

    [Fact]
    public void AdmissionHasOneSharedStickyAuthorityAndNeverAcquiresDeniedCapacity()
    {
        var invocation = new Utf8SliceInvocation(new(0, 128), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(71));
        var copy = invocation;
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, copy.TryReserveWorking(58));
        Assert.Equal(71UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(129UL, invocation.Budget.PeakWorkingBytes);
        Denied(invocation.Budget.CreateResourceFailure(), 128);
        copy.Ledger.Release(64);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.TryReserveWorking(0));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.CheckInput());
    }

    [Fact]
    public void LargestFiniteAllowanceStillDeniesOverflowAndTemporaryReleaseIsReusable()
    {
        var invocation = new Utf8SliceInvocation(new(0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(ulong.MaxValue));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.TryReserveWorking(1));
        Assert.Equal(ulong.MaxValue, invocation.Ledger.ReservedBytes);
        var error = invocation.Budget.CreateResourceFailure();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        Assert.Equal(ulong.MaxValue, error.Limit); Assert.Equal(ulong.MaxValue, error.Requested);
        var fresh = new Utf8SliceInvocation(new(0, 64), Regex.InfiniteMatchTimeout, default);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, fresh.TryReserveWorking(64));
            fresh.Ledger.Release(64);
        }
        Assert.Equal(0UL, fresh.Ledger.ReservedBytes); Assert.Equal(64UL, fresh.Budget.PeakWorkingBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionPrecedesDeniedWorkingAdmission(bool deadline)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 8),
            deadline ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(7));
        if (deadline) ticks = 25;
        else source.Cancel();
        if (deadline) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => invocation.TryReserveWorking(2));
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => invocation.TryReserveWorking(2)).CancellationToken);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StateGrowthChecksDeadlineAfterCallbacksAndRefunds(bool allowanceExpires)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var allowance = allowanceExpires ? new GrowthAllowance(() => ticks = 25) : null;
        var invocation = new Utf8SliceInvocation(new(0, 4096),
            TimeSpan.FromMilliseconds(20), default, allowance);
        if (!allowanceExpires)
        {
            for (var step = 0; step < 63; step++)
                Assert.Equal(Utf8ExecutionLimitKind.None, invocation.CheckProgress());
            invocation.CheckpointHook = _ => ticks = 25;
        }

        var buffer = new Utf8InvocationStateBuffer<int>();
        var pending = buffer.PendingGrowthBytes;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => buffer.TryPush(1, ref invocation));
        Assert.Equal(pending, buffer.PendingGrowthBytes);
        Assert.Equal(0, buffer.Count);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(0UL, allowance?.Used ?? 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopiedCursorWorkingFailurePreservesTheFirstRangeAndSharedDenial(bool compiled)
    {
        // A late assertion keeps the ambiguous branch on the scratch-using verifier.
        const string pattern = "(ab)|(😀|😀😀)+ab(?=!)";
        var input = Encoding.UTF8.GetBytes("ab " + string.Concat(Enumerable.Repeat("😀", 70)) + "ab!");
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var program = new Utf8Regex(pattern, options: options).Inspection.AvailableOwnedSliceProgram!;
        var invocation = new Utf8SliceInvocation(new(0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        invocation.ShareStorageForCursor();
        var probe = new Utf8OperationMatchCursor(program, input, invocation);
        Assert.True(probe.MoveNext());
        var cap = probe.DebugPeakWorkingBytes;
        var regex = Create(pattern, cap, options);
        var cursor = regex.EnumerateMatches(input);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        try { alias.MoveNext(); Assert.Fail("The later ambiguous run must exceed the first match's working peak."); }
        catch (Utf8ResourceLimitExceededException error) { Denied(error, cap); }
        try { cursor.MoveNext(); Assert.Fail("Copies must share the first denial."); }
        catch (Utf8ResourceLimitExceededException error) { Denied(error, cap); }
        Assert.Equal((0, 2), (retained.StartOffsetInBytes, (retained.EndOffsetInBytes - retained.StartOffsetInBytes)));
        Assert.True(regex.IsMatch("ab"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiniteWorkingPolicyStillChecksScalarOffsets(bool compiled)
    {
        var regex = Create("ab", 1_048_576, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("😀ab"u8, 1, []));
        Assert.Equal(4, regex.Match("😀ab"u8, 4, []).StartOffsetInBytes);
    }

    private sealed class GrowthAllowance(Action onReserve) : IUtf8MemoryAllowance
    {
        internal ulong Used { get; private set; }

        public bool TryReserve(ulong bytes)
        {
            Used = checked(Used + bytes);
            onReserve();
            return true;
        }

        public void Release(ulong bytes) => Used = checked(Used - bytes);
    }
}
