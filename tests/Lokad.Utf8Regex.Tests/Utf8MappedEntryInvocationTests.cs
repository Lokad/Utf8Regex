using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MappedEntryInvocationTests
{
    private static ulong MapBytes(int units) => 64UL
        + ((ulong)(units + 1) * 4 + 15) / 16 * 16
        + ((ulong)(units + 32) / 32 * 4 + 15) / 16 * 16;

    private static readonly string[] Patterns = [@"(ab)\1", @"(?<=a+)b", @"(?<x>a)+\k<x>",
        @"(?:(?<x>a)|b)+", @"(a|b)*c", @"(?i:a+)"];

    public static IEnumerable<object[]> EntryCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var mixed in new[] { false, true })
        foreach (var split in new[] { false, true }) yield return [pattern, compiled, mixed, split];
    }

    [Theory]
    [MemberData(nameof(EntryCases))]
    public void InitialMaterializationObservesTheExactTokenBeforeReturning(string pattern, bool compiled, bool mixed, bool split)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0), TimeSpan.FromSeconds(10));
        var input = Encoding.UTF8.GetBytes(new string(mixed ? 'é' : 'x', 200000));
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            var canceled = false;
            try
            {
                if (split) _ = regex.EnumerateSplits(input, source.Token);
                else _ = regex.EnumerateMatches(input, source.Token);
            }
            catch (OperationCanceledException exception)
            {
                Assert.Equal(source.Token, exception.CancellationToken);
                canceled = true;
            }
            Assert.True(canceled);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
        finally { session.Complete(); }
    }

    public static IEnumerable<object[]> SemanticCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [pattern, compiled, rtl];
    }

    [Theory]
    [MemberData(nameof(SemanticCases))]
    public void CompleteOperationsPreserveUnitsCapturesAnchorsAndSplitTail(string pattern, bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, options),
            ["", "abab aaab aabc", "é😀 abab 中aaab aabc 😀", "abab\naaab\naabc"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("é中😀\U00010400abc")]
    [InlineData("é中\0\uffff")]
    public void MapMatchesEveryOriginalScalarAndSurrogateSplitBoundary(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var map = Utf8BoundaryMap.Create(input, validation, ref invocation, out var reservation);
        var original = Utf8BoundaryMap.Create(input, validation);
        Assert.Equal(7 + reservation, invocation.Ledger.ReservedBytes);
        Assert.Equal(validation.IsAscii ? 0UL : MapBytes(text.Length), reservation);
        for (var unit = 0; unit <= text.Length; unit++) Assert.Equal(original.Resolve(unit), map.Resolve(unit));
        for (var offset = 0; offset <= input.Length; offset++)
        {
            Assert.Equal(original.TryGetUtf16OffsetForByteOffset(offset, out var expected), map.TryGetUtf16OffsetForByteOffset(offset, out var actual));
            Assert.Equal(expected, actual);
        }
        invocation.Ledger.Release(reservation);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void EveryWorkAndStorageCutoffRollsBackOnlyTheMapReservation()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("é😀", 2200)));
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BoundaryMap.TryCreate(input, validation, ref baseline, out _, out var bytes));
        for (ulong limit = 1; limit < baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out var map, out var reservation));
            Assert.Equal(0, map.Utf16Length);
            Assert.Equal(0UL, reservation);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        // Sweep all extents on a small independent map, including the exact cap.
        input = Encoding.UTF8.GetBytes("é😀abc");
        validation = Utf8Validation.Validate(input);
        baseline = new(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Utf8BoundaryMap.Create(input, validation, ref baseline, out bytes);
        for (ulong cap = 7; cap <= bytes + 7; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var breach = Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out _, out var reservation);
            Assert.Equal(cap == bytes + 7 ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, breach);
            Assert.Equal(cap == bytes + 7 ? bytes : 0UL, reservation);
            Assert.Equal(7 + reservation, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationAndDeadlineDuringBuildRestoreBorrowedStorage(bool deadline)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var input = Encoding.UTF8.GetBytes(new string('é', 200000));
        var validation = Utf8Validation.Validate(input);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        invocation.CheckpointHook = _ => { if (deadline) clock.Advance(250); else source.Cancel(); };
        Assert.True(invocation.Ledger.TryReserve(7));
        try
        {
            _ = Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out _, out _);
            Assert.Fail("Map publication must observe the interrupted invocation.");
        }
        catch (OperationCanceledException exception) { Assert.False(deadline); Assert.Equal(source.Token, exception.CancellationToken); }
        catch (Utf8ExecutionDeadlineExpiredException) { Assert.True(deadline); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void OversizedMapIsRejectedBeforeAllocation()
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, Utf8BoundaryMap.TryCreate([], new(0, int.MaxValue, false, false),
            ref invocation, out _, out var reservation));
        Assert.Equal(0UL, reservation);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LastHolderCollectionReleasesOnlyUnretainedCoordinateStorage(bool rtl, bool retain)
    {
        var regex = new Utf8Regex("(ab)", RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0));
        var input = Encoding.UTF8.GetBytes("é😀 abab 中abab");
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var validation = Utf8Validation.Validate(input, ref invocation);
        var map = FinishMappedCursor(regex, input, validation, retain, ref invocation);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var mapBytes = MapBytes(validation.Utf16Length);
        Assert.Equal(retain ? mapBytes + 7 : 7UL, invocation.Ledger.ReservedBytes);
        if (retain) Assert.Equal(validation.EndBoundary, map.Resolve(map.Utf16Length));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8BoundaryMap FinishMappedCursor(Utf8Regex regex, byte[] input, Utf8ValidationResult validation,
        bool retain, ref Utf8SliceInvocation invocation)
    {
        var program = (Utf8SliceProgram)typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
        var cursor = Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref invocation, retain, out var map);
        var mapBytes = MapBytes(validation.Utf16Length);
        Assert.True(cursor.DebugInvocationSteps >= invocation.Budget.Steps);
        Assert.Equal(mapBytes + 7, cursor.DebugInvocationReservedBytes);
        var expected = new Regex(regex.Pattern, regex.Options).Matches("é😀 abab 中abab");
        Assert.Equal(4, expected.Count);
        foreach (Match match in expected)
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal((match.Index, match.Length), (cursor.CurrentValueMatch.IndexInUtf16, cursor.CurrentValueMatch.LengthInUtf16));
        }
        var previous = cursor.CurrentValueMatch;
        Assert.False(cursor.MoveNext());
        Assert.Equal(previous, cursor.CurrentValueMatch);
        Assert.False(cursor.MoveNext());
        // Physical lifetime follows every value holder, including Debug's
        // hidden temporaries. The caller verifies collection after this frame.
        return retain ? map : default;
    }

    [Theory]
    [InlineData(200000)] // filling offsets
    [InlineData(1200000)] // clearing boundary bits
    public void CancellationDuringBothMapInitializationPhasesRollsBack(int units)
    {
        var input = Encoding.UTF8.GetBytes(new string('é', units));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        invocation.CheckpointHook = _ => source.Cancel();
        Assert.True(invocation.Ledger.TryReserve(7));
        var canceled = false;
        try { Utf8BoundaryMap.Create(input, Utf8Validation.Validate(input), ref invocation, out _); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
        Assert.True(canceled);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void CancellationAtFinalMapPublicationReturnsNeitherMapNorReservation()
    {
        var input = Encoding.UTF8.GetBytes("é😀abc");
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Utf8BoundaryMap.Create(input, validation, ref baseline, out _);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        while (invocation.Budget.Steps < 64 - baseline.Budget.Steps) invocation.TryChargeStep();
        invocation.CheckpointHook = _ => source.Cancel();
        Assert.True(invocation.Ledger.TryReserve(7));
        var map = default(Utf8BoundaryMap);
        ulong reservation = 0;
        var canceled = false;
        try { Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out map, out reservation); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
        Assert.True(canceled);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(0, map.Utf16Length);
        Assert.Equal(0UL, reservation);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void MalformedScalarRetainsOriginalErrorOffsetAndRollsBackAdmission()
    {
        byte[] input = [0xc3, 0xa9, 0xff];
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var thrown = false;
        try { Utf8BoundaryMap.Create(input, new(3, 2, false, false), ref invocation, out _); }
        catch (ArgumentException exception) { Assert.Equal(Utf8Validation.CreateInvalidUtf8Exception(2).Message, exception.Message); thrown = true; }
        Assert.True(thrown);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void EveryConstructorWorkCutoffRestoresTheCallersBorrowedAggregate()
    {
        var regex = new Utf8Regex("(ab)", RegexOptions.CultureInvariant);
        var program = (Utf8SliceProgram)typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("é😀ab", 1100)));
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        var entrySteps = baseline.Budget.Steps;
        var cursor = Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref baseline, false, out _);
        var totalSteps = cursor.DebugInvocationSteps;
        Assert.True(totalSteps > entrySteps);
        Assert.Equal(totalSteps, baseline.Budget.Steps);
        for (ulong cap = 1; cap < totalSteps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var denied = false;
            try { _ = Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref invocation, false, out _); }
            catch (InvalidOperationException) { denied = true; }
            Assert.True(denied);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void InterruptedMovePreservesPublishedRangeAndReleasesOnlyOwnedStorage(bool rtl, bool retain)
    {
        var regex = new Utf8Regex("(ab)", RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0));
        var input = Encoding.UTF8.GetBytes("é😀 ab 中ab");
        var validation = Utf8Validation.Validate(input);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var map = InterruptMappedCursor(regex, input, validation, retain, source, ref invocation);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var mapBytes = MapBytes(validation.Utf16Length);
        Assert.Equal(retain ? mapBytes + 7 : 7UL, invocation.Ledger.ReservedBytes);
        if (retain) Assert.Equal(validation.EndBoundary, map.Resolve(map.Utf16Length));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8BoundaryMap InterruptMappedCursor(Utf8Regex regex, byte[] input, Utf8ValidationResult validation,
        bool retain, CancellationTokenSource source, ref Utf8SliceInvocation invocation)
    {
        var program = (Utf8SliceProgram)typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
        var cursor = Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref invocation, retain, out var map);
        Assert.True(cursor.MoveNext());
        var previous = cursor.CurrentValueMatch;
        source.Cancel();
        var canceled = false;
        try { _ = cursor.MoveNext(); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
        Assert.True(canceled);
        Assert.Equal(previous, cursor.CurrentValueMatch);
        return retain ? map : default;
    }
}
