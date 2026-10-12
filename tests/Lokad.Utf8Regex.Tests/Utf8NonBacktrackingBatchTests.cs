using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonBacktrackingBatchTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PersistentScalarTableFundsCompleteOffsetAndBitArrays(bool empty)
    {
        var program = Program("(.)?");
        Assert.InRange(program.Instructions.Length, 1, 64);
        var input = empty ? [] : "😀é"u8.ToArray();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out var table));
        Assert.NotNull(table);
        Assert.Equal(13UL + (empty ? 96UL : 112UL), invocation.Ledger.ReservedBytes);
        Assert.Equal(0, table.ResolveByteOffset(0));
        if (!empty)
        {
            Assert.Equal(1, table.ResolveByteOffset(4));
            Assert.Equal(2, table.ResolveByteOffset(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.ResolveByteOffset(1));
        }
        table.Release(ref invocation);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    private static Utf8SliceProgram Program(string pattern) =>
        new Utf8Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)
            .Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Owned graph missing.");

    [Theory]
    [InlineData("a*b|", false)] [InlineData("a*b|", true)]
    [InlineData("(a*)*b|", false)] [InlineData("(a*)*b|", true)]
    [InlineData("a*?b|", false)] [InlineData("a*?b|", true)]
    [InlineData("(?:a|aa)*?b|", false)] [InlineData("(?:a|aa)*?b|", true)]
    [InlineData("a*b", false)] [InlineData("a*b", true)]
    public void CountWorkIsLinearAcrossTheWholeBatch(string pattern, bool hit)
    {
        var program = Program(pattern);
        ulong previous = 0;
        foreach (var size in new[] { 64, 128, 256, 512, 1024, 2048 })
        {
            var input = Encoding.UTF8.GetBytes(new string('a', size) + (hit ? "b" : ""));
            var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0,
                new int[program.GroupCount], new int[program.GroupCount], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            Assert.Equal(hit ? (pattern.EndsWith('|') ? 2 : 1) : (pattern.EndsWith('|') ? size + 1 : 0), result.Count);
            if (previous != 0) Assert.InRange(invocation.Budget.Steps, previous, previous * 2 + 64);
            previous = invocation.Budget.Steps;
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarBatchesPreserveOffsetsSplitAndBothReplacementForms(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("(?<x>😀)*z|", options);
        var input = "😀😀"u8.ToArray();
        Assert.Equal(3, regex.Count(input));
        var ranges = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(input)) ranges.Add((match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(new[] { (0, 0), (4, 0), (8, 0) }, ranges);
        var fromOffset = new List<int>();
        foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, 2)) fromOffset.Add(match.IndexInBytes);
        Assert.Equal(new[] { 4, 8 }, fromOffset);
        Assert.Throws<ArgumentOutOfRangeException>(() => { var cursor = regex.EnumerateMatchesFromUtf16Offset(input, 1); cursor.MoveNext(); });
        Assert.Equal("Q😀Q😀Q", regex.ReplaceToString(input, "Q"));
        Assert.Equal("Q😀Q😀Q", regex.ReplaceToString(input, 0,
            static (in Utf8MatchContext match, ref int state) => "Q"));
        var pieces = new List<string>();
        foreach (var piece in regex.EnumerateSplits(input)) pieces.Add(piece.GetValueString());
        Assert.Equal(new[] { "", "😀", "😀", "" }, pieces);
        var shared = new Utf8Regex("(?<x>a(?<x>b)c)", options);
        Assert.Equal("<abc>", shared.ReplaceToString("abc"u8, "<${x}>"));
        Assert.Equal("<abc>", shared.ReplaceToString("abc"u8, 0,
            static (in Utf8MatchContext match, ref int state) => "<" + match.GetGroup("x").GetValueString() + ">"));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(shared.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void EveryCountWorkAndStorageCutoffRestoresBorrowedStorage()
    {
        var program = Program("(?<x>.)*z|");
        var input = "😀a"u8.ToArray();
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(3, Utf8SliceRunner.CountFromBytes(program, input, 0, new int[1], new int[1], ref baseline).Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong limit = 1; limit <= baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[1], new int[1], ref invocation);
            Assert.Equal(limit == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, result.Breach);
            Assert.InRange(result.Count, 0, 3);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        for (ulong cap = 13; cap <= baseline.Budget.PeakWorkingBytes; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[1], new int[1], ref invocation);
            Assert.Equal(cap == baseline.Budget.PeakWorkingBytes ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void PreparationCancellationReleasesAnUnpublishedTable()
    {
        var program = Program("(a*)*b|");
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // The first two passes have 128 steps; cancel inside viability filling.
        invocation.CheckpointHook = steps => { if (steps >= 192) source.Cancel(); };
        var error = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.CountFromBytes(program, Encoding.UTF8.GetBytes(new string('a', 64)), 0,
                new int[1], new int[1], ref invocation));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void EveryHistoryReplayWorkCutoffReleasesScratchAndUnpublishedCaptures()
    {
        var program = Program("(?<x>a(?<x>b)c)");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, "abc"u8, ref baseline, out var measured));
        Assert.NotNull(measured);
        var preparationSteps = baseline.Budget.Steps;
        var boundary = 0;
        var found = measured.FindFirst(program, ref boundary, new int[1], new int[1], true, ref baseline);
        Assert.True(found.Found);
        var replaySteps = baseline.Budget.Steps - preparationSteps;
        Utf8SliceRunner.ReleaseHistory(found.CaptureStarts, found.CaptureLengths, ref baseline);
        measured.Release(ref baseline);
        for (ulong limit = 1; limit <= replaySteps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(preparationSteps + limit, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, "abc"u8, ref invocation, out var table));
            Assert.NotNull(table);
            var held = invocation.Ledger.ReservedBytes;
            boundary = 0;
            var starts = new[] { 42 }; var ends = new[] { 43 };
            var result = table.FindFirst(program, ref boundary, starts, ends, true, ref invocation);
            Assert.Equal(limit == replaySteps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, result.Breach);
            Utf8SliceRunner.ReleaseHistory(result.CaptureStarts, result.CaptureLengths, ref invocation);
            Assert.Equal(held, invocation.Ledger.ReservedBytes);
            if (!result.Found) { Assert.Equal(-1, starts[0]); Assert.Equal(-1, ends[0]); }
            table.Release(ref invocation);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        for (ulong cap = 13; cap <= baseline.Budget.PeakWorkingBytes; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var breach = Utf8NonBacktrackingSearchState.TryCreate(program, "abc"u8, ref invocation, out var table);
            if (table is not null)
            {
                boundary = 0;
                var result = table.FindFirst(program, ref boundary, new int[1], new int[1], true, ref invocation);
                breach = result.Breach;
                Utf8SliceRunner.ReleaseHistory(result.CaptureStarts, result.CaptureLengths, ref invocation);
                table.Release(ref invocation);
            }
            Assert.Equal(cap == baseline.Budget.PeakWorkingBytes ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void FinalHistoryCancellationReleasesTheTableAndUnpublishedCaptures()
    {
        var program = Program("(a)");
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, "a"u8, ref baseline, out var measured));
        Assert.NotNull(measured);
        var boundary = 0;
        var found = measured.FindFirst(program, ref boundary, new int[1], new int[1], true, ref baseline);
        Assert.True(found.Found);
        Utf8SliceRunner.ReleaseHistory(found.CaptureStarts, found.CaptureLengths, ref baseline);
        measured.Release(ref baseline);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var padding = (64 - baseline.Budget.Steps % 64) % 64;
        for (ulong step = 0; step < padding; step++) _ = invocation.TryChargeStep();
        var finalStep = baseline.Budget.Steps + padding;
        invocation.CheckpointHook = steps => { if (steps == finalStep) source.Cancel(); };
        var starts = new[] { 42 }; var ends = new[] { 43 };
        var error = Assert.Throws<OperationCanceledException>(() =>
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, "a"u8, ref invocation, out var table));
            Assert.NotNull(table);
            try { boundary = 0; _ = table.FindFirst(program, ref boundary, starts, ends, true, ref invocation); }
            finally { table.Release(ref invocation); }
        });
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(-1, starts[0]); Assert.Equal(-1, ends[0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicEnumerationWorkGrowsLinearlyAndCopiesKeepTheirPosition(bool compiled)
    {
        var regex = new Utf8Regex("a*b|", RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0));
        ulong previous = 0;
        foreach (var size in new[] { 64, 128, 256, 512 })
        {
            var cursor = regex.EnumerateMatches(Encoding.UTF8.GetBytes(new string('a', size)));
            var copy = cursor.GetEnumerator();
            var count = 0;
            while (cursor.MoveNext()) count++;
            Assert.Equal(size + 1, count);
            var steps = cursor.DebugInvocationSteps;
            if (previous != 0) Assert.InRange(steps, previous, previous * 2 + 64);
            previous = steps;
            Assert.True(copy.MoveNext());
            Assert.Equal(0, copy.Current.IndexInBytes);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void AbortedSplitSetupImmediatelyReturnsTheTableLoan()
    {
        var program = Program("(a*)*b|");
        var input = "aaaa"u8.ToArray();
        var measured = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        var cursor = Utf8OwnedSplitPreparation.Create(program, input, 0, ref measured);
        Assert.True(measured.Ledger.ReservedBytes > 0);
        var invocation = new Utf8SliceInvocation(new(measured.Budget.Steps - 1, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.Throws<InvalidOperationException>(() => { var failed = Utf8OwnedSplitPreparation.Create(program, input, 0, ref invocation); });
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.True(cursor.MoveNext());
    }

    [Fact]
    public void CaptureOpeningsAreReusedAcrossFiniteEmissionCopies()
    {
        var small = Program("(?<x>a){2}");
        var large = Program("(?<x>a){1024}");
        Assert.Equal(1, small.NonBacktrackingCaptureOpeningCount);
        Assert.Equal(small.NonBacktrackingCaptureOpeningCount, large.NonBacktrackingCaptureOpeningCount);
        var nested = Program("(?<x>a(?<x>b)c){16}");
        Assert.Equal(2, nested.NonBacktrackingCaptureOpeningCount);
        Assert.Equal("abc", new Utf8Regex("(?<x>a(?<x>b)c){16}", RegexOptions.NonBacktracking)
            .MatchDetailed(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abc", 16))))
            .GetGroup("x").GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CopiedAndAbandonedCursorsShareOneTableLoan(bool compiled)
    {
        var regex = new Utf8Regex("(?<x>.)*z|", RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Owned graph missing.");
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var held = ExerciseCopiesAndAbandon(program, "a😀b"u8.ToArray(), ref invocation);
        CollectLeases();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ExerciseCopiesAndAbandon(Utf8SliceProgram program, byte[] input, ref Utf8SliceInvocation invocation)
    {
        var validation = Utf8Validation.Validate(input, ref invocation);
        var original = Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref invocation, false, out _);
        var held = invocation.Ledger.ReservedBytes;
        FinishCopy(ref original);
        CollectLeases();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        Assert.True(original.MoveNext());
        Assert.Equal(0, original.CurrentValueMatch.IndexInBytes);
        var firstSteps = invocation.Budget.Steps;
        FinishCopy(ref original);
        Assert.True(invocation.Budget.Steps > firstSteps);
        CollectLeases();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        Assert.True(original.MoveNext());
        Assert.Equal(1, original.CurrentValueMatch.IndexInBytes);
        // Abandon the original; only the numeric invocation charge persists.
        return held;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FinishCopy(ref Utf8OperationMatchCursor original)
    {
        var copy = original;
        while (copy.MoveNext()) { }
    }

    private static void CollectLeases()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }
}
