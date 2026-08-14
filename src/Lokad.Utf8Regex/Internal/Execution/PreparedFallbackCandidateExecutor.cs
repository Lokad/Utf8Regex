using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class PreparedFallbackCandidateExecutor
{
    public static bool TryFindNextCandidate(
        this PreparedFallbackCandidateSource source,
        ReadOnlySpan<byte> input,
        ref PreparedFallbackCandidateState state,
        out PreparedFallbackCandidate candidate)
    {
        candidate = default;

        switch (source.Kind)
        {
            case PreparedFallbackCandidateKind.Start:
                var searchState = state.SearchState;
                while (source.Searcher.TryFindNextOverlappingMatch(input, ref searchState, out var match))
                {
                    var startIndex = source.StartTransform.Apply(input, match.Index);
                    if (startIndex < 0)
                    {
                        continue;
                    }

                    state = new PreparedFallbackCandidateState(searchState, default);
                    candidate = PreparedFallbackCandidate.AtStart(startIndex);
                    return true;
                }

                state = new PreparedFallbackCandidateState(searchState, default);
                return false;

            case PreparedFallbackCandidateKind.Window:
                var windowState = state.WindowState;
                while (source.WindowSearch.TryFindNextWindow(input, ref windowState, out var window))
                {
                    if (!SatisfiesWindowConstraints(input, source.WindowSearch, window))
                    {
                        continue;
                    }

                    state = new PreparedFallbackCandidateState(default, windowState);
                    candidate = PreparedFallbackCandidate.ForWindow(
                        window.Leading.Index,
                        window.Trailing.Index + window.Trailing.Length);
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
