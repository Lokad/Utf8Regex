using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    // Same compiled programs and native finders; only final capture shaping
    // differs from MatchDetailed. No UTF-16 coordinate projection is requested.
    internal static Pcre2BacktrackingMatch MatchByteRanges(Pcre2CompiledProgram compiled,
        ref Utf8ValidatedInput input, Utf8BytePosition start, Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions options, ref Pcre2ResourceBudget budget,
        ref Utf8SliceSlots sliceSlots, ref Utf8SliceRunner.AttemptWorkspace sliceWorkspace,
        scoped Span<Utf8Pcre2CaptureRange> destination = default)
    {
        Pcre2CharacterMatch match;
        switch (compiled.Operations.Match)
        {
            case Pcre2SliceDirectProgram slice when options == Pcre2MatchOptions.None ||
                options == (Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart):
                return MatchSliceByteRanges(slice, compiled, ref input, start, firstMatchingPosition,
                    options != Pcre2MatchOptions.None, ref budget, ref sliceSlots, ref sliceWorkspace, destination);
            case Pcre2LiteralDirectProgram literal:
                var literalMatch = Pcre2LiteralRunner.Match(literal.Program, ref input, start, options, ref budget);
                match = literalMatch.Success ? new(true, literalMatch.StartOffsetInBytes, literalMatch.EndOffsetInBytes,
                    literalMatch.StartOffsetInBytes, literalMatch.ConsumedEndOffsetInBytes, false) : default;
                break;
            case Pcre2LiteralFamilyDirectProgram family when options == Pcre2MatchOptions.None &&
                family.Regex.TryFindNativeLiteralForResult(input.Bytes, start.Value, ref budget.WorkingInvocation,
                    out var found, out var begin, out var end):
                match = found ? Pcre2CharacterMatch.Create(begin, end) : default;
                break;
            default:
                var program = compiled.Operations.Match switch
                {
                    Pcre2BacktrackingDirectProgram vm => vm.Program,
                    Pcre2SliceDirectProgram slice => slice.Fallback,
                    Pcre2LiteralFamilyDirectProgram literals => literals.Fallback,
                    _ => compiled.Operations.Enumerate switch
                    {
                        Pcre2SameStartLiteralDirectProgram same => same.Fallback,
                        _ => throw new NotSupportedException("SPEC-PCRE2 does not support this pattern for range discovery.")
                    }
                };
                return Pcre2BacktrackingRunner.MatchByteRanges(program, compiled.CandidateSearch, ref input,
                    start, firstMatchingPosition, options, ref budget, destination);
        }
        if (!match.Success) return default;
        if (!destination.IsEmpty)
        {
            Pcre2BacktrackingRunner.WriteByteGroups(destination, match.StartOffsetInBytes, match.EndOffsetInBytes,
                default, input.Bytes, ref budget);
            return new(true, match.StartOffsetInBytes, match.EndOffsetInBytes,
                match.ConsumedStartOffsetInBytes, match.ConsumedEndOffsetInBytes, match.MatchBoundaryWasReset, default, null);
        }
        var groups = Pcre2BacktrackingRunner.MaterializeByteGroups(1, match.StartOffsetInBytes,
            match.EndOffsetInBytes, default, input.Bytes, ref budget);
        return new(true, match.StartOffsetInBytes, match.EndOffsetInBytes,
            match.ConsumedStartOffsetInBytes, match.ConsumedEndOffsetInBytes, match.MatchBoundaryWasReset,
            Pcre2BacktrackingCaptureResult.FromByteGroups(groups), null);
    }

    private static Pcre2BacktrackingMatch MatchSliceByteRanges(Pcre2SliceDirectProgram slice,
        Pcre2CompiledProgram compiled, ref Utf8ValidatedInput input, Utf8BytePosition start,
        Utf8BytePosition firstMatchingPosition, bool retryNonEmpty, ref Pcre2ResourceBudget budget,
        ref Utf8SliceSlots slots, ref Utf8SliceRunner.AttemptWorkspace workspace,
        scoped Span<Utf8Pcre2CaptureRange> destination)
    {
        var program = slice.SliceProgram;
        ref var invocation = ref budget.WorkingInvocation;
        invocation.AnchorOriginByte = firstMatchingPosition.Value;
        if (!retryNonEmpty && Pcre2SliceDriver.RejectMissingCandidate(program, compiled.CandidateSearch,
                input.Bytes, start.Value, ref invocation)) return default;
        // The exclusive memory cursor owns this admitted storage across moves,
        // just as Core's memory cursor does. Attempts reset semantic VM state;
        // capacity and its loan survive host waits until EOF/failure/Dispose.
        if (slots.Starts.Length != program.GroupCount) slots = Pcre2SliceDriver.RentProgramSlots(program, ref invocation);
        int begin, end;
        if (retryNonEmpty)
        {
            // PCRE2 retries an empty match anchored at the same position,
            // admitting only a nonempty result under the original origin.
            var retry = Utf8SliceRunner.TryMatchAt(program, input.Bytes, start.Value, slots.Starts, slots.Ends,
                false, true, Pcre2SliceDriver.IsScalarAligned(input.Bytes, start.Value), ref workspace, ref invocation);
            Pcre2SliceDriver.ThrowIfSliceBreached(retry.Breach);
            if (!retry.IsMatch) return default;
            begin = start.Value; end = retry.EndIndex;
        }
        else
        {
            var outcome = Pcre2SliceDriver.FindCaptureRanges(slice, input.Bytes, start.Value, slots, ref workspace, ref invocation);
            Pcre2SliceDriver.ThrowIfSliceBreached(outcome.Breach);
            if (!outcome.Found) return default;
            begin = outcome.StartByte; end = outcome.EndByte;
        }
        if (!destination.IsEmpty)
        {
            Pcre2BacktrackingRunner.WriteByteGroups(destination, begin, end, new(slots.Starts, slots.Ends),
                input.Bytes, ref budget, -1, true);
            return new(true, begin, end, begin, end, false, default, null);
        }
        var groups = Pcre2BacktrackingRunner.MaterializeByteGroups(checked(program.GroupCount + 1),
            begin, end, new(slots.Starts, slots.Ends), input.Bytes, ref budget, -1, true);
        return new(true, begin, end, begin, end, false, Pcre2BacktrackingCaptureResult.FromByteGroups(groups), null);
    }
}
