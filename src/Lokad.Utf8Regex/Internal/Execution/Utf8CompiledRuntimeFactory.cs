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
        if ((compiledEngine.Kind == Utf8CompiledEngineKind.ByteSafeLinear ||
             compiledEngine.Kind == Utf8CompiledEngineKind.CompiledFallback) &&
            ShouldPreferFallbackRegexRuntime(compiledEngine.Kind, regexPlan))
        {
            compiledEngine = new Utf8CompiledEngine(Utf8CompiledEngineKind.FallbackRegex);
        }

        var literalRuntime = new Utf8LiteralCompiledEngineRuntime(compiledEngine, regexPlan, (options & RegexOptions.RightToLeft) != 0);
        var nonLiteralRuntime = new Utf8NonLiteralCompiledEngineRuntime(compiledEngine, regexPlan, verifierRuntime);
        return compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral
                => new Utf8ExactLiteralCompiledEngineRuntime(literalRuntime),
            Utf8CompiledEngineKind.LiteralFamily
                => new Utf8LiteralFamilyCompiledEngineRuntime(literalRuntime),
            Utf8CompiledEngineKind.StructuralFamily
                => new Utf8StructuralFamilyCompiledEngineRuntime(nonLiteralRuntime),
            Utf8CompiledEngineKind.EmittedKernel
                => new Utf8StructuralLinearAutomatonCompiledEngineRuntime(nonLiteralRuntime, emitEnabled: true),
            Utf8CompiledEngineKind.StructuralLinearAutomaton
                => new Utf8StructuralLinearAutomatonCompiledEngineRuntime(nonLiteralRuntime, (options & RegexOptions.Compiled) != 0),
            Utf8CompiledEngineKind.SimplePatternInterpreter
                => new Utf8SimplePatternCompiledEngineRuntime(nonLiteralRuntime, (options & RegexOptions.Compiled) != 0),
            Utf8CompiledEngineKind.ByteSafeLinear
                => new Utf8ByteSafeLinearCompiledEngineRuntime(nonLiteralRuntime),
            Utf8CompiledEngineKind.CompiledFallback
                => new Utf8CompiledFallbackCompiledEngineRuntime(nonLiteralRuntime),
            Utf8CompiledEngineKind.SearchGuidedFallback
                => new Utf8SearchGuidedFallbackCompiledEngineRuntime(compiledEngine, nonLiteralRuntime),
            _ => new Utf8FallbackRegexCompiledEngineRuntime(nonLiteralRuntime),
        };
    }

    public static RegexOptions NormalizeDirectRouteOptions(RegexOptions options) => options & ~RegexOptions.Compiled;

    private static bool ShouldPreferFallbackRegexRuntime(
        Utf8CompiledEngineKind engineKind,
        Utf8PreparedRegex regexPlan)
    {
        if (regexPlan.ExecutionKind != NativeExecutionKind.FallbackRegex ||
            !string.Equals(regexPlan.FallbackReason, "unsupported_loop", StringComparison.Ordinal))
        {
            return false;
        }

        return HasWeakFallbackPrefilter(regexPlan) ||
            engineKind == Utf8CompiledEngineKind.CompiledFallback &&
            HasVariableWidthFixedDistanceCandidateOutsidePrefix(regexPlan);
    }

    private static bool HasWeakFallbackPrefilter(Utf8PreparedRegex regexPlan)
    {
        return regexPlan.SearchPlan.LiteralUtf8 is { Length: > 0 and <= 3 } &&
            regexPlan.SearchPlan.FallbackCandidatePlans is { Length: 1 } &&
            !regexPlan.SearchPlan.HasWindowSearch &&
            regexPlan.SearchPlan.RequiredWindowPrefilterPlans is not { Length: > 0 };
    }

    private static bool HasVariableWidthFixedDistanceCandidateOutsidePrefix(Utf8PreparedRegex regexPlan)
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
            var offset = searchPlan.Distance + i;
            var covered = false;
            foreach (var guard in prefixGuards)
            {
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
