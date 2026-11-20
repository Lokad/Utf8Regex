using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8PrefilterReplay
{
    public static bool Rejects(in Utf8SearchPlan plan, ReadOnlySpan<byte> input)
    {
        ref readonly var searcher = ref plan.RequiredPrefilterSearcher;
        return searcher.HasValue && searcher.FindFirst(input) < 0;
    }
}
