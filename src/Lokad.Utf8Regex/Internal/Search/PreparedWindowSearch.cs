namespace Lokad.Utf8Regex.Internal.Search;

internal readonly struct PreparedWindowSearch
{
    // Each leading source and trailing source advances monotonically. A failed
    // trailing scan reaches end-of-input once, so the scan is O(n + candidates).
    public PreparedWindowSearch(PreparedSearcher leadingSearcher, PreparedSearcher trailingSearcher)
        : this(leadingSearcher, trailingSearcher, maxGap: null, sameLine: false)
    {
    }

    public PreparedWindowSearch(
        PreparedSearcher leadingSearcher,
        PreparedSearcher trailingSearcher,
        int? maxGap,
        bool sameLine)
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
        var state = PreparedWindowScanState.Create(startIndex);
        return TryFindNextWindow(input, ref state, out var window) ? window.Leading.Index : -1;
    }

    public bool TryFindNextWindow(ReadOnlySpan<byte> input, ref PreparedWindowScanState state, out PreparedWindowMatch window)
    {
        window = default;

        if (!HasValue || state.TrailingExhausted || (uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        var leadingState = state.LeadingState;
        var trailingState = state.TrailingState;
        var trailing = state.TrailingMatch;
        var hasTrailing = state.HasTrailingMatch;
        var trailingExhausted = state.TrailingExhausted;
        while (!trailingExhausted &&
               LeadingSearcher.TryFindNextOverlappingMatch(input, ref leadingState, out var leading))
        {
            if (TryFindTrailingAnchor(
                    input,
                    leading,
                    ref trailingState,
                    ref trailing,
                    ref hasTrailing,
                    ref trailingExhausted))
            {
                state = new PreparedWindowScanState(
                    leading.Index + 1,
                    leadingState,
                    trailingState,
                    trailing,
                    hasTrailing,
                    trailingExhausted);
                window = new PreparedWindowMatch(leading, trailing);
                return true;
            }
        }

        state = new PreparedWindowScanState(
            input.Length,
            leadingState,
            trailingState,
            trailing,
            hasTrailing,
            trailingExhausted);
        return false;
    }

    private bool TryFindTrailingAnchor(
        ReadOnlySpan<byte> input,
        PreparedSearchMatch leading,
        ref PreparedSearchScanState trailingState,
        ref PreparedSearchMatch trailing,
        ref bool hasTrailing,
        ref bool trailingExhausted)
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
                trailingExhausted = true;
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
    PreparedSearchScanState TrailingState,
    PreparedSearchMatch TrailingMatch,
    bool HasTrailingMatch,
    bool TrailingExhausted)
{
    public PreparedWindowScanState(int nextStart, PreparedSearchScanState leadingState)
        : this(nextStart, leadingState, default, default, HasTrailingMatch: false, TrailingExhausted: false)
    {
    }

    public static PreparedWindowScanState Create(int startIndex) =>
        new(
            startIndex,
            new PreparedSearchScanState(startIndex, default),
            default,
            default,
            HasTrailingMatch: false,
            TrailingExhausted: false);
}
