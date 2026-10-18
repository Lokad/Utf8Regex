using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CoreWorkingPolicyTests
{
    private static readonly string[] Operations = ["is", "count", "match", "detail", "is-offset", "count-offset",
        "match-offset", "detail-offset", "matches", "matches-offset", "split", "split-count", "replace",
        "replace-bytes", "replace-text", "destination", "destination-bytes", "callback-bytes", "callback-text"];

    private static Utf8Regex Create(string pattern, ulong bytes, RegexOptions options)
        => new(pattern, options, Regex.InfiniteMatchTimeout, new Utf8ResourcePolicy { MaxWorkingBytes = bytes });

    private static void Denied(Utf8ResourceLimitExceededException error, ulong bytes)
    {
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        Assert.Equal(bytes, error.Limit);
        Assert.True(error.Requested > bytes);
    }

    private static void Denied(Utf8ResourceLimitExceededException error, ulong bytes, string operation)
    {
        if (operation is "replace" or "replace-bytes" or "replace-text" or "destination" or "destination-bytes" &&
            error.ResourceKind == Utf8ResourceLimitKind.ConstructionBytes)
        {
            // Template preparation has its own additional-capacity ceiling
            // before execution; diagnostics include its construction parent.
            Assert.True(error.Limit >= bytes);
            Assert.True(error.Requested > error.Limit);
        }
        else Denied(error, bytes);
    }

    public static IEnumerable<object[]> Modes()
    {
        foreach (var operation in Operations)
        foreach (var compiled in new[] { false, true })
        foreach (var live in new[] { false, true }) yield return [operation, compiled, live];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void AllPublicOperationsHaveFiniteWorkingSuccessAndExactCutoff(string operation, bool compiled, bool live)
    {
        const string pattern = "(?<x>😀+)(?<y>ab)";
        var input = "😀😀ab"u8.ToArray();
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        var expected = Utf8CoreWorkPolicyTests.Run(new Utf8Regex(pattern, options), operation, input, token, 0);
        Assert.Equal(expected, Utf8CoreWorkPolicyTests.Run(Create(pattern, ulong.MaxValue, options), operation, input, token, 0));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8CoreWorkPolicyTests.Run(Create(pattern, 1, options), operation, input, token, 0)), 1, operation);
        ulong low = 1, high = 1_048_576;
        while (high - low > 1)
        {
            var middle = low + (high - low) / 2;
            try
            {
                Assert.Equal(expected, Utf8CoreWorkPolicyTests.Run(Create(pattern, middle, options), operation, input, token, 0));
                high = middle;
            }
            catch (Utf8ResourceLimitExceededException error) { Denied(error, middle, operation); low = middle; }
        }
        Assert.Equal(expected, Utf8CoreWorkPolicyTests.Run(Create(pattern, high, options), operation, input, token, 0));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8CoreWorkPolicyTests.Run(Create(pattern, high - 1, options), operation, input, token, 0)), high - 1, operation);
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
            Assert.Equal(Utf8CoreWorkPolicyTests.Run(new Utf8Regex(pattern, options), operation, input, default, 0),
                Utf8CoreWorkPolicyTests.Run(Create(pattern, 1_048_576, options), operation, input, default, 0));
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth)]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkingBytes)]
    [InlineData(Utf8ResourceLimitKind.ExecutionResultBytes)]
    public void CombinedPolicyPreservesEachPublicFailureKind(Utf8ResourceLimitKind kind)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = kind == Utf8ResourceLimitKind.ExecutionWorkSteps ? 1 : ulong.MaxValue,
            MaxDepth = kind == Utf8ResourceLimitKind.ExecutionDepth ? 1 : uint.MaxValue,
            MaxWorkingBytes = kind == Utf8ResourceLimitKind.ExecutionWorkingBytes ? 1 : ulong.MaxValue,
            MaxResultBytes = kind == Utf8ResourceLimitKind.ExecutionResultBytes ? 1 : ulong.MaxValue,
        };
        var regex = new Utf8Regex("(?<x>😀+)(?<y>ab)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, policy);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.MatchDetailed("😀😀ab"u8));
        Assert.Equal(kind, error.ResourceKind); Assert.Equal(1UL, error.Limit); Assert.True(error.Requested > 1);
    }

    [Fact]
    public void AdmissionHasOneSharedStickyAuthorityAndNeverAcquiresDeniedCapacity()
    {
        var invocation = new Utf8SliceInvocation(new(0, 0, 128), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(71));
        var copy = invocation;
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, copy.TryReserveWorking(58));
        Assert.Equal(71UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(129UL, invocation.Budget.PeakWorkingBytes);
        Denied(invocation.Budget.CreateResourceFailure(), 128);
        copy.Ledger.Release(64);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.TryReserveWorking(0));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.TryChargeInputStep());
    }

    [Fact]
    public void LargestFiniteAllowanceStillDeniesOverflowAndTemporaryReleaseIsReusable()
    {
        var invocation = new Utf8SliceInvocation(new(0, 0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(ulong.MaxValue));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.TryReserveWorking(1));
        Assert.Equal(ulong.MaxValue, invocation.Ledger.ReservedBytes);
        var error = invocation.Budget.CreateResourceFailure();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        Assert.Equal(ulong.MaxValue, error.Limit); Assert.Equal(ulong.MaxValue, error.Requested);
        var fresh = new Utf8SliceInvocation(new(0, 0, 64), Regex.InfiniteMatchTimeout, default);
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
        var invocation = new Utf8SliceInvocation(new(0, 0, 8),
            deadline ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(7));
        if (deadline) ticks = 25;
        else source.Cancel();
        if (deadline) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => invocation.TryReserveWorking(2));
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => invocation.TryReserveWorking(2)).CancellationToken);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
    }

    [Fact]
    public void ArrayRepresentabilityFailureDoesNotInventAConfiguredBudgetDenial()
    {
        var invocation = new Utf8SliceInvocation(new(0, 0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8BoundaryMap.TryCreate([], new(0, int.MaxValue, false, false), ref invocation, out _, out _));
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => invocation.Budget.CreateResourceFailure());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopiedCursorWorkingFailurePreservesTheFirstRangeAndSharedDenial(bool compiled)
    {
        const string pattern = "(ab)|(😀|😀😀)+ab";
        var input = Encoding.UTF8.GetBytes("ab " + string.Concat(Enumerable.Repeat("😀", 70)) + "ab");
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var program = new Utf8Regex(pattern, options).Inspection.AvailableOwnedSliceProgram!;
        var invocation = new Utf8SliceInvocation(new(0, 0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        var validation = Utf8Validation.Validate(input, ref invocation);
        var probe = Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref invocation, false, out _);
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
        Assert.Equal((0, 2), (retained.IndexInBytes, retained.LengthInBytes));
        Assert.True(regex.IsMatch("ab"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiniteWorkingPolicyStillValidatesMalformedTailsAndScalarOffsets(bool compiled)
    {
        var regex = Create("ab", 1_048_576, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        Assert.Throws<ArgumentException>(() => regex.IsMatch([97, 98, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchFromUtf16Offset("😀ab"u8, 1));
        Assert.Equal(4, regex.MatchFromUtf16Offset("😀ab"u8, 2).IndexInBytes);
    }
}
