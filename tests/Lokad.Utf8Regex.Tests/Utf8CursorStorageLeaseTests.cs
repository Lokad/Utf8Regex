using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CursorStorageLeaseTests
{
    private static Utf8SliceProgram Program(Utf8Regex regex) =>
        (Utf8SliceProgram)typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    private static void CollectLeases()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public void CollectedReleaseTransportPreservesTheFullUnsignedRange()
    {
        var ledger = new Utf8StorageLedger(ulong.MaxValue);
        Assert.True(ledger.TryReserve(ulong.MaxValue));
        var leases = AbandonUnsignedLoans(ref ledger);
        CollectLeases();
        Assert.All(leases, lease => Assert.False(lease.IsAlive));
        Assert.Equal(7UL, ledger.ReservedBytes);
        Assert.True(ledger.TryReserve(ulong.MaxValue - 7));
        Assert.False(ledger.TryReserve(1));
        ledger.Release(ulong.MaxValue);
        Assert.Equal(0UL, ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] AbandonUnsignedLoans(ref Utf8StorageLedger ledger) =>
        [new(ledger.CreateLease(ulong.MaxValue - 13)!), new(ledger.CreateLease(6)!)];

    [Fact]
    public void SetupAbortReturnsItsLoanImmediatelyAndDoesNotReleaseAgainOnCollection()
    {
        var ledger = new Utf8StorageLedger(31);
        Assert.True(ledger.TryReserve(31));
        var lease = AbortLoan(ref ledger);
        Assert.Equal(7UL, ledger.ReservedBytes);
        CollectLeases();
        Assert.False(lease.IsAlive);
        Assert.Equal(7UL, ledger.ReservedBytes);
        Assert.Null(ledger.CreateLease(0));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AbortLoan(ref Utf8StorageLedger ledger)
    {
        var lease = ledger.CreateLease(24)!;
        var reference = new WeakReference(lease);
        lease.AbortBeforePublication();
        return reference;
    }

    [Fact]
    public void ControlledInvocationCopiesReserveAgainstOneAggregate()
    {
        var original = new Utf8SliceInvocation(new(1000, 0, 64), Regex.InfiniteMatchTimeout, default);
        var copy = original;
        Assert.True(original.Ledger.TryReserve(7));
        Assert.Equal(7UL, copy.Ledger.ReservedBytes);
        Assert.False(copy.Ledger.TryReserve(58));
        Assert.True(copy.Ledger.TryReserve(57));
        Assert.Equal(64UL, original.Ledger.ReservedBytes);
        copy.Ledger.Release(57);
        Assert.Equal(7UL, original.Ledger.ReservedBytes);
        original.Ledger.Release(7);
        Assert.Equal(0UL, copy.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OneCopyEofCannotUnchargeAnotherCopysSnapshot(bool compiled, bool rtl)
    {
        const string text = "é😀 abab 中abab";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex("(ab)", options);
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        ExerciseCopies(regex, input, text, ref invocation);
        CollectLeases();
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExerciseCopies(Utf8Regex regex, byte[] input, string text, ref Utf8SliceInvocation invocation)
    {
        var validation = Utf8Validation.Validate(input, ref invocation);
        var original = Utf8OperationMatchCursor.CreateMapped(Program(regex), input, validation, ref invocation, false, out _);
        var held = original.DebugInvocationReservedBytes;
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        FinishCopy(ref original);
        CollectLeases();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        Assert.Equal(held, original.DebugInvocationReservedBytes);
        // Each copy keeps the original borrowed input and coordinate map after
        // another copy reaches EOF.
        foreach (Match expected in new Regex(regex.Pattern, regex.Options).Matches(text))
        {
            Assert.True(original.MoveNext());
            Assert.Equal((expected.Index, expected.Length),
                (original.CurrentValueMatch.IndexInUtf16, original.CurrentValueMatch.LengthInUtf16));
        }
        Assert.False(original.MoveNext());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FinishCopy(ref Utf8OperationMatchCursor original)
    {
        var copy = original;
        while (copy.MoveNext()) { }
    }

    [Fact]
    public void AbandoningTheLastCursorReclaimsOnlyItsPayloadLoans()
    {
        var regex = new Utf8Regex("(ab)", RegexOptions.CultureInvariant);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var buffer = Abandon(regex, Encoding.UTF8.GetBytes("é😀 abab"), ref invocation);
        CollectLeases();
        Assert.False(buffer.IsAlive);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Abandon(Utf8Regex regex, byte[] input, ref Utf8SliceInvocation invocation)
    {
        var validation = Utf8Validation.Validate(input, ref invocation);
        var cursor = Utf8OperationMatchCursor.CreateMapped(Program(regex), input, validation, ref invocation, false, out var map);
        Assert.True(cursor.MoveNext());
        var offsets = typeof(Utf8BoundaryMap).GetField("_byteOffsets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map)!;
        return new WeakReference(offsets);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AReturnedSplitViewKeepsItsMapLoanAfterCursorAbandonment(int count)
    {
        var input = Encoding.UTF8.GetBytes("é😀 abab tail");
        var regex = new Utf8Regex("(ab)", RegexOptions.CultureInvariant);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var validation = Utf8Validation.Validate(input);
        var held = 7UL + (ulong)((validation.Utf16Length + 1) + (validation.Utf16Length + 32) / 32) * 4;
        ReadViewAfterCollection(regex, input, count, held, source, ref invocation);
        CollectLeases();
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReadViewAfterCollection(Utf8Regex regex, byte[] input, int count, ulong held,
        CancellationTokenSource source, ref Utf8SliceInvocation invocation)
    {
        var view = CreateView(regex, input, count, ref invocation);
        var expected = view.GetValueString();
        CollectLeases();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        source.Cancel();
        Assert.Equal(expected, view.GetValueString());
        Assert.True(view.IsByteAligned);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), view.GetValueBytes().ToArray());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8ValueSplit CreateView(Utf8Regex regex, byte[] input, int count, ref Utf8SliceInvocation invocation)
    {
        var cursor = Utf8OwnedSplitPreparation.Create(Program(regex), input, count, ref invocation);
        Assert.True(cursor.MoveNext());
        return cursor.Current;
    }

    [Fact]
    public void ACopyRetainsTheScalarCoordinateLoanAfterTheOtherCopyEnds()
    {
        var regex = new Utf8Regex("(.)", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("😀");
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        var validation = Utf8Validation.Validate(input, ref invocation);
        var cursor = Utf8OperationMatchCursor.CreateMapped(Program(regex), input, validation, ref invocation, false, out _);
        FinishCopy(ref cursor);
        Assert.True(cursor.MoveNext());
        Assert.Equal((0, 2), (cursor.CurrentValueMatch.IndexInUtf16, cursor.CurrentValueMatch.LengthInUtf16));
        Assert.True(cursor.CurrentValueMatch.IsByteAligned);
        Assert.Equal((0, 4), (cursor.CurrentValueMatch.IndexInBytes, cursor.CurrentValueMatch.LengthInBytes));
        Assert.False(cursor.MoveNext());
    }
}
