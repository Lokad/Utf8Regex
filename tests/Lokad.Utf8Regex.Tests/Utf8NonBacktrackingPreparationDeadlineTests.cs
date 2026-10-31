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
        Assert.InRange(invocation.Budget.Steps, 64UL, 2048UL);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterruptionOnTheLastCellCannotPublishTheTable(bool expire)
    {
        var regex = new Utf8Regex(new string('a', 62), RegexOptions.NonBacktracking);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(63, program.Instructions.Length);
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        invocation.CheckpointHook = _ => { if (expire) ticks = 100; else live.Cancel(); };
        Utf8NonBacktrackingSearchState? state = null;
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8NonBacktrackingSearchState.TryCreate(program, [], ref invocation, out state));
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
            Utf8NonBacktrackingSearchState.TryCreate(program, [], ref invocation, out state)).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps); Assert.Null(state);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }
}
