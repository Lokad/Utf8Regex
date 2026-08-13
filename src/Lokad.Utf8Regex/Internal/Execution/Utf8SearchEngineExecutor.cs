using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Utilities;
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

    public static bool TryFindFirst(Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget = null)
    {
        var primaryExecutionEngine = GetPrimaryExecutionEngine(regexPlan);
        return primaryExecutionEngine.Kind switch
        {
            Utf8CandidateSearchKind.StructuralIdentifierFamily
                => Utf8BackendInstructionExecutor.IsMatchStructuralIdentifierFamily(regexPlan, verifierRuntime, input, budget),
            Utf8CandidateSearchKind.OrderedLiteralWindow
                => Utf8BackendInstructionExecutor.IsMatchOrderedLiteralWindow(regexPlan, input, budget),
            Utf8CandidateSearchKind.StructuralDeterministicAutomaton
                => Utf8StructuralLinearRuntime.Create(regexPlan.StructuralLinearProgram).IsMatch(input, Utf8InputAnalyzer.ValidateOnly(input), verifierRuntime, budget),
            _ => TryFindFirst(primaryExecutionEngine, input),
        };
    }

    public static Utf8CandidateSearchPlan GetPrimaryExecutionEngine(Utf8PreparedRegex regexPlan)
    {
        return Utf8CompiledEngineSelector.Select(regexPlan).Kind switch
        {
            Utf8CompiledEngineKind.LiteralFamily or Utf8CompiledEngineKind.ExactLiteral
                => regexPlan.SearchPlan.NativeCandidateSource,
            Utf8CompiledEngineKind.SearchGuidedFallback
                => regexPlan.SearchPlan.FallbackCandidateSource,
            Utf8CompiledEngineKind.StructuralFamily
                => Utf8CandidateSearchPlan.ForExecutionFamily(Utf8CandidateSearchKind.StructuralIdentifierFamily),
            Utf8CompiledEngineKind.StructuralLinearAutomaton when regexPlan.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
                => Utf8CandidateSearchPlan.ForExecutionFamily(Utf8CandidateSearchKind.OrderedLiteralWindow),
            Utf8CompiledEngineKind.StructuralLinearAutomaton
                => Utf8CandidateSearchPlan.ForExecutionFamily(Utf8CandidateSearchKind.StructuralDeterministicAutomaton),
            _ => default,
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
