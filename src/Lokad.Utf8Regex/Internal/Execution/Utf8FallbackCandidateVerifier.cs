using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8FallbackCandidateVerifier
{
    // R03: BCL engines construct lazily so owned routes never construct them; the first
    // fallback use pays the construction cost once (thread-safe).
    internal readonly record struct ConstructionState(
        Utf8FallbackVerifierPlan Plan, Lazy<Regex> FallbackRegex,
        Lazy<Regex>? AnchoredFallbackRegex, RegexOptions FallbackOptions);

    internal Utf8FallbackCandidateVerifier(ConstructionState state)
    {
        Plan = state.Plan;
        FallbackOptions = state.FallbackOptions;
        _fallbackRegex = state.FallbackRegex;
        _anchoredFallbackRegex = state.AnchoredFallbackRegex;
    }

    public Utf8FallbackVerifierPlan Plan { get; }

    /// <summary>Gets the options the lazily constructed fallback engines use, so routing
    /// decisions can read them without constructing anything.</summary>
    public RegexOptions FallbackOptions { get; }

    private readonly Lazy<Regex> _fallbackRegex;

    private readonly Lazy<Regex>? _anchoredFallbackRegex;

    public Regex FallbackRegex => _fallbackRegex.Value;

    public Regex? AnchoredFallbackRegex => _anchoredFallbackRegex?.Value;

    public bool IsFallbackRegexCreated => _fallbackRegex.IsValueCreated;

    public bool IsAnchoredFallbackRegexCreated => _anchoredFallbackRegex?.IsValueCreated ?? false;

    internal int[] GetGroupNumbers() => FallbackRegex.GetGroupNumbers();

    internal string[] GetGroupNames() => FallbackRegex.GetGroupNames();

}
