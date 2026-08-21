namespace Lokad.Utf8Regex;

/// <summary>Reports the result, selected execution route, and search counters for a diagnostic match probe.</summary>
public readonly record struct Utf8IsMatchDiagnostics(
    bool Result,
    string ExecutionKind,
    string SearchKind,
    string FallbackVerifierMode,
    bool RequiresCandidateEndCoverage,
    bool RequiresTrailingAnchorCoverage,
    int SearchCandidates,
    int FixedCheckRejects,
    int VerifierInvocations,
    int VerifierMatches,
    int PrefilterWindows,
    int PrefilterSkippedWindows,
    int PrefilterPromotedWindows,
    int PrefilterSkippedBytes,
    int PrefilterPromotedBytes,
    int EngineDemotions);
