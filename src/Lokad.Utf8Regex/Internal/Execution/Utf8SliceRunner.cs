using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
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
    internal Utf8SliceMatchOutcome(bool isMatch, int endIndex, Utf8ExecutionLimitKind breach, int[][]? captureStarts, int[][]? captureLengths)
    {
        IsMatch = isMatch;
        EndIndex = endIndex;
        Breach = breach;
        CaptureStarts = captureStarts;
        CaptureLengths = captureLengths;
    }

    internal bool IsMatch { get; }
    internal int EndIndex { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
    internal int[][]? CaptureStarts { get; }
    internal int[][]? CaptureLengths { get; }
}

internal readonly struct Utf8SliceSlots
{
    internal Utf8SliceSlots(int[] starts, int[] ends)
    {
        Starts = starts;
        Ends = ends;
    }

    internal int[] Starts { get; }
    internal int[] Ends { get; }
}

internal readonly struct Utf8SliceSearchOutcome
{
    internal Utf8SliceSearchOutcome(
        bool found, int startByte, int endByte,
        int[][]? captureStarts, int[][]? captureLengths, Utf8ExecutionLimitKind breach)
    {
        Found = found;
        StartByte = startByte;
        EndByte = endByte;
        CaptureStarts = captureStarts;
        CaptureLengths = captureLengths;
        Breach = breach;
    }

    internal bool Found { get; }
    internal int StartByte { get; }
    internal int EndByte { get; }
    internal int[][]? CaptureStarts { get; }
    internal int[][]? CaptureLengths { get; }
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
            slots = new Utf8SliceSlots(Array.Empty<int>(), Array.Empty<int>());
            return Utf8ExecutionLimitKind.None;
        }
        return TryRentNonemptySlots(groups, ref invocation, out slots);
    }

    private static Utf8ExecutionLimitKind TryRentNonemptySlots(int groups, ref Utf8SliceInvocation invocation, out Utf8SliceSlots slots)
    {
        slots = default;
        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(groups) * 2);
        var breach = invocation.TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            return breach;
        }

        try
        {
            var starts = new int[groups];
            var ends = new int[groups];
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            slots = new Utf8SliceSlots(starts, ends);
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
        if (slots.Starts.Length != 0)
        {
            invocation.Ledger.Release(checked(Utf8ConstructionBudget.ArrayBytes<int>(slots.Starts.Length)
                + Utf8ConstructionBudget.ArrayBytes<int>(slots.Ends.Length)));
        }
    }

    internal static void ReleaseHistory(int[][]? starts, int[][]? lengths, ref Utf8SliceInvocation invocation)
    {
        if (starts is null || lengths is null)
        {
            return;
        }

        invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int[]>(starts.Length));
        invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int[]>(lengths.Length));
        for (var group = 0; group < starts.Length && group < lengths.Length; group++)
        {
            if (starts[group].Length != 0)
            {
                invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int>(starts[group].Length));
            }

            if (lengths[group].Length != 0)
            {
                invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int>(lengths[group].Length));
            }
        }
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

    private static bool MatchesBoundary(Utf8SliceProgram program, in Utf8SliceInstruction instruction,
        ReadOnlySpan<byte> input, int index)
    {
        if (Utf8ScalarWordBoundary.TryGetAsciiBoundary(input, index, out var asciiBoundary))
            return asciiBoundary == (instruction.A != 0);

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
        int[] captureStarts,
        int[] captureEnds,
        bool startIsScalarBoundary,
        ref Utf8SliceInvocation invocation,
        bool collectHistory = false,
        bool requireNonEmpty = false)
    {
        if (program.IsNonBacktracking)
        {
            if (!startIsScalarBoundary || (uint)start > (uint)input.Length ||
                start < input.Length && (input[start] & 0xC0) == 0x80)
                return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
            var outcome = RunNonBacktracking(program, input, start, captureStarts, captureEnds,
                true, requireNonEmpty, ref invocation);
            return new(outcome.Found, outcome.EndByte, outcome.Breach, null, null);
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
        int[] captureStarts,
        int[] captureEnds,
        bool collectHistory,
        bool requireNonEmpty,
        bool startIsScalarBoundary,
        ref AttemptWorkspace workspace,
        ref Utf8SliceInvocation invocation, bool existenceOnly = false,
        (int Instruction, int Bytes) verifiedPrefix = default, bool requireFullInput = false)
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
            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None, null, null);

        if (!workspace.Initialized)
        {
            var breach = workspace.Initialize(program, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, breach, null, null);
        }
        var counts = workspace.Counts;
        var positions = workspace.Positions;
        var heads = workspace.Heads;
        if (counts.Length != 0) Array.Fill(counts, -1);
        if (positions.Length != 0) Array.Fill(positions, -1);
        if (captureStarts.Length != 0) Array.Fill(captureStarts, -1);
        if (captureEnds.Length != 0) Array.Fill(captureEnds, -1);
        if (heads.Length != 0) Array.Fill(heads, -1);
        workspace.Stacks.ResetAttempt();
        var completed = false;
        try
        {
            Utf8SliceMatchOutcome attempt;
            var canStopEarly = existenceOnly && !collectHistory && !requireNonEmpty && !requireFullInput;
            int? requiredEnd = requireFullInput ? input.Length : null;
            var terminalRoot = collectHistory ? Utf8SliceTerminalRootKind.None : program.TerminalRoot;
            if (terminalRoot == Utf8SliceTerminalRootKind.Capture)
            {
                // A proved complete root has no continuation or capture rollback.
                attempt = RunTerminalCaptureRoot(program, input, start, captureStarts, captureEnds,
                    ref workspace.Stacks, ref invocation, canStopEarly);
            }
            else if (terminalRoot == Utf8SliceTerminalRootKind.Boundary)
            {
                // Keep initial workspace admission and reset above. This complete
                // root has no continuation or consuming nonempty alternative. Preserve
                // VM step/deadline cadence; callers retain final publication checks.
                var breach = invocation.TryChargeStep();
                invocation.Budget.ThrowIfCancellationRequested();
                if (breach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, breach, null, null);
                var matched = !requireNonEmpty && MatchesBoundary(program, program.Instructions[0], input, start);
                breach = invocation.TryChargeStep();
                invocation.Budget.ThrowIfCancellationRequested();
                attempt = new(matched && breach == Utf8ExecutionLimitKind.None,
                    matched && breach == Utf8ExecutionLimitKind.None ? start : -1, breach, null, null);
            }
            else if (terminalRoot == Utf8SliceTerminalRootKind.ClassRepeat)
            {
                // This complete repeat has no assertion continuation to drive.
                attempt = RunTerminalClassRepeat(program, input, start, requireNonEmpty, ref invocation, canStopEarly);
            }
            else if (!collectHistory && program.Assertions.Length == 0)
            {
                // This root cannot suspend or publish capture history. Execute
                // the same VM without the unused assertion-continuation driver.
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
                        false, preserveCaptureTrail: false, ref workspace.Stacks, ref invocation, out _, canStopEarly);
                }
                finally
                {
                    workspace.Counts = current.Counts;
                    workspace.Positions = current.Positions;
                }
            }
            else
                attempt = RunFrom(program, input, start, captureStarts, captureEnds,
                    collectHistory, requireNonEmpty, startIsScalarBoundary, ref workspace, ref invocation, requiredEnd);
            // Complete terminal proofs return a unique or maximal extent. The
            // scalar VM tests the end within Accept so other outcomes can retry.
            if (requireFullInput && attempt.IsMatch && attempt.EndIndex != input.Length)
                return new(false, -1, attempt.Breach, null, null);
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
                if (captureStarts.Length != 0) Array.Fill(captureStarts, -1);
                if (captureEnds.Length != 0) Array.Fill(captureEnds, -1);
            }
        }
    }

    // Callers supply valid UTF-8. Candidates and results use original byte
    // coordinates; no validity pass or subject coordinate map is required.
    internal static Utf8SliceSearchOutcome TryFindFirst(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        int[] captureStarts, int[] captureEnds, bool collectHistory,
        ref Utf8SliceInvocation invocation, in Utf8SearchPlan candidatePlan = default,
        bool existenceOnly = false, int screenedLiteral = -1)
    {
        var workspace = new AttemptWorkspace();
        try
        {
            return TryFindFirst(program, input, fromByte, captureStarts, captureEnds,
                collectHistory, ref workspace, ref invocation, candidatePlan, existenceOnly, screenedLiteral);
        }
        finally { workspace.Release(ref invocation); }
    }

    // Repeated searches for one program/invocation can keep their admitted
    // scratch. The caller releases it; each attempt still resets all VM state.
    internal static Utf8SliceSearchOutcome TryFindFirst(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        int[] captureStarts, int[] captureEnds, bool collectHistory,
        ref AttemptWorkspace workspace, ref Utf8SliceInvocation invocation, in Utf8SearchPlan candidatePlan = default,
        bool existenceOnly = false, int screenedLiteral = -1)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)fromByte > (uint)input.Length ||
            fromByte < input.Length && (input[fromByte] & 0xC0) == 0x80)
            return new(false, -1, -1, null, null, Utf8ExecutionLimitKind.None);
        if (program.IsNonBacktracking)
        {
            if (captureStarts.Length == 0 && program.NonBacktrackingOrderedClosures.Length != 0)
                return FindOrderedRegularRange(program, input, fromByte, ref invocation);
            return RunNonBacktracking(program, input, fromByte, captureStarts, captureEnds,
                false, false, ref invocation);
        }
        var remaining = program.RightToLeft ? fromByte : input.Length - fromByte;
        if (CanSearchOrdinaryOrdered(program, remaining, candidatePlan, ref invocation) &&
            CanAdmitOrderedRows(program, ref invocation))
        {
            var selected = FindOrderedRegularRange(program, input, fromByte, ref invocation);
            if (!selected.Found || captureStarts.Length == 0 && !collectHistory) return selected;
            // Captures never affect discovery for this graph. Run the chosen
            // start on the original subject to publish ordinary capture history.
            var origin = program.RightToLeft ? selected.EndByte : selected.StartByte;
            var replay = TryMatchAt(program, input, origin, captureStarts, captureEnds,
                collectHistory, false, true, ref workspace, ref invocation);
            return replay.IsMatch
                ? new(true, Math.Min(origin, replay.EndIndex), Math.Max(origin, replay.EndIndex),
                    replay.CaptureStarts, replay.CaptureLengths, replay.Breach)
                : new(false, -1, -1, null, null, replay.Breach);
        }
        var first = program.Instructions[0];
        var seekBeginning = !program.RightToLeft && first.Kind == Utf8SliceInstructionKind.Anchor &&
            first.A == (int)Utf8SliceAnchorKind.Beginning;
        var atContiguousOrigin = first is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Contiguous } &&
            !program.RightToLeft && invocation.AnchorOriginByte == fromByte;
        if (seekBeginning && first.B == 0 || atContiguousOrigin)
        {
            // An absolute beginning or an explicit contiguous origin admits
            // one start. Keep its normal attempt and interruption checks.
            if (seekBeginning)
            {
                var seekBreach = TrySeekBeginning(program, input, ref fromByte, ref invocation);
                if (seekBreach != Utf8ExecutionLimitKind.None || fromByte < 0)
                    return new(false, -1, -1, null, null, seekBreach);
            }
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, -1, null, null, breach);
            invocation.Budget.ThrowIfCancellationRequested();
            var proofStart = -1;
            if (seekBeginning && existenceOnly && !collectHistory)
            {
                if (program.GroupCount == 0 && program.SkipsTerminalRewind(1) &&
                    program.Instructions[1].RepeatMaximum <= 4 && input.Length <= 32)
                    proofStart = 1;
                else if (captureStarts.Length == 0)
                    proofStart = FindAnchoredRepeatProofStart(program, input.Length);
            }
            if (proofStart >= 0)
            {
                // The admitted short witness starts after the confirmed anchor.
                // Unknown instructions still enter the original attempt.
                var possible = MayMatchBranchedAsciiPrefix(program, input, fromByte, proofStart, out var acceptedEnd, proveMatch: true);
                if (!possible || acceptedEnd >= 0)
                {
                    breach = invocation.TryReserveWorking(0);
                    if (breach == Utf8ExecutionLimitKind.None) breach = invocation.TryChargeInputStep();
                    invocation.Budget.ThrowIfCancellationRequested();
                    return possible && breach == Utf8ExecutionLimitKind.None
                        ? new(true, fromByte, acceptedEnd, null, null, breach)
                        : new(false, -1, -1, null, null, breach);
                }
            }
            var outcome = TryMatchAt(program, input, fromByte, captureStarts, captureEnds,
                collectHistory, false, true, ref workspace, ref invocation, existenceOnly);
            return outcome.IsMatch && outcome.Breach == Utf8ExecutionLimitKind.None
                ? new(true, fromByte, outcome.EndIndex, outcome.CaptureStarts, outcome.CaptureLengths, Utf8ExecutionLimitKind.None)
                : new(false, -1, -1, null, null, outcome.Breach);
        }
        var (leading, leadingLast, second, secondLast, leadingScalarIndex, fromBranch, sparsePair) = program.LeadingByteCandidate;
        // Prefer a straight ASCII proof. Prepared literal search can be more
        // selective than a branch interval or the lead of a wider scalar.
        var seekLiteral = (leading is < 0 or >= 128 || fromBranch) && !seekBeginning && CanSeekExactPrefix(program, candidatePlan);
        if (seekLiteral) leading = -1;
        var checkBranchedPrefix = leading is >= 0 and < 128 && program.Instructions[leadingScalarIndex] is
            { Kind: Utf8SliceInstructionKind.Fork } or { IsLiteralChoice: true };
        var lastProbedStart = input.Length - candidatePlan.MinRequiredLength;
        var leadingClass = -1;
        var classLiteral = -1;
        var classSearch = default(PreparedSubstringSearch?);
        byte[][]? classAlternatives = null;
        (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd) repeatPrefix = default;
        long classGapMaximum = 0;
        var directClassLiteral = false;
        var singleWindowAttempt = false;
        var requiredClassSuffix = default((int First, int Second));
        var verifiedClassLiteralLength = 0;
        var screenedDelimiter = -1;
        var wideInput = input.Length - fromByte >= 256;
        var needsClassWindow = leading is < 0 or >= 128;
        var alignedByteDelimiter = program.LeadingClassDelimiter is { Direct: true, Literal: >= 0 } &&
            candidatePlan.LiteralSearch is { Length: 1, IgnoreCase: false };
        if ((needsClassWindow || alignedByteDelimiter) && !seekLiteral && !seekBeginning && !program.RightToLeft &&
            !program.IgnoreCase && !program.HasContiguousAnchor &&
            (wideInput || candidatePlan.LiteralSearch is { Length: > 0, IgnoreCase: false } ||
                candidatePlan.RequiredPrefilterSearcher.Kind == PreparedSearcherKind.MultiLiteral))
        {
            var (literal, windowClass, literalIndex, gapMaximum, direct) = program.LeadingClassDelimiter;
            leadingClass = windowClass;
            classGapMaximum = gapMaximum;
            directClassLiteral = direct;
            if (leadingClass >= 0 && literal >= 0)
            {
                classSearch = GetAlignedDelimiterSearch(program, literalIndex, candidatePlan);
                if (directClassLiteral && classSearch is { } aligned)
                {
                    var proofBreach = TryProveSingleWindowAttempt(program, leadingClass, (byte)literal,
                        ref invocation, out singleWindowAttempt);
                    if (proofBreach != Utf8ExecutionLimitKind.None)
                        return new(false, -1, -1, null, null, proofBreach);
                    if (singleWindowAttempt)
                    {
                        requiredClassSuffix = GetRequiredHybridSuffix(program.Instructions, literalIndex + aligned.Length);
                        if (existenceOnly && !collectHistory && CanReuseLeadingClassWindow(program, leadingClass, literalIndex))
                        {
                            verifiedClassLiteralLength = aligned.Length;
                            // The prefilter already searched this exact needle
                            // from byte zero. A later floor may exclude its hit.
                            if (aligned.Length > 1 && screenedLiteral >= fromByte)
                                screenedDelimiter = screenedLiteral;
                        }
                    }
                }
                // One attempt per disjoint delimiter can replace a broad
                // leading-byte proof, including on the final cursor advances.
                if (singleWindowAttempt || needsClassWindow &&
                    (directClassLiteral && classSearch is { Length: 1 } ||
                     wideInput && (directClassLiteral || classSearch is not null)))
                    classLiteral = literal;
            }
            else if (leadingClass >= 0 && literalIndex >= 0 &&
                candidatePlan.RequiredPrefilterSearcher.Kind == PreparedSearcherKind.MultiLiteral)
            {
                var proofBreach = invocation.TryChargeInputStep();
                if (proofBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, null, null, proofBreach);
                classAlternatives = GetAlignedDelimiterAlternatives(program, literalIndex, candidatePlan.RequiredPrefilterSearcher);
            }
            if (needsClassWindow && classLiteral < 0 && classAlternatives is null &&
                GetRepeatPrefixSearch(program, candidatePlan, out repeatPrefix) is { } prefixSearch)
            {
                classSearch = prefixSearch;
                classLiteral = prefixSearch.Needle[0];
            }
        }
        // Keep existing literal guidance when available. The decimal scan
        // lands at an ASCII digit or a wider scalar to classify below.
        var hasClassWindow = classLiteral >= 0 || classAlternatives is not null;
        if (hasClassWindow && leading >= 128) leading = -1;
        var screenDecimalWindow = hasClassWindow && first is { Kind: Utf8SliceInstructionKind.Boundary, A: 1 } &&
            leadingClass == leadingScalarIndex && program.LeadingScalarIsDecimal;
        var followingWindow = screenDecimalWindow && directClassLiteral && classLiteral >= 0 &&
            !Utf8AsciiBytePredicates.IsDigit((byte)classLiteral)
            ? program.FollowingDecimalLiteralWindow : default;
        var seekDecimal = leading < 0 && !seekLiteral && !hasClassWindow &&
            program.LeadingScalarIsDecimal;
        var canProveDecimal = !collectHistory && captureStarts.Length == 0 && !program.ReadsCaptures;
        var shortDecimalHead = canProveDecimal && seekDecimal &&
            program.Instructions.Length <= 32 && leadingScalarIndex > 0 &&
            program.Instructions[leadingScalarIndex - 1] is
                { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: > 0, RepeatMaximum: <= 4 };
        var proveDecimalWindow = canProveDecimal && followingWindow.Min != 0 && program.SkipsTerminalRewind(1);
        var reuseDecimalWindow = proveDecimalWindow && leadingClass == 2 && program.Instructions[1].RepeatMinimum == 1;
        var verifyFollowingWindow = reuseDecimalWindow && program.FollowingDecimalContinuation != 0 &&
            !repeatPrefix.Any.HasValue && classAlternatives is null && classLiteral == program.LeadingClassDelimiter.Literal;
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
        if (leading >= 0 || seekLiteral || hasClassWindow)
        {
            // A miss can avoid local scratch, but not an enclosing storage loan.
            var admission = invocation.TryReserveWorking(0);
            if (admission != Utf8ExecutionLimitKind.None)
                return new(false, -1, -1, null, null, admission);
        }
        var classWindowEnd = -1;
        var followingWindowEnd = 0;
        for (var position = fromByte; position >= 0; position = AdvancePastScalar(input, position, program.RightToLeft))
        {
            // The required delimiter proves a window of possible starts.
            // Verify every survivor against the complete subject so later
            // matches, captures and assertion context keep their usual order.
            while (hasClassWindow && position > classWindowEnd)
            {
                var seekBreach = TrySeekLeadingClassLiteralWindow(program, input, leadingClass,
                    (byte)classLiteral, classSearch, classGapMaximum, directClassLiteral,
                    ref position, ref invocation, out classWindowEnd, repeatPrefix, classAlternatives,
                    requiredClassSuffix, screenedDelimiter);
                screenedDelimiter = -1;
                if (seekBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, null, null, seekBreach);
                if (position < 0)
                    return new(false, -1, -1, null, null,
                        new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                // A decimal repeat cannot consume this delimiter. Every start
                // in its window must reach the next literal from the same suffix.
                if (followingWindow.Min != 0 && position <= classWindowEnd)
                {
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
                    return new(false, -1, -1, null, null, seekBreach);
                if (position < 0)
                    return new(false, -1, -1, null, null,
                        new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
            }
            if (leading >= 0 && ((uint)position >= (uint)input.Length ||
                !MayStartAtByte(input[position], leading, leadingLast, sparsePair) &&
                !(seekDecimal && input[position] is >= 0xc2 and <= 0xf4) ||
                !leadingPrefix.IsEmpty && !input[position..].StartsWith(leadingPrefix)))
            {
                var seekBreach = TrySeekLeadingByte(input, ref position, (byte)leading, leadingLast, seekDecimal,
                    ref invocation, leadingPrefix, sparsePair);
                if (seekBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, null, null, seekBreach);
                if (position < 0)
                    return new(false, -1, -1, null, null,
                        new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
            }
            if (seekBeginning)
            {
                var seekBreach = TrySeekBeginning(program, input, ref position, ref invocation);
                if (seekBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, null, null, seekBreach);
                if (position < 0) break;
            }
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, -1, null, null, breach);
            invocation.Budget.ThrowIfCancellationRequested();
            // Charge even rejected neighbours: dense false heads must retain
            // bounded work and the original interruption authority.
            if (leadingPrefix.IsEmpty && leading >= 0 && second >= 0 && ((uint)(position + 1) >= (uint)input.Length ||
                (uint)(input[position + 1] - second) > (uint)(secondLast - second)))
                continue;
            // A delimiter window can include interior digits and the
            // delimiter itself. ASCII starts must be digits and cannot
            // follow another ASCII word byte. The VM checks wider neighbors.
            if (screenDecimalWindow && (position == input.Length || input[position] < 128 &&
                (!Utf8AsciiBytePredicates.IsDigit(input[position]) ||
                    position > 0 && Utf8AsciiBytePredicates.IsWord(input[position - 1]))))
                continue;
            if (seekDecimal && IsNonDecimalWideStart(input, position))
                continue;
            if (shortRepeatWindow.Min != 0 && !MayReachShortRepeatLiteral(input, position, shortRepeatWindow, seekDecimal))
                continue;
            if (checkBranchedPrefix || shortDecimalHead || proveDecimalWindow && input[position] < 128)
            {
                // Starts only advance in these forward searches. The
                // input step above also covers stopping after the last one.
                if (position > lastProbedStart) break;
                var proveDecimalPrefix = shortDecimalHead || proveDecimalWindow;
                var proveMatch = proveDecimalPrefix || existenceOnly && !collectHistory && program.GroupCount == 0 && leadingScalarIndex == 0;
                var proofStart = proveDecimalPrefix ? 0 : leadingScalarIndex;
                var proofPosition = position;
                var proofLimit = BranchedPrefixInstructionLimit;
                if (reuseDecimalWindow && position < classWindowEnd)
                {
                    // Rewind already admitted a nonempty decimal run ending at
                    // this excluded delimiter. Check its leading boundary and
                    // start the bounded proof at the repeat's continuation.
                    if (!MatchesBoundary(program, first, input, position)) continue;
                    proofPosition = classWindowEnd;
                    proofStart = program.Instructions[1].A + 1;
                    if (followingWindowEnd > 0)
                    {
                        proofPosition = followingWindowEnd;
                        proofStart = program.FollowingDecimalContinuation;
                        proofLimit -= followingWindow.Max + 3;
                    }
                }
                if (!MayMatchBranchedAsciiPrefix(program, input, proofPosition, proofStart, out var acceptedEnd, proveMatch, proofLimit))
                {
                    var remainingDigits = classWindowEnd - position;
                    // The proved leading boundary excludes the interior of a
                    // short ASCII digit run. Its delimiter is an excluded byte.
                    if (reuseDecimalWindow && remainingDigits is > 0 and <= 4 &&
                        !input.Slice(position, remainingDigits).ContainsAnyExceptInRange((byte)'0', (byte)'9'))
                        position = classWindowEnd;
                    continue;
                }
                if (acceptedEnd >= 0)
                {
                    // The candidate step admitted the bounded walk. Confirm
                    // the original interruption and work before publishing it.
                    breach = invocation.TryChargeInputStep();
                    invocation.Budget.ThrowIfCancellationRequested();
                    return breach == Utf8ExecutionLimitKind.None
                        ? new(true, position, acceptedEnd, null, null, breach)
                        : new(false, -1, -1, null, null, breach);
                }
            }
            // For an initial single-class +, a start strictly before the
            // aligned delimiter admits at least one complete scalar. The
            // metered rewind proved the whole run, and the class excludes
            // the delimiter. Shorter endpoints cannot reach this literal.
            var verifiedPrefix = verifiedClassLiteralLength != 0 && position < classWindowEnd
                ? (4 + verifiedClassLiteralLength, classWindowEnd - position + verifiedClassLiteralLength)
                : default;
            var outcome = TryMatchAt(program, input, position, captureStarts, captureEnds,
                collectHistory, false, true, ref workspace, ref invocation, existenceOnly, verifiedPrefix);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, -1, null, null, outcome.Breach);
            if (outcome.IsMatch)
                return new(true, Math.Min(position, outcome.EndIndex), Math.Max(position, outcome.EndIndex),
                    outcome.CaptureStarts, outcome.CaptureLengths, Utf8ExecutionLimitKind.None);
            if (singleWindowAttempt) position = classWindowEnd;
        }
        return new(false, -1, -1, null, null, Utf8ExecutionLimitKind.None);
    }

    // -1 is the exhausted sentinel in either direction; avoid Length + 1 overflow.
    internal static int AdvancePastScalar(ReadOnlySpan<byte> input, int position, bool reverse)
        => TryReadScalar(input, position, reverse, out _, out var width)
            ? position + (reverse ? -width : width) : -1;

    // Count and primary matching share the same necessary-prefix proof. Every
    // candidate is still verified by the scalar VM under the original invocation.
    private static bool CanSeekExactPrefix(Utf8SliceProgram program, in Utf8SearchPlan candidatePlan)
        => !program.RightToLeft && !program.CanMatchEmpty &&
            !program.HasContiguousAnchor && !program.IgnoreCase && candidatePlan.CanGuideFallbackStarts &&
            candidatePlan.Distance == 0 && candidatePlan.Kind is
                Utf8SearchKind.ExactAsciiLiteral or
                Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals;

    private static Utf8ExecutionLimitKind TrySeekExactPrefix(in Utf8SearchPlan candidatePlan,
        ReadOnlySpan<byte> input, ref int position, ref Utf8SliceInvocation invocation, out int familyLength)
    {
        familyLength = 0;
        if (candidatePlan.LiteralSearch is { Length: > 0 } literal)
            return Utf8SearchExecutor.TryFindLiteralCandidate(literal, input, position,
                input.Length - literal.Length, false, ref invocation, out position);
        var breach = Utf8SearchExecutor.TryFindLiteralFamilyCandidate(
            candidatePlan.AlternateLiteralsUtf8 ?? [], false, input, position, input.Length,
            false, ref invocation, out var candidate, out var found,
            candidatePlan.AlternateLiteralSearch?.SearchData ?? default);
        position = found ? candidate.Index : -1;
        if (found) familyLength = candidate.Length;
        return breach;
    }

    // Only an initial mandatory anchor proves that other starts cannot match.
    // Callers establish LTR/backtracking eligibility. An immediate exact ASCII
    // literal can also reject a start before capture reset/workspace acquisition.
    // LF is one byte and cannot occur inside a scalar. The caller establishes
    // valid UTF-8; this filter neither restricts a match's end nor its body.
    internal static Utf8ExecutionLimitKind TrySeekBeginning(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, ref int position, ref Utf8SliceInvocation invocation)
    {
        var instructions = program.Instructions;
        var leadingLiteral = !program.IgnoreCase && !program.CanMatchEmpty &&
            instructions.Length > 1 &&
            instructions[1] is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } leading &&
            (uint)leading.A < 0x80 ? leading.A : -1;
        if (instructions[0].B == 0)
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
                input[position] == leadingLiteral) return Utf8ExecutionLimitKind.None;

            // The same required first scalar admits a checked byte-pair search.
            // Only candidate discovery changes; the original VM verifies each
            // survivor. One-byte overlap preserves pairs crossing block edges.
            Span<byte> pair = stackalloc byte[2];
            pair[0] = (byte)'\n';
            pair[1] = (byte)leadingLiteral;
            while (input.Length - position >= 2)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(2048, input.Length - position);
                var relative = input.Slice(position, length).IndexOf(pair);
                invocation.Budget.ThrowIfCancellationRequested();
                if (relative >= 0)
                { position += relative + 1; return Utf8ExecutionLimitKind.None; }
                position += length - 1;
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
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation,
        in Utf8SearchPlan candidatePlan = default,
        in PreparedSearcher branchPrefixes = default)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)fromByte > (uint)input.Length ||
            fromByte < input.Length && (input[fromByte] & 0xC0) == 0x80)
            return new Utf8SliceCountOutcome(0, Utf8ExecutionLimitKind.None);
        var count = 0;
        var position = fromByte;
        invocation.AnchorOriginByte = fromByte;
        // Nullable roots retain the caller's empty-match progression below.
        if (TryCountTerminalScalar(program, input, fromByte, ref invocation, out var terminal))
            return terminal;
        if (program.IsNonBacktracking)
        {
            Array.Fill(captureStarts, -1);
            Array.Fill(captureEnds, -1);
            if (TryCountAnchoredLine(program, input, fromByte, ref invocation, out var lineCount))
                return lineCount;
            // Wide Count uses program-sized scratch on longer subjects.
            if (captureStarts.Length == 0 && program.Instructions.Length > 64 && input.Length - fromByte >= 256)
                return CountWideNonBacktracking(program, input, fromByte, ref invocation);
            if (input.Length - fromByte >= 64)
            {
                if (program.NonBacktrackingOrderedForwardOnly)
                    return Utf8NonBacktrackingSearchState.CountInWindows(program, input, fromByte,
                        new(captureStarts, captureEnds), ref invocation);
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
        var seekVeryEnd = program.RightToLeft && first.Kind == Utf8SliceInstructionKind.Anchor &&
            first.A == (int)Utf8SliceAnchorKind.VeryEnd;
        // A distance-zero exact prefix is necessary, never sufficient. The scalar
        // VM still verifies every candidate; stateful/empty/casefold starts decline.
        // PCRE2-INTEGRATION-POINT: a caller may provide its proved branch prefixes.
        var useBranchPrefixes = branchPrefixes.HasValue;
        var seekLiteral = useBranchPrefixes || CanSeekExactPrefix(program, candidatePlan);
        var prefixContinuation = 0;
        if (seekLiteral && !useBranchPrefixes && input.Length - fromByte >= 256 &&
            program.Assertions.Length == 0 && candidatePlan.Kind == Utf8SearchKind.ExactAsciiLiterals &&
            candidatePlan.LiteralSearch is null && candidatePlan.AlternateLiteralsUtf8 is { Length: >= 2 and <= 8 } literals)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
            prefixContinuation = FindExactFamilyContinuation(program.Instructions, literals);
        }
        // A direct beginning anchor already owns bounded line/prefix discovery.
        // Keep one candidate search rather than scanning its prefix twice.
        var (leadingFirst, leadingLast, leadingSecond, leadingSecondLast, leadingScalarIndex, _, sparsePair) = seekLiteral
            ? (-1, (byte)0, -1, (byte)0, -1, false, false)
            : program.LeadingByteCandidate;
        var checkBranchedPrefix = leadingFirst is >= 0 and < 128 && program.Instructions[leadingScalarIndex] is
            { Kind: Utf8SliceInstructionKind.Fork } or { IsLiteralChoice: true };
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
            ? FindBranchedAsciiLiteralWindow(program.Instructions, leadingScalarIndex) : default;
        var checkRepeatChain = leadingFirst is >= 0 and < 128 && leadingScalarIndex == 1 && CanSkipShortRepeatChain(program);
        var (whitespaceLiteral, leadingClass, whitespaceLiteralIndex, whitespaceGapMaximum, directClassLiteral) =
            !seekLiteral && !program.RightToLeft && !program.IgnoreCase &&
            !program.HasContiguousAnchor && input.Length - fromByte >= 256
            ? program.LeadingClassDelimiter : (-1, -1, -1, 0L, false);
        // Count keeps its prepared literal search when capture markers or
        // consuming instructions separate the repeat from its delimiter.
        var literalImmediatelyAfterRepeat = whitespaceLiteral >= 0 && whitespaceLiteralIndex == leadingClass + 3;
        var classSearch = default(PreparedSubstringSearch?);
        (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd) repeatPrefix = default;
        var singleWindowAttempt = false;
        var verifiedClassLiteralLength = 0;
        var requiredClassSuffix = default((int First, int Second));
        if (whitespaceLiteral >= 0 && leadingClass >= 0)
        {
            classSearch = GetAlignedDelimiterSearch(program, whitespaceLiteralIndex, candidatePlan);
            if (!literalImmediatelyAfterRepeat && classSearch is null) whitespaceLiteral = -1;
            if (directClassLiteral && classSearch is { } aligned)
            {
                var proofBreach = TryProveSingleWindowAttempt(program, leadingClass, (byte)whitespaceLiteral,
                    ref invocation, out singleWindowAttempt);
                if (proofBreach != Utf8ExecutionLimitKind.None) return new(count, proofBreach);
                if (singleWindowAttempt)
                {
                    requiredClassSuffix = GetRequiredHybridSuffix(program.Instructions, whitespaceLiteralIndex + aligned.Length);
                    if (CanReuseLeadingClassWindow(program, leadingClass, whitespaceLiteralIndex))
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
        // Keep the existing wide delimiter search when it applies. Otherwise
        // the short window also makes decimal-or-wide-scalar seeking worthwhile.
        if (whitespaceLiteral >= 0 && leadingFirst >= 128) leadingFirst = -1;
        var shortRepeatWindow = whitespaceLiteral < 0 && leadingScalarIndex > 0 && leadingFirst < 128
            ? program.LeadingRepeatLiteralWindow : default;
        var seekDecimal = shortRepeatWindow.Min != 0 && leadingFirst < 0 && program.LeadingScalarIsDecimal;
        if (seekDecimal)
        {
            leadingFirst = '0';
            leadingLast = (byte)'9';
        }
        Span<byte> prefixStorage = stackalloc byte[16];
        var leadingPrefix = input.Length - fromByte >= 256 && whitespaceLiteral < 0 && leadingSecond >= 0
            ? GetExactLeadingLiteral(program, leadingScalarIndex, prefixStorage)
            : default;
        var (prefixRepeatAscii, prefixRepeatFollower) = GetExactPrefixRepeatHead(program, prefixContinuation);
        if (!leadingPrefix.IsEmpty && !program.HasContiguousAnchor &&
            (leadingScalarIndex == 0 || leadingScalarIndex == 1 && first.Kind == Utf8SliceInstructionKind.Boundary))
        {
            // The checked search has verified these literals. Reuse the family
            // repeat proof when only the leading boundary still needs checking.
            var afterPrefix = leadingScalarIndex + leadingPrefix.Length;
            (prefixRepeatAscii, prefixRepeatFollower) = GetExactPrefixRepeatHead(program, afterPrefix);
            if (prefixRepeatAscii.HasValue) prefixContinuation = afterPrefix;
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
        var workspace = new AttemptWorkspace();
        try
        {
            while (true)
            {
                if (leadingFirst >= 0)
                {
                    var seekBreach = TrySeekLeadingByte(input, ref position, (byte)leadingFirst, leadingLast, seekDecimal,
                        ref invocation, leadingPrefix, sparsePair);
                    if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
                    if (position < 0)
                        return new(count, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                    if (seekDecimal && IsNonDecimalWideStart(input, position))
                    {
                        position = AdvancePastScalar(input, position, false);
                        continue;
                    }
                    if (leadingFirst < 128 && (shortRepeatWindow.Min != 0 ? !MayReachShortRepeatLiteral(input, position, shortRepeatWindow, seekDecimal) :
                        (leadingPrefix.IsEmpty && leadingSecond >= 0 && ((uint)(position + 1) >= (uint)input.Length ||
                        (uint)(input[position + 1] - leadingSecond) > (uint)(leadingSecondLast - leadingSecond))) ||
                        (branchWindow.Min != 0 && !HasAsciiLiteralInWindow(input, position, branchWindow)) ||
                        !checkRepeatChain && !(checkBranchedPrefix ? MayMatchBranchedAsciiPrefix(program, input, position, leadingScalarIndex, out _) :
                            MatchesStraightAsciiPrefix(program, input, position, 0))))
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
                            ref position, ref invocation, out whitespaceWindowEnd, repeatPrefix, requiredSuffix: requiredClassSuffix);
                        if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
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
                if (seekLiteral)
                {
                    Utf8ExecutionLimitKind seekBreach;
                    if (useBranchPrefixes)
                    {
                        seekBreach = branchPrefixes.TryFindMatch(input, position, input.Length, false,
                            ref invocation, out var candidate, out var found);
                        position = found ? candidate.Index : -1;
                    }
                    else seekBreach = TrySeekExactPrefix(candidatePlan, input, ref position, ref invocation, out prefixBytes);
                    if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
                    // Keep the final charge and recheck the original token after
                    // its diagnostic hook before publishing any accumulated count.
                    if (position < 0)
                        return new(count, new Utf8InvocationBufferControl(ref invocation).TryChargeWorkspaceStep());
                }
                if (seekBeginning)
                {
                    var seekBreach = TrySeekBeginning(program, input, ref position, ref invocation);
                    if (seekBreach != Utf8ExecutionLimitKind.None) return new(count, seekBreach);
                    if (position < 0) break;
                }
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None)
                    return new Utf8SliceCountOutcome(count, breach);
                invocation.Budget.ThrowIfCancellationRequested();
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
                if (prefixContinuation != 0 && first.Kind == Utf8SliceInstructionKind.Boundary &&
                    !MatchesBoundary(program, first, input, position))
                {
                    // The verified literal begins with a complete ASCII scalar.
                    position++;
                    continue;
                }
                var verifiedPrefix = prefixContinuation == 0 ? default : (prefixContinuation, prefixBytes);
                // The rewind proved this nonempty class run up to the
                // excluded delimiter. Continue with the original suffix.
                if (verifiedClassLiteralLength != 0 && position < whitespaceWindowEnd)
                    verifiedPrefix = (4 + verifiedClassLiteralLength, whitespaceWindowEnd - position + verifiedClassLiteralLength);
                if (prefixRepeatAscii.HasValue)
                {
                    var extra = ScanExactPrefixRepeatHead(input[(position + prefixBytes)..],
                        prefixRepeatAscii, prefixRepeatFollower);
                    if (extra < 0)
                    {
                        position++;
                        continue;
                    }
                    if (extra != 0)
                        verifiedPrefix = (prefixContinuation + 5, prefixBytes + extra);
                }
                var outcome = TryMatchAt(program, input, position, captureStarts, captureEnds, false, false, true,
                    ref workspace, ref invocation, verifiedPrefix: verifiedPrefix);
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                    return new Utf8SliceCountOutcome(count, outcome.Breach);
                invocation.Budget.ThrowIfCancellationRequested();
                if (outcome.IsMatch)
                {
                    count++;
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
            return new Utf8SliceCountOutcome(count, Utf8ExecutionLimitKind.None);
        }

        finally { workspace.Release(ref invocation); }
    }

    private static Utf8SliceMatchOutcome RunSegment(
        ReadOnlySpan<byte> input, int[] captureStarts, int[] captureEnds, int[] balanceHeads,
        ref AssertionCursor cursor, bool resumeFailure, bool preserveCaptureTrail,
        ref ChunkedStacks stacks, ref Utf8SliceInvocation invocation, out bool suspended, bool existenceOnly = false)
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
        Utf8ExecutionLimitKind failure;
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
                    // A nonzero range operand proves a complete positive ASCII
                    // interval. One checked byte decides it in either direction.
                    if (instruction.D != 0)
                    {
                        var rangeOffset = program.RightToLeft ? inputIndex - 1 : inputIndex;
                        var firstByte = (instruction.D & 255) - 1;
                        var lastByte = instruction.D >> 8;
                        if ((uint)rangeOffset < (uint)input.Length &&
                            (uint)(input[rangeOffset] - firstByte) <= (uint)(lastByte - firstByte))
                        {
                            inputIndex = program.RightToLeft ? rangeOffset : rangeOffset + 1;
                            programIndex++;
                        }
                        else failed = true;
                        break;
                    }
                    if (TryReadScalar(input, inputIndex, program.RightToLeft, out var setScalar, out var setWidth) &&
                        Utf8SliceScalarClasses.MatchesMask(setScalar, program.SetMasks[instruction.A], instruction.C != 0, program.CaseBehavior) != (instruction.B != 0))
                    {
                        inputIndex += program.RightToLeft ? -setWidth : setWidth;
                        programIndex++;
                    }
                    else failed = true;
                    break;

                case Utf8SliceInstructionKind.Fork:
                    if (instruction is { B: 1, C: > 0 })
                    {
                        // Both forward-lookahead layouts end their body two
                        // instructions before the failed-body target.
                        var assertionBreach = TryMatchInlineLiteralAssertion(instructions,
                            programIndex + 1, instruction.A - 2, instruction.C - 1, input, inputIndex,
                            false, null, (uint)stacks.FrameCount + 1, ref invocation, out var matched);
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
                        if ((instruction.C | instruction.D) != 0)
                        {
                            var remaining = input.Length - inputIndex;
                            if (instruction.C < 0)
                            {
                                // Only the alternate starts with this byte.
                                // The fallthrough excludes it, so neither choice needs a retry.
                                programIndex = remaining > 0 && input[inputIndex] == ~instruction.C
                                    ? instruction.A : programIndex + 1;
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
                            allowAcceptProof, out var provesAccept);
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
                            Utf8SliceMatchOutcome repeated;
                            int continuation;
                            bool ranWithoutRewind;
                            // Both proofs verify the atom and exit during preparation.
                            // Only the terminal proof depends on the absence of a required endpoint.
                            if ((cursor.RequiredEnd is null && program.SkipsTerminalRewind(programIndex)) ||
                                instructions[instruction.A].RepeatExitRejectsShorterEndpoints)
                            {
                                var run = RunClassRepeat(program, programIndex, input, inputIndex, ref invocation);
                                repeated = new(run.EndIndex >= 0, run.EndIndex, run.Breach, null, null);
                                continuation = instruction.A + 1;
                                ranWithoutRewind = true;
                            }
                            else
                            {
                                ranWithoutRewind = TryRunClassRepeatWithoutRewind(program, programIndex, input,
                                    inputIndex, cursor.RequiredEnd, ref invocation, out repeated, out continuation,
                                    existenceOnly);
                            }
                            if (ranWithoutRewind)
                            {
                                if (repeated.Breach != Utf8ExecutionLimitKind.None) return repeated;
                                if (repeated.IsMatch)
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
                                var breach = invocation.TryChargeStep();
                                if (breach != Utf8ExecutionLimitKind.None)
                                { failure = breach; goto Failure; }
                                var suffixLength = 0;
                                if (TryReadScalar(input, inputIndex, false, out var scalar, out var width))
                                {
                                    breach = TryMatchScalarClass(program, instructions[programIndex + 1], scalar,
                                        ref invocation, out var hit);
                                    if (breach != Utf8ExecutionLimitKind.None)
                                    { failure = breach; goto Failure; }
                                    if (hit)
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
                                            else if ((lazy || cursor.RequiredEnd.HasValue || scalar.Value == '\n' ||
                                                !program.HasTerminalGreedyDot(continuation)) &&
                                                !stacks.PushFrame(new Utf8SliceFrame
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
                                if (run.EndIndex > minimumEnd && !stacks.PushFrame(new Utf8SliceFrame
                                {
                                    ResumeKind = Utf8SliceResumeKind.ScalarRepeatRewind,
                                    Target = continuation,
                                    InputIndex = run.EndIndex,
                                    TrailCount = stacks.TrailCount,
                                    Aux = minimumEnd,
                                    Count = instructions[instruction.A].RepeatExitRewindStarts,
                                    JournalCount = stacks.JournalCount,
                                }, ref invocation))
                                { failure = stacks.Breach; goto Failure; }
                                inputIndex = run.EndIndex;
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
                        stacks.CompactScalarRepeatExit(frameBase, repeat.A, positions[repeat.B], input, inputIndex) &&
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
                    // Empty buffers mean the caller proved these values are
                    // unobserved. The instruction still spends its normal work.
                    if (captureStarts.Length == 0) { programIndex++; break; }
                    if (program.UsesCaptureOpeningJournal)
                    {
                        if (!stacks.PushTrailEntry(new Utf8SliceTrailEntry { Slot = instruction.A, NewValue = inputIndex, IsOpening = true }, ref invocation))
                        { failure = stacks.Breach; goto Failure; }
                        programIndex++;
                        break;
                    }
                    // The assertion/history driver retains every entry. In an
                    // assertion-free latest-value root, a plain start slot needs
                    // undo only while a frame can return to an earlier value.
                    // Closing entries still preserve multi-group completion order.
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
                    if (captureStarts.Length == 0) { programIndex++; break; }
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
                        if ((preserveCaptureTrail || captureStarts.Length != 1 || balanceHeads.Length != 0 || stacks.FrameCount != 0) &&
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
                        if (!TryReadScalar(input, inputIndex, program.RightToLeft, out var classScalar, out var classWidth))
                        {
                            failed = true;
                            break;
                        }

                        var classBreach = TryMatchScalarClass(program, instruction, classScalar,
                            ref invocation, out var classHit);
                        if (classBreach != Utf8ExecutionLimitKind.None)
                            { failure = classBreach; goto Failure; }

                        if (classHit)
                        {
                            inputIndex += program.RightToLeft ? -classWidth : classWidth;
                            programIndex++;
                        }
                        else failed = true;
                        break;
                    }

                case Utf8SliceInstructionKind.Backref:
                    {
                        var referenced = RunBackreference(program, instruction, input,
                            captureStarts, captureEnds, inputIndex, ref invocation);
                        if (referenced.Breach != Utf8ExecutionLimitKind.None) return referenced;
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
                    return default;
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
                    return new Utf8SliceMatchOutcome(true, inputIndex, Utf8ExecutionLimitKind.None, null, null);
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
        return new(false, -1, failure, null, null);
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

        public AttemptWorkspace()
        {
            Counts = [];
            Positions = [];
            Heads = [];
            Stacks = default;
            AssertionContinuations = default;
            Initialized = false;
            _reservation = 0;
        }

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

        internal bool CompactScalarRepeatExit(int frameBase, int exit, int start, ReadOnlySpan<byte> input, int end)
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
            latest.Count = 0;
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

        internal void TruncateTrail(int count, int[] captureStarts, int[] captureEnds, int[] balanceHeads)
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

        internal Utf8ExecutionLimitKind MaterializeHistory(int groups,
            ref Utf8SliceInvocation invocation, out int[][] captureStarts, out int[][] captureLengths)
        {
            captureStarts = [];
            captureLengths = [];
            if (groups == 0) return Utf8ExecutionLimitKind.None;
            var ownedBytes = checked(Utf8ConstructionBudget.ArrayBytes<int[]>(groups) * 2);
            var temporaryBytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(groups) * 2);
            var resultAdmission = invocation.Budget.TryChargeResultBytes(checked(ownedBytes + temporaryBytes));
            if (resultAdmission != Utf8ExecutionLimitKind.None) return resultAdmission;
            var breach = invocation.TryReserveWorking(checked(ownedBytes + temporaryBytes));
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            int[]? rentedCounts = null;
            int[]? rentedOpen = null;
            var retain = invocation.HasUnlimitedWorkingMemory;
            var published = false;
            try
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                captureStarts = new int[groups][];
                captureLengths = new int[groups][];
                var counts = Utf8ExactStatePool<int>.Rent(groups, retain);
                rentedCounts = counts;
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var open = Utf8ExactStatePool<int>.Rent(groups, retain);
                rentedOpen = open;
                for (var offset = 0; offset < groups;)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var count = Math.Min(2048, groups - offset);
                    counts.AsSpan(offset, count).Clear();
                    offset += count;
                }
                var sawBalanceDrain = false;
                for (var entry = 0; entry < _trailCount; entry++)
                {
                    if ((entry & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsRestore || item.IsOpening) continue;
                    if (item.IsHead)
                    {
                        if (item.NewValue < item.OldValue) sawBalanceDrain = true;
                    }
                    else if (!item.IsStart) counts[item.Slot]++;
                }
                ulong captureBytes = 0;
                for (var group = 0; group < groups; group++)
                {
                    if ((group & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                    captureBytes = checked(captureBytes + Utf8ConstructionBudget.ArrayBytes<int>(counts[group]) * 2);
                }
                breach = invocation.Budget.TryChargeResultBytes(captureBytes);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                breach = invocation.TryReserveWorking(captureBytes);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                ownedBytes = checked(ownedBytes + captureBytes);
                for (var group = 0; group < groups; group++)
                {
                    if ((group & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                    open[group] = -1;
                    captureStarts[group] = counts[group] == 0 ? [] : new int[counts[group]];
                    captureLengths[group] = counts[group] == 0 ? [] : new int[counts[group]];
                    counts[group] = 0;
                }
                for (var entry = 0; entry < _trailCount; entry++)
                {
                    if ((entry & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsRestore || item.IsOpening) continue;
                    if (item.IsHead)
                    {
                        if (item.NewValue < item.OldValue) counts[item.Slot]--;
                    }
                    else if (item.IsStart) open[item.Slot] = item.NewValue;
                    else
                    {
                        // Histories normalize mirrored capture edges; the trail
                        // itself retains its original rollback representation.
                        var left = item.IsClosing ? item.OldValue : open[item.Slot];
                        var right = item.NewValue;
                        captureStarts[item.Slot][counts[item.Slot]] = Math.Min(left, right);
                        captureLengths[item.Slot][counts[item.Slot]] = Math.Abs(right - left);
                        counts[item.Slot]++;
                    }
                }
                if (sawBalanceDrain)
                {
                    for (var group = 0; group < groups; group++)
                    {
                        if ((group & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                        if (counts[group] == captureStarts[group].Length) continue;
                        var oldBytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(captureStarts[group].Length) * 2);
                        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(counts[group]) * 2);
                        // Admit both replacement arrays while old capacity is
                        // held. Copy in bounded blocks, then transfer ownership.
                        breach = invocation.Budget.TryChargeResultBytes(bytes);
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        breach = invocation.TryReserveWorking(bytes);
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        ownedBytes += bytes;
                        var starts = counts[group] == 0 ? [] : new int[counts[group]];
                        var lengths = counts[group] == 0 ? [] : new int[counts[group]];
                        for (var offset = 0; offset < counts[group];)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                            var count = Math.Min(512, counts[group] - offset);
                            captureStarts[group].AsSpan(offset, count).CopyTo(starts.AsSpan(offset, count));
                            captureLengths[group].AsSpan(offset, count).CopyTo(lengths.AsSpan(offset, count));
                            offset += count;
                        }
                        captureStarts[group] = starts;
                        captureLengths[group] = lengths;
                        invocation.Ledger.Release(oldBytes);
                        ownedBytes -= oldBytes;
                    }
                }
                published = true;
                return Utf8ExecutionLimitKind.None;
            }
            finally
            {
                if (rentedOpen is not null) Utf8ExactStatePool<int>.Return(rentedOpen, retain);
                if (rentedCounts is not null) Utf8ExactStatePool<int>.Return(rentedCounts, retain);
                invocation.Ledger.Release(temporaryBytes);
                if (!published)
                {
                    invocation.Ledger.Release(ownedBytes);
                    captureStarts = [];
                    captureLengths = [];
                }
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

            _frameTables = Array.Empty<Utf8SliceFrame[]>();
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
                var leaf = new T[chunkCapacity];
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
        int[] captureStarts, int[] captureEnds, int inputIndex, ref Utf8SliceInvocation invocation)
    {
        // ECMAScript permits a defined group that has not completed a capture
        // yet to match an empty reference. Other unmatched references fail.
        if (captureStarts[instruction.A] < 0 || captureEnds[instruction.A] < 0)
            return new(instruction.C != 0, instruction.C != 0 ? inputIndex : -1,
                Utf8ExecutionLimitKind.None, null, null);

        var referStart = Math.Min(captureStarts[instruction.A], captureEnds[instruction.A]);
        var referEnd = Math.Max(captureStarts[instruction.A], captureEnds[instruction.A]);
        var referLength = referEnd - referStart;
        if (program.RightToLeft)
        {
            // Mirrored captures record their right edge at opening and left
            // edge at closing. Compare both spans from their own scalar ends.
            if (instruction.B != 0)
            {
                var capIndex = referEnd;
                var backIndex = inputIndex;
                while (capIndex > referStart)
                {
                    var charge = invocation.TryChargeStep();
                    if (charge != Utf8ExecutionLimitKind.None) return new(false, -1, charge, null, null);
                    if (backIndex <= 0 ||
                        !TryReadScalar(input, capIndex, true, out var captureScalar, out var capWidth) ||
                        capIndex - capWidth < referStart ||
                        !TryReadScalar(input, backIndex, true, out var inputScalar, out var inWidth) ||
                        !BackreferenceScalarsEqual(program, instruction, captureScalar, inputScalar))
                        return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
                    capIndex -= capWidth;
                    backIndex -= inWidth;
                }
                return new(true, backIndex, Utf8ExecutionLimitKind.None, null, null);
            }
            if (referLength > inputIndex) return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
            for (var compared = 0; compared < referLength; compared++)
            {
                var charge = invocation.TryChargeStep();
                if (charge != Utf8ExecutionLimitKind.None) return new(false, -1, charge, null, null);
                if (input[referEnd - 1 - compared] != input[inputIndex - 1 - compared])
                    return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
            }
            return new(true, inputIndex - referLength, Utf8ExecutionLimitKind.None, null, null);
        }

        if (instruction.B != 0)
        {
            // Each side advances by its own UTF-8 scalar width (for example k
            // versus Kelvin), using the same existing profile equivalence.
            var capIndex = referStart;
            var backIndex = inputIndex;
            while (capIndex < referEnd)
            {
                var charge = invocation.TryChargeStep();
                if (charge != Utf8ExecutionLimitKind.None) return new(false, -1, charge, null, null);
                if (backIndex >= input.Length ||
                    !TryReadScalar(input, capIndex, false, out var captureScalar, out var capWidth) ||
                    capIndex + capWidth > referEnd ||
                    !TryReadScalar(input, backIndex, false, out var inputScalar, out var inWidth) ||
                    !BackreferenceScalarsEqual(program, instruction, captureScalar, inputScalar))
                    return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
                capIndex += capWidth;
                backIndex += inWidth;
            }
            return new(true, backIndex, Utf8ExecutionLimitKind.None, null, null);
        }
        // A scalar-aligned capture has one strict UTF-8 encoding. Retain the
        // original work charge before every ordinal byte comparison.
        if (referLength > input.Length - inputIndex) return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
        for (var compared = 0; compared < referLength; compared++)
        {
            var charge = invocation.TryChargeStep();
            if (charge != Utf8ExecutionLimitKind.None) return new(false, -1, charge, null, null);
            if (input[referStart + compared] != input[inputIndex + compared])
                return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
        }
        return new(true, inputIndex + referLength, Utf8ExecutionLimitKind.None, null, null);
    }

    private static bool BackreferenceScalarsEqual(Utf8SliceProgram program,
        Utf8SliceInstruction instruction, Rune captured, Rune input)
    {
        if (instruction.D == 0) return Utf8SliceCaseEquivalences.ScalarsEquivalent(captured, input, program.CaseBehavior);
        var lower = program.BackreferenceLowerings[instruction.D - 1];
        return lower(captured.Value) == lower(input.Value);
    }

}
