using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

// Typed owned state includes final managed-copy receipts. Other CLR method,
// resolver and native JIT metadata remains excluded. Inline state belongs to its owner.

internal readonly partial struct PreparedAsciiDelimitedTokenSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        _delimiterFinder.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedAsciiLiteralStructuredTokenSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        _literalFinder.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedBmpThreeByteLiteralFamilySearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literals, storage);
        Utf8PreparedRetainedStorage.CollectArray(FirstScalars, storage);
        Utf8PreparedRetainedStorage.CollectArray(SecondScalars, storage);
        Utf8PreparedRetainedStorage.CollectArray(ThirdScalars, storage);
    }
}

internal readonly partial struct PreparedShortAsciiLiteralFamilyCounter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_literal0, storage);
        Utf8PreparedRetainedStorage.CollectArray(_literal1, storage);
        Utf8PreparedRetainedStorage.CollectArray(_literal2, storage);
        Utf8PreparedRetainedStorage.CollectArray(_literal3, storage);
        _primarySearch.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8AsciiLiteralFinder
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_literal, storage);
    }
}

internal sealed partial class Utf8ByteSafeLinearCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
        _verifierRuntime?.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(_prefixUntilByteLiteral, storage);
    }
}

internal sealed partial class Utf8CompiledFallbackCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
        _verifierRuntime?.CollectRetainedStorage(storage);
        Utf8RuntimeRetainedStorage.CollectArray(_candidatePlans, storage);
    }
}

internal readonly partial struct Utf8CompiledFindOptimization
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(AnchorBytes, storage);
        Utf8PreparedRetainedStorage.CollectArray(AnchorPrefixesByByte, storage);
        Utf8PreparedRetainedStorage.CollectArray(CommonPrefix, storage);
        PrefixDiscriminator.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8CompiledPatternFamilyPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        AnchoredValidatorPlan.CollectRetainedStorage(storage);
        RepeatedDigitGroupPlan.CollectRetainedStorage(storage);
        BoundedSuffixLiteralPlan.CollectRetainedStorage(storage);
        SymmetricLiteralWindowPlan.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8CompiledSymmetricLiteralWindowCounter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        _plan.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(_firstLiteral, storage);
        Utf8PreparedRetainedStorage.CollectArray(_secondLiteral, storage);
    }
}

internal sealed partial class Utf8EmittedDeterministicMatcher
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<(Utf8EmittedDelegate<FindNextDelegate>, byte[], AsciiCharClass[], bool)>())) return;
        _findNext.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(_searchLiteral, storage);
        Utf8PreparedRetainedStorage.CollectArray(_charClasses, storage);
    }
}

internal sealed partial class Utf8EmittedKernelMatcher
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        _plan.CollectRetainedStorage(storage);
        _anchorSearch?.CollectRetainedStorage(storage);
        _findNext.CollectRetainedStorage(storage);
        _count.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8EmittedKernelPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        FindOptimization.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(Prefixes, storage);
        Utf8PreparedRetainedStorage.CollectArray(Blocks, storage);
    }
}

