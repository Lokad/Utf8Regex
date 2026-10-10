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

    private readonly record struct ConstructionState(
        Utf8FallbackDirectFamilyPlan FallbackDirectFamily,
        Utf8OperationMatchCursorPlans OperationMatchCursorPlans,
        Utf8SemanticRegex SemanticRegex,
        Utf8RegexFeatures Features,
        string ExecutionPattern,
        NativeExecutionKind ExecutionKind,
        Utf8ExecutionBackend ExecutionBackend,
        Utf8CompiledPatternCategory CompiledPatternCategory,
        Utf8ExecutionTree? ExecutionTree,
        Utf8ExecutionProgram? ExecutionProgram,
        Utf8SliceProgram? ScalarProgram,
        Utf8StructuralSearchPlan StructuralSearchPlan,
        Utf8DeterministicAnchorSearch DeterministicAnchor,
        Utf8DeterministicVerifierGuards DeterministicGuards,
        Utf8ScalarVerifierPlan ScalarVerifier,
        Utf8StructuralVerifierPlan StructuralVerifier,
        AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan,
        AsciiStructuralTokenWindowPlan StructuralTokenWindowPlan,
        AsciiStructuralRepeatedSegmentPlan StructuralRepeatedSegmentPlan,
        AsciiStructuralQuotedRelationPlan StructuralQuotedRelationPlan,
        AsciiOrderedLiteralWindowPlan OrderedLiteralWindowPlan,
        byte[]? LiteralUtf8,
        string? FallbackReason);

    private Utf8PreparedRegex(ConstructionState state)
    {
        _fallbackDirectFamily = state.FallbackDirectFamily;
        _operationMatchCursorPlans = state.OperationMatchCursorPlans;
        SemanticRegex = state.SemanticRegex;
        Features = state.Features;
        ExecutionPattern = state.ExecutionPattern;
        ExecutionKind = state.ExecutionKind;
        ExecutionBackend = state.ExecutionBackend;
        CompiledPatternCategory = state.CompiledPatternCategory;
        ExecutionTree = state.ExecutionTree;
        ExecutionProgram = state.ExecutionProgram;
        ScalarProgram = state.ScalarProgram;
        StructuralSearchPlan = state.StructuralSearchPlan;
        DeterministicAnchor = state.DeterministicAnchor;
        DeterministicGuards = state.DeterministicGuards;
        ScalarVerifier = state.ScalarVerifier;
        StructuralVerifier = state.StructuralVerifier;
        StructuralIdentifierFamilyPlan = state.StructuralIdentifierFamilyPlan;
        StructuralTokenWindowPlan = state.StructuralTokenWindowPlan;
        StructuralRepeatedSegmentPlan = state.StructuralRepeatedSegmentPlan;
        StructuralQuotedRelationPlan = state.StructuralQuotedRelationPlan;
        OrderedLiteralWindowPlan = state.OrderedLiteralWindowPlan;
        LiteralUtf8 = state.LiteralUtf8;
        FallbackReason = state.FallbackReason;
    }

    internal static Utf8PreparedRegex Create(
        Utf8SemanticRegex semanticRegex,
        Utf8RegexFeatures features,
        string executionPattern,
        NativeExecutionKind executionKind,
        Utf8ExecutionBackend executionBackend,
        Utf8CompiledPatternCategory compiledPatternCategory,
        Utf8ExecutionTree? executionTree,
        Utf8ExecutionProgram? executionProgram,
        Utf8SliceProgram? scalarProgram,
        Utf8OperationMatchCursorPlans operationMatchCursorPlans,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8DeterministicAnchorSearch deterministicAnchor,
        Utf8DeterministicVerifierGuards deterministicGuards,
        Utf8ScalarVerifierPlan scalarVerifier,
        Utf8StructuralVerifierPlan structuralVerifier,
        AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan,
        AsciiStructuralTokenWindowPlan structuralTokenWindowPlan,
        AsciiStructuralRepeatedSegmentPlan structuralRepeatedSegmentPlan,
        AsciiStructuralQuotedRelationPlan structuralQuotedRelationPlan,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan,
        byte[]? literalUtf8,
        string? fallbackReason,
        Utf8FallbackDirectFamilyPlan fallbackDirectFamily)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var result = scope.Budget.AllocateObject(new ConstructionState(
            fallbackDirectFamily,
            operationMatchCursorPlans,
            semanticRegex,
            features,
            executionPattern,
            executionKind,
            executionBackend,
            compiledPatternCategory,
            executionTree,
            executionProgram,
            scalarProgram,
            structuralSearchPlan,
            deterministicAnchor,
            deterministicGuards,
            scalarVerifier,
            structuralVerifier,
            structuralIdentifierFamilyPlan,
            structuralTokenWindowPlan,
            structuralRepeatedSegmentPlan,
            structuralQuotedRelationPlan,
            orderedLiteralWindowPlan,
            literalUtf8,
            fallbackReason),
            static state => new Utf8PreparedRegex(state));
        scope.Complete();
        return result;
    }

    public Utf8SemanticRegex SemanticRegex { get; }

    public Utf8RegexFeatures Features { get; }

    public string ExecutionPattern { get; }

    public NativeExecutionKind ExecutionKind { get; }

    public Utf8ExecutionBackend ExecutionBackend { get; }

    public Utf8CompiledPatternCategory CompiledPatternCategory { get; }

    public Utf8ExecutionTree? ExecutionTree { get; }

    public Utf8ExecutionProgram? ExecutionProgram { get; }

    public Utf8SliceProgram? ScalarProgram { get; }

    public ref readonly Utf8SearchPlan SearchPlan => ref _operationMatchCursorPlans.SearchPlan;

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8DeterministicAnchorSearch DeterministicAnchor { get; }

    public Utf8DeterministicVerifierGuards DeterministicGuards { get; }

    public Utf8ScalarVerifierPlan ScalarVerifier { get; }

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
