using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Planning;

/// <summary>
/// Immutable output of core regex preparation. Construction is deliberately inert:
/// every executable artifact is supplied by <see cref="Utf8RegexPreparer"/>.
/// </summary>
internal sealed class Utf8PreparedRegex
{
    private readonly Utf8FallbackDirectFamilyPlan _fallbackDirectFamily;
    private readonly Utf8OperationMatchCursorPlans _operationMatchCursorPlans;

    public Utf8PreparedRegex(
        Utf8SemanticRegex semanticRegex,
        Utf8RegexFeatures features,
        string executionPattern,
        NativeExecutionKind executionKind,
        Utf8ExecutionBackend executionBackend,
        Utf8CompiledPatternCategory compiledPatternCategory,
        Utf8ExecutionTree? executionTree,
        Utf8ExecutionProgram? executionProgram,
        Utf8SearchPlan searchPlan,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8DeterministicAnchorSearch deterministicAnchor,
        Utf8DeterministicVerifierGuards deterministicGuards,
        Utf8FallbackVerifierPlan fallbackVerifier,
        Utf8StructuralVerifierPlan structuralVerifier,
        Utf8StructuralLinearProgram structuralLinearProgram,
        AsciiSimplePatternPlan simplePatternPlan,
        AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan,
        AsciiStructuralTokenWindowPlan structuralTokenWindowPlan,
        AsciiStructuralRepeatedSegmentPlan structuralRepeatedSegmentPlan,
        AsciiStructuralQuotedRelationPlan structuralQuotedRelationPlan,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan,
        byte[]? literalUtf8,
        string? fallbackReason,
        Utf8FallbackDirectFamilyPlan fallbackDirectFamily)
    {
        SemanticRegex = semanticRegex;
        Features = features;
        ExecutionPattern = executionPattern;
        ExecutionKind = executionKind;
        ExecutionBackend = executionBackend;
        CompiledPatternCategory = compiledPatternCategory;
        ExecutionTree = executionTree;
        ExecutionProgram = executionProgram;
        _operationMatchCursorPlans = new Utf8OperationMatchCursorPlans(
            executionProgram,
            searchPlan,
            structuralLinearProgram,
            simplePatternPlan);
        StructuralSearchPlan = structuralSearchPlan;
        DeterministicAnchor = deterministicAnchor;
        DeterministicGuards = deterministicGuards;
        FallbackVerifier = fallbackVerifier;
        StructuralVerifier = structuralVerifier;
        StructuralIdentifierFamilyPlan = structuralIdentifierFamilyPlan;
        StructuralTokenWindowPlan = structuralTokenWindowPlan;
        StructuralRepeatedSegmentPlan = structuralRepeatedSegmentPlan;
        StructuralQuotedRelationPlan = structuralQuotedRelationPlan;
        OrderedLiteralWindowPlan = orderedLiteralWindowPlan;
        LiteralUtf8 = literalUtf8;
        FallbackReason = fallbackReason;
        _fallbackDirectFamily = fallbackDirectFamily;
    }

    public Utf8SemanticRegex SemanticRegex { get; }

    public Utf8RegexFeatures Features { get; }

    public string ExecutionPattern { get; }

    public NativeExecutionKind ExecutionKind { get; }

    public Utf8ExecutionBackend ExecutionBackend { get; }

    public Utf8CompiledPatternCategory CompiledPatternCategory { get; }

    public Utf8ExecutionTree? ExecutionTree { get; }

    public Utf8ExecutionProgram? ExecutionProgram { get; }

    public ref readonly Utf8SearchPlan SearchPlan => ref _operationMatchCursorPlans.SearchPlan;

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8DeterministicAnchorSearch DeterministicAnchor { get; }

    public Utf8DeterministicVerifierGuards DeterministicGuards { get; }

    public Utf8FallbackVerifierPlan FallbackVerifier { get; }

    public Utf8StructuralVerifierPlan StructuralVerifier { get; }

    public ref readonly Utf8StructuralLinearProgram StructuralLinearProgram => ref _operationMatchCursorPlans.StructuralLinearProgram;

    public ref readonly AsciiSimplePatternPlan SimplePatternPlan => ref _operationMatchCursorPlans.SimplePatternPlan;

    public Utf8OperationMatchCursorPlans OperationMatchCursorPlans => _operationMatchCursorPlans;

    public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan { get; }

    public AsciiStructuralTokenWindowPlan StructuralTokenWindowPlan { get; }

    public AsciiStructuralRepeatedSegmentPlan StructuralRepeatedSegmentPlan { get; }

    public AsciiStructuralQuotedRelationPlan StructuralQuotedRelationPlan { get; }

    public AsciiOrderedLiteralWindowPlan OrderedLiteralWindowPlan { get; }

    public byte[]? LiteralUtf8 { get; }

    public string? FallbackReason { get; }

    public ref readonly Utf8FallbackDirectFamilyPlan FallbackDirectFamily => ref _fallbackDirectFamily;
}
