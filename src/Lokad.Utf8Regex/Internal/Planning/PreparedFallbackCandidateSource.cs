using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Planning;

internal enum PreparedFallbackCandidateKind : byte
{
    None = 0,
    Start = 1,
    Window = 2,
}

internal readonly struct PreparedFallbackCandidateSource
{
    public PreparedFallbackCandidateSource(PreparedSearcher searcher, Utf8FallbackStartTransform startTransform = default)
    {
        Searcher = searcher;
        StartTransform = startTransform;
        WindowSearch = default;
        Kind = searcher.HasValue ? PreparedFallbackCandidateKind.Start : PreparedFallbackCandidateKind.None;
    }

    public PreparedFallbackCandidateSource(PreparedWindowSearch windowSearch)
    {
        Searcher = default;
        StartTransform = default;
        WindowSearch = windowSearch;
        Kind = windowSearch.HasValue ? PreparedFallbackCandidateKind.Window : PreparedFallbackCandidateKind.None;
    }

    public PreparedFallbackCandidateKind Kind { get; }

    public PreparedSearcher Searcher { get; }

    public Utf8FallbackStartTransform StartTransform { get; }

    public PreparedWindowSearch WindowSearch { get; }

    public bool HasValue => Kind != PreparedFallbackCandidateKind.None;

    public bool TryFindNextCandidate(
        ReadOnlySpan<byte> input,
        ref PreparedFallbackCandidateState state,
        out PreparedFallbackCandidate candidate)
    {
        candidate = default;

        switch (Kind)
        {
            case PreparedFallbackCandidateKind.Start:
                var searchState = state.SearchState;
                while (Searcher.TryFindNextOverlappingMatch(input, ref searchState, out var match))
                {
                    var startIndex = StartTransform.Apply(input, match.Index);
                    if (startIndex < 0)
                    {
                        continue;
                    }

                    state = new PreparedFallbackCandidateState(searchState, default);
                    candidate = new PreparedFallbackCandidate(startIndex);
                    return true;
                }

                state = new PreparedFallbackCandidateState(searchState, default);
                return false;

            case PreparedFallbackCandidateKind.Window:
                var windowState = state.WindowState;
                while (WindowSearch.TryFindNextWindow(input, ref windowState, out var window))
                {
                    if (!SatisfiesWindowConstraints(input, WindowSearch, window))
                    {
                        continue;
                    }

                    state = new PreparedFallbackCandidateState(default, windowState);
                    candidate = new PreparedFallbackCandidate(window.Leading.Index, window.Trailing.Index + window.Trailing.Length);
                    return true;
                }

                state = new PreparedFallbackCandidateState(default, windowState);
                return false;

            default:
                return false;
        }
    }

    private static bool SatisfiesWindowConstraints(ReadOnlySpan<byte> input, PreparedWindowSearch windowSearch, PreparedWindowMatch window)
    {
        if (windowSearch.MaxGap is int maxGap &&
            window.Trailing.Index + window.Trailing.Length - window.Leading.Index > maxGap)
        {
            return false;
        }

        if (!windowSearch.SameLine)
        {
            return true;
        }

        return input[window.Leading.Index..window.Trailing.Index].IndexOf((byte)'\n') < 0 &&
            input[window.Leading.Index..window.Trailing.Index].IndexOf((byte)'\r') < 0;
    }
}

internal readonly record struct PreparedFallbackCandidate(int StartIndex, int EndIndex = -1);

internal readonly record struct PreparedFallbackCandidateState(PreparedSearchScanState SearchState, PreparedWindowScanState WindowState);
