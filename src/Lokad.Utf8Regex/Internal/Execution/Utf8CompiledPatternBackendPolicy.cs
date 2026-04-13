using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledPatternBackendPolicy
{
    public static Utf8CompiledSearchAnalysis CreateLiteralAnalysis(Utf8RegexPlan regexPlan, Utf8ExecutablePipelinePlan countPipeline)
    {
        if (regexPlan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase)
        {
            return new Utf8CompiledSearchAnalysis(
                Utf8CompiledSearchMode.ExactLiteral,
                new Utf8CompiledEngine(Utf8CompiledEngineKind.ExactLiteral),
                CandidateEngineKind: countPipeline.Strategy.CandidateEngine.Kind);
        }

        var backend = Utf8CompiledBackendCapability.CanUseEmittedLiteralFamily(regexPlan.SearchPlan)
            ? Utf8CompiledExecutionBackend.EmittedInstruction
            : Utf8CompiledExecutionBackend.InterpretedInstruction;
        return new Utf8CompiledSearchAnalysis(
            Utf8CompiledSearchMode.LiteralFamily,
            new Utf8CompiledEngine(Utf8CompiledEngineKind.LiteralFamily, backend),
            backend == Utf8CompiledExecutionBackend.EmittedInstruction ? Utf8CompiledEmittedFamily.LiteralFamily : Utf8CompiledEmittedFamily.None,
            countPipeline.Strategy.CandidateEngine.Kind);
    }

    public static Utf8CompiledSearchAnalysis CreateDeterministicLinearAnalysis(Utf8RegexPlan regexPlan, Utf8ExecutablePipelinePlan countPipeline)
    {
        var backend = Utf8CompiledBackendCapability.CanUseEmittedStructuralLinear(regexPlan)
            ? Utf8CompiledExecutionBackend.EmittedInstruction
            : Utf8CompiledExecutionBackend.Legacy;
        return new Utf8CompiledSearchAnalysis(
            regexPlan.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
                ? Utf8CompiledSearchMode.OrderedLiteralWindow
                : regexPlan.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily
                    ? Utf8CompiledSearchMode.StructuralIdentifierFamily
                    : Utf8CompiledSearchMode.StructuralLinearAutomaton,
            new Utf8CompiledEngine(Utf8CompiledEngineKind.StructuralLinearAutomaton, backend),
            backend == Utf8CompiledExecutionBackend.EmittedInstruction ? Utf8CompiledEmittedFamily.StructuralDeterministic : Utf8CompiledEmittedFamily.None,
            countPipeline.Strategy.CandidateEngine.Kind);
    }

    public static Utf8CompiledSearchAnalysis CreateSimplePatternInterpreterAnalysis(Utf8ExecutablePipelinePlan countPipeline)
    {
        return new Utf8CompiledSearchAnalysis(
            Utf8CompiledSearchMode.SimplePattern,
            new Utf8CompiledEngine(Utf8CompiledEngineKind.SimplePatternInterpreter),
            CandidateEngineKind: countPipeline.Strategy.CandidateEngine.Kind);
    }

    public static bool SupportsLiteralCompiledAnalysis(Utf8RegexPlan regexPlan, Utf8ExecutablePipelinePlan countPipeline)
    {
        return regexPlan.CompiledPatternCategory == Utf8CompiledPatternCategory.Literal &&
            ((regexPlan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase &&
              Utf8CompiledSearchAnalysisPolicy.IsExactLiteralPipeline(countPipeline)) ||
             (regexPlan.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals &&
              Utf8CompiledSearchAnalysisPolicy.IsLiteralFamilyPipeline(countPipeline)));
    }

    public static bool SupportsDeterministicLinearAnalysis(Utf8RegexPlan regexPlan)
    {
        return regexPlan.CompiledPatternCategory == Utf8CompiledPatternCategory.DeterministicLinear;
    }

    public static bool SupportsSimplePatternInterpreterAnalysis(Utf8RegexPlan regexPlan)
    {
        return regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            regexPlan.CompiledPatternCategory is Utf8CompiledPatternCategory.AnchoredWhole or Utf8CompiledPatternCategory.SearchGuided or Utf8CompiledPatternCategory.None;
    }
}
