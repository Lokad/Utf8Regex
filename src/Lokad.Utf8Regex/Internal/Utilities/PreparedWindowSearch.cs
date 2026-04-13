namespace Lokad.Utf8Regex.Internal.Utilities;

internal readonly struct PreparedWindowSearch
{
    public PreparedWindowSearch(
        PreparedSearcher leadingSearcher,
        PreparedSearcher trailingSearcher,
        int? maxGap = null,
        bool sameLine = false)
    {
        LeadingSearcher = leadingSearcher;
        TrailingSearcher = trailingSearcher;
        MaxGap = maxGap;
        SameLine = sameLine;
    }

    public PreparedSearcher LeadingSearcher { get; }

    public PreparedSearcher TrailingSearcher { get; }

    public int? MaxGap { get; }

    public bool SameLine { get; }

    public bool HasValue => LeadingSearcher.HasValue && TrailingSearcher.HasValue;

    public int FindFirstStart(ReadOnlySpan<byte> input, int startIndex)
    {
        var state = new PreparedWindowScanState(startIndex, new PreparedSearchScanState(startIndex, default));
        return TryFindNextWindow(input, ref state, out var window) ? window.Leading.Index : -1;
    }

    public bool TryFindNextWindow(ReadOnlySpan<byte> input, ref PreparedWindowScanState state, out PreparedWindowMatch window)
    {
        window = default;

        if (!HasValue || (uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        var leadingState = state.LeadingState;
        var trailingState = state.TrailingState;
        var trailing = state.TrailingMatch;
        var hasTrailing = state.HasTrailingMatch;
        while (LeadingSearcher.TryFindNextOverlappingMatch(input, ref leadingState, out var leading))
        {
            if (TryFindTrailingAnchor(input, leading, ref trailingState, ref trailing, ref hasTrailing))
            {
                state = new PreparedWindowScanState(leading.Index + 1, leadingState, trailingState, trailing, hasTrailing);
                window = new PreparedWindowMatch(leading, trailing);
                return true;
            }
        }

        state = new PreparedWindowScanState(input.Length, leadingState, trailingState, trailing, hasTrailing);
        return false;
    }

    private bool TryFindTrailingAnchor(
        ReadOnlySpan<byte> input,
        PreparedSearchMatch leading,
        ref PreparedSearchScanState trailingState,
        ref PreparedSearchMatch trailing,
        ref bool hasTrailing)
    {
        var trailingSearchStart = leading.Index + leading.Length;
        if ((uint)trailingSearchStart > (uint)input.Length)
        {
            return false;
        }

        if (!hasTrailing)
        {
            trailingState = new PreparedSearchScanState(trailingSearchStart, default);
        }

        while (true)
        {
            if (hasTrailing && trailing.Index >= trailingSearchStart)
            {
                return true;
            }

            if (!TrailingSearcher.TryFindNextOverlappingMatch(input, ref trailingState, out trailing))
            {
                hasTrailing = false;
                return false;
            }

            hasTrailing = true;
        }
    }
}
internal readonly record struct PreparedWindowMatch(PreparedSearchMatch Leading, PreparedSearchMatch Trailing);

internal readonly record struct PreparedWindowScanState(
    int NextStart,
    PreparedSearchScanState LeadingState,
    PreparedSearchScanState TrailingState = default,
    PreparedSearchMatch TrailingMatch = default,
    bool HasTrailingMatch = false);
