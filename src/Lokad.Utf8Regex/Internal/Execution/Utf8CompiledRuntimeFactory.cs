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
                    _ => Utf8FallbackRegexCompiledEngineRuntime.Create(regexPlan, verifierRuntime),
                };
            }
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
