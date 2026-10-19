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
    FallbackRegex = 10,
}

internal enum Utf8CompiledEmittedFamily : byte
{
    None = 0,
    LiteralFamily = 1,
    StructuralDeterministic = 3,
    UpperWordIdentifier = 4,
    SharedPrefixSuffix = 5,
    OrderedLiteralWindow = 6,
}

internal readonly record struct Utf8CompiledSearchAnalysis(
    Utf8CompiledSearchMode Mode,
    Utf8CompiledEngine Engine,
    Utf8CompiledEmittedFamily EmittedFamily,
    Utf8CandidateSearchKind CandidateSourceKind)
{
    public bool HasEmittedBackend => Engine.Backend == Utf8CompiledExecutionBackend.EmittedInstruction || Engine.Kind == Utf8CompiledEngineKind.EmittedKernel;
}

internal static class Utf8CompiledSearchAnalyzer
{
    public static Utf8CompiledSearchAnalysis Analyze(Utf8PreparedRegex regexPlan, bool preferCompiled)
    {
        var countPipeline = regexPlan.SearchPlan.CountOperation;

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
                regexPlan.SearchPlan.CountOperation.CandidateSource.Kind);
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

        return new Utf8CompiledSearchAnalysis(
            Utf8CompiledSearchMode.FallbackRegex,
            new Utf8CompiledEngine(Utf8CompiledEngineKind.FallbackRegex),
            Utf8CompiledEmittedFamily.None,
            CandidateSourceKind: countPipeline.CandidateSource.Kind);
    }

    private static Utf8CompiledEmittedFamily MapEmittedKernel(Utf8EmittedKernelKind kind) => kind switch
    {
        Utf8EmittedKernelKind.UpperWordIdentifierFamily => Utf8CompiledEmittedFamily.UpperWordIdentifier,
        Utf8EmittedKernelKind.SharedPrefixAsciiWhitespaceSuffix => Utf8CompiledEmittedFamily.SharedPrefixSuffix,
        Utf8EmittedKernelKind.OrderedAsciiWhitespaceLiteralWindow => Utf8CompiledEmittedFamily.OrderedLiteralWindow,
        _ => Utf8CompiledEmittedFamily.None,
    };

}
