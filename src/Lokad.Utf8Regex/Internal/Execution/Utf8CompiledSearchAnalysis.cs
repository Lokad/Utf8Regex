using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8CompiledSearchMode : byte
{
    None = 0,
    ExactLiteral = 1,
    LiteralFamily = 2,
    StructuralIdentifierFamily = 3,
    StructuralLinearAutomaton = 4,
    OrderedLiteralWindow = 5,
    SimplePattern = 6,
    ByteSafeLinear = 7,
    CompiledFallback = 8,
    SearchGuidedFallback = 9,
    FallbackRegex = 10,
}

internal enum Utf8CompiledEmittedFamily : byte
{
    None = 0,
    LiteralFamily = 1,
    SearchGuidedFallback = 2,
    StructuralDeterministic = 3,
    UpperWordIdentifier = 4,
    SharedPrefixSuffix = 5,
    OrderedLiteralWindow = 6,
}

internal readonly record struct Utf8CompiledSearchAnalysis(
    Utf8CompiledSearchMode Mode,
    Utf8CompiledEngine Engine,
    Utf8CompiledEmittedFamily EmittedFamily = Utf8CompiledEmittedFamily.None,
    Utf8SearchEngineKind CandidateEngineKind = Utf8SearchEngineKind.None)
{
    public bool HasEmittedBackend => Engine.Backend == Utf8CompiledExecutionBackend.EmittedInstruction || Engine.Kind == Utf8CompiledEngineKind.EmittedKernel;
}

