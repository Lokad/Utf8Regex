using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

// The cursor owns PCRE2 progression and runs each attempt through the shared
// scalar matcher under one cumulative invocation. Enumeration refunds its
// scratch at every step so aliases and abandoned cursors hold no working loan.
// Synchronous Count can reuse its scratch across steps and refund it on return.
internal ref struct Pcre2SliceGlobalMatchCursor
{
    private readonly Utf8SliceProgram _program;
    private Utf8ValidatedInput _input;
    private readonly Pcre2CompileRequest _request;
    private Utf8SliceInvocation _invocation;
    private int _position;
    private bool _retryPending;
    private readonly byte? _leadingAsciiByte;
    private readonly int _disjointClassDelimiter;

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
        byte? leadingAsciiByte = null,
        int disjointClassDelimiter = -1)
    {
        _program = program;
        _input = input;
        _request = request;
        _invocation = invocation;
        _position = start.Value;
        _retryPending = false;
        _leadingAsciiByte = leadingAsciiByte;
        _disjointClassDelimiter = disjointClassDelimiter;
        Current = Pcre2CharacterMatch.NoMatch;
    }

    internal Pcre2CharacterMatch Current { get; private set; }

    [UnscopedRef] internal ref Utf8SliceInvocation WorkingInvocation => ref _invocation;

    // The caller owns the slot and projection loans. An existing projection
    // buffer is already admitted and has room for this program's groups.
    internal Pcre2GroupData[] ProjectCurrentCaptures(Utf8SliceSlots slots, Pcre2GroupData[]? groups = null)
        => Pcre2SliceDriver.ProjectSliceGroups(_program,
            Current.StartOffsetInBytes, Current.EndOffsetInBytes, slots.Starts, slots.Ends, ref _invocation, groups);

    internal bool MoveNext(Utf8SliceSlots slots = default)
    {
        Pcre2SliceDriver.CheckInvocation(ref _invocation);
        if (_position < 0) return false;
        var ownsSlots = slots.Starts is null;
        if (ownsSlots)
            slots = _disjointClassDelimiter >= 0 ? new Utf8SliceSlots([], []) :
                Pcre2SliceDriver.RentProgramSlots(_program, ref _invocation);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        bool moved;
        try { moved = MoveNextCore(slots, ref workspace); }
        finally
        {
            try { workspace.Release(ref _invocation); }
            finally { if (ownsSlots) Utf8SliceRunner.ReleaseSlots(slots, ref _invocation); }
        }
        Pcre2SliceDriver.CheckInvocation(ref _invocation);
        return moved;
    }

    internal int CountRemaining()
    {
        Pcre2SliceDriver.CheckInvocation(ref _invocation);
        var slots = _program.ReadsCaptures
            ? Pcre2SliceDriver.RentProgramSlots(_program, ref _invocation)
            : new Utf8SliceSlots([], []);
        // A synchronous Count can hold one admitted workspace until it returns.
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var count = 0;
        try
        {
            while (MoveNextCore(slots, ref workspace))
            {
                Pcre2SliceDriver.CheckInvocation(ref _invocation);
                count = checked(count + 1);
            }
        }
        finally
        {
            try { workspace.Release(ref _invocation); }
            finally { Utf8SliceRunner.ReleaseSlots(slots, ref _invocation); }
        }
        Pcre2SliceDriver.CheckInvocation(ref _invocation);
        return count;
    }

    internal Pcre2GroupData ProjectCurrent()
    {
        // The public enumerator retains this byte/status snapshot for Current;
        // publication does not scan text or maintain coordinate state.
        return Pcre2MatchPublication.Publish(Current.StartOffsetInBytes,
            Current.EndOffsetInBytes, ref _input, new Pcre2ResultControl(ref _invocation));
    }

    private bool MoveNextCore(Utf8SliceSlots slots, scoped ref Utf8SliceRunner.AttemptWorkspace workspace)
    {
        // Each MoveNext call is one scan: pin \\G to the entry position.
        _invocation.AnchorOriginByte = _position;
        try
        {
            var bytes = _input.Bytes;
            if (_retryPending)
            {
                _retryPending = false;
                var retry = Utf8SliceRunner.TryMatchAt(
                    _program, bytes, _position, slots.Starts, slots.Ends,
                    false, true, Pcre2SliceDriver.IsScalarAligned(bytes, _position),
                    ref workspace, ref _invocation);
                Pcre2SliceDriver.ThrowIfSliceBreached(retry.Breach);
                if (retry.IsMatch)
                {
                    Current = Pcre2CharacterMatch.Create(_position, retry.EndIndex);
                    _position = retry.EndIndex;
                    return true;
                }

                if (!Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(
                        ref _input, _request, Pcre2MatchOptions.None, new Utf8BytePosition(_position), out var advanced))
                {
                    _position = -1;
                    return false;
                }
                _position = advanced.Value;
            }

            var lastStart = _leadingAsciiByte.HasValue ? bytes.Length - 1 : bytes.Length;
            var end = Pcre2SliceDriver.FindMatchingEnd(_program, bytes, ref _position, lastStart, _leadingAsciiByte, slots,
                ref workspace, ref _invocation, _disjointClassDelimiter);
            if (end < 0)
            {
                _position = -1;
                return false;
            }
            Current = Pcre2CharacterMatch.Create(_position, end);
            _retryPending = end == _position;
            _position = end;
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException(
                "The PCRE2 match deadline expired.",
                Pcre2ErrorKind.Timeout);
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
    // operations before renting VM scratch. A leading byte hit can also
    // supply the first possible start for capture verification.
    internal static bool RejectMissingCandidate(
        Utf8SliceProgram program,
        in Pcre2CandidateSearchProgram candidates,
        ReadOnlySpan<byte> input,
        int start,
        scoped ref Utf8SliceInvocation invocation, out int firstAsciiStart)
    {
        firstAsciiStart = -1;
        if (program.RejectsByFinalAscii(input))
        {
            ThrowIfSliceBreached(invocation.TryChargeInputStep());
            return true;
        }
        if (program.CanMatchEmpty || program.RightToLeft ||
            candidates.Kind is not (Pcre2CandidateSearchKind.LeadingAsciiSet or
                Pcre2CandidateSearchKind.LeadingRunThenLiteral)) return false;

        var breach = candidates.Searcher.TryFindMatch(input, start, input.Length,
            false, ref invocation, out var hit, out var found);
        ThrowIfSliceBreached(breach);
        if (found && candidates.Kind == Pcre2CandidateSearchKind.LeadingAsciiSet)
            firstAsciiStart = hit.Index;
        return !found;
    }

    internal static Pcre2CharacterMatch FindFirst(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int minimumByteLength,
        scoped ref Utf8SliceInvocation invocation,
        byte? leadingAsciiByte = null,
        int delimiter = -1,
        int firstAsciiStart = -1)
    {
        CheckInvocation(ref invocation);
        // Bound candidate starts; the matcher still sees the full subject.
        var lastStart = input.Length - minimumByteLength;
        var position = Math.Max(start, firstAsciiStart);
        if (position > lastStart) return Pcre2CharacterMatch.NoMatch;
        // Only matching dependencies can observe slots in a whole-value search.
        var slots = program.ReadsCaptures ? RentProgramSlots(program, ref invocation) : new Utf8SliceSlots([], []);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        invocation.AnchorOriginByte = start;
        if (program.CanMatchEmpty || program.HasContiguousAnchor) leadingAsciiByte = null;
        int end;
        try
        {
            end = FindMatchingEnd(program, input, ref position, lastStart, leadingAsciiByte, slots,
                ref workspace, ref invocation, delimiter);
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
        scoped ref Utf8SliceInvocation invocation, int delimiter = -1)
    {
        var seekLineStart = leadingAsciiByte is { } head && program.Instructions[0] is
            { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning, B: 1, C: var literal } &&
            literal == head + 1;
        while (position <= lastStart)
        {
            if (leadingAsciiByte is { } leading && position < input.Length &&
                (input[position] != leading || seekLineStart && position != 0 && input[position - 1] != (byte)'\n'))
            {
                var candidates = lastStart < input.Length ? input[..(lastStart + 1)] : input;
                if (seekLineStart)
                {
                    // The mandatory LF anchor and exact first byte bound discovery only.
                    // The verifier still sees the complete subject and the original origin.
                    ThrowIfSliceBreached(Utf8SliceRunner.TrySeekBeginning(program, candidates, ref position, ref invocation));
                    if (position < 0) return -1;
                }
                else if (!SeekLeadingAsciiByte(candidates, leading, ref position, ref invocation))
                    return -1;
            }
            var outcome = Utf8SliceRunner.TryMatchAt(program, input, position, slots.Starts, slots.Ends,
                false, false, IsScalarAligned(input, position), ref workspace, ref invocation);
            ThrowIfSliceBreached(outcome.Breach);
            if (outcome.IsMatch) return outcome.EndIndex;

            if (delimiter >= 0 && lastStart - position >= 64)
            {
                var previous = position;
                position = Utf8SliceRunner.AdvancePastScalar(input, position, reverse: false);
                var (first, last, _, _, _, _, _) = program.LeadingByteCandidate;
                // A separator before a plausible start does not need a window seek.
                if (first >= 0 && position >= 0 &&
                    (uint)(input[previous] - first) > (uint)(last - first) &&
                    (uint)(input[position] - first) <= (uint)(last - first))
                    continue;
                return FindInDelimiterWindows(program, input, (byte)delimiter, ref position,
                    lastStart, slots, ref workspace, ref invocation);
            }
            position++;
        }
        return -1;
    }

    internal static int GetDisjointClassDelimiter(Utf8SliceProgram program)
    {
        if (program.ReadsCaptures || program.HasContiguousAnchor || program.SawLookaround ||
            program.LeadingClassDelimiter is not { Direct: true, LeadingClass: > 0, Literal: >= 0 } window)
            return -1;

        var code = program.Instructions;
        var repeat = window.LeadingClass - 1;
        if (code[repeat] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: > 0, RepeatMaximum: long.MaxValue } ||
            !program.TryGetUnfoldedAsciiSet(code[window.LeadingClass], out var leading, out var asciiOnly) ||
            !asciiOnly || leading.Contains((byte)window.Literal))
            return -1;

        // Only capture markers may surround the run before its delimiter.
        // A later start in the same run reaches the same suffix with fewer
        // bytes. A different capture value cannot affect acceptance here.
        Utf8ConstructionScope.ChargeWork((ulong)(window.LiteralIndex - 4));
        for (var pc = 0; pc < window.LiteralIndex; pc++)
        {
            if (pc == repeat) { pc += 3; continue; }
            if (code[pc].Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd))
                return -1;
        }
        return window.Literal;
    }

    private static int FindInDelimiterWindows(Utf8SliceProgram program, ReadOnlySpan<byte> input, byte delimiter,
        ref int position, int lastStart, Utf8SliceSlots slots, scoped ref Utf8SliceRunner.AttemptWorkspace workspace,
        scoped ref Utf8SliceInvocation invocation)
    {
        while (position >= 0 && position <= lastStart)
        {
            var breach = Utf8SliceRunner.TrySeekLeadingClassLiteralWindow(program, input,
                program.LeadingClassDelimiter.LeadingClass, delimiter,
                search: null, gapMaximum: 0, direct: true, ref position, ref invocation, out var windowEnd);
            ThrowIfSliceBreached(breach);
            if (position < 0 || position > lastStart) break;
            if (position < windowEnd)
            {
                var result = Utf8SliceRunner.TryMatchAt(program, input, position, slots.Starts, slots.Ends,
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
        Pcre2SliceDirectProgram slice,
        in Pcre2CandidateSearchProgram candidates,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        scoped ref Utf8SliceInvocation invocation)
    {
        var program = slice.SliceProgram;
        // The shared Count loop already skips impossible capture-first
        // candidates without paying for the explicit delimiter rewind.
        if (candidates.Kind == Pcre2CandidateSearchKind.LeadingRunThenLiteral &&
            slice.DisjointClassDelimiter >= 0 && input.ByteLength - start.Value - slice.Fallback.MinimumByteLength >= 64)
            return CountWithDelimiterWindows(slice, input.Bytes, start.Value, ref invocation);

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
        return cursor.CountRemaining();
    }

    private static int CountWithDelimiterWindows(Pcre2SliceDirectProgram slice, ReadOnlySpan<byte> input, int position,
        scoped ref Utf8SliceInvocation invocation)
    {
        // The delimiter proof requires a nonempty prefix and no capture reads.
        var slots = new Utf8SliceSlots([], []);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var lastStart = input.Length - slice.Fallback.MinimumByteLength;
        var count = 0;
        try
        {
            while (position <= lastStart)
            {
                invocation.AnchorOriginByte = position;
                var end = FindMatchingEnd(slice.SliceProgram, input, ref position, lastStart,
                    slice.Fallback.LeadingAsciiByte, slots, ref workspace, ref invocation, slice.DisjointClassDelimiter);
                if (end < 0) break;
                count = checked(count + 1);
                position = end;
            }
            return count;
        }
        finally { workspace.Release(ref invocation); }
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

    internal static Utf8SliceSearchOutcome FindCaptureRanges(Pcre2SliceDirectProgram slice, ReadOnlySpan<byte> bytes,
        int start, Utf8SliceSlots slots, scoped ref Utf8SliceRunner.AttemptWorkspace workspace,
        scoped ref Utf8SliceInvocation invocation)
    {
        var lastStart = bytes.Length - slice.Fallback.MinimumByteLength;
        if (lastStart - start >= 64 && slice.DisjointClassDelimiter >= 0)
        {
            var begin = start;
            var end = FindMatchingEnd(slice.SliceProgram, bytes, ref begin, lastStart, leadingAsciiByte: null,
                slots, ref workspace, ref invocation, slice.DisjointClassDelimiter);
            return end < 0 ? new(false, -1, -1, Utf8ExecutionLimitKind.None) : new(true, begin, end, Utf8ExecutionLimitKind.None);
        }
        return Utf8SliceRunner.TryFindFirst(slice.SliceProgram, bytes, start,
            slots.Starts, slots.Ends, false, ref workspace, ref invocation);
    }

    internal static bool TryMatchDetailed(
        Pcre2SliceDirectProgram slice, in Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        scoped ref Utf8SliceInvocation invocation,
        out Pcre2GroupData[] groups,
        out string? mark)
    {
        var program = slice.SliceProgram;
        var slots = new Utf8SliceSlots([], []);
        Pcre2GroupData[]? projected = null;
        groups = [];
        mark = null;
        try
        {
            var bytes = input.Bytes;
            invocation.AnchorOriginByte = start.Value;
            if (RejectMissingCandidate(program, candidateSearch, bytes, start.Value, ref invocation, out var firstAsciiStart))
                return true;
            slots = RentProgramSlots(program, ref invocation);
            var workspace = new Utf8SliceRunner.AttemptWorkspace();
            Utf8SliceSearchOutcome outcome = default;
            try
            {
                var position = firstAsciiStart >= 0 ? firstAsciiStart : start.Value;
                if (firstAsciiStart >= 0 && bytes.Length - position <= 64)
                {
                    // The screen skipped only impossible starts. Verify
                    // its ASCII hit on the original subject and origin.
                    var attempt = Utf8SliceRunner.TryMatchAt(program, bytes, position, slots.Starts, slots.Ends,
                        false, false, true, ref workspace, ref invocation);
                    ThrowIfSliceBreached(attempt.Breach);
                    if (attempt.IsMatch)
                        outcome = new(true, position, attempt.EndIndex, Utf8ExecutionLimitKind.None);
                    else
                        position++;
                }
                if (!outcome.Found)
                    outcome = FindCaptureRanges(slice, bytes, position, slots, ref workspace, ref invocation);
            }
            finally { workspace.Release(ref invocation); }
            ThrowIfSliceBreached(outcome.Breach);
            if (outcome.Found)
                groups = projected = ProjectSliceGroups(program, outcome.StartByte, outcome.EndByte,
                    slots.Starts, slots.Ends, ref invocation);
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
        Utf8SliceProgram program,
        int matchStart, int matchEnd, int[] captureStarts, int[] captureEnds,
        ref Utf8SliceInvocation invocation, Pcre2GroupData[]? groups = null) =>
        Pcre2Runner.PublishSliceCaptures(checked(program.GroupCount + 1),
            matchStart, matchEnd, captureStarts, captureEnds, ref invocation, groups);

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
