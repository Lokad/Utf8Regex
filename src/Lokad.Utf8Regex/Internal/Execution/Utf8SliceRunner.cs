using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Resume actions for explicit slice backtrack frames.</summary>
internal enum Utf8SliceResumeKind : byte
{
    Alternation = 0,
    RepeatUnwind = 1,
    RepeatContinue = 2,
    Assertion = 3,
    Atomic = 4,
    ScalarRepeatRewind = 5,
}

internal struct Utf8SliceFrame
{
    internal Utf8SliceResumeKind ResumeKind;
    internal int Target;
    internal int InputIndex;
    internal int TrailCount;
    internal int Aux;
    // RepeatUnwind stores the iteration count. ScalarRepeatRewind can store ASCII starters.
    internal int Count;
    internal int JournalCount;
}

internal struct Utf8SliceTrailEntry
{
    internal int Slot;
    internal int OldValue;
    internal int NewValue;
    // Keep the journal entry at sixteen bytes as capture markers are added.
    private byte _flags;
    internal bool IsStart { readonly get => (_flags & 1) != 0; set => _flags = (byte)(value ? _flags | 1 : _flags & ~1); }
    internal bool IsHead { readonly get => (_flags & 2) != 0; set => _flags = (byte)(value ? _flags | 2 : _flags & ~2); }
    internal bool IsRestore { readonly get => (_flags & 4) != 0; set => _flags = (byte)(value ? _flags | 4 : _flags & ~4); }
    internal bool IsOpening { readonly get => (_flags & 8) != 0; set => _flags = (byte)(value ? _flags | 8 : _flags & ~8); }
    internal bool IsClosing { readonly get => (_flags & 16) != 0; set => _flags = (byte)(value ? _flags | 16 : _flags & ~16); }
}

internal struct Utf8SliceBalanceEntry
{
    internal int Start;
    internal int End;
    internal int Prev;
}

internal struct Utf8SliceCountMutation
{
    internal int RepeatId;
    internal int OldCount;
    internal int OldPosition;
}