internal sealed partial class Utf8EmittedLiteralFamilyCounter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        _count.CollectRetainedStorage(storage);
        _asciiValidatedCount?.CollectRetainedStorage(storage);
        _isMatch.CollectRetainedStorage(storage);
        _tryMatch.CollectRetainedStorage(storage);
        _plan.CollectRetainedStorage(storage);
        _countProgram.CollectRetainedStorage(storage);
        _firstMatchProgram.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8EmittedSearchGuidedFallback
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<(Utf8SearchPlan, Utf8VerifierRuntime, Utf8EmittedDelegate<IsMatchDelegate>, Utf8EmittedDelegate<CountDelegate>)>())) return;
        _searchPlan.CollectRetainedStorage(storage);
        _verifierRuntime?.CollectRetainedStorage(storage);
        _isMatch.CollectRetainedStorage(storage);
        _count.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8EmittedTokenFamilyMatcher
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        _plan.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8EmittedWholeMatcher
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<Utf8EmittedDelegate<MatchDelegate>>())) return;
        _match.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8ExactLiteralCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<Utf8LiteralCompiledEngineRuntime>())) return;
        _inner?.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8FallbackRegexCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
        _verifierRuntime?.CollectRetainedStorage(storage);
        _directFamily.CollectRetainedStorage(storage);
        _linePrefixFinder.CollectRetainedStorage(storage);
        _emittedTokenFamilyMatcher?.CollectRetainedStorage(storage);
        _delimitedTokenSearch.CollectRetainedStorage(storage);
        _literalStructuredTokenSearch.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8LiteralCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
        _countProgram.CollectRetainedStorage(storage);
        _firstMatchProgram.CollectRetainedStorage(storage);
        _emittedLiteralFamilyCounter?.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(_smallAsciiLiteralFamily, storage);
        _smallAsciiLiteralFamilyFirstBytes.CollectRetainedStorage(storage);
        _leadingUtf8SegmentLiteralFamilyPrefilter.CollectRetainedStorage(storage);
        _leadingUtf8SegmentLiteralFamilyAutomaton.CollectRetainedStorage(storage);
        _smallAsciiLiteralFamilyPrimitive?.CollectRetainedStorage(storage);
        _shortAsciiLiteralFamilyCounter.CollectRetainedStorage(storage);
        _bmpThreeByteLiteralFamilySearch.CollectRetainedStorage(storage);
        _compositeTrailingLiteralSearch?.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8LiteralFamilyCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<Utf8LiteralCompiledEngineRuntime>())) return;
        _inner?.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8RegexProgram
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(PreparedRegex, storage);
        VerifierRuntime?.CollectRetainedStorage(storage);
        CompiledEngineRuntime?.CollectRetainedStorage(storage);
        DirectStructuralFamilyKernelMatcher?.CollectRetainedStorage(storage);
        CaptureCatalog.CollectStorage(storage);
    }
}

internal sealed partial class Utf8SearchGuidedFallbackCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
        _verifierRuntime?.CollectRetainedStorage(storage);
        _emittedBackend?.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8SimplePatternCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
        _linearRuntime?.CollectRetainedStorage(storage);
        _verifierRuntime?.CollectRetainedStorage(storage);
        _compiledPatternFamily.CollectRetainedStorage(storage);
        _repeatedDigitGroupPlan.CollectRetainedStorage(storage);
        _emittedWholeMatcher?.CollectRetainedStorage(storage);
        _boundedSuffixLiteralPlan.CollectRetainedStorage(storage);
        _symmetricLiteralWindowPlan.CollectRetainedStorage(storage);
        _compiledSymmetricLiteralWindowCounter?.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8StructuralFamilyCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
        _verifierRuntime?.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8StructuralLinearAutomatonCompiledEngineRuntime
{
    internal override void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
        _linearRuntime?.CollectRetainedStorage(storage);
        _verifierRuntime?.CollectRetainedStorage(storage);
        _emittedDeterministicMatcher?.CollectRetainedStorage(storage);
        _emittedKernelMatcher?.CollectRetainedStorage(storage);
        _emittedBoundaryLiteralFamily?.CollectRetainedStorage(storage);
        _orderedWindowTrailingFindPlan.CollectRetainedStorage(storage);
    }
}

internal abstract partial class Utf8StructuralLinearRuntime
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<Utf8PreparedRegex>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
    }
}

internal abstract partial class Utf8StructuralVerifierRuntime
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_regexPlan, storage);
    }
}

internal sealed partial class Utf8VerifierRuntime
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        Utf8PreparedRetainedStorage.Collect(_preparedRegex, storage);
        StructuralVerifierRuntime?.CollectRetainedStorage(storage);
    }
}
