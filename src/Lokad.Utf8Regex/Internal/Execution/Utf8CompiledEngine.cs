using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8CompiledEngineKind : byte
{
    FallbackRegex = 0,
    SearchGuidedFallback = 1,
    ByteSafeLinear = 2,
    ExactLiteral = 3,
    LiteralFamily = 4,
    StructuralFamily = 5,
    SimplePatternInterpreter = 6,
    StructuralLinearAutomaton = 7,
    CompiledFallback = 8,
    EmittedKernel = 9,
}

internal enum Utf8CompiledExecutionBackend : byte
{
    Legacy = 0,
    InterpretedInstruction = 1,
    EmittedInstruction = 2,
}

internal readonly struct Utf8CompiledEngine
{
    public Utf8CompiledEngine(Utf8CompiledEngineKind kind, Utf8CompiledExecutionBackend backend = Utf8CompiledExecutionBackend.Legacy)
    {
        Kind = kind;
        Backend = backend;
    }

    public Utf8CompiledEngineKind Kind { get; }

    public Utf8CompiledExecutionBackend Backend { get; }

    public bool HasValue => Kind != Utf8CompiledEngineKind.FallbackRegex || true;
}

internal static class Utf8CompiledEngineSelector
{
    public static Utf8CompiledEngine Select(Utf8RegexPlan regexPlan)
        => Select(regexPlan, preferCompiled: false);

    public static Utf8CompiledEngine Select(Utf8RegexPlan regexPlan, bool preferCompiled)
        => Utf8CompiledSearchAnalyzer.Analyze(regexPlan, preferCompiled).Engine;
}
