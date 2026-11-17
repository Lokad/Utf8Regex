using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8CompiledEngineKind : byte
{
    FallbackRegex = 0, ExactLiteral = 3, LiteralFamily = 4
}

internal enum Utf8CompiledExecutionBackend : byte
{
    Legacy = 0, InterpretedInstruction = 1, EmittedInstruction = 2
}

internal readonly struct Utf8CompiledEngine
{
    public Utf8CompiledEngine(Utf8CompiledEngineKind kind)
        : this(kind, Utf8CompiledExecutionBackend.Legacy)
    {
    }

    public Utf8CompiledEngine(Utf8CompiledEngineKind kind, Utf8CompiledExecutionBackend backend)
    {
        Kind = kind;
        Backend = backend;
    }

    public Utf8CompiledEngineKind Kind { get; }

    public Utf8CompiledExecutionBackend Backend { get; }

}

internal static class Utf8CompiledEngineSelector
{
    public static Utf8CompiledEngine Select(Utf8PreparedRegex regexPlan)
    {
        var count = regexPlan.SearchPlan.CountOperation;
        if (IsLiteral(regexPlan, count))
        {
            if (regexPlan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase)
                return new(Utf8CompiledEngineKind.ExactLiteral);

            return new(Utf8CompiledEngineKind.LiteralFamily,
                Utf8CompiledBackendCapability.CanUseEmittedLiteralFamily(regexPlan.SearchPlan)
                    ? Utf8CompiledExecutionBackend.EmittedInstruction
                    : Utf8CompiledExecutionBackend.InterpretedInstruction);
        }

        return new(Utf8CompiledEngineKind.FallbackRegex);
    }

    private static bool IsLiteral(Utf8PreparedRegex plan, Utf8SearchOperationPlan count)
    {
        var single = plan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase;
        // Empty literals have no search candidate; the scalar cursor owns progress.
        if (single && plan.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral &&
            plan.LiteralUtf8 is { Length: 0 } && !plan.SearchPlan.HasBoundaryRequirements &&
            !plan.SearchPlan.HasTrailingLiteralRequirement)
            return true;
        if (plan.CompiledPatternCategory != Utf8CompiledPatternCategory.Literal ||
            count.CandidateSource.Kind != Utf8CandidateSearchKind.PreparedSearcher)
            return false;
        return single
            ? count.CandidateSource.PortfolioKind is Utf8SearchPortfolioKind.ExactLiteral or Utf8SearchPortfolioKind.IgnoreCaseLiteral
            : (plan.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) &&
                (count.CandidateSource.PortfolioKind is Utf8SearchPortfolioKind.ExactDirectFamily or Utf8SearchPortfolioKind.ExactTrieFamily or
                    Utf8SearchPortfolioKind.ExactAutomatonFamily or Utf8SearchPortfolioKind.ExactPackedFamily or
                    Utf8SearchPortfolioKind.ExactEarliestFamily or Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily);
    }
}
