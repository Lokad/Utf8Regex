using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Planning;

/// <summary>
/// Immutable output of core regex preparation. Construction is deliberately inert:
/// every executable artifact is supplied by <see cref="Utf8RegexPreparer"/>.
/// </summary>
internal sealed class Utf8PreparedRegex
{
    private readonly Utf8AnchoredLiteralPlan _anchoredLiteral;
    private readonly Utf8OperationMatchCursorPlans _operationMatchCursorPlans;

    // Conservative carrier allowance; major children have separate admission.
    internal static ulong ObjectBytes => 2048;

    private Utf8PreparedRegex(
        Utf8AnchoredLiteralPlan anchoredLiteral, Utf8OperationMatchCursorPlans operationMatchCursorPlans,
        Utf8PreparedSemanticRegex semanticRegex, Utf8RegexFeatures features, string? executionPattern,
        NativeExecutionKind executionKind, Utf8SliceProgram? scalarProgram, byte[]? literalUtf8, string? fallbackReason,
        bool requiredLiteralProvesMatch, int requiredLiteralContinuation, byte[]? requiredLiteralTerminalSuffix)
    {
        _anchoredLiteral = anchoredLiteral;
        _operationMatchCursorPlans = operationMatchCursorPlans;
        SemanticRegex = semanticRegex;
        Features = features;
        ExecutionPattern = executionPattern;
        ExecutionKind = executionKind;
        ScalarProgram = scalarProgram;
        LiteralUtf8 = literalUtf8;
        FallbackReason = fallbackReason;
        RequiredLiteralProvesMatch = requiredLiteralProvesMatch;
        RequiredLiteralContinuation = requiredLiteralContinuation;
        RequiredLiteralTerminalSuffix = requiredLiteralTerminalSuffix;
    }

    internal static Utf8PreparedRegex Create(
        Utf8PreparedSemanticRegex semanticRegex, Utf8RegexFeatures features, string? executionPattern,
        NativeExecutionKind executionKind, Utf8SliceProgram? scalarProgram, Utf8OperationMatchCursorPlans operationMatchCursorPlans,
        byte[]? literalUtf8, string? fallbackReason, Utf8AnchoredLiteralPlan anchoredLiteral)
    {
        using var scope = Utf8ConstructionScope.Enter();
        scope.Budget.CheckBytes(ObjectBytes);
        scope.Budget.ChargeWork(2);
        ref readonly var search = ref operationMatchCursorPlans.SearchPlan;
        ref readonly var prefilter = ref search.RequiredPrefilterSearcher;
        var requiredLiteralProvesMatch = false;
        var requiredLiteralContinuation = 0;
        byte[]? requiredLiteralTerminalSuffix = null;
        if (scalarProgram is { RightToLeft: false, HasContiguousAnchor: false } &&
            !features.HasAbsoluteStartAnchor && !search.CanGuideFallbackStarts &&
            prefilter.Kind == PreparedSearcherKind.ExactLiteral)
        {
            var needle = prefilter.LiteralSearch.Needle;
            requiredLiteralProvesMatch = Utf8SliceRunner.PrepareRequiredLiteralWitness(scalarProgram, needle, scope.Budget);
            if (!requiredLiteralProvesMatch)
            {
                requiredLiteralContinuation = Utf8SliceRunner.PrepareRequiredLiteralContinuation(scalarProgram, needle, scope.Budget);
                if (requiredLiteralContinuation == needle.Length && needle.Length is >= 3 and <= 16)
                    requiredLiteralTerminalSuffix = Utf8SliceRunner.PrepareRequiredLiteralTerminalSuffix(
                        scalarProgram, requiredLiteralContinuation, scope.Budget);
            }
        }
        scope.Budget.ReserveBytes(ObjectBytes);
        var result = new Utf8PreparedRegex(
            anchoredLiteral, operationMatchCursorPlans, semanticRegex, features, executionPattern,
            executionKind, scalarProgram, literalUtf8, fallbackReason, requiredLiteralProvesMatch,
            requiredLiteralContinuation, requiredLiteralTerminalSuffix);
        scope.Complete();
        return result;
    }

    public Utf8PreparedSemanticRegex SemanticRegex { get; }

    public Utf8RegexFeatures Features { get; }

    public string? ExecutionPattern { get; }

    public NativeExecutionKind ExecutionKind { get; }

    public bool HasLiteralPlan => ExecutionKind != NativeExecutionKind.FallbackRegex;

    internal bool RequiredLiteralProvesMatch { get; }
    internal int RequiredLiteralContinuation { get; }
    internal byte[]? RequiredLiteralTerminalSuffix { get; }

    public Utf8SliceProgram? ScalarProgram { get; }

    public ref readonly Utf8SearchPlan SearchPlan => ref _operationMatchCursorPlans.SearchPlan;

    public Utf8OperationMatchCursorPlans OperationMatchCursorPlans => _operationMatchCursorPlans;

    public byte[]? LiteralUtf8 { get; }

    public string? FallbackReason { get; }

    public ref readonly Utf8AnchoredLiteralPlan AnchoredLiteral => ref _anchoredLiteral;
}
