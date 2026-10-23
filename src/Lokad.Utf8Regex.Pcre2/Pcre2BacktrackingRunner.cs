using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

// PLAN.md D09: backtracking execution state and runner, split from the compiler collection along the execution ownership boundary.
internal enum Pcre2BacktrackingResumeAction : byte
{
    None = 0,
    EnterRepeat = 1,
    ControlVerb = 2,
}

internal readonly record struct Pcre2BacktrackingFrame(
    int Instruction,
    int InputOffsetInBytes,
    int ReportedStartOffsetInBytes,
    bool MatchBoundaryWasReset,
    int RepeatCheckpoint,
    int CaptureCheckpoint,
    int CallCheckpoint,
    int AtomicCheckpoint,
    int MarkMutationCheckpoint,
    int MarkTrailCheckpoint,
    Pcre2BacktrackingResumeAction ResumeAction,
    int RepeatId,
    int RepeatCount,
    int RepeatPosition,
    Pcre2ControlVerbKind ControlVerb,
    int ControlTarget,
    int ControlPosition,
    int ControlNameId);

internal readonly record struct Pcre2RepeatMutation(
    int RepeatId,
    int PreviousCount,
    int PreviousPosition);

internal readonly record struct Pcre2CaptureMutation(
    int Slot,
    int PreviousStart,
    int PreviousEnd,
    int PreviousOpenStart,
    int PreviousOwner,
    int PreviousOpenOwner);

internal readonly record struct Pcre2SubroutineCallFrame(
    int ReturnInstruction,
    int SubroutineSlot,
    int RepeatCheckpoint,
    int CaptureCheckpoint,
    int BacktrackingCheckpoint,
    int AtomicCheckpoint,
    int MarkTrailCheckpoint);

internal readonly record struct Pcre2SubroutineCaptureSnapshot(
    int Start,
    int End,
    int OpenStart,
    int Owner,
    int OpenOwner);

internal readonly record struct Pcre2SubroutineRepeatSnapshot(int Count, int Position);

internal enum Pcre2SubroutineCallMutationKind : byte
{
    Pushed = 0,
    Popped = 1,
}

internal readonly record struct Pcre2SubroutineCallMutation(
    Pcre2SubroutineCallMutationKind Kind,
    Pcre2SubroutineCallFrame Frame);

internal readonly record struct Pcre2AtomicCheckpoint(int BacktrackingCheckpoint, int MarkTrailCheckpoint);

internal readonly record struct Pcre2MarkMutation(int PreviousMarkId, int PreviousPosition);

internal readonly record struct Pcre2MarkTrailEntry(int MarkId, int Position);

internal enum Pcre2BacktrackingFailureKind : byte
{
    None = 0,
    Commit = 1,
    Prune = 2,
    Skip = 3,
}

internal readonly record struct Pcre2BacktrackingFailure(Pcre2BacktrackingFailureKind Kind, int SkipPosition);

internal readonly record struct Pcre2CaptureByteRange(bool Success, int StartOffsetInBytes, int EndOffsetInBytes);

internal readonly struct Pcre2BacktrackingCaptureResult
{
    private readonly object? _value;

    private Pcre2BacktrackingCaptureResult(object value) => _value = value;

    internal static Pcre2BacktrackingCaptureResult FromAssertionRanges(Pcre2CaptureByteRange[] captures) =>
        new(captures);

    internal static Pcre2BacktrackingCaptureResult FromProjectedGroups(Pcre2GroupData[] groups) =>
        new(groups);

    internal Pcre2CaptureByteRange[] AssertionRanges => _value as Pcre2CaptureByteRange[] ?? [];

    internal Pcre2GroupData[] ProjectedGroups => _value as Pcre2GroupData[] ?? [];

    // The lexical publishing caller invokes this once, after VM scratch exits.
    // Final arrays are immutable external result storage and never pool leaves.
    internal void DetachProjectedWorking(ref Pcre2ResourceBudget budget)
    {
        if (_value is Pcre2GroupData[] groups)
            budget.ReleaseWorking(Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(groups.Length));
    }
}

internal enum Pcre2CaptureMaterialization : byte
{
    None = 0,
    AssertionRanges = 1,
    ProjectedFinalSlots = 2,
}

internal readonly record struct Pcre2BacktrackingMatch(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int ConsumedStartOffsetInBytes,
    int ConsumedEndOffsetInBytes,
    bool MatchBoundaryWasReset,
    Pcre2BacktrackingCaptureResult CaptureResult,
    string? Mark)
{
    internal Pcre2CharacterMatch ToCharacterMatch() => Success
        ? new Pcre2CharacterMatch(
            true,
            StartOffsetInBytes,
            EndOffsetInBytes,
            ConsumedStartOffsetInBytes,
            ConsumedEndOffsetInBytes,
            MatchBoundaryWasReset)
        : Pcre2CharacterMatch.NoMatch;
}

internal static class Pcre2BacktrackingRunner
{
    internal static Pcre2CharacterMatch Match(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            candidateSearch,
            ref input,
            start,
            start,
            matchOptions,
            Pcre2CaptureMaterialization.None,
            ref budget).ToCharacterMatch();

    internal static Pcre2InvocationStateStack<Pcre2BacktrackingFrame> CreateFrames(
        scoped ref Pcre2ResourceBudget budget, int depthBase)
    {
        var limit = budget.Limits.DepthLimit == 0 ? int.MaxValue : (int)Math.Min(budget.Limits.DepthLimit, int.MaxValue);
        return new(limit == int.MaxValue ? limit : Math.Max(0, limit - depthBase), budget.CollectsDiagnostics);
    }

    internal static Pcre2CharacterMatch Match(
        Pcre2BacktrackingProgram program, Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input, Utf8BytePosition searchStart, Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions, ref Pcre2ResourceBudget budget,
        scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace)
        => MatchCore(program, candidateSearch, ref input, searchStart, firstMatchingPosition,
            matchOptions, Pcre2CaptureMaterialization.None, ref budget, ref frames, ref repeatMutations, ref workspace).ToCharacterMatch();

    internal static Pcre2BacktrackingMatch MatchDetailed(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            candidateSearch,
            ref input,
            start,
            start,
            matchOptions,
            Pcre2CaptureMaterialization.ProjectedFinalSlots,
            ref budget);

