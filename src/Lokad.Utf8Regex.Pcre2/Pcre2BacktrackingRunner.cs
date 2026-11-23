using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
// A lazy-repeat retry starts its next iteration at the saved input offset.
// A class-repeat frame stores the last tried endpoint in InputOffsetInBytes
// and the number of shorter choices in RepeatCount.
internal readonly record struct Pcre2BacktrackingFrame(
    int Instruction,
    int InputOffsetInBytes,
    int ReportedStartOffsetInBytes,
    int RepeatCheckpoint,
    int CaptureCheckpoint,
    (int Head, int Extent) CallCheckpoint,
    int AtomicCheckpoint,
    int MarkMutationCheckpoint,
    int MarkTrailCheckpoint,
    bool MatchBoundaryWasReset,
    Pcre2BacktrackingResumeAction ResumeAction = Pcre2BacktrackingResumeAction.None,
    Pcre2ControlVerbKind ControlVerb = default,
    int RepeatIdOrNameId = 0,
    int RepeatCount = 0);

internal readonly record struct Pcre2RepeatMutation(
    int RepeatId,
    int PreviousCount,
    int PreviousPosition);

// An unset start has owner -1. An open capture and its owner are set and
// cleared together, including when the journal restores an earlier state.
internal readonly record struct Pcre2CaptureState(
    int Start, int End, int OpenStart, int Owner, int OpenOwner)
{
    internal static int WordCount => Unsafe.SizeOf<Pcre2CaptureState>() / sizeof(int);
}

internal readonly record struct Pcre2CaptureMutation(int Slot, Pcre2CaptureState Previous);

