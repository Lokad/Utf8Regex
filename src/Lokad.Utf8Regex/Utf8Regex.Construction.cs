using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex;

/// <summary>Construction metadata for library-created cached instances.</summary>
public sealed partial class Utf8Regex
{
    // Complete facade field layout, including the fields filled by preparation.
    // This factory admits the library-created cached instance before allocation.
    // Caller-created public boundary instances and their complete public policy
    // remain separate work; a metadata loan is not a program graph certificate.
    private readonly record struct CachedConstructionState(
        Utf8RegexProgram Program,
        Utf8PrioritizedBooleanFamilyKind PrioritizedBooleanFamilyKind,
        bool PrioritizePlainAsciiLiteralCount,
        int FusedAsciiLiteralCountAnchorOffset,
        bool PrioritizeBoundaryLiteralFamilyCount,
        Utf8EmittedLiteralFamilyCounter? FusedAsciiBoundaryLiteralFamilyCounter,
        bool PrioritizeOrderedLiteralWindowCount,
        bool RequiresKelvinSignFallback,
        Utf8InvariantCyrillicLiteralCountStrategy? InvariantCyrillicLiteralCountStrategy,
        Utf8PackedNibbleLiteralFamilyCountStrategy? PackedNibbleLiteralFamilyCountStrategy,
        Utf8LiteralFamilyCaptureMap? LiteralFamilyCaptureMap,
        bool HasCaptures,
        Utf8GroupedLiteralSpanTable? GroupedLiteralSpanTable,
        byte[]? CaretAnchoredGroupedLiteralUtf8,
        Utf8AlternationBranchSpanMap? AlternationBranchSpanMap,
        Utf8SliceProgram OwnedSliceProgram,
        Utf8ReplacementPlanCache ReplacementCache,
        string Pattern,
        RegexOptions Options,
        TimeSpan MatchTimeout,
        Utf8ResourcePolicy ResourcePolicy);

    internal static ulong CachedObjectBytes => Utf8ConstructionBudget.ObjectBytes<CachedConstructionState>();

    internal static Utf8Regex CreateAdmittedForCache(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var fields = default(CachedConstructionState) with
        { Pattern = pattern, Options = options, MatchTimeout = matchTimeout, ResourcePolicy = Utf8ResourcePolicy.Unlimited };
        var result = owner.Budget.AllocateObject(fields,
            static state => new Utf8Regex(state.Pattern, state.Options, state.MatchTimeout));
        owner.Complete(); return result;
    }
}
