using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonBacktrackingRowAdmissionTests
{
    [Fact]
    public void OversizedViabilityTableReportsArrayCapacity()
    {
        using var regex = new Utf8Regex("a{16384}z|", RegexOptions.NonBacktracking);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = new byte[8 * 1024 * 1024];
        var wordsPerRow = (program.Instructions.Length + 63) / 64;
        Assert.True((input.Length + 1L) * wordsPerRow > Array.MaxLength);

        var error = Assert.Throws<InvalidOperationException>(() => regex.CreateMatchCursor(input).Dispose());
        Assert.Contains("viability table", error.Message, StringComparison.Ordinal);
        Assert.Contains("array length", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(129, true)]
    [InlineData(400_000, false)]
    public void OversizedThreadRegistersAreAvoidedWhenCaptureReplayIsEligible(int length, bool replay)
    {
        using var regex = new Utf8Regex(string.Concat(Enumerable.Repeat("(a)", 16_000)), RegexOptions.NonBacktracking);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var registers = 2L * program.GroupCount + program.NonBacktrackingCaptureOpeningCount;
        Assert.True(registers * program.Instructions.Length > Array.MaxLength);

        var input = Encoding.UTF8.GetBytes(new string('a', length));
        var captures = new Utf8CaptureRange[2];
        if (replay)
        {
            Assert.False(regex.Match(input, captures: captures).Success);
            Assert.All(captures, capture => Assert.False(capture.Success));
            return;
        }
        var error = Assert.Throws<InvalidOperationException>(() => regex.Match(input, captures: captures));
        Assert.Contains("capture registers", error.Message, StringComparison.Ordinal);
        Assert.Contains("array length", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void RejectedLiteralRowsKeepRealAdmissionAndFinalPublicationChecks(bool lastRow, bool expire)
    {
        var program = new Utf8Regex("z", options: RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(2, program.Instructions.Length);
        var input = Encoding.UTF8.GetBytes(new string('x', lastRow ? 1 : 128));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // Size/allocation admission plus EOF row align the actual false-literal
        // row with step64. It has no separately charged predicate anymore.
        for (var step = 0; step < (lastRow ? 60 : 59); step++) _ = invocation.TryChargeStep();
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
        var pattern = states <= 64 ? new string('a', states - 1) : "." + new string('a', states - 2);
        using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(states, program.Instructions.Length);
        var boundary = states <= 64 ? 64 / states : 1;
        foreach (var scalars in new[] { boundary - 1, boundary, boundary + 1 })
        foreach (var unit in new[] { "x", "😀" })
        {
            var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(unit, scalars)));
            // Only the last literal can be feasible, and it does not match.
            // Up to three wide mask words fit in one charged batch per row.
            var steps = (ulong)((input.Length + 63) / 64 + 1 + scalars + 1);
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
                    Assert.Equal(scalars, table.ResolveByteOffset(input, input.Length));
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
        var program = new Utf8Regex("", options: RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        Assert.Single(program.Instructions);
        var input = Encoding.UTF8.GetBytes(new string('x', 128));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // Two size steps and one allocation step precede the first and second rows.
        // The second admission is step64; there are no atom charges here.
        for (var step = 0; step < 59; step++) _ = invocation.TryChargeStep();
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
