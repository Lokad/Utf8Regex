namespace Lokad.Utf8Regex;

public readonly record struct Utf8CountDiagnostics(
    int Result,
    string ExecutionKind,
    string SearchKind,
    string FallbackVerifierMode,
    bool RequiresCandidateEndCoverage,
    bool RequiresTrailingAnchorCoverage,
    string ExecutionRoute,
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
