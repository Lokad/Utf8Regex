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

        if (plan.PrimaryEngine.HasValue &&
            !Utf8SearchEngineExecutor.TryFindFirst(plan.PrimaryEngine, input))
        {
            return true;
        }

        if (plan.SecondaryEngine.HasValue &&
            !Utf8SearchEngineExecutor.TryFindFirst(plan.SecondaryEngine, input))
        {
            return true;
        }

        return plan.WindowEngine.HasValue &&
            !Utf8SearchEngineExecutor.TryFindFirst(plan.WindowEngine, input);
    }
}
