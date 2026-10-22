using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchEngineExecutor
{
    public static bool TryFindFirst(Utf8CandidateSearchPlan engine, ReadOnlySpan<byte> input)
    {
        return engine.Kind switch
        {
            Utf8CandidateSearchKind.PreparedSearcher => engine.PreparedSearcher.HasValue && engine.PreparedSearcher.FindFirst(input) >= 0,
            Utf8CandidateSearchKind.StructuralSearch => TryFindFirst(engine.StructuralSearchPlan, input),
            Utf8CandidateSearchKind.StructuralSearchSet => TryFindFirst(engine.StructuralSearchPlans, input),
            _ => false,
        };
    }

    private static bool TryFindFirst(Utf8StructuralSearchPlan plan, ReadOnlySpan<byte> input)
    {
        if (!plan.HasValue)
        {
            return false;
        }

        var state = new Utf8StructuralSearchState(
            default,
            PreparedWindowScanState.Create(0));
        return plan.TryFindNextCandidate(input, ref state, out _);
    }

    private static bool TryFindFirst(Utf8StructuralSearchPlan[]? plans, ReadOnlySpan<byte> input)
    {
        if (plans is not { Length: > 0 })
        {
            return false;
        }

        foreach (var plan in plans)
        {
            if (TryFindFirst(plan, input))
            {
                return true;
            }
        }

        return false;
    }
}