internal static class Utf8CompiledSearchAnalyzer
{
    public static Utf8CompiledSearchAnalysis Analyze(Utf8RegexPlan regexPlan, bool preferCompiled)
    {
        var canPromoteFallbackExecution = Utf8CompiledSearchAnalysisPolicy.CanPromoteFallbackExecution(regexPlan);
        var countPipeline = regexPlan.SearchPlan.CountPipeline;
        var firstMatchPipeline = regexPlan.SearchPlan.FirstMatchPipeline;
        var shouldKeepFallbackRegexOnCompiledHint = preferCompiled && ShouldKeepFallbackRegexOnCompiledHint(regexPlan);

        if (Utf8CompiledPatternBackendPolicy.SupportsLiteralCompiledAnalysis(regexPlan, countPipeline))
        {
            return Utf8CompiledPatternBackendPolicy.CreateLiteralAnalysis(regexPlan, countPipeline);
        }

        if (Utf8CompiledPatternBackendPolicy.SupportsDeterministicLinearAnalysis(regexPlan) &&
            (regexPlan.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily ||
             regexPlan.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow) &&
            preferCompiled &&
            Utf8EmittedKernelLowerer.TryLower(regexPlan, out var emittedKernelPlan))
        {
            return new Utf8CompiledSearchAnalysis(
                regexPlan.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
                    ? Utf8CompiledSearchMode.OrderedLiteralWindow
                    : Utf8CompiledSearchMode.StructuralIdentifierFamily,
                new Utf8CompiledEngine(Utf8CompiledEngineKind.EmittedKernel),
                MapEmittedKernel(emittedKernelPlan.Kind),
                regexPlan.SearchPlan.CountPipeline.Strategy.CandidateEngine.Kind);
        }

        if (Utf8CompiledPatternBackendPolicy.SupportsDeterministicLinearAnalysis(regexPlan) &&
            regexPlan.ExecutionKind is NativeExecutionKind.AsciiStructuralIdentifierFamily or
                NativeExecutionKind.AsciiStructuralTokenWindow or
                NativeExecutionKind.AsciiStructuralRepeatedSegment or
                NativeExecutionKind.AsciiStructuralQuotedRelation or
                NativeExecutionKind.AsciiOrderedLiteralWindow)
        {
            return Utf8CompiledPatternBackendPolicy.CreateDeterministicLinearAnalysis(regexPlan, countPipeline);
        }

        if (regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            Utf8CompiledPatternBackendPolicy.SupportsDeterministicLinearAnalysis(regexPlan) &&
            Utf8CompiledSearchAnalysisPolicy.CanUseStructuralLinearSimplePattern(regexPlan))
        {
            return Utf8CompiledPatternBackendPolicy.CreateDeterministicLinearAnalysis(regexPlan, countPipeline) with
            {
                Mode = Utf8CompiledSearchMode.SimplePattern
            };
        }

        if (Utf8CompiledPatternBackendPolicy.SupportsSimplePatternInterpreterAnalysis(regexPlan))
        {
            return Utf8CompiledPatternBackendPolicy.CreateSimplePatternInterpreterAnalysis(countPipeline);
        }

        if (regexPlan.ExecutionKind == NativeExecutionKind.FallbackRegex && shouldKeepFallbackRegexOnCompiledHint)
        {
            return new Utf8CompiledSearchAnalysis(
                Utf8CompiledSearchMode.FallbackRegex,
                new Utf8CompiledEngine(Utf8CompiledEngineKind.FallbackRegex),
                CandidateEngineKind: countPipeline.Strategy.CandidateEngine.Kind);
        }

        if (regexPlan.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            canPromoteFallbackExecution &&
            (regexPlan.StructuralVerifier.ByteSafeLazyDfaProgram.HasValue ||
             regexPlan.StructuralVerifier.ByteSafeLinearProgram.HasValue) &&
            Utf8ByteSafeLinearExecutor.HasCandidateSource(regexPlan))
        {
            return new Utf8CompiledSearchAnalysis(
                Utf8CompiledSearchMode.ByteSafeLinear,
                new Utf8CompiledEngine(Utf8CompiledEngineKind.ByteSafeLinear),
                CandidateEngineKind: countPipeline.Strategy.CandidateEngine.Kind);
        }

        if (regexPlan.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            preferCompiled &&
            Utf8CompiledSearchAnalysisPolicy.CanUseCompiledFallback(regexPlan))
        {
            return new Utf8CompiledSearchAnalysis(
                Utf8CompiledSearchMode.CompiledFallback,
                new Utf8CompiledEngine(Utf8CompiledEngineKind.CompiledFallback),
                CandidateEngineKind: countPipeline.Strategy.CandidateEngine.Kind);
        }

        if (regexPlan.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            canPromoteFallbackExecution &&
            regexPlan.SearchPlan.FallbackSearch.CandidatePlans is { Length: > 0 } candidatePlans &&
            (regexPlan.SearchPlan.CanGuideFallbackStarts ||
             regexPlan.SearchPlan.FallbackStartTransform.HasValue ||
             candidatePlans.Any(static plan => plan.YieldKind == Utf8StructuralSearchYieldKind.Window) ||
             Utf8CompiledSearchAnalysisPolicy.IsSearchGuidedFallbackPipeline(countPipeline, firstMatchPipeline)))
        {
            var backend = Utf8CompiledBackendCapability.CanUseEmittedSearchGuidedFallback(regexPlan)
                ? Utf8CompiledExecutionBackend.EmittedInstruction
                : Utf8CompiledExecutionBackend.InterpretedInstruction;
            return new Utf8CompiledSearchAnalysis(
                Utf8CompiledSearchMode.SearchGuidedFallback,
                new Utf8CompiledEngine(Utf8CompiledEngineKind.SearchGuidedFallback, backend),
                backend == Utf8CompiledExecutionBackend.EmittedInstruction ? Utf8CompiledEmittedFamily.SearchGuidedFallback : Utf8CompiledEmittedFamily.None,
                countPipeline.Strategy.CandidateEngine.Kind);
        }

        return new Utf8CompiledSearchAnalysis(
            Utf8CompiledSearchMode.FallbackRegex,
            new Utf8CompiledEngine(Utf8CompiledEngineKind.FallbackRegex),
            CandidateEngineKind: countPipeline.Strategy.CandidateEngine.Kind);
    }

    private static Utf8CompiledEmittedFamily MapEmittedKernel(Utf8EmittedKernelKind kind) => kind switch
    {
        Utf8EmittedKernelKind.UpperWordIdentifierFamily => Utf8CompiledEmittedFamily.UpperWordIdentifier,
        Utf8EmittedKernelKind.SharedPrefixAsciiWhitespaceSuffix => Utf8CompiledEmittedFamily.SharedPrefixSuffix,
        Utf8EmittedKernelKind.OrderedAsciiWhitespaceLiteralWindow => Utf8CompiledEmittedFamily.OrderedLiteralWindow,
        Utf8EmittedKernelKind.PairedOrderedAsciiWhitespaceLiteralWindow => Utf8CompiledEmittedFamily.OrderedLiteralWindow,
        _ => Utf8CompiledEmittedFamily.None,
    };

    private static bool ShouldKeepFallbackRegexOnCompiledHint(Utf8RegexPlan regexPlan)
    {
        if (regexPlan.ExecutionKind != NativeExecutionKind.FallbackRegex ||
            !regexPlan.FallbackDirectFamily.SupportsNativeFallbackRoute)
        {
            return false;
        }

        return string.IsNullOrEmpty(regexPlan.FallbackReason) ||
            string.Equals(regexPlan.FallbackReason, "unsupported_loop", StringComparison.Ordinal);
    }

}
