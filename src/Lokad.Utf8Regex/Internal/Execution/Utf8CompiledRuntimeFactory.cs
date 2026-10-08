using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledRuntimeFactory
{
    public static Utf8CompiledEngineRuntime Create(
        Utf8CompiledEngine compiledEngine,
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        RegexOptions options)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            if ((compiledEngine.Kind == Utf8CompiledEngineKind.ByteSafeLinear ||
                 compiledEngine.Kind == Utf8CompiledEngineKind.CompiledFallback) &&
                ShouldPreferFallbackRegexRuntime(compiledEngine.Kind, regexPlan, budget))
            {
                compiledEngine = new Utf8CompiledEngine(Utf8CompiledEngineKind.FallbackRegex);
            }

            Utf8CompiledEngineRuntime result;
            if (compiledEngine.Kind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily)
            {
                var inner = Utf8LiteralCompiledEngineRuntime.Create(compiledEngine, regexPlan,
                    (options & RegexOptions.RightToLeft) != 0);
                result = compiledEngine.Kind == Utf8CompiledEngineKind.ExactLiteral
                    ? budget.AllocateObject(inner, static value => new Utf8ExactLiteralCompiledEngineRuntime(value))
                    : budget.AllocateObject(inner, static value => new Utf8LiteralFamilyCompiledEngineRuntime(value));
            }
            else
            {
                // Build the selected runtime directly. No temporary implementation
                // is needed to transfer borrowed plan and verifier references.
                result = compiledEngine.Kind switch
                {
                    Utf8CompiledEngineKind.StructuralFamily
                        => Utf8StructuralFamilyCompiledEngineRuntime.Create(regexPlan, verifierRuntime),
                    Utf8CompiledEngineKind.EmittedKernel
                        => Utf8StructuralLinearAutomatonCompiledEngineRuntime.Create(regexPlan, verifierRuntime, true),
                    Utf8CompiledEngineKind.StructuralLinearAutomaton
                        => Utf8StructuralLinearAutomatonCompiledEngineRuntime.Create(regexPlan, verifierRuntime, (options & RegexOptions.Compiled) != 0),
                    Utf8CompiledEngineKind.SimplePatternInterpreter
                        => Utf8SimplePatternCompiledEngineRuntime.Create(regexPlan, verifierRuntime, (options & RegexOptions.Compiled) != 0),
                    Utf8CompiledEngineKind.ByteSafeLinear
                        => Utf8ByteSafeLinearCompiledEngineRuntime.Create(regexPlan, verifierRuntime),
                    Utf8CompiledEngineKind.CompiledFallback
                        => Utf8CompiledFallbackCompiledEngineRuntime.Create(regexPlan, verifierRuntime),
                    Utf8CompiledEngineKind.SearchGuidedFallback
                        => Utf8SearchGuidedFallbackCompiledEngineRuntime.Create(compiledEngine, regexPlan, verifierRuntime),
                    _ => Utf8FallbackRegexCompiledEngineRuntime.Create(regexPlan, verifierRuntime),
                };
            }
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    public static RegexOptions NormalizeDirectRouteOptions(RegexOptions options) => options & ~RegexOptions.Compiled;

    private static bool ShouldPreferFallbackRegexRuntime(
        Utf8CompiledEngineKind engineKind,
        Utf8PreparedRegex regexPlan,
        Utf8ConstructionBudget budget)
    {
        if (regexPlan.ExecutionKind != NativeExecutionKind.FallbackRegex ||
            !string.Equals(regexPlan.FallbackReason, "unsupported_loop", StringComparison.Ordinal))
        {
            return false;
        }

        return HasWeakFallbackPrefilter(regexPlan) ||
            engineKind == Utf8CompiledEngineKind.CompiledFallback &&
            HasVariableWidthFixedDistanceCandidateOutsidePrefix(regexPlan, budget);
    }

    private static bool HasWeakFallbackPrefilter(Utf8PreparedRegex regexPlan)
    {
        return regexPlan.SearchPlan.LiteralUtf8 is { Length: > 0 and <= 3 } &&
            regexPlan.SearchPlan.FallbackCandidatePlans is { Length: 1 } &&
            !regexPlan.SearchPlan.HasWindowSearch &&
            regexPlan.SearchPlan.RequiredWindowPrefilterPlans is not { Length: > 0 };
    }

    private static bool HasVariableWidthFixedDistanceCandidateOutsidePrefix(Utf8PreparedRegex regexPlan, Utf8ConstructionBudget budget)
    {
        var searchPlan = regexPlan.SearchPlan;
        if (searchPlan.Kind is not (Utf8SearchKind.FixedDistanceAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiChar) ||
            searchPlan.ExactRequiredLength.HasValue ||
            searchPlan.LiteralUtf8 is not { Length: > 0 } literal)
        {
            return false;
        }

        var prefixGuards = regexPlan.DeterministicGuards.PrefixGuards;
        if (prefixGuards is not { Length: > 0 })
        {
            return true;
        }

        for (var i = 0; i < literal.Length; i++)
        {
            budget.ChargeWork(1);
            var offset = searchPlan.Distance + i;
            var covered = false;
            foreach (var guard in prefixGuards)
            {
                budget.ChargeWork(1);
                if (guard.Offset == offset && guard.Literal == literal[i])
                {
                    covered = true;
                    break;
                }
            }

            if (!covered)
            {
                return true;
            }
        }

        return false;
    }
}
