using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Planning;

internal readonly struct Utf8RegexPlan
{
    private readonly Utf8StructuralSearchPlan _structuralSearchPlan;
    private readonly Utf8StructuralLinearProgram _structuralLinearProgram;

    public Utf8RegexPlan(
        Utf8ExecutionPlan executionPlan,
        Utf8SearchPlan searchPlan,
        Utf8StructuralSearchPlan structuralSearchPlan = default)
    {
        ExecutionPlan = executionPlan;
        SearchPlan = searchPlan;
        _structuralSearchPlan = structuralSearchPlan.HasValue ? structuralSearchPlan : searchPlan.StructuralSearchPlan;
        _structuralLinearProgram = Utf8StructuralLinearProgram.Create(executionPlan, searchPlan, _structuralSearchPlan);
    }

    public Utf8ExecutionPlan ExecutionPlan { get; }

    public Utf8SearchPlan SearchPlan { get; }

    public string ExecutionPattern => ExecutionPlan.ExecutionPattern;

    public NativeExecutionKind ExecutionKind => ExecutionPlan.NativeKind;

    public Utf8ExecutionBackend ExecutionBackend => ExecutionPlan.Backend;

    public Utf8CompiledPatternCategory CompiledPatternCategory => Utf8CompiledPatternCategories.GetRegexPlanCategory(this);

    public Utf8ExecutionTree? ExecutionTree => ExecutionPlan.Tree;

    public Utf8ExecutionProgram? ExecutionProgram => ExecutionPlan.Program;

    public Utf8StructuralSearchPlan StructuralSearchPlan => _structuralSearchPlan;

    public Utf8DeterministicAnchorSearch DeterministicAnchor => ExecutionPlan.DeterministicAnchor;

    public Utf8DeterministicVerifierGuards DeterministicGuards => ExecutionPlan.DeterministicGuards;

    public Utf8FallbackVerifierPlan FallbackVerifier => ExecutionPlan.FallbackVerifier;

    public Utf8StructuralVerifierPlan StructuralVerifier => ExecutionPlan.StructuralVerifier;

    public Utf8StructuralLinearProgram StructuralLinearProgram => _structuralLinearProgram;

    public Utf8CompiledEngine CompiledEngine => Utf8CompiledEngineSelector.Select(this);

    public AsciiSimplePatternPlan SimplePatternPlan => ExecutionPlan.SimplePatternPlan;

    public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan => ExecutionPlan.StructuralIdentifierFamilyPlan;

    public AsciiStructuralTokenWindowPlan StructuralTokenWindowPlan => ExecutionPlan.StructuralTokenWindowPlan;

    public AsciiStructuralRepeatedSegmentPlan StructuralRepeatedSegmentPlan => ExecutionPlan.StructuralRepeatedSegmentPlan;

    public AsciiStructuralQuotedRelationPlan StructuralQuotedRelationPlan => ExecutionPlan.StructuralQuotedRelationPlan;

    public byte[]? LiteralUtf8 => ExecutionPlan.LiteralUtf8;

    public string? FallbackReason => ExecutionPlan.FallbackReason;

    public Utf8FallbackDirectFamilyPlan FallbackDirectFamily => ExecutionPlan.FallbackDirectFamily;

    public Utf8SearchEnginePlan PrimaryExecutionEngine => CreatePrimaryExecutionEngine();

    private Utf8SearchEnginePlan CreatePrimaryExecutionEngine()
    {
        return CompiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.LiteralFamily or Utf8CompiledEngineKind.ExactLiteral
                => SearchPlan.NativeCandidateEngine,
            Utf8CompiledEngineKind.SearchGuidedFallback
                => SearchPlan.FallbackCandidateEngine,
            Utf8CompiledEngineKind.StructuralFamily
                => new Utf8SearchEnginePlan(Utf8SearchEngineKind.StructuralIdentifierFamily, Utf8SearchSemantics.FirstMatch),
            Utf8CompiledEngineKind.StructuralLinearAutomaton when ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
                => new Utf8SearchEnginePlan(Utf8SearchEngineKind.OrderedLiteralWindow, Utf8SearchSemantics.FirstMatch),
            Utf8CompiledEngineKind.StructuralLinearAutomaton
                => new Utf8SearchEnginePlan(Utf8SearchEngineKind.StructuralDeterministicAutomaton, Utf8SearchSemantics.FirstMatch),
            _ => default,
        };
    }
}

