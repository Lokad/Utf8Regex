using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CursorInvocationReservationTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_nonBacktrackingState")]
    private static extern ref Utf8NonBacktrackingSearchState? Table(ref Utf8OperationMatchCursor cursor);

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static Utf8SliceProgram Program(Utf8Regex regex) => regex.Inspection.AvailableOwnedSliceProgram!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinearCopiesKeepTheirTablesAndCreditsWithoutRootingAbandonedPayloads(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("(?<x>😀|é)", options: options);
        var invocation = new Utf8SliceInvocation(new(0, 4096), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var arrays = ExerciseLinearCopies(Program(regex), ref invocation, out var held);
        Collect();
        Assert.All(arrays, array => Assert.False(array.IsAlive));
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        Assert.True(held > 7);
        // Accounting never roots abandoned arrays or refunds published credits.
        Assert.True(invocation.Ledger.TryReserve(4096 - held));
        var before = invocation.Ledger.ReservedBytes;
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        try
        {
            invocation.ShareStorageForCursor();
            _ = new Utf8OperationMatchCursor(Program(regex), "😀é😀"u8, invocation);
            Assert.Fail("The full same-invocation allowance must deny another map.");
        }
        catch (InvalidOperationException) { Assert.Equal(before, invocation.Ledger.ReservedBytes); }
        var fresh = new Utf8SliceInvocation(new(0, 4096), Regex.InfiniteMatchTimeout, default);
        Assert.True(fresh.Ledger.TryReserve(7));
        fresh.ShareStorageForCursor();
        var cursor = new Utf8OperationMatchCursor(Program(regex), "😀é😀"u8, fresh);
        Assert.True(cursor.MoveNext());
        Assert.Equal((0, 4), (cursor.CurrentValueMatch.StartOffsetInBytes, (cursor.CurrentValueMatch.EndOffsetInBytes - cursor.CurrentValueMatch.StartOffsetInBytes)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ExerciseLinearCopies(Utf8SliceProgram program, ref Utf8SliceInvocation invocation, out ulong held)
    {
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        invocation.ShareStorageForCursor();
        var cursor = new Utf8OperationMatchCursor(program, "😀é😀"u8, invocation);
        var table = Table(ref cursor)!;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        WeakReference[] arrays = [new(typeof(Utf8NonBacktrackingSearchState).GetField("_boundaries", flags)!.GetValue(table)!),
            new(typeof(Utf8NonBacktrackingSearchState).GetField("_viability", flags)!.GetValue(table)!)];
        held = invocation.Ledger.ReservedBytes;
        var copy = cursor;
        while (copy.MoveNext()) { }
        Assert.False(copy.MoveNext());
        Collect();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        Assert.True(cursor.MoveNext());
        var retained = cursor.CurrentValueMatch;
        Assert.True(cursor.MoveNext());
        Assert.True(cursor.MoveNext());
        Assert.False(cursor.MoveNext());
        Assert.False(cursor.MoveNext());
        Assert.Equal((0, 4), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        return arrays;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinearSplitSetupInterruptionReleasesUnpublishedTable(bool deadline)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("(?<x>😀|é)", options: RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        var invocation = new Utf8SliceInvocation(new(0, 4096),
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        var cursor = Utf8OperationMatchCursor.CreateSplitCursor(Program(regex), "😀é😀"u8, invocation, false);
        Assert.True(invocation.Ledger.ReservedBytes > 7);
        if (deadline) ticks = 250;
        else source.Cancel();
        try { cursor.CheckOwnedSliceCursorBoundary(); Assert.Fail("The original interruption must stop setup."); }
        catch (Utf8ExecutionDeadlineExpiredException) { Assert.True(deadline); }
        catch (OperationCanceledException error) { Assert.False(deadline); Assert.Equal(source.Token, error.CancellationToken); }
        finally { cursor.AbortOwnedSplitSetup(); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        cursor.AbortOwnedSplitSetup();
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ShippedCursorAccountingHasNoFinalizerOrCollectionReleaseState()
    {
        Assert.Empty(typeof(Utf8StorageLedger).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Assert.Null(typeof(Utf8NonBacktrackingSearchState).GetMethod("Finalize", flags));
        // Borrowing caller admission authority does not restore payload roots,
        // collection-triggered refunds or a retained-object accounting graph.
        Assert.DoesNotContain(typeof(Utf8StorageLedger).GetFields(flags), field =>
            !field.FieldType.IsValueType && field.FieldType != typeof(IUtf8MemoryAllowance));
    }
}
