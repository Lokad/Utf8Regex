using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedWindowSearch
{
    internal Utf8ExecutionLimitKind TryFindNextWindow(ReadOnlySpan<byte> input,
        ref PreparedWindowScanState state, ref Utf8SliceInvocation invocation,
        out PreparedWindowMatch window, out bool found)
    {
        window = default;
        found = false;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            found = TryFindNextWindow(input, ref state, out window);
            return Utf8ExecutionLimitKind.None;
        }
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (!HasValue || state.TrailingExhausted || (uint)state.NextStart > (uint)input.Length)
            return Utf8ExecutionLimitKind.None;
        var leadingState = state.LeadingState;
        var trailingState = state.TrailingState;
        var trailing = state.TrailingMatch;
        var hasTrailing = state.HasTrailingMatch;
        var exhausted = state.TrailingExhausted;
        while (!exhausted)
        {
            breach = LeadingSearcher.TryFindNextOverlappingMatch(input, ref leadingState,
                ref invocation, out var leading, out var hasLeading);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!hasLeading) break;
            var floor = leading.Index + leading.Length;
            if (floor > input.Length) continue;
            if (!hasTrailing) trailingState = new PreparedSearchScanState(floor, default);
            while (!hasTrailing || trailing.Index < floor)
            {
                breach = TrailingSearcher.TryFindNextOverlappingMatch(input, ref trailingState,
                    ref invocation, out trailing, out hasTrailing);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!hasTrailing) { exhausted = true; break; }
            }
            if (exhausted) break;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            invocation.Budget.ThrowIfCancellationRequested();
            state = new PreparedWindowScanState(leading.Index + 1, leadingState, trailingState, trailing, hasTrailing, exhausted);
            window = new PreparedWindowMatch(leading, trailing);
            found = true;
            return Utf8ExecutionLimitKind.None;
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        state = new PreparedWindowScanState(input.Length, leadingState, trailingState, trailing, hasTrailing, exhausted);
        return Utf8ExecutionLimitKind.None;
    }
}
