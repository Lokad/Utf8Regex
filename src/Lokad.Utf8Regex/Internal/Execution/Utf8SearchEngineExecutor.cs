using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchEngineExecutor
{
    public static bool TryFindFirst(Utf8SearchEnginePlan engine, ReadOnlySpan<byte> input)
    {
        return engine.Kind switch
        {
            Utf8SearchEngineKind.PreparedSearcher => engine.PreparedSearcher.HasValue && engine.PreparedSearcher.FindFirst(input) >= 0,
            Utf8SearchEngineKind.StructuralSearch => TryFindFirst(engine.StructuralSearchPlan, input),
            Utf8SearchEngineKind.StructuralSearchSet => TryFindFirst(engine.StructuralSearchPlans, input),
            _ => false,
        };
    }

    public static bool TryFindFirst(Utf8RegexPlan regexPlan, Utf8VerifierRuntime verifierRuntime, ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget = null)
    {
        return regexPlan.PrimaryExecutionEngine.Kind switch
        {
            Utf8SearchEngineKind.StructuralIdentifierFamily
                => Utf8BackendInstructionExecutor.IsMatchStructuralIdentifierFamily(regexPlan, verifierRuntime, input, budget),
            Utf8SearchEngineKind.OrderedLiteralWindow
                => Utf8BackendInstructionExecutor.IsMatchOrderedLiteralWindow(regexPlan, input, budget),
            Utf8SearchEngineKind.StructuralDeterministicAutomaton
                => Utf8StructuralLinearRuntime.Create(regexPlan.StructuralLinearProgram).IsMatch(input, Utf8InputAnalyzer.ValidateOnly(input), verifierRuntime, budget),
            _ => TryFindFirst(regexPlan.PrimaryExecutionEngine, input),
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
            new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));
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
