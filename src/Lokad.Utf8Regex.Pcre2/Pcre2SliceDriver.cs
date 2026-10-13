using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

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
// on misses. IsMatch, first-match, and Count all ride this stepping loop.
internal ref struct Pcre2SliceGlobalMatchCursor
{
    private readonly Utf8SliceProgram _program;
    private Utf8ValidatedInput _input;
    private readonly Pcre2CompileRequest _request;
    private Utf8SliceInvocation _invocation;
    private int _position;
    private bool _retryPending;
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
        Pcre2CompileRequest request)
    {
        _program = program;
        _input = input;
        _request = request;
        _invocation = invocation;
        _position = start.Value;
        _retryPending = false;
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
                false,
                requireNonEmpty,
                Pcre2SliceDriver.IsScalarAligned(_input.Bytes, startByte),
                ref _invocation);
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
        if (_invocation.IsUnmetered)
            return Pcre2GroupData.FromByteOffsets(_input.Bytes, 0,
                Current.StartOffsetInBytes, Current.EndOffsetInBytes);
        var projection = _input.CreateProjectionCursor();
        return Pcre2GlobalCursorProjection.Project(Current.StartOffsetInBytes,
            Current.EndOffsetInBytes, ref _input, ref projection,
            new Pcre2CaptureProjectionBudget(ref _invocation));
    }

    private bool MoveNextCore()
    {
        // Each MoveNext call is one scan: pin \\G to the entry position.
        _invocation.AnchorOriginByte = _position;
        var slots = Pcre2SliceDriver.RentProgramSlots(_program, ref _invocation);
        try
        {
            var bytes = _input.Bytes;
            while (true)
            {
                if (_retryPending)
                {
                    _retryPending = false;
                    var retry = Utf8SliceRunner.TryMatchAt(
                        _program,
                        bytes,
                        _retryPosition,
                        slots.Starts,
                        slots.Ends,
                        false,
                        true,
                        Pcre2SliceDriver.IsScalarAligned(bytes, _retryPosition),
                        ref _invocation);
                    Pcre2SliceDriver.ThrowIfSliceBreached(retry.Breach);
                    if (retry.IsMatch)
                    {
                        Current = Pcre2CharacterMatch.Create(
                            _retryPosition, retry.EndIndex);
                        _position = retry.EndIndex;
                        return true;
                    }

                    if (!Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(
                            ref _input,
                            _request.Settings,
                            new Utf8BytePosition(_retryPosition),
                            out var advanced))
                    {
                        return false;
                    }

                    _position = advanced.Value;
                }

                while (_position <= bytes.Length)
                {
                    var outcome = Utf8SliceRunner.TryMatchAt(
                        _program,
                        bytes,
                        _position,
                        slots.Starts,
                        slots.Ends,
                        false,
                        false,
                        Pcre2SliceDriver.IsScalarAligned(bytes, _position),
                        ref _invocation);
                    Pcre2SliceDriver.ThrowIfSliceBreached(outcome.Breach);
                    if (!outcome.IsMatch)
                    {
                        _position++;
                        continue;
                    }

                    Current = Pcre2CharacterMatch.Create(
                        _position, outcome.EndIndex);
                    if (outcome.EndIndex == _position)
                    {
                        _retryPending = true;
                        _retryPosition = _position;
                    }
                    else
                    {
                        _position = outcome.EndIndex;
                    }

                    return true;
                }

                return false;
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException(
                "The PCRE2 match deadline expired.",
                Pcre2ErrorKind.Timeout);
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref _invocation);
        }
    }
}

internal static class Pcre2SliceDriver
{
    // Attempt-origin alignment for absolute-origin assertions (`^`): the end
    // of input is always aligned, and any byte below it is aligned unless it
    // opens a UTF-8 continuation byte.
    internal static bool IsScalarAligned(ReadOnlySpan<byte> bytes, int position)
    {
        return (uint)position >= (uint)bytes.Length || (bytes[position] & 0xC0) != 0x80;
    }

    internal static bool IsMatch(
        Utf8SliceProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        scoped ref Utf8SliceInvocation invocation)
    {
        var cursor = new Pcre2SliceGlobalMatchCursor(
            program, invocation, input, start, request);
        return cursor.MoveNext();
    }

    internal static Pcre2CharacterMatch FindFirst(
        Utf8SliceProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        scoped ref Utf8SliceInvocation invocation)
    {
        var cursor = new Pcre2SliceGlobalMatchCursor(
            program, invocation, input, start, request);
        return cursor.MoveNext() ? cursor.Current : Pcre2CharacterMatch.NoMatch;
    }

    internal static int Count(
        Utf8SliceProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        scoped ref Utf8SliceInvocation invocation)
    {
        var cursor = new Pcre2SliceGlobalMatchCursor(
            program, invocation, input, start, request);
        var count = 0;
        while (cursor.MoveNext())
        {
            count = checked(count + 1);
        }

        return count;
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
                    false,
                    false,
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
        Pcre2Runner.ProjectSliceCaptures(checked(program.GroupCount + 1),
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
        CancellationToken cancellationToken)
    {
        return new Utf8SliceInvocation(
            new Utf8ExecutionLimits(
                request.DefaultLimits.MatchLimit,
                request.DefaultLimits.DepthLimit,
                request.DefaultLimits.HeapLimitInBytes,
                request.DefaultLimits.ResultLimitInBytes),
            request.MatchTimeout,
            cancellationToken);
    }

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
