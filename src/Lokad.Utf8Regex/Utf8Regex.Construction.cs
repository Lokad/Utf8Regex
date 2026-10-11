using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.FrontEnd;

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

    // The global owner covers this facade, complete scalar/prepared graphs
    // and its bounded replacement child. Runtime/facade helpers and
    // opaque framework storage remain outside this declared subgraph.
    internal void CollectCacheRoots<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Add(this, CachedObjectBytes);
        storage.Text(Pattern);
        storage.Add(_replacementCache, _replacementCache.MaximumRetainedBytes);
        Utf8ScalarProgramRetainedStorage.Collect(_ownedSliceProgram, storage);
        Utf8PreparedRetainedStorage.Collect(_preparedRegex, storage);
    }

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