internal readonly struct Utf8SliceMatchOutcome
{
    // Expose field stores in hot repeat/capture return paths.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Utf8SliceMatchOutcome(bool isMatch, int endIndex, Utf8ExecutionLimitKind breach)
    {
        IsMatch = isMatch;
        EndIndex = endIndex;
        Breach = breach;
    }

    internal bool IsMatch { get; }
    internal int EndIndex { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
}

internal readonly struct Utf8SliceSlots
{
    private readonly int[]? _offsets;

    internal Utf8SliceSlots(int[] offsets) => _offsets = offsets;

    internal static Utf8SliceSlots Empty => new(Array.Empty<int>());
    internal bool IsInitialized => _offsets is not null;
    internal int Count => OffsetCount / 2;
    internal int OffsetCount => _offsets?.Length ?? 0;
    internal Span<int> Starts => _offsets.AsSpan(0, Count);
    internal Span<int> Ends => _offsets.AsSpan(Count);
}

internal readonly struct Utf8SliceSearchOutcome
{
    internal Utf8SliceSearchOutcome(
        bool found, int startByte, int endByte,
        Utf8ExecutionLimitKind breach)
    {
        Found = found;
        StartByte = startByte;
        EndByte = endByte;
        Breach = breach;
    }

    internal bool Found { get; }
    internal int StartByte { get; }
    internal int EndByte { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
}

internal readonly struct Utf8SliceCountOutcome
{
    internal Utf8SliceCountOutcome(int count, Utf8ExecutionLimitKind breach)
    {
        Count = count;
        Breach = breach;
    }

    internal int Count { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
}

/// <summary>Explicit-frame backtracking runner over immutable slice programs.
/// One budgeted step per visited instruction; depth is the live frame count.
/// Repeat count/position writes go through a mutation journal so backtracking
/// past one repeat restores sibling repeats to their push-time state; frames,
/// trail chunks, journal chunks, tables, counts, and capture slots are
/// ledger-admitted before acquisition and released on every return path,
/// including breach, cancellation, and disposal. Capture slot pairs hold
/// start/end input indexes, -1 when unset; the trail journal restores them.</summary>
internal static partial class Utf8SliceRunner
{
    internal const int FrameChunkCapacity = 16;
    internal const int TrailChunkCapacity = 8;
    internal const int JournalChunkCapacity = 32;
    internal const int BalanceChunkCapacity = 128;
    internal const int ChunkTableCapacity = 4;

    internal static int FrameReserveBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Utf8SliceFrame>(FrameChunkCapacity));
    internal static int TrailReserveBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Utf8SliceTrailEntry>(TrailChunkCapacity));
    internal static int JournalReserveBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Utf8SliceCountMutation>(JournalChunkCapacity));
    internal static int BalanceReserveBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Utf8SliceBalanceEntry>(BalanceChunkCapacity));

    internal static Utf8SliceSlots RentSlots(int groups, ref Utf8SliceInvocation invocation)
    {
        if (TryRentSlots(groups, ref invocation, out var slots) != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned slice capture slots exceeded the invocation storage budget.");
        return slots;
    }

    internal static Utf8ExecutionLimitKind TryRentSlots(int groups, ref Utf8SliceInvocation invocation, out Utf8SliceSlots slots)
    {
        if (groups == 0)
        {
            slots = Utf8SliceSlots.Empty;
            return Utf8ExecutionLimitKind.None;
        }
        return TryRentNonemptySlots(groups, ref invocation, out slots);
    }

    private static Utf8ExecutionLimitKind TryRentNonemptySlots(int groups, ref Utf8SliceInvocation invocation, out Utf8SliceSlots slots)
    {
        slots = default;
        var offsetCount = checked(groups * 2);
        var bytes = Utf8ConstructionBudget.ArrayBytes<int>(offsetCount);
        var breach = invocation.TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            return breach;
        }

        try
        {
            // Matching and capture replay install the unset markers before
            // reading the slots. Renting only admits and allocates storage.
            slots = new Utf8SliceSlots(new int[offsetCount]);
            return Utf8ExecutionLimitKind.None;
        }
        catch
        {
            invocation.Ledger.Release(bytes);
            throw;
        }
    }

    internal static void ReleaseSlots(Utf8SliceSlots slots, ref Utf8SliceInvocation invocation)
    {
        if (slots.OffsetCount != 0)
            invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int>(slots.OffsetCount));
    }

    // Width of the scalar ending at rightEdge (validated input): ASCII is
    // one byte, longer scalars measure back from their lead. Mid-scalar
    // edges and out-of-range positions fail closed so mirrored right-to-left
    // attempts cannot report unaligned starts. Shared by the mirrored consuming arms.
    private static bool TryGetScalarWidthEndingAt(ReadOnlySpan<byte> input, int rightEdge, out int width)
    {
        width = 0;
        if (rightEdge <= 0 || rightEdge > input.Length)
        {
            return false;
        }

        var leadIndex = rightEdge - 1;
        if (input[leadIndex] < 0x80)
        {
            width = 1;
            return true;
        }

        var steps = 0;
        while ((input[leadIndex] & 0xC0) == 0x80)
        {
            leadIndex--;
            steps++;
            if (leadIndex < 0 || steps > 3)
            {
                return false;
            }
        }

        var lead = input[leadIndex];
        if (lead < 0xC2 || lead >= 0xF5)
        {
            return false;
        }

        width = lead < 0xE0 ? 2 : lead < 0xF0 ? 3 : 4;
        return leadIndex + width == rightEdge;
    }

    private static bool TryReadScalar(ReadOnlySpan<byte> input, int edge, bool rightToLeft, out Rune scalar, out int width)
    {
        if ((uint)edge <= (uint)input.Length)
        {
            var status = rightToLeft
                ? Rune.DecodeLastFromUtf8(input[..edge], out scalar, out width)
                : Rune.DecodeFromUtf8(input[edge..], out scalar, out width);
            if (status == OperationStatus.Done) return true;
        }
        scalar = default;
        width = 0;
        return false;
    }

    private static bool IsWordScalarStartingAt(Utf8SliceProgram program, in Utf8SliceInstruction instruction,
        ReadOnlySpan<byte> input, int index)
        => TryReadScalar(input, index, false, out var scalar, out _) &&
            Utf8SliceScalarClasses.IsBoundaryWord(scalar, program, instruction);

    private static bool MatchesEndAnchor(ReadOnlySpan<byte> input, int position, in Utf8SliceInstruction instruction) =>
        position == input.Length ||
        instruction.A != (int)Utf8SliceAnchorKind.VeryEnd &&
        (uint)position < (uint)input.Length &&
        (instruction.B != 0 || position == input.Length - 1) && input[position] == (byte)'\n';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesBoundary(Utf8SliceProgram program, in Utf8SliceInstruction instruction,
        ReadOnlySpan<byte> input, int index)
        => Utf8ScalarWordBoundary.TryGetAsciiBoundary(input, index, out var asciiBoundary)
            ? asciiBoundary == (instruction.A != 0)
            : MatchesUnicodeBoundary(program, instruction, input, index);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MatchesUnicodeBoundary(Utf8SliceProgram program, in Utf8SliceInstruction instruction,
        ReadOnlySpan<byte> input, int index)
    {
        // Callers have checked the scalar-aligned position. The VM and its
        // complete boundary root classify the same two complete neighbors.
        var beforeWord = false;
        if (index > 0)
        {
            var previous = index - 1;
            while (previous > 0 && (input[previous] & 0xC0) == 0x80) previous--;
            beforeWord = IsWordScalarStartingAt(program, instruction, input, previous);
        }
        var afterWord = index < input.Length && IsWordScalarStartingAt(program, instruction, input, index);
        return (beforeWord != afterWord) == (instruction.A != 0);
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        scoped Span<int> captureStarts,
        scoped Span<int> captureEnds,
        bool startIsScalarBoundary,
        ref Utf8SliceInvocation invocation,
        bool collectHistory = false,
        bool requireNonEmpty = false)
    {
        if (program.IsNonBacktracking)
        {
            if (!startIsScalarBoundary || (uint)start > (uint)input.Length ||
                start < input.Length && (input[start] & 0xC0) == 0x80)
                return new(false, -1, Utf8ExecutionLimitKind.None);
            var outcome = RunNonBacktracking(program, input, start, captureStarts, captureEnds,
                true, requireNonEmpty, ref invocation);
            return new(outcome.Found, outcome.EndByte, outcome.Breach);
        }
        var workspace = new AttemptWorkspace();
        try
        {
            return TryMatchAt(program, input, start, captureStarts, captureEnds,
                collectHistory, requireNonEmpty, startIsScalarBoundary, ref workspace, ref invocation);
        }
        finally { workspace.Release(ref invocation); }
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        scoped Span<int> captureStarts,
        scoped Span<int> captureEnds,
        bool collectHistory,
        bool requireNonEmpty,
        bool startIsScalarBoundary,
        ref AttemptWorkspace workspace,
        ref Utf8SliceInvocation invocation, bool existenceOnly = false,
        (int Instruction, int Bytes) verifiedPrefix = default, bool requireFullInput = false, bool dotTailChecked = false,
        bool preserveClosingOrder = false)
    {
        // Profile cursors can reuse this admitted workspace across candidate
        // attempts while preserving their own empty-match and anchor progress.
        if (program.IsNonBacktracking)
        {
            if (requireFullInput) throw new InvalidOperationException("Full input matching requires the scalar backtracker.");
            return TryMatchAt(program, input, start, captureStarts, captureEnds,
                startIsScalarBoundary, ref invocation, collectHistory, requireNonEmpty);
        }
        if (!startIsScalarBoundary || (uint)start > (uint)input.Length ||
            start < input.Length && (input[start] & 0xC0) == 0x80)
            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None);

        var terminalRoot = collectHistory ? Utf8SliceTerminalRootKind.None : program.TerminalRoot;
        var canStopEarly = existenceOnly && !collectHistory && !requireNonEmpty && !requireFullInput;
        if (terminalRoot == Utf8SliceTerminalRootKind.ClassRepeat)
        {
            // The complete repeat has no mutable VM state. Its metered scalar
            // scan supplies the endpoint, including for a nonempty retry.
            if (captureStarts.Length != 0) captureStarts.Fill(-1);
            if (captureEnds.Length != 0) captureEnds.Fill(-1);
            var repeat = RunTerminalClassRepeat(program, input, start, requireNonEmpty, ref invocation, canStopEarly);
            return requireFullInput && repeat.IsMatch && repeat.EndIndex != input.Length
                ? new(false, -1, repeat.Breach) : repeat;
        }

        if (!workspace.Initialized)
        {
            var breach = workspace.Initialize(program, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, breach);
        }
        workspace.Stacks.ResetAttempt();
        if (verifiedPrefix.Bytes > 0 && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            !collectHistory && !requireFullInput && !program.RightToLeft &&
            TryGetTerminalInlineRepeatAssertion(program, verifiedPrefix.Instruction, out var guard))
        {
            // No instruction after this capture-free guard observes an
            // assertion endpoint. Its result belongs to the verified prefix.
            return MatchTerminalInlineRepeatAssertion(program, verifiedPrefix.Instruction + 1, guard,
                input, start + verifiedPrefix.Bytes, ref invocation);
        }
        var counts = workspace.Counts;
        var positions = workspace.Positions;
        var heads = workspace.Heads;
        if (counts.Length != 0) Array.Fill(counts, -1);
        if (positions.Length != 0) Array.Fill(positions, -1);
        if (captureStarts.Length != 0) captureStarts.Fill(-1);
        if (captureEnds.Length != 0) captureEnds.Fill(-1);
        if (heads.Length != 0) Array.Fill(heads, -1);
        var completed = false;
        try
        {
            Utf8SliceMatchOutcome attempt;
            int? requiredEnd = requireFullInput ? input.Length : null;
            if (terminalRoot == Utf8SliceTerminalRootKind.Capture)
            {
                // A proved complete root has no continuation or capture rollback.
                attempt = RunTerminalCaptureRoot(program, input, start, captureStarts, captureEnds,
                    ref invocation, canStopEarly);
            }
            else if (terminalRoot == Utf8SliceTerminalRootKind.Boundary)
            {
                // Keep initial workspace admission and reset above. This complete
                // root has no continuation or consuming nonempty alternative. Preserve
                // VM step/deadline cadence; callers retain final publication checks.
                var breach = invocation.TryChargeStep();
                invocation.Budget.ThrowIfCancellationRequested();
                if (breach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, breach);
                var matched = !requireNonEmpty && MatchesBoundary(program, program.Instructions[0], input, start);
                breach = invocation.TryChargeStep();
                invocation.Budget.ThrowIfCancellationRequested();
                attempt = new(matched && breach == Utf8ExecutionLimitKind.None,
                    matched && breach == Utf8ExecutionLimitKind.None ? start : -1, breach);
            }
            else if (!collectHistory && program.Assertions is [] or [{ CanRunInline: true }])
            {
                // A root with no suspending assertion can run one segment.
                // Leave larger assertion lists to the existing driver.
                var current = new AssertionCursor(program, start, counts, positions,
                    requireNonEmpty, startIsScalarBoundary, workspace.Stacks.FrameCount)
                {
                    ProgramIndex = verifiedPrefix.Instruction,
                    InputIndex = start + verifiedPrefix.Bytes,
                    RequiredEnd = requiredEnd
                };
                try
                {
                    attempt = RunSegment(input, captureStarts, captureEnds, heads, ref current,
                        false, preserveCaptureTrail: false, ref workspace.Stacks, ref invocation, out _, canStopEarly,
                        dotTailChecked ? verifiedPrefix.Instruction : -1, preserveClosingOrder);
                }
                finally
                {
                    workspace.Counts = current.Counts;
                    workspace.Positions = current.Positions;
                }
            }
            else
                attempt = RunFrom(program, input, start, captureStarts, captureEnds,
                    requireNonEmpty, startIsScalarBoundary, ref workspace, ref invocation, requiredEnd);
            // Complete terminal proofs return a unique or maximal extent. The
            // scalar VM tests the end within Accept so other outcomes can retry.
            if (requireFullInput && attempt.IsMatch && attempt.EndIndex != input.Length)
                return new(false, -1, attempt.Breach);
            if (attempt.IsMatch && program.RightToLeft)
            {
                // Mirrored runs record right edges at capture starts and left
                // edges at capture ends; projection and backreferences read
                // normalized spans while the trail stays raw and consistent.
                for (var slot = 0; slot < captureStarts.Length; slot++)
                {
                    if (captureStarts[slot] >= 0 && captureEnds[slot] >= 0 && captureEnds[slot] < captureStarts[slot])
                    {
                        var normalizedStart = captureEnds[slot];
                        captureEnds[slot] = captureStarts[slot];
                        captureStarts[slot] = normalizedStart;
                    }
                }
            }

            completed = attempt.IsMatch;
            return attempt;
        }
        finally
        {
            if (!completed)
            {
                // A committed positive assertion still belongs to this attempt;
                // failure or cancellation must not publish its capture slots.
                if (captureStarts.Length != 0) captureStarts.Fill(-1);
                if (captureEnds.Length != 0) captureEnds.Fill(-1);
            }
        }
    }

    // Callers supply valid UTF-8. Candidates and results use original byte
    // coordinates; no validity pass or subject coordinate map is required.
    internal static Utf8SliceSearchOutcome TryFindFirst(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds, bool collectHistory,
        ref Utf8SliceInvocation invocation, in Utf8SearchPlan candidatePlan = default,
        bool existenceOnly = false, int screenedLiteral = -1, int requiredLiteralContinuation = 0)
    {
        if (requiredLiteralContinuation > 0 && !existenceOnly && !collectHistory &&
            TryFindPreparedLiteralRange(program, input, ref fromByte, candidatePlan.RequiredPrefilterSearcher.LiteralSearch,
                ref screenedLiteral, requiredLiteralContinuation, ref invocation, out var range))
            return range;

        // The prepared forward proof needs no attempt workspace at byte zero.
        if (fromByte == 0 && program.TerminalRoot == Utf8SliceTerminalRootKind.AsciiSetThenRepeat &&
            TryFindFirstShortAsciiRepeat(program, input, fromByte, candidatePlan.MinRequiredLength,
                !existenceOnly && !collectHistory, ref invocation, out var shortMatch))
            return shortMatch;

        // A complete single-origin proof can finish without initializing
        // an attempt workspace. The unresolved case keeps the same origin.
        var singleOrigin = false;
        ref readonly var first = ref program.Instructions[0];
        if (first.Kind == Utf8SliceInstructionKind.Lookbehind && !collectHistory &&
            captureStarts.IsEmpty && captureEnds.IsEmpty && !program.ReadsCaptures &&
            TryFindGuardedRepeat(program, input, ref fromByte, ref invocation, out var guarded))
            return guarded;
        if (fromByte == 0 && !program.IsNonBacktracking && !program.RightToLeft &&
            first is { Kind: Utf8SliceInstructionKind.Anchor, B: 0, D: not 0 })
        {
            var seekBeginning = first.A == (int)Utf8SliceAnchorKind.Beginning;
            singleOrigin = seekBeginning || first.A == (int)Utf8SliceAnchorKind.Contiguous &&
                input.Length < SingleOriginPrefixInstructionLimit && invocation.AnchorOriginByte == 0;
            if (singleOrigin)
            {
                invocation.Budget.ThrowIfCancellationRequested();
                if (TryProveSingleOrigin(program, input, ref fromByte, !collectHistory && captureStarts.IsEmpty,
                    ref invocation, existenceOnly, first, seekBeginning, seekVeryEnd: false, out var result))
                    return result;
            }
        }
        return FindFirstWithLocalWorkspace(program, input, fromByte, captureStarts, captureEnds,
            collectHistory, ref invocation, candidatePlan, existenceOnly, screenedLiteral, singleOrigin);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8SliceSearchOutcome FindFirstWithLocalWorkspace(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds, bool collectHistory,
        ref Utf8SliceInvocation invocation, in Utf8SearchPlan candidatePlan,
        bool existenceOnly, int screenedLiteral, bool singleOrigin)
    {
        var workspace = new AttemptWorkspace();
        try
        {
            return singleOrigin
                ? MatchSingleOrigin(program, input, fromByte, captureStarts, captureEnds, collectHistory,
                    ref workspace, ref invocation, existenceOnly, seekVeryEnd: false)
                : TryFindFirst(program, input, fromByte, captureStarts, captureEnds,
                    collectHistory, ref workspace, ref invocation, candidatePlan, existenceOnly, screenedLiteral);
        }
        finally
        {
            if (workspace.NeedsRelease) workspace.Release(ref invocation);
        }
    }

    // Repeated searches for one program/invocation can keep their admitted
    // scratch. The caller releases it; each attempt still resets all VM state.
    internal static Utf8SliceSearchOutcome TryFindFirst(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds, bool collectHistory,
        ref AttemptWorkspace workspace, ref Utf8SliceInvocation invocation, in Utf8SearchPlan candidatePlan = default,
        bool existenceOnly = false, int screenedLiteral = -1, int requiredLiteralContinuation = 0)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)fromByte > (uint)input.Length ||
            fromByte < input.Length && (input[fromByte] & 0xC0) == 0x80)
            return new(false, -1, -1, Utf8ExecutionLimitKind.None);
        if (program.TerminalRoot == Utf8SliceTerminalRootKind.DelimitedClassRepeat &&
            !collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty)
            return FindDelimitedClassRepeat(program, input, fromByte, ref invocation);
        if (program.TerminalRoot == Utf8SliceTerminalRootKind.LazyDotLiteral &&
            !collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty)
            return FindTerminalLazyDotLiteral(program, input, fromByte, ref invocation);
        if (!collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            TryScanTerminalAsciiSuffixAlternatives(program, input, fromByte, firstOnly: true,
                ref invocation, out var suffixes, out var suffixRange))
            return suffixRange.Success ? new(true, suffixRange.StartOffsetInBytes, suffixRange.EndOffsetInBytes, suffixes.Breach)
                : new(false, -1, -1, suffixes.Breach);
        if (program.TerminalRoot == Utf8SliceTerminalRootKind.DecimalWithOptionalFraction &&
            !collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit)
            return FindTerminalDecimalWithOptionalFraction(input, fromByte, ref invocation, existenceOnly);
        if (requiredLiteralContinuation > 0 && !existenceOnly && !collectHistory &&
            TryFindPreparedLiteralRange(program, input, ref fromByte, candidatePlan.RequiredPrefilterSearcher.LiteralSearch,
                ref screenedLiteral, requiredLiteralContinuation, ref invocation, out var range))
            return range;
        if (program.IsNonBacktracking)
        {
            if (captureStarts.Length == 0 && program.NonBacktrackingOrderedClosures.Length != 0)
                return FindOrderedRegularRange(program, input, fromByte, ref invocation);
            // Replay one set of captures when the remaining input is small
            // relative to the register matrix. For longer input, select the
            // range first so an early hit avoids building the whole table.
            var suffixBytes = input.Length - fromByte;
            if (captureStarts.Length != 0 && program.Instructions.Length > 64 &&
                suffixBytes <= 8L * (2L * program.GroupCount + program.NonBacktrackingCaptureOpeningCount))
            {
                if (suffixBytes <= 128)
                    return Utf8NonBacktrackingSearchState.FindFirstWideCapture(program, input, fromByte,
                        captureStarts, captureEnds, ref invocation);
                var selected = RunNonBacktracking(program, input, fromByte, [], [], false, false, ref invocation);
                if (selected.Found)
                    return Utf8NonBacktrackingSearchState.FindFirstWideCapture(program, input, selected.StartByte,
                        captureStarts, captureEnds, ref invocation, selected.EndByte);
                captureStarts.Fill(-1);
                captureEnds.Fill(-1);
                return selected;
            }
            return RunNonBacktracking(program, input, fromByte, captureStarts, captureEnds,
                false, false, ref invocation);
        }
        var reverse = program.RightToLeft;
        var remaining = reverse ? fromByte : input.Length - fromByte;
        if (CanSearchOrdinaryOrdered(program, remaining, candidatePlan, ref invocation) &&
            CanAdmitOrderedRows(program, ref invocation))
        {
            var orderedStart = fromByte;
            var breach = TrySeedOrderedClassFamily(program, input, candidatePlan,
                ref orderedStart, ref invocation, out var orderedEnd, out var provedPrefix);
            if (breach != Utf8ExecutionLimitKind.None || orderedStart < 0) return new(false, -1, -1, breach);
            var selected = orderedEnd < 0
                ? FindOrderedRegularRange(program, input, orderedStart, ref invocation)
                : new Utf8SliceSearchOutcome(true, orderedStart, orderedEnd, Utf8ExecutionLimitKind.None);
            if (!selected.Found || captureStarts.Length == 0 && !collectHistory) return selected;
            // Captures never affect discovery for this graph. Run the chosen
            // start to publish ordinary capture history. With no anchors or
            // boundaries, a path past the winning end cannot return to it.
            var replayInput = !reverse && program.NonBacktrackingOrderedConditions == 0
                ? input[..selected.EndByte] : input;
            var origin = reverse ? selected.EndByte : selected.StartByte;
            var replay = orderedEnd >= 0 && !collectHistory && !program.ReadsCaptures && program.HasSingleCapturePerGroup
                ? ReplayOrderedFamilyPrefix(program, replayInput, origin, captureStarts, captureEnds,
                    provedPrefix, ref workspace, ref invocation)
                : TryMatchAt(program, replayInput, origin, captureStarts, captureEnds,
                    collectHistory, false, true, ref workspace, ref invocation);
            return replay.IsMatch
                ? new(true, Math.Min(origin, replay.EndIndex), Math.Max(origin, replay.EndIndex),
                    replay.Breach)
                : new(false, -1, -1, replay.Breach);
        }
        if (program.FollowingDecimalContinuation.Aligned &&
            !collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            screenedLiteral < fromByte && candidatePlan.LiteralUtf8 is [var delimiter] &&
            program.LeadingClassDelimiter is var window && window.Literal == delimiter)
        {
            if (invocation.Ledger.ReservedBytes != 0)
            {
                var admission = invocation.TryReserveWorking(0);
                if (admission != Utf8ExecutionLimitKind.None) return new(false, -1, -1, admission);
            }
            return ScanAlignedDecimalWindows(program, input, fromByte, delimiter, window.LiteralIndex,
                ref workspace, ref invocation, countAll: false, existenceOnly, out _);
        }

        if (program.TerminalRoot == Utf8SliceTerminalRootKind.AsciiSetThenRepeat &&
            TryFindFirstShortAsciiRepeat(program, input, fromByte, candidatePlan.MinRequiredLength,
                !existenceOnly && !collectHistory, ref invocation, out var shortMatch))
            return shortMatch;

        ref readonly var first = ref program.Instructions[0];
        var seekBeginning = !reverse && first.Kind == Utf8SliceInstructionKind.Anchor &&
            first.A == (int)Utf8SliceAnchorKind.Beginning;
        var seekVeryEnd = reverse && first is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.VeryEnd };
        var atContiguousOrigin = first is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Contiguous } &&
            !reverse && invocation.AnchorOriginByte == fromByte;
        if (seekBeginning && first.B == 0 || seekVeryEnd || atContiguousOrigin)
            return TryFindFirstAtSingleOrigin(program, input, fromByte, captureStarts, captureEnds, collectHistory,
                ref workspace, ref invocation, existenceOnly, first, seekBeginning, seekVeryEnd);

        if (remaining >= 256 && !program.ReadsCaptures &&
            program.HasShortDecimalHead && (program.EmittedMatch ?? program.TieredProof) is { } decimalProof &&
            invocation.CheckpointHook is null && invocation.IsUnmetered &&
            TryGetFixedDecimalFollower(program, out var digits, out var firstFollower, out var lastFollower))
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
            if (!TryFindFixedDecimalMatch(input, fromByte, digits, firstFollower, lastFollower, decimalProof, out var start, out var end))
                return new(false, -1, -1, Utf8ExecutionLimitKind.None);
            if (!collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty)
            {
                breach = invocation.TryChargeInputStep();
                return breach == Utf8ExecutionLimitKind.None
                    ? new(true, start, end, breach) : new(false, -1, -1, breach);
            }

            // The complete proof selects the ordered origin. Publish all
            // captures using the original input and the invocation's workspace.
            var replay = TryMatchAt(program, input, start, captureStarts, captureEnds,
                collectHistory, false, true, ref workspace, ref invocation);
            if (replay.Breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, replay.Breach);
            if (replay.IsMatch) return new(true, start, replay.EndIndex, Utf8ExecutionLimitKind.None);
        }

        var emitted = GetCandidateProof(program, !collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            first.Kind != Utf8SliceInstructionKind.Anchor, ref invocation, out var interpretedVisits);

        if (remaining is > 0 and <= 32 && !reverse && !program.IgnoreCase && !program.HasContiguousAnchor &&
            !collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty && !program.ReadsCaptures &&
            remaining >= candidatePlan.MinRequiredLength && input[fromByte] is < 128 and var firstByte &&
            (first.Kind == Utf8SliceInstructionKind.Fork
                ? program.LeadingByteCandidate is { First: >= 0 and < 128, InstructionIndex: 0, FromBranch: true } branch &&
                    MayStartAtByte(firstByte, branch.First, branch.Last, branch.SparsePair, false)
                : emitted is not null && Utf8AsciiBytePredicates.IsDigit(firstByte) && program.HasShortDecimalHead &&
                    invocation.CheckpointHook is null && invocation.IsUnmetered) &&
            !CanSeekExactPrefix(program, candidatePlan))
        {
            // A branch or ready decimal proof can verify the first origin.
            // A rejection advances one ASCII byte before general discovery.
            if (invocation.Ledger.ReservedBytes != 0)
            {
                var admission = invocation.TryReserveWorking(0);
                if (admission != Utf8ExecutionLimitKind.None) return new(false, -1, -1, admission);
            }
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
            invocation.Budget.ThrowIfCancellationRequested();
            if (MayMatchAdmittedCandidate(program, input, fromByte, 0, out var acceptedEnd, emitted,
                ref interpretedVisits, proveMatch: true))
            {
                if (acceptedEnd >= 0)
                {
                    breach = invocation.TryChargeInputStep();
                    invocation.Budget.ThrowIfCancellationRequested();
                    if (interpretedVisits > 0 && breach == Utf8ExecutionLimitKind.None)
                        program.ObserveProofWork(interpretedVisits);
                    return breach == Utf8ExecutionLimitKind.None
                        ? new(true, fromByte, acceptedEnd, breach) : new(false, -1, -1, breach);
                }
                var attempt = TryMatchAt(program, input, fromByte, [], [], false, false, true,
                    ref workspace, ref invocation, existenceOnly);
                if (attempt.Breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, attempt.Breach);
                if (attempt.IsMatch) return new(true, fromByte, attempt.EndIndex, Utf8ExecutionLimitKind.None);
            }
            fromByte++;
        }

        var outcome = TryFindFirstByCandidateSearch(program, input, fromByte, captureStarts, captureEnds, collectHistory,
            ref workspace, ref invocation, candidatePlan, existenceOnly, screenedLiteral, seekBeginning, first,
            emitted, ref interpretedVisits);
        // Publish once after the admitted search, including a scan of many candidates.
        if (interpretedVisits > 0 && outcome.Breach == Utf8ExecutionLimitKind.None)
            program.ObserveProofWork(interpretedVisits);
        return outcome;
    }

    private static Utf8SliceSearchOutcome TryFindFirstAtSingleOrigin(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds, bool collectHistory,
        ref AttemptWorkspace workspace, ref Utf8SliceInvocation invocation, bool existenceOnly,
        in Utf8SliceInstruction first, bool seekBeginning, bool seekVeryEnd)
        => TryProveSingleOrigin(program, input, ref fromByte, !collectHistory && captureStarts.IsEmpty,
            ref invocation, existenceOnly, first, seekBeginning, seekVeryEnd, out var result)
            ? result
            : MatchSingleOrigin(program, input, fromByte, captureStarts, captureEnds,
                collectHistory, ref workspace, ref invocation, existenceOnly, seekVeryEnd);

    private static bool TryProveSingleOrigin(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, ref int fromByte, bool allowProof,
        ref Utf8SliceInvocation invocation, bool existenceOnly, in Utf8SliceInstruction first,
        bool seekBeginning, bool seekVeryEnd, out Utf8SliceSearchOutcome result)
    {
        // These anchors admit one origin in the search direction.
        // Keep its normal attempt and interruption checks.
        if (seekBeginning && (fromByte != 0 || first.C > 0))
        {
            var seekBreach = TrySeekBeginning(program, input, ref fromByte, ref invocation);
            if (seekBreach != Utf8ExecutionLimitKind.None || fromByte < 0)
            {
                result = new(false, -1, -1, seekBreach);
                return true;
            }
        }
        var breach = invocation.TryChargeInputStep();
        if (breach == Utf8ExecutionLimitKind.None)
            invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None || seekVeryEnd && fromByte != input.Length)
        {
            result = new(false, -1, -1, breach);
            return true;
        }
        var proofStart = -1;
        var proofLimit = BranchedPrefixInstructionLimit;
        if (!seekVeryEnd && allowProof)
        {
            if (first.D == 1 && input.Length - fromByte <= ShortSingleOriginProofInputLimit)
                proofStart = 1;
            else if (first.D < 0 && input.Length <= SingleOriginPrefixInstructionLimit)
            {
                proofStart = -first.D;
                // The anchor admits one proof. Keep the smaller allowance
                // on short inputs and when searching multiple origins.
                proofLimit = GetLongSingleOriginProofLimit(input.Length);
            }
            else if (!existenceOnly &&
                first is { A: (int)Utf8SliceAnchorKind.Contiguous, D: 1 } &&
                input.Length - fromByte <= SingleOriginPrefixInstructionLimit - 2 &&
                program.Instructions[1] is { Kind: Utf8SliceInstructionKind.RepeatGreedy, HasSingleAtomBody: true } repeat &&
                program.Instructions[repeat.A + 1].Kind == Utf8SliceInstructionKind.Accept &&
                invocation.IsUnmetered && invocation.CheckpointHook is null)
            {
                // A terminal repeat can use the existing longer range proof.
                // Leave one visit each for the repeat and its acceptance.
                proofStart = 1;
                proofLimit = SingleOriginPrefixInstructionLimit;
            }
        }
        if (proofStart >= 0)
        {
            // The admitted short witness starts after the confirmed anchor.
            // Unknown instructions still enter the original attempt.
            int acceptedEnd;
            bool possible;
            var visits = 0;
            var atBeginning = seekBeginning && fromByte == 0;
            var emitted = atBeginning ? program.EmittedMatch : null;
            var tiered = false;
            if (emitted is null &&
                (atBeginning || proofStart == 1 && first.A == (int)Utf8SliceAnchorKind.Contiguous &&
                    proofLimit == BranchedPrefixInstructionLimit) &&
                (program.ObservesProofWork || program.TieredProof is not null || program.EmittedMatch is not null) &&
                invocation.CheckpointHook is null && invocation.IsUnmetered)
            {
                emitted = program.EmittedMatch ?? program.TieredProof;
                tiered = emitted is null && program.ObservesProofWork;
            }
            // Allow the same visits as the bounded interpreter, including
            // entering the repeat, consuming its LF delimiter and accepting.
            if (first.C < 0 && input.Length - fromByte <= proofLimit - 3)
            {
                var length = MatchLeadingFoldedRepeat(program, input[fromByte..]);
                possible = length >= 0;
                acceptedEnd = possible ? fromByte + length : -1;
            }
            else if (first is { A: (int)Utf8SliceAnchorKind.Contiguous, C: > 0 } && input.Length - fromByte <= proofLimit - 3)
            {
                var length = MatchLiteralLine(input[fromByte..], program.Instructions.AsSpan(1, first.C));
                possible = length >= 0;
                acceptedEnd = possible ? fromByte + length : -1;
            }
            else if (emitted is not null && emitted(fromByte == 0 ? input : input[fromByte..]) is var end &&
                end != Utf8BoundedProgramEmitter.Incomplete)
            {
                possible = end >= 0;
                acceptedEnd = possible ? fromByte + end : -1;
            }
            else if (tiered)
            {
                var remaining = proofLimit;
                var probeEnd = ProbeBranchedAsciiPrefix(program, input, fromByte, proofStart, ref remaining, proveMatch: true);
                possible = probeEnd != BranchedPrefixRejected;
                acceptedEnd = Math.Max(BranchedPrefixUndecided, probeEnd);
                visits = proofLimit - remaining;
            }
            else
                possible = MayMatchBranchedAsciiPrefix(program, input, fromByte, proofStart, out acceptedEnd,
                    proveMatch: true, instructionLimit: proofLimit);
            if (!possible || acceptedEnd >= 0)
            {
                // The proof acquires no storage. Any enclosing loan was
                // already admitted, and this step retains a prior denial.
                breach = invocation.TryChargeInputStep();
                invocation.Budget.ThrowIfCancellationRequested();
                if (breach == Utf8ExecutionLimitKind.None && visits != 0)
                    program.ObserveProofWork(visits);
                result = possible && breach == Utf8ExecutionLimitKind.None
                    ? new(true, fromByte, acceptedEnd, breach)
                    : new(false, -1, -1, breach);
                return true;
            }
        }
        result = default;
        return false;
    }

    private static int MatchLeadingFoldedRepeat(Utf8SliceProgram program, ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty) return -1;
        var code = program.Instructions;
        var head = program.SetMasks[code[1].A];
        var value = input[0];
        var width = value < 128 && (head[value >> 3] & (1 << (value & 7))) != 0
            ? 1 : GetFoldedMaskScalarWidth(input, 0, head, program.CaseBehavior);
        if (width == 0) return -1;

        var body = program.SetMasks[code[3].A];
        var end = width + ScanAsciiMask(input[width..], body, category: false);
        if (code[6].Kind == Utf8SliceInstructionKind.Accept)
            return end == input.Length ? end : ContinueFoldedAsciiMask(input, end, body, program.CaseBehavior);
        if (MatchesEndAnchor(input, end, code[6])) return end;
        end = ContinueFoldedAsciiMask(input, end, body, program.CaseBehavior);
        return MatchesEndAnchor(input, end, code[6]) ? end : -1;
    }

    private static int MatchLiteralLine(ReadOnlySpan<byte> input, ReadOnlySpan<Utf8SliceInstruction> prefix)
    {
        if (input.Length <= prefix.Length) return -1;
        for (var i = 0; i < prefix.Length; i++)
            if (input[i] != prefix[i].A) return -1;
        // LF cannot occur inside a multibyte scalar. It ends the first line.
        var newline = input[prefix.Length..].IndexOf((byte)'\n');
        return newline < 0 ? -1 : prefix.Length + newline + 1;
    }

    private static Utf8SliceSearchOutcome MatchSingleOrigin(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds, bool collectHistory,
        ref AttemptWorkspace workspace, ref Utf8SliceInvocation invocation, bool existenceOnly, bool seekVeryEnd)
    {
        var outcome = TryMatchAt(program, input, fromByte, captureStarts, captureEnds,
            collectHistory, false, true, ref workspace, ref invocation, existenceOnly);
        return outcome.IsMatch && outcome.Breach == Utf8ExecutionLimitKind.None
            ? new(true, seekVeryEnd ? outcome.EndIndex : fromByte, seekVeryEnd ? fromByte : outcome.EndIndex,
                Utf8ExecutionLimitKind.None)
            : new(false, -1, -1, outcome.Breach);
    }

    private static bool TryFindFirstShortAsciiRepeat(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int fromByte, int minimumLength, bool allowProof, ref Utf8SliceInvocation invocation, out Utf8SliceSearchOutcome result)
    {
        if (input.Length - fromByte <= 64 && allowProof && program.GroupCount == 0 &&
            program.LeadingByteCandidate is { First: >= 0 and < 128 } starter)
        {
            result = FindFirstShortAsciiRepeat(program, input, fromByte, input.Length - minimumLength,
                (byte)starter.First, starter.Last, starter.SparsePair, ref invocation);
            return true;
        }
        result = default;
        return false;
    }

    private static Utf8SliceSearchOutcome FindFirstShortAsciiRepeat(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte, int lastProbedStart,
        byte first, byte last, bool sparsePair, ref Utf8SliceInvocation invocation)
    {
        // The complete shape has one ASCII set followed by a terminal ASCII
        // repeat. Its own starter and masks suffice for the bounded search.
        var breach = invocation.TryReserveWorking(0);
        if (breach != Utf8ExecutionLimitKind.None)
            return new(false, -1, -1, breach);
        var code = program.Instructions;
        var head = program.SetMasks[code[0].A];
        var tail = program.SetMasks[code[2].A];
        var minimum = code[1].RepeatMinimum;
        for (var position = fromByte; ; position++)
        {
            if (position == input.Length || !MayStartAtByte(input[position], first, last, sparsePair, includeKelvin: false))
            {
                breach = TrySeekLeadingByte(input, ref position, first, last, false, ref invocation, sparsePair: sparsePair);
                if (breach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, breach);
                if (position < 0)
                    return new(false, -1, -1, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
            }
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, -1, breach);
            invocation.Budget.ThrowIfCancellationRequested();
            if (position > lastProbedStart)
                return new(false, -1, -1, Utf8ExecutionLimitKind.None);

            // The starter is ASCII. A rejected candidate advances one byte.
            var value = input[position];
            if ((head[value >> 3] & (1 << (value & 7))) == 0) continue;
            var length = ScanAsciiMask(input[(position + 1)..], tail, category: false);
            if (length < minimum) continue;

            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            return breach == Utf8ExecutionLimitKind.None
                ? new(true, position, position + 1 + length, breach)
                : new(false, -1, -1, breach);
        }
    }

    // Single-origin matches and ordered search do not need the candidate
    // scanner's search windows, delimiter state or per-candidate proofs.
    private static Utf8SliceSearchOutcome TryFindFirstByCandidateSearch(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds, bool collectHistory,
        ref AttemptWorkspace workspace, ref Utf8SliceInvocation invocation, in Utf8SearchPlan candidatePlan,
        bool existenceOnly, int screenedLiteral, bool seekBeginning, in Utf8SliceInstruction first,
        Utf8BoundedProgramEmitter.Match? emitted, ref int interpretedVisits)
    {
        var (leading, leadingLast, second, secondLast, leadingScalarIndex, fromBranch, sparsePair) = program.LeadingByteCandidate;
        var includeKelvin = leading == 'K' && program.LeadingByteAdmitsKelvin;
        // Prefer a straight ASCII proof. Prepared literal search can be more
        // selective than a branch interval or the lead of a wider scalar.
        var seekLiteral = (leading is < 0 or >= 128 || fromBranch) && !seekBeginning && CanSeekExactPrefix(program, candidatePlan);
        if (seekLiteral) leading = -1;
        var checkBranchedPrefix = leading is >= 0 and < 128 && program.Instructions[leadingScalarIndex] is
            { Kind: Utf8SliceInstructionKind.Fork } or { IsLiteralChoice: true };
        // A capture declaration can survive lowering even when the complete
        // path contains no capture instructions. Keep its attempt checks.
        var checkStraightRepeat = leading is >= 0 and < 128 && leadingScalarIndex == 0 &&
            !existenceOnly && !collectHistory && input.Length - fromByte <= 64 &&
            program.TerminalRoot == Utf8SliceTerminalRootKind.AsciiSetThenRepeat;
        var requiresEqualScalars = StartsWithSingleScalarBackreference(program);
        var lastProbedStart = input.Length - candidatePlan.MinRequiredLength;
        var leadingClass = -1;
        var classLiteral = -1;
        var classSearch = default(PreparedSubstringSearch?);
        byte[][]? classAlternatives = null;
        (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd, bool Terminal) repeatPrefix = default;
        long classGapMaximum = 0;
        var directClassLiteral = false;
        var seekDecimalWindow = false;
        var singleWindowAttempt = false;
        var requiredClassSuffix = default((int First, int Second));
        var verifiedClassLiteralLength = 0;
        var screenedDelimiter = -1;
        var wideInput = input.Length - fromByte >= 256;
        var needsClassWindow = leading is < 0 or >= 128;
        var leadingDecimalBoundary = first is { Kind: Utf8SliceInstructionKind.Boundary, A: 1 } &&
            program.LeadingScalarIsDecimal;
        var exactNeedle = candidatePlan.Kind == Utf8SearchKind.AsciiFoldedByteLiteral ? null : candidatePlan.LiteralUtf8;
        var classDelimiter = program.LeadingClassDelimiter;
        if ((needsClassWindow || exactNeedle is { Length: 1 } &&
                classDelimiter is { Direct: true, Literal: >= 0 }) &&
            !seekLiteral && !seekBeginning && !program.RightToLeft &&
            !program.IgnoreCase && !program.HasContiguousAnchor &&
            (wideInput || exactNeedle is { Length: > 0 } ||
                candidatePlan.RequiredPrefilterSearcher.Kind == PreparedSearcherKind.MultiLiteral))
        {
            var (literal, windowClass, literalIndex, gapMaximum, direct) = classDelimiter;
            leadingClass = windowClass;
            classGapMaximum = gapMaximum;
            directClassLiteral = direct;
            if (needsClassWindow && directClassLiteral && leadingDecimalBoundary && leadingClass == leadingScalarIndex &&
                screenedLiteral < fromByte && exactNeedle is [var delimiter] && delimiter == literal)
            {
                // A leading boundary admits only a bounded direct class window.
                // The window seeks this byte directly and verifies every start.
                classLiteral = literal;
                seekDecimalWindow = leadingClass == 2 && classGapMaximum == 0;
            }
            else if (leadingClass >= 0 && literal >= 0)
            {
                classSearch = GetAlignedDelimiterSearch(program, literalIndex, literal, candidatePlan);
                var proof = program.LeadingClassWindowProof;
                if (directClassLiteral && classSearch is { } aligned)
                {
                    var proofBreach = TryProveSingleWindowAttempt(program, leadingClass, (byte)literal,
                        ref invocation, out singleWindowAttempt);
                    if (proofBreach != Utf8ExecutionLimitKind.None)
                        return new(false, -1, -1, proofBreach);
                    if (singleWindowAttempt)
                    {
                        requiredClassSuffix = proof.SuffixAfter(aligned.Length);
                        if (!collectHistory && proof.CanReusePrefix &&
                            (program.GroupCount == 0 || captureStarts.IsEmpty && captureEnds.IsEmpty))
                        {
                            verifiedClassLiteralLength = aligned.Length;
                            if (existenceOnly && captureStarts.IsEmpty && captureEnds.IsEmpty &&
                                requiredClassSuffix.Second != 0 && proof.CanAcceptAfterPair)
                                return FindAlignedClassPair(program, input, fromByte, leadingClass, (byte)literal,
                                    aligned, requiredClassSuffix,
                                    ReferenceEquals(candidatePlan.RequiredPrefilterSearcher.LiteralSearch.Needle, aligned.Needle)
                                        ? screenedLiteral : -1,
                                    ref invocation);
                        }
                    }
                }
                // A complete terminal window also pays off on short subjects
                // and the final cursor advances.
                if (singleWindowAttempt || needsClassWindow &&
                    (directClassLiteral && (classSearch is { Length: 1 } ||
                        proof.CanFinishWhitespace && classSearch?.Length == proof.LiteralLength &&
                        invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit) ||
                     wideInput && (directClassLiteral || classSearch is not null)))
                {
                    classLiteral = literal;
                    // Reuse the prefilter's first hit only for the same
                    // aligned needle and a floor that still admits it.
                    if (directClassLiteral && screenedLiteral >= fromByte && classSearch is { } selected &&
                        ReferenceEquals(candidatePlan.RequiredPrefilterSearcher.LiteralSearch.Needle, selected.Needle))
                        screenedDelimiter = screenedLiteral;
                }
            }
            else if (leadingClass >= 0 && literalIndex >= 0 &&
                candidatePlan.RequiredPrefilterSearcher.Kind == PreparedSearcherKind.MultiLiteral)
            {
                var proofBreach = invocation.TryChargeInputStep();
                if (proofBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, proofBreach);
                if (ReferenceEquals(candidatePlan.AlignedClassDelimiterProgram, program))
                    classAlternatives = candidatePlan.RequiredPrefilterSearcher.ExactMultiLiterals;
            }
            if (needsClassWindow && classAlternatives is null &&
                (classLiteral < 0 || !directClassLiteral && program.GroupCount == 0 &&
                    !collectHistory && captureStarts.IsEmpty && captureEnds.IsEmpty &&
                    invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit &&
                    classSearch is { Length: > 1 } windowSearch &&
                    literalIndex + windowSearch.Length == program.Instructions.Length - 1) &&
                GetRepeatPrefixSearch(program, candidatePlan, out var repeated) is { } prefixSearch &&
                (classLiteral < 0 || repeated.Terminal))
            {
                // A complete repeat proof can finish at the same delimiter.
                classSearch = prefixSearch;
                classLiteral = prefixSearch.Needle[0];
                repeatPrefix = repeated;
                if (screenedLiteral >= fromByte &&
                    ReferenceEquals(candidatePlan.RequiredPrefilterSearcher.LiteralSearch.Needle, prefixSearch.Needle))
                    screenedDelimiter = screenedLiteral;
            }
        }
        // Keep literal guidance when available. Decimal search classifies
        // each candidate scalar before entering the verifier.
        var hasClassWindow = classLiteral >= 0 || classAlternatives is not null;
        if (hasClassWindow && leading >= 128) leading = -1;
        var screenDecimalWindow = hasClassWindow && leadingDecimalBoundary && leadingClass == leadingScalarIndex;
        var followingWindow = screenDecimalWindow && directClassLiteral && classLiteral >= 0 &&
            !Utf8AsciiBytePredicates.IsDigit((byte)classLiteral)
            ? program.FollowingDecimalLiteralWindow : default;
        var seekDecimal = leading < 0 && !seekLiteral && !hasClassWindow &&
            program.LeadingScalarIsDecimal;
        var canProveValue = !collectHistory && captureStarts.Length == 0 && !program.ReadsCaptures;
        var lookbehindMask = leading is >= 0 and < 128 && leadingScalarIndex == 1 && canProveValue && captureEnds.IsEmpty &&
            invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit
                ? GetGuardedRepeatLookbehind(program, first) : null;
        var proveBranchedMatch = canProveValue && checkBranchedPrefix &&
            (leadingScalarIndex == 0 ||
             Utf8SliceInstruction.SkipLeadingCaptureMarkers(program.Instructions, 0) == leadingScalarIndex);
        var shortDecimalHead = canProveValue && seekDecimal && program.HasShortDecimalHead;
        var proveDecimalWindow = canProveValue && followingWindow.Min != 0 && program.SkipsTerminalRewind(1);
        var reuseDecimalWindow = proveDecimalWindow && leadingClass == 2 && program.Instructions[1].RepeatMinimum == 1;
        var verifyFollowingWindow = reuseDecimalWindow && program.FollowingDecimalContinuation.Index != 0 &&
            !repeatPrefix.Any.HasValue && classAlternatives is null && classLiteral == classDelimiter.Literal;
        var rewoundDigitsAreWords = verifyFollowingWindow &&
            first is { B: (int)Utf8SliceWordKind.Unicode, C: 0 };
        if (seekDecimal)
        {
            leading = '0';
            leadingLast = (byte)'9';
        }
        var shortRepeatWindow = leadingScalarIndex > 0 && leading is >= 0 and < 128
            ? program.LeadingRepeatLiteralWindow : default;
        Span<byte> prefixStorage = stackalloc byte[16];
        var leadingPrefix = wideInput && second >= 0 && !candidatePlan.CanGuideFallbackStarts
            ? GetExactLeadingLiteral(program, leadingScalarIndex, prefixStorage) : default;
        // The checked search also verifies a prefix at the current position.
        // Only a forward, instruction-zero run can enter the VM directly.
        var verifiedLeadingPrefix = !leadingPrefix.IsEmpty && leadingScalarIndex == 0 &&
            !program.RightToLeft && !program.HasContiguousAnchor ? (leadingPrefix.Length, leadingPrefix.Length) : default;
        var classContinuation = verifiedClassLiteralLength != 0 ? classDelimiter.LiteralIndex + verifiedClassLiteralLength : 0;
        var prefixContinuation = classContinuation != 0 ? classContinuation : verifiedLeadingPrefix.Item1;
        var proofRepeatIndex = classContinuation != 0 && program.GroupCount != 0
            ? Utf8SliceInstruction.SkipLeadingCaptureMarkers(program.Instructions, classContinuation) : prefixContinuation;
        var prefixProof = prefixContinuation != 0 && canProveValue && captureEnds.IsEmpty &&
            // Each nested proof branch spends a visit. Tight depth limits
            // stay with the VM's exact admission of individual choices.
            invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit &&
            (CanProveRepeatPrefix(program, proofRepeatIndex) ||
                !existenceOnly && verifiedLeadingPrefix.Item1 == proofRepeatIndex &&
                CanProveOptionalLiteralThenHybrid(program.Instructions, proofRepeatIndex));
        var lineProofStart = GetAnchoredLineProofStart(seekBeginning, first, ref invocation);
        if ((leading >= 0 || seekLiteral || hasClassWindow || lineProofStart != 0) && invocation.Ledger.ReservedBytes != 0)
        {
            // An enclosing loan still counts when candidate discovery needs no storage.
            var admission = invocation.TryReserveWorking(0);
            if (admission != Utf8ExecutionLimitKind.None)
                return new(false, -1, -1, admission);
        }
        var classWindowEnd = -1;
        var followingWindowEnd = 0;
        // Enable overlapping suffix checks only after a complete proof rejects.
        var rejectedSuffix = NoBranchSuffixCheck;
        var searchStart = screenedLiteral - fromByte >= 64 && checkBranchedPrefix
            ? GetFirstPossibleBranchStart(program, input, fromByte, screenedLiteral, candidatePlan) : fromByte;
        if (existenceOnly && seekLiteral && wideInput && program.Assertions.Length == 0 &&
            candidatePlan.Kind == Utf8SearchKind.ExactAsciiLiterals && candidatePlan.LiteralSearch is null &&
            candidatePlan.AlternateLiteralsUtf8 is { Length: >= 2 and <= 8 } literals)
        {
            var breach = ScreenFirstExactFamilyFollower(program, input, literals, ref searchStart, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
            if (searchStart < 0)
                return new(false, -1, -1, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
        }
        for (var position = searchStart; position >= 0; position = AdvancePastScalar(input, position, program.RightToLeft))
        {
            // The required delimiter proves a window of possible starts.
            // Verify every survivor against the complete subject so later
            // matches, captures and assertion context keep their usual order.
            while (hasClassWindow && position > classWindowEnd)
            {
                var provedMatchEnd = -1;
                var seekBreach = seekDecimalWindow
                    ? TrySeekDecimalLiteralWindow(program, input, (byte)classLiteral,
                        ref position, ref invocation, out classWindowEnd, screenedDelimiter,
                        verifyFollowingWindow, out followingWindowEnd)
                    : TrySeekLeadingClassLiteralWindow(program, input, leadingClass,
                        (byte)classLiteral, classSearch, classGapMaximum, directClassLiteral,
                        ref position, ref invocation, out classWindowEnd, out provedMatchEnd, repeatPrefix, classAlternatives,
                        requiredClassSuffix, screenedDelimiter);
                screenedDelimiter = -1;
                if (seekBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, seekBreach);
                if (position < 0)
                    return new(false, -1, -1,
                        new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                if (provedMatchEnd >= 0 && canProveValue && captureEnds.IsEmpty &&
                    invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit)
                {
                    var admission = invocation.TryChargeInputStep();
                    invocation.Budget.ThrowIfCancellationRequested();
                    return admission == Utf8ExecutionLimitKind.None
                        ? new(true, position, provedMatchEnd, admission) : new(false, -1, -1, admission);
                }
                // A decimal repeat cannot consume this delimiter. Every start
                // in its window must reach the next literal from the same suffix.
                if (followingWindow.Min != 0 && position <= classWindowEnd)
                {
                    if (!seekDecimalWindow)
                        followingWindowEnd = verifyFollowingWindow
                            ? FindShortAsciiDecimalLiteralEnd(input, classWindowEnd + 1, followingWindow)
                            : 0;
                    if (followingWindowEnd < 0 || !verifyFollowingWindow &&
                        !MayReachShortRepeatLiteral(input, classWindowEnd + 1, followingWindow, admitsWideScalars: true))
                        position = classWindowEnd + 1;
                }
            }
            if (seekLiteral)
            {
                var seekBreach = TrySeekExactPrefix(candidatePlan, input, ref position, ref invocation, out _);
                if (seekBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, seekBreach);
                if (position < 0)
                    return new(false, -1, -1,
                        new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
            }
            if (leading >= 0 && ((uint)position >= (uint)input.Length ||
                !MayStartAtByte(input[position], leading, leadingLast, sparsePair, includeKelvin) ||
                !leadingPrefix.IsEmpty && !input[position..].StartsWith(leadingPrefix)))
            {
                var seekBreach = TrySeekLeadingByte(input, ref position, (byte)leading, leadingLast, seekDecimal,
                    ref invocation, leadingPrefix, sparsePair, includeKelvin);
                if (seekBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, seekBreach);
                if (position < 0)
                    return new(false, -1, -1,
                        new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
            }
            if (seekBeginning)
            {
                var seekBreach = TrySeekBeginning(program, input, ref position, ref invocation);
                if (seekBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, seekBreach);
                if (position < 0) break;
            }
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, -1, breach);
            invocation.Budget.ThrowIfCancellationRequested();
            if (lineProofStart != 0)
            {
                // Candidate discovery already verified the line anchor. The
                // existing bounded proof can now start at its continuation.
                if (!MayMatchBranchedAsciiPrefix(program, input, position, lineProofStart, out var lineEnd, proveMatch: true))
                    continue;
                if (lineEnd >= 0 && canProveValue && captureEnds.IsEmpty)
                {
                    breach = invocation.TryChargeInputStep();
                    invocation.Budget.ThrowIfCancellationRequested();
                    return breach == Utf8ExecutionLimitKind.None
                        ? new(true, position, lineEnd, breach) : new(false, -1, -1, breach);
                }
            }
            // Charge even rejected neighbours: dense false heads must retain
            // bounded work and the original interruption authority.
            if (leadingPrefix.IsEmpty && leading >= 0 && second >= 0 && ((uint)(position + 1) >= (uint)input.Length ||
                (uint)(input[position + 1] - second) > (uint)(secondLast - second)))
                continue;
            if (requiresEqualScalars && !HasEqualLeadingScalars(input[position..]))
                continue;
            var reuseRewoundDigits = reuseDecimalWindow && position < classWindowEnd;
            var provedAsciiBoundary = false;
            if (screenDecimalWindow)
            {
                if (position == input.Length) continue;
                if (input[position] < 128)
                {
                    // The aligned direct rewind already classified every scalar
                    // between this start and the excluded delimiter.
                    if (!(verifyFollowingWindow && reuseRewoundDigits) &&
                        !Utf8AsciiBytePredicates.IsDigit(input[position])) continue;
                    var previous = position == 0 ? (byte)0 : input[position - 1];
                    if (Utf8AsciiBytePredicates.IsWord(previous))
                    {
                        // Every interior start still has a decimal word scalar on both sides.
                        if (rewoundDigitsAreWords && reuseRewoundDigits) position = classWindowEnd;
                        continue;
                    }
                    provedAsciiBoundary = previous < 128;
                }
            }
            if (shortRepeatWindow.Min != 0 && !MayReachShortRepeatLiteral(input, position, shortRepeatWindow, seekDecimal))
                continue;
            // The metered rewind proved the nonempty leading class and its
            // aligned delimiter. Shorter endpoints cannot reach the literal.
            var verifiedPrefix = verifiedClassLiteralLength != 0 && position < classWindowEnd
                ? (classContinuation, classWindowEnd - position + verifiedClassLiteralLength)
                : verifiedLeadingPrefix;
            var provePrefix = prefixProof && verifiedPrefix.Item1 == prefixContinuation;
            var provedLookbehind = false;
            if (lookbehindMask is not null)
            {
                if (position == 0) continue;
                var previous = input[position - 1];
                if (previous < 128)
                {
                    if (!MatchesAsciiHybridClass(lookbehindMask, false, previous)) continue;
                    provedLookbehind = true;
                }
            }
            if (checkBranchedPrefix || checkStraightRepeat || shortDecimalHead || provePrefix || provedLookbehind ||
                proveDecimalWindow && input[position] < 128)
            {
                // Starts only advance in these forward searches. The
                // input step above also covers stopping after the last one.
                if (position > lastProbedStart) break;
                var proveDecimalPrefix = shortDecimalHead || proveDecimalWindow;
                // The exact search already proved the literal prefix. Verify
                // only the remaining instructions inside the same allowance.
                var proveMatch = proveDecimalPrefix || proveBranchedMatch || provePrefix;
                var proofStart = provePrefix ? verifiedPrefix.Item1 : proveMatch ? 0 : leadingScalarIndex;
                var proofPosition = provePrefix ? position + verifiedPrefix.Item2 : position;
                var proofLimit = BranchedPrefixInstructionLimit - (provePrefix ? verifiedPrefix.Item1 : 0);
                if (reuseRewoundDigits)
                {
                    // Rewind already admitted a nonempty decimal run ending at
                    // this excluded delimiter. Confirm any remaining Unicode
                    // boundary and start the proof at the continuation.
                    if (!provedAsciiBoundary && !MatchesBoundary(program, first, input, position))
                    {
                        if (rewoundDigitsAreWords) position = classWindowEnd;
                        continue;
                    }
                    proofPosition = classWindowEnd;
                    proofStart = program.Instructions[1].A + 1;
                    if (followingWindowEnd > 0)
                    {
                        proofPosition = followingWindowEnd;
                        proofStart = program.FollowingDecimalContinuation.Index;
                        proofLimit -= followingWindow.Max + 3;
                    }
                }
                if (rejectedSuffix != NoBranchSuffixCheck &&
                    RejectRequiredBranchLiteralSuffix(program, input, position, ref rejectedSuffix, ref proofLimit))
                    continue;
                var possible = provedLookbehind
                    ? MayMatchGuardedRepeat(program, input, position, out var acceptedEnd)
                    : MayMatchAdmittedCandidate(program, input, proofPosition, proofStart, out acceptedEnd,
                        emitted, ref interpretedVisits, proveMatch, proofLimit);
                if (!possible)
                {
                    // Keep the full proof allowance for a compiled candidate.
                    if (rejectedSuffix == NoBranchSuffixCheck && proveBranchedMatch && emitted is null &&
                        program.RequiredBranchLiteral.Min != 0 && input.Length - position <= 64)
                        rejectedSuffix = -1;
                    var remainingDigits = classWindowEnd - position;
                    // The leading boundary excludes interior starts in a word run.
                    if (reuseRewoundDigits && (rewoundDigitsAreWords || remainingDigits <= 4 &&
                        !input.Slice(position, remainingDigits).ContainsAnyExceptInRange((byte)'0', (byte)'9')))
                    {
                        // The next iteration resumes immediately after this
                        // delimiter. A verified following run with the same
                        // delimiter can supply its next window directly.
                        if (seekDecimalWindow && followingWindowEnd > 0 && followingWindow.Literal == classLiteral)
                            screenedDelimiter = followingWindowEnd - 1;
                        position = classWindowEnd;
                    }
                    if (provePrefix && singleWindowAttempt) position = classWindowEnd;
                    continue;
                }
                if (acceptedEnd >= 0)
                {
                    // The candidate step admitted the bounded walk. Confirm
                    // the original interruption and work before publishing it.
                    breach = invocation.TryChargeInputStep();
                    invocation.Budget.ThrowIfCancellationRequested();
                    return breach == Utf8ExecutionLimitKind.None
                        ? new(true, position, acceptedEnd, breach)
                        : new(false, -1, -1, breach);
                }
            }
            var outcome = TryMatchAt(program, input, position, captureStarts, captureEnds,
                collectHistory, false, true, ref workspace, ref invocation, existenceOnly, verifiedPrefix);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, -1, outcome.Breach);
            if (outcome.IsMatch)
                return new(true, Math.Min(position, outcome.EndIndex), Math.Max(position, outcome.EndIndex),
                    Utf8ExecutionLimitKind.None);
            if (singleWindowAttempt) position = classWindowEnd;
        }
        return new(false, -1, -1, Utf8ExecutionLimitKind.None);
    }

    // -1 is the exhausted sentinel in either direction; avoid Length + 1 overflow.
    internal static int AdvancePastScalar(ReadOnlySpan<byte> input, int position, bool reverse)
        => TryReadScalar(input, position, reverse, out _, out var width)
            ? position + (reverse ? -width : width) : -1;

    private static bool StartsWithSingleScalarBackreference(Utf8SliceProgram program)
    {
        // At instruction zero, these markers close exactly one scalar in the
        // slot used by the immediate ordinal reference. Nothing can jump to
        // the reference before the capture completes.
        var code = program.Instructions;
        return program.ReadsCaptures && !program.RightToLeft && code.Length >= 4 &&
            code[0] is { Kind: Utf8SliceInstructionKind.CaptureStart, A: var slot, B: var opening } &&
            code[1].Kind is Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set or
                Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.CategorySet or
                Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.Shorthand &&
            code[2] is { Kind: Utf8SliceInstructionKind.CaptureEnd } end && end.A == slot && end.B == opening &&
            code[3] is { Kind: Utf8SliceInstructionKind.Backref, B: 0 } reference && reference.A == slot;
    }

    private static bool HasEqualLeadingScalars(ReadOnlySpan<byte> input)
    {
        if (input.Length < 2) return false;
        if (input[0] < 128) return input[0] == input[1];
        return TryReadScalar(input, 0, false, out _, out var width) && input.Length >= 2 * width &&
            input[..width].SequenceEqual(input.Slice(width, width));
    }

    // Count and primary matching share the same necessary-prefix proof. Every
    // candidate is still verified by the scalar VM under the original invocation.
    private static bool CanSeekExactPrefix(Utf8SliceProgram program, in Utf8SearchPlan candidatePlan)
        => candidatePlan.CanGuideFallbackStarts && candidatePlan.Distance == 0 &&
            !program.RightToLeft && !program.CanMatchEmpty && !program.HasContiguousAnchor && !program.IgnoreCase &&
            candidatePlan.Kind is
                Utf8SearchKind.ExactAsciiLiteral or
                Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals;

    private static Utf8ExecutionLimitKind TrySeekExactPrefix(in Utf8SearchPlan candidatePlan,
        ReadOnlySpan<byte> input, ref int position, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match)
    {
        if (candidatePlan.LiteralSearch is { Length: > 0 } literal)
        {
            var literalBreach = Utf8SearchExecutor.TryFindLiteralCandidate(literal, input, position,
                input.Length - literal.Length, false, ref invocation, out position);
            match = position >= 0 ? new(position, literal.Length, 0) : default;
            return literalBreach;
        }
        var breach = Utf8SearchExecutor.TryFindLiteralFamilyCandidate(
            candidatePlan.AlternateLiteralsUtf8 ?? [], false, input, position, input.Length,
            false, ref invocation, out match, out var found,
            candidatePlan.AlternateLiteralSearch?.SearchData ?? default);
        position = found ? match.Index : -1;
        return breach;
    }

    // Only an initial mandatory anchor proves that other starts cannot match.
    // Callers establish LTR/backtracking eligibility. An immediate exact ASCII
    // literal can also reject a start before capture reset/workspace acquisition.
    // LF is one byte and cannot occur inside a scalar. The caller establishes
    // valid UTF-8; this filter neither restricts a match's end nor its body.
    internal static Utf8ExecutionLimitKind TrySeekBeginning(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, ref int position, ref Utf8SliceInvocation invocation,
        scoped ReadOnlySpan<byte> linePrefix = default)
    {
        var anchor = program.Instructions[0];
        var leadingLiteral = anchor.C - 1;
        if (anchor.B == 0)
        {
            if (position != 0)
            {
                position = -1;
                return Utf8ExecutionLimitKind.None;
            }
            if (leadingLiteral < 0) return Utf8ExecutionLimitKind.None;

            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            if (input.IsEmpty || input[0] != leadingLiteral) position = -1;
            return Utf8ExecutionLimitKind.None;
        }
        if (leadingLiteral >= 0)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            if (position < input.Length && (position == 0 || input[position - 1] == (byte)'\n') &&
                (linePrefix.IsEmpty ? input[position] == leadingLiteral : input[position..].StartsWith(linePrefix[1..])))
                return Utf8ExecutionLimitKind.None;

            // A Count caller can supply LF and its proved exact prefix together.
            // Retain enough overlap to discover starts across charged blocks.
            ReadOnlySpan<byte> pair = stackalloc byte[] { (byte)'\n', (byte)leadingLiteral };
            scoped ReadOnlySpan<byte> needle = linePrefix.IsEmpty ? pair : linePrefix;
            var overlap = needle.Length - 1;
            while (input.Length - position > overlap)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(2048, input.Length - position);
                var relative = input.Slice(position, length).IndexOf(needle);
                invocation.Budget.ThrowIfCancellationRequested();
                if (relative >= 0)
                { position += relative + 1; return Utf8ExecutionLimitKind.None; }
                position += length - overlap;
            }
            position = -1;
            return Utf8ExecutionLimitKind.None;
        }
        if (position == 0 || input[position - 1] == (byte)'\n') return Utf8ExecutionLimitKind.None;
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var length = Math.Min(2048, input.Length - position);
            var newline = input.Slice(position, length).IndexOf((byte)'\n');
            position += newline >= 0 ? newline + 1 : length;
            // A trailing LF also admits the final empty line.
            if (newline >= 0) return Utf8ExecutionLimitKind.None;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    // Count receives caller-valid text and needs no coordinate map:
    // attempts, successful endpoints and empty progress are all byte boundaries.
    internal static Utf8SliceCountOutcome CountFromBytes(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int fromByte,
        scoped Span<int> captureStarts,
        scoped Span<int> captureEnds,
        ref Utf8SliceInvocation invocation,
        in Utf8SearchPlan candidatePlan = default,
        in PreparedSearcher branchPrefixes = default,
        int screenedLiteral = -1)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)fromByte > (uint)input.Length ||
            fromByte < input.Length && (input[fromByte] & 0xC0) == 0x80)
            return new Utf8SliceCountOutcome(0, Utf8ExecutionLimitKind.None);
        var count = 0;
        var position = fromByte;
        invocation.AnchorOriginByte = fromByte;
        // Nullable roots retain the caller's empty-match progression below.
        if (TryScanTerminalAsciiSuffixAlternatives(program, input, fromByte, firstOnly: false,
                ref invocation, out var suffixes, out _))
            return suffixes;
        if (TryCountTerminalScalar(program, input, fromByte, ref invocation, out var terminal))
            return terminal;
        if (program.RepeatCount == 1 && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            TryCountTerminalLiteralPair(program, input[fromByte..], ref invocation, out terminal))
            return terminal;
        if (program.IsNonBacktracking)
        {
            captureStarts.Fill(-1);
            captureEnds.Fill(-1);
            if (TryCountAnchoredLine(program, input, fromByte, ref invocation, out var lineCount))
                return lineCount;
            // Wide Count uses program-sized scratch on longer subjects.
            if (captureStarts.Length == 0 && program.Instructions.Length > 64 && input.Length - fromByte >= 256)
                return CountWideNonBacktracking(program, input, fromByte, ref invocation);
            if (input.Length - fromByte >= 64)
            {
                if (program.NonBacktrackingOrderedForwardOnly)
                    return Utf8NonBacktrackingSearchState.CountInWindows(program, input, fromByte,
                        captureStarts, captureEnds, ref invocation);
                if (captureStarts.Length == 0 && program.NonBacktrackingOrderedClosures.Length != 0 &&
                    CanAdmitOrderedRows(program, ref invocation))
                    return CountOrderedRegular(program, input, fromByte, ref invocation);
            }
            var breach = Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out var batch);
            if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
            if (batch is null) throw new InvalidOperationException("NonBacktracking preparation returned no state.");
            try
            {
                var boundary = batch.ResolveByteOffset(input, fromByte);
                while (true)
                {
                    var found = batch.FindFirst(program, ref boundary, captureStarts, captureEnds, ref invocation);
                    if (found.Breach != Utf8ExecutionLimitKind.None) return new(count, found.Breach);
                    if (!found.Found) break;
                    count++;
                }
                return new(count, Utf8ExecutionLimitKind.None);
            }
            finally { batch.Release(ref invocation); }
        }
        var first = program.Instructions[0];
        var seekBeginning = !program.RightToLeft && first.Kind == Utf8SliceInstructionKind.Anchor &&
            first.A == (int)Utf8SliceAnchorKind.Beginning;
        var lineProofStart = GetAnchoredLineProofStart(seekBeginning, first, ref invocation);
        if (seekBeginning && first is { B: 1, C: > 0 } && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            TryCountAnchoredLine(program, input, fromByte, ref invocation, out var anchoredLineCount))
            return anchoredLineCount;
        var seekVeryEnd = program.RightToLeft && first.Kind == Utf8SliceInstructionKind.Anchor &&
            first.A == (int)Utf8SliceAnchorKind.VeryEnd;
        // A distance-zero exact prefix is necessary, never sufficient. The scalar
        // VM still verifies every candidate; stateful/empty/casefold starts decline.
        // PCRE2-INTEGRATION-POINT: a caller may provide its proved branch prefixes.
        var useBranchPrefixes = branchPrefixes.HasValue;
        var seekLiteral = useBranchPrefixes || CanSeekExactPrefix(program, candidatePlan);
        // A successful line seek has already checked both of these instructions.
        // A capture or other instruction before the literal keeps the VM entry.
        var verifiedLinePrefix = seekBeginning && !seekLiteral && first is { B: 1, C: > 0 } &&
            program.Instructions[1] is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } lineHead &&
            lineHead.A == first.C - 1 ? (2, 1) : default;
        var prefixContinuation = 0;
        scoped Span<int> familyContinuations = default;
        if (seekLiteral && !useBranchPrefixes && input.Length - fromByte >= 256 &&
            program.Assertions.Length == 0)
        {
            if (candidatePlan.LiteralSearch is { IgnoreCase: false, Length: > 0 and <= 64 } literal)
            {
                var prefixStart = first.Kind == Utf8SliceInstructionKind.Boundary ? 1 : 0;
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
                if (LiteralStartsAtInstruction(program, prefixStart, literal.Needle))
                    prefixContinuation = prefixStart + literal.Length;
            }
            else if (candidatePlan.Kind == Utf8SearchKind.ExactAsciiLiterals &&
                candidatePlan.LiteralSearch is null && candidatePlan.AlternateLiteralsUtf8 is { Length: >= 2 and <= 8 } literals)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
                familyContinuations = stackalloc int[literals.Length];
                if (!TryFindExactFamilyContinuations(program.Instructions, literals, familyContinuations, out prefixContinuation))
                    familyContinuations = default;
                else if (prefixContinuation != 0)
                {
                    breach = ScreenExactFamilyFollower(program, input, literals, prefixContinuation,
                        ref position, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
                    if (position < 0) return new(0, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                }
            }
        }
        var splitFamilyTails = prefixContinuation == 0 && !familyContinuations.IsEmpty;
        // A direct beginning anchor already owns bounded line/prefix discovery.
        // Keep one candidate search rather than scanning its prefix twice.
        var (leadingFirst, leadingLast, leadingSecond, leadingSecondLast, leadingScalarIndex, _, sparsePair) = seekLiteral
            ? (-1, (byte)0, -1, (byte)0, -1, false, false)
            : program.LeadingByteCandidate;
        var includeKelvin = leadingFirst == 'K' && program.LeadingByteAdmitsKelvin;
        var checkBranchedPrefix = leadingFirst is >= 0 and < 128 && program.Instructions[leadingScalarIndex] is
            { Kind: Utf8SliceInstructionKind.Fork } or { IsLiteralChoice: true };
        if (screenedLiteral - fromByte >= 64 && checkBranchedPrefix)
            position = GetFirstPossibleBranchStart(program, input, fromByte, screenedLiteral, candidatePlan);
        if (checkBranchedPrefix && input.Length - fromByte >= 256 && program.RequiredUnanchoredFinalByte is { } finalByte)
        {
            // One bounded required-byte search can reject a whole Count before
            // trying every prefix. A hit leaves the original subject and VM order intact.
            var suffix = fromByte;
            var breach = TrySeekAsciiCandidate(input, ref suffix, finalByte, finalByte, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
            if (suffix < 0) return new(0, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
        }
        var branchWindow = checkBranchedPrefix && input.Length - fromByte >= 256
            ? FindBranchedAsciiWindow(program, leadingScalarIndex) : default;
        var checkRepeatChain = leadingFirst is >= 0 and < 128 && leadingScalarIndex == 1 && CanSkipShortRepeatChain(program);
        var (whitespaceLiteral, leadingClass, whitespaceLiteralIndex, whitespaceGapMaximum, directClassLiteral) =
            !seekLiteral && !program.RightToLeft && !program.IgnoreCase &&
            !program.HasContiguousAnchor && (input.Length - fromByte >= 256 ||
                program.LeadingClassWindowProof.CanFinishWhitespace &&
                invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit)
            ? program.LeadingClassDelimiter : (-1, -1, -1, 0L, false);
        // Count keeps its prepared literal search when capture markers or
        // consuming instructions separate the repeat from its delimiter.
        var literalImmediatelyAfterRepeat = whitespaceLiteral >= 0 && whitespaceLiteralIndex == leadingClass + 3;
        var classSearch = default(PreparedSubstringSearch?);
        (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd, bool Terminal) repeatPrefix = default;
        var singleWindowAttempt = false;
        var verifiedClassLiteralLength = 0;
        var requiredClassSuffix = default((int First, int Second));
        if (whitespaceLiteral >= 0 && leadingClass >= 0)
        {
            classSearch = GetAlignedDelimiterSearch(program, whitespaceLiteralIndex, whitespaceLiteral, candidatePlan);
            if (!literalImmediatelyAfterRepeat && classSearch is null) whitespaceLiteral = -1;
            if (directClassLiteral && classSearch is { } aligned)
            {
                var proofBreach = TryProveSingleWindowAttempt(program, leadingClass, (byte)whitespaceLiteral,
                    ref invocation, out singleWindowAttempt);
                if (proofBreach != Utf8ExecutionLimitKind.None) return new(count, proofBreach);
                if (singleWindowAttempt)
                {
                    var proof = program.LeadingClassWindowProof;
                    requiredClassSuffix = proof.SuffixAfter(aligned.Length);
                    if (proof.CanReusePrefix && captureStarts.IsEmpty && captureEnds.IsEmpty)
                        verifiedClassLiteralLength = aligned.Length;
                }
            }
        }
        if (whitespaceLiteral < 0 && leadingFirst is < 0 or >= 128 && !seekLiteral && !program.RightToLeft &&
            !program.IgnoreCase && !program.HasContiguousAnchor && input.Length - fromByte >= 256 &&
            GetRepeatPrefixSearch(program, candidatePlan, out repeatPrefix) is { } prefixSearch)
        {
            classSearch = prefixSearch;
            whitespaceLiteral = prefixSearch.Needle[0];
        }
        if (program.FollowingDecimalContinuation.Aligned && literalImmediatelyAfterRepeat && leadingFirst < 0 &&
            (classSearch is null or { Length: 1 }) && verifiedClassLiteralLength == 0 &&
            captureStarts.IsEmpty && captureEnds.IsEmpty)
            return CountAlignedDecimalWindows(program, input, position, (byte)whitespaceLiteral,
                whitespaceLiteralIndex, ref invocation);
        var classContinuation = verifiedClassLiteralLength != 0 ? whitespaceLiteralIndex + verifiedClassLiteralLength : 0;
        var repeatIndex = classContinuation != 0 && program.GroupCount != 0
            ? Utf8SliceInstruction.SkipLeadingCaptureMarkers(program.Instructions, classContinuation) : classContinuation;
        var proveClassSuffix = classContinuation != 0 &&
            invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit &&
            CanProveRepeatPrefix(program, repeatIndex);
        // Keep the existing literal and delimiter searches. A required decimal
        // starter can also skip unrelated bytes on a long forward search.
        if (whitespaceLiteral >= 0 && leadingFirst >= 128) leadingFirst = -1;
        var shortRepeatWindow = whitespaceLiteral < 0 && leadingScalarIndex > 0 && leadingFirst < 128
            ? program.LeadingRepeatLiteralWindow : default;
        var seekDecimal = leadingFirst < 0 && program.LeadingScalarIsDecimal &&
            (shortRepeatWindow.Min != 0 || input.Length - fromByte >= 256 && whitespaceLiteral < 0 &&
                !seekLiteral && !seekBeginning && !program.RightToLeft && !program.HasContiguousAnchor && !program.CanMatchEmpty);
        var proveDecimalMatch = shortRepeatWindow.Min != 0 && seekDecimal && !program.RightToLeft && leadingScalarIndex == 2 &&
            first is { Kind: Utf8SliceInstructionKind.Boundary, A: 1 } &&
            captureStarts.Length == 0 && !program.ReadsCaptures && program.Instructions.Length <= 32;
        if (seekDecimal)
        {
            leadingFirst = '0';
            leadingLast = (byte)'9';
        }
        Span<byte> prefixStorage = stackalloc byte[17];
        var leadingPrefix = input.Length - fromByte >= 256 && whitespaceLiteral < 0 && leadingSecond >= 0
            ? GetExactLeadingLiteral(program, leadingScalarIndex, prefixStorage[..16])
            : default;
        scoped ReadOnlySpan<byte> linePrefix = default;
        if (leadingPrefix.IsEmpty && verifiedLinePrefix.Item1 != 0 && input.Length - fromByte >= 256)
        {
            var literal = GetExactLeadingLiteral(program, 1, prefixStorage[1..]);
            if (!literal.IsEmpty)
            {
                prefixStorage[0] = (byte)'\n';
                linePrefix = prefixStorage[..(literal.Length + 1)];
                verifiedLinePrefix = (1 + literal.Length, literal.Length);
            }
        }
        byte tailFirst = 0, tailLast = 0;
        var vectorBranchWindow = Vector512.IsHardwareAccelerated &&
            !program.RightToLeft && !includeKelvin && !seekDecimal && leadingFirst is >= 0 and < 128 &&
            (leadingFirst == leadingLast || sparsePair) && leadingPrefix.IsEmpty && shortRepeatWindow.Min == 0 &&
            branchWindow.Min > 0 && branchWindow.Max > branchWindow.Min &&
            TryGetAsciiBytePair(branchWindow.Bytes, out tailFirst, out tailLast);
        var reuseLeadingPrefix = !leadingPrefix.IsEmpty && !program.HasContiguousAnchor &&
            (leadingScalarIndex == 0 || leadingScalarIndex == 1 && first.Kind == Utf8SliceInstructionKind.Boundary);
        if (reuseLeadingPrefix) prefixContinuation = leadingScalarIndex + leadingPrefix.Length;
        // Keep a leading boundary in the original verification order.
        var straightPrefixStart = reuseLeadingPrefix && leadingScalarIndex == 0 ? prefixContinuation : 0;
        var straightPrefixBytes = straightPrefixStart != 0 ? leadingPrefix.Length : 0;
        var proveStraightSuffix = straightPrefixStart != 0 && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            !program.ReadsCaptures && invocation.Budget.Limits.MaxDepth is 0 or >= BranchedPrefixInstructionLimit &&
            CanProveOptionalLiteralThenHybrid(program.Instructions, straightPrefixStart);
        var terminalGuard = default(Utf8SliceInstruction);
        var terminalAssertion = prefixContinuation != 0 && !program.RightToLeft &&
            captureStarts.IsEmpty && captureEnds.IsEmpty &&
            TryGetTerminalInlineRepeatAssertion(program, prefixContinuation, out terminalGuard);
        var (prefixRepeatAscii, prefixRepeatFollower, prefixRepeatMinimum) = GetExactPrefixRepeatHead(program, prefixContinuation);
        var terminalRepeat = 0;
        var terminalWordMinimum = -1;
        var terminalPrefixAccept = 0;
        if (prefixRepeatAscii.HasValue && captureStarts.IsEmpty && captureEnds.IsEmpty && !program.ReadsCaptures)
        {
            var code = program.Instructions;
            var index = prefixContinuation + 5;
            if (code.Length - index == 1 && code[index].Kind == Utf8SliceInstructionKind.Accept &&
                program.Assertions.Length == 0 && !program.HasBalancing)
                terminalPrefixAccept = index;
            else if (code.Length - index == 5 && IsSingleClassRepeat(code, index) &&
                code[index + 1] is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } &&
                code[^1].Kind == Utf8SliceInstructionKind.Accept)
            {
                // The complete tail needs no retry frame or rented state.
                // Its scan still uses the original invocation and working ledger.
                var admission = invocation.TryReserveWorking(0);
                if (admission != Utf8ExecutionLimitKind.None) return new(count, admission);
                terminalRepeat = index;
                if (code[index] is { RepeatMinimum: 0 or 1, RepeatMaximum: long.MaxValue } repeat &&
                    IsAsciiWordMask(program.SetMasks[code[index + 1].A]))
                    terminalWordMinimum = (int)repeat.RepeatMinimum;
            }
        }
        var remaining = program.RightToLeft ? fromByte : input.Length - fromByte;
        if (CanSearchOrdinaryOrdered(program, remaining, candidatePlan, ref invocation) &&
            captureStarts.Length == 0 && !seekLiteral && !seekBeginning && !seekVeryEnd &&
            whitespaceLiteral < 0 && branchWindow.Min == 0 && leadingPrefix.IsEmpty &&
            CanAdmitOrderedRows(program, ref invocation))
            return CountOrderedRegular(program, input, fromByte, ref invocation);

        // When Count has no stronger start search, reject an impossible ASCII
        // boundary before resetting the VM. Profile predicates and wider
        // neighbors keep the complete scalar check inside the VM.
        var checkAsciiBoundary = first is { Kind: Utf8SliceInstructionKind.Boundary, C: 0 } &&
            !program.RightToLeft && !program.CanMatchEmpty && !seekLiteral &&
            leadingFirst < 0 && whitespaceLiteral < 0 && input.Length - fromByte >= 256;
        var requiresEqualScalars = StartsWithSingleScalarBackreference(program);
        var repeatMinimum = 0;
        var repeatAscii = default(Utf8SearchAsciiSet);
        if (checkAsciiBoundary)
        {
            var admission = invocation.TryReserveWorking(0);
            if (admission != Utf8ExecutionLimitKind.None) return new(count, admission);
            var code = program.Instructions.AsSpan();
            if (code[1] is { Kind: Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy,
                    RepeatMinimum: >= 2 and <= 16 } repeat &&
                IsSingleAtomRepeat(code, 1, repeat) &&
                program.TryGetUnfoldedAsciiSet(code[2], out repeatAscii, out _))
                repeatMinimum = (int)repeat.RepeatMinimum;
        }
        var whitespaceWindowEnd = -1;
        // Only a proof from the actual candidate can supply a whole match.
        // A delimiter window can still move that start before verification.
        var proveBranchedMatch = checkBranchedPrefix && captureStarts.Length == 0 &&
            !program.ReadsCaptures && whitespaceLiteral < 0 &&
            (leadingScalarIndex == 0 ||
             Utf8SliceInstruction.SkipLeadingCaptureMarkers(program.Instructions, 0) == leadingScalarIndex);
        var shortDecimalHead = seekDecimal && program.HasShortDecimalHead && captureStarts.IsEmpty && captureEnds.IsEmpty &&
            !program.ReadsCaptures && first.Kind != Utf8SliceInstructionKind.Anchor;
        var emitted = GetCandidateProof(program, proveBranchedMatch || proveDecimalMatch || shortDecimalHead,
            ref invocation, out var interpretedVisits);
        if (shortDecimalHead && emitted is not null && input.Length - fromByte >= 256 &&
            invocation.CheckpointHook is null && invocation.IsUnmetered &&
            TryGetFixedDecimalFollower(program, out var digits, out var firstFollower, out var lastFollower))
            return CountFromFixedDecimalFollowers(input, fromByte, digits, firstFollower, lastFollower, emitted, ref invocation);
        // The prepared decimal starter establishes the original candidate.
        // Keep the existing proof routes for controlled invocations.
        proveDecimalMatch |= shortDecimalHead && (interpretedVisits >= 0 || emitted is not null &&
            invocation.CheckpointHook is null && invocation.IsUnmetered);
        var rejectedSuffix = NoBranchSuffixCheck;
        var workspace = new AttemptWorkspace();
        try
        {
            while (true)
            {
                var provedEnd = -1;
                if (leadingFirst >= 0)
                {
                    var seekBreach = vectorBranchWindow
                        ? TrySeekAsciiBranchWindow(input, ref position, (byte)leadingFirst, leadingLast,
                            branchWindow.Min, branchWindow.Max, tailFirst, tailLast, ref invocation)
                        : TrySeekLeadingByte(input, ref position, (byte)leadingFirst, leadingLast, seekDecimal,
                            ref invocation, leadingPrefix, sparsePair, includeKelvin);
                    if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
                    if (position < 0)
                        return CompleteCandidateCount(program, count, interpretedVisits,
                            new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                    if (leadingFirst < 128 && (shortRepeatWindow.Min != 0 ? !MayReachShortRepeatLiteral(input, position, shortRepeatWindow, seekDecimal) :
                        (leadingPrefix.IsEmpty && leadingSecond >= 0 && ((uint)(position + 1) >= (uint)input.Length ||
                        (uint)(input[position + 1] - leadingSecond) > (uint)(leadingSecondLast - leadingSecond))) ||
                        (branchWindow.Min != 0 && !HasAsciiByteInWindow(input, position, branchWindow)) ||
                        !checkRepeatChain && !(checkBranchedPrefix ? MayMatchCountBranch(program, input, position,
                            proveBranchedMatch ? 0 : leadingScalarIndex, proveBranchedMatch, ref rejectedSuffix, out provedEnd,
                            emitted, ref interpretedVisits) :
                            MatchesStraightAsciiPrefix(program, input, position + straightPrefixBytes,
                                straightPrefixStart, 64 - straightPrefixStart))))
                    {
                        // The delimiter window retains wider decimals. Each
                        // rejection here is at an admitted ASCII scalar.
                        position++;
                        continue;
                    }
                    if (checkRepeatChain)
                    {
                        var chainBreach = CheckRepeatChain(program, input, (byte)leadingFirst, leadingLast,
                            ref position, ref invocation, out var possible);
                        if (chainBreach != Utf8ExecutionLimitKind.None) return new(count, chainBreach);
                        if (!possible) continue;
                    }
                }
                while (whitespaceLiteral >= 0 && position > whitespaceWindowEnd)
                {
                    if (leadingClass >= 0 || repeatPrefix.Any.HasValue)
                    {
                        var seekBreach = TrySeekLeadingClassLiteralWindow(program, input, leadingClass,
                            (byte)whitespaceLiteral, classSearch, whitespaceGapMaximum, directClassLiteral,
                            ref position, ref invocation, out whitespaceWindowEnd, out var windowMatchEnd, repeatPrefix, requiredSuffix: requiredClassSuffix,
                            useNearestViableStart: verifiedClassLiteralLength != 0);
                        if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
                        if (windowMatchEnd >= 0 && program.LeadingClassWindowProof.CanFinishWhitespace)
                            provedEnd = windowMatchEnd;
                    }
                    else
                    {
                        var seekBreach = TrySeekLeadingWhitespaceLiteral(program, input, ref position,
                            whitespaceLiteralIndex, ref invocation, out whitespaceWindowEnd);
                        if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
                    }
                    if (position < 0)
                    {
                        return new(count, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                    }
                }
                // A mandatory initial strict-end anchor admits only EOF, even
                // after an empty match. Keep original terminal authority when
                // an offset or the preceding match has moved below that start.
                if (seekVeryEnd && position != input.Length)
                    return new(count, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                var prefixBytes = leadingPrefix.Length;
                var candidateContinuation = prefixContinuation;
                if (seekLiteral)
                {
                    Utf8ExecutionLimitKind seekBreach;
                    if (useBranchPrefixes)
                    {
                        seekBreach = branchPrefixes.TryFindMatch(input, position, input.Length, false,
                            ref invocation, out var candidate, out var found);
                        position = found ? candidate.Index : -1;
                    }
                    else
                    {
                        seekBreach = TrySeekExactPrefix(candidatePlan, input, ref position, ref invocation, out var match);
                        prefixBytes = match.Length;
                        if ((uint)match.LiteralId < (uint)familyContinuations.Length)
                            candidateContinuation = familyContinuations[match.LiteralId];
                    }
                    if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
                    // Keep the final charge and recheck the original token after
                    // its diagnostic hook before publishing any accumulated count.
                    if (position < 0)
                        return new(count, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                }
                if (seekBeginning)
                {
                    var seekBreach = TrySeekBeginning(program, input, ref position, ref invocation, linePrefix);
                    if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
                    if (position < 0) break;
                }
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None)
                    return new Utf8SliceCountOutcome(count, breach);
                invocation.Budget.ThrowIfCancellationRequested();
                if (lineProofStart != 0)
                {
                    if (!MayMatchBranchedAsciiPrefix(program, input, position, lineProofStart, out var lineEnd, proveMatch: true))
                    {
                        position = AdvancePastScalar(input, position, reverse: false);
                        if (position < 0) break;
                        continue;
                    }
                    if (lineEnd >= 0 && captureStarts.IsEmpty && captureEnds.IsEmpty)
                        provedEnd = lineEnd;
                }
                if (requiresEqualScalars && !HasEqualLeadingScalars(input[position..]))
                {
                    position = AdvancePastScalar(input, position, false);
                    if (position < 0) break;
                    continue;
                }
                if (checkAsciiBoundary)
                {
                    if (Utf8ScalarWordBoundary.TryGetAsciiBoundary(input, position, out var boundary) &&
                        boundary != (first.A != 0))
                    {
                        if (position == input.Length) break;
                        position++;
                        continue;
                    }
                    if (repeatMinimum != 0)
                    {
                        if (input.Length - position < repeatMinimum) break;
                        var rejected = CountStartsBelowRepeatMinimum(input.Slice(position, repeatMinimum), repeatAscii);
                        if (rejected != 0)
                        {
                            position += rejected;
                            continue;
                        }
                    }
                }
                if (candidateContinuation != 0 && first.Kind == Utf8SliceInstructionKind.Boundary &&
                    !MatchesBoundary(program, first, input, position))
                {
                    // The verified literal begins with a complete ASCII scalar.
                    position++;
                    continue;
                }
                var verifiedPrefix = candidateContinuation == 0 ? verifiedLinePrefix : (candidateContinuation, prefixBytes);
                // The rewind proved this nonempty class run up to the
                // excluded delimiter. Continue with the original suffix.
                if (classContinuation != 0 && position < whitespaceWindowEnd)
                    verifiedPrefix = (classContinuation, whitespaceWindowEnd - position + verifiedClassLiteralLength);
                if (prefixRepeatAscii.HasValue)
                {
                    var extra = ScanExactPrefixRepeatHead(input[(position + prefixBytes)..],
                        prefixRepeatAscii, prefixRepeatFollower, prefixRepeatMinimum);
                    if (extra < 0)
                    {
                        position++;
                        continue;
                    }
                    if (extra != 0)
                        verifiedPrefix = (prefixContinuation + 5, prefixBytes + extra);
                }
                var dotTailChecked = false;
                if (splitFamilyTails && candidateContinuation != 0)
                {
                    var code = program.Instructions;
                    var repeat = code[candidateContinuation];
                    if (repeat is { Kind: Utf8SliceInstructionKind.RepeatGreedy, HasSingleAtomBody: true } &&
                        code[repeat.A - 1].D is > 0 and var packedTail &&
                        !code[repeat.A].RepeatExitRejectsShorterEndpoints &&
                        !program.SkipsTerminalRewind(candidateContinuation))
                    {
                        // Screen the selected tail before starting an attempt.
                        if (!MayMatchPreparedDotTail(program, input, position + prefixBytes, packedTail,
                            (int)repeat.RepeatMaximum, code[candidateContinuation + 1].A != 0,
                            ref invocation, out var probeBreach))
                        {
                            if (probeBreach != Utf8ExecutionLimitKind.None) return new(count, probeBreach);
                            position++;
                            continue;
                        }
                        dotTailChecked = true;
                    }
                }
                // The bounded proof can count a complete ordered endpoint.
                // An undecided result still enters the scalar matcher.
                if (proveDecimalMatch &&
                    !MayMatchAdmittedCandidate(program, input, position, 0, out provedEnd, emitted,
                        ref interpretedVisits, proveMatch: true))
                {
                    position = AdvancePastScalar(input, position, reverse: false);
                    continue;
                }
                Utf8SliceMatchOutcome outcome;
                var proveSuffix = proveClassSuffix && position < whitespaceWindowEnd && verifiedPrefix.Item1 == classContinuation ||
                    proveStraightSuffix && verifiedPrefix.Item1 == straightPrefixStart;
                if (proveSuffix && provedEnd < 0 &&
                    !MayMatchBranchedAsciiPrefix(program, input, position + verifiedPrefix.Item2, verifiedPrefix.Item1,
                        out provedEnd, proveMatch: true, BranchedPrefixInstructionLimit - verifiedPrefix.Item1))
                {
                    // A class window shares the rejected suffix across its starts.
                    outcome = new(false, -1, Utf8ExecutionLimitKind.None);
                }
                else if (provedEnd >= 0)
                    outcome = new(true, provedEnd, invocation.TryReserveWorking(0));
                else if (terminalPrefixAccept != 0 && verifiedPrefix.Item1 == terminalPrefixAccept && workspace.Initialized)
                {
                    // The first attempt admits the workspace. Accept reads no
                    // attempt state, so later proved ranges only debit that step.
                    var acceptance = invocation.TryChargeStep();
                    outcome = acceptance == Utf8ExecutionLimitKind.None
                        ? new(true, position + verifiedPrefix.Item2, acceptance) : new(false, -1, acceptance);
                }
                else if (terminalRepeat != 0 && verifiedPrefix.Item1 == terminalRepeat)
                {
                    // An admitted prefix has reached a terminal repeat with no
                    // further choice. Reuse the same scan without VM workspace.
                    var start = position + verifiedPrefix.Item2;
                    var tail = terminalWordMinimum >= 0
                        ? RunUnboundedAsciiWord(input, start, terminalWordMinimum != 0, ref invocation)
                        : RunClassRepeat(program, terminalRepeat, input, start, ref invocation);
                    outcome = new(tail.EndIndex >= 0, tail.EndIndex, tail.Breach);
                }
                else if (terminalAssertion && verifiedPrefix.Item1 == prefixContinuation && verifiedPrefix.Item2 > 0)
                {
                    // The search established the prefix. The terminal assertion
                    // has no mutable attempt state, but admission still comes first.
                    if (!workspace.Initialized)
                    {
                        var admission = workspace.Initialize(program, ref invocation);
                        if (admission != Utf8ExecutionLimitKind.None) return new(count, admission);
                    }
                    outcome = MatchTerminalInlineRepeatAssertion(program, prefixContinuation + 1, terminalGuard,
                        input, position + verifiedPrefix.Item2, ref invocation);
                }
                else
                    outcome = TryMatchAt(program, input, position, captureStarts, captureEnds, false, false, true,
                        ref workspace, ref invocation, verifiedPrefix: verifiedPrefix, dotTailChecked: dotTailChecked);
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                    return new Utf8SliceCountOutcome(count, outcome.Breach);
                invocation.Budget.ThrowIfCancellationRequested();
                if (outcome.IsMatch)
                {
                    count++;
                    rejectedSuffix = NoBranchSuffixCheck;
                    invocation.AnchorOriginByte = outcome.EndIndex;
                    if (outcome.EndIndex != position)
                    {
                        position = outcome.EndIndex;
                        continue;
                    }
                }
                // Presence in one alternative does not anchor other branches.
                // Only a mandatory initial \G proves later starts impossible.
                else if (program.RightToLeft && first.Kind == Utf8SliceInstructionKind.Anchor &&
                    first.A == (int)Utf8SliceAnchorKind.Contiguous)
                    break;
                if (!outcome.IsMatch && singleWindowAttempt) position = whitespaceWindowEnd;
                position = AdvancePastScalar(input, position, program.RightToLeft);
                if (position < 0) break;
            }
            return CompleteCandidateCount(program, count, interpretedVisits, Utf8ExecutionLimitKind.None);
        }

        finally { workspace.Release(ref invocation); }
    }

    private static Utf8SliceMatchOutcome RunSegment(
        ReadOnlySpan<byte> input, scoped Span<int> captureStarts, scoped Span<int> captureEnds, int[] balanceHeads,
        ref AssertionCursor cursor, bool resumeFailure, bool preserveCaptureTrail,
        ref ChunkedStacks stacks, ref Utf8SliceInvocation invocation, out bool suspended, bool existenceOnly = false,
        int checkedDotTailAt = -1, bool preserveClosingOrder = false)
    {
        suspended = false;
        var program = cursor.Program;
        var start = cursor.Start;
        var counts = cursor.Counts;
        var positions = cursor.Positions;
        var requireNonEmpty = cursor.RequireNonEmpty;
        var startIsScalarBoundary = cursor.Aligned;
        var frameBase = cursor.FrameBase;
        var instructions = program.Instructions;
        var programIndex = cursor.ProgramIndex;
        var inputIndex = cursor.InputIndex;
        var checkedInlineAssertion = false;
        Utf8ExecutionLimitKind failure;
        Utf8SliceMatchOutcome outcome;
        while (true)
        {
            var failed = resumeFailure;
            if (resumeFailure)
            {
                resumeFailure = false;
                goto Backtrack;
            }
            var stepBreach = invocation.TryChargeStep();
            if (stepBreach != Utf8ExecutionLimitKind.None)
            {
                failure = stepBreach;
                goto Failure;
            }

            var instruction = instructions[programIndex];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Literal:
                    // Adjacent exact ASCII literals share this checked byte loop.
                    // Each keeps its original instruction charge; effects and
                    // wider/folded/mirrored atoms return to normal dispatch.
                    if (!program.RightToLeft && instruction.B == 0 && (uint)instruction.A < 128)
                    {
                        while (true)
                        {
                            if ((uint)inputIndex >= (uint)input.Length || input[inputIndex] != instruction.A)
                            { failed = true; break; }
                            inputIndex++;
                            instruction = instructions[++programIndex];
                            if (instruction.Kind != Utf8SliceInstructionKind.Literal || instruction.B != 0 ||
                                (uint)instruction.A >= 128) break;
                            var literalBreach = invocation.TryChargeStep();
                            if (literalBreach != Utf8ExecutionLimitKind.None)
                                { failure = literalBreach; goto Failure; }
                        }
                        break;
                    }
                    // Literal atoms consume one complete scalar in either direction.
                    var literalIndex = inputIndex;
                    var literalWidth = 0;
                    if (program.RightToLeft)
                    {
                        if (TryGetScalarWidthEndingAt(input, inputIndex, out literalWidth))
                            literalIndex -= literalWidth;
                    }
                    else if (inputIndex < input.Length && (input[inputIndex] & 0xC0) != 0x80)
                    {
                        var lead = input[inputIndex];
                        literalWidth = lead < 0x80 ? 1 : lead < 0xE0 ? 2 : lead < 0xF0 ? 3 : 4;
                    }

                    if (literalWidth == 0)
                    {
                        failed = true;
                        break;
                    }

                    var literalLead = input[literalIndex];
                    bool literalMatches;
                    if (literalLead < 0x80 && instruction.A < 0x80)
                    {
                        // Retain the byte-only ASCII fast path.
                        literalMatches = instruction.B == 0
                            ? literalLead == instruction.A
                            : program.CaseBehavior == Utf8ScalarCaseBehavior.Turkic &&
                                ((literalLead | 0x20) == 'i' || (instruction.A | 0x20) == 'i')
                                ? literalLead == instruction.A
                            : Internal.Search.AsciiSearch.FoldCase(literalLead) ==
                                Internal.Search.AsciiSearch.FoldCase((byte)instruction.A);
                    }
                    else
                    {
                        if (Rune.DecodeFromUtf8(input.Slice(literalIndex), out var literalScalar, out _) != OperationStatus.Done)
                        {
                            failed = true;
                            break;
                        }

                        literalMatches = literalScalar.Value == instruction.A;
                        if (!literalMatches && instruction.B != 0)
                        {
                            literalMatches = Rune.IsValid(instruction.A) &&
                                Utf8SliceCaseEquivalences.ScalarsEquivalent(literalScalar, new Rune(instruction.A), program.CaseBehavior);
                        }
                    }

                    if (literalMatches)
                    {
                        inputIndex += program.RightToLeft ? -literalWidth : literalWidth;
                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }
                    break;
                case Utf8SliceInstructionKind.Set:
                {
                    var reverse = program.RightToLeft;
                    var offset = reverse ? inputIndex - 1 : inputIndex;
                    if ((uint)offset >= (uint)input.Length) { failed = true; break; }

                    var value = input[offset];
                    var width = 1;
                    bool matches;
                    if (instruction.D != 0)
                    {
                        // This operand describes the complete positive ASCII interval.
                        matches = instruction.MatchesAsciiInterval(value);
                    }
                    else if (value < 128)
                    {
                        // Set masks already include the ASCII case equivalents.
                        var mask = program.SetMasks[instruction.A];
                        matches = ((mask[value >> 3] & (1 << (value & 7))) != 0) != (instruction.B != 0);
                    }
                    else
                    {
                        matches = TryReadScalar(input, inputIndex, reverse, out var scalar, out width) &&
                            Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[instruction.A], instruction.C != 0, program.CaseBehavior) != (instruction.B != 0);
                    }
                    if (matches)
                    {
                        inputIndex += reverse ? -width : width;
                        programIndex++;
                    }
                    else failed = true;
                    break;
                }

                case Utf8SliceInstructionKind.Fork:
                    if (instruction is { B: 1, C: not 0 })
                    {
                        // Both forward-lookahead layouts end their body two
                        // instructions before the failed-body target.
                        checkedInlineAssertion = true;
                        var depth = (uint)stacks.FrameCount + 1;
                        bool matched;
                        var assertionBreach = instruction.C switch
                        {
                            Utf8SliceInstruction.InlineClassAssertion => TryMatchInlineClassAssertion(program,
                                instructions[programIndex + 1], input, inputIndex, false, null, depth,
                                ref invocation, out matched, checkExitDeadline: false),
                            Utf8SliceInstruction.InlineRepeatAssertion => TryMatchInlineRepeatAssertion(program,
                                programIndex + 1, instruction.A - 2, input, inputIndex, depth, ref invocation, out matched),
                            _ => TryMatchInlineLiteralAssertion(instructions, programIndex + 1, instruction.A - 2,
                                instruction.C - 1, input, inputIndex, false, null, depth, ref invocation,
                                out matched, checkExitDeadline: false),
                        };
                        if (assertionBreach != Utf8ExecutionLimitKind.None)
                            { failure = assertionBreach; goto Failure; }
                        failed = matched != (instruction.D != 0);
                        if (!failed) programIndex = instruction.A + instruction.D;
                        break;
                    }
                    // The fork step covers a bounded necessary-prefix check
                    // for each arm. Capture markers on a disproved arm would
                    // be rolled back, including their balance heads.
                    var verifiedPrefix = 0;
                    var saveAlternative = true;
                    if (instruction.B == 0 && !program.RightToLeft)
                    {
                        if (existenceOnly && instruction.A == instructions.Length - 1 &&
                            instructions[instruction.A].Kind == Utf8SliceInstructionKind.Accept &&
                            instructions[programIndex + 1] is { Kind: Utf8SliceInstructionKind.Jump, A: var loopStart } &&
                            (uint)loopStart < (uint)programIndex)
                        {
                            // An already accepted iteration needs no further
                            // work when the caller only asks whether it matched.
                            programIndex = instruction.A;
                            break;
                        }
                        if ((instruction.C | instruction.D) != 0)
                        {
                            var remaining = input.Length - inputIndex;
                            if (instruction.C < 0)
                            {
                                // Only the alternate starts with this byte.
                                // A captured fallthrough requires its complete prefix first.
                                var takeTarget = instruction.D == 0
                                    ? remaining > 0 && input[inputIndex] == ~instruction.C
                                    : remaining < 2 || BinaryPrimitives.ReadUInt16LittleEndian(input[inputIndex..]) != instruction.D - 1;
                                programIndex = takeTarget ? instruction.A : programIndex + 1;
                                break;
                            }
                            var excludesTarget = instruction.D > 0 && remaining >= instruction.D;
                            if (instruction.C > 0 && remaining >= instruction.C)
                            {
                                failed = excludesTarget;
                                if (!failed) programIndex = instruction.A;
                                break;
                            }
                            if (excludesTarget)
                            {
                                programIndex++;
                                break;
                            }
                        }
                        var allowAcceptProof = !cursor.RequiredEnd.HasValue;
                        verifiedPrefix = GetForkPrefixLength(program, input, inputIndex, programIndex + 1,
                            allowAcceptProof, out var provesAccept,
                            allowEmptyAccept: allowAcceptProof && (!requireNonEmpty || inputIndex != start));
                        if (verifiedPrefix < 0)
                        {
                            programIndex = instruction.A;
                            break;
                        }
                        // A consumed prefix followed by an unconditional path
                        // to Accept cannot need the other arm. Still execute the
                        // preferred tail for its greedy extent and captures.
                        saveAlternative = !provesAccept && (verifiedPrefix == 0 || !allowAcceptProof ||
                            !CanAcceptEmptyTail(instructions, programIndex + 1 + verifiedPrefix)) &&
                            GetForkPrefixLength(program, input, inputIndex, instruction.A, false, out _) >= 0;
                    }

                    if (saveAlternative && !stacks.PushFrame(
                        new Utf8SliceFrame
                        {
                            ResumeKind = instruction.B == 0 ? Utf8SliceResumeKind.Alternation : instruction.B == 1 ? Utf8SliceResumeKind.Assertion : Utf8SliceResumeKind.Atomic,
                            Target = instruction.A,
                            InputIndex = inputIndex,
                            TrailCount = stacks.TrailCount,
                            JournalCount = stacks.JournalCount
                        },
                        ref invocation))
                    {
                        failure = stacks.Breach;
                        goto Failure;
                    }

                    // The saved retry retains the alternative's original location.
                    // Charge each verified instruction before advancing over it.
                    for (var consumed = 0; consumed < verifiedPrefix; consumed++)
                    {
                        var prefixBreach = invocation.TryChargeStep();
                        if (prefixBreach != Utf8ExecutionLimitKind.None)
                        { failure = prefixBreach; goto Failure; }
                    }
                    inputIndex += verifiedPrefix;
                    programIndex += 1 + verifiedPrefix;
                    break;
                case Utf8SliceInstructionKind.Jump:
                    if (instruction.D != 0 && captureStarts.Length == 0 && !cursor.RequiredEnd.HasValue && inputIndex < input.Length)
                    {
                        // These complete iterations have no retry or observed
                        // capture effects. Resume at the loop choice before
                        // the first excluded byte, wider scalar, or next block.
                        var scanBreach = invocation.TryChargeInputStep();
                        if (scanBreach != Utf8ExecutionLimitKind.None)
                        { failure = scanBreach; goto Failure; }
                        var block = input.Slice(inputIndex, Math.Min(64, input.Length - inputIndex));
                        var consumed = ScanAsciiMask(block, program.SetMasks[instruction.D - 1], category: false, negated: true);
                        if (consumed != 0)
                        {
                            inputIndex += consumed;
                            programIndex--;
                            break;
                        }
                    }
                    programIndex = instruction.A;
                    break;
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                case Utf8SliceInstructionKind.RepeatGreedyAfterMinimum:
                case Utf8SliceInstructionKind.RepeatLazyAfterMinimum:
                    {
                        var repeatId = instruction.B;
                        // Counts store unsigned bits. Only an inactive repeat has
                        // both UINT_MAX and position -1; active body starts are
                        // nonnegative scalar boundaries, including RTL execution.
                        var count = counts.Length == 0 ? 0 : (long)unchecked((uint)counts[repeatId]);
                        if (counts.Length == 0 || counts[repeatId] == -1 && positions[repeatId] == -1)
                        {
                            count = 0;
                            (int EndIndex, Utf8ExecutionLimitKind Breach) repeated;
                            int continuation;
                            bool ranWithoutRewind;
                            // Prepared proofs verify the atom and exit. An empty
                            // tail allows the greedy range path to keep its maximum
                            // run. Boolean calls can still stop at the minimum.
                            var exit = instructions[instruction.A];
                            var unobservedDecimal = exit.RepeatExitUnobservedDecimal && captureStarts.Length == 0;
                            if (exit.RepeatExitRejectsShorterEndpoints || unobservedDecimal ||
                                cursor.RequiredEnd is null && (program.SkipsTerminalRewind(programIndex) ||
                                    !existenceOnly && exit.RepeatExitAcceptsEmptyTail))
                            {
                                // The unobserved decimal body opens a capture before its atom.
                                var repeatAtomIndex = programIndex + (unobservedDecimal ? 2 : 1);
                                repeated = RunClassRepeat(program, instruction, instructions[repeatAtomIndex], input, inputIndex,
                                    ref invocation, out _);
                                continuation = instruction.A + 1;
                                ranWithoutRewind = true;
                            }
                            else if (exit.RepeatExitBeforeTerminalDot && cursor.RequiredEnd is null &&
                                (inputIndex == input.Length || input[inputIndex] != '\n'))
                            {
                                var breach = TryMatchOptionalScalar(program, instructions[programIndex + 1],
                                    input, inputIndex, ref invocation, out var width);
                                if (breach != Utf8ExecutionLimitKind.None)
                                { failure = breach; goto Failure; }
                                inputIndex += width;
                                programIndex = instruction.A + 1;
                                break;
                            }
                            else
                            {
                                ranWithoutRewind = TryRunClassRepeatWithoutRewind(program, programIndex, input,
                                    inputIndex, cursor.RequiredEnd, ref invocation, out repeated, out continuation,
                                    existenceOnly, checkedDotTailAt == programIndex && inputIndex == cursor.InputIndex);
                            }
                            if (ranWithoutRewind)
                            {
                                if (repeated.Breach != Utf8ExecutionLimitKind.None)
                                { failure = repeated.Breach; goto Failure; }
                                if (repeated.EndIndex >= 0)
                                {
                                    inputIndex = repeated.EndIndex;
                                    // Fusion never activates the repeat counters.
                                    // Its proof validates RepeatExit, whose reset
                                    // would only journal their unchanged values.
                                    programIndex = continuation;
                                }
                                else failed = true;
                                break;
                            }
                            // A lazy category atom may require a longer table
                            // or subtraction walk. Defer it until backtracking.
                            if (continuation > programIndex &&
                                instruction is { RepeatMinimum: 0, RepeatMaximum: 1 } &&
                                (instruction.Kind != Utf8SliceInstructionKind.RepeatLazy ||
                                    instructions[programIndex + 1].Kind != Utf8SliceInstructionKind.CategorySet))
                            {
                                // The repeat proof has verified one forward,
                                // side-effect-free atom and its exit. The two
                                // endpoints share that exit, just like a fork.
                                var breach = TryMatchOptionalScalar(program, instructions[programIndex + 1], input,
                                    inputIndex, ref invocation, out var width);
                                if (breach != Utf8ExecutionLimitKind.None)
                                { failure = breach; goto Failure; }
                                var suffixLength = 0;
                                if (width != 0)
                                {
                                    var end = inputIndex + width;
                                    var lazy = instruction.Kind == Utf8SliceInstructionKind.RepeatLazy;
                                    var preferred = lazy ? inputIndex : end;
                                    var retry = lazy ? end : inputIndex;
                                    // The optional's input step covers bounded prefix probes.
                                    // The straight prefix can stop before another optional.
                                    const int suffixProbeLimit = 4;
                                    var endLimit = instructions[instruction.A].B;
                                    if (!MatchesStraightAsciiPrefix(program, input, preferred, continuation,
                                        suffixProbeLimit, out suffixLength, out var completes))
                                        preferred = retry;
                                    else if ((!completes || cursor.RequiredEnd.HasValue) &&
                                        (endLimit > 0 ? input.Length - retry < endLimit &&
                                            MayMatchBranchedAsciiPrefix(program, input, retry, continuation, out _, instructionLimit: suffixProbeLimit * 2)
                                            : MatchesStraightAsciiPrefix(program, input, retry, continuation, suffixProbeLimit)))
                                    {
                                        // Only extend the preferred check when a retry would be useful.
                                        if (endLimit > 0 && !completes &&
                                            !MayMatchBranchedAsciiPrefix(program, input, preferred + suffixLength, continuation + suffixLength,
                                                out _, instructionLimit: suffixProbeLimit * 2))
                                        {
                                            preferred = retry;
                                            suffixLength = 0;
                                        }
                                        else if (!stacks.PushFrame(new Utf8SliceFrame
                                        {
                                            ResumeKind = Utf8SliceResumeKind.Alternation,
                                            Target = continuation,
                                            InputIndex = retry,
                                            TrailCount = stacks.TrailCount,
                                            JournalCount = stacks.JournalCount,
                                        }, ref invocation))
                                        { failure = stacks.Breach; goto Failure; }
                                    }
                                    inputIndex = preferred + suffixLength;
                                }
                                programIndex = continuation + suffixLength;
                                break;
                            }
                            var (atomIndex, guarded) = continuation == 0 && instruction.Kind == Utf8SliceInstructionKind.RepeatGreedy
                                ? GetCompoundRepeatAtom(program, programIndex, captureStarts.Length == 0) : (-1, false);
                            if (instruction.Kind == Utf8SliceInstructionKind.RepeatGreedy && (continuation > programIndex || atomIndex >= 0))
                            {
                                // A pure scalar run has no intermediate state to restore.
                                // One frame can revisit its earlier endpoints in greedy order.
                                var run = guarded
                                    ? RunGuardedClassRepeat(program, programIndex, atomIndex, input, inputIndex,
                                        (uint)stacks.FrameCount + 1, ref invocation, out var minimumEnd)
                                    : RunClassRepeat(program, instruction, instructions[atomIndex < 0 ? programIndex + 1 : atomIndex],
                                        input, inputIndex, ref invocation, out minimumEnd);
                                if (atomIndex >= 0) continuation = instruction.A + 1;
                                if (run.Breach != Utf8ExecutionLimitKind.None)
                                { failure = run.Breach; goto Failure; }
                                if (run.EndIndex < 0) { failed = true; break; }
                                if (run.EndIndex > minimumEnd && existenceOnly && cursor.RequiredEnd is null &&
                                    TryRunTerminalHybridMinimum(program, input, continuation, run.EndIndex, ref invocation, out var terminal))
                                {
                                    if (terminal.Breach != Utf8ExecutionLimitKind.None)
                                    { failure = terminal.Breach; goto Failure; }
                                    if (terminal.EndIndex >= 0)
                                    {
                                        inputIndex = terminal.EndIndex;
                                        programIndex = instructions.Length - 1;
                                        break;
                                    }
                                    failed = true;
                                }
                                var endpoint = run.EndIndex;
                                var rewindStarts = exit.RepeatExitRewindStarts;
                                if (rewindStarts != 0)
                                {
                                    var firstByte = endpoint < input.Length ? input[endpoint] : -1;
                                    if (firstByte != (byte)rewindStarts && firstByte != (byte)(rewindStarts >> 8) &&
                                        firstByte != (byte)(rewindStarts >> 16))
                                    {
                                        // The continuation needs one of these starters at every endpoint.
                                        // Select the first viable one before opening captures or saving a rewind frame.
                                        failure = FindPreviousRepeatStart(input, minimumEnd, endpoint, rewindStarts,
                                            ref invocation, out endpoint);
                                        if (failure != Utf8ExecutionLimitKind.None) goto Failure;
                                        if (endpoint < 0) { failed = true; break; }
                                    }
                                }
                                if (endpoint > minimumEnd && !stacks.PushFrame(new Utf8SliceFrame
                                {
                                    ResumeKind = Utf8SliceResumeKind.ScalarRepeatRewind,
                                    Target = continuation,
                                    InputIndex = endpoint,
                                    TrailCount = stacks.TrailCount,
                                    Aux = minimumEnd,
                                    Count = rewindStarts,
                                    JournalCount = stacks.JournalCount,
                                }, ref invocation))
                                { failure = stacks.Breach; goto Failure; }
                                inputIndex = endpoint;
                                programIndex = continuation;
                                break;
                            }
                            if (counts.Length == 0)
                            {
                                var breach = stacks.TryRentRepeatTables(program.RepeatCount, ref invocation,
                                    out counts, out positions);
                                if (breach != Utf8ExecutionLimitKind.None)
                                    { failure = breach; goto Failure; }
                                cursor.Counts = counts;
                                cursor.Positions = positions;
                            }
                            if (!SetRepeat(counts, positions, repeatId, 0, -1, ref stacks, ref invocation))
                            {
                                failure = stacks.Breach;
                                goto Failure;
                            }
                        }

                        var madeNoProgress = count > 0 && positions[repeatId] == inputIndex;
                        var minimum = instruction.RepeatMinimum;
                        var maximum = instruction.RepeatMaximum;
                        var greedy = instruction.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatGreedyAfterMinimum;
                        // Repetition counts atom iterations, independently of UTF-8 or UTF-16 width.
                        if (count < minimum)
                        {
                            if (!SetRepeat(counts, positions, repeatId, count + 1, inputIndex, ref stacks, ref invocation))
                            {
                                failure = stacks.Breach;
                                goto Failure;
                            }

                            programIndex++;
                            break;
                        }

                        if (count >= maximum || madeNoProgress && (count > minimum ||
                            instruction.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy))
                        {
                            programIndex = instruction.A;
                            break;
                        }

                        // Finite repeats retain their exact iteration count. An
                        // unbounded optional phase only distinguishes min from
                        // min+1; later totals cannot affect any repeat decision.
                        // This also supports a UINT_MAX-1 minimum without growing
                        // every program's counter, frame or rollback record.
                        var nextCount = maximum == long.MaxValue ? minimum + 1 : count + 1;

                        var frame = new Utf8SliceFrame
                        {
                            InputIndex = inputIndex,
                            TrailCount = stacks.TrailCount,
                            JournalCount = stacks.JournalCount,
                            Aux = repeatId
                        };
                        if (greedy)
                        {
                            frame.ResumeKind = Utf8SliceResumeKind.RepeatUnwind;
                            frame.Target = instruction.A;
                            frame.Count = unchecked((int)count);
                        }
                        else
                        {
                            frame.ResumeKind = Utf8SliceResumeKind.RepeatContinue;
                            frame.Target = programIndex + 1;
                            frame.Count = unchecked((int)nextCount);
                        }

                        if (!stacks.PushFrame(frame, ref invocation))
                        {
                            failure = stacks.Breach;
                            goto Failure;
                        }

                        if (greedy)
                        {
                            if (!SetRepeat(counts, positions, repeatId, nextCount, inputIndex, ref stacks, ref invocation))
                            {
                                failure = stacks.Breach;
                                goto Failure;
                            }

                            programIndex++;
                        }
                        else
                        {
                            programIndex = instruction.A;
                        }

                        break;
                    }

                case Utf8SliceInstructionKind.RepeatEnd:
                {
                    var repeat = instructions[instruction.A];
                    if (!program.RightToLeft && repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
                        repeat.A == programIndex + 1 && instructions[repeat.A].B == 0 &&
                        stacks.CompactScalarRepeatExit(frameBase, repeat.A, positions[repeat.B], input, inputIndex,
                            instructions[repeat.A].RepeatExitRewindStarts) &&
                        instruction.B != 0)
                    {
                        failure = stacks.ScanScalarRepeatExit(input, instruction.B, ref positions[repeat.B], ref inputIndex, ref invocation);
                        if (failure != Utf8ExecutionLimitKind.None) goto Failure;
                    }
                    programIndex = instruction.A;
                    break;
                }
                case Utf8SliceInstructionKind.RepeatExit:
                    if (!SetRepeat(counts, positions, instruction.A, -1, -1, ref stacks, ref invocation))
                    {
                        failure = stacks.Breach;
                        goto Failure;
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                    // Capture effects beyond the requested slots are unobserved.
                    // The instruction still spends its normal work.
                    if ((uint)instruction.A >= (uint)captureStarts.Length) { programIndex++; break; }
                    if (program.UsesCaptureOpeningJournal)
                    {
                        if (!stacks.PushTrailEntry(new Utf8SliceTrailEntry { Slot = instruction.A, NewValue = inputIndex, IsOpening = true }, ref invocation))
                        { failure = stacks.Breach; goto Failure; }
                        programIndex++;
                        break;
                    }
                    // The assertion/history driver retains every entry. A plain
                    // start slot in a latest-value root needs undo only while
                    // a frame can return to an earlier value.
                    if ((preserveCaptureTrail || balanceHeads.Length != 0 || stacks.FrameCount != 0) &&
                        !stacks.PushTrail(instruction.A, captureStarts[instruction.A], inputIndex, true, ref invocation))
                    {
                        failure = stacks.Breach;
                        goto Failure;
                    }

                    captureStarts[instruction.A] = inputIndex;
                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    if ((uint)instruction.A >= (uint)captureStarts.Length) { programIndex++; break; }
                    if (program.UsesCaptureOpeningJournal)
                    {
                        // An opening capture does not overwrite the last completed one.
                        // This preserves nested aliases and references inside their bodies.
                        if (!stacks.TryFindCaptureOpening(instruction.A, ref invocation, out var opening, out var openingBreach))
                            { failure = openingBreach; goto Failure; }
                        if (!stacks.PushRestoreTrail(instruction.A, captureStarts[instruction.A], opening, captureEnds[instruction.A], inputIndex, ref invocation) ||
                            !stacks.PushTrailEntry(new Utf8SliceTrailEntry { Slot = instruction.A, OldValue = opening, NewValue = inputIndex, IsClosing = true }, ref invocation))
                        { failure = stacks.Breach; goto Failure; }
                        captureStarts[instruction.A] = opening;
                        captureEnds[instruction.A] = inputIndex;
                    }
                    else
                    {
                        // Python metadata observes closing order separately
                        // from the final slot values. Latest-only output needs
                        // a trail entry only for rollback.
                        if ((preserveCaptureTrail || preserveClosingOrder || balanceHeads.Length != 0 || stacks.FrameCount != 0) &&
                            !stacks.PushTrail(instruction.A, captureEnds[instruction.A], inputIndex, false, ref invocation))
                        {
                            failure = stacks.Breach;
                            goto Failure;
                        }
                        captureEnds[instruction.A] = inputIndex;
                    }
                    if (program.HasBalancing)
                    {
                        // Balancing stacks mirror every capture so pops can
                        // restore previous tops; the head journal makes the
                        // push vanish on backtrack like any trail write.
                        if (!stacks.PushBalance(captureStarts[instruction.A], inputIndex, balanceHeads[instruction.A], ref invocation) ||
                            !stacks.PushHeadTrail(instruction.A, balanceHeads[instruction.A], stacks.BalanceCount - 1, ref invocation))
                        {
                            failure = stacks.Breach;
                            goto Failure;
                        }

                        balanceHeads[instruction.A] = stacks.BalanceCount - 1;
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.Dot:
                    // Mirrored dots consume the scalar ending at the attempt edge.
                    if (program.RightToLeft)
                    {
                        if (TryGetScalarWidthEndingAt(input, inputIndex, out var rtlDotWidth) &&
                            (instruction.A != 0 || input[inputIndex - 1] != (byte)'\n'))
                        {
                            inputIndex -= rtlDotWidth;
                            programIndex++;
                        }
                        else
                        {
                            failed = true;
                        }

                        break;
                    }

                    // Dots consume one scalar: ASCII and BMP leads take
                    // their scalar, astral leads take all four bytes.
                    // Continuation bytes fail the attempt, so byte-stepping
                    // drivers cannot report unaligned starts. The caller's
                    // valid UTF-8 guarantee keeps each lead's width in bounds.
                    if (inputIndex < input.Length &&
                        (instruction.A != 0 || input[inputIndex] != (byte)'\n') &&
                        (input[inputIndex] & 0xC0) != 0x80)
                    {
                        var lead = input[inputIndex];
                        if (lead < 0x80)
                        {
                            inputIndex++;
                        }
                        else if (lead < 0xE0)
                        {
                            inputIndex += 2;
                        }
                        else if (lead < 0xF0)
                        {
                            inputIndex += 3;
                        }
                        else
                        {
                            inputIndex += 4;
                        }

                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Anchor:
                    // Zero-width edges: beginning holds at the input origin
                    // or (multiline `^`) just after a line feed; lax end
                    // (`$`, `\Z`) holds at the input end, before a trailing
                    // line feed, or (multiline `$`) just before any line
                    // feed; strict end (`\z`) holds only at the input end.
                    // Line feeds are single bytes, so these checks are
                    // encoding-indifferent and need no astral routing.
                    // Guards order short-circuit before any indexing.
                    var anchorMultiline = instruction.B != 0;
                    bool anchorHolds;
                    if (instruction.A == 0)
                    {
                        // Origin assertions require a scalar-aligned attempt.
                        anchorHolds = (inputIndex == 0 && startIsScalarBoundary) ||
                            (anchorMultiline && inputIndex > 0 && input[inputIndex - 1] == (byte)'\n');
                    }
                    else if (instruction.A == 3)
                    {
                        // Contiguous matches pin to the scan start (BCL startat),
                        // defaulting to the attempt start when no driver set an origin.
                        var origin = invocation.AnchorOriginByte;
                        anchorHolds = inputIndex == (origin < 0 ? start : origin);
                    }
                    else
                    {
                        anchorHolds = MatchesEndAnchor(input, inputIndex, instruction);
                    }

                    if (anchorHolds)
                    {
                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Boundary:
                    // Zero-width word edges: `\b` holds on a word/non-word
                    // transition, `\B` where both sides agree. Both require
                    // scalar-aligned positions and classify complete neighbors.
                    if (!startIsScalarBoundary)
                    {
                        failed = true;
                    }
                    else if (inputIndex <= input.Length &&
                        (inputIndex == input.Length || (input[inputIndex] & 0xC0) != 0x80))
                    {
                        if (MatchesBoundary(program, instruction, input, inputIndex))
                        {
                            programIndex++;
                        }
                        else
                        {
                            failed = true;
                        }
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Shorthand:
                case Utf8SliceInstructionKind.CategorySet:
                case Utf8SliceInstructionKind.Hybrid:
                    {
                        var reverse = program.RightToLeft;
                        var edge = reverse ? inputIndex - 1 : inputIndex;
                        int classWidth;
                        bool classHit;
                        if (instruction is { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } &&
                            (uint)edge < (uint)input.Length && input[edge] is < 128 and var value)
                        {
                            // Lowering includes the shorthand members in the ASCII mask.
                            classHit = MatchesAsciiHybridClass(program.SetMasks[instruction.A], instruction.B != 0, value);
                            classWidth = 1;
                        }
                        else
                        {
                            if (!TryReadScalar(input, inputIndex, reverse, out var classScalar, out classWidth))
                            {
                                failed = true;
                                break;
                            }

                            var classBreach = TryMatchScalarClass(program, instruction, classScalar,
                                ref invocation, out classHit);
                            if (classBreach != Utf8ExecutionLimitKind.None)
                            { failure = classBreach; goto Failure; }
                        }

                        if (classHit)
                        {
                            inputIndex += reverse ? -classWidth : classWidth;
                            programIndex++;
                        }
                        else failed = true;
                        break;
                    }

                case Utf8SliceInstructionKind.Backref:
                    {
                        var referenced = RunBackreference(program, instruction, input,
                            captureStarts, captureEnds, inputIndex, ref invocation);
                        if (referenced.Breach != Utf8ExecutionLimitKind.None)
                        { failure = referenced.Breach; goto Failure; }
                        if (referenced.IsMatch)
                        {
                            inputIndex = referenced.EndIndex;
                            programIndex++;
                        }
                        else failed = true;
                        break;
                    }
                case Utf8SliceInstructionKind.TestGroup:
                    // One-shot group test (BCL IsMatched: participated, even empty;
                    // in-progress and unset groups test false, group zero never matches):
                    // jump to the no-branch on false, fall into yes on true. Downstream
                    // failure never retries the other branch (BCL commits to the test).
                    // Direction-free: mirrored runs record inverted spans, but both ends
                    // stay non-negative exactly when the group participated.
                    if (instruction.A < 0 || captureStarts[instruction.A] < 0 || captureEnds[instruction.A] < 0)
                    {
                        programIndex = instruction.B;
                    }
                    else
                    {
                        programIndex++;
                    }

                    break;
                case Utf8SliceInstructionKind.Fail:
                    failed = true;
                    break;
                case Utf8SliceInstructionKind.AtomicCommit:
                    {
                        // Possessive commit: discard every frame up to and
                        // including the atomic fork (a surviving body must not
                        // resume its alternatives), keeping input, captures,
                        // and repeat state.
                        var foundAtomic = false;
                        while (stacks.FrameCount > frameBase && stacks.PopFrame(out var committed))
                        {
                            if (committed.ResumeKind == Utf8SliceResumeKind.Atomic)
                            {
                                foundAtomic = true;
                                break;
                            }
                        }

                        if (!foundAtomic)
                        {
                            failed = true;
                            break;
                        }

                        programIndex++;
                        break;
                    }
                case Utf8SliceInstructionKind.Commit:
                    {
                        // Assertion commit: discard every frame up to and
                        // including the assertion fork (a surviving body must
                        // not resume its alternatives), rewind to the
                        // assertion position (zero width), restore repeat
                        // state to the fork mark, and keep capture writes.
                        var journalMark = -1;
                        var rewindTo = -1;
                        while (stacks.FrameCount > frameBase && stacks.PopFrame(out var committed))
                        {
                            if (committed.ResumeKind == Utf8SliceResumeKind.Assertion)
                            {
                                journalMark = committed.JournalCount;
                                rewindTo = committed.InputIndex;
                                break;
                            }
                        }

                        if (journalMark < 0)
                        {
                            failed = true;
                            break;
                        }

                        inputIndex = rewindTo;
                        stacks.TruncateJournal(journalMark, counts, positions);
                        programIndex++;
                        break;
                    }
                case Utf8SliceInstructionKind.Lookbehind:
                case Utf8SliceInstructionKind.Lookahead:
                {
                    // Simple side-effect-free assertions can stay in this segment.
                    if (TryRunInlineAssertion(program.Assertions[instruction.A], input, inputIndex,
                        (uint)stacks.FrameCount + 1, ref invocation, out var matched, out var assertionBreach))
                    {
                        if (assertionBreach != Utf8ExecutionLimitKind.None)
                            { failure = assertionBreach; goto Failure; }
                        failed = matched != (instruction.B != 0);
                        if (!failed) programIndex++;
                        break;
                    }
                    // The driver owns child arrays and saves this exact edge;
                    // returning here suspends, rather than recursively entering,
                    // the assertion's independent instruction stream.
                    cursor.ProgramIndex = programIndex;
                    cursor.InputIndex = inputIndex;
                    suspended = true;
                    outcome = default;
                    goto Finish;
                }
                case Utf8SliceInstructionKind.BalanceStart:
                    // Retain the body's opening without testing its source yet:
                    // the body can establish the capture popped at successful close.
                    if (!stacks.PushTrailEntry(new Utf8SliceTrailEntry {
                        Slot = instruction.B,
                        NewValue = inputIndex,
                        IsOpening = true
                    }, ref invocation))
                    { failure = stacks.Breach; goto Failure; }
                    programIndex++;
                    break;

                case Utf8SliceInstructionKind.BalancePop:
                    {
                        // Check at successful close: the body may have consumed
                        // the source through a nested balancing group. Pop its
                        // current completed capture, then project the interval
                        // against the completed body independently of direction.
                        var popSlot = instruction.A;
                        var captureSlot = instruction.B;
                        if (balanceHeads[popSlot] < 0)
                        {
                            failed = true;
                            break;
                        }
                        var top = stacks.BalanceEntryAt(balanceHeads[popSlot]);
                        var previousStart = -1;
                        var previousEnd = -1;
                        if (top.Prev >= 0)
                        {
                            var below = stacks.BalanceEntryAt(top.Prev);
                            previousStart = below.Start;
                            previousEnd = below.End;
                        }
                        if (!stacks.PushRestoreTrail(popSlot, captureStarts[popSlot], previousStart,
                                captureEnds[popSlot], previousEnd, ref invocation) ||
                            !stacks.PushHeadTrail(popSlot, balanceHeads[popSlot], top.Prev, ref invocation))
                            { failure = stacks.Breach; goto Failure; }
                        captureStarts[popSlot] = previousStart;
                        captureEnds[popSlot] = previousEnd;
                        balanceHeads[popSlot] = top.Prev;
                        if (captureSlot >= 0)
                        {
                            if (!stacks.TryFindCaptureOpening(captureSlot, ref invocation,
                                    out var opening, out var openingBreach))
                                { failure = openingBreach; goto Failure; }
                            var sourceStart = Math.Min(top.Start, top.End);
                            var sourceEnd = Math.Max(top.Start, top.End);
                            var gapStart = Math.Min(opening, inputIndex);
                            var gapEnd = Math.Max(opening, inputIndex);
                            // Capture the gap for disjoint spans or their
                            // intersection when the body overlaps its source.
                            if (gapStart >= sourceEnd)
                            {
                                gapEnd = gapStart;
                                gapStart = sourceEnd;
                            }
                            else if (gapEnd <= sourceStart)
                            {
                                gapStart = gapEnd;
                                gapEnd = sourceStart;
                            }
                            else
                            {
                                gapStart = Math.Max(gapStart, sourceStart);
                                gapEnd = Math.Min(gapEnd, sourceEnd);
                            }
                            if (!stacks.PushRestoreTrail(captureSlot, captureStarts[captureSlot], gapStart,
                                    captureEnds[captureSlot], gapEnd, ref invocation) ||
                                !stacks.PushTrailEntry(new Utf8SliceTrailEntry {
                                    Slot = captureSlot,
                                    OldValue = gapStart,
                                    NewValue = gapEnd,
                                    IsClosing = true
                                }, ref invocation) ||
                                !stacks.PushBalance(gapStart, gapEnd, balanceHeads[captureSlot], ref invocation) ||
                                !stacks.PushHeadTrail(captureSlot, balanceHeads[captureSlot], stacks.BalanceCount - 1, ref invocation))
                                { failure = stacks.Breach; goto Failure; }
                            captureStarts[captureSlot] = gapStart;
                            captureEnds[captureSlot] = gapEnd;
                            balanceHeads[captureSlot] = stacks.BalanceCount - 1;
                        }
                        programIndex++;
                        break;
                    }

                case Utf8SliceInstructionKind.Accept:
                    if (requireNonEmpty && inputIndex == start ||
                        cursor.RequiredEnd is int requiredEnd && inputIndex != requiredEnd)
                    {
                        failed = true;
                        break;
                    }

                    // Publication belongs to RunFrom after assertion continuations
                    // close, so metadata denial cannot strand owned history roots.
                    outcome = new(true, inputIndex, Utf8ExecutionLimitKind.None);
                    goto Finish;
                default:
                    throw new InvalidOperationException($"Unsupported slice instruction {instruction.Kind}.");
            }

        Backtrack:
            if (failed)
            {
                if (stacks.FrameCount == frameBase || !stacks.PopFrame(out var popped))
                {
                    failure = Utf8ExecutionLimitKind.None;
                    goto Failure;
                }

                stacks.TruncateJournal(popped.JournalCount, counts, positions);
                stacks.TruncateTrail(popped.TrailCount, captureStarts, captureEnds, balanceHeads);
                switch (popped.ResumeKind)
                {
                    case Utf8SliceResumeKind.Alternation:
                    case Utf8SliceResumeKind.Assertion:
                    case Utf8SliceResumeKind.Atomic:
                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    case Utf8SliceResumeKind.ScalarRepeatRewind:
                        if (popped.Count != 0)
                        {
                            failure = FindPreviousRepeatStart(input, popped.Aux, popped.InputIndex, popped.Count,
                                ref invocation, out inputIndex);
                            if (failure != Utf8ExecutionLimitKind.None) goto Failure;
                            if (inputIndex < 0) goto Backtrack;
                        }
                        else
                        {
                            failure = invocation.TryChargeStep();
                            if (failure != Utf8ExecutionLimitKind.None) goto Failure;
                            if (!TryGetScalarWidthEndingAt(input, popped.InputIndex, out var width)) goto Backtrack;
                            inputIndex = popped.InputIndex - width;
                        }
                        programIndex = popped.Target;
                        if (inputIndex > popped.Aux)
                        {
                            popped.InputIndex = inputIndex;
                            if (!stacks.PushFrame(popped, ref invocation)) { failure = stacks.Breach; goto Failure; }
                        }
                        break;
                    case Utf8SliceResumeKind.RepeatUnwind:
                        var endLimit = instructions[popped.Target].B;
                        if (endLimit != 0 && input.Length - popped.InputIndex >= endLimit)
                        {
                            // This saved endpoint cannot reach the terminal anchor.
                            // The undo has run normally and the next frame owns its state.
                            var skipBreach = invocation.TryChargeStep();
                            if (skipBreach != Utf8ExecutionLimitKind.None) { failure = skipBreach; goto Failure; }
                            goto Backtrack;
                        }
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            failure = stacks.Breach;
                            goto Failure;
                        }

                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    default:
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            failure = stacks.Breach;
                            goto Failure;
                        }

                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                }
            }
        }
    Failure:
        outcome = new(false, -1, failure);
    Finish:
        // Inline assertions share the instruction cadence during retries.
        // Check before returning an outcome or handing off to another segment.
        if (checkedInlineAssertion)
        {
            invocation.Budget.CheckDeadline();
            invocation.Budget.ThrowIfCancellationRequested();
        }
        return outcome;
    }

    private static bool SetRepeat(
        int[] counts,
        int[] positions,
        int repeatId,
        long count,
        int position,
        ref ChunkedStacks stacks,
        ref Utf8SliceInvocation invocation)
    {
        if (!stacks.PushJournal(repeatId, counts[repeatId], positions[repeatId], ref invocation))
        {
            return false;
        }

        counts[repeatId] = unchecked((int)count);
        positions[repeatId] = position;
        return true;
    }

    // One search/Count call retains its admitted scratch between candidates.
    // State is reset for each attempt; no workspace escapes or crosses invocations.
    internal struct AttemptWorkspace
    {
        internal int[] Counts;
        internal int[] Positions;
        internal int[] Heads;
        internal ChunkedStacks Stacks;
        // Attempts unwind every child. The empty buffer retains its admitted
        // capacity alongside the root tables for the next attempt.
        internal Utf8InvocationStateBuffer<AssertionContinuation> AssertionContinuations;
        internal bool Initialized;
        private ulong _reservation;

        // A failed balancing setup can still own its reservation.
        internal readonly bool NeedsRelease => Initialized || _reservation != 0;

        internal Utf8ExecutionLimitKind Initialize(Utf8SliceProgram program, ref Utf8SliceInvocation invocation)
        {
            // Fused repeats use scalar locals. Root repeat vectors are acquired
            // by the existing stack owner only when a repeat needs mutable state.
            // Lowering propagates the same balancing requirement to assertion
            // bodies. Only balancing captures produce or restore head entries.
            var balanceGroups = program.HasBalancing ? program.GroupCount : 0;
            var bytes = Utf8ConstructionBudget.ArrayBytes<int>(balanceGroups);
            var breach = invocation.TryReserveWorking(bytes);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            _reservation = bytes;
            if (balanceGroups != 0)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                Heads = new int[balanceGroups];
            }
            else Heads = [];
            // These tables are read only after the first attempt is admitted.
            Counts = Positions = [];
            Initialized = true;
            return Utf8ExecutionLimitKind.None;
        }

        internal void Release(ref Utf8SliceInvocation invocation)
        {
            AssertionContinuations.Release(ref invocation);
            Stacks.ReleaseAll(ref invocation);
            invocation.Ledger.Release(_reservation);
            _reservation = 0;
            Heads = [];
            Positions = [];
            Counts = [];
            Initialized = false;
        }
    }

    internal struct ChunkedStacks
    {
        private Utf8SliceFrame[][] _frameTables;
        private Utf8SliceTrailEntry[][] _trailTables;
        private Utf8SliceCountMutation[][] _journalTables;
        private Utf8SliceBalanceEntry[][] _balanceTables;
        private int _frameCount;
        private int _trailCount;
        private int _journalCount;
        private int _balanceCount;
        private ulong _reservedBytes;
        internal Utf8ExecutionLimitKind Breach;

        internal int FrameCount => _frameCount;
        internal int TrailCount => _trailCount;
        internal int JournalCount => _journalCount;
        internal int BalanceCount => _balanceCount;

        // These root arrays share this workspace's reservation lifetime and are
        // retained between attempts. Assertion children keep their own leases.
        internal Utf8ExecutionLimitKind TryRentRepeatTables(int repeats, ref Utf8SliceInvocation invocation,
            out int[] counts, out int[] positions)
        {
            counts = positions = [];
            var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(repeats) * 2);
            var reserved = checked(_reservedBytes + bytes);
            var breach = invocation.TryReserveWorking(bytes);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            // The workspace releases this reservation even on partial allocation
            // or interruption, just as it releases partially acquired stack chunks.
            _reservedBytes = reserved;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            counts = new int[repeats];
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            positions = new int[repeats];
            for (var offset = 0; offset < repeats;)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(2048, repeats - offset);
                counts.AsSpan(offset, length).Fill(-1);
                positions.AsSpan(offset, length).Fill(-1);
                offset += length;
            }
            return Utf8ExecutionLimitKind.None;
        }

        // Capture end writes and explicit close markers already survive exactly
        // the chosen path. Reuse their order; no completion history is allocated.
        internal Utf8ExecutionLimitKind FindLastClosedSlot(ref Utf8SliceInvocation invocation, out int slot)
        {
            slot = -1;
            for (var index = _trailCount - 1; index >= 0; index--)
            {
                if ((index & 63) == 0 || index == _trailCount - 1)
                {
                    var breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                }
                var entry = _trailTables[index / TrailChunkCapacity][index % TrailChunkCapacity];
                if (entry.IsClosing || !entry.IsStart && !entry.IsHead && !entry.IsRestore && !entry.IsOpening)
                { slot = entry.Slot; break; }
            }
            return Utf8ExecutionLimitKind.None;
        }

        internal bool PushFrame(Utf8SliceFrame frame, ref Utf8SliceInvocation invocation)
        {
            // Admit the prospective live depth before a frame table/chunk can
            // grow. Assertion, fork and repeat callers share this one rule.
            Breach = invocation.Budget.TryNoteDepth((uint)_frameCount + 1);
            if (Breach != Utf8ExecutionLimitKind.None) return false;
            return Push(ref _frameTables, ref _frameCount, FrameChunkCapacity, frame, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool CompactScalarRepeatExit(int frameBase, int exit, int start, ReadOnlySpan<byte> input, int end,
            int rewindStarts)
        {
            if (_frameCount <= frameBase) return false;
            ref var latest = ref FrameAt(_frameCount - 1);
            // The repeat's own exit must be on top, with no surviving body
            // alternative, capture change, or nested repeat mutation.
            if (latest.ResumeKind != Utf8SliceResumeKind.RepeatUnwind || latest.Target != exit ||
                latest.InputIndex != start || latest.TrailCount != _trailCount || latest.JournalCount != _journalCount - 1 ||
                !TryGetScalarWidthEndingAt(input, end, out var width) || end - start != width)
                return false;

            var mark = latest.JournalCount;
            if (_journalTables[mark / JournalChunkCapacity][mark % JournalChunkCapacity].RepeatId != latest.Aux) return false;
            if (_frameCount - frameBase > 1)
            {
                ref var previous = ref FrameAt(_frameCount - 2);
                if (previous.ResumeKind == Utf8SliceResumeKind.ScalarRepeatRewind && previous.Target == exit &&
                    previous.InputIndex == start && previous.TrailCount == _trailCount && previous.JournalCount == mark - 1)
                {
                    // Keep the current counter for forward execution. The
                    // oldest journal entry suffices on unwind: every saved
                    // endpoint resumes at RepeatExit, which clears this slot.
                    previous.InputIndex = end;
                    _frameCount--;
                    _journalCount--;
                    return true;
                }
            }

            latest.ResumeKind = Utf8SliceResumeKind.ScalarRepeatRewind;
            latest.Aux = start;
            latest.Count = rewindStarts;
            latest.InputIndex = end;
            return true;
        }

        internal Utf8ExecutionLimitKind ScanScalarRepeatExit(ReadOnlySpan<byte> input, int stops,
            ref int lastStart, ref int end, ref Utf8SliceInvocation invocation)
        {
            if (end == input.Length) return Utf8ExecutionLimitKind.None;
            var first = (byte)stops;
            var second = (byte)(stops >> 8);
            var third = (byte)(stops >> 16);
            var next = input[end];
            if (next == first || next == second || next == third) return Utf8ExecutionLimitKind.None;

            var breach = invocation.TryChargeInputStep();
            // The skipped body would hold one transient repeat exit and
            // evaluate its assertion above the existing compact rewind.
            if (breach == Utf8ExecutionLimitKind.None) breach = invocation.Budget.TryNoteDepth((uint)_frameCount + 2);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var position = end;
            while (true)
            {
                var block = input.Slice(position, Math.Min(512, input.Length - position));
                var stop = block.IndexOfAny(first, second, third);
                if (stop >= 0) { position += stop; break; }
                position += block.Length;
                if (position == input.Length) break;
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
            }

            invocation.Budget.ThrowIfCancellationRequested();
            invocation.Budget.CheckDeadline();
            // A found ASCII byte or the end of valid input is a scalar
            // boundary even if the intermediate blocks split a scalar.
            if (TryGetScalarWidthEndingAt(input, position, out var width))
            {
                lastStart = position - width;
                FrameAt(_frameCount - 1).InputIndex = end = position;
            }
            return Utf8ExecutionLimitKind.None;
        }

        internal bool PushTrail(int slot, int oldValue, int newValue, bool isStart, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldValue, NewValue = newValue, IsStart = isStart }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushTrailEntry(Utf8SliceTrailEntry entry, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                entry, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool TryFindCaptureOpening(int slot, ref Utf8SliceInvocation invocation, out int opening, out Utf8ExecutionLimitKind breach)
        {
            var closed = 0;
            opening = -1;
            breach = Utf8ExecutionLimitKind.None;
            for (var index = _trailCount - 1; index >= 0; index--)
            {
                breach = invocation.TryChargeStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                var entry = TrailEntryAt(index);
                if (entry.Slot != slot) continue;
                if (entry.IsClosing) closed++;
                else if (entry.IsOpening)
                {
                    if (closed != 0) closed--;
                    else
                    {
                        opening = entry.NewValue;
                        return true;
                    }
                }
            }
            throw new InvalidOperationException("Capture close has no surviving opening marker.");
        }

        internal bool PushRestoreTrail(int slot, int oldStart, int newStart, int oldEnd, int newEnd, ref Utf8SliceInvocation invocation)
        {
            // Balance-pop slot restores: live slot state for backtrack
            // truncation, but no capture record (the popped capture
            // drains through its head entry; the restored top stays
            // recorded from its own capture time).
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldStart, NewValue = newStart, IsStart = true, IsRestore = true }, ref invocation, ref _reservedBytes, out Breach) &&
                Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldEnd, NewValue = newEnd, IsStart = false, IsRestore = true }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushJournal(int repeatId, int oldCount, int oldPosition, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _journalTables, ref _journalCount, JournalChunkCapacity,
                new Utf8SliceCountMutation { RepeatId = repeatId, OldCount = oldCount, OldPosition = oldPosition }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushBalance(int start, int end, int prev, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _balanceTables, ref _balanceCount, BalanceChunkCapacity,
                new Utf8SliceBalanceEntry { Start = start, End = end, Prev = prev }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushHeadTrail(int slot, int oldHead, int newHead, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldHead, NewValue = newHead, IsStart = false, IsHead = true }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal Utf8SliceBalanceEntry BalanceEntryAt(int index)
        {
            return _balanceTables[index / BalanceChunkCapacity][index % BalanceChunkCapacity];
        }

        internal bool PopFrame(out Utf8SliceFrame frame)
        {
            if (_frameCount == 0)
            {
                frame = default;
                return false;
            }

            frame = FrameAt(--_frameCount);
            return true;
        }

        private ref Utf8SliceFrame FrameAt(int index) =>
            ref _frameTables[index / FrameChunkCapacity][index % FrameChunkCapacity];

        internal Utf8SliceTrailEntry TrailEntryAt(int index)
        {
            return _trailTables[index / TrailChunkCapacity][index % TrailChunkCapacity];
        }

        internal void TruncateTrail(int count, scoped Span<int> captureStarts, scoped Span<int> captureEnds, int[] balanceHeads)
        {
            while (_trailCount > count)
            {
                _trailCount--;
                var entry = _trailTables[_trailCount / TrailChunkCapacity][_trailCount % TrailChunkCapacity];
                if (entry.IsOpening || entry.IsClosing) continue;
                if (entry.IsHead)
                {
                    balanceHeads[entry.Slot] = entry.OldValue;
                }
                else if (entry.IsStart)
                {
                    captureStarts[entry.Slot] = entry.OldValue;
                }
                else
                {
                    captureEnds[entry.Slot] = entry.OldValue;
                }
            }
        }

        internal void TruncateJournal(int count, int[] counts, int[] positions)
        {
            while (_journalCount > count)
            {
                _journalCount--;
                var entry = _journalTables[_journalCount / JournalChunkCapacity][_journalCount % JournalChunkCapacity];
                counts[entry.RepeatId] = entry.OldCount;
                positions[entry.RepeatId] = entry.OldPosition;
            }
        }

        internal Utf8RangeMatch SnapshotHistory(ReadOnlyMemory<byte> input, int[] numbers, int matchStart, int matchEnd,
            ref Utf8InvocationResultLedger results, ref Utf8SliceInvocation invocation)
        {
            const int maxStackGroups = 16;
            var groups = numbers.Length - 1;
            var useStack = groups <= maxStackGroups;
            var temporaryBytes = useStack ? 0UL : checked(Utf8ConstructionBudget.ArrayBytes<int>(groups) * 2);
            if (!useStack)
            {
                Utf8SliceBufferWork.ChargeResultBytes(temporaryBytes, ref invocation);
                if (invocation.TryReserveWorking(temporaryBytes) != Utf8ExecutionLimitKind.None)
                    throw invocation.Budget.CreateResourceFailure();
            }

            int[]? rentedCounts = null;
            int[]? rentedOpen = null;
            var retain = invocation.HasUnlimitedWorkingMemory;
            try
            {
                Span<int> scratch = useStack ? stackalloc int[maxStackGroups * 2] : default;
                scoped Span<int> counts;
                scoped Span<int> open;
                if (useStack)
                {
                    counts = scratch[..groups];
                    open = scratch.Slice(maxStackGroups, groups);
                }
                else
                {
                    Utf8SliceBufferWork.Checkpoint(ref invocation);
                    counts = rentedCounts = Utf8ExactStatePool<int>.Rent(groups, retain);
                    Utf8SliceBufferWork.Checkpoint(ref invocation);
                    open = rentedOpen = Utf8ExactStatePool<int>.Rent(groups, retain);
                }
                for (var offset = 0; offset < groups;)
                {
                    Utf8SliceBufferWork.Checkpoint(ref invocation);
                    var count = Math.Min(2048, groups - offset);
                    counts.Slice(offset, count).Clear();
                    offset += count;
                }

                // Gross counts leave room for captures subsequently popped by
                // balancing. The accepted trail excludes abandoned branches.
                var length = 1;
                var removed = 0;
                for (var entry = 0; entry < _trailCount; entry++)
                {
                    if ((entry & 255) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsRestore || item.IsOpening) continue;
                    if (item.IsHead)
                    {
                        if (item.NewValue < item.OldValue) removed++;
                    }
                    else if (!item.IsStart)
                    {
                        counts[item.Slot]++;
                        length = checked(length + 1);
                    }
                }

                var offsets = results.AllocateArray<int>(checked(numbers.Length + 1), ref invocation);
                var ranges = results.AllocateArray<Utf8CaptureRange>(length, ref invocation);
                ranges[0] = new(matchStart, matchEnd);
                var next = 1;
                for (var group = 0; group < groups; group++)
                {
                    if ((group & 255) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
                    open[group] = -1;
                    offsets[group + 1] = next;
                    next += counts[group];
                    counts[group] = offsets[group + 1];
                }
                offsets[^1] = length;

                for (var entry = 0; entry < _trailCount; entry++)
                {
                    if ((entry & 255) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsRestore || item.IsOpening) continue;
                    if (item.IsHead)
                    {
                        if (item.NewValue < item.OldValue) counts[item.Slot]--;
                    }
                    else if (item.IsStart) open[item.Slot] = item.NewValue;
                    else
                    {
                        var left = item.IsClosing ? item.OldValue : open[item.Slot];
                        var right = item.NewValue;
                        ranges[counts[item.Slot]++] = new(Math.Min(left, right), Math.Max(left, right));
                    }
                }

                if (removed != 0)
                {
                    var compact = results.AllocateArray<Utf8CaptureRange>(length - removed, ref invocation);
                    compact[0] = ranges[0];
                    var written = 1;
                    for (var group = 0; group < groups; group++)
                    {
                        if ((group & 255) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
                        var start = offsets[group + 1];
                        offsets[group + 1] = written;
                        while (start < counts[group])
                        {
                            Utf8SliceBufferWork.Checkpoint(ref invocation);
                            var count = Math.Min(512, counts[group] - start);
                            ranges.AsSpan(start, count).CopyTo(compact.AsSpan(written, count));
                            start += count;
                            written += count;
                        }
                    }
                    offsets[^1] = written;
                    results.ReleaseArray(ranges, ref invocation);
                    ranges = compact;
                }

                return new(input, ranges, offsets, numbers);
            }
            finally
            {
                if (rentedOpen is not null) Utf8ExactStatePool<int>.Return(rentedOpen, retain);
                if (rentedCounts is not null) Utf8ExactStatePool<int>.Return(rentedCounts, retain);
                if (!useStack) invocation.Ledger.Release(temporaryBytes);
            }
        }

        internal void ResetAttempt()
        {
            _frameCount = 0;
            _trailCount = 0;
            _journalCount = 0;
            _balanceCount = 0;
        }

        internal void ReleaseAll(ref Utf8SliceInvocation invocation)
        {
            if (_reservedBytes != 0)
            {
                invocation.Ledger.Release(_reservedBytes);
                _reservedBytes = 0;
            }

            var frames = _frameTables;
            _frameTables = Array.Empty<Utf8SliceFrame[]>();
            if (frames is { Length: > 0 } && frames[0] is { } first)
                Utf8ExactStatePool<Utf8SliceFrame>.Return(first, invocation.HasUnlimitedWorkingMemory);
            _trailTables = Array.Empty<Utf8SliceTrailEntry[]>();
            _journalTables = Array.Empty<Utf8SliceCountMutation[]>();
            _balanceTables = Array.Empty<Utf8SliceBalanceEntry[]>();
            _frameCount = 0;
            _trailCount = 0;
            _journalCount = 0;
            _balanceCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Push<T>(ref T[][] tables, ref int count, int chunkCapacity,
            T value, ref Utf8SliceInvocation invocation, ref ulong reservedBytes, out Utf8ExecutionLimitKind breach)
            where T : struct
        {
            breach = Utf8ExecutionLimitKind.None;
            if (count == int.MaxValue) { breach = Utf8ExecutionLimitKind.WorkingBytes; return false; }
            var chunk = count / chunkCapacity;
            var slot = count % chunkCapacity;
            if (slot == 0 && (tables is null || chunk >= tables.Length || tables[chunk] is null) &&
                !TryGrowStack(ref tables!, chunk, chunkCapacity, ref invocation, ref reservedBytes, out breach))
                return false;
            tables[chunk][slot] = value;
            count++;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGrowStack<T>(ref T[][] tables, int chunk, int chunkCapacity,
            ref Utf8SliceInvocation invocation, ref ulong reservedBytes, out Utf8ExecutionLimitKind breach)
            where T : struct
        {
            var replaceTable = tables is null || chunk >= tables.Length;
            var oldCapacity = tables?.Length ?? 0;
            var capacity = oldCapacity == 0 ? ChunkTableCapacity :
                oldCapacity <= Array.MaxLength / 2 ? oldCapacity * 2 : Array.MaxLength;
            if (replaceTable && capacity <= chunk) { breach = Utf8ExecutionLimitKind.WorkingBytes; return false; }
            var tableBytes = replaceTable ? Utf8ConstructionBudget.ArrayBytes<T[]>(capacity) : 0;
            var bytes = checked(tableBytes + Utf8ConstructionBudget.ArrayBytes<T>(chunkCapacity));
            breach = invocation.TryReserveWorking(bytes);
            if (breach != Utf8ExecutionLimitKind.None) return false;
            var adopted = false;
            try
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                var candidate = replaceTable ? new T[capacity][] : tables ?? throw new InvalidOperationException("Live slice stack table is missing.");
                if (replaceTable && tables is not null)
                {
                    for (var offset = 0; offset < oldCapacity;)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return false;
                        var length = Math.Min(256, oldCapacity - offset);
                        tables.AsSpan(offset, length).CopyTo(candidate.AsSpan(offset, length));
                        offset += length;
                    }
                }
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                // Keep the common first frame leaf within the shared retention
                // bounds. The table and all other chunks stay invocation-owned.
                var leaf = typeof(T) == typeof(Utf8SliceFrame) && chunk == 0
                    ? Utf8ExactStatePool<T>.Rent(chunkCapacity, invocation.HasUnlimitedWorkingMemory)
                    : new T[chunkCapacity];
                // Publish only after acquisition and copy complete. Failed growth
                // leaves the original table, count and parent reservation intact.
                var oldBytes = replaceTable ? Utf8ConstructionBudget.ArrayBytes<T[]>(oldCapacity) : 0;
                candidate[chunk] = leaf;
                tables = candidate;
                invocation.Ledger.Release(oldBytes);
                reservedBytes = checked(reservedBytes + bytes - oldBytes);
                adopted = true;
                return true;
            }
            finally { if (!adopted) invocation.Ledger.Release(bytes); }
        }
    }

    private static Utf8SliceMatchOutcome RunBackreference(Utf8SliceProgram program,
        in Utf8SliceInstruction instruction, ReadOnlySpan<byte> input,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds, int inputIndex, ref Utf8SliceInvocation invocation)
    {
        // ECMAScript permits a defined group that has not completed a capture
        // yet to match an empty reference. Other unmatched references fail.
        if (captureStarts[instruction.A] < 0 || captureEnds[instruction.A] < 0)
            return new(instruction.C != 0, instruction.C != 0 ? inputIndex : -1,
                Utf8ExecutionLimitKind.None);

        var referStart = Math.Min(captureStarts[instruction.A], captureEnds[instruction.A]);
        var referEnd = Math.Max(captureStarts[instruction.A], captureEnds[instruction.A]);
        var referLength = referEnd - referStart;
        var reverse = program.RightToLeft;
        if (instruction.B == 0)
        {
            var available = reverse ? inputIndex : input.Length - inputIndex;
            if (referLength > available) return new(false, -1, Utf8ExecutionLimitKind.None);

            // Scalar-aligned captures have a single UTF-8 encoding. Use the
            // same bounded ordinal comparison as the literal search paths.
            var start = reverse ? inputIndex - referLength : inputIndex;
            var end = reverse ? start : inputIndex + referLength;
            var breach = Utf8SearchExecutor.TryCompareLiteral(input.Slice(start, referLength),
                input.Slice(referStart, referLength), false, ref invocation, out var equal, reverse);
            return new(equal, equal ? end : -1, breach);
        }

        if (reverse)
        {
            // Mirrored captures record their right edge at opening and left
            // edge at closing. Compare both spans from their own scalar ends.
            var capIndex = referEnd;
            var backIndex = inputIndex;
            while (capIndex > referStart)
            {
                var charge = invocation.TryChargeStep();
                if (charge != Utf8ExecutionLimitKind.None) return new(false, -1, charge);
                if (backIndex <= 0 ||
                    !TryReadScalar(input, capIndex, true, out var captureScalar, out var capWidth) ||
                    capIndex - capWidth < referStart ||
                    !TryReadScalar(input, backIndex, true, out var inputScalar, out var inWidth) ||
                    !BackreferenceScalarsEqual(program, instruction, captureScalar, inputScalar))
                    return new(false, -1, Utf8ExecutionLimitKind.None);
                capIndex -= capWidth;
                backIndex -= inWidth;
            }
            return new(true, backIndex, Utf8ExecutionLimitKind.None);
        }
        else
        {
            // Each side advances by its own UTF-8 scalar width (for example k
            // versus Kelvin), using the same existing profile equivalence.
            var capIndex = referStart;
            var backIndex = inputIndex;
            while (capIndex < referEnd)
            {
                var charge = invocation.TryChargeStep();
                if (charge != Utf8ExecutionLimitKind.None) return new(false, -1, charge);
                if (backIndex >= input.Length ||
                    !TryReadScalar(input, capIndex, false, out var captureScalar, out var capWidth) ||
                    capIndex + capWidth > referEnd ||
                    !TryReadScalar(input, backIndex, false, out var inputScalar, out var inWidth) ||
                    !BackreferenceScalarsEqual(program, instruction, captureScalar, inputScalar))
                    return new(false, -1, Utf8ExecutionLimitKind.None);
                capIndex += capWidth;
                backIndex += inWidth;
            }
            return new(true, backIndex, Utf8ExecutionLimitKind.None);
        }
    }

    private static bool BackreferenceScalarsEqual(Utf8SliceProgram program,
        Utf8SliceInstruction instruction, Rune captured, Rune input)
    {
        if (instruction.D == 0) return Utf8SliceCaseEquivalences.ScalarsEquivalent(captured, input, program.CaseBehavior);
        var lower = program.BackreferenceLowerings[instruction.D - 1];
        return lower(captured.Value) == lower(input.Value);
    }

}
