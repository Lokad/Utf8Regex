using System;
using System.Threading;
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
internal static class Pcre2SliceDriver
{
    internal static bool IsMatch(
        Utf8SliceProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        CancellationToken cancellationToken)
    {
        var invocation = CreateInvocation(request, cancellationToken);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var bytes = input.Bytes;
            for (var position = start.Value; position <= bytes.Length; position++)
            {
                var outcome = Utf8SliceRunner.TryMatchAt(
                    program,
                    bytes,
                    position,
                    slots.Starts,
                    slots.Ends,
                    false,
                    false,
                    ref invocation);
                ThrowIfSliceBreached(outcome.Breach);
                if (outcome.IsMatch)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException(
                "The PCRE2 match deadline expired.",
                Pcre2ErrorKind.Timeout);
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    internal static Pcre2CharacterMatch FindFirst(
        Utf8SliceProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        CancellationToken cancellationToken)
    {
        var invocation = CreateInvocation(request, cancellationToken);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var bytes = input.Bytes;
            for (var position = start.Value; position <= bytes.Length; position++)
            {
                var outcome = Utf8SliceRunner.TryMatchAt(
                    program,
                    bytes,
                    position,
                    slots.Starts,
                    slots.Ends,
                    false,
                    false,
                    ref invocation);
                ThrowIfSliceBreached(outcome.Breach);
                if (outcome.IsMatch)
                {
                    return Pcre2CharacterMatch.Create(position, outcome.EndIndex);
                }
            }

            return Pcre2CharacterMatch.NoMatch;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException(
                "The PCRE2 match deadline expired.",
                Pcre2ErrorKind.Timeout);
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    internal static int Count(
        Utf8SliceProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        CancellationToken cancellationToken)
    {
        var invocation = CreateInvocation(request, cancellationToken);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var bytes = input.Bytes;
            var count = 0;
            var position = start.Value;
            while (position <= bytes.Length)
            {
                var outcome = Utf8SliceRunner.TryMatchAt(
                    program,
                    bytes,
                    position,
                    slots.Starts,
                    slots.Ends,
                    false,
                    false,
                    ref invocation);
                ThrowIfSliceBreached(outcome.Breach);
                if (!outcome.IsMatch)
                {
                    position++;
                    continue;
                }

                count = checked(count + 1);
                if (outcome.EndIndex != position)
                {
                    position = outcome.EndIndex;
                    continue;
                }

                var retry = Utf8SliceRunner.TryMatchAt(
                    program,
                    bytes,
                    position,
                    slots.Starts,
                    slots.Ends,
                    false,
                    true,
                    ref invocation);
                ThrowIfSliceBreached(retry.Breach);
                if (retry.IsMatch)
                {
                    position = retry.EndIndex;
                    continue;
                }

                if (!Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(
                        ref input,
                        request.Settings,
                        new Utf8BytePosition(position),
                        out var advanced))
                {
                    break;
                }

                position = advanced.Value;
            }

            return count;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException(
                "The PCRE2 match deadline expired.",
                Pcre2ErrorKind.Timeout);
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    internal static bool TryMatchDetailed(
        Utf8SliceProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        CancellationToken cancellationToken,
        out Pcre2GroupData[] groups,
        out string? mark)
    {
        var invocation = CreateInvocation(request, cancellationToken);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var bytes = input.Bytes;
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
                    ref invocation);
                ThrowIfSliceBreached(outcome.Breach);
                if (!outcome.IsMatch)
                {
                    continue;
                }

                groups = ProjectSliceGroups(
                    program,
                    bytes,
                    position,
                    outcome.EndIndex,
                    slots.Starts,
                    slots.Ends);
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
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    // Slice capture slots are zero-based for groups 1..N while
    // Pcre2Runner.ProjectCaptures expects one-based slots, so the
    // shared batch projector cannot consume them directly. Project
    // group by group instead: every slice range starts and ends on
    // scalar boundaries, where FromByteOffsets agrees exactly with
    // the batch projector (same flags, same prefix code-unit counts).
    private static Pcre2GroupData[] ProjectSliceGroups(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int matchStart,
        int matchEnd,
        int[] captureStarts,
        int[] captureEnds)
    {
        var groups = new Pcre2GroupData[program.GroupCount + 1];
        groups[0] = Pcre2GroupData.FromByteOffsets(
            input, 0, matchStart, matchEnd);
        for (var group = 1; group <= program.GroupCount; group++)
        {
            var captureStart = captureStarts[group - 1];
            var captureEnd = captureEnds[group - 1];
            groups[group] = captureStart < 0 || captureEnd < captureStart
                ? new Pcre2GroupData { Number = group, Success = false }
                : Pcre2GroupData.FromByteOffsets(
                    input, group, captureStart, captureEnd);
        }

        return groups;
    }

    private static Utf8SliceInvocation CreateInvocation(
        Pcre2CompileRequest request,
        CancellationToken cancellationToken)
    {
        return new Utf8SliceInvocation(
            new Utf8ExecutionLimits(
                request.DefaultLimits.MatchLimit,
                request.DefaultLimits.DepthLimit,
                request.DefaultLimits.HeapLimitInBytes),
            request.MatchTimeout,
            cancellationToken);
    }

    private static void ThrowIfSliceBreached(Utf8ExecutionLimitKind breach)
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
            default:
                throw new InvalidOperationException(
                    "Unknown slice limit kind.");
        }
    }
}
