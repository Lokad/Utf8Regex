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

    // Conservative carrier allowance; major children have separate admission.
    internal static ulong ObjectBytes => 2048;

    private Utf8PreparedRegex(
        Utf8FallbackDirectFamilyPlan fallbackDirectFamily,
        Utf8OperationMatchCursorPlans operationMatchCursorPlans,
        Utf8PreparedSemanticRegex semanticRegex,
        Utf8RegexFeatures features,
        string? executionPattern,
        NativeExecutionKind executionKind,
        Utf8ExecutionBackend executionBackend,
        Utf8CompiledPatternCategory compiledPatternCategory,
        Utf8ExecutionTree? executionTree,
        Utf8SliceProgram? scalarProgram,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralVerifierPlan structuralVerifier,
        AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan,
        AsciiStructuralTokenWindowPlan structuralTokenWindowPlan,
        AsciiStructuralRepeatedSegmentPlan structuralRepeatedSegmentPlan,
        AsciiStructuralQuotedRelationPlan structuralQuotedRelationPlan,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan,
        byte[]? literalUtf8,
        string? fallbackReason)
    {
        _fallbackDirectFamily = fallbackDirectFamily;
        _operationMatchCursorPlans = operationMatchCursorPlans;
        SemanticRegex = semanticRegex;
        Features = features;
        ExecutionPattern = executionPattern;
        ExecutionKind = executionKind;
        ExecutionBackend = executionBackend;
        CompiledPatternCategory = compiledPatternCategory;
        ExecutionTree = executionTree;
        ScalarProgram = scalarProgram;
        StructuralSearchPlan = structuralSearchPlan;
        StructuralVerifier = structuralVerifier;
        StructuralIdentifierFamilyPlan = structuralIdentifierFamilyPlan;
        StructuralTokenWindowPlan = structuralTokenWindowPlan;
        StructuralRepeatedSegmentPlan = structuralRepeatedSegmentPlan;
        StructuralQuotedRelationPlan = structuralQuotedRelationPlan;
        OrderedLiteralWindowPlan = orderedLiteralWindowPlan;
        LiteralUtf8 = literalUtf8;
        FallbackReason = fallbackReason;
    }

    internal static Utf8PreparedRegex Create(
        Utf8PreparedSemanticRegex semanticRegex,
        Utf8RegexFeatures features,
        string? executionPattern,
        NativeExecutionKind executionKind,
        Utf8ExecutionBackend executionBackend,
        Utf8CompiledPatternCategory compiledPatternCategory,
        Utf8ExecutionTree? executionTree,
        Utf8SliceProgram? scalarProgram,
        Utf8OperationMatchCursorPlans operationMatchCursorPlans,
        Utf8StructuralSearchPlan structuralSearchPlan,
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
        scope.Budget.CheckBytes(ObjectBytes);
        scope.Budget.ChargeWork(2);
        scope.Budget.ReserveBytes(ObjectBytes);
        var result = new Utf8PreparedRegex(
            fallbackDirectFamily,
            operationMatchCursorPlans,
            semanticRegex,
            features,
            executionPattern,
            executionKind,
            executionBackend,
            compiledPatternCategory,
            executionTree,
            scalarProgram,
            structuralSearchPlan,
            structuralVerifier,
            structuralIdentifierFamilyPlan,
            structuralTokenWindowPlan,
            structuralRepeatedSegmentPlan,
            structuralQuotedRelationPlan,
            orderedLiteralWindowPlan,
            literalUtf8,
            fallbackReason);
        scope.Complete();
        return result;
    }

    public Utf8PreparedSemanticRegex SemanticRegex { get; }

    public Utf8RegexFeatures Features { get; }

    public string? ExecutionPattern { get; }

    public NativeExecutionKind ExecutionKind { get; }

    public Utf8ExecutionBackend ExecutionBackend { get; }

    public Utf8CompiledPatternCategory CompiledPatternCategory { get; }

    public Utf8ExecutionTree? ExecutionTree { get; }

    public Utf8SliceProgram? ScalarProgram { get; }

    public ref readonly Utf8SearchPlan SearchPlan => ref _operationMatchCursorPlans.SearchPlan;

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

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
