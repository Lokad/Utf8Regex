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
    private readonly Utf8SearchPlan _searchPlan;

    // Includes the inline search plan. Its referenced buffers are admitted separately.
    internal static ulong ObjectBytes => 2048;

    private Utf8PreparedRegex(
        Utf8AnchoredLiteralPlan anchoredLiteral, in Utf8SearchPlan searchPlan,
        Utf8PreparedSemanticRegex semanticRegex, Utf8RegexFeatures features, string? executionPattern,
        NativeExecutionKind executionKind, Utf8SliceProgram? scalarProgram, byte[]? literalUtf8,
        bool requiredLiteralProvesMatch, int requiredLiteralContinuation, byte[]? requiredLiteralTerminalSuffix,
        int requiredLiteralOptionalRun)
    {
        _anchoredLiteral = anchoredLiteral;
        _searchPlan = searchPlan;
        SemanticRegex = semanticRegex;
        Features = features;
        ExecutionPattern = executionPattern;
        ExecutionKind = executionKind;
        ScalarProgram = scalarProgram;
        LiteralUtf8 = literalUtf8;
        RequiredLiteralProvesMatch = requiredLiteralProvesMatch;
        RequiredLiteralContinuation = requiredLiteralContinuation;
        RequiredLiteralTerminalSuffix = requiredLiteralTerminalSuffix;
        RequiredLiteralOptionalRun = requiredLiteralOptionalRun;
    }

    internal static Utf8PreparedRegex Create(
        Utf8PreparedSemanticRegex semanticRegex, Utf8RegexFeatures features, string? executionPattern,
        NativeExecutionKind executionKind, Utf8SliceProgram? scalarProgram, in Utf8SearchPlan searchPlan,
        byte[]? literalUtf8, Utf8AnchoredLiteralPlan anchoredLiteral)
    {
        using var scope = Utf8ConstructionScope.Enter();
        scope.Budget.CheckBytes(ObjectBytes);
        scope.Budget.ChargeWork(2);
        ref readonly var prefilter = ref searchPlan.RequiredPrefilterSearcher;
        var requiredLiteralProvesMatch = false;
        var requiredLiteralContinuation = 0;
        byte[]? requiredLiteralTerminalSuffix = null;
        var requiredLiteralOptionalRun = 0;
        if (scalarProgram is { RightToLeft: false, HasContiguousAnchor: false } &&
            !features.HasAbsoluteStartAnchor && !searchPlan.CanGuideFallbackStarts &&
            prefilter.Kind == PreparedSearcherKind.ExactLiteral)
        {
            var needle = prefilter.LiteralSearch.Needle;
            requiredLiteralProvesMatch = Utf8SliceRunner.PrepareRequiredLiteralWitness(scalarProgram, needle, scope.Budget);
            if (!requiredLiteralProvesMatch)
            {
                (requiredLiteralContinuation, requiredLiteralOptionalRun) =
                    Utf8SliceRunner.PrepareRequiredLiteralContinuation(scalarProgram, needle, scope.Budget);
                if (requiredLiteralContinuation == needle.Length && needle.Length is >= 3 and <= 16)
                    requiredLiteralTerminalSuffix = Utf8SliceRunner.PrepareRequiredLiteralTerminalSuffix(
                        scalarProgram, requiredLiteralContinuation, scope.Budget);
            }
        }
        scope.Budget.ReserveBytes(ObjectBytes);
        var result = new Utf8PreparedRegex(
            anchoredLiteral, searchPlan, semanticRegex, features, executionPattern,
            executionKind, scalarProgram, literalUtf8, requiredLiteralProvesMatch,
            requiredLiteralContinuation, requiredLiteralTerminalSuffix, requiredLiteralOptionalRun);
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
    internal int RequiredLiteralOptionalRun { get; }

    public Utf8SliceProgram? ScalarProgram { get; }

    public ref readonly Utf8SearchPlan SearchPlan => ref _searchPlan;

    public byte[]? LiteralUtf8 { get; }


    public ref readonly Utf8AnchoredLiteralPlan AnchoredLiteral => ref _anchoredLiteral;
}
