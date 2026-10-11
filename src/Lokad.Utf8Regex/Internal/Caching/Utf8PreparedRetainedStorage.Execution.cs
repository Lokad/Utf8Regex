using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

// Typed collection follows every stored reference-bearing field, including
// inactive portfolio payloads. Inline values are charged by their owner.

internal readonly partial struct AsciiFixedLiteralCheck
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literal, storage);
    }
}

internal readonly partial struct AsciiOrderedLiteralWindowPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_leadingLiteralsUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(_trailingLiteralsUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(LeadingLiteralUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(TrailingLiteralUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(BranchSeparators, storage);
        Utf8PreparedRetainedStorage.CollectArray(TrailingSearchers, storage);
    }
}

internal readonly partial struct AsciiSimplePatternAnchoredHeadTailRunPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        if (TailSearch is { } value3) value3.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiSimplePatternAnchoredValidatorPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Segments, storage);
    }
}

internal readonly partial struct AsciiSimplePatternAnchoredValidatorSegment
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_literal, storage);
    }
}

internal readonly partial struct AsciiSimplePatternBoundedSuffixLiteralPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_literalUtf8, storage);
    }
}

internal readonly partial struct AsciiSimplePatternPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Branches, storage);
        Utf8PreparedRetainedStorage.CollectArray(SearchLiterals, storage);
        Utf8PreparedRetainedStorage.CollectArray(FixedLiteralChecks, storage);
        RunPlan.CollectRetainedStorage(storage);
        AnchoredHeadTailRunPlan.CollectRetainedStorage(storage);
        AnchoredValidatorPlan.CollectRetainedStorage(storage);
        RepeatedDigitGroupPlan.CollectRetainedStorage(storage);
        BoundedSuffixLiteralPlan.CollectRetainedStorage(storage);
        SymmetricLiteralWindowPlan.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiSimplePatternRepeatedDigitGroupPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(SeparatorBytes, storage);
    }
}

internal readonly partial struct AsciiSimplePatternRunPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Search.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiSimplePatternSymmetricLiteralWindowPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(FirstLiteralUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(SecondLiteralUtf8, storage);
        SearchData.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiStructuralIdentifierFamilyPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Prefixes, storage);
        Utf8PreparedRetainedStorage.CollectArray(SuffixUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(SuffixParts, storage);
        VerifierProgram.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiStructuralLinearVerifierInstruction
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(SuffixParts, storage);
    }
}

internal readonly partial struct AsciiStructuralLinearVerifierProgram
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Instructions, storage);
    }
}

internal readonly partial struct AsciiStructuralQuotedRelationPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(PrefixesUtf8, storage);
    }
}

internal readonly partial struct AsciiStructuralSuffixPart
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        _literalSearch.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiStructuralTokenWindowPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(AnchorLiteralUtf8, storage);
        LeadingRunPlan.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiStructuralVerifierProgram
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Steps, storage);
        LinearProgram.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiStructuralVerifierStep
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(SuffixParts, storage);
    }
}

internal readonly partial struct PreparedAsciiFindPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_fixedDistanceSets, storage);
        LiteralSearch.CollectRetainedStorage(storage);
        LiteralFamilySearch.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8AsciiDeterministicProgram
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Steps, storage);
        Utf8PreparedRetainedStorage.CollectArray(FixedLiteralChecks, storage);
        Utf8PreparedRetainedStorage.CollectArray(SearchLiterals, storage);
        Utf8PreparedRetainedStorage.CollectArray(FixedWidthChecks, storage);
    }
}

internal readonly partial record struct Utf8ByteSafeLazyDfaCompileOutcome
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Program.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8ByteSafeLazyDfaVerifierProgram
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_transitionOffsets, storage);
        Utf8PreparedRetainedStorage.CollectArray(_transitionCounts, storage);
        Utf8PreparedRetainedStorage.CollectArray(_transitionSets, storage);
        Utf8PreparedRetainedStorage.CollectArray(_transitionTargets, storage);
        Utf8PreparedRetainedStorage.CollectArray(_acceptingStates, storage);
    }
}

internal readonly partial record struct Utf8ByteSafeLinearCompileOutcome
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Program.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8ByteSafeLinearVerifierProgram
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Steps, storage);
    }
}

internal readonly partial struct Utf8ByteSafeLinearVerifierStep
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_text, storage);
        Utf8PreparedRetainedStorage.CollectArray(_alternatives, storage);
        Utf8PreparedRetainedStorage.CollectArray(_program, storage);
    }
}

internal readonly partial record struct Utf8DeterministicAnchorSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Searcher.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8DeterministicVerifierGuards
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(PrefixGuards, storage);
        Utf8PreparedRetainedStorage.CollectArray(FixedLiteralUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(FixedDistanceSets, storage);
    }
}

internal readonly partial struct Utf8FallbackDirectFamilyPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Literals.CollectRetainedStorage(storage);
        Url.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8FallbackLiteralPayload
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_literalUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(_secondaryLiteralUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(_tertiaryLiteralUtf8, storage);
    }
}

internal readonly partial struct Utf8FallbackUrlPayload
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_primaryPrefixUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(_secondaryPrefixUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(_relativePrefixUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(_routeMarkerUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(_requiredParameterUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(_optionalParameterUtf8, storage);
    }
}

internal sealed partial class Utf8OperationMatchCursorPlans
{
    internal static ulong RetainedObjectBytes => Utf8ConstructionBudget.ObjectBytes<ConstructionState>();

    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, RetainedObjectBytes)) return;
        _searchPlan.CollectRetainedStorage(storage);
        _structuralLinearProgram.CollectRetainedStorage(storage);
        _simplePatternPlan.CollectRetainedStorage(storage);
        _structuralSearchPlan.CollectRetainedStorage(storage);
        _structuralVerifier.CollectRetainedStorage(storage);
        if (ExecutionProgram is { } value5) Utf8ExecutionRetainedStorage.Collect(null, value5, storage);
    }
}

internal readonly partial struct Utf8StructuralLinearInstructionProgram
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Instructions, storage);
        Search.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(SearchLiterals, storage);
        SearchLiteralsSearch.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(FixedLiteralChecks, storage);
    }
}

internal readonly partial struct Utf8StructuralLinearProgram
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        InstructionProgram.CollectRetainedStorage(storage);
        DeterministicProgram.CollectRetainedStorage(storage);
        RunPlan.CollectRetainedStorage(storage);
        SimplePatternPlan.CollectRetainedStorage(storage);
        TokenWindowPlan.CollectRetainedStorage(storage);
        QuotedRelationPlan.CollectRetainedStorage(storage);
        OrderedLiteralWindowPlan.CollectRetainedStorage(storage);
        StructuralIdentifierFamilyPlan.CollectRetainedStorage(storage);
        SearchPlan.CollectRetainedStorage(storage);
        StructuralSearchPlan.CollectRetainedStorage(storage);
        StructuralVerifierPlan.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8StructuralVerifierPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        AsciiProgram.CollectRetainedStorage(storage);
        LinearCompileOutcome.CollectRetainedStorage(storage);
        LazyDfaCompileOutcome.CollectRetainedStorage(storage);
        if (ByteSafeProgram is { } value4) Utf8ExecutionRetainedStorage.Collect(null, value4, storage);
        ByteSafeGuards.CollectRetainedStorage(storage);
    }
}
