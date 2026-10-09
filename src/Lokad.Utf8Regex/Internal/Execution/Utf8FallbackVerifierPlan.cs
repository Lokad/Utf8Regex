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
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var source = RegexFactory.Create(pattern, options, matchTimeout, budget);
            var fallbackRegex = new Lazy<Regex>(source.CreateRoot);
            Lazy<Regex>? anchoredFallbackRegex = plan.Mode is Utf8FallbackVerifierMode.AnchoredRegex or Utf8FallbackVerifierMode.AnchoredSliceRegex
                ? new Lazy<Regex>(source.CreateAnchored)
                : null;
            var result = budget.AllocateObject<Utf8FallbackCandidateVerifier.ConstructionState, Utf8FallbackCandidateVerifier>(
                new(plan, fallbackRegex, anchoredFallbackRegex, options), static state => state.Plan.Mode switch
                {
                    Utf8FallbackVerifierMode.AnchoredSliceRegex when state.AnchoredFallbackRegex is not null
                        => new Utf8BoundedSliceFallbackCandidateVerifier(state),
                    _ => new Utf8StartFallbackCandidateVerifier(state),
                });
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    // Admit the callback's retained inputs explicitly, after the factory depth
    // guard. Lazy/delegate and BCL engine payloads remain separate migration debt.
    private sealed class RegexFactory
    {
        private readonly record struct ConstructionState(string Pattern, RegexOptions Options, TimeSpan MatchTimeout);
        private readonly string _pattern;
        private readonly RegexOptions _options;
        private readonly TimeSpan _matchTimeout;

        private RegexFactory(ConstructionState state)
        {
            _pattern = state.Pattern;
            _options = state.Options;
            _matchTimeout = state.MatchTimeout;
        }

        internal static RegexFactory Create(string pattern, RegexOptions options, TimeSpan matchTimeout, Utf8ConstructionBudget budget)
            => budget.AllocateObject(new ConstructionState(pattern, options, matchTimeout), static state => new RegexFactory(state));

        internal Regex CreateRoot() => new Regex(_pattern, _options, _matchTimeout);
        internal Regex CreateAnchored() => new Regex(@"\G(?:" + _pattern + ")", _options, _matchTimeout);
    }
}
