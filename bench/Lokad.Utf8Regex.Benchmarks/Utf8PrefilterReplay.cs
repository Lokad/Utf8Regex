using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Benchmarks;

// Replays the prepared filters in isolation for attribution and diagnostics.
internal static class Utf8PrefilterReplay
{
    public static bool Rejects(in Utf8PrefilterPlan plan, ReadOnlySpan<byte> input)
    {
        ref readonly var primary = ref plan.PrimarySearcher;
        if (primary.HasValue && primary.FindFirst(input) < 0)
        {
            return true;
        }

        var secondary = plan.SecondarySearcher;
        if (secondary.HasValue && secondary.FindFirst(input) < 0)
        {
            return true;
        }

        if (plan.WindowPlans is not { Length: > 0 } windows)
        {
            return false;
        }

        foreach (var window in windows)
        {
            var state = new Utf8StructuralSearchState(default, PreparedWindowScanState.Create(0));
            if (window.TryFindNextCandidate(input, ref state, out _))
            {
                return false;
            }
        }

        return true;
    }
}
