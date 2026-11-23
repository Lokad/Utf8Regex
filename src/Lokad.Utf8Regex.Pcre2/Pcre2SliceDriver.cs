using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

// R02a slice execution for the PCRE2 profile on the shared primary-library
// runner. Global progression (byte search order, same-position non-empty
// retry, scalar advance) stays PCRE2-owned; every attempt runs
// Utf8SliceRunner.TryMatchAt under one cumulative invocation built from the
// request limits, timeout, and live token. Limit breaches and deadline expiry
// surface with the established PCRE2 failure vocabulary. Non-None match
// options decline to the shell backtracking fallback at the dispatch site.
// R02a lazy slice enumeration for the PCRE2 profile. One cursor owns a
// cumulative invocation across MoveNext calls (budgets accumulate through
// lazy enumeration with no reset); every step rents and releases its slots
// in a finally block, so an abandoned cursor retains no ledger reservations.
// Global progression mirrors the census driver: PCRE2 same-position
// non-empty retry on empty matches, scalar advance otherwise, byte steps
// on misses. One-shot search borrows the same candidate scan.
internal ref struct Pcre2SliceGlobalMatchCursor
{
    private readonly Utf8SliceProgram _program;
    private Utf8ValidatedInput _input;
    private readonly Pcre2CompileRequest _request;
    private Utf8SliceInvocation _invocation;
    private int _position;
    private bool _retryPending;
    private readonly byte? _leadingAsciiByte;
    private int _retryPosition;

    internal Pcre2SliceGlobalMatchCursor(
        Utf8SliceProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        CancellationToken cancellationToken)
        : this(program, Pcre2SliceDriver.CreateInvocation(request, cancellationToken), input, start, request)
    {
    }

    // Callback output and discovery borrow one existing authority. The caller
    // promotes its ledger before copying it so even unlimited scope loans share.
    internal Pcre2SliceGlobalMatchCursor(
        Utf8SliceProgram program,
        Utf8SliceInvocation invocation,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        byte? leadingAsciiByte = null)
    {
        _program = program;
        _input = input;
        _request = request;
        _invocation = invocation;
        _position = start.Value;
        _retryPending = false;
        _leadingAsciiByte = leadingAsciiByte;
        _retryPosition = start.Value;
        Current = Pcre2CharacterMatch.NoMatch;
    }

    internal Pcre2CharacterMatch Current { get; private set; }

    [UnscopedRef] internal ref Utf8SliceInvocation WorkingInvocation => ref _invocation;

    // Single-attempt detailed capture for a yielded match start. The
    // caller shares this cursor invocation so replacement budgets stay
    // cumulative; slots are rented and released inside this call.
    // A yielded non-empty range re-queries with requireNonEmpty so a
    // retry-won match reproduces exactly; the pruned search provably
    // returns the same first hit (pruning only removes empty accepts
    // that precede nothing, since the unpruned search passed them by).
    // Empty yields re-query normally. Callers pass whether the yielded
    // range was non-empty.
    internal Pcre2GroupData[] CaptureGroupsAt(int startByte, bool requireNonEmpty)
    {
        var slots = Pcre2SliceDriver.RentProgramSlots(_program, ref _invocation);
        Pcre2GroupData[]? projected = null;
        try
        {
            Array.Fill(slots.Starts, -1);
            Array.Fill(slots.Ends, -1);
            var outcome = Utf8SliceRunner.TryMatchAt(
                _program,
                _input.Bytes,
                startByte,
                slots.Starts,
                slots.Ends,
                Pcre2SliceDriver.IsScalarAligned(_input.Bytes, startByte),
                ref _invocation,
                requireNonEmpty: requireNonEmpty);
            Pcre2SliceDriver.ThrowIfSliceBreached(outcome.Breach);
            if (!outcome.IsMatch)
            {
                throw new InvalidOperationException(
                    "Slice re-query missed a yielded match.");
            }

            return projected = Pcre2SliceDriver.ProjectSliceGroups(
                _program,
                ref _input,
                startByte,
                outcome.EndIndex,
                slots.Starts,
                slots.Ends, ref _invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException(
                "The PCRE2 match deadline expired.",
                Pcre2ErrorKind.Timeout);
        }
        finally
        {
            try { Utf8SliceRunner.ReleaseSlots(slots, ref _invocation); }
            finally { if (projected is not null) Pcre2SliceDriver.DetachProjectedGroups(projected, ref _invocation); }
        }
    }

    internal bool MoveNext()
    {
        Pcre2SliceDriver.CheckInvocation(ref _invocation);
        var moved = MoveNextCore();
        Pcre2SliceDriver.CheckInvocation(ref _invocation);
        return moved;
    }

    internal Pcre2GroupData ProjectCurrent()
    {
        // The public enumerator retains this byte/status snapshot for Current;
        // publication does not scan text or maintain coordinate state.
        return Pcre2MatchPublication.Publish(Current.StartOffsetInBytes,
            Current.EndOffsetInBytes, ref _input, new Pcre2ResultControl(ref _invocation));
    }

    private bool MoveNextCore()
    {
        // Each MoveNext call is one scan: pin \\G to the entry position.
        _invocation.AnchorOriginByte = _position;
        var slots = Pcre2SliceDriver.RentProgramSlots(_program, ref _invocation);
        // Reuse admitted scratch across candidates in this scan. It never
        // escapes the call, so copied or abandoned cursors retain no workspace.
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        try
        {
            var bytes = _input.Bytes;
            if (_retryPending)
            {
                _retryPending = false;
                var retry = Utf8SliceRunner.TryMatchAt(
                    _program, bytes, _retryPosition, slots.Starts, slots.Ends,
                    false, true, Pcre2SliceDriver.IsScalarAligned(bytes, _retryPosition),
                    ref workspace, ref _invocation);
                Pcre2SliceDriver.ThrowIfSliceBreached(retry.Breach);
                if (retry.IsMatch)
                {
                    Current = Pcre2CharacterMatch.Create(_retryPosition, retry.EndIndex);
                    _position = retry.EndIndex;
                    return true;
                }

                if (!Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(
                        ref _input, _request, Pcre2MatchOptions.None, new Utf8BytePosition(_retryPosition), out var advanced))
                    return false;
                _position = advanced.Value;
            }

            var lastStart = _leadingAsciiByte.HasValue ? bytes.Length - 1 : bytes.Length;
            var end = Pcre2SliceDriver.FindMatchingEnd(_program, bytes, ref _position, lastStart, _leadingAsciiByte, slots,
                ref workspace, ref _invocation);
            if (end < 0) return false;
            Current = Pcre2CharacterMatch.Create(_position, end);
            if (end == _position)
            {
                _retryPending = true;
                _retryPosition = _position;
            }
            else _position = end;
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException(
                "The PCRE2 match deadline expired.",
                Pcre2ErrorKind.Timeout);
        }
        finally
        {
            try { workspace.Release(ref _invocation); }
            finally { Utf8SliceRunner.ReleaseSlots(slots, ref _invocation); }
        }
    }

}

internal static class Pcre2SliceDriver
{
    // Count borrows immutable empty inputs when it has no prepared search.
    private static readonly Utf8SearchPlan s_emptyCountSearchPlan = default;
    private static readonly PreparedSearcher s_emptyCountBranchPrefixes = default;

    // Attempt-origin alignment for absolute-origin assertions (`^`): the end
    // of input is always aligned, and any byte below it is aligned unless it
    // opens a UTF-8 continuation byte.
    internal static bool IsScalarAligned(ReadOnlySpan<byte> bytes, int position)
    {
        return (uint)position >= (uint)bytes.Length || (bytes[position] & 0xC0) != 0x80;
    }

    // These admitted searches prove a necessary byte/literal absent, never a
    // match present. Share the proof and original authority across scalar public
    // operations before renting VM scratch; candidates still need verification.
    internal static bool RejectMissingCandidate(
        Utf8SliceProgram program,
        in Pcre2CandidateSearchProgram candidates,
        ReadOnlySpan<byte> input,
        int start,
        scoped ref Utf8SliceInvocation invocation)
    {
        if (program.RejectsByFinalAscii(input))
        {
            ThrowIfSliceBreached(invocation.TryChargeInputStep());
            return true;
        }
        if (program.CanMatchEmpty || program.RightToLeft ||
            candidates.Kind is not (Pcre2CandidateSearchKind.LeadingAsciiSet or
                Pcre2CandidateSearchKind.LeadingRunThenLiteral)) return false;

        var breach = candidates.Searcher.TryFindMatch(input, start, input.Length,
            false, ref invocation, out _, out var found);
        ThrowIfSliceBreached(breach);
        return !found;
    }

    internal static Pcre2CharacterMatch FindFirst(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int minimumByteLength,
        scoped ref Utf8SliceInvocation invocation,
        byte? leadingAsciiByte = null,
        bool seekDelimiterAfterMiss = false)
    {
        CheckInvocation(ref invocation);
        // Bound candidate starts; the matcher still sees the full subject.
        var lastStart = input.Length - minimumByteLength;
        if (start > lastStart) return Pcre2CharacterMatch.NoMatch;
        // Only matching dependencies can observe slots in a whole-value search.
        var slots = program.ReadsCaptures ? RentProgramSlots(program, ref invocation) : new Utf8SliceSlots([], []);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var position = start;
        invocation.AnchorOriginByte = start;
        if (program.CanMatchEmpty || program.HasContiguousAnchor) leadingAsciiByte = null;
        int end;
        try
        {
            end = FindMatchingEnd(program, input, ref position, lastStart, leadingAsciiByte, slots,
                ref workspace, ref invocation, seekDelimiterAfterMiss);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
        finally
        {
            try { workspace.Release(ref invocation); }
            finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
        }
        CheckInvocation(ref invocation);
        return end < 0 ? Pcre2CharacterMatch.NoMatch : Pcre2CharacterMatch.Create(position, end);
    }

    internal static int FindMatchingEnd(Utf8SliceProgram program, ReadOnlySpan<byte> input, ref int position,
        int lastStart, byte? leadingAsciiByte, Utf8SliceSlots slots, scoped ref Utf8SliceRunner.AttemptWorkspace workspace,
        scoped ref Utf8SliceInvocation invocation, bool seekDelimiterAfterMiss = false)
    {
        while (position <= lastStart)
        {
            if (leadingAsciiByte is { } leading && position < input.Length &&
                input[position] != leading && !SeekLeadingAsciiByte(
                    lastStart < input.Length ? input[..(lastStart + 1)] : input, leading, ref position, ref invocation))
                return -1;
            var outcome = Utf8SliceRunner.TryMatchAt(program, input, position, slots.Starts, slots.Ends,
                false, false, IsScalarAligned(input, position), ref workspace, ref invocation);
            ThrowIfSliceBreached(outcome.Breach);
            if (outcome.IsMatch) return outcome.EndIndex;

            if (seekDelimiterAfterMiss && lastStart - position >= 64)
            {
                seekDelimiterAfterMiss = false;
                if (GetDisjointClassDelimiter(program) is >= 0 and var delimiter)
                {
                    position = Utf8SliceRunner.AdvancePastScalar(input, position, reverse: false);
                    return FindInDelimiterWindows(program, input, (byte)delimiter, ref position,
                        lastStart, ref workspace, ref invocation);
                }
            }
            position++;
        }
        return -1;
    }

    private static int GetDisjointClassDelimiter(Utf8SliceProgram program)
    {
        // The delimiter follows the unbounded leading class directly.
        // A later start in the same run reaches the same suffix with fewer
        // scalars, so it cannot survive a failed verification of that run.
        if (program.ReadsCaptures || program.HasContiguousAnchor || program.Assertions.Length != 0 ||
            program.LeadingClassDelimiter is not { Direct: true, LeadingClass: 1, LiteralIndex: 4, Literal: >= 0 } window ||
            program.Instructions[0].RepeatMaximum != long.MaxValue ||
            !program.TryGetUnfoldedAsciiSet(program.Instructions[1], out var leading, out var asciiOnly) ||
            !asciiOnly || leading.Contains((byte)window.Literal))
            return -1;
        return window.Literal;
    }

    private static int FindInDelimiterWindows(Utf8SliceProgram program, ReadOnlySpan<byte> input, byte delimiter,
        ref int position, int lastStart, scoped ref Utf8SliceRunner.AttemptWorkspace workspace,
        scoped ref Utf8SliceInvocation invocation)
    {
        while (position >= 0 && position <= lastStart)
        {
            var breach = Utf8SliceRunner.TrySeekLeadingClassLiteralWindow(program, input, 1, delimiter,
                search: null, gapMaximum: 0, direct: true, ref position, ref invocation, out var windowEnd);
            ThrowIfSliceBreached(breach);
            if (position < 0 || position > lastStart) break;
            if (position < windowEnd)
            {
                var result = Utf8SliceRunner.TryMatchAt(program, input, position, [], [],
                    false, false, true, ref workspace, ref invocation);
                ThrowIfSliceBreached(result.Breach);
                if (result.IsMatch) return result.EndIndex;
            }
            position = windowEnd + 1;
        }
        return -1;
    }

    private static bool SeekLeadingAsciiByte(ReadOnlySpan<byte> bytes, byte leading, ref int position,
        scoped ref Utf8SliceInvocation invocation)
    {
        // This compiler proof is a necessary first byte. Every bounded scan
        // spends work under the original invocation before reading its block.
        while (position < bytes.Length)
        {
            ThrowIfSliceBreached(invocation.TryChargeInputStep());
            var length = Math.Min(2048, bytes.Length - position);
            var found = bytes.Slice(position, length).IndexOf(leading);
            if (found >= 0) { position += found; return true; }
            position += length;
        }
        return false;
    }

    internal static int Count(
        Utf8SliceProgram program,
        in Pcre2CandidateSearchProgram candidates,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        scoped ref Utf8SliceInvocation invocation)
    {
        // A non-empty program needs no PCRE2 empty retry. Keep admitted scratch
        // across matches instead of renting/releasing it on every MoveNext.
        if (!program.CanMatchEmpty && !program.RightToLeft)
        {
            ref readonly var branchPrefixes = ref (candidates.Kind == Pcre2CandidateSearchKind.BranchLeadingLiterals
                ? ref candidates.Searcher : ref s_emptyCountBranchPrefixes);
            return CountNonEmpty(program, in s_emptyCountSearchPlan, ref input, start, ref invocation, in branchPrefixes);
        }

        var cursor = new Pcre2SliceGlobalMatchCursor(
            program, invocation, input, start, request);
        var count = 0;
        while (cursor.MoveNext())
        {
            count = checked(count + 1);
        }

        return count;
    }

    internal static bool TryCountGeneralCategory(Pcre2BacktrackingProgram program, ReadOnlySpan<byte> input,
        scoped ref Utf8SliceInvocation invocation, out int count)
    {
        count = 0;
        var code = program.Instructions;
        if ((program.Request.Options & (Pcre2CompileOptions.Anchored | Pcre2CompileOptions.EndAnchored |
                Pcre2CompileOptions.FirstLine)) != 0 || program.Request.SkipsCrLfContinuation ||
            code.Length is < 2 or > 64 || code[^1].Kind != Pcre2BacktrackingInstructionKind.Accept)
            return false;

        // A complete one-scalar program needs no capture state for Count.
        var index = 0;
        while (code[index].Kind == Pcre2BacktrackingInstructionKind.CaptureStart) index++;
        ref readonly var instruction = ref code[index++];
        if (instruction.Kind is not (Pcre2BacktrackingInstructionKind.Token or Pcre2BacktrackingInstructionKind.CaptureToken) ||
            instruction.Token.Kind != Pcre2CharacterTokenKind.CharacterClass ||
            (instruction.Token.Options & Pcre2CharacterOptions.Caseless) != 0) return false;
        while (code[index].Kind == Pcre2BacktrackingInstructionKind.CaptureEnd) index++;
        var characterClass = instruction.Token.CharacterClass;
        if (index != code.Length - 1 || !characterClass.TryGetGeneralCategories(out var categories)) return false;

        ThrowIfSliceBreached(Utf8UnicodeCategoryExecutor.TryCount(input, categories,
            characterClass.AsciiSet.ActualLowMask, characterClass.AsciiSet.ActualHighMask, ref invocation, out count));
        return true;
    }

    // The caller proves a non-empty left-to-right language, so no PCRE2 empty
    // retry is needed. Keep one scratch rental and one original invocation for
    // the entire count; the shared runner meters search and verification.
    internal static int CountNonEmpty(
        Utf8SliceProgram program,
        in Utf8SearchPlan searchPlan,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        scoped ref Utf8SliceInvocation invocation,
        in PreparedSearcher branchPrefixes = default)
    {
        // Count observes only the endpoints. The shared runner owns the
        // terminal-scalar proof and only capture reads require slots here.
        var slots = program.ReadsCaptures ? RentProgramSlots(program, ref invocation) : new Utf8SliceSlots([], []);
        try
        {
            var outcome = Utf8SliceRunner.CountFromBytes(program, input.Bytes, start.Value,
                slots.Starts, slots.Ends, ref invocation, searchPlan, branchPrefixes);
            ThrowIfSliceBreached(outcome.Breach);
            return outcome.Count;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    internal static bool TryMatchDetailed(
        Utf8SliceProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        scoped ref Utf8SliceInvocation invocation,
        out Pcre2GroupData[] groups,
        out string? mark)
    {
        var slots = Pcre2SliceDriver.RentProgramSlots(program, ref invocation);
        Pcre2GroupData[]? projected = null;
        try
        {
            var bytes = input.Bytes;
            invocation.AnchorOriginByte = start.Value;
            for (var position = start.Value; position <= bytes.Length; position++)
            {
                Array.Fill(slots.Starts, -1);
                Array.Fill(slots.Ends, -1);
                var outcome = Utf8SliceRunner.TryMatchAt(
                    program,
                    bytes,
                    position,
                    slots.Starts,
                    slots.Ends,
                    Pcre2SliceDriver.IsScalarAligned(bytes, position),
                    ref invocation);
                ThrowIfSliceBreached(outcome.Breach);
                if (!outcome.IsMatch)
                {
                    continue;
                }

                groups = projected = ProjectSliceGroups(
                    program,
                    ref input,
                    position,
                    outcome.EndIndex,
                    slots.Starts,
                    slots.Ends, ref invocation);
                mark = null;
                return true;
            }

            groups = [];
            mark = null;
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException(
                "The PCRE2 match deadline expired.",
                Pcre2ErrorKind.Timeout);
        }
        finally
        {
            try { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
            finally { if (projected is not null) DetachProjectedGroups(projected, ref invocation); }
        }
    }

    // Slice slots are zero-based and backward assertions reverse their edges.
    // The shared batch projector handles both layouts without copied slot vectors.
    internal static Pcre2GroupData[] ProjectSliceGroups(
        Utf8SliceProgram program, ref Utf8ValidatedInput input,
        int matchStart, int matchEnd, int[] captureStarts, int[] captureEnds,
        ref Utf8SliceInvocation invocation) =>
        Pcre2Runner.PublishSliceCaptures(checked(program.GroupCount + 1),
            matchStart, matchEnd, captureStarts, captureEnds, ref input, ref invocation);

    // Single lexical publishing owner; immutable external arrays are never pooled.
    internal static void DetachProjectedGroups(Pcre2GroupData[] groups, ref Utf8SliceInvocation invocation)
        => invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(groups.Length));

    // Preserve the original sticky denial when a shared output/cursor authority
    // has already failed; array representability still reports heap capacity.
    internal static Utf8SliceSlots RentProgramSlots(
        Utf8SliceProgram program,
        ref Utf8SliceInvocation invocation)
    {
        try
        {
            ThrowIfSliceBreached(Utf8SliceRunner.TryRentSlots(program.GroupCount, ref invocation, out var slots));
            return slots;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    // Request limits apply to slice per-instruction-visit steps, frame depth, and
    // ledger bytes. Step counts are not comparable across engines (a backtracking VM
    // step is not a slice visit); only limit enforcement behavior is shared, so
    // numeric limits must not be read as cross-engine budgets.
    internal static Utf8SliceInvocation CreateInvocation(
        Pcre2CompileRequest request,
        CancellationToken cancellationToken, IUtf8MemoryAllowance? memoryAllowance = null)
        => new(request.DefaultLimits.ToSliceLimits(), request.MatchTimeout, cancellationToken, memoryAllowance);

    internal static Utf8SliceInvocation CreateExclusiveInvocation(Pcre2CompileRequest request,
        CancellationToken token, IUtf8MemoryAllowance? allowance = null)
        => Utf8SliceInvocation.CreateExclusive(request.DefaultLimits.ToSliceLimits(), request.MatchTimeout, token, allowance);

    // Global cursors retain this authority across waits/copies. A return check
    // spends no additional work and preserves cancellation raised by the clock.
    internal static void CheckInvocation(scoped ref Utf8SliceInvocation invocation)
    {
        if (invocation.IsUnmetered) return;
        try
        {
            invocation.Budget.ThrowIfCancellationRequested();
            invocation.Budget.CheckDeadline();
            invocation.Budget.ThrowIfCancellationRequested();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    internal static void ThrowIfSliceBreached(Utf8ExecutionLimitKind breach)
    {
        switch (breach)
        {
            case Utf8ExecutionLimitKind.None:
                return;
            case Utf8ExecutionLimitKind.WorkSteps:
                throw new Pcre2MatchException(
                    "The PCRE2 match limit was exceeded.",
                    Pcre2ErrorKind.MatchLimit);
            case Utf8ExecutionLimitKind.Depth:
                throw new Pcre2MatchException(
                    "The PCRE2 depth limit was exceeded.",
                    Pcre2ErrorKind.DepthLimit);
            case Utf8ExecutionLimitKind.WorkingBytes:
                throw new Pcre2MatchException(
                    "The PCRE2 heap limit was exceeded.",
                    Pcre2ErrorKind.HeapLimit);
            case Utf8ExecutionLimitKind.ResultBytes:
                throw new Pcre2MatchException(
                    "The PCRE2 result-capacity limit was exceeded.",
                    Pcre2ErrorKind.ResultLimit);
            default:
                throw new InvalidOperationException(
                    "Unknown slice limit kind.");
        }
    }
}
