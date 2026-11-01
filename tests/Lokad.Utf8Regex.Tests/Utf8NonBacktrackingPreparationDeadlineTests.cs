using System.Text.RegularExpressions;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonBacktrackingPreparationDeadlineTests
{
    [Theory]
    [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void PublicEntryPointsRejectMalformedTailsBeforeEarlyMatches(int prefix)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', prefix)).Concat(new byte[] { 0xc3 }).ToArray();
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex("(?<x>a+)|$", RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0));
            Assert.Throws<ArgumentException>(() => regex.Count(input));
            Assert.Throws<ArgumentException>(() => regex.IsMatch(input));
            Assert.Throws<ArgumentException>(() => regex.MatchDetailed(input));
            Assert.Throws<ArgumentException>(() => regex.ReplaceToString(input, "X"));
            Assert.Throws<ArgumentException>(() => { var cursor = regex.EnumerateMatches(input); _ = cursor.MoveNext(); });
            using var live = new CancellationTokenSource();
            live.Cancel();
            Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, live.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData(0, "", 0)] [InlineData(64, "", 0)] [InlineData(65, "", 0)]
    [InlineData(63, "é", 1)] [InlineData(62, "😀", 1)] [InlineData(63, "😀", 1)]
    [InlineData(0, "😀", 65)] [InlineData(1, "é", 128)]
    public void SizeBlocksPreserveEveryScalarBoundary(int prefix, string unit, int repeats)
    {
        var text = new string('a', prefix) + string.Concat(Enumerable.Repeat(unit, repeats));
        var input = Encoding.UTF8.GetBytes(text);
        var program = new Utf8Regex("(.)?", RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None,
            Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out var state));
        Assert.NotNull(state);
        var boundary = 0;
        var index = 0;
        Assert.Equal(0, state.ResolveByteOffset(0));
        foreach (var scalar in text.EnumerateRunes())
        {
            for (var continuation = 1; continuation < scalar.Utf8SequenceLength; continuation++)
            {
                var offset = boundary + continuation;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.ResolveByteOffset(offset));
            }
            boundary += scalar.Utf8SequenceLength;
            Assert.Equal(++index, state.ResolveByteOffset(boundary));
        }
        Assert.Equal(input.Length, boundary);
        state.Release(ref invocation);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void SizeAndOffsetBlocksKeepBoundedInterruptionAndRefunds(bool offsets, bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', offsets ? 65 : 4097));
        var program = new Utf8Regex("a", RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // For 65 ASCII scalars: two size blocks, one allocation admission,
        // two offset blocks. Align the second offset admission with the hook.
        if (offsets) for (var step = 0; step < 59; step++) _ = invocation.TryChargeStep();
        invocation.CheckpointHook = step =>
        {
            if (step != 64) return;
            if (expire) ticks = 100; else live.Cancel();
        };
        Utf8NonBacktrackingSearchState? state = null;
        if (expire)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out state));
        else
            Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
                Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out state)).CancellationToken);
        Assert.Equal(64UL, invocation.Budget.Steps);
        Assert.Equal(offsets, invocation.Budget.PeakWorkingBytes > 0);
        Assert.Null(state);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void BothSearchRoutesAdmitCategoryBlocksBeforeTheyRun(bool batch, bool expireAtAdmission)
    {
        long ticks = 0;
        var armed = false;
        var visits = 0;
        ulong firstAtomStep = 0;
        Utf8SliceInvocation invocation = default;
        var table = Utf8SliceCategoryTable.ForScalarPredicate(_ =>
        {
            if (firstAtomStep == 0) firstAtomStep = invocation.Budget.Steps;
            visits++;
            if (armed && !expireAtAdmission && visits == 1) ticks = 100;
            return false;
        });
        var members = Enumerable.Range(0, 33).Select(_ => Utf8SliceNode.CategorySetMember.Create(table, false)).ToArray();
        var root = new Utf8SliceNode.CategorySetNode(new byte[16], false, [], members, false);
        var program = Utf8SliceLowerer.Lower(root, null, false, false, false, null, false, false,
            Utf8ScalarCaseBehavior.Invariant, true)!;
        Assert.Equal(Utf8SliceInstructionKind.CategorySet, program.Instructions[0].Kind);
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        void Run()
        {
            if (batch)
            {
                Assert.Equal(Utf8ExecutionLimitKind.None,
                    Utf8NonBacktrackingSearchState.TryCreate(program, "z"u8, ref invocation, out var state));
                Assert.NotNull(state);
                state.Release(ref invocation);
            }
            else
            {
                var result = Utf8SliceRunner.TryFindFirst(program, "z"u8, 0, [], [], false, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            }
        }
        invocation = new(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), default);
        Run();
        Assert.InRange(firstAtomStep, 1UL, 63UL);
        var padding = 64 - firstAtomStep;
        visits = 0;
        invocation = new(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), default);
        Assert.True(invocation.Ledger.TryReserve(13));
        if (expireAtAdmission)
        {
            for (ulong i = 0; i < padding; i++) _ = invocation.TryChargeStep();
            invocation.CheckpointHook = step => { if (step == 64) ticks = 100; };
        }
        armed = true;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(Run);
        Assert.Equal(expireAtAdmission ? 0 : 32, visits);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("1", false)] [InlineData("1", true)]
    [InlineData("[12]", false)] [InlineData("[12]", true)]
    [InlineData(@"\d", false)] [InlineData(@"\d", true)]
    [InlineData(".", false)] [InlineData(".", true)]
    public void LastSimpleAtomCannotPublishAfterInterruption(string pattern, bool cancel)
    {
        var program = new Utf8Regex(pattern, RegexOptions.NonBacktracking).Inspection.AvailableOwnedSliceProgram!;
        var measured = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), Timeout.InfiniteTimeSpan, default);
        Assert.Equal(Utf8ExecutionLimitKind.None,
            Utf8NonBacktrackingSearchState.TryCreate(program, "1"u8, ref measured, out var complete));
        Assert.NotNull(complete);
        var steps = measured.Budget.Steps;
        complete.Release(ref measured);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var live = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 1_000_000), TimeSpan.FromMilliseconds(50), live.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var padding = (64 - steps % 64) % 64;
        for (ulong i = 0; i < padding; i++) _ = invocation.TryChargeStep();
        invocation.CheckpointHook = _ => { if (cancel) live.Cancel(); else ticks = 100; };
        Utf8NonBacktrackingSearchState? state = null;
        if (cancel)
            Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() =>
                Utf8NonBacktrackingSearchState.TryCreate(program, "1"u8, ref invocation, out state)).CancellationToken);
        else
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8NonBacktrackingSearchState.TryCreate(program, "1"u8, ref invocation, out state));
        Assert.Equal(steps + padding, invocation.Budget.Steps);
        Assert.Null(state);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

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
