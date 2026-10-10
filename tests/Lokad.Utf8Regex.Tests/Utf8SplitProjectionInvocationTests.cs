using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SplitProjectionInvocationTests
{
    private static Utf8SliceProgram Program(Utf8Regex regex) =>
        (Utf8SliceProgram)typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_matchCursor")]
    private static extern ref Utf8OperationMatchCursor MatchCursor(ref Utf8ValueSplitEnumerator cursor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_sliceInvocation")]
    private static extern ref Utf8SliceInvocation Invocation(ref Utf8OperationMatchCursor cursor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_startTickCount64")]
    private static extern ref long StartedAt(Utf8ExecutionDeadline deadline);

    public static IEnumerable<object[]> Texts()
    {
        foreach (var text in new[] { "", "a", "é中😀\U00010400\0\uffff", new string('a', 8193),
            string.Concat(Enumerable.Repeat("é中😀", 2200)) }) yield return [text];
        foreach (var width in new[] { "é", "中", "😀" })
        foreach (var prefix in new[] { 2045, 2046, 2047, 2048, 2049 })
            yield return [new string('a', prefix) + width + new string('b', 4097) + width];
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void BoundariesAndSplitGetterKeepDerivedUtf16AcrossEveryScalarBlockBoundary(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out var map, out var bytes));
        Assert.Equal(MapBytes(validation), bytes);
        var view = new Utf8ValueSplit(input, 0, validation.Utf16Length, map);
        Assert.True(view.IsByteAligned);
        Assert.Equal((0, input.Length), (view.IndexInBytes, view.LengthInBytes));
        Assert.Equal((0, text.Length), (view.IndexInUtf16, view.LengthInUtf16));
        Assert.Equal(input, view.GetValueBytes().ToArray());
        Assert.Equal(text, view.GetValueString());
        var unit = 0;
        for (var offset = 0; offset < input.Length;)
        {
            Assert.True(map.Resolve(unit).IsScalarBoundary);
            Assert.Equal(offset, map.Resolve(unit).ByteOffset);
            Assert.Equal(System.Buffers.OperationStatus.Done, Rune.DecodeFromUtf8(input.AsSpan(offset), out var scalar, out var width));
            if (!scalar.IsBmp) Assert.False(map.Resolve(unit + 1).IsScalarBoundary);
            unit += scalar.Utf16SequenceLength;
            offset += width;
        }
        Assert.True(map.Resolve(unit).IsScalarBoundary);
        Assert.Equal(input.Length, map.Resolve(unit).ByteOffset);
        Assert.Equal(7 + bytes, invocation.Ledger.ReservedBytes);
        Assert.True(invocation.Budget.Steps > 0);
        invocation.Ledger.Release(bytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    private static ulong MapBytes(Utf8ValidationResult validation) => validation.IsAscii ? 0UL :
        (ulong)((validation.Utf16Length + 1) + (validation.Utf16Length + 32) / 32) * sizeof(int);

    [Fact]
    public void EveryProjectionWorkAndStorageCutoffReturnsNoPartialMapAndRestoresBorrowedStorage()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("é中😀", 2200)));
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BoundaryMap.TryCreate(input, validation, ref baseline, out _, out _));
        for (ulong cap = 1; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out var map, out var bytes));
            Assert.Equal(0, map.ByteLength);
            Assert.Equal(0UL, bytes);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        input = Encoding.UTF8.GetBytes("é😀中");
        validation = Utf8Validation.Validate(input);
        var required = MapBytes(validation);
        for (ulong cap = 7; cap <= required + 7; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var breach = Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out var map, out var bytes);
            Assert.Equal(cap == required + 7 ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, breach);
            Assert.Equal(cap == required + 7 ? required : 0UL, bytes);
            Assert.Equal(cap == required + 7 ? input.Length : 0, map.ByteLength);
            if (cap == required + 7)
            {
                var view = new Utf8ValueSplit(input, 0, validation.Utf16Length, map);
                Assert.Equal("é😀中", view.GetValueString());
            }
            Assert.Equal(7 + bytes, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(bytes);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionCancellationAndDeadlineObserveExactAuthorityAndRollback(bool deadline)
    {
        var input = Encoding.UTF8.GetBytes(new string('é', 200000));
        var validation = Utf8Validation.Validate(input);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        invocation.CheckpointHook = _ => { if (deadline) Thread.Sleep(250); else source.Cancel(); };
        Assert.True(invocation.Ledger.TryReserve(7));
        var map = default(Utf8BoundaryMap);
        ulong bytes = 0;
        var interrupted = false;
        try { Utf8BoundaryMap.TryCreate(input, validation, ref invocation, out map, out bytes); }
        catch (Utf8ExecutionDeadlineExpiredException) { Assert.True(deadline); interrupted = true; }
        catch (OperationCanceledException exception) { Assert.False(deadline); Assert.Equal(source.Token, exception.CancellationToken); interrupted = true; }
        Assert.True(interrupted);
        Assert.Equal(0, map.ByteLength);
        Assert.Equal(0UL, bytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void FinalProjectionPublicationChecksCancellationAndImpossibleExtentBeforeAllocation()
    {
        var input = Encoding.UTF8.GetBytes("é😀中");
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Utf8BoundaryMap.Create(input, validation, ref baseline, out _);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        while (invocation.Budget.Steps < 64 - baseline.Budget.Steps) invocation.TryChargeStep();
        invocation.CheckpointHook = _ => source.Cancel();
        Assert.True(invocation.Ledger.TryReserve(7));
        var interrupted = false;
        try { Utf8BoundaryMap.Create(input, validation, ref invocation, out _); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); interrupted = true; }
        Assert.True(interrupted);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation = new(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, Utf8BoundaryMap.TryCreate([], new(0, int.MaxValue, false, false), ref invocation, out var map, out var bytes));
        Assert.Equal(0, map.ByteLength);
        Assert.Equal(0UL, bytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ProjectionExceptionAfterAllocationRestoresItsAdmission()
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var failed = false;
        // Deliberately inconsistent internal validation forces an exception
        // after map allocation. Public preparation obtains real validation.
        try { Utf8BoundaryMap.Create("abc"u8, new(3, 2, false, false), ref invocation, out _); }
        catch (IndexOutOfRangeException) { failed = true; }
        Assert.True(failed);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryPreparationWorkCutoffAndOverlappingStorageAdmissionRestoreOnlyBorrowedCapacity(int count)
    {
        var regex = new Utf8Regex("(ab)", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("é😀 abab ab");
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(7));
        var prepared = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref baseline);
        var steps = prepared.DebugInvocationSteps;
        var total = prepared.DebugInvocationReservedBytes;
        var map = (ulong)((validation.Utf16Length + 1) + (validation.Utf16Length + 32) / 32) * 4;
        Assert.Equal(total, prepared.DebugPeakWorkingBytes);
        Assert.Equal(map + 7, total);
        for (ulong cap = 1; cap < steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var denied = false;
            try { _ = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref invocation); }
            catch (InvalidOperationException) { denied = true; }
            Assert.True(denied);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
        foreach (var cap in new[] { map + 6, map + 7, total - 1, total }.Distinct())
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var denied = false;
            try
            {
                var cursor = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref invocation);
                Assert.Equal(total, cursor.DebugInvocationReservedBytes);
            }
            catch (InvalidOperationException) { denied = true; }
            Assert.Equal(cap < total, denied);
            if (denied) Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    public static IEnumerable<object[]> LifetimeCases()
    {
        foreach (var pattern in new[] { "ab", "", "(.)", @"(ab)\1" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var count in new[] { 0, 1, 2 }) yield return [pattern, compiled, rtl, count];
    }

    [Theory]
    [MemberData(nameof(LifetimeCases))]
    public void CountOneFinalTailAndTerminalMovesObserveTheOriginalDeadlineAndExactToken(string pattern, bool compiled, bool rtl, int count)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var input = Encoding.UTF8.GetBytes("é😀 abab ab");
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(30));
        var cursor = regex.EnumerateSplits(input, count);
        if (count == 2) Assert.True(cursor.MoveNext());
        if (count == 0) while (cursor.MoveNext()) { }
        var prior = (cursor.Current.IndexInUtf16, cursor.Current.LengthInUtf16);
        // Expire the actual deadline retained by the public factory after its
        // successful advances, without depending on scheduler or GC latency.
        var deadline = Invocation(ref MatchCursor(ref cursor)).Budget.Deadline;
        Assert.False(deadline.IsInfinite);
        StartedAt(deadline) = Environment.TickCount64 - 30_001;
        var expired = false;
        try { _ = cursor.MoveNext(); }
        catch (RegexMatchTimeoutException exception)
        {
            Assert.Equal(regex.Pattern, exception.Pattern);
            Assert.Equal(regex.MatchTimeout, exception.MatchTimeout);
            Assert.Equal(Encoding.UTF8.GetString(input), exception.Input);
            expired = true;
        }
        Assert.True(expired);
        Assert.Equal(prior, (cursor.Current.IndexInUtf16, cursor.Current.LengthInUtf16));
        using var source = new CancellationTokenSource();
        var token = new Utf8Regex(pattern, options).EnumerateSplits(input, count, source.Token);
        if (count == 2) Assert.True(token.MoveNext());
        if (count == 0) while (token.MoveNext()) { }
        prior = (token.Current.IndexInUtf16, token.Current.LengthInUtf16);
        source.Cancel();
        var canceled = false;
        try { _ = token.MoveNext(); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
        Assert.True(canceled);
        Assert.Equal(prior, (token.Current.IndexInUtf16, token.Current.LengthInUtf16));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void EofAndInterruptedMovesRetainMapStorageForExistingViews(bool rtl, int count)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex("(ab)", options);
        var input = Encoding.UTF8.GetBytes("é😀 abab ab");
        var validation = Utf8Validation.Validate(input);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var view = FinishSplit(regex, input, count, ref invocation, out var cursor);
        var value = view.GetValueString();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var held = 7 + (ulong)((validation.Utf16Length + 1) + (validation.Utf16Length + 32) / 32) * 4;
        Assert.Equal(held, cursor.DebugInvocationReservedBytes);
        Assert.Equal(value, view.GetValueString());
        source.Cancel();
        var canceled = false;
        try { _ = cursor.MoveNext(); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
        Assert.True(canceled);
        Assert.Equal(held, cursor.DebugInvocationReservedBytes);
        Assert.Equal(value, view.GetValueString());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8ValueSplit FinishSplit(Utf8Regex regex, byte[] input, int count,
        ref Utf8SliceInvocation invocation, out Utf8ValueSplitEnumerator cursor)
    {
        cursor = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref invocation);
        Assert.True(cursor.MoveNext());
        var view = cursor.Current;
        while (cursor.MoveNext()) { }
        return view;
    }
}
