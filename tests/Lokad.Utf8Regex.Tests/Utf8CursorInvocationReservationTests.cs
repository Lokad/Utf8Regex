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
        var regex = new Utf8Regex("(?<x>😀|é)", options);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 4096), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var arrays = ExerciseLinearCopies(Program(regex), ref invocation, out var held);
        Collect();
        Assert.All(arrays, array => Assert.False(array.IsAlive));
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        Assert.True(held > 7);
        // Accounting never roots abandoned arrays or refunds published credits.
        Assert.True(invocation.Ledger.TryReserve(4096 - held));
        var before = invocation.Ledger.ReservedBytes;
        var validation = Utf8Validation.Validate("😀é😀"u8, ref invocation);
        try
        {
            _ = Utf8OperationMatchCursor.CreateMapped(Program(regex), "😀é😀"u8, validation, ref invocation, false, out _);
            Assert.Fail("The full same-invocation allowance must deny another map.");
        }
        catch (InvalidOperationException) { Assert.Equal(before, invocation.Ledger.ReservedBytes); }
        var fresh = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 4096), Regex.InfiniteMatchTimeout, default);
        Assert.True(fresh.Ledger.TryReserve(7));
        var cursor = Utf8OperationMatchCursor.CreateMapped(Program(regex), "😀é😀"u8, validation, ref fresh, false, out _);
        Assert.True(cursor.MoveNext());
        Assert.Equal((0, 4), (cursor.CurrentValueMatch.IndexInBytes, cursor.CurrentValueMatch.LengthInBytes));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ExerciseLinearCopies(Utf8SliceProgram program, ref Utf8SliceInvocation invocation, out ulong held)
    {
        var validation = Utf8Validation.Validate("😀é😀"u8, ref invocation);
        var cursor = Utf8OperationMatchCursor.CreateMapped(program, "😀é😀"u8, validation, ref invocation, false, out var map);
        var table = Table(ref cursor)!;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        WeakReference[] arrays = [new(typeof(Utf8BoundaryMap).GetField("_byteOffsets", flags)!.GetValue(map)!),
            new(typeof(Utf8NonBacktrackingSearchState).GetField("_boundaries", flags)!.GetValue(table)!),
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
        Assert.Equal((0, 4, 0, 2), (retained.IndexInBytes, retained.LengthInBytes, retained.IndexInUtf16, retained.LengthInUtf16));
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        return arrays;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinearSplitSetupInterruptionReleasesUnpublishedMapAndTable(bool deadline)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex("(?<x>😀|é)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 4096),
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var validation = Utf8Validation.Validate("😀é😀"u8, ref invocation);
        var map = Utf8BoundaryMap.Create("😀é😀"u8, validation, ref invocation, out var mapBytes);
        var cursor = Utf8OperationMatchCursor.CreateSplitCursor(Program(regex), "😀é😀"u8, map, invocation, false);
        Assert.True(invocation.Ledger.ReservedBytes > 7 + mapBytes);
        if (deadline) ticks = 250;
        else source.Cancel();
        try { cursor.CheckOwnedSliceCursorBoundary(); Assert.Fail("The original interruption must stop setup."); }
        catch (Utf8ExecutionDeadlineExpiredException) { Assert.True(deadline); }
        catch (OperationCanceledException error) { Assert.False(deadline); Assert.Equal(source.Token, error.CancellationToken); }
        finally { cursor.AbortOwnedSplitSetup(ref mapBytes); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        cursor.AbortOwnedSplitSetup(ref mapBytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void CursorSetupWorkFailureReturnsOnlyUnpublishedPayloads()
    {
        var regex = new Utf8Regex("(?<x>😀|é)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        var input = "😀é😀"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        // Cover every cutoff through map and table preparation. Denied setup
        // restores the parent's seven bytes but never refunds spent work.
        var success = false;
        for (ulong cap = 1; cap <= 256 && !success; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 4096), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            try
            {
                _ = Utf8OperationMatchCursor.CreateMapped(Program(regex), input, validation, ref invocation, false, out _);
                Assert.True(invocation.Ledger.ReservedBytes > 7);
                success = true;
            }
            catch (InvalidOperationException)
            {
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, invocation.Budget.LimitBreach);
                Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
                Assert.True(invocation.Budget.Steps >= cap);
            }
        }
        Assert.True(success);
    }

    [Fact]
    public void ShippedCursorAccountingHasNoFinalizerOrCollectionReleaseState()
    {
        var assembly = typeof(Utf8Regex).Assembly;
        Assert.Null(assembly.GetType("Lokad.Utf8Regex.Internal.Execution.Utf8StorageLease"));
        Assert.Empty(typeof(Utf8StorageLedger).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Assert.Null(typeof(Utf8NonBacktrackingSearchState).GetMethod("Finalize", flags));
        Assert.Null(typeof(Utf8BoundaryMap).GetField("_storageLease", flags));
        Assert.DoesNotContain(typeof(Utf8StorageLedger).GetFields(flags), field => !field.FieldType.IsValueType);
    }
}
