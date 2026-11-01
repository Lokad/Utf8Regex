using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonBacktrackingRowAdmissionTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void RejectedLiteralRowsKeepRealAdmissionAndFinalPublicationChecks(bool lastRow, bool expire)
    {
        var program = new Utf8Regex("z", RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(2, program.Instructions.Length);
        var input = Encoding.UTF8.GetBytes(new string('x', lastRow ? 1 : 128));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // Size/offset admission plus EOF row align the actual false-literal
        // row with step64. It has no separately charged predicate anymore.
        for (var step = 0; step < (lastRow ? 59 : 57); step++) _ = invocation.TryChargeStep();
        invocation.CheckpointHook = step => { if (step == 64) { if (expire) ticks = 100; else live.Cancel(); } };
        Utf8NonBacktrackingSearchState? table = null;
        if (expire)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out table));
        else
            Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
                Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out table)).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Null(table);
        Assert.Equal(13UL + Utf8ConstructionBudget.ArrayBytes<int>(input.Length + 1)
            + Utf8ConstructionBudget.ArrayBytes<ulong>(input.Length + 1), invocation.Budget.PeakWorkingBytes);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(16)]
    [InlineData(17)] [InlineData(31)] [InlineData(32)] [InlineData(33)]
    [InlineData(63)] [InlineData(64)] [InlineData(65)] [InlineData(129)]
    public void EachRowHasExactWorkAndEveryRefusalRefundsTheTable(int states)
    {
        var program = new Utf8Regex(new string('a', states - 1), RegexOptions.NonBacktracking)
            .Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(states, program.Instructions.Length);
        var boundary = states <= 64 ? 64 / states : 1;
        foreach (var scalars in new[] { boundary - 1, boundary, boundary + 1 })
        foreach (var unit in new[] { "x", "😀" })
        {
            var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(unit, scalars)));
            // Packed rows reject the impossible last literal under their cell
            // admission; wide rows retain their separate failed-atom charges.
            var rowWork = (scalars + 1) * ((states + 63) / 64);
            var steps = (ulong)((input.Length + 63) / 64 + 1 + (scalars + 63) / 64 + rowWork
                + (states <= 64 ? 0 : scalars));
            for (ulong limit = 1; limit <= steps; limit++)
            {
                var invocation = new Utf8SliceInvocation(new(limit, 0, 1_000_000), Timeout.InfiniteTimeSpan, default);
                Assert.True(invocation.Ledger.TryReserve(13));
                var breach = Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out var table);
                if (limit < steps)
                {
                    Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach);
                    Assert.Null(table);
                    Assert.Equal(limit + 1, invocation.Budget.Steps);
                }
                else
                {
                    Assert.Equal(Utf8ExecutionLimitKind.None, breach);
                    Assert.NotNull(table);
                    Assert.Equal(steps, invocation.Budget.Steps);
                    Assert.Equal(states == 1, table.CanMatch(0, 0));
                    Assert.Equal(scalars, table.ResolveByteOffset(input.Length));
                    table.Release(ref invocation);
                }
                Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ARealPackedRowAdmissionKeepsInterruptionAndBorrowedStorage(bool expire)
    {
        var program = new Utf8Regex("", RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        Assert.Single(program.Instructions);
        var input = Encoding.UTF8.GetBytes(new string('x', 128));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // Size2 + allocation1 + offsets2, then first and second row admissions.
        // The second admission is step64; there are no atom charges here.
        for (var step = 0; step < 57; step++) _ = invocation.TryChargeStep();
        invocation.CheckpointHook = step => { if (step == 64) { if (expire) ticks = 100; else live.Cancel(); } };
        Utf8NonBacktrackingSearchState? table = null;
        if (expire)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out table));
        else
            Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
                Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out table)).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL + Utf8ConstructionBudget.ArrayBytes<int>(129)
            + Utf8ConstructionBudget.ArrayBytes<ulong>(129), invocation.Budget.PeakWorkingBytes);
        Assert.Null(table);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
