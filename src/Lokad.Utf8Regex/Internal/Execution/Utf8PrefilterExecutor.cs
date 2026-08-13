using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8PrefilterExecutor
{
    public static bool Rejects(Utf8PrefilterPlan plan, ReadOnlySpan<byte> input)
    {
        if (!plan.HasValue)
        {
            return false;
        }

        if (plan.PrimarySource.HasValue &&
            !Utf8SearchEngineExecutor.TryFindFirst(plan.PrimarySource, input))
        {
            return true;
        }

        if (plan.SecondarySource.HasValue &&
            !Utf8SearchEngineExecutor.TryFindFirst(plan.SecondarySource, input))
        {
            return true;
        }

        return plan.WindowSource.HasValue &&
            !Utf8SearchEngineExecutor.TryFindFirst(plan.WindowSource, input);
    }
}
