using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledRuntimeFactory
{
    public static Utf8CompiledEngineRuntime Create(
        Utf8CompiledEngine compiledEngine, Utf8PreparedRegex regexPlan, RegexOptions options)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            if (compiledEngine.Kind is not (Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily))
                throw new ArgumentOutOfRangeException(nameof(compiledEngine), "Only literal engines have a separate runtime.");
            var result = Utf8LiteralCompiledEngineRuntime.Create(compiledEngine, regexPlan,
                (options & RegexOptions.RightToLeft) != 0);
            budget.ChargeWork(0); // Original interruption authority before publication.
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
