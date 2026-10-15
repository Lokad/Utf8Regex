using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CoreResultPolicyTests
{
    [Fact]
    public void ByteBoundedTimeoutDoesNotDecodeTheSubjectForDiagnostics()
    {
        var input = Enumerable.Repeat((byte)'x', 200_000).ToArray();
        var regex = new Utf8Regex("z", RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(1), new() { MaxResultBytes = 1 });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input));
        Assert.Empty(error.Input);
        Assert.Equal("z", error.Pattern);
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("detail")]
    [InlineData("matches")]
    [InlineData("split")]
    [InlineData("replace")]
    [InlineData("callback-text")]
    public void ByteLimitsOmitTimeoutInputAcrossFailureOwners(string operation)
    {
        var input = Enumerable.Repeat((byte)'x', 200_000).ToArray();
        foreach (var kind in new[] { 0, 1, 2 })
        {
            var policy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = kind != 1 ? 1_048_576ul : null,
                MaxResultBytes = kind != 0 ? 1_048_576ul : null,
            };
            var regex = new Utf8Regex("z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1), policy);
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
            var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, operation, input, default));
            Assert.Empty(error.Input);
            Assert.Equal("z", error.Pattern);
            Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        }
    }

    [Fact]
    public void WorkOnlyTimeoutKeepsExistingDiagnosticInput()
    {
        var regex = new Utf8Regex("z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1),
            new() { MaxWorkSteps = ulong.MaxValue });
        var input = "xxxxxxxx"u8.ToArray();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input));
        Assert.Equal("xxxxxxxx", error.Input);
    }

    [Fact]
    public void CopiedMatchAndSplitCursorsOmitInputAfterAnInterveningWait()
    {
        var regex = new Utf8Regex(".", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200),
            new() { MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 });
        var input = "😀a"u8.ToArray();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var cursor = regex.EnumerateMatches(input);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        ticks = 250;
        try { _ = cursor.MoveNext(); Assert.Fail("Cursor deadline must expire."); }
        catch (RegexMatchTimeoutException error) { Assert.Empty(error.Input); Assert.Equal(".", error.Pattern); }
        try { _ = alias.MoveNext(); Assert.Fail("A copy must keep the same deadline."); }
        catch (RegexMatchTimeoutException error) { Assert.Empty(error.Input); }
        Assert.Equal((0, 4), (retained.IndexInBytes, retained.LengthInBytes));
        Assert.Equal("😀"u8.ToArray(), input[..4]);

        ticks = 0;
        var split = regex.EnumerateSplits(input);
        ticks = 250;
        try { _ = split.MoveNext(); Assert.Fail("Split deadline must expire."); }
        catch (RegexMatchTimeoutException error) { Assert.Empty(error.Input); Assert.Equal(".", error.Pattern); }
    }

    private static Utf8Regex Create(string pattern, ulong result, RegexOptions options = RegexOptions.CultureInvariant,
        ulong? work = null, TimeSpan? timeout = null) => new(pattern, options, timeout ?? Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxResultBytes = result, MaxWorkSteps = work });

    public static IEnumerable<object[]> OperationModes() => Utf8CoreWorkPolicyTests.OperationModes();
    private static string Run(Utf8Regex regex, string operation, byte[] input, CancellationToken token, int offset = 1)
        => Utf8CoreWorkPolicyTests.Run(regex, operation, input, token, offset);

    [Theory]
    [MemberData(nameof(OperationModes))]
    public void EveryAcceptedOperationPreservesResultsAndEnforcesItsDeclaredCapacityScope(string operation, bool compiled, bool live)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        var input = Encoding.UTF8.GetBytes("é😀 ab😀 ab");
        const string pattern = "😀|(?<x>ab)";
        var expected = Run(new Utf8Regex(pattern, options), operation, input, token);
        Assert.Equal(expected, Run(Create(pattern, 1000000, options), operation, input, token));
        Assert.Equal(expected, Run(Create(pattern, ulong.MaxValue, options), operation, input, token));
        if (operation is "is" or "count" or "match")
            Assert.Equal(expected, Run(Create(pattern, 1, options), operation, input, token));
        else AssertResultFailure(() => Run(Create(pattern, 1, options), operation, input, token), 1);
    }

    [Theory]
    [MemberData(nameof(OperationModes))]
    public void ExactAndOneUnderCapacityAndAllSmallCapsHaveDefinedOutcomes(string operation, bool compiled, bool live)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        var input = Encoding.UTF8.GetBytes("éab ab");
        var expected = Run(Create("ab", 1000000, options), operation, input, token);
        ulong first = 1, last = 1000000;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Run(Create("ab", cap, options), operation, input, token)); last = cap; }
            catch (Utf8ResourceLimitExceededException error) { CheckResultFailure(error, cap); first = cap + 1; }
        }
        Assert.Equal(expected, Run(Create("ab", first, options), operation, input, token));
        if (first > 1) AssertResultFailure(() => Run(Create("ab", first - 1, options), operation, input, token), first - 1);
        for (ulong cap = 1; cap <= 64; cap++)
        {
            if (cap >= first) Assert.Equal(expected, Run(Create("ab", cap, options), operation, input, token));
            else AssertResultFailure(() => Run(Create("ab", cap, options), operation, input, token), cap);
        }
    }

    [Theory]
    [InlineData("^abc", "abc")]
    [InlineData("abc$", "xxabc")]
    [InlineData("[0-9]{4}-[0-9]{2}-[0-9]{2}", "2026-10-11")]
    [InlineData("(?:get|set)[A-Z][A-Za-z0-9_]*", "getAlpha setBeta")]
    public void NativeEligibleMaterializingOperationsDoNotBypassThePolicy(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        foreach (var operation in new[] { "detail", "replace", "replace-bytes", "replace-text", "destination", "destination-bytes", "callback-bytes", "callback-text" })
        {
            Assert.Equal(Run(new Utf8Regex(pattern, options), operation, input, default),
                Run(Create(pattern, 1000000, options), operation, input, default));
            AssertResultFailure(() => Run(Create(pattern, 1, options), operation, input, default), 1);
        }
    }

    [Theory]
    [InlineData("(?<x>😀)+\\k<x>", false)]
    [InlineData("(?<x>😀)+\\k<x>", true)]
    [InlineData("(?'x'a)+(?'-x'b)+", false)]
    [InlineData("(?=(.))", false)]
    [InlineData("", false)]
    [InlineData("", true)]
    public void HistoryRollbackScalarBoundariesAndRtlOutputTablesKeepTheirSemantics(string pattern, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0);
        var input = Encoding.UTF8.GetBytes("é😀😀 aaabb 😀");
        foreach (var operation in new[] { "detail", "detail-offset", "matches", "split", "replace", "replace-text", "callback-bytes", "callback-text" })
            Assert.Equal(Run(new Utf8Regex(pattern, options), operation, input, default),
                Run(Create(pattern, 1000000, options), operation, input, default));
        var dot = Create(".", 1, options);
        Assert.Equal(4, dot.Match("😀"u8).LengthInBytes);
    }

    [Fact]
    public void NonBacktrackingResultHistoriesAreAdmittedAndPreserveLastCaptureSemantics()
    {
        var input = Encoding.UTF8.GetBytes("é😀中");
        var options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
        var context = Create("(?<x>.)+", 1000000, options).MatchDetailed(input);
        Assert.Equal(1, context.GetGroup("x").CaptureCount);
        Assert.Equal("中", context.GetGroup("x").GetValueString());
        AssertResultFailure(() => Create("(?<x>.)+", 1, options).MatchDetailed(input), 1);
    }

    [Fact]
    public void MajorMapAllocationIsDeniedBeforeAcquisitionAndPreservesBorrowedCapacity()
    {
        var validation = new Utf8ValidationResult(0, 5000000, false, false);
        var warm = new Utf8SliceInvocation(new(0, 0, 0, 1), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, Utf8BoundaryMap.TryCreate([], validation, ref warm, out _, out _));
        var invocation = new Utf8SliceInvocation(new(0, 0, 0, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Utf8BoundaryMap.TryCreate([], validation, ref invocation, out _, out var reservation);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, failure);
        Assert.Equal(0UL, reservation);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.True(allocated < 16384, $"Denied map allocated {allocated} bytes.");
    }

    [Fact]
    public void MajorCaptureProjectionIsDeniedBeforeArrayAcquisition()
    {
        var small = new Utf8SliceSearchOutcome(true, 0, 1, [[0]], [[1]], Utf8ExecutionLimitKind.None);
        var map = Utf8BoundaryMap.Create("a"u8);
        var warm = new Utf8SliceInvocation(new(0, 0, 0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        var prepared = Utf8SliceCaptureProjection.Create(small, map, ref warm);
        prepared.Release(ref warm);
        var large = new Utf8SliceSearchOutcome(true, 0, 1, [new int[1000000]], [new int[1000000]], Utf8ExecutionLimitKind.None);
        var invocation = new Utf8SliceInvocation(new(0, 0, 0, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var before = GC.GetAllocatedBytesForCurrentThread();
        try { Utf8SliceCaptureProjection.Create(large, map, ref invocation); Assert.Fail("Projection must deny before large arrays."); }
        catch (InvalidOperationException) { Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.Budget.LimitBreach); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.True(allocated < 16384, $"Denied projection allocated {allocated} bytes.");
    }

    [Fact]
    public void ResultHistoryFailureReturnsNoPublicationAndReleasesWorkingState()
    {
        var program = Create("(?<x>a)+", 1000000).Inspection.PreparedRegex.ScalarProgram!;
        var invocation = new Utf8SliceInvocation(new(0, 0, 0, 1), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        var borrowed = invocation.Ledger.ReservedBytes;
        try
        {
            var outcome = Utf8SliceRunner.TryFindFirst(program, "aaaa"u8, 0, slots.Starts, slots.Ends, true, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, outcome.Breach);
            Assert.False(outcome.Found);
            Assert.Equal(borrowed, invocation.Ledger.ReservedBytes);
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ResultTrafficIsSharedAcrossInvocationCopiesAndNeverRefundedByRelease()
    {
        var input = "é😀"u8;
        var validation = Utf8Validation.Validate(input);
        var probe = new Utf8SliceInvocation(new(0, 0, 0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        var map = Utf8BoundaryMap.Create(input, validation, ref probe, out var bytes);
        probe.Ledger.Release(bytes);
        Assert.Equal(bytes, probe.Budget.ResultBytes);
        var invocation = new Utf8SliceInvocation(new(0, 0, 0, bytes), Regex.InfiniteMatchTimeout, default);
        var retained = Utf8BoundaryMap.Create(input, validation, ref invocation, out var firstBytes);
        var alias = invocation;
        invocation.Ledger.Release(firstBytes);
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, Utf8BoundaryMap.TryCreate(input, validation, ref alias, out _, out _));
        Assert.Equal(alias.Budget.ResultBytes, invocation.Budget.ResultBytes);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(3, retained.GetUtf16OffsetForByteOffset(6));
        Assert.Equal(map.ByteLength, retained.ByteLength);
    }

    [Fact]
    public void CopiedPublicCursorsAndRetainedViewsRemainValidWithExactMapAdmission()
    {
        var input = Encoding.UTF8.GetBytes("é😀 ab ab");
        var probe = new Utf8SliceInvocation(new(0, 0, 0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        var validation = Utf8Validation.Validate(input);
        _ = Utf8BoundaryMap.Create(input, validation, ref probe, out var bytes);
        var regex = Create("ab", bytes);
        var cursor = regex.EnumerateMatches(input);
        var copy = cursor;
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        Assert.True(copy.MoveNext());
        Assert.Equal(retained.IndexInBytes, copy.Current.IndexInBytes);
        Assert.True(cursor.MoveNext());
        Assert.False(cursor.MoveNext());
        Assert.True(copy.MoveNext());
        Assert.False(copy.MoveNext());
        Assert.Equal("ab", Encoding.UTF8.GetString(input.AsSpan(retained.IndexInBytes, retained.LengthInBytes)));
        var splits = regex.EnumerateSplits(input);
        Assert.True(splits.MoveNext());
        var view = splits.Current;
        while (splits.MoveNext()) { }
        Assert.Equal("é😀 ", view.GetValueString());
    }

    [Fact]
    public void OutputGrowthAndResetControlsConsumeTheOriginalAllowance()
    {
        var content = new string('é', 1000000);
        var regex = Create("ab", 4096);
        AssertResultFailure(() => regex.ReplaceToString("ab"u8, content,
            static (in Utf8MatchContext match, ref string value) => value), 4096);
        AssertResultFailure(() => regex.Replace("ab"u8, 0,
            static (in Utf8MatchContext match, ref Utf8ReplacementWriter writer, ref int resets) =>
            {
                for (var i = 0; i < 1000; i++) { writer = default; writer.Append(ReadOnlySpan<byte>.Empty); resets++; }
            }), 4096);
        // Failure restores the synchronous scope; an independent operation gets a fresh allowance.
        Assert.Equal("<ab>", Create("ab", 1000000).ReplaceToString("ab"u8, "<$&>"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargeOutputGrowthIsDeniedBeforeAcquisition(bool text)
    {
        var bytes = new byte[1000000];
        var chars = new string('x', 1000000);
        var invocation = new Utf8SliceInvocation(new(0, 0, 0, 512), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var previous = Utf8SliceByteOutputScope.Current;
        var scope = Utf8SliceByteOutputScope.Enter(invocation);
        try
        {
            var output = scope.CreateBuffer();
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                if (text) output.Append(chars.AsSpan()); else output.Append(bytes);
                Assert.Fail("Output growth must deny before acquiring a large buffer.");
            }
            catch (InvalidOperationException) { Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.Budget.LimitBreach); }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 16384, $"Denied output allocated {allocated} bytes.");
        }
        finally { scope.Release(); }
        Assert.Same(previous, Utf8SliceByteOutputScope.Current);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.True(invocation.Budget.ResultBytes > 512);
    }

    [Fact]
    public void NestedCallbacksRestoreTheParentResultAllowance()
    {
        var child = Create("ab", 1000000);
        var rejectedChild = Create("ab", 1);
        var parent = Create("ab", 4096);
        var text = new string('x', 10000);
        AssertResultFailure(() => parent.Replace("ab"u8, (child, rejectedChild, text),
            static (in Utf8MatchContext match, ref Utf8ReplacementWriter writer,
                ref (Utf8Regex child, Utf8Regex rejectedChild, string text) state) =>
            {
                Assert.Equal("child", state.child.ReplaceToString("ab"u8, "child"));
                var rejected = state.rejectedChild;
                AssertResultFailure(() => rejected.ReplaceToString("ab"u8, "child"), 1);
                writer = default;
                writer.Append(state.text);
            }), 4096);
        Assert.Equal("child", child.ReplaceToString("ab"u8, "child"));
    }

    [Fact]
    public void FirstDenialAndSaturatingRequestsHaveOneResourceIdentity()
    {
        var resultFirst = new Utf8ExecutionBudget(new(1, 0, 0, 2), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, resultFirst.TryChargeResultBytes(3));
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, resultFirst.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, resultFirst.TryChargeStep());
        Assert.False(resultFirst.WorkLimitExceeded);
        var workFirst = new Utf8ExecutionBudget(new(1, 0, 0, 2), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, workFirst.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, workFirst.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, workFirst.TryChargeResultBytes(3));
        Assert.Equal(0UL, workFirst.ResultBytes);
        var overflow = new Utf8ExecutionBudget(new(0, 0, 0, ulong.MaxValue), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, overflow.TryChargeResultBytes(ulong.MaxValue));
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, overflow.TryChargeResultBytes(1));
        Assert.Equal(ulong.MaxValue, overflow.ResultBytes);
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, overflow.TryChargeResultBytes(0));
        AssertResultFailure(() => Create("ab", 1, work: 100000).ReplaceToString("ab"u8, "Z"), 1);
        var denied = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create("ab", 100000, work: 1).ReplaceToString("ab"u8, "Z"));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, denied.ResourceKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationAndDeadlinePrecedeResultDenial(bool deadline)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 0, 0, 1), deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        if (deadline) clock.Advance(250); else source.Cancel();
        try { invocation.Budget.TryChargeResultBytes(2); Assert.Fail("Interruption must precede result admission."); }
        catch (Utf8ExecutionDeadlineExpiredException) { Assert.True(deadline); }
        catch (OperationCanceledException error) { Assert.False(deadline); Assert.Equal(source.Token, error.CancellationToken); }
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
        var canceled = Assert.Throws<OperationCanceledException>(() =>
        {
            using var already = new CancellationTokenSource(); already.Cancel();
            Create("ab", 1).ReplaceToString("ab"u8, "Z", already.Token);
        });
        Assert.True(canceled.CancellationToken.IsCancellationRequested);
    }

    [Theory]
    [MemberData(nameof(OperationModes))]
    public void GenerousResultBudgetsKeepStrictFullValidationAndOriginalTokens(string operation, bool compiled, bool live)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        var input = "ab x"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => Run(Create("ab", 1000000, options), operation, input, token));
        source.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => Run(Create("ab", 1, options), operation, input, source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
    }

    [Fact]
    public void RetainedResultsStandaloneGettersAndDestinationSizingKeepTheirScope()
    {
        var input = "é😀ab"u8.ToArray();
        var regex = Create("ab", 1000000);
        var context = regex.MatchDetailed(input);
        AssertResultFailure(() => Create("ab", 1).ReplaceToString(input, "Z"), 1);
        Assert.Equal("ab", context.GetValueString());
        var destination = new byte[] { 0x7f };
        Assert.Equal(OperationStatus.DestinationTooSmall, regex.TryReplace(input, "longer", destination, out var written));
        Assert.Equal(0, written);
        Assert.Equal(new byte[] { 0x7f }, destination);
        Assert.Equal(5, (int)Utf8ResourceLimitKind.ExecutionResultBytes);
        Assert.Equal(4, (int)Utf8ResourceLimitKind.ExecutionWorkSteps);
    }

    private static void AssertResultFailure(Action action, ulong cap) => CheckResultFailure(Assert.Throws<Utf8ResourceLimitExceededException>(action), cap);
    private static void CheckResultFailure(Utf8ResourceLimitExceededException error, ulong cap)
    {
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
        Assert.Equal(cap, error.Limit);
        Assert.True(error.Requested > cap || cap == ulong.MaxValue && error.Requested == cap);
    }
}
