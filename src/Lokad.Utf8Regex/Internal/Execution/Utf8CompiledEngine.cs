using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8CompiledEngineKind : byte
{
    FallbackRegex = 0, ExactLiteral = 3, LiteralFamily = 4
}

internal static class Utf8CompiledEngineSelector
{
    public static Utf8CompiledEngineKind Select(Utf8PreparedRegex plan)
    {
        ref readonly var searchPlan = ref plan.SearchPlan;
        var single = plan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase;
        // Empty literals have no search candidate; the scalar cursor owns progress.
        if (single && plan.LiteralUtf8 is { Length: 0 } && !searchPlan.HasBoundaryRequirements &&
            !searchPlan.HasTrailingLiteralRequirement)
            return Utf8CompiledEngineKind.ExactLiteral;
        if (!searchPlan.HasPreparedSearcher)
            return Utf8CompiledEngineKind.FallbackRegex;
        if (single)
            return searchPlan.PortfolioKind is Utf8SearchPortfolioKind.ExactLiteral or Utf8SearchPortfolioKind.IgnoreCaseLiteral
                ? Utf8CompiledEngineKind.ExactLiteral : Utf8CompiledEngineKind.FallbackRegex;
        return (plan.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) &&
            (searchPlan.PortfolioKind is Utf8SearchPortfolioKind.ExactDirectFamily or Utf8SearchPortfolioKind.ExactTrieFamily or
                Utf8SearchPortfolioKind.ExactAutomatonFamily or Utf8SearchPortfolioKind.ExactPackedFamily or
                Utf8SearchPortfolioKind.ExactEarliestFamily or Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily)
            ? Utf8CompiledEngineKind.LiteralFamily : Utf8CompiledEngineKind.FallbackRegex;
    }
}
