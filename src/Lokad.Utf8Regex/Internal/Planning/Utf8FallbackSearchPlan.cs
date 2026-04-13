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

    public Utf8SearchEnginePlan CandidateEngine =>
        CandidatePlans is { Length: > 0 } candidatePlans
            ? new Utf8SearchEnginePlan(
                Utf8SearchEngineKind.StructuralSearchSet,
                Utf8SearchSemantics.CandidateScan with { RequiresConfirmation = true },
                structuralSearchPlans: candidatePlans)
            : default;

    public bool HasRequiredPrefilter => PrefilterPlan.HasValue;

    public bool HasCandidates => CandidatePlans is { Length: > 0 };

    public bool RejectsByRequiredPrefilter(ReadOnlySpan<byte> input)
    {
        return PrefilterPlan.Rejects(input);
    }
}
