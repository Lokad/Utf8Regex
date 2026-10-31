using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonBacktrackingPreparationDeadlineTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LongEmptyRowKeepsBoundedInterruptionAndRefundsTheUnpublishedTable(bool expire)
    {
        var regex = new Utf8Regex(string.Concat(Enumerable.Repeat("a?", 2048)), RegexOptions.NonBacktracking);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.True(program.Instructions.Length > 4096);
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var interrupted = false;
        invocation.CheckpointHook = _ =>
        {
            if (interrupted) return;
            interrupted = true;
            if (expire) ticks = 100; else live.Cancel();
        };
        Utf8NonBacktrackingSearchState? state = null;
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8NonBacktrackingSearchState.TryCreate(program, [], ref invocation, out state));
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
            Utf8NonBacktrackingSearchState.TryCreate(program, [], ref invocation, out state)).CancellationToken);
        Assert.True(interrupted); Assert.Null(state);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void CancellationOnTheLastQuantumCannotPublishTheTable()
    {
        var regex = new Utf8Regex(new string('a', 4031), RegexOptions.NonBacktracking);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(4032, program.Instructions.Length);
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        invocation.CheckpointHook = _ => live.Cancel();
        Utf8NonBacktrackingSearchState? state = null;
        Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
            Utf8NonBacktrackingSearchState.TryCreate(program, [], ref invocation, out state)).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps); Assert.Null(state);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void DeadlineExpiringAfterTheLastQuantumCannotPublishTheTable()
    {
        var program = new Utf8Regex("", RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        Assert.Single(program.Instructions);
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads >= 4 ? 100 : 0);
        var invocation = new Utf8SliceInvocation(new(1000, 0, 1000), TimeSpan.FromMilliseconds(50), default);
        Assert.True(invocation.Ledger.TryReserve(13));
        // Start, allocation and the one quantum see zero; publication sees expiry.
        Utf8NonBacktrackingSearchState? state = null;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8NonBacktrackingSearchState.TryCreate(program, [], ref invocation, out state));
        Assert.Equal(2UL, invocation.Budget.Steps); Assert.Null(state);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(63, 2)] [InlineData(64, 2)] [InlineData(65, 3)]
    [InlineData(128, 3)] [InlineData(129, 4)] [InlineData(4097, 66)]
    public void EmptyRowWorkBoundsEachQuantumAndRefusesBeforeItRuns(int states, ulong steps)
    {
        var program = new Utf8Regex(new string('a', states - 1), RegexOptions.NonBacktracking)
            .Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(states, program.Instructions.Length);
        for (ulong limit = 1; limit <= steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 0, 1_000_000), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var breach = Utf8NonBacktrackingSearchState.TryCreate(program, [], ref invocation, out var table);
            if (limit < steps)
            {
                Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, breach); Assert.Null(table);
                Assert.Equal(limit + 1, invocation.Budget.Steps);
            }
            else
            {
                Assert.Equal(Utf8ExecutionLimitKind.None, breach); Assert.NotNull(table);
                Assert.Equal(steps, invocation.Budget.Steps);
                table.Release(ref invocation);
            }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }
}
