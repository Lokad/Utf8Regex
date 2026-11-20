using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

// backtracking execution state and runner, split from the compiler collection along the execution ownership boundary.
internal enum Pcre2BacktrackingResumeAction : byte
{
    None = 0,
    EnterRepeat = 1,
    ControlVerb = 2,
    RetreatClassRepeat = 3,
}

// A control frame stores its THEN target in Instruction and its SKIP
// name in RepeatIdOrNameId. Its input offset is already the SKIP position.
// A class-repeat frame stores the last tried endpoint in InputOffsetInBytes
// and the number of shorter choices in RepeatCount.
internal readonly record struct Pcre2BacktrackingFrame(
    int Instruction,
    int InputOffsetInBytes,
    int ReportedStartOffsetInBytes,
    int RepeatCheckpoint,
    int CaptureCheckpoint,
    int CallCheckpoint,
    int AtomicCheckpoint,
    int MarkMutationCheckpoint,
    int MarkTrailCheckpoint,
    bool MatchBoundaryWasReset,
    Pcre2BacktrackingResumeAction ResumeAction = Pcre2BacktrackingResumeAction.None,
    Pcre2ControlVerbKind ControlVerb = default,
    int RepeatIdOrNameId = 0,
    int RepeatCount = 0,
    int RepeatPosition = 0);

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
    int MarkTrailCheckpoint)
{
    // Undoing a push only pops the current call. Every real return address
    // is nonnegative, so this entry needs no second tag beside the frame.
    internal static Pcre2SubroutineCallFrame UndoPush => new(-1, 0, 0, 0, 0, 0, 0);
}

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

    internal static Pcre2BacktrackingCaptureResult FromByteGroups(Utf8Pcre2CaptureRange[] groups) => new(groups);
    internal Utf8Pcre2CaptureRange[] ByteGroups => _value as Utf8Pcre2CaptureRange[] ?? [];

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
    ByteFinalSlots = 3,
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
        in Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget, out string? mark)
    {
        var match = MatchCore(
            program,
            candidateSearch,
            ref input,
            start,
            start,
            matchOptions,
            Pcre2CaptureMaterialization.None,
            ref budget);
        mark = match.Mark;
        return match.ToCharacterMatch();
    }

    internal static Pcre2InvocationStateStack<Pcre2BacktrackingFrame> CreateFrames(
        scoped ref Pcre2ResourceBudget budget, int depthBase)
    {
        var limit = budget.Limits.DepthLimit == 0 ? int.MaxValue : (int)Math.Min(budget.Limits.DepthLimit, int.MaxValue);
        return new(limit == int.MaxValue ? limit : Math.Max(0, limit - depthBase), budget.CollectsDiagnostics);
    }

    internal static Pcre2CharacterMatch Match(
        Pcre2BacktrackingProgram program, in Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input, Utf8BytePosition searchStart, Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions, ref Pcre2ResourceBudget budget,
        scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace)
        => MatchCore(program, candidateSearch, ref input, searchStart, firstMatchingPosition,
            matchOptions, Pcre2CaptureMaterialization.None, ref budget, ref frames, ref repeatMutations, ref workspace).ToCharacterMatch();

    internal static Pcre2BacktrackingMatch MatchDetailed(
        Pcre2BacktrackingProgram program,
        in Pcre2CandidateSearchProgram candidateSearch,
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
        in Pcre2CandidateSearchProgram candidateSearch,
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

    internal static Pcre2BacktrackingMatch MatchByteRanges(Pcre2BacktrackingProgram program,
        in Pcre2CandidateSearchProgram candidateSearch, ref Utf8ValidatedInput input,
        Utf8BytePosition start, Utf8BytePosition firstMatchingPosition, Pcre2MatchOptions options,
        ref Pcre2ResourceBudget budget, scoped Span<Utf8Pcre2CaptureRange> destination = default)
        => MatchCore(program, candidateSearch, ref input, start, firstMatchingPosition,
            options, Pcre2CaptureMaterialization.ByteFinalSlots, ref budget, destination);

    private static Pcre2BacktrackingMatch MatchCore(
        Pcre2BacktrackingProgram program,
        in Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ref Pcre2ResourceBudget budget, scoped Span<Utf8Pcre2CaptureRange> destination = default)
    {
        var frames = CreateFrames(ref budget, 0);
        var repeatMutations = new Pcre2InvocationStateStack<Pcre2RepeatMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var workspace = new Pcre2BacktrackingWorkspace();
        try
        {
            return MatchCore(program, candidateSearch, ref input, start, firstMatchingPosition,
                matchOptions, captureMaterialization, ref budget, ref frames, ref repeatMutations, ref workspace, destination);
        }
        finally { workspace.Dispose(ref budget); repeatMutations.Dispose(ref budget); frames.Dispose(ref budget); }
    }

    private static Pcre2BacktrackingMatch MatchCore(
        Pcre2BacktrackingProgram program,
        in Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ref Pcre2ResourceBudget budget,
        scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace, scoped Span<Utf8Pcre2CaptureRange> destination = default)
    {
        var bytes = input.Bytes;
        var (lastScalars, allowTrailingLf, canMatchEmpty) = program.AnchoredFinalAscii;
        if (lastScalars.HasValue && matchOptions == Pcre2MatchOptions.None &&
            lastScalars.ExcludesFinalScalar(bytes, allowTrailingLf, canMatchEmpty))
        {
            budget.ChargeCandidate();
            budget.CheckDeadline();
            return default;
        }
        var anchored = program.RestrictsSearchToInitialCandidate ||
            (program.Request.Options & Pcre2CompileOptions.Anchored) != 0 ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0;
        var candidate = start.Value;
        var maximumCandidate = program.MinimumByteLength > bytes.Length
            ? -1
            : bytes.Length - program.MinimumByteLength;
        var meterLiteralPrefix = candidateSearch.Kind == Pcre2CandidateSearchKind.BranchLeadingLiterals &&
            !budget.WorkingInvocation.IsUnmetered;
        var meterDelimiter = candidateSearch.Kind is (Pcre2CandidateSearchKind.LeadingRunThenLiteral or
            Pcre2CandidateSearchKind.BoundedLiteralWindow) &&
            !budget.WorkingInvocation.IsUnmetered;
        var useCandidateSearch = !anchored &&
            candidateSearch.HasValue &&
            matchOptions == Pcre2MatchOptions.None &&
            (candidate < maximumCandidate || candidateSearch.Kind != Pcre2CandidateSearchKind.BranchLeadingLiterals) &&
            (budget.IsUnmetered || meterLiteralPrefix || meterDelimiter || candidateSearch.MinimumWordRunLength > 0);
        var candidateState = new PreparedSearchScanState(
            candidate,
            new PreparedMultiLiteralScanState(candidate, candidate, 0));
        var nextWindowCandidate = -1;
        var lastWindowCandidate = -1;
        var windowCandidateEnd = -1;
        string? lastMark = null;
        var skipsCrLfContinuation = program.Request.SkipsCrLfContinuation;
        while (candidate <= maximumCandidate)
        {
            if (useCandidateSearch)
            {
                var found = meterLiteralPrefix
                    ? TryFindMeteredLiteral(candidateSearch, bytes,
                        maximumCandidate, ref candidateState, ref budget, out candidate)
                    : TryFindNextCandidate(
                        candidateSearch,
                        program.Request,
                        bytes,
                        start.Value,
                        meterDelimiter,
                        ref candidateState,
                        ref nextWindowCandidate,
                        ref lastWindowCandidate,
                        ref windowCandidateEnd,
                        ref budget,
                        out candidate);
                if (!found || candidate > maximumCandidate)
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
                    out var failure, destination))
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

    private static bool TryFindMeteredLiteral(
        in Pcre2CandidateSearchProgram search,
        ReadOnlySpan<byte> input,
        int maximumStart,
        ref PreparedSearchScanState state,
        ref Pcre2ResourceBudget budget,
        out int candidate)
    {
        try
        {
            if (search.Searcher.ExactMultiLiterals is { } prefixes)
            {
                var end = maximumStart + Math.Min(search.MaximumBranchPrefixLength, input.Length - maximumStart);
                Pcre2SliceDriver.ThrowIfSliceBreached(Utf8SearchExecutor.TryFindLiteralFamilyCandidate(prefixes, false,
                    input, state.NextStart, end, false, ref budget.WorkingInvocation, out var match, out var found));
                candidate = found ? match.Index : -1;
            }
            else
            {
                var literal = search.Searcher.LiteralSearch;
                Pcre2SliceDriver.ThrowIfSliceBreached(Utf8SearchExecutor.TryFindLiteralCandidate(literal,
                    input, state.NextStart, Math.Min(maximumStart, input.Length - literal.Length),
                    false, ref budget.WorkingInvocation, out candidate));
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }

        if (candidate < 0) return false;
        // A failed verification may have consumed an overlapping prefix.
        state = new PreparedSearchScanState(candidate + 1, default);
        return true;
    }

    private static bool TryFindNextCandidate(
        in Pcre2CandidateSearchProgram candidateSearch,
        Pcre2CompileRequest request,
        ReadOnlySpan<byte> input,
        int lowerBound,
        bool meterDelimiter,
        ref PreparedSearchScanState state,
        ref int nextWindowCandidate,
        ref int lastWindowCandidate,
        ref int windowCandidateEnd,
        ref Pcre2ResourceBudget budget,
        out int candidate)
    {
        if (candidateSearch.Kind is Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun or Pcre2CandidateSearchKind.LeadingAsciiWordRun)
        {
            return TryFindNextLeadingAsciiWordRun(
                input,
                lowerBound,
                candidateSearch.MinimumWordRunLength,
                candidateSearch.Kind == Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun,
                ref nextWindowCandidate,
                ref windowCandidateEnd,
                ref lastWindowCandidate,
                ref budget,
                out candidate);
        }

        if (candidateSearch.Kind == Pcre2CandidateSearchKind.BoundedLiteralWindow)
        {
            return TryFindNextBoundedWindowCandidate(
                candidateSearch,
                input,
                lowerBound,
                meterDelimiter,
                ref state,
                ref nextWindowCandidate,
                ref lastWindowCandidate,
                ref windowCandidateEnd,
                ref budget,
                out candidate);
        }

        while (true)
        {
            int literalStart;
            if (meterDelimiter)
            {
                // A delimiter can occur beyond the last possible match start.
                if (!TryFindMeteredLiteral(candidateSearch, input, input.Length, ref state, ref budget, out literalStart)) break;
            }
            else
            {
                if (!candidateSearch.Searcher.TryFindNextOverlappingMatch(input, ref state, out var preparedCandidate)) break;
                literalStart = preparedCandidate.Index;
            }
            if (candidateSearch.Kind is Pcre2CandidateSearchKind.BranchLeadingLiterals or
                Pcre2CandidateSearchKind.LeadingAsciiSet or
                Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow)
            {
                if (candidateSearch.Kind == Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow &&
                    !MatchesCandidateWindow(input, literalStart, candidateSearch.WindowConstraint))
                {
                    continue;
                }

                candidate = literalStart;
                return true;
            }

            if (candidateSearch.Kind != Pcre2CandidateSearchKind.LeadingRunThenLiteral)
            {
                break;
            }

            var runStart = literalStart;
            var valid = true;
            var rewindSteps = 0;
            for (var i = candidateSearch.LeadingRunTokens.Length - 1; i >= 0; i--)
            {
                var consumed = false;
                while (TryRetreatScalarToBoundary(input, lowerBound, runStart, out var previous))
                {
                    if (meterDelimiter && (rewindSteps++ & 63) == 0) budget.ChargeWorkspaceStep();
                    if (!Pcre2CharacterRunner.TryMatchToken(candidateSearch.LeadingRunTokens[i], request, input,
                            previous, lowerBound, Pcre2MatchOptions.None, ref budget, out var next) || next != runStart) break;
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

    private static bool TryFindNextLeadingAsciiWordRun(
        ReadOnlySpan<byte> input,
        int lowerBound,
        int minimumWordRunLength,
        bool requiresBoundary,
        ref int scanOffset,
        ref int confirmedWordEnd,
        ref int admittedEnd,
        ref Pcre2ResourceBudget budget,
        out int candidate)
    {
        var offset = Math.Max(lowerBound, scanOffset);
        if (admittedEnd < 0)
        {
            if (budget.IsUnmetered) admittedEnd = input.Length;
            else
            {
                budget.ReserveWorking(0);
                admittedEnd = offset;
            }
        }
        var lastStart = input.Length - minimumWordRunLength;
        while (offset <= lastStart)
        {
            // A rejected byte excludes all starts through that byte. When
            // the VM needs another start in the same word, read only the
            // additional byte needed to prove its minimum.
            var requiredEnd = offset + minimumWordRunLength;
            var next = ScanAsciiWord(input, Math.Max(offset, confirmedWordEnd), requiredEnd, ref admittedEnd, ref budget);
            if (next < requiredEnd)
            {
                offset = next + 1;
                continue;
            }

            if (!requiresBoundary || offset == 0 || !Utf8AsciiBytePredicates.IsWord(input[offset - 1]))
            {
                confirmedWordEnd = requiredEnd;
                scanOffset = requiresBoundary ? requiredEnd : offset + 1;
                candidate = offset;
                return true;
            }

            // An interior offset cannot begin a boundary-qualified match.
            offset = ScanAsciiWord(input, requiredEnd, input.Length, ref admittedEnd, ref budget);
        }

        scanOffset = input.Length;
        candidate = -1;
        return false;
    }

    private static int ScanAsciiWord(ReadOnlySpan<byte> input, int offset, int end,
        ref int admittedEnd, ref Pcre2ResourceBudget budget)
    {
        while (offset < end)
        {
            if (offset >= admittedEnd)
            {
                budget.ChargeWorkspaceStep();
                admittedEnd = offset + Math.Min(2048, input.Length - offset);
            }
            var blockEnd = Math.Min(end, admittedEnd);
            while (offset < blockEnd && Utf8AsciiBytePredicates.IsWord(input[offset]))
                offset++;
            if (offset < blockEnd) break;
        }
        return offset;
    }

    private static bool TryFindNextBoundedWindowCandidate(
        in Pcre2CandidateSearchProgram candidateSearch,
        ReadOnlySpan<byte> input,
        int lowerBound,
        bool meterDelimiter,
        ref PreparedSearchScanState state,
        ref int nextWindowCandidate,
        ref int lastWindowCandidate,
        ref int windowCandidateEnd,
        ref Pcre2ResourceBudget budget,
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
                    if (candidateSearch.RequiresLeadingWordBoundary &&
                        !Pcre2CharacterSemantics.IsWordBoundary(input, current, ucp: false))
                        continue;
                    candidate = current;
                    return true;
                }
            }

            int literalStart;
            if (meterDelimiter)
            {
                // The required literal can begin after the last candidate.
                if (!TryFindMeteredLiteral(candidateSearch, input, input.Length, ref state, ref budget, out literalStart)) break;
            }
            else
            {
                if (!candidateSearch.Searcher.TryFindNextOverlappingMatch(input, ref state, out var requiredLiteral)) break;
                literalStart = requiredLiteral.Index;
            }

            nextWindowCandidate = Math.Max(
                Math.Max(lowerBound, lastWindowCandidate + 1),
                literalStart - candidateSearch.WindowConstraint.MaximumOffset);
            windowCandidateEnd = literalStart - candidateSearch.WindowConstraint.MinimumOffset;
        }

        candidate = -1;
        return false;
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
        out Pcre2BacktrackingFailure failure, scoped Span<Utf8Pcre2CaptureRange> destination = default)
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
        var subroutineCallMutations = new Pcre2InvocationStateStack<Pcre2SubroutineCallFrame>(int.MaxValue, budget.CollectsDiagnostics);
        var atomicCheckpoints = new Pcre2InvocationStateStack<Pcre2AtomicCheckpoint>(frameLimit, budget.CollectsDiagnostics);
        var markMutations = new Pcre2InvocationStateStack<Pcre2MarkMutation>(int.MaxValue, budget.CollectsDiagnostics);
        var markTrail = new Pcre2InvocationStateStack<Pcre2MarkTrailEntry>(int.MaxValue, budget.CollectsDiagnostics);
        var retainCaptures = captureMaterialization != Pcre2CaptureMaterialization.None || !initialCaptureStarts.IsEmpty;
        var greedyOptionalTail = !isAssertion && matchOptions == Pcre2MatchOptions.None
            ? program.GreedyOptionalTail : int.MaxValue;
        var needsCaptureState = program.CaptureReadSlot != 0 || retainCaptures;
        // Without output or an assertion boundary, a write to another slot
        // cannot be seen by the sole reader, even across calls and retries.
        var onlyCaptureSlot = !retainCaptures && !isAssertion && program.AssertionPrograms.Length == 0 &&
            program.CaptureReadSlot > 0 ? program.CaptureReadSlot : 0;
        var captureSlots = needsCaptureState && program.CaptureSlotCount > 1 ? program.CaptureSlotCount : 0;
        var captureStateLength = checked(captureSlots * 5);
        var restoresSubroutines = program.SubroutineTargets.Length != 0;
        // Slot zero belongs to the match result, outside the capture journal.
        var restoresMultipleCaptures = restoresSubroutines && captureSlots > 2 && onlyCaptureSlot == 0;
        var acquiredWorkspace = workspace.EnsureInitialized(program.RepeatCount, captureSlots,
            restoresSubroutines, restoresMultipleCaptures, ref budget);
        var rentedRepeatState = workspace.RepeatState;
        var rentedCaptureState = workspace.CaptureState;
        var rentedCaptureRestoreGenerations = workspace.CaptureRestoreGenerations;
        var rentedRepeatRestoreGenerations = workspace.RepeatRestoreGenerations;
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
        var repeatRestoreGenerations = rentedRepeatRestoreGenerations is null
            ? Span<int>.Empty
            : rentedRepeatRestoreGenerations.AsSpan(0, program.RepeatCount);
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
                ref readonly var instruction = ref program.Instructions[instructionIndex];
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
                        if (instructionIndex >= greedyOptionalTail)
                        {
                            var literal = program.Instructions[instruction.PrimaryTarget].Token.Literal.Value;
                            instructionIndex = inputIndex < input.Length && input[inputIndex] == literal
                                ? instruction.PrimaryTarget : instruction.SecondaryTarget;
                            continue;
                        }
                        PushFrame(
                            ref frames,
                            new Pcre2BacktrackingFrame(
                                instruction.SecondaryTarget,
                                inputIndex,
                                reportedStartOffsetInBytes,
                                repeatMutations.Count,
                                captureMutations.Count,
                                subroutineCallMutations.Count,
                                atomicCheckpoints.Count,
                                markMutations.Count,
                                markTrail.Count,
                                matchBoundaryWasReset),
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
                                            repeatMutations.Count,
                                            captureMutations.Count,
                                            subroutineCallMutations.Count,
                                            atomicCheckpoints.Count,
                                            markMutations.Count,
                                            markTrail.Count,
                                            matchBoundaryWasReset),
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
                                            repeatMutations.Count,
                                            captureMutations.Count,
                                            subroutineCallMutations.Count,
                                            atomicCheckpoints.Count,
                                            markMutations.Count,
                                            markTrail.Count,
                                            matchBoundaryWasReset,
                                            Pcre2BacktrackingResumeAction.EnterRepeat,
                                            RepeatIdOrNameId: repeatId,
                                            RepeatCount: count + 1,
                                            RepeatPosition: inputIndex),
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
                    case Pcre2BacktrackingInstructionKind.GreedyClassRepeat:
                        var canRetryRun = instruction.Kind == Pcre2BacktrackingInstructionKind.GreedyClassRepeat;
                        var runCount = 0;
                        var meterRun = !budget.IsUnmetered;
                        var asciiByteSet =
                            instruction.Token.Kind == Pcre2CharacterTokenKind.CharacterClass &&
                            (instruction.Token.Options & Pcre2CharacterOptions.Caseless) == 0
                                ? instruction.Token.CharacterClass.AsciiByteSet
                                : null;
                        while (runCount < instruction.Maximum)
                        {
                            if (meterRun) budget.ChargeWorkspaceStep();
                            if (asciiByteSet is not null && inputIndex < input.Length)
                            {
                                var remainingMaximum = instruction.Maximum - runCount;
                                var remainingLength = Math.Min(remainingMaximum, input.Length - inputIndex);
                                if (meterRun) remainingLength = Math.Min(remainingLength, 2048);
                                var stop = asciiByteSet.IndexOfAnyExcept(input.Slice(inputIndex, remainingLength));
                                var asciiCount = stop < 0 ? remainingLength : stop;
                                inputIndex += asciiCount;
                                runCount += asciiCount;
                                if (runCount >= instruction.Maximum || inputIndex >= input.Length)
                                {
                                    break;
                                }
                                if (stop < 0) continue;
                            }

                            if (!Pcre2CharacterRunner.TryMatchToken(
                                    instruction.Token,
                                    program.Request,
                                    input,
                                    inputIndex,
                                    firstMatchingPosition,
                                    matchOptions,
                                    ref budget,
                                    out var runEnd))
                            {
                                break;
                            }

                            inputIndex = runEnd;
                            runCount++;
                        }
                        if (!canRetryRun && budget.CollectsDiagnostics)
                        {
                            budget.RecordPossessiveTokenScanCharacters((ulong)runCount);
                        }

                        if (runCount < instruction.Minimum) break;
                        // Retain the visited endpoint and the number of shorter
                        // choices. The subject stays valid and unchanged.
                        if (canRetryRun && runCount > instruction.Minimum && instructionIndex + 1 < greedyOptionalTail &&
                            (instructionIndex + 2 != greedyOptionalTail ||
                             !CanEnterRequiredClass(program.Instructions[instructionIndex + 1], input, inputIndex)))
                        {
                            PushFrame(
                                ref frames,
                                new Pcre2BacktrackingFrame(
                                    instructionIndex + 1,
                                    inputIndex,
                                    reportedStartOffsetInBytes,
                                    repeatMutations.Count,
                                    captureMutations.Count,
                                    subroutineCallMutations.Count,
                                    atomicCheckpoints.Count,
                                    markMutations.Count,
                                    markTrail.Count,
                                    matchBoundaryWasReset,
                                    Pcre2BacktrackingResumeAction.RetreatClassRepeat,
                                    RepeatCount: runCount - instruction.Minimum),
                                depthBase,
                                subroutineCalls.Count,
                                ref budget);
                        }

                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.CaptureToken:
                        var tokenStart = inputIndex;
                        if (!Pcre2CharacterRunner.TryMatchToken(instruction.Token, program.Request, input,
                                inputIndex, firstMatchingPosition, matchOptions, ref budget, out inputIndex))
                            break;
                        var tokenCaptureSlot = instruction.CaptureSlot;
                        if (!captureStarts.IsEmpty && (onlyCaptureSlot == 0 || tokenCaptureSlot == onlyCaptureSlot))
                        {
                            var slot = tokenCaptureSlot;
                            var depth = subroutineCalls.Count;
                            var recordRollback = frames.Count != 0 || depth != 0;
                            if (captureStarts[slot] >= 0 && captureOwners[slot] > depth ||
                                captureOpenStarts[slot] >= 0 && captureOpenOwners[slot] > depth)
                            {
                                // The normal open would be blocked by a deeper
                                // owner. Only an opening at this depth can close.
                                tokenStart = captureOpenStarts[slot];
                                if (tokenStart < 0 || captureOpenOwners[slot] != depth)
                                {
                                    instructionIndex++;
                                    continue;
                                }
                                recordRollback = NeedsCaptureCloseRollback(slot, depth, ref frames,
                                    ref subroutineCalls, ref captureMutations);
                            }

                            SetCapture(slot, tokenStart, inputIndex, -1, depth, -1,
                                captureStarts, captureEnds, captureOpenStarts, captureOwners, captureOpenOwners,
                                ref captureMutations, ref budget, recordRollback);
                        }
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.CaptureStart:
                        var startCaptureSlot = instruction.CaptureSlot;
                        if (captureStarts.IsEmpty || onlyCaptureSlot != 0 && startCaptureSlot != onlyCaptureSlot)
                        {
                            instructionIndex++;
                            continue;
                        }

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
                        var endCaptureSlot = instruction.CaptureSlot;
                        if (captureStarts.IsEmpty || onlyCaptureSlot != 0 && endCaptureSlot != onlyCaptureSlot)
                        {
                            instructionIndex++;
                            continue;
                        }

                        var endCallDepth = subroutineCalls.Count;
                        if (captureOpenStarts[endCaptureSlot] < 0 ||
                            captureOpenOwners[endCaptureSlot] != endCallDepth)
                        {
                            instructionIndex++;
                            continue;
                        }

                        var recordClose = NeedsCaptureCloseRollback(endCaptureSlot, endCallDepth, ref frames,
                            ref subroutineCalls, ref captureMutations);
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
                            recordRollback: recordClose);
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
                            depthBase + frames.Count,
                            ref budget);
                        // Only a live retry can need the call stack before this push.
                        // A later frame checkpoints the calls in effect when it is created.
                        if (frames.Count != 0)
                            RecordSubroutineUndo(Pcre2SubroutineCallFrame.UndoPush,
                                ref subroutineCallMutations, ref budget);
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
                            captureRestoreGenerations,
                            ref subroutineRestoreGeneration,
                            ref repeatMutations,
                            ref captureMutations,
                            ref budget);
                        var completedCall = subroutineCalls.Pop();
                        if (frames.Count != 0)
                            RecordSubroutineUndo(completedCall, ref subroutineCallMutations, ref budget);
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
                                captureRestoreGenerations,
                                ref subroutineRestoreGeneration,
                                ref repeatMutations,
                                ref captureMutations,
                                ref budget);
                            var completedAcceptedCall = subroutineCalls.Pop();
                            if (frames.Count != 0)
                                RecordSubroutineUndo(completedAcceptedCall, ref subroutineCallMutations, ref budget);
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
                                ref projectionInput, ref budget, destination);
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
                                instruction.PrimaryTarget,
                                inputIndex,
                                reportedStartOffsetInBytes,
                                repeatMutations.Count,
                                captureMutations.Count,
                                subroutineCallMutations.Count,
                                atomicCheckpoints.Count,
                                markMutations.Count,
                                markTrail.Count,
                                matchBoundaryWasReset,
                                Pcre2BacktrackingResumeAction.ControlVerb,
                                instruction.ControlVerb,
                                RepeatIdOrNameId: instruction.ControlNameId),
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
                                ref projectionInput, ref budget, destination);
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
                        input,
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
                (rentedRepeatRestoreGenerations is null ? 0 : 1);
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
            if (captureStarts.IsEmpty && TryMatchStatelessLookahead(assertionProgram, input, inputIndex,
                    outerFirstMatchingPosition, nestedOptions, ref budget, out var probeMatched))
            {
                captures = [];
                mark = null;
                reportedStartOffsetInBytes = -1;
                return isNegative != probeMatched;
            }

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

    private static bool CanEnterRequiredClass(in Pcre2BacktrackingInstruction next, ReadOnlySpan<byte> input, int position)
        => next is { Kind: Pcre2BacktrackingInstructionKind.GreedyClassRepeat, Minimum: 1, Maximum: > 0 } &&
            (next.Token.Options & Pcre2CharacterOptions.Caseless) == 0 && position < input.Length &&
            next.Token.CharacterClass.AsciiByteSet is { } ascii && ascii.Contains(input[position]);

    private static bool TryMatchStatelessLookahead(
        Pcre2BacktrackingProgram program,
        ReadOnlySpan<byte> input,
        int position,
        int firstMatchingPosition,
        Pcre2MatchOptions options,
        ref Pcre2ResourceBudget budget,
        out bool matched)
    {
        matched = false;
        var instructions = program.Instructions.AsSpan();
        if (instructions.Length > 8) return false;
        foreach (ref readonly var instruction in instructions)
        {
            if (instruction.Kind is Pcre2BacktrackingInstructionKind.Token or Pcre2BacktrackingInstructionKind.CaptureStart
                or Pcre2BacktrackingInstructionKind.CaptureEnd or Pcre2BacktrackingInstructionKind.CaptureToken
                or Pcre2BacktrackingInstructionKind.Accept)
                continue;
            if (instruction.Kind != Pcre2BacktrackingInstructionKind.ControlAccept || instruction.ControlNameId >= 0)
                return false;
        }

        // Keep the zero-byte admission checkpoint and charge the same reached
        // instructions as a nested VM. A stateless lookahead has no open
        // captures to close on ACCEPT and cannot publish a mark or reset.
        budget.ReserveWorking(0);
        foreach (ref readonly var instruction in instructions)
        {
            budget.ChargeBacktracking(instruction.Kind, instruction.Token.Kind);
            if (instruction.Kind is Pcre2BacktrackingInstructionKind.Token or Pcre2BacktrackingInstructionKind.CaptureToken)
            {
                if (!Pcre2CharacterRunner.TryMatchToken(instruction.Token, program.Request, input,
                        position, firstMatchingPosition, options, ref budget, out position))
                    break;
            }
            else if (instruction.Kind is Pcre2BacktrackingInstructionKind.Accept
                or Pcre2BacktrackingInstructionKind.ControlAccept)
            {
                matched = true;
                break;
            }
        }
        return true;
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
        ReadOnlySpan<byte> input,
        scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> subroutineCalls,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> subroutineCallMutations,
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
        // Verb retries can discard several frames in one VM step. The
        // frame scan and each journal rewind check the same invocation.
        var scannedFrames = 0;
        while (frames.Count != 0)
        {
            if ((++scannedFrames & 63) == 0) budget.ChargeWorkspaceStep();
            var frame = frames.Pop();
            RollbackRepeats(frame.RepeatCheckpoint, ref repeatMutations, counts, positions, ref budget);
            RollbackCaptures(
                frame.CaptureCheckpoint,
                ref captureMutations,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners,
                ref budget);
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
                ref currentMarkPosition,
                ref budget);
            reportedStartOffsetInBytes = frame.ReportedStartOffsetInBytes;
            matchBoundaryWasReset = frame.MatchBoundaryWasReset;
            var resumeOffset = frame.InputOffsetInBytes;
            if (frame.ResumeAction == Pcre2BacktrackingResumeAction.EnterRepeat)
            {
                SetRepeat(
                    frame.RepeatIdOrNameId,
                    frame.RepeatCount,
                    frame.RepeatPosition,
                    counts,
                    positions,
                    ref repeatMutations,
                    ref budget);
            }
            else if (frame.ResumeAction == Pcre2BacktrackingResumeAction.RetreatClassRepeat)
            {
                budget.ChargeWorkspaceStep();
                if (!TryRetreatScalar(input, resumeOffset, out resumeOffset))
                    throw new InvalidOperationException("A PCRE2 class-repeat endpoint must follow a consumed scalar.");

                // The next retry uses the capacity and depth just vacated by
                // this frame, with the restored checkpoints still in force.
                if (frame.RepeatCount > 1 && !frames.TryPush(frame with
                    { InputOffsetInBytes = resumeOffset, RepeatCount = frame.RepeatCount - 1 }, ref budget))
                    throw new InvalidOperationException("The PCRE2 class-repeat frame lost its admitted capacity.");
            }

            if (frame.ResumeAction != Pcre2BacktrackingResumeAction.ControlVerb)
            {
                instructionIndex = frame.Instruction;
                inputIndex = resumeOffset;
                failure = default;
                return true;
            }

            if (subroutineCalls.Count != 0)
            {
                var call = subroutineCalls[subroutineCalls.Count - 1];
                frames.Truncate(Math.Min(frames.Count, call.BacktrackingCheckpoint));
                continue;
            }

            if (frame.ControlVerb == Pcre2ControlVerbKind.Then && frame.Instruction >= 0)
            {
                var targetFrame = -1;
                var lastFrame = frames.Count - 1;
                for (var index = lastFrame; index >= 0; index--)
                {
                    if (((lastFrame - index) & 63) == 63) budget.ChargeWorkspaceStep();
                    if (frames[index].Instruction == frame.Instruction &&
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

            if (frame.ControlVerb == Pcre2ControlVerbKind.Skip && frame.RepeatIdOrNameId >= 0)
            {
                var namedSkipPosition = FindMarkPosition(frame.RepeatIdOrNameId, markTrail, ref budget);
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
                    frame.InputOffsetInBytes),
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
        int depthBase,
        ref Pcre2ResourceBudget budget)
    {
        var nextDepth = checked(depthBase + calls.Count + 1);
        budget.ChargeFrame((uint)nextDepth);
        if (!calls.TryPush(frame, ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", Pcre2ErrorKind.DepthLimit);
        }
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
        Span<int> captureRestoreGenerations,
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

        // The first mutation of each slot records its value at call entry.
        // Freeze the journal ends so inverse entries appended during restoration
        // are available for backtracking but never visited by this return.
        var repeatMutationCount = repeatMutations.Count;
        for (var index = call.RepeatCheckpoint; index < repeatMutationCount; index++)
        {
            if (((index - call.RepeatCheckpoint) & 63) == 63) budget.ChargeWorkspaceStep();
            var mutation = repeatMutations[index];
            if (repeatRestoreGenerations[mutation.RepeatId] == generation)
            {
                continue;
            }

            repeatRestoreGenerations[mutation.RepeatId] = generation;
            SetRepeat(
                mutation.RepeatId,
                mutation.PreviousCount,
                mutation.PreviousPosition,
                counts,
                positions,
                ref repeatMutations,
                ref budget);
        }

        var captureMutationCount = captureMutations.Count;
        // With at most one writable slot, the first entry is its call-entry
        // value. The inverse write still belongs to the retry journal.
        var tracksMultipleCaptures = !captureRestoreGenerations.IsEmpty;
        if (!tracksMultipleCaptures && call.CaptureCheckpoint < captureMutationCount)
            captureMutationCount = call.CaptureCheckpoint + 1;

        for (var index = call.CaptureCheckpoint; index < captureMutationCount; index++)
        {
            if (((index - call.CaptureCheckpoint) & 63) == 63) budget.ChargeWorkspaceStep();
            var mutation = captureMutations[index];
            if (tracksMultipleCaptures)
            {
                if (captureRestoreGenerations[mutation.Slot] == generation) continue;
                captureRestoreGenerations[mutation.Slot] = generation;
            }

            SetCapture(
                mutation.Slot,
                mutation.PreviousStart,
                mutation.PreviousEnd,
                mutation.PreviousOpenStart,
                mutation.PreviousOwner,
                mutation.PreviousOpenOwner,
                captureStarts,
                captureEnds,
                captureOpenStarts,
                captureOwners,
                captureOpenOwners,
                ref captureMutations,
                ref budget, recordRollback: true);
        }
    }

    private static void RecordSubroutineUndo(
        Pcre2SubroutineCallFrame frame,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> mutations,
        ref Pcre2ResourceBudget budget)
    {
        if (!mutations.TryPush(frame, ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }
    }

    private static void RollbackSubroutineCalls(
        int checkpoint,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> calls,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> mutations,
        ref Pcre2ResourceBudget budget)
    {
        for (var remaining = mutations.Count - checkpoint; remaining > 0; remaining--)
        {
            if ((remaining & 63) == 0) budget.ChargeWorkspaceStep();
            var mutation = mutations.Pop();
            if (mutation.ReturnInstruction < 0)
            {
                _ = calls.Pop();
            }
            else if (!calls.TryPush(mutation, ref budget))
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
        ref int currentMarkPosition,
        ref Pcre2ResourceBudget budget)
    {
        for (var remaining = mutations.Count - mutationCheckpoint; remaining > 0; remaining--)
        {
            if ((remaining & 63) == 0) budget.ChargeWorkspaceStep();
            var mutation = mutations.Pop();
            currentMarkId = mutation.PreviousMarkId;
            currentMarkPosition = mutation.PreviousPosition;
        }

        trail.Truncate(trailCheckpoint);
    }

    private static int FindMarkPosition(
        int markId,
        Pcre2InvocationStateStack<Pcre2MarkTrailEntry> trail,
        ref Pcre2ResourceBudget budget)
    {
        var last = trail.Count - 1;
        for (var index = last; index >= 0; index--)
        {
            if (((last - index) & 63) == 63) budget.ChargeWorkspaceStep();
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
        Span<int> positions,
        ref Pcre2ResourceBudget budget)
    {
        for (var remaining = mutations.Count - checkpoint; remaining > 0; remaining--)
        {
            if ((remaining & 63) == 0) budget.ChargeWorkspaceStep();
            var mutation = mutations.Pop();
            counts[mutation.RepeatId] = mutation.PreviousCount;
            positions[mutation.RepeatId] = mutation.PreviousPosition;
        }
    }

    private static bool NeedsCaptureCloseRollback(int slot, int depth,
        ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        ref Pcre2InvocationStateStack<Pcre2SubroutineCallFrame> calls,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> mutations)
    {
        // An existing mutation covers this close when no live retry or
        // return needs the value between that write and this one.
        var count = mutations.Count;
        return (frames.Count != 0 || depth != 0) && (count == 0 ||
            (frames.Count != 0 && frames[frames.Count - 1].CaptureCheckpoint >= count) ||
            (depth != 0 && calls[depth - 1].CaptureCheckpoint >= count) ||
            mutations[count - 1].Slot != slot);
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
        Span<int> openOwners,
        ref Pcre2ResourceBudget budget)
    {
        for (var remaining = mutations.Count - checkpoint; remaining > 0; remaining--)
        {
            if ((remaining & 63) == 0) budget.ChargeWorkspaceStep();
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
        ref Utf8ValidatedInput input, ref Pcre2ResourceBudget budget,
        scoped Span<Utf8Pcre2CaptureRange> destination = default)
    {
        if (materialization == Pcre2CaptureMaterialization.ByteFinalSlots && !destination.IsEmpty)
        {
            WriteByteGroups(destination, matchStart, matchEnd, captureStarts, captureEnds, input.Bytes, ref budget);
            return default;
        }
        return materialization switch
        {
            Pcre2CaptureMaterialization.AssertionRanges =>
                Pcre2BacktrackingCaptureResult.FromAssertionRanges(
                    MaterializeCaptures(captureSlotCount, matchStart, matchEnd, captureStarts, captureEnds, ref budget)),
            Pcre2CaptureMaterialization.ProjectedFinalSlots =>
                Pcre2BacktrackingCaptureResult.FromProjectedGroups(
                    Pcre2Runner.PublishCaptures(
                        captureSlotCount,
                        matchStart,
                        matchEnd,
                        captureStarts,
                        captureEnds,
                        ref input, ref budget)),
            Pcre2CaptureMaterialization.ByteFinalSlots =>
                Pcre2BacktrackingCaptureResult.FromByteGroups(MaterializeByteGroups(captureSlotCount,
                    matchStart, matchEnd, captureStarts, captureEnds, input.Bytes, ref budget)),
            _ => default,
        };
    }

    internal static Utf8Pcre2CaptureRange[] MaterializeByteGroups(int count, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds, ReadOnlySpan<byte> input,
        ref Pcre2ResourceBudget budget, int slotOffset = 0, bool normalizeDirection = false)
    {
        var bytes = Utf8ConstructionBudget.ArrayBytes<Utf8Pcre2CaptureRange>(count);
        var control = new Pcre2ResultControl(ref budget);
        control.TryChargeResultBytes(bytes);
        control.ReserveWorking(bytes);
        try
        {
            control.ChargeWorkspaceStep();
            var groups = new Utf8Pcre2CaptureRange[count];
            WriteByteGroups(groups, matchStart, matchEnd, captureStarts, captureEnds, input, ref budget, slotOffset, normalizeDirection);
            return groups;
        }
        catch { control.ReleaseWorking(bytes); throw; }
    }

    // Caller-owned ranges and immutable arrays share the same publication work.
    // A caller destination is not producer-owned result storage.
    internal static void WriteByteGroups(scoped Span<Utf8Pcre2CaptureRange> destination, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds, ReadOnlySpan<byte> input,
        ref Pcre2ResourceBudget budget, int slotOffset = 0, bool normalizeDirection = false)
    {
        var control = new Pcre2ResultControl(ref budget);
        for (var slot = 0; slot < destination.Length; slot++)
        {
            if ((slot & 63) == 0) control.ChargeWorkspaceStep();
            var start = slot == 0 ? matchStart : captureStarts[slot + slotOffset];
            var end = slot == 0 ? matchEnd : captureEnds[slot + slotOffset];
            if (normalizeDirection && start >= 0 && end >= 0 && start > end) (start, end) = (end, start);
            var success = slot == 0 || start >= 0 && end >= start;
            var wellFormed = success && start <= end &&
                Pcre2SliceDriver.IsScalarAligned(input, start) && Pcre2SliceDriver.IsScalarAligned(input, end);
            destination[slot] = new(slot, success, success ? start : 0, success ? end : 0, wellFormed);
        }
    }
}