internal readonly record struct Pcre2SubroutineCallFrame(
    int ReturnInstruction,
    int SubroutineSlot,
    int RepeatCheckpoint,
    int CaptureCheckpoint,
    int BacktrackingCheckpoint,
    int AtomicCheckpoint,
    int MarkTrailCheckpoint);

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
        var stateless = captureMaterialization == Pcre2CaptureMaterialization.None && program.IsShortStateless;
        while (candidate <= maximumCandidate)
        {
            if (useCandidateSearch)
            {
                var found = meterLiteralPrefix
                    ? TryFindMeteredLiteral(candidateSearch, bytes,
                        maximumCandidate, ref candidateState, ref budget, out candidate)
                    : TryFindNextCandidate(
                        candidateSearch,
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
            if (stateless)
            {
                if (MatchShortStateless(program, bytes, candidate, firstMatchingPosition.Value,
                        matchOptions, false, ref budget, out var end))
                    return new(true, candidate, end, candidate, end, false, default, null);
            }
            else
            {
                if (TryMatchAt(
                        program,
                        bytes,
                        ref input,
                        candidate,
                        firstMatchingPosition.Value,
                        matchOptions,
                        captureMaterialization,
                        default,
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

            var rewind = input[lowerBound..literalStart];
            var valid = true;
            var rewindSteps = 0;
            // The compiler admits exact literals and character classes with default options.
            for (var i = candidateSearch.LeadingRunTokens.Length - 1; i >= 0; i--)
            {
                ref readonly var token = ref candidateSearch.LeadingRunTokens[i];
                var consumed = false;
                while (!rewind.IsEmpty)
                {
                    if (meterDelimiter && (rewindSteps++ & 63) == 0) budget.ChargeWorkspaceStep();
                    if (Rune.DecodeLastFromUtf8(rewind, out var scalar, out var width) != OperationStatus.Done ||
                        !(token.Kind == Pcre2CharacterTokenKind.Literal ? scalar == token.Literal :
                            token.CharacterClass.Matches(scalar, ucp: false, Pcre2CharacterOptions.None))) break;
                    rewind = rewind[..^width];
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
                candidate = lowerBound + rewind.Length;
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

    private static bool TryMatchAt(
        Pcre2BacktrackingProgram program,
        ReadOnlySpan<byte> input,
        ref Utf8ValidatedInput projectionInput,
        int candidate,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        Pcre2CaptureMaterialization captureMaterialization,
        ReadOnlySpan<Pcre2CaptureState> initialCaptures,
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
                matchOptions, captureMaterialization, initialCaptures,
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
        ReadOnlySpan<Pcre2CaptureState> initialCaptures,
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
        var collectsDiagnostics = budget.CollectsDiagnostics;
        var captureMutations = new Pcre2InvocationStateStack<Pcre2CaptureMutation>(int.MaxValue, collectsDiagnostics);
        var subroutineCalls = new Pcre2SubroutineCalls(frameLimit, collectsDiagnostics);
        var atomicCheckpoints = new Pcre2InvocationStateStack<Pcre2AtomicCheckpoint>(frameLimit, collectsDiagnostics);
        var markMutations = new Pcre2InvocationStateStack<Pcre2MarkMutation>(int.MaxValue, collectsDiagnostics);
        var markTrail = new Pcre2InvocationStateStack<Pcre2MarkTrailEntry>(int.MaxValue, collectsDiagnostics);
        var retainCaptures = captureMaterialization != Pcre2CaptureMaterialization.None || !initialCaptures.IsEmpty;
        var greedyOptionalTail = !isAssertion && matchOptions == Pcre2MatchOptions.None
            ? program.GreedyOptionalTail : int.MaxValue;
        var needsCaptureState = program.CaptureReadSlot != 0 || retainCaptures;
        // Without output or an assertion boundary, a write to another slot
        // cannot be seen by the sole reader, even across calls and retries.
        var onlyCaptureSlot = !retainCaptures && !isAssertion && program.AssertionPrograms.Length == 0 &&
            program.CaptureReadSlot > 0 ? program.CaptureReadSlot : 0;
        var repeatSlots = program.RepeatCount;
        var captureSlots = needsCaptureState && program.CaptureSlotCount > 1 ? program.CaptureSlotCount : 0;
        // Retain the original slot index. Later slots are unobservable in
        // the same single-reader case that skips their capture writes.
        if (onlyCaptureSlot != 0) captureSlots = onlyCaptureSlot + 1;
        var restoresSubroutines = program.SubroutineTargets.Length != 0;
        // Slot zero belongs to the match result, outside the capture journal.
        var restoresMultipleCaptures = restoresSubroutines && captureSlots > 2 && onlyCaptureSlot == 0;
        var acquiredWorkspace = workspace.EnsureInitialized(repeatSlots, captureSlots,
            restoresSubroutines, restoresMultipleCaptures, ref budget);
        var rentedRepeatState = workspace.RepeatState;
        var rentedCaptureState = workspace.CaptureState;
        var rentedCaptureRestoreGenerations = workspace.CaptureRestoreGenerations;
        var rentedRepeatRestoreGenerations = workspace.RepeatRestoreGenerations;
        Span<int> counts = default, positions = default, repeatRestoreGenerations = default;
        if (repeatSlots != 0)
        {
            var repeatState = rentedRepeatState.AsSpan(0, repeatSlots * 2);
            repeatState.Fill(-1);
            counts = repeatState[..repeatSlots];
            positions = repeatState[repeatSlots..];
            if (rentedRepeatRestoreGenerations is not null)
            {
                repeatRestoreGenerations = rentedRepeatRestoreGenerations.AsSpan(0, repeatSlots);
                repeatRestoreGenerations.Clear();
            }
        }

        Span<Pcre2CaptureState> captureState = default;
        Span<int> captureRestoreGenerations = default;
        if (captureSlots != 0)
        {
            var words = rentedCaptureState.AsSpan(0, captureSlots * Pcre2CaptureState.WordCount);
            words.Fill(-1);
            captureState = MemoryMarshal.Cast<int, Pcre2CaptureState>(words);
            if (rentedCaptureRestoreGenerations is not null)
            {
                captureRestoreGenerations = rentedCaptureRestoreGenerations.AsSpan(0, captureSlots);
                captureRestoreGenerations.Clear();
            }
        }
        // Assertions inherit closed ranges. Open captures and ownership stay local.
        for (var slot = 0; slot < initialCaptures.Length; slot++)
        {
            var previous = initialCaptures[slot];
            captureState[slot] = new(previous.Start, previous.End, -1, slot != 0 && previous.Start >= 0 ? 0 : -1, -1);
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
                budget.ChargeBacktracking(in instruction);
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
                        if (inputIndex == input.Length)
                        {
                            ref readonly var primary = ref program.Instructions[instruction.PrimaryTarget];
                            if (primary.Kind is Pcre2BacktrackingInstructionKind.Token or Pcre2BacktrackingInstructionKind.CaptureToken &&
                                Pcre2BacktrackingAnalysis.ConsumesInput(primary.Token.Kind))
                            {
                                // A consuming token fails at EOF. A captured token
                                // writes only after its predicate succeeds.
                                instructionIndex = instruction.SecondaryTarget;
                                continue;
                            }
                        }
                        PushFrame(
                            ref frames,
                            new Pcre2BacktrackingFrame(
                                instruction.SecondaryTarget,
                                inputIndex,
                                reportedStartOffsetInBytes,
                                repeatMutations.Count,
                                captureMutations.Count,
                                subroutineCalls.Checkpoint,
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
                                            subroutineCalls.Checkpoint,
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
                                            subroutineCalls.Checkpoint,
                                            atomicCheckpoints.Count,
                                            markMutations.Count,
                                            markTrail.Count,
                                            matchBoundaryWasReset,
                                            Pcre2BacktrackingResumeAction.EnterRepeat,
                                            RepeatIdOrNameId: repeatId,
                                            RepeatCount: count + 1),
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
                                    subroutineCalls.Checkpoint,
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
                        if (!captureState.IsEmpty && (onlyCaptureSlot == 0 || tokenCaptureSlot == onlyCaptureSlot))
                        {
                            var slot = tokenCaptureSlot;
                            var depth = subroutineCalls.Count;
                            var recordRollback = frames.Count != 0 || depth != 0;
                            ref var current = ref captureState[slot];
                            if (current.Owner > depth || current.OpenOwner > depth)
                            {
                                // The normal open would be blocked by a deeper
                                // owner. Only an opening at this depth can close.
                                if (current.OpenOwner != depth)
                                {
                                    instructionIndex++;
                                    continue;
                                }
                                tokenStart = current.OpenStart;
                                recordRollback = NeedsCaptureCloseRollback(slot, depth, ref frames,
                                    ref subroutineCalls, ref captureMutations);
                            }

                            SetCapture(slot, ref current, new(tokenStart, inputIndex, -1, depth, -1),
                                ref captureMutations, ref budget, recordRollback);
                        }
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.CaptureStart:
                        var startCaptureSlot = instruction.CaptureSlot;
                        if (captureState.IsEmpty || onlyCaptureSlot != 0 && startCaptureSlot != onlyCaptureSlot)
                        {
                            instructionIndex++;
                            continue;
                        }

                        var currentCallDepth = subroutineCalls.Count;
                        ref var opening = ref captureState[startCaptureSlot];
                        if (opening.Owner > currentCallDepth || opening.OpenOwner > currentCallDepth)
                        {
                            instructionIndex++;
                            continue;
                        }

                        SetCapture(
                            startCaptureSlot,
                            ref opening,
                            new(opening.Start, opening.End, inputIndex, opening.Owner, currentCallDepth),
                            ref captureMutations,
                            ref budget,
                            recordRollback: frames.Count != 0 || currentCallDepth != 0);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.CaptureEnd:
                        var endCaptureSlot = instruction.CaptureSlot;
                        if (captureState.IsEmpty || onlyCaptureSlot != 0 && endCaptureSlot != onlyCaptureSlot)
                        {
                            instructionIndex++;
                            continue;
                        }

                        var endCallDepth = subroutineCalls.Count;
                        ref var closing = ref captureState[endCaptureSlot];
                        if (closing.OpenOwner != endCallDepth)
                        {
                            instructionIndex++;
                            continue;
                        }

                        var recordClose = NeedsCaptureCloseRollback(endCaptureSlot, endCallDepth, ref frames,
                            ref subroutineCalls, ref captureMutations);
                        SetCapture(
                            endCaptureSlot,
                            ref closing,
                            new(closing.OpenStart, inputIndex, -1, endCallDepth, -1),
                            ref captureMutations,
                            ref budget,
                            recordRollback: recordClose);
                        instructionIndex++;
                        continue;

                    case Pcre2BacktrackingInstructionKind.Backreference:
                        ref readonly var referencedCapture = ref captureState[instruction.CaptureSlot];
                        if (referencedCapture.Start >= 0 && referencedCapture.End >= referencedCapture.Start &&
                            TryMatchBackreference(
                                input,
                                referencedCapture.Start,
                                referencedCapture.End,
                                inputIndex,
                                instruction.BackreferenceOptions,
                                ref budget,
                                out inputIndex))
                        {
                            instructionIndex++;
                            continue;
                        }
                        break;

                    case Pcre2BacktrackingInstructionKind.BackreferenceSlotSet:
                        var duplicateSlots = program.BackreferenceSlotSets[instruction.BackreferenceSlotSetId].Slots;
                        var selectedSlot = FindFirstParticipatingCapture(duplicateSlots, captureState, ref budget);

                        if (selectedSlot != 0 && TryMatchBackreference(
                                input,
                                captureState[selectedSlot].Start,
                                captureState[selectedSlot].End,
                                inputIndex,
                                instruction.BackreferenceOptions,
                                ref budget,
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
                                    captureState,
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
                                    captureState,
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

                        subroutineCalls.Push(
                            new Pcre2SubroutineCallFrame(
                                instructionIndex + 1,
                                subroutineSlot,
                                repeatMutations.Count,
                                captureMutations.Count,
                                frames.Count,
                                atomicCheckpoints.Count,
                                markTrail.Count),
                            checked(depthBase + frames.Count),
                            ref budget);
                        for (var resetStart = 0; resetStart < counts.Length;)
                        {
                            // The call's instruction step admits the first
                            // block. Even unused repeat slots cost scan work.
                            if (resetStart != 0) budget.ChargeWorkspaceStep();
                            var block = counts.Slice(resetStart, Math.Min(64, counts.Length - resetStart));
                            var firstUsed = block.IndexOfAnyExcept(-1);
                            for (var offset = firstUsed; offset >= 0 && offset < block.Length; offset++)
                            {
                                if (block[offset] < 0) continue;
                                var activeRepeatId = resetStart + offset;
                                SetRepeat(
                                    activeRepeatId,
                                    -1,
                                    -1,
                                    counts,
                                    positions,
                                    ref repeatMutations,
                                    ref budget);
                            }
                            resetStart += block.Length;
                        }
                        instructionIndex = subroutineTarget;
                        continue;

                    case Pcre2BacktrackingInstructionKind.SubroutineReturn:
                        if (subroutineCalls.Count == 0)
                        {
                            break;
                        }

                        instructionIndex = ReturnFromSubroutine(
                            subroutineCalls.Current,
                            in frames,
                            ref subroutineCalls,
                            counts,
                            positions,
                            captureState,
                            repeatRestoreGenerations,
                            captureRestoreGenerations,
                            ref subroutineRestoreGeneration,
                            ref repeatMutations,
                            ref captureMutations,
                            ref budget);
                        continue;

                    case Pcre2BacktrackingInstructionKind.Conditional:
                        var condition = program.Conditions[instruction.ConditionId];
                        var conditionMet = condition.Kind switch
                        {
                            Pcre2BacktrackingConditionKind.CaptureSet =>
                                FindFirstParticipatingCapture(condition.CaptureSlots, captureState, ref budget) != 0,
                            Pcre2BacktrackingConditionKind.RecursionAny => subroutineCalls.Count != 0,
                            Pcre2BacktrackingConditionKind.RecursionSlot =>
                                subroutineCalls.Contains(condition.SubroutineSlot, ref budget),
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
                                    captureState,
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
                                captureState,
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
                        subroutineCalls.Trim(RetainedCallExtent(in frames));
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
                            onlyCaptureSlot,
                            captureState,
                            ref captureMutations,
                            ref budget);
                        if (subroutineCalls.Count != 0)
                        {
                            var acceptedCall = subroutineCalls.Current;
                            frames.Truncate(acceptedCall.BacktrackingCheckpoint);
                            atomicCheckpoints.Truncate(acceptedCall.AtomicCheckpoint);
                            markTrail.Truncate(acceptedCall.MarkTrailCheckpoint);
                            instructionIndex = ReturnFromSubroutine(
                                acceptedCall,
                                in frames,
                                ref subroutineCalls,
                                counts,
                                positions,
                                captureState,
                                repeatRestoreGenerations,
                                captureRestoreGenerations,
                                ref subroutineRestoreGeneration,
                                ref repeatMutations,
                                ref captureMutations,
                                ref budget);
                            continue;
                        }

                        var acceptEmptyDisallowed =
                            (matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && inputIndex == reportedStartOffsetInBytes ||
                            (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0 &&
                            candidate == firstMatchingPosition && inputIndex == reportedStartOffsetInBytes;
                        if (!acceptEmptyDisallowed) goto PublishMatch;
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
                                subroutineCalls.Checkpoint,
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
                        if ((!endAnchored || inputIndex == input.Length) && !emptyDisallowed) goto PublishMatch;
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
                        ref atomicCheckpoints,
                        ref markMutations,
                        ref markTrail,
                        counts,
                        positions,
                        captureState,
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

        PublishMatch:
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
                captureState,
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
        finally
        {
            if (collectsDiagnostics)
            {
                var frameRents = frames.RentCount - initialFrameRents;
                var frameGrowths = initialFrameRents == 0 ? Math.Max(0, frameRents - 1) : frameRents;
                var repeatRents = repeatMutations.RentCount - initialRepeatRents;
                var repeatGrowths = initialRepeatRents == 0 ? Math.Max(0, repeatRents - 1) : repeatRents;
                var controlRents = subroutineCalls.RentCount + atomicCheckpoints.RentCount +
                    markMutations.RentCount + markTrail.RentCount;
                var stackGrowths = frameGrowths +
                    repeatGrowths +
                    Math.Max(0, captureMutations.RentCount - 1) +
                    Math.Max(0, subroutineCalls.RentCount - 1) +
                    Math.Max(0, atomicCheckpoints.RentCount - 1) +
                    Math.Max(0, markMutations.RentCount - 1) +
                    Math.Max(0, markTrail.RentCount - 1);
                var fixedRents = !acquiredWorkspace ? 0 : (rentedRepeatState is null ? 0 : 1) +
                    (rentedCaptureState is null ? 0 : 1) +
                    (rentedCaptureRestoreGenerations is null ? 0 : 1) +
                    (rentedRepeatRestoreGenerations is null ? 0 : 1);
                budget.RecordWorkspacePoolTraffic((ulong)fixedRents, (ulong)frameRents, (ulong)repeatRents,
                    (ulong)captureMutations.RentCount, (ulong)controlRents, (ulong)stackGrowths);
            }
            markTrail.Dispose(ref budget);
            markMutations.Dispose(ref budget);
            atomicCheckpoints.Dispose(ref budget);
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
        ReadOnlySpan<Pcre2CaptureState> captureState,
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
        var captureMaterialization = !captureState.IsEmpty && assertionProgram.HasCaptureWrites
            ? Pcre2CaptureMaterialization.AssertionRanges
            : Pcre2CaptureMaterialization.None;

        if (!isLookbehind)
        {
            budget.ChargeCandidate();
            if (captureState.IsEmpty && assertionProgram.IsShortStateless)
            {
                captures = [];
                mark = null;
                reportedStartOffsetInBytes = -1;
                return isNegative != MatchShortStateless(assertionProgram, input, inputIndex,
                    outerFirstMatchingPosition, nestedOptions, true, ref budget, out _);
            }

            var matched = TryMatchAt(
                assertionProgram,
                input,
                ref projectionInput,
                inputIndex,
                outerFirstMatchingPosition,
                nestedOptions,
                captureMaterialization,
                captureState,
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
                    captureState,
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

    private static bool MatchShortStateless(
        Pcre2BacktrackingProgram program,
        ReadOnlySpan<byte> input,
        int position,
        int firstMatchingPosition,
        Pcre2MatchOptions options,
        bool isAssertion,
        ref Pcre2ResourceBudget budget,
        out int end)
    {
        // Keep the zero-byte admission checkpoint and charge the same reached
        // instructions as the general VM. A stateless attempt has no open
        // captures to close on ACCEPT and cannot publish a mark or reset.
        budget.ReserveWorking(0);
        end = position;
        foreach (ref readonly var instruction in program.Instructions.AsSpan())
        {
            budget.ChargeBacktracking(in instruction);
            switch (instruction.Kind)
            {
                case Pcre2BacktrackingInstructionKind.Token:
                case Pcre2BacktrackingInstructionKind.CaptureToken:
                    if (!Pcre2CharacterRunner.TryMatchToken(instruction.Token, program.Request, input,
                            end, firstMatchingPosition, options, ref budget, out end))
                        return false;
                    break;
                case Pcre2BacktrackingInstructionKind.Accept:
                    var endAnchored = (options & Pcre2MatchOptions.EndAnchored) != 0 ||
                        !isAssertion && (program.Request.Options & Pcre2CompileOptions.EndAnchored) != 0;
                    if (endAnchored && end != input.Length) return false;
                    goto case Pcre2BacktrackingInstructionKind.ControlAccept;
                case Pcre2BacktrackingInstructionKind.ControlAccept:
                    return end != position || (options & Pcre2MatchOptions.NotEmpty) == 0 &&
                        ((options & Pcre2MatchOptions.NotEmptyAtStart) == 0 || position != firstMatchingPosition);
            }
        }
        return false;
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

    private static int FindFirstParticipatingCapture(
        ReadOnlySpan<int> slots,
        ReadOnlySpan<Pcre2CaptureState> captureState,
        ref Pcre2ResourceBudget budget)
    {
        // The instruction charge admits the first block. Backreferences
        // compare only the first participating capture in declaration order.
        for (var offset = 0; offset < slots.Length;)
        {
            if (offset != 0) budget.ChargeWorkspaceStep();
            var block = slots.Slice(offset, Math.Min(64, slots.Length - offset));
            foreach (var slot in block)
            {
                if ((uint)slot >= (uint)captureState.Length) continue;
                ref readonly var capture = ref captureState[slot];
                if (capture.Start >= 0 && capture.End >= capture.Start) return slot;
            }
            offset += block.Length;
        }

        return 0;
    }

    private static int RetainedCallExtent(scoped in Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames)
        => frames.Count == 0 ? 0 : frames[frames.Count - 1].CallCheckpoint.Extent;

    private static void MergeAssertionCaptures(
        ReadOnlySpan<Pcre2CaptureByteRange> assertionCaptures,
        int callDepth,
        Span<Pcre2CaptureState> captureState,
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
                ref captureState[slot],
                new(capture.StartOffsetInBytes, capture.EndOffsetInBytes, -1, callDepth, -1),
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
        int onlyCaptureSlot,
        Span<Pcre2CaptureState> captureState,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2ResourceBudget budget)
    {
        // The same proof that skips other capture writes also excludes
        // their open states. The ACCEPT instruction admits the first block.
        var first = onlyCaptureSlot == 0 ? 1 : onlyCaptureSlot;
        var end = onlyCaptureSlot == 0 ? captureState.Length : onlyCaptureSlot + 1;
        for (var firstInBlock = first; firstInBlock < end;)
        {
            if (firstInBlock != first) budget.ChargeWorkspaceStep();
            var blockEnd = firstInBlock + Math.Min(64, end - firstInBlock);
            for (var slot = firstInBlock; slot < blockEnd; slot++)
            {
                ref var current = ref captureState[slot];
                if (current.OpenOwner != callDepth) continue;

                SetCapture(
                    slot,
                    ref current,
                    new(current.OpenStart, inputIndex, -1, callDepth, -1),
                    ref captureMutations,
                    ref budget, recordRollback: true);
            }
            firstInBlock = blockEnd;
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
        ref Pcre2SubroutineCalls subroutineCalls,
        ref Pcre2InvocationStateStack<Pcre2AtomicCheckpoint> atomicCheckpoints,
        ref Pcre2InvocationStateStack<Pcre2MarkMutation> markMutations,
        ref Pcre2InvocationStateStack<Pcre2MarkTrailEntry> markTrail,
        Span<int> counts,
        Span<int> positions,
        Span<Pcre2CaptureState> captureState,
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
                captureState,
                ref budget);
            subroutineCalls.Restore(frame.CallCheckpoint);
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
                    resumeOffset,
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
                subroutineCalls.Trim(RetainedCallExtent(in frames));
                instructionIndex = frame.Instruction;
                inputIndex = resumeOffset;
                failure = default;
                return true;
            }

            if (subroutineCalls.Count != 0)
            {
                var call = subroutineCalls.Current;
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

    private static int ReturnFromSubroutine(
        Pcre2SubroutineCallFrame call,
        scoped in Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2SubroutineCalls calls,
        Span<int> counts,
        Span<int> positions,
        Span<Pcre2CaptureState> captureState,
        Span<int> repeatRestoreGenerations,
        Span<int> captureRestoreGenerations,
        ref int restoreGeneration,
        ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> captureMutations,
        ref Pcre2ResourceBudget budget)
    {
        var retainedExtent = RetainedCallExtent(in frames);
        // Checkpoints below this call's record precede its journal entries.
        // Only a retry saved during the call can need their inverse writes.
        var retainMutations = retainedExtent >= calls.Checkpoint.Head;

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
            var repeatId = mutation.RepeatId;
            if (repeatRestoreGenerations[repeatId] == generation)
            {
                continue;
            }

            repeatRestoreGenerations[repeatId] = generation;
            if (retainMutations)
                SetRepeat(repeatId, mutation.PreviousCount, mutation.PreviousPosition,
                    counts, positions, ref repeatMutations, ref budget);
            else
            {
                counts[repeatId] = mutation.PreviousCount;
                positions[repeatId] = mutation.PreviousPosition;
            }
        }

        var captureMutationCount = captureMutations.Count;
        // With at most one writable slot, the first entry is its call-entry
        // value.
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
                ref captureState[mutation.Slot],
                mutation.Previous,
                ref captureMutations,
                ref budget, recordRollback: retainMutations);
        }

        if (!retainMutations)
        {
            if (repeatMutationCount != call.RepeatCheckpoint)
                repeatMutations.Truncate(call.RepeatCheckpoint);
            captureMutations.Truncate(call.CaptureCheckpoint);
        }
        calls.Return(retainedExtent);
        return call.ReturnInstruction;
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
        scoped ref Pcre2SubroutineCalls calls,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> mutations)
    {
        // An existing mutation covers this close when no live retry or
        // return needs the value between that write and this one.
        var count = mutations.Count;
        return (frames.Count != 0 || depth != 0) && (count == 0 ||
            (frames.Count != 0 && frames[frames.Count - 1].CaptureCheckpoint >= count) ||
            (depth != 0 && calls.Current.CaptureCheckpoint >= count) ||
            mutations[count - 1].Slot != slot);
    }

    private static void SetCapture(
        int slot,
        scoped ref Pcre2CaptureState current,
        Pcre2CaptureState value,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> mutations,
        ref Pcre2ResourceBudget budget,
        bool recordRollback)
    {
        // Future frames checkpoint the current capture values. Only existing
        // backtracking frames or subroutine returns need an earlier value.
        if (recordRollback && !mutations.TryPush(new(slot, current), ref budget))
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);
        }

        current = value;
    }

    internal static void RollbackCaptures(
        int checkpoint,
        ref Pcre2InvocationStateStack<Pcre2CaptureMutation> mutations,
        Span<Pcre2CaptureState> captureState,
        ref Pcre2ResourceBudget budget)
    {
        for (var remaining = mutations.Count - checkpoint; remaining > 0; remaining--)
        {
            if ((remaining & 63) == 0) budget.ChargeWorkspaceStep();
            var mutation = mutations.Pop();
            captureState[mutation.Slot] = mutation.Previous;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryMatchBackreference(
        ReadOnlySpan<byte> input,
        int captureStart,
        int captureEnd,
        int subjectStart,
        Pcre2CharacterOptions options,
        ref Pcre2ResourceBudget budget,
        out int subjectEnd)
    {
        if ((options & Pcre2CharacterOptions.Caseless) != 0 || captureEnd - captureStart != 1)
            return TryMatchBackreferenceSequence(input, captureStart, captureEnd, subjectStart, options, ref budget, out subjectEnd);

        subjectEnd = subjectStart;
        if ((uint)subjectStart >= (uint)input.Length || input[subjectStart] != input[captureStart]) return false;
        subjectEnd = subjectStart + 1;
        return true;
    }

    private static bool TryMatchBackreferenceSequence(
        ReadOnlySpan<byte> input,
        int captureStart,
        int captureEnd,
        int subjectStart,
        Pcre2CharacterOptions options,
        ref Pcre2ResourceBudget budget,
        out int subjectEnd)
    {
        subjectEnd = subjectStart;
        var capture = input[captureStart..captureEnd];
        if ((options & Pcre2CharacterOptions.Caseless) == 0)
        {
            const int blockBytes = 2048;
            if (subjectStart > input.Length - capture.Length) return false;
            var subject = input.Slice(subjectStart, capture.Length);
            if (capture.Length <= blockBytes || budget.IsUnmetered)
            {
                if (!subject.SequenceEqual(capture)) return false;
            }
            else
            {
                // The backreference instruction admits the first block.
                for (var offset = 0; offset < capture.Length;)
                {
                    if (offset != 0) budget.ChargeWorkspaceStep();
                    var length = Math.Min(blockBytes, capture.Length - offset);
                    if (!subject.Slice(offset, length).SequenceEqual(capture.Slice(offset, length))) return false;
                    offset += length;
                }
            }
            subjectEnd = subjectStart + capture.Length;
            return true;
        }

        const int blockScalars = 64;
        var captureIndex = captureStart;
        var subjectIndex = subjectStart;
        while (captureIndex < captureEnd)
        {
            if (captureIndex != captureStart) budget.ChargeWorkspaceStep();
            if (captureEnd - captureIndex >= blockScalars && input.Length - subjectIndex >= blockScalars &&
                Ascii.EqualsIgnoreCase(input.Slice(captureIndex, blockScalars), input.Slice(subjectIndex, blockScalars)))
            {
                captureIndex += blockScalars;
                subjectIndex += blockScalars;
                continue;
            }
            // An ASCII comparison can decline wider equivalent scalars.
            for (var compared = 0; compared < blockScalars && captureIndex < captureEnd; compared++)
            {
                if (subjectIndex >= input.Length ||
                    Rune.DecodeFromUtf8(input[captureIndex..captureEnd], out var capturedRune, out var capturedWidth) != OperationStatus.Done ||
                    Rune.DecodeFromUtf8(input[subjectIndex..], out var subjectRune, out var subjectWidth) != OperationStatus.Done ||
                    !Pcre2CharacterSemantics.Equals(capturedRune, subjectRune, options))
                    return false;
                captureIndex += capturedWidth;
                subjectIndex += subjectWidth;
            }
        }

        subjectEnd = subjectIndex;
        return true;
    }

    private static Pcre2CaptureByteRange[] MaterializeCaptures(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<Pcre2CaptureState> captureState,
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
                var start = captureState[slot].Start;
                var end = captureState[slot].End;
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
        ReadOnlySpan<Pcre2CaptureState> captureState,
        ref Utf8ValidatedInput input, ref Pcre2ResourceBudget budget,
        scoped Span<Utf8Pcre2CaptureRange> destination = default)
    {
        if (materialization == Pcre2CaptureMaterialization.ByteFinalSlots && !destination.IsEmpty)
        {
            WriteByteGroups(destination, matchStart, matchEnd, new(captureState), input.Bytes, ref budget);
            return default;
        }
        return materialization switch
        {
            Pcre2CaptureMaterialization.AssertionRanges =>
                Pcre2BacktrackingCaptureResult.FromAssertionRanges(
                    MaterializeCaptures(captureSlotCount, matchStart, matchEnd, captureState, ref budget)),
            Pcre2CaptureMaterialization.ProjectedFinalSlots =>
                Pcre2BacktrackingCaptureResult.FromProjectedGroups(
                    Pcre2Runner.PublishCaptures(
                        captureSlotCount,
                        matchStart,
                        matchEnd,
                        new(captureState),
                        ref input, ref budget)),
            Pcre2CaptureMaterialization.ByteFinalSlots =>
                Pcre2BacktrackingCaptureResult.FromByteGroups(MaterializeByteGroups(captureSlotCount,
                    matchStart, matchEnd, new(captureState), input.Bytes, ref budget)),
            _ => default,
        };
    }

    internal static Utf8Pcre2CaptureRange[] MaterializeByteGroups(int count, int matchStart, int matchEnd,
        scoped Pcre2CaptureRanges captureRanges, ReadOnlySpan<byte> input,
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
            WriteByteGroups(groups, matchStart, matchEnd, captureRanges, input, ref budget, slotOffset, normalizeDirection);
            return groups;
        }
        catch { control.ReleaseWorking(bytes); throw; }
    }

    // Caller-owned ranges and immutable arrays share the same publication work.
    // A caller destination is not producer-owned result storage.
    internal static void WriteByteGroups(scoped Span<Utf8Pcre2CaptureRange> destination, int matchStart, int matchEnd,
        scoped Pcre2CaptureRanges captureRanges, ReadOnlySpan<byte> input,
        ref Pcre2ResourceBudget budget, int slotOffset = 0, bool normalizeDirection = false)
    {
        var control = new Pcre2ResultControl(ref budget);
        for (var slot = 0; slot < destination.Length; slot++)
        {
            if ((slot & 63) == 0) control.ChargeWorkspaceStep();
            var (start, end) = slot == 0 ? (matchStart, matchEnd) : captureRanges[slot + slotOffset];
            if (normalizeDirection && start >= 0 && end >= 0 && start > end) (start, end) = (end, start);
            var success = slot == 0 || start >= 0 && end >= start;
            var wellFormed = success && start <= end &&
                Pcre2SliceDriver.IsScalarAligned(input, start) && Pcre2SliceDriver.IsScalarAligned(input, end);
            destination[slot] = new(slot, success, success ? start : 0, success ? end : 0, wellFormed);
        }
    }
}
