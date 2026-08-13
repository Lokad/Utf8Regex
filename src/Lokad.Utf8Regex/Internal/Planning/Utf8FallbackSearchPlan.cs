using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.Planning;

internal readonly struct Utf8FallbackSearchPlan
{
    public Utf8FallbackSearchPlan(
        Utf8PrefilterPlan prefilterPlan,
        Utf8StructuralSearchPlan[]? candidatePlans)
    {
        PrefilterPlan = prefilterPlan;
        CandidatePlans = candidatePlans;
    }

    public Utf8PrefilterPlan PrefilterPlan { get; }

    public PreparedSearcher RequiredPrefilterSearcher => PrefilterPlan.PrimarySearcher;

    public PreparedSearcher SecondaryRequiredPrefilterSearcher => PrefilterPlan.SecondarySearcher;

    public Utf8StructuralSearchPlan[]? RequiredWindowPrefilterPlans => PrefilterPlan.WindowPlans;

    public Utf8StructuralSearchPlan[]? CandidatePlans { get; }

    public Utf8CandidateSearchPlan CandidateSource =>
        Utf8CandidateSearchPlan.FromStructuralSearchSet(
            CandidatePlans,
            Utf8SearchSemantics.CandidateScan with { RequiresConfirmation = true });

    public bool HasRequiredPrefilter => PrefilterPlan.HasValue;

    public bool HasCandidates => CandidatePlans is { Length: > 0 };

}
