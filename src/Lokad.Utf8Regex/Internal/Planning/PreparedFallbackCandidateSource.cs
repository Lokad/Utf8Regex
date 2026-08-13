using Lokad.Utf8Regex.Internal.Execution;
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

}

internal readonly record struct PreparedFallbackCandidate(int StartIndex, int EndIndex = -1);

internal readonly record struct PreparedFallbackCandidateState(PreparedSearchScanState SearchState, PreparedWindowScanState WindowState);
