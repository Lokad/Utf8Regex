using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledRuntimeFactory
{
    // One conservative runtime-object allowance; major referents retain their
    // independent admission. No mirrored field-transfer descriptor is needed.
    internal const ulong FixedRuntimeMetadataBytes = 2 * 1024;

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
                budget.CheckBytes(FixedRuntimeMetadataBytes);
                budget.ChargeWork(2);
                budget.ReserveBytes(FixedRuntimeMetadataBytes);
                result = compiledEngine.Kind switch
                {
                    Utf8CompiledEngineKind.StructuralFamily
                        => new Utf8StructuralFamilyCompiledEngineRuntime(regexPlan, verifierRuntime),
                    Utf8CompiledEngineKind.EmittedKernel
                        => new Utf8StructuralLinearAutomatonCompiledEngineRuntime(regexPlan, verifierRuntime, true),
                    Utf8CompiledEngineKind.StructuralLinearAutomaton
                        => new Utf8StructuralLinearAutomatonCompiledEngineRuntime(regexPlan, verifierRuntime, (options & RegexOptions.Compiled) != 0),
                    Utf8CompiledEngineKind.SimplePatternInterpreter
                        => new Utf8SimplePatternCompiledEngineRuntime(regexPlan, verifierRuntime, (options & RegexOptions.Compiled) != 0),
                    _ => new Utf8FallbackRegexCompiledEngineRuntime(regexPlan),
                };
            }
            budget.ChargeWork(0); // Original interruption authority before publication.
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
