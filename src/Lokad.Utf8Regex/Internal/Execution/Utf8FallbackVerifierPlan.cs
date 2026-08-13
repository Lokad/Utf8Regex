namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8FallbackVerifierMode : byte
{
    DirectRegex = 0,
    AnchoredRegex = 1,
    AnchoredSliceRegex = 2,
}

internal readonly struct Utf8FallbackVerifierPlan
{
    public Utf8FallbackVerifierPlan(
        Utf8FallbackVerifierMode mode,
        bool requiresCandidateEndCoverage,
        bool requiresTrailingAnchorCoverage)
    {
        Mode = mode;
        RequiresCandidateEndCoverage = requiresCandidateEndCoverage;
        RequiresTrailingAnchorCoverage = requiresTrailingAnchorCoverage;
    }

    public Utf8FallbackVerifierMode Mode { get; }

    public bool RequiresCandidateEndCoverage { get; }

    public bool RequiresTrailingAnchorCoverage { get; }

}

internal static class Utf8FallbackVerifierRuntimeFactory
{
    public static Utf8FallbackCandidateVerifier Create(
        Utf8FallbackVerifierPlan plan,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        var fallbackRegex = new Regex(pattern, options, matchTimeout);
        var anchoredFallbackRegex = plan.Mode is Utf8FallbackVerifierMode.AnchoredRegex or Utf8FallbackVerifierMode.AnchoredSliceRegex
            ? new Regex(@"\G(?:" + pattern + ")", options, matchTimeout)
            : null;
        return plan.Mode switch
        {
            Utf8FallbackVerifierMode.AnchoredSliceRegex when anchoredFallbackRegex is not null
                => new Utf8BoundedSliceFallbackCandidateVerifier(plan, fallbackRegex, anchoredFallbackRegex),
            _ => new Utf8StartFallbackCandidateVerifier(plan, fallbackRegex, anchoredFallbackRegex),
        };
    }
}
