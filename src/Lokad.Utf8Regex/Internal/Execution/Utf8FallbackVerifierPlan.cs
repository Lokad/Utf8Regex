using Lokad.Utf8Regex.Internal.Planning;

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

    public static Utf8FallbackVerifierPlan Create(
        string pattern,
        RegexOptions options,
        Utf8StructuralSearchPlan structuralSearchPlan)
    {
        var usesBoundedCandidateSlice = structuralSearchPlan.ProducesBoundedCandidates;
        var requiresCandidateEndCoverage = structuralSearchPlan.RequiresCandidateEndCoverage;
        var requiresTrailingAnchorCoverage = structuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Window;
        var mode = usesBoundedCandidateSlice && CanUseAnchoredRegex(pattern, options)
            ? Utf8FallbackVerifierMode.AnchoredSliceRegex
            : CanUseAnchoredRegex(pattern, options)
                ? Utf8FallbackVerifierMode.AnchoredRegex
                : Utf8FallbackVerifierMode.DirectRegex;
        return new Utf8FallbackVerifierPlan(
            mode,
            requiresCandidateEndCoverage,
            requiresTrailingAnchorCoverage);
    }

    public Utf8FallbackCandidateVerifier CreateRuntime(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        var fallbackRegex = new Regex(pattern, options, matchTimeout);
        var anchoredFallbackRegex = Mode is Utf8FallbackVerifierMode.AnchoredRegex or Utf8FallbackVerifierMode.AnchoredSliceRegex
            ? new Regex(@"\G(?:" + pattern + ")", options, matchTimeout)
            : null;
        return Mode switch
        {
            Utf8FallbackVerifierMode.AnchoredSliceRegex when anchoredFallbackRegex is not null
                => new Utf8BoundedSliceFallbackCandidateVerifier(this, fallbackRegex, anchoredFallbackRegex),
            _ => new Utf8StartFallbackCandidateVerifier(this, fallbackRegex, anchoredFallbackRegex),
        };
    }

    private static bool CanUseAnchoredRegex(string pattern, RegexOptions options)
    {
        if ((options & (RegexOptions.RightToLeft | RegexOptions.NonBacktracking)) != 0)
        {
            return false;
        }

        // Wrapping the pattern changes semantics for leading global inline constructs.
        return !pattern.StartsWith("(?", StringComparison.Ordinal);
    }
}