    internal static Pcre2BacktrackingMatch MatchDetailed(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition searchStart,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
        => MatchCore(
            program,
            candidateSearch,
            ref input,
            searchStart,
            firstMatchingPosition,
            matchOptions,
            Pcre2CaptureMaterialization.ProjectedFinalSlots,
            ref budget);

    private static Pcre2BacktrackingMatch MatchCore(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ref Pcre2ResourceBudget budget)
    {
        var frames = CreateFrames(ref budget, 0);
        var repeatMutations = new Pcre2InvocationStateStack<Pcre2RepeatMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var workspace = new Pcre2BacktrackingWorkspace();
        try
        {
            return MatchCore(program, candidateSearch, ref input, start, firstMatchingPosition,
                matchOptions, captureMaterialization, ref budget, ref frames, ref repeatMutations, ref workspace);
        }
        finally { workspace.Dispose(ref budget); repeatMutations.Dispose(ref budget); frames.Dispose(ref budget); }
    }

    private static Pcre2BacktrackingMatch MatchCore(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ref Pcre2ResourceBudget budget,
        scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace)
    {
        var bytes = input.Bytes;
        var anchored = program.RestrictsSearchToInitialCandidate ||
            (program.Request.Options & Pcre2CompileOptions.Anchored) != 0 ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0;
        var candidate = start.Value;
        var useCandidateSearch = !anchored &&
            candidateSearch.HasValue &&
            matchOptions == Pcre2MatchOptions.None &&
            budget.IsUnmetered;
        var candidateState = new PreparedSearchScanState(
            candidate,
            new PreparedMultiLiteralScanState(candidate, candidate, 0));
        var nextWindowCandidate = -1;
        var lastWindowCandidate = -1;
        var windowCandidateEnd = -1;
        string? lastMark = null;
        var maximumCandidate = program.MinimumByteLength > bytes.Length
            ? -1
            : bytes.Length - program.MinimumByteLength;
        var skipsCrLfContinuation = program.Request.SkipsCrLfContinuation;
        while (candidate <= maximumCandidate)
        {
            if (useCandidateSearch)
            {
                if (!TryFindNextCandidate(
                        candidateSearch,
                        program.Request,
                        bytes,
                        start.Value,
                        ref candidateState,
                        ref nextWindowCandidate,
                        ref lastWindowCandidate,
                        ref windowCandidateEnd,
                        ref budget,
                        out candidate) ||
                    candidate > maximumCandidate)
                {
                    break;
                }
            }

            if (Pcre2CharacterSemantics.IsSkippedCrLfCandidate(skipsCrLfContinuation, bytes, candidate, start.Value))
            {
                if (useCandidateSearch) continue;
                if (++candidate > maximumCandidate) break;
            }
            budget.ChargeCandidate();
            if (TryMatchAt(
                    program,
                    bytes,
                    ref input,
                    candidate,
                    firstMatchingPosition.Value,
                    matchOptions,
                    captureMaterialization,
                    ReadOnlySpan<int>.Empty,
                    ReadOnlySpan<int>.Empty,
                    0,
                    false,
                    ref budget,
                    ref frames,
                    ref repeatMutations,
                    ref workspace,
                    out var match,
                    out var failure))
            {
                return match;
            }

            lastMark = match.Mark ?? lastMark;

            if (failure.Kind == Pcre2BacktrackingFailureKind.Commit)
            {
                break;
            }

            if (failure.Kind == Pcre2BacktrackingFailureKind.Skip && failure.SkipPosition > candidate)
            {
                candidate = failure.SkipPosition;
                continue;
            }

            if (useCandidateSearch)
            {
                continue;
            }

            if (anchored || !Pcre2CharacterRunner.TryAdvanceCandidate(
                    program.Request,
                    program.LeadingAsciiByte,
                    ref input,
                    candidate,
                    !budget.RequiresCandidateMetering,
                    out var nextCandidate))
            {
                break;
            }

            if (program.LeadingExtendedGraphemeCluster &&
                (program.Request.Options & Pcre2CompileOptions.FirstLine) == 0 &&
                Pcre2GraphemeClusterSemantics.TryGetWidth(bytes, candidate, ref budget, out var graphemeWidth))
            {
                candidate += graphemeWidth;
            }
            else
            {
                candidate = nextCandidate;
            }
        }

        return new Pcre2BacktrackingMatch(false, 0, 0, 0, 0, false, default, lastMark);
    }

    private static bool TryFindNextCandidate(
        Pcre2CandidateSearchProgram candidateSearch,
        Pcre2CompileRequest request,
        ReadOnlySpan<byte> input,
        int lowerBound,
        ref PreparedSearchScanState state,
        ref int nextWindowCandidate,
        ref int lastWindowCandidate,
        ref int windowCandidateEnd,
        ref Pcre2ResourceBudget budget,
        out int candidate)
    {
        if (candidateSearch.Kind == Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun)
        {
            return TryFindNextLeadingAsciiWordBoundaryRun(
                input,
                lowerBound,
                candidateSearch.MinimumWordRunLength,
                ref nextWindowCandidate,
                out candidate);
        }

        if (candidateSearch.Kind == Pcre2CandidateSearchKind.BoundedLiteralWindow)
        {
            return TryFindNextBoundedWindowCandidate(
                candidateSearch,
                input,
                lowerBound,
                ref state,
                ref nextWindowCandidate,
                ref lastWindowCandidate,
                ref windowCandidateEnd,
                out candidate);
        }

        while (candidateSearch.Searcher.TryFindNextOverlappingMatch(input, ref state, out var preparedCandidate))
        {
            if (candidateSearch.Kind is Pcre2CandidateSearchKind.BranchLeadingLiterals or
                Pcre2CandidateSearchKind.LeadingAsciiSet or
                Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow)
            {
                if (candidateSearch.Kind == Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow &&
                    !MatchesCandidateWindow(input, preparedCandidate.Index, candidateSearch.WindowConstraint))
                {
                    continue;
                }

                candidate = preparedCandidate.Index;
                return true;
            }

            if (candidateSearch.Kind != Pcre2CandidateSearchKind.LeadingRunThenLiteral)
            {
                break;
            }

            var runStart = preparedCandidate.Index;
            var valid = true;
            for (var i = candidateSearch.LeadingRunTokens.Length - 1; i >= 0; i--)
            {
                var consumed = false;
                while (TryRetreatScalarToBoundary(input, lowerBound, runStart, out var previous) &&
                       Pcre2CharacterRunner.TryMatchToken(
                           candidateSearch.LeadingRunTokens[i],
                           request,
                           input,
                           previous,
                           lowerBound,
                           Pcre2MatchOptions.None,
                           ref budget,
                           out var next) &&
                       next == runStart)
                {
                    runStart = previous;
                    consumed = true;
                }

                if (!consumed)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                candidate = runStart;
                return true;
            }
        }

        candidate = -1;
        return false;
    }

    private static bool TryFindNextLeadingAsciiWordBoundaryRun(
        ReadOnlySpan<byte> input,
        int lowerBound,
        int minimumWordRunLength,
        ref int scanOffset,
        out int candidate)
    {
        var offset = Math.Max(lowerBound, scanOffset);
        while (offset < input.Length)
        {
            if (!IsAsciiWordByte(input[offset]))
            {
                offset++;
                continue;
            }

            var runStart = offset;
            do
            {
                offset++;
            }
            while (offset < input.Length && IsAsciiWordByte(input[offset]));

            scanOffset = offset;
            if ((runStart == 0 || !IsAsciiWordByte(input[runStart - 1])) &&
                offset - runStart >= minimumWordRunLength)
            {
                candidate = runStart;
                return true;
            }
        }

        scanOffset = input.Length;
        candidate = -1;
        return false;
    }

    private static bool IsAsciiWordByte(byte value) =>
        value is >= (byte)'a' and <= (byte)'z' or
            >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'0' and <= (byte)'9' or
            (byte)'_';

    private static bool TryFindNextBoundedWindowCandidate(
        Pcre2CandidateSearchProgram candidateSearch,
        ReadOnlySpan<byte> input,
        int lowerBound,
        ref PreparedSearchScanState state,
        ref int nextWindowCandidate,
        ref int lastWindowCandidate,
        ref int windowCandidateEnd,
        out int candidate)
    {
        while (true)
        {
            while (nextWindowCandidate <= windowCandidateEnd)
            {
                var current = nextWindowCandidate++;
                if (current > lastWindowCandidate &&
                    (current == input.Length || (input[current] & 0xC0) != 0x80))
                {
                    lastWindowCandidate = current;
                    candidate = current;
                    return true;
                }
            }

            if (!candidateSearch.Searcher.TryFindNextOverlappingMatch(input, ref state, out var requiredLiteral))
            {
                candidate = -1;
                return false;
            }

            nextWindowCandidate = Math.Max(
                Math.Max(lowerBound, lastWindowCandidate + 1),
                requiredLiteral.Index - candidateSearch.WindowConstraint.MaximumOffset);
            windowCandidateEnd = requiredLiteral.Index - candidateSearch.WindowConstraint.MinimumOffset;
        }
    }

    private static bool MatchesCandidateWindow(
        ReadOnlySpan<byte> input,
        int candidate,
        Pcre2CandidateWindowConstraint constraint)
    {
        var latest = Math.Min(constraint.MaximumOffset, input.Length - candidate - constraint.Literal.Length);
        for (var offset = constraint.MinimumOffset; offset <= latest; offset++)
        {
            if (input.Slice(candidate + offset, constraint.Literal.Length).SequenceEqual(constraint.Literal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryRetreatScalarToBoundary(
        ReadOnlySpan<byte> input,
        int lowerBound,
        int position,
        out int previous)
    {
        if (position <= lowerBound)
        {
            previous = 0;
            return false;
        }

        previous = position - 1;
        while (previous > lowerBound && (input[previous] & 0xC0) == 0x80)
        {
            previous--;
        }

        return true;
    }

    private static bool TryMatchAt(
        Pcre2BacktrackingProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8ValidatedInput projectionInput,
        int candidate,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ReadOnlySpan<int> initialCaptureStarts,
        ReadOnlySpan<int> initialCaptureEnds,
        int depthBase,
        bool isAssertion,
        ref Pcre2ResourceBudget budget,
        out Pcre2BacktrackingMatch match,
        out Pcre2BacktrackingFailure failure)
    {
        var frames = CreateFrames(ref budget, depthBase);
        var repeatMutations = new Pcre2InvocationStateStack<Pcre2RepeatMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var workspace = new Pcre2BacktrackingWorkspace();
        try
        {
            return TryMatchAt(program, input, ref projectionInput, candidate, firstMatchingPosition,
                matchOptions, captureMaterialization, initialCaptureStarts, initialCaptureEnds,
                depthBase, isAssertion, ref budget, ref frames, ref repeatMutations, ref workspace, out match, out failure);
        }
        finally { workspace.Dispose(ref budget); repeatMutations.Dispose(ref budget); frames.Dispose(ref budget); }
    }

    private static bool TryMatchAt(
        Pcre2BacktrackingProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8ValidatedInput projectionInput,
        int candidate,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ReadOnlySpan<int> initialCaptureStarts,
        ReadOnlySpan<int> initialCaptureEnds,
        int depthBase,
        bool isAssertion,
        ref Pcre2ResourceBudget budget,
        scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace,
        out Pcre2BacktrackingMatch match,
        out Pcre2BacktrackingFailure failure)
    {
        var configuredFrameLimit = budget.Limits.DepthLimit == 0
            ? int.MaxValue
            : (int)Math.Min(budget.Limits.DepthLimit, int.MaxValue);
        var frameLimit = configuredFrameLimit == int.MaxValue
            ? int.MaxValue
            : Math.Max(0, configuredFrameLimit - depthBase);
        var initialFrameRents = frames.RentCount;
        var initialRepeatRents = repeatMutations.RentCount;
        var captureMutations = new Pcre2InvocationStateStack<Pcre2CaptureMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var subroutineCalls = new Pcre2InvocationStateStack<Pcre2SubroutineCallFrame>(frameLimit, budget.CollectsDiagnostics);
        var subroutineCallMutations = new Pcre2InvocationStateStack<Pcre2SubroutineCallMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var atomicCheckpoints = new Pcre2InvocationStateStack<Pcre2AtomicCheckpoint>(frameLimit, budget.CollectsDiagnostics);
        var markMutations = new Pcre2InvocationStateStack<Pcre2MarkMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var markTrail = new Pcre2InvocationStateStack<Pcre2MarkTrailEntry>(int.MaxValue, budget.CollectsDiagnostics);
        var needsCaptureState = program.RequiresCaptureState ||
            captureMaterialization != Pcre2CaptureMaterialization.None ||
            !initialCaptureStarts.IsEmpty;
        var captureSlots = needsCaptureState && program.CaptureSlotCount > 1 ? program.CaptureSlotCount : 0;
        var captureStateLength = checked(captureSlots * 5);
        var acquiredWorkspace = workspace.EnsureInitialized(program.RepeatCount, captureSlots, program.SubroutineTargets.Length != 0, ref budget);
        var rentedRepeatState = workspace.RepeatState;
        var rentedCaptureState = workspace.CaptureState;
        var rentedCaptureRestoreGenerations = workspace.CaptureRestoreGenerations;
        var rentedCaptureRestoreSlots = workspace.CaptureRestoreSlots;
        var rentedCaptureRestoreSnapshots = workspace.CaptureRestoreSnapshots;
        var rentedRepeatRestoreGenerations = workspace.RepeatRestoreGenerations;
        var rentedRepeatRestoreIds = workspace.RepeatRestoreIds;
        var rentedRepeatRestoreSnapshots = workspace.RepeatRestoreSnapshots;
        var captureState = rentedCaptureState is null ? Span<int>.Empty : rentedCaptureState.AsSpan(0, captureStateLength);

        var counts = rentedRepeatState is null
            ? Span<int>.Empty
            : rentedRepeatState.AsSpan(0, program.RepeatCount);
        var positions = rentedRepeatState is null
            ? Span<int>.Empty
            : rentedRepeatState.AsSpan(program.RepeatCount, program.RepeatCount);
        var captureStarts = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState[..program.CaptureSlotCount];
        var captureEnds = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState.Slice(program.CaptureSlotCount, program.CaptureSlotCount);
        var captureOpenStarts = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState.Slice(program.CaptureSlotCount * 2, program.CaptureSlotCount);
        var captureOwners = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState.Slice(program.CaptureSlotCount * 3, program.CaptureSlotCount);
        var captureOpenOwners = captureState.IsEmpty
            ? Span<int>.Empty
            : captureState.Slice(program.CaptureSlotCount * 4, program.CaptureSlotCount);
        var captureRestoreGenerations = rentedCaptureRestoreGenerations is null
            ? Span<int>.Empty
            : rentedCaptureRestoreGenerations.AsSpan(0, program.CaptureSlotCount);
        var captureRestoreSlots = rentedCaptureRestoreSlots is null
            ? Span<int>.Empty
            : rentedCaptureRestoreSlots.AsSpan(0, program.CaptureSlotCount);
        var captureRestoreSnapshots = rentedCaptureRestoreSnapshots is null
            ? Span<Pcre2SubroutineCaptureSnapshot>.Empty
            : rentedCaptureRestoreSnapshots.AsSpan(0, program.CaptureSlotCount);
        var repeatRestoreGenerations = rentedRepeatRestoreGenerations is null
            ? Span<int>.Empty
            : rentedRepeatRestoreGenerations.AsSpan(0, program.RepeatCount);
        var repeatRestoreIds = rentedRepeatRestoreIds is null
            ? Span<int>.Empty
            : rentedRepeatRestoreIds.AsSpan(0, program.RepeatCount);
        var repeatRestoreSnapshots = rentedRepeatRestoreSnapshots is null
            ? Span<Pcre2SubroutineRepeatSnapshot>.Empty
            : rentedRepeatRestoreSnapshots.AsSpan(0, program.RepeatCount);
        counts.Fill(-1);
        positions.Fill(-1);
        captureStarts.Fill(-1);
        captureEnds.Fill(-1);
        captureOpenStarts.Fill(-1);
        captureOwners.Fill(-1);
        captureOpenOwners.Fill(-1);
        captureRestoreGenerations.Clear();
        repeatRestoreGenerations.Clear();
        if (!initialCaptureStarts.IsEmpty)
        {
            initialCaptureStarts.CopyTo(captureStarts);
            initialCaptureEnds.CopyTo(captureEnds);
            for (var slot = 1; slot < captureStarts.Length; slot++)
            {
                if (captureStarts[slot] >= 0)
                {
                    captureOwners[slot] = 0;
                }
            }
        }


        try
        {
            var instructionIndex = 0;
            var inputIndex = candidate;
            var reportedStartOffsetInBytes = candidate;
            var matchBoundaryWasReset = false;
            var subroutineRestoreGeneration = 0;
            var currentMarkId = -1;
            var currentMarkPosition = -1;
            var lastEncounteredMarkId = -1;
            while (true)
            {
                var instruction = program.Instructions[instructionIndex];
                budget.ChargeBacktracking(instruction.Kind, instruction.Token.Kind);
                switch (instruction.Kind)
                {
                    case Pcre2BacktrackingInstructionKind.Token:
                        if (Pcre2CharacterRunner.TryMatchToken(
                                instruction.Token,
                                program.Request,
                                input,
                                inputIndex,
                                firstMatchingPosition,
                                matchOptions,
                                ref budget,
                                out inputIndex))
                        {
                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.Split:
                        PushFrame(
                            ref frames,
                            new Pcre2BacktrackingFrame(
                                instruction.SecondaryTarget,
                                inputIndex,
                                reportedStartOffsetInBytes,
                                matchBoundaryWasReset,
                                repeatMutations.Count,
                                captureMutations.Count,
                                subroutineCallMutations.Count,
                                atomicCheckpoints.Count,
                                markMutations.Count,
                                markTrail.Count,
                                Pcre2BacktrackingResumeAction.None,
                                0,
                                0,
                                0,
                                default,
                                -1,
                                0,
                                -1),
                            depthBase,
                            subroutineCalls.Count,
                            ref budget);
                        instructionIndex = instruction.PrimaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Jump:
                        instructionIndex = instruction.PrimaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Repeat:
                        var repeatId = instruction.RepeatId;
                        var count = counts[repeatId];
                        if (count < 0)
                        {
                            SetRepeat(repeatId, 0, -1, counts, positions, ref repeatMutations, ref budget);
                            count = 0;
                        }

                        var madeNoProgress = count > 0 && positions[repeatId] == inputIndex;
                        if (count < instruction.Minimum || !madeNoProgress && count < instruction.Maximum)
                        {
                            if (count >= instruction.Minimum)
                            {
                                if (instruction.Preference == Pcre2RepeatPreference.Greedy)
                                {
                                    PushFrame(
                                        ref frames,
                                        new Pcre2BacktrackingFrame(
                                            instruction.SecondaryTarget,
                                            inputIndex,
                                            reportedStartOffsetInBytes,
                                            matchBoundaryWasReset,
                                            repeatMutations.Count,
                                            captureMutations.Count,
                                            subroutineCallMutations.Count,
                                            atomicCheckpoints.Count,
                                            markMutations.Count,
                                            markTrail.Count,
                                            Pcre2BacktrackingResumeAction.None,
                                            0,
                                            0,
                                            0,
                                            default,
                                            -1,
                                            0,
                                            -1),
                                        depthBase,
                                        subroutineCalls.Count,
                                        ref budget);
                                }
                                else
                                {
                                    PushFrame(
                                        ref frames,
                                        new Pcre2BacktrackingFrame(
                                            instruction.PrimaryTarget,
                                            inputIndex,
                                            reportedStartOffsetInBytes,
                                            matchBoundaryWasReset,
                                            repeatMutations.Count,
                                            captureMutations.Count,
                                            subroutineCallMutations.Count,
                                            atomicCheckpoints.Count,
                                            markMutations.Count,
                                            markTrail.Count,
                                            Pcre2BacktrackingResumeAction.EnterRepeat,
                                            repeatId,
                                            count + 1,
                                            inputIndex,
                                            default,
                                            -1,
                                            0,
                                            -1),
                                        depthBase,
                                        subroutineCalls.Count,
                                        ref budget);
                                    instructionIndex = instruction.SecondaryTarget;
                                    continue;
                                }
                            }

                            SetRepeat(repeatId, count + 1, inputIndex, counts, positions, ref repeatMutations, ref budget);
                            instructionIndex = instruction.PrimaryTarget;
                            continue;
                        }

                        instructionIndex = instruction.SecondaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.RepeatEnd:
                        instructionIndex = instruction.PrimaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.RepeatExit:
                        SetRepeat(instruction.RepeatId, -1, -1, counts, positions, ref repeatMutations, ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.PossessiveTokenRepeat:
                        var possessiveCount = 0;
                        var asciiByteSet =
                            instruction.Token.Kind == Pcre2CharacterTokenKind.CharacterClass &&
                            (instruction.Token.Options & Pcre2CharacterOptions.Caseless) == 0
                                ? instruction.Token.CharacterClass.AsciiByteSet
                                : null;
                        while (possessiveCount < instruction.Maximum)
                        {
                            if (asciiByteSet is not null && inputIndex < input.Length)
                            {
                                var remainingMaximum = instruction.Maximum - possessiveCount;
                                var remainingLength = Math.Min(remainingMaximum, input.Length - inputIndex);
                                var stop = asciiByteSet.IndexOfAnyExcept(input.Slice(inputIndex, remainingLength));
                                var asciiCount = stop < 0 ? remainingLength : stop;
                                inputIndex += asciiCount;
                                possessiveCount += asciiCount;
                                if (possessiveCount >= instruction.Maximum || inputIndex >= input.Length)
                                {
                                    break;
                                }
                            }

                            if (!Pcre2CharacterRunner.TryMatchToken(
                                    instruction.Token,
                                    program.Request,
                                    input,
                                    inputIndex,
                                    firstMatchingPosition,
                                    matchOptions,
                                    ref budget,
                                    out var possessiveEnd))
                            {
                                break;
                            }

                            inputIndex = possessiveEnd;
                            possessiveCount++;
                        }

                        if (budget.CollectsDiagnostics)
                        {
                            budget.RecordPossessiveTokenScanCharacters((ulong)possessiveCount);
                        }

                        if (possessiveCount >= instruction.Minimum)
                        {
                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.CollapsibleTokenRepeat:
                        if (!budget.IsUnmetered)
                        {
                            instructionIndex = instruction.PrimaryTarget;
                            continue;
                        }

                        var collapsibleToken = instruction.Token;
                        var collapsibleMaximum = instruction.Maximum;
                        var collapsibleStart = inputIndex;
                        var collapsibleCount = 0;
                        var collapsibleAsciiByteSet =
                            collapsibleToken.Kind == Pcre2CharacterTokenKind.CharacterClass &&
                            (collapsibleToken.Options & Pcre2CharacterOptions.Caseless) == 0
                                ? collapsibleToken.CharacterClass.AsciiByteSet
                                : null;
                        var collapsibleLiteralByte =
                            collapsibleToken.Kind == Pcre2CharacterTokenKind.Literal &&
                            collapsibleToken.Literal.Value <= 0x7F
                                ? (byte)collapsibleToken.Literal.Value
                                : (byte?)null;
                        while (collapsibleCount < collapsibleMaximum)
                        {
                            if (inputIndex < input.Length)
                            {
                                if (collapsibleLiteralByte is { } literalByte)
                                {
                                    while (collapsibleCount < collapsibleMaximum &&
                                        inputIndex < input.Length &&
                                        input[inputIndex] == literalByte)
                                    {
                                        inputIndex++;
                                        collapsibleCount++;
                                    }
                                }
                                else if (collapsibleAsciiByteSet is not null)
                                {
                                    var remainingMaximum = collapsibleMaximum - collapsibleCount;
                                    var remainingLength = Math.Min(remainingMaximum, input.Length - inputIndex);
                                    var stop = collapsibleAsciiByteSet.IndexOfAnyExcept(input.Slice(inputIndex, remainingLength));
                                    var asciiCount = stop < 0 ? remainingLength : stop;
                                    inputIndex += asciiCount;
                                    collapsibleCount += asciiCount;
                                }

                                if (collapsibleCount >= collapsibleMaximum || inputIndex >= input.Length)
                                {
                                    break;
                                }
                            }

                            if (!Pcre2CharacterRunner.TryMatchToken(
                                    collapsibleToken,
                                    program.Request,
                                    input,
                                    inputIndex,
                                    firstMatchingPosition,
                                    matchOptions,
                                    ref budget,
                                    out var collapsibleEnd))
                            {
                                break;
                            }

                            inputIndex = collapsibleEnd;
                            collapsibleCount++;
                        }

                        if (collapsibleCount < instruction.Minimum)
                        {
                            break;
                        }

                        if (collapsibleCount > 0)
                        {
                            PushFrame(
                                ref frames,
                                new Pcre2BacktrackingFrame(
                                    instruction.PrimaryTarget,
                                    collapsibleStart,
                                    reportedStartOffsetInBytes,
                                    matchBoundaryWasReset,
                                    repeatMutations.Count,
                                    captureMutations.Count,
                                    subroutineCallMutations.Count,
                                    atomicCheckpoints.Count,
                                    markMutations.Count,
                                    markTrail.Count,
                                    Pcre2BacktrackingResumeAction.None,
                                    0,
                                    0,
                                    0,
                                    default,
                                    -1,
                                    0,
                                    -1),
                                depthBase,
                                subroutineCalls.Count,
                                ref budget);
                        }

                        SetRepeat(instruction.RepeatId, collapsibleCount, inputIndex, counts, positions, ref repeatMutations, ref budget);
                        instructionIndex = instruction.SecondaryTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.CaptureStart:
                        if (captureStarts.IsEmpty)
                        {
                            instructionIndex++;
                            continue;
                        }

                        var startCaptureSlot = instruction.CaptureSlot;
                        var currentCallDepth = subroutineCalls.Count;
                        if (captureStarts[startCaptureSlot] >= 0 && captureOwners[startCaptureSlot] > currentCallDepth ||
                            captureOpenStarts[startCaptureSlot] >= 0 && captureOpenOwners[startCaptureSlot] > currentCallDepth)
                        {
                            instructionIndex++;
                            continue;
                        }

                        SetCapture(
                            startCaptureSlot,
                            captureStarts[startCaptureSlot],
                            captureEnds[startCaptureSlot],
                            inputIndex,
                            captureOwners[startCaptureSlot],
                            currentCallDepth,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
                            ref captureMutations,
                            ref budget,
                            recordRollback: frames.Count != 0 || subroutineCalls.Count != 0);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.CaptureEnd:
                        if (captureStarts.IsEmpty)
                        {
                            instructionIndex++;
                            continue;
                        }

                        var endCaptureSlot = instruction.CaptureSlot;
                        if (captureOpenStarts[endCaptureSlot] < 0 ||
                            captureOpenOwners[endCaptureSlot] != subroutineCalls.Count)
                        {
                            instructionIndex++;
                            continue;
                        }

                        SetCapture(
                            endCaptureSlot,
                            captureOpenStarts[endCaptureSlot],
                            inputIndex,
                            -1,
                            captureOpenOwners[endCaptureSlot],
                            -1,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
                            ref captureMutations,
                            ref budget,
                            recordRollback: frames.Count != 0 || subroutineCalls.Count != 0);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Backreference:
                        var captureSlot = instruction.CaptureSlot;
                        if (captureStarts[captureSlot] >= 0 && captureEnds[captureSlot] >= captureStarts[captureSlot] &&
                            TryMatchBackreference(
                                input,
                                captureStarts[captureSlot],
                                captureEnds[captureSlot],
                                inputIndex,
                                instruction.BackreferenceOptions,
                                out inputIndex))
                        {
                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.BackreferenceSlotSet:
                        var duplicateSlots = program.BackreferenceSlotSets[instruction.BackreferenceSlotSetId].Slots;
                        var selectedSlot = 0;
                        foreach (var slot in duplicateSlots)
                        {
                            if (captureStarts[slot] >= 0 && captureEnds[slot] >= captureStarts[slot])
                            {
                                selectedSlot = slot;
                                break;
                            }
                        }

                        if (selectedSlot != 0 && TryMatchBackreference(
                                input,
                                captureStarts[selectedSlot],
                                captureEnds[selectedSlot],
                                inputIndex,
                                instruction.BackreferenceOptions,
                                out inputIndex))
                        {
                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.Assertion:
                        Pcre2CaptureByteRange[] assertionCaptures = [];
                        try
                        {
                            if (TryEvaluateAssertion(
                                    program.AssertionPrograms[instruction.AssertionProgramId],
                                    instruction.AssertionKind,
                                    input,
                                    ref projectionInput,
                                    inputIndex,
                                    firstMatchingPosition,
                                    matchOptions,
                                    captureStarts,
                                    captureEnds,
                                    depthBase + frames.Count + subroutineCalls.Count + 1,
                                    ref budget,
                                    out assertionCaptures,
                                    out var assertionMark,
                                    out var assertionReportedStart))
                            {
                                if (assertionReportedStart >= 0)
                                {
                                    reportedStartOffsetInBytes = assertionReportedStart;
                                    matchBoundaryWasReset = true;
                                }

                                MergeAssertionCaptures(
                                    assertionCaptures,
                                    subroutineCalls.Count,
                                    captureStarts,
                                    captureEnds,
                                    captureOpenStarts,
                                    captureOwners,
                                    captureOpenOwners,
                                    ref captureMutations,
                                    ref budget);
                                MergeAssertionMark(
                                    program,
                                    assertionMark,
                                    inputIndex,
                                    ref currentMarkId,
                                    ref currentMarkPosition,
                                    ref lastEncounteredMarkId,
                                    ref markMutations,
                                    ref markTrail,
                                    ref budget);

                                instructionIndex++;
                                continue;
                            }
                            break;

                        }
                        finally { ReleaseAssertionCaptures(assertionCaptures, ref budget); }

                    case Pcre2BacktrackingInstructionKind.SubroutineCall:
                        var subroutineSlot = instruction.SubroutineSlot;
                        var subroutineTarget = (uint)subroutineSlot < (uint)program.SubroutineTargets.Length
                            ? program.SubroutineTargets[subroutineSlot]
                            : -1;
                        if (subroutineTarget < 0)
                        {
                            break;
                        }

                        PushSubroutineCall(
                            new Pcre2SubroutineCallFrame(
                                instructionIndex + 1,
                                subroutineSlot,
                                repeatMutations.Count,
                                captureMutations.Count,
                                frames.Count,
                                atomicCheckpoints.Count,
                                markTrail.Count),
                            ref subroutineCalls,
                            ref subroutineCallMutations,
                            depthBase + frames.Count,
                            ref budget);
                        for (var activeRepeatId = 0; activeRepeatId < counts.Length; activeRepeatId++)
                        {
                            if (counts[activeRepeatId] >= 0)
                            {
                                SetRepeat(
                                    activeRepeatId,
                                    -1,
                                    -1,
                                    counts,
                                    positions,
                                    ref repeatMutations,
                                    ref budget);
                            }
                        }
                        instructionIndex = subroutineTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.SubroutineReturn:
                        if (subroutineCalls.Count == 0)
                        {
                            break;
                        }

                        var returnCall = subroutineCalls[subroutineCalls.Count - 1];
                        RestoreSubroutineState(
                            returnCall,
                            counts,
                            positions,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
                            repeatRestoreGenerations,
                            repeatRestoreIds,
                            repeatRestoreSnapshots,
                            captureRestoreGenerations,
                            captureRestoreSlots,
                            captureRestoreSnapshots,
                            ref subroutineRestoreGeneration,
                            ref repeatMutations,
                            ref captureMutations,
                            ref budget);
                        var completedCall = subroutineCalls.Pop();
                        RecordSubroutineCallMutation(
                            Pcre2SubroutineCallMutationKind.Popped,
                            completedCall,
                            ref subroutineCallMutations,
                            ref budget);
                        instructionIndex = completedCall.ReturnInstruction;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Conditional:
                        var condition = program.Conditions[instruction.ConditionId];
                        var conditionMet = condition.Kind switch
                        {
                            Pcre2BacktrackingConditionKind.CaptureSet =>
                                IsAnyCaptureSet(condition.CaptureSlots, captureStarts, captureEnds),
                            Pcre2BacktrackingConditionKind.RecursionAny => subroutineCalls.Count != 0,
                            Pcre2BacktrackingConditionKind.RecursionSlot =>
                                IsSubroutineActive(condition.SubroutineSlot, subroutineCalls),
                            _ => false,
                        };
                        Pcre2CaptureByteRange[] conditionalCaptures = [];
                        try
                        {
                            if (condition.Kind == Pcre2BacktrackingConditionKind.Assertion)
                            {
                                conditionMet = TryEvaluateAssertion(
                                    program.AssertionPrograms[condition.AssertionProgramId],
                                    condition.AssertionKind,
                                    input,
                                    ref projectionInput,
                                    inputIndex,
                                    firstMatchingPosition,
                                    matchOptions,
                                    captureStarts,
                                    captureEnds,
                                    depthBase + frames.Count + subroutineCalls.Count + 1,
                                    ref budget,
                                    out conditionalCaptures,
                                    out var conditionalMark,
                                    out var conditionalReportedStart);
                                if (conditionalReportedStart >= 0)
                                {
                                    reportedStartOffsetInBytes = conditionalReportedStart;
                                    matchBoundaryWasReset = true;
                                }
                                MergeAssertionMark(
                                    program,
                                    conditionalMark,
                                    inputIndex,
                                    ref currentMarkId,
                                    ref currentMarkPosition,
                                    ref lastEncounteredMarkId,
                                    ref markMutations,
                                    ref markTrail,
                                    ref budget);
                            }

                            MergeAssertionCaptures(
                                conditionalCaptures,
                                subroutineCalls.Count,
                                captureStarts,
                                captureEnds,
                                captureOpenStarts,
                                captureOwners,
                                captureOpenOwners,
                                ref captureMutations,
                                ref budget);

                            instructionIndex = conditionMet
                                ? instruction.PrimaryTarget
                                : instruction.SecondaryTarget;
                            continue;

                        }
                        finally { ReleaseAssertionCaptures(conditionalCaptures, ref budget); }

                    case Pcre2BacktrackingInstructionKind.AtomicStart:
                        var nextAtomicDepth = checked(depthBase + subroutineCalls.Count + atomicCheckpoints.Count + 1);
                        budget.ChargeFrame((uint)nextAtomicDepth);
                        if (!atomicCheckpoints.TryPush(new Pcre2AtomicCheckpoint(frames.Count, markTrail.Count), ref budget))
                        {
                            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
                        }
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.AtomicEnd:
                        if (atomicCheckpoints.Count == 0)
                        {
                            break;
                        }

                        var atomic = atomicCheckpoints.Pop();
                        frames.Truncate(atomic.BacktrackingCheckpoint);
                        markTrail.Truncate(atomic.MarkTrailCheckpoint);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.ControlMark:
                        SetMark(
                            instruction.ControlNameId,
                            inputIndex,
                            true,
                            ref currentMarkId,
                            ref currentMarkPosition,
                            ref lastEncounteredMarkId,
                            ref markMutations,
                            ref markTrail,
                            ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.ControlAccept:
                        SetMark(
                            instruction.ControlNameId,
                            inputIndex,
                            false,
                            ref currentMarkId,
                            ref currentMarkPosition,
                            ref lastEncounteredMarkId,
                            ref markMutations,
                            ref markTrail,
                            ref budget);
                        CloseOpenCaptures(
                            inputIndex,
                            subroutineCalls.Count,
                            captureStarts,
                            captureEnds,
                            captureOpenStarts,
                            captureOwners,
                            captureOpenOwners,
                            ref captureMutations,
                            ref budget);
                        if (subroutineCalls.Count != 0)
                        {
                            var acceptedCall = subroutineCalls[subroutineCalls.Count - 1];
                            frames.Truncate(acceptedCall.BacktrackingCheckpoint);
                            atomicCheckpoints.Truncate(acceptedCall.AtomicCheckpoint);
                            markTrail.Truncate(acceptedCall.MarkTrailCheckpoint);
                            RestoreSubroutineState(
                                acceptedCall,
                                counts,
                                positions,
                                captureStarts,
                                captureEnds,
                                captureOpenStarts,
                                captureOwners,
                                captureOpenOwners,
                                repeatRestoreGenerations,
                                repeatRestoreIds,
                                repeatRestoreSnapshots,
                                captureRestoreGenerations,
                                captureRestoreSlots,
                                captureRestoreSnapshots,
                                ref subroutineRestoreGeneration,
                                ref repeatMutations,
                                ref captureMutations,
                                ref budget);
                            var completedAcceptedCall = subroutineCalls.Pop();
                            RecordSubroutineCallMutation(
                                Pcre2SubroutineCallMutationKind.Popped,
                                completedAcceptedCall,
                                ref subroutineCallMutations,
                                ref budget);
                            instructionIndex = completedAcceptedCall.ReturnInstruction;
                            continue;
                        }

                        var acceptEmptyDisallowed =
                            (matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && inputIndex == reportedStartOffsetInBytes ||
                            (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0 &&
                            candidate == firstMatchingPosition && inputIndex == reportedStartOffsetInBytes;
                        if (!acceptEmptyDisallowed)
                        {
                            ThrowIfDisallowedNonMonotoneLookaroundReset(
                                program,
                                isAssertion,
                                matchBoundaryWasReset,
                                reportedStartOffsetInBytes,
                                candidate,
                                inputIndex);
                            var acceptedCaptures = MaterializeCaptureResult(
                                captureMaterialization,
                                program.CaptureSlotCount,
                                reportedStartOffsetInBytes,
                                inputIndex,
                                captureStarts,
                                captureEnds,
                                ref projectionInput, ref budget);
                            failure = default;
                            match = new Pcre2BacktrackingMatch(
                                true,
                                reportedStartOffsetInBytes,
                                inputIndex,
                                candidate,
                                inputIndex,
                                matchBoundaryWasReset,
                                acceptedCaptures,
                                GetMark(program, currentMarkId));
                            return true;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.ControlFail:
                        SetMark(
                            instruction.ControlNameId,
                            inputIndex,
                            false,
                            ref currentMarkId,
                            ref currentMarkPosition,
                            ref lastEncounteredMarkId,
                            ref markMutations,
                            ref markTrail,
                            ref budget);
                        break;

                    case Pcre2BacktrackingInstructionKind.ControlDeferred:
                        if (instruction.ControlVerb != Pcre2ControlVerbKind.Skip)
                        {
                            SetMark(
                                instruction.ControlNameId,
                                inputIndex,
                                false,
                                ref currentMarkId,
                                ref currentMarkPosition,
                                ref lastEncounteredMarkId,
                                ref markMutations,
                                ref markTrail,
                                ref budget);
                        }
                        PushFrame(
                            ref frames,
                            new Pcre2BacktrackingFrame(
                                instructionIndex,
                                inputIndex,
                                reportedStartOffsetInBytes,
                                matchBoundaryWasReset,
                                repeatMutations.Count,
                                captureMutations.Count,
                                subroutineCallMutations.Count,
                                atomicCheckpoints.Count,
                                markMutations.Count,
                                markTrail.Count,
                                Pcre2BacktrackingResumeAction.ControlVerb,
                                0,
                                0,
                                0,
                                instruction.ControlVerb,
                                instruction.PrimaryTarget,
                                inputIndex,
                                instruction.ControlNameId),
                            depthBase,
                            subroutineCalls.Count,
                            ref budget);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.MatchBoundaryReset:
                        reportedStartOffsetInBytes = inputIndex;
                        matchBoundaryWasReset = true;
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Accept:
                        var endAnchored = !isAssertion && (program.Request.Options & Pcre2CompileOptions.EndAnchored) != 0 ||
                            (matchOptions & Pcre2MatchOptions.EndAnchored) != 0;
                        var emptyDisallowed = (matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && inputIndex == reportedStartOffsetInBytes ||
                            (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0 && candidate == firstMatchingPosition && inputIndex == reportedStartOffsetInBytes;
                        if ((!endAnchored || inputIndex == input.Length) && !emptyDisallowed)
                        {
                            ThrowIfDisallowedNonMonotoneLookaroundReset(
                                program,
                                isAssertion,
                                matchBoundaryWasReset,
                                reportedStartOffsetInBytes,
                                candidate,
                                inputIndex);
                            var captures = MaterializeCaptureResult(
                                captureMaterialization,
                                program.CaptureSlotCount,
                                reportedStartOffsetInBytes,
                                inputIndex,
                                captureStarts,
                                captureEnds,
                                ref projectionInput, ref budget);
                            match = new Pcre2BacktrackingMatch(
                                true,
                                reportedStartOffsetInBytes,
                                inputIndex,
                                candidate,
                                inputIndex,
                                matchBoundaryWasReset,
                                captures,
                                GetMark(program, currentMarkId));
                            failure = default;
                            return true;
                        }
                        break;

                    default:
                        break;
                }

                if (!TryResume(
                        ref frames,
                        ref repeatMutations,
                        ref captureMutations,
                        ref subroutineCalls,
                        ref subroutineCallMutations,
                        ref atomicCheckpoints,
                        ref markMutations,
                        ref markTrail,
                        counts,
                        positions,
                        captureStarts,
                        captureEnds,
                        captureOpenStarts,
                        captureOwners,
                        captureOpenOwners,
                        ref reportedStartOffsetInBytes,
                        ref matchBoundaryWasReset,
                        ref currentMarkId,
                        ref currentMarkPosition,
                        ref budget,
                        out instructionIndex,
                        out inputIndex,
                        out failure))
                {
                    match = new Pcre2BacktrackingMatch(
                        false,
                        0,
                        0,
                        0,
                        0,
                        false,
                        default,
                        GetMark(program, lastEncounteredMarkId));
                    return false;
                }
            }
        }
        finally
        {
            var frameRents = frames.RentCount - initialFrameRents;
            var frameGrowths = initialFrameRents == 0 ? Math.Max(0, frameRents - 1) : frameRents;
            var repeatRents = repeatMutations.RentCount - initialRepeatRents;
            var repeatGrowths = initialRepeatRents == 0 ? Math.Max(0, repeatRents - 1) : repeatRents;
            var stackRents = frameRents +
                repeatRents +
                captureMutations.RentCount +
                subroutineCalls.RentCount +
                subroutineCallMutations.RentCount +
                atomicCheckpoints.RentCount +
                markMutations.RentCount +
                markTrail.RentCount;
            var stackGrowths = frameGrowths +
                repeatGrowths +
                Math.Max(0, captureMutations.RentCount - 1) +
                Math.Max(0, subroutineCalls.RentCount - 1) +
                Math.Max(0, subroutineCallMutations.RentCount - 1) +
                Math.Max(0, atomicCheckpoints.RentCount - 1) +
                Math.Max(0, markMutations.RentCount - 1) +
                Math.Max(0, markTrail.RentCount - 1);
            var fixedRents = !acquiredWorkspace ? 0 : (rentedRepeatState is null ? 0 : 1) +
                (rentedCaptureState is null ? 0 : 1) +
                (rentedCaptureRestoreGenerations is null ? 0 : 1) +
                (rentedCaptureRestoreSlots is null ? 0 : 1) +
                (rentedCaptureRestoreSnapshots is null ? 0 : 1) +
                (rentedRepeatRestoreGenerations is null ? 0 : 1) +
                (rentedRepeatRestoreIds is null ? 0 : 1) +
                (rentedRepeatRestoreSnapshots is null ? 0 : 1);
            budget.RecordWorkspacePoolTraffic(
                (ulong)fixedRents,
                (ulong)frameRents,
                (ulong)repeatRents,
                (ulong)captureMutations.RentCount,
                (ulong)(stackRents - frameRents - repeatRents - captureMutations.RentCount),
                (ulong)stackGrowths);
            markTrail.Dispose(ref budget);
            markMutations.Dispose(ref budget);
            atomicCheckpoints.Dispose(ref budget);
            subroutineCallMutations.Dispose(ref budget);
            subroutineCalls.Dispose(ref budget);
            captureMutations.Dispose(ref budget);
            // The caller owns both capacities across top-level attempts. No
            // logical rollback state survives; nested assertions own separate stacks.
            repeatMutations.Truncate(0);
            frames.Truncate(0);

        }
    }

    private static bool TryEvaluateAssertion(
        Pcre2BacktrackingProgram assertionProgram,
        Pcre2AssertionKind assertionKind,
        ReadOnlySpan<byte> input,
        ref Utf8ValidatedInput projectionInput,
        int inputIndex,
        int outerFirstMatchingPosition,
        Pcre2MatchOptions outerMatchOptions,
        ReadOnlySpan<int> captureStarts,
        ReadOnlySpan<int> captureEnds,
        int depth,
        ref Pcre2ResourceBudget budget,
        out Pcre2CaptureByteRange[] captures,
        out string? mark,
        out int reportedStartOffsetInBytes)
    {
        budget.ChargeFrame((uint)depth);
        var isNegative = assertionKind is Pcre2AssertionKind.NegativeLookahead or Pcre2AssertionKind.NegativeLookbehind;
        var isLookbehind = assertionKind is Pcre2AssertionKind.PositiveLookbehind or Pcre2AssertionKind.NegativeLookbehind;
        var nestedOptions = outerMatchOptions & (Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol);
        nestedOptions |= Pcre2MatchOptions.Anchored;
        var captureMaterialization = !captureStarts.IsEmpty && assertionProgram.HasCaptureWrites
            ? Pcre2CaptureMaterialization.AssertionRanges
            : Pcre2CaptureMaterialization.None;

        if (!isLookbehind)
        {
            budget.ChargeCandidate();
            var matched = TryMatchAt(
                assertionProgram,
                input,
                ref projectionInput,
                inputIndex,
                outerFirstMatchingPosition,
                nestedOptions,
                captureMaterialization,
                captureStarts,
                captureEnds,
                depth,
                true,
                ref budget,
                out var assertionMatch,
                out _);
            captures = matched ? assertionMatch.CaptureResult.AssertionRanges : [];
            mark = matched ? assertionMatch.Mark : null;
            reportedStartOffsetInBytes = matched && !isNegative && assertionMatch.MatchBoundaryWasReset
                ? assertionMatch.StartOffsetInBytes
                : -1;
            return isNegative ? !matched : matched;
        }

        nestedOptions |= Pcre2MatchOptions.EndAnchored;
        var earliest = inputIndex;
        var maximumLength = assertionProgram.MaximumScalarLength;
        for (var scalarCount = 0; scalarCount < maximumLength &&
             TryRetreatScalar(input, earliest, out var previous); scalarCount++)
        {
            earliest = previous;
        }

        var latest = inputIndex;
        for (var scalarCount = 0; scalarCount < assertionProgram.MinimumScalarLength; scalarCount++)
        {
            if (!TryRetreatScalar(input, latest, out latest))
            {
                captures = [];
                mark = null;
                reportedStartOffsetInBytes = -1;
                return isNegative;
            }
        }

        var candidate = earliest;
        while (candidate <= latest)
        {
            budget.ChargeCandidate();
            if (TryMatchAt(
                    assertionProgram,
                    input[..inputIndex],
                    ref projectionInput,
                    candidate,
                    outerFirstMatchingPosition,
                    nestedOptions,
                    captureMaterialization,
                    captureStarts,
                    captureEnds,
                    depth,
                    true,
                    ref budget,
                    out var assertionMatch,
                    out _))
            {
                captures = assertionMatch.CaptureResult.AssertionRanges;
                mark = assertionMatch.Mark;
                reportedStartOffsetInBytes = !isNegative && assertionMatch.MatchBoundaryWasReset
                    ? assertionMatch.StartOffsetInBytes
                    : -1;
                return !isNegative;
            }

            if (candidate == latest ||
                Rune.DecodeFromUtf8(input[candidate..latest], out _, out var width) != OperationStatus.Done)
            {
                break;
            }

            candidate += width;
        }

        captures = [];
        mark = null;
        reportedStartOffsetInBytes = -1;
        return isNegative;
    }

    private static void ThrowIfDisallowedNonMonotoneLookaroundReset(
        Pcre2BacktrackingProgram program,
        bool isAssertion,
        bool matchBoundaryWasReset,
        int reportedStartOffsetInBytes,
        int consumedStartOffsetInBytes,
        int consumedEndOffsetInBytes)
    {
        if (!isAssertion && matchBoundaryWasReset &&
            (reportedStartOffsetInBytes < consumedStartOffsetInBytes ||
             reportedStartOffsetInBytes > consumedEndOffsetInBytes) &&
            !program.Request.Settings.AllowLookaroundBackslashK)
        {
            throw new Pcre2MatchException(
                "disallowed use of \\K in lookaround",
                Pcre2ErrorKind.DisallowedLookaroundBackslashK);
        }
    }

    private static bool IsAnyCaptureSet(
        ReadOnlySpan<int> slots,
        ReadOnlySpan<int> captureStarts,
        ReadOnlySpan<int> captureEnds)
    {
        foreach (var slot in slots)
        {
            if ((uint)slot < (uint)captureStarts.Length &&
                captureStarts[slot] >= 0 &&
                captureEnds[slot] >= captureStarts[slot])
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSubroutineActive(
        int slot,
        Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> calls)
    {
        for (var index = calls.Count - 1; index >= 0; index--)
        {
            if (calls[index].SubroutineSlot == slot)
            {
                return true;
            }
        }

        return false;
    }

    private static void MergeAssertionCaptures(
        ReadOnlySpan<Pcre2CaptureByteRange> assertionCaptures,
        int callDepth,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        Span<int> captureOwners,
        Span<int> captureOpenOwners,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2ResourceBudget budget)
    {
        for (var slot = 1; slot < assertionCaptures.Length; slot++)
        {
            if ((slot & 63) == 0) budget.ChargeWorkspaceStep();
            var capture = assertionCaptures[slot];
            if (!capture.Success)
            {
                continue;
            }

            SetCapture(
                slot,
                capture.StartOffsetInBytes,
                capture.EndOffsetInBytes,
                -1,
                callDepth,
                -1,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners,
                ref captureMutations,
                ref budget, recordRollback: true);
        }
    }

    private static void MergeAssertionMark(
        Pcre2BacktrackingProgram program,
        string? assertionMark,
        int inputIndex,
        ref int currentMarkId,
        ref int currentMarkPosition,
        ref int lastEncounteredMarkId,
        ref Pcre2InvocationStateStack<Pcre2MarkMutation> markMutations,
        ref Pcre2InvocationStateStack<Pcre2MarkTrailEntry> markTrail,
        ref Pcre2ResourceBudget budget)
    {
        if (assertionMark is null)
        {
            return;
        }

        var markId = Array.IndexOf(program.MarkNames, assertionMark);
        SetMark(
            markId,
            inputIndex,
            false,
            ref currentMarkId,
            ref currentMarkPosition,
            ref lastEncounteredMarkId,
            ref markMutations,
            ref markTrail,
            ref budget);
    }

    private static void CloseOpenCaptures(
        int inputIndex,
        int callDepth,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        Span<int> captureOwners,
        Span<int> captureOpenOwners,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2ResourceBudget budget)
    {
        for (var slot = 1; slot < captureOpenStarts.Length; slot++)
        {
            if (captureOpenStarts[slot] < 0 || captureOpenOwners[slot] != callDepth)
            {
                continue;
            }

            SetCapture(
                slot,
                captureOpenStarts[slot],
                inputIndex,
                -1,
                callDepth,
                -1,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners,
                ref captureMutations,
                ref budget, recordRollback: true);
        }
    }

    private static bool TryRetreatScalar(ReadOnlySpan<byte> input, int position, out int previous)
    {
        if (position == 0 ||
            Rune.DecodeLastFromUtf8(input[..position], out _, out var width) != OperationStatus.Done)
        {
            previous = position;
            return false;
        }

        previous = position - width;
        return true;
    }

    private static void PushFrame(
        scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        Pcre2BacktrackingFrame frame,
        int depthBase,
        int callCount,
        ref Pcre2ResourceBudget budget)
    {
        var nextDepth = checked(depthBase + callCount + frames.Count + 1);
        budget.ChargeFrame((uint)nextDepth);
        if (!frames.TryPush(frame, ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
        }
    }

    private static bool TryResume(
        scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> subroutineCalls,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallMutation> subroutineCallMutations,
        ref Pcre2InvocationStateStack<Pcre2AtomicCheckpoint> atomicCheckpoints,
        ref Pcre2InvocationStateStack<Pcre2MarkMutation> markMutations,
        ref Pcre2InvocationStateStack<Pcre2MarkTrailEntry> markTrail,
        Span<int> counts,
        Span<int> positions,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        Span<int> captureOwners,
        Span<int> captureOpenOwners,
        ref int reportedStartOffsetInBytes,
        ref bool matchBoundaryWasReset,
        ref int currentMarkId,
        ref int currentMarkPosition,
        ref Pcre2ResourceBudget budget,
        out int instructionIndex,
        out int inputIndex,
        out Pcre2BacktrackingFailure failure)
    {
        while (frames.Count != 0)
        {
            var frame = frames.Pop();
            RollbackRepeats(frame.RepeatCheckpoint, ref repeatMutations, counts, positions);
            RollbackCaptures(
                frame.CaptureCheckpoint,
                ref captureMutations,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners);
            RollbackSubroutineCalls(
                frame.CallCheckpoint,
                ref subroutineCalls,
                ref subroutineCallMutations, ref budget);
            atomicCheckpoints.Truncate(frame.AtomicCheckpoint);
            RollbackMarks(
                frame.MarkMutationCheckpoint,
                frame.MarkTrailCheckpoint,
                ref markMutations,
                ref markTrail,
                ref currentMarkId,
                ref currentMarkPosition);
            reportedStartOffsetInBytes = frame.ReportedStartOffsetInBytes;
            matchBoundaryWasReset = frame.MatchBoundaryWasReset;
            if (frame.ResumeAction == Pcre2BacktrackingResumeAction.EnterRepeat)
            {
                SetRepeat(
                    frame.RepeatId,
                    frame.RepeatCount,
                    frame.RepeatPosition,
                    counts,
                    positions,
                    ref repeatMutations,
                    ref budget);
            }

            if (frame.ResumeAction != Pcre2BacktrackingResumeAction.ControlVerb)
            {
                instructionIndex = frame.Instruction;
                inputIndex = frame.InputOffsetInBytes;
                failure = default;
                return true;
            }

            if (subroutineCalls.Count != 0)
            {
                var call = subroutineCalls[subroutineCalls.Count - 1];
                frames.Truncate(Math.Min(frames.Count, call.BacktrackingCheckpoint));
                continue;
            }

            if (frame.ControlVerb == Pcre2ControlVerbKind.Then && frame.ControlTarget >= 0)
            {
                var targetFrame = -1;
                for (var index = frames.Count - 1; index >= 0; index--)
                {
                    if (frames[index].Instruction == frame.ControlTarget &&
                        frames[index].ResumeAction == Pcre2BacktrackingResumeAction.None)
                    {
                        targetFrame = index;
                        break;
                    }
                }

                if (targetFrame >= 0)
                {
                    frames.Truncate(targetFrame + 1);
                    continue;
                }
            }

            if (frame.ControlVerb == Pcre2ControlVerbKind.Skip && frame.ControlNameId >= 0)
            {
                var namedSkipPosition = FindMarkPosition(frame.ControlNameId, markTrail);
                if (namedSkipPosition < 0)
                {
                    continue;
                }

                instructionIndex = 0;
                inputIndex = 0;
                failure = new Pcre2BacktrackingFailure(
                    Pcre2BacktrackingFailureKind.Skip,
                    namedSkipPosition);
                return false;
            }

            instructionIndex = 0;
            inputIndex = 0;
            failure = frame.ControlVerb switch
            {
                Pcre2ControlVerbKind.Commit => new Pcre2BacktrackingFailure(
                    Pcre2BacktrackingFailureKind.Commit,
                    0),
                Pcre2ControlVerbKind.Skip => new Pcre2BacktrackingFailure(
                    Pcre2BacktrackingFailureKind.Skip,
                    frame.ControlPosition),
                _ => new Pcre2BacktrackingFailure(
                    Pcre2BacktrackingFailureKind.Prune,
                    0),
            };
            return false;
        }

        instructionIndex = 0;
        inputIndex = 0;
        failure = default;
        return false;
    }

    private static void PushSubroutineCall(
        Pcre2SubroutineCallFrame frame,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> calls,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallMutation> mutations,
        int depthBase,
        ref Pcre2ResourceBudget budget)
    {
        var nextDepth = checked(depthBase + calls.Count + 1);
        budget.ChargeFrame((uint)nextDepth);
        if (!calls.TryPush(frame, ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
        }

        RecordSubroutineCallMutation(
            Pcre2SubroutineCallMutationKind.Pushed,
            frame,
            ref mutations,
            ref budget);
    }

    private static void RestoreSubroutineState(
        Pcre2SubroutineCallFrame call,
        Span<int> counts,
        Span<int> positions,
        Span<int> captureStarts,
        Span<int> captureEnds,
        Span<int> captureOpenStarts,
        Span<int> captureOwners,
        Span<int> captureOpenOwners,
        Span<int> repeatRestoreGenerations,
        Span<int> repeatRestoreIds,
        Span<Pcre2SubroutineRepeatSnapshot> repeatRestoreSnapshots,
        Span<int> captureRestoreGenerations,
        Span<int> captureRestoreSlots,
        Span<Pcre2SubroutineCaptureSnapshot> captureRestoreSnapshots,
        ref int restoreGeneration,
        ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2ResourceBudget budget)
    {
        restoreGeneration++;
        if (restoreGeneration <= 0)
        {
            repeatRestoreGenerations.Clear();
            captureRestoreGenerations.Clear();
            restoreGeneration = 1;
        }

        var generation = restoreGeneration;

        var repeatRestoreCount = 0;
        var repeatMutationCount = repeatMutations.Count;
        for (var index = call.RepeatCheckpoint; index < repeatMutationCount; index++)
        {
            var mutation = repeatMutations[index];
            if (repeatRestoreGenerations[mutation.RepeatId] == generation)
            {
                continue;
            }

            repeatRestoreGenerations[mutation.RepeatId] = generation;
            repeatRestoreIds[repeatRestoreCount++] = mutation.RepeatId;
            repeatRestoreSnapshots[mutation.RepeatId] = new Pcre2SubroutineRepeatSnapshot(
                mutation.PreviousCount,
                mutation.PreviousPosition);
        }

        for (var index = 0; index < repeatRestoreCount; index++)
        {
            var repeatId = repeatRestoreIds[index];
            var snapshot = repeatRestoreSnapshots[repeatId];
            SetRepeat(
                repeatId,
                snapshot.Count,
                snapshot.Position,
                counts,
                positions,
                ref repeatMutations,
                ref budget);
        }

        var captureRestoreCount = 0;
        var captureMutationCount = captureMutations.Count;
        for (var index = call.CaptureCheckpoint; index < captureMutationCount; index++)
        {
            var mutation = captureMutations[index];
            if (captureRestoreGenerations[mutation.Slot] == generation)
            {
                continue;
            }

            captureRestoreGenerations[mutation.Slot] = generation;
            captureRestoreSlots[captureRestoreCount++] = mutation.Slot;
            captureRestoreSnapshots[mutation.Slot] = new Pcre2SubroutineCaptureSnapshot(
                mutation.PreviousStart,
                mutation.PreviousEnd,
                mutation.PreviousOpenStart,
                mutation.PreviousOwner,
                mutation.PreviousOpenOwner);
        }

        // PCRE2 restores caller capture state on return, including unset slots.
        // Non-atomic re-entry still needs the mutation trail below.
        for (var index = 0; index < captureRestoreCount; index++)
        {
            var slot = captureRestoreSlots[index];
            var snapshot = captureRestoreSnapshots[slot];
            SetCapture(
                slot,
                snapshot.Start,
                snapshot.End,
                snapshot.OpenStart,
                snapshot.Owner,
                snapshot.OpenOwner,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners,
                ref captureMutations,
                ref budget, recordRollback: true);
        }
    }

    private static void RecordSubroutineCallMutation(
        Pcre2SubroutineCallMutationKind kind,
        Pcre2SubroutineCallFrame frame,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallMutation> mutations,
        ref Pcre2ResourceBudget budget)
    {
        if (!mutations.TryPush(new Pcre2SubroutineCallMutation(kind, frame), ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }
    }

    private static void RollbackSubroutineCalls(
        int checkpoint,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> calls,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallMutation> mutations,
        ref Pcre2ResourceBudget budget)
    {
        while (mutations.Count > checkpoint)
        {
            var mutation = mutations.Pop();
            if (mutation.Kind == Pcre2SubroutineCallMutationKind.Pushed)
            {
                _ = calls.Pop();
            }
            else if (!calls.TryPush(mutation.Frame, ref budget))
            {
                throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
            }
        }
    }

    private static void SetMark(
        int markId,
        int position,
        bool addToTrail,
        ref int currentMarkId,
        ref int currentMarkPosition,
        ref int lastEncounteredMarkId,
        ref Pcre2InvocationStateStack<Pcre2MarkMutation> mutations,
        ref Pcre2InvocationStateStack<Pcre2MarkTrailEntry> trail,
        ref Pcre2ResourceBudget budget)
    {
        if (markId < 0)
        {
            return;
        }

        if (!mutations.TryPush(new Pcre2MarkMutation(currentMarkId, currentMarkPosition), ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }

        if (addToTrail && !trail.TryPush(new Pcre2MarkTrailEntry(markId, position), ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }

        currentMarkId = markId;
        currentMarkPosition = position;
        lastEncounteredMarkId = markId;
    }

    private static void RollbackMarks(
        int mutationCheckpoint,
        int trailCheckpoint,
        ref Pcre2InvocationStateStack<Pcre2MarkMutation> mutations,
        ref Pcre2InvocationStateStack<Pcre2MarkTrailEntry> trail,
        ref int currentMarkId,
        ref int currentMarkPosition)
    {
        while (mutations.Count > mutationCheckpoint)
        {
            var mutation = mutations.Pop();
            currentMarkId = mutation.PreviousMarkId;
            currentMarkPosition = mutation.PreviousPosition;
        }

        trail.Truncate(trailCheckpoint);
    }

    private static int FindMarkPosition(
        int markId,
        Pcre2InvocationStateStack<Pcre2MarkTrailEntry> trail)
    {
        for (var index = trail.Count - 1; index >= 0; index--)
        {
            var mark = trail[index];
            if (mark.MarkId == markId)
            {
                return mark.Position;
            }
        }

        return -1;
    }

    private static string? GetMark(Pcre2BacktrackingProgram program, int markId) =>
        (uint)markId < (uint)program.MarkNames.Length ? program.MarkNames[markId] : null;

    private static void SetRepeat(
        int repeatId,
        int count,
        int position,
        Span<int> counts,
        Span<int> positions,
        ref Pcre2InvocationStateStack<Pcre2RepeatMutation> mutations,
        ref Pcre2ResourceBudget budget)
    {
        if (!mutations.TryPush(new Pcre2RepeatMutation(repeatId, counts[repeatId], positions[repeatId]), ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }

        counts[repeatId] = count;
        positions[repeatId] = position;
    }

    private static void RollbackRepeats(
        int checkpoint,
        ref Pcre2InvocationStateStack<Pcre2RepeatMutation> mutations,
        Span<int> counts,
        Span<int> positions)
    {
        while (mutations.Count > checkpoint)
        {
            var mutation = mutations.Pop();
            counts[mutation.RepeatId] = mutation.PreviousCount;
            positions[mutation.RepeatId] = mutation.PreviousPosition;
        }
    }

    private static void SetCapture(
        int slot,
        int start,
        int end,
        int openStart,
        int owner,
        int openOwner,
        Span<int> starts,
        Span<int> ends,
        Span<int> openStarts,
        Span<int> owners,
        Span<int> openOwners,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> mutations,
        ref Pcre2ResourceBudget budget,
        bool recordRollback)
    {
        // Future frames checkpoint the current capture values. Only existing
        // backtracking frames or subroutine returns need an earlier value.
        if (recordRollback && !mutations.TryPush(new Pcre2CaptureMutation(
                slot,
                starts[slot],
                ends[slot],
                openStarts[slot],
                owners[slot],
                openOwners[slot]), ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }

        starts[slot] = start;
        ends[slot] = end;
        openStarts[slot] = openStart;
        owners[slot] = owner;
        openOwners[slot] = openOwner;
    }

    private static void RollbackCaptures(
        int checkpoint,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> mutations,
        Span<int> starts,
        Span<int> ends,
        Span<int> openStarts,
        Span<int> owners,
        Span<int> openOwners)
    {
        while (mutations.Count > checkpoint)
        {
            var mutation = mutations.Pop();
            starts[mutation.Slot] = mutation.PreviousStart;
            ends[mutation.Slot] = mutation.PreviousEnd;
            openStarts[mutation.Slot] = mutation.PreviousOpenStart;
            owners[mutation.Slot] = mutation.PreviousOwner;
            openOwners[mutation.Slot] = mutation.PreviousOpenOwner;
        }
    }

    private static bool TryMatchBackreference(
        ReadOnlySpan<byte> input,
        int captureStart,
        int captureEnd,
        int subjectStart,
        Pcre2CharacterOptions options,
        out int subjectEnd)
    {
        var capture = input[captureStart..captureEnd];
        if ((options & Pcre2CharacterOptions.Caseless) == 0)
        {
            if (subjectStart <= input.Length - capture.Length &&
                input.Slice(subjectStart, capture.Length).SequenceEqual(capture))
            {
                subjectEnd = subjectStart + capture.Length;
                return true;
            }

            subjectEnd = subjectStart;
            return false;
        }

        var captureIndex = captureStart;
        var subjectIndex = subjectStart;
        while (captureIndex < captureEnd)
        {
            if (subjectIndex >= input.Length ||
                Rune.DecodeFromUtf8(input[captureIndex..captureEnd], out var capturedRune, out var capturedWidth) != OperationStatus.Done ||
                Rune.DecodeFromUtf8(input[subjectIndex..], out var subjectRune, out var subjectWidth) != OperationStatus.Done ||
                !Pcre2CharacterSemantics.Equals(capturedRune, subjectRune, options))
            {
                subjectEnd = subjectStart;
                return false;
            }

            captureIndex += capturedWidth;
            subjectIndex += subjectWidth;
        }

        subjectEnd = subjectIndex;
        return true;
    }

    private static Pcre2CaptureByteRange[] MaterializeCaptures(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds,
        ref Pcre2ResourceBudget budget)
    {
        var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2CaptureByteRange>(captureSlotCount);
        budget.ReserveWorking(bytes);
        Pcre2CaptureByteRange[]? captures = null;
        try
        {
            budget.ChargeWorkspaceStep();
            captures = Utf8ExactStatePool<Pcre2CaptureByteRange>.Rent(captureSlotCount, budget.CanRetainWorkspace);
            captures[0] = new Pcre2CaptureByteRange(true, matchStart, matchEnd);
            for (var slot = 1; slot < captureSlotCount; slot++)
            {
                if ((slot & 63) == 0) budget.ChargeWorkspaceStep();
                var start = captureStarts[slot];
                var end = captureEnds[slot];
                captures[slot] = start >= 0 && end >= start
                    ? new Pcre2CaptureByteRange(true, start, end)
                    : new Pcre2CaptureByteRange(false, 0, 0);
            }
            return captures;
        }
        catch
        {
            if (captures is not null) Utf8ExactStatePool<Pcre2CaptureByteRange>.Return(captures, budget.CanRetainWorkspace);
            budget.ReleaseWorking(bytes);
            throw;
        }
    }

    private static void ReleaseAssertionCaptures(Pcre2CaptureByteRange[] captures, ref Pcre2ResourceBudget budget)
    {
        Utf8ExactStatePool<Pcre2CaptureByteRange>.Return(captures, budget.CanRetainWorkspace);
        budget.ReleaseWorking(Utf8ConstructionBudget.ArrayBytes<Pcre2CaptureByteRange>(captures.Length));
    }

    private static Pcre2BacktrackingCaptureResult MaterializeCaptureResult(
        Pcre2CaptureMaterialization materialization,
        int captureSlotCount,
        int matchStart,
        int matchEnd,
        ReadOnlySpan<int> captureStarts,
        ReadOnlySpan<int> captureEnds,
        ref Utf8ValidatedInput input, ref Pcre2ResourceBudget budget) => materialization switch
        {
            Pcre2CaptureMaterialization.AssertionRanges =>
                Pcre2BacktrackingCaptureResult.FromAssertionRanges(
                    MaterializeCaptures(captureSlotCount, matchStart, matchEnd, captureStarts, captureEnds, ref budget)),
            Pcre2CaptureMaterialization.ProjectedFinalSlots =>
                Pcre2BacktrackingCaptureResult.FromProjectedGroups(
                    Pcre2Runner.ProjectCaptures(
                        captureSlotCount,
                        matchStart,
                        matchEnd,
                        captureStarts,
                        captureEnds,
                        ref input, ref budget)),
            _ => default,
        };
}
