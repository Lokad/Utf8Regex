namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Coverage required when a search plan supplies a conservative scalar candidate.</summary>
internal readonly struct Utf8ScalarVerifierPlan
{
    public Utf8ScalarVerifierPlan(bool requiresCandidateEndCoverage, bool requiresTrailingAnchorCoverage)
    {
        RequiresCandidateEndCoverage = requiresCandidateEndCoverage;
        RequiresTrailingAnchorCoverage = requiresTrailingAnchorCoverage;
    }

    public bool RequiresCandidateEndCoverage { get; }

    public bool RequiresTrailingAnchorCoverage { get; }
}
