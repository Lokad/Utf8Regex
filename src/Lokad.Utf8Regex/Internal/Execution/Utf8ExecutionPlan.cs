namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8ExecutionPlan
{
    public Utf8ExecutionPlan(
        string executionPattern,
        NativeExecutionKind nativeKind,
        Utf8ExecutionTree? tree,
        Utf8ExecutionProgram? program,
        Utf8DeterministicAnchorSearch deterministicAnchor = default,
        Utf8DeterministicVerifierGuards deterministicGuards = default,
        Utf8FallbackVerifierPlan fallbackVerifier = default,
        Utf8StructuralVerifierPlan structuralVerifier = default,
        AsciiSimplePatternPlan simplePatternPlan = default,
        AsciiStructuralIdentifierFamilyPlan asyncIdentifierFamilyPlan = default,
        AsciiStructuralTokenWindowPlan tokenWindowPlan = default,
        AsciiStructuralRepeatedSegmentPlan repeatedSegmentPlan = default,
        AsciiStructuralQuotedRelationPlan quotedRelationPlan = default,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan = default,
        byte[]? literalUtf8 = null,
        string? fallbackReason = null,
        Utf8FallbackDirectFamilyPlan fallbackDirectFamily = default)
    {
        ExecutionPattern = executionPattern;
        NativeKind = nativeKind;
        var compiledPatternCategory = Utf8CompiledPatternCategories.GetNativeCategory(nativeKind);
        if (compiledPatternCategory == Utf8CompiledPatternCategory.None && nativeKind == NativeExecutionKind.AsciiSimplePattern)
        {
            compiledPatternCategory = simplePatternPlan.CompiledPatternCategory;
        }

        Backend = compiledPatternCategory switch
        {
            Utf8CompiledPatternCategory.Literal => Utf8ExecutionBackend.NativeLiteral,
            Utf8CompiledPatternCategory.AnchoredWhole or
            Utf8CompiledPatternCategory.SearchGuided or
            Utf8CompiledPatternCategory.DeterministicLinear => Utf8ExecutionBackend.NativeSimplePattern,
            _ => Utf8ExecutionBackend.FallbackRegex,
        };
        Tree = tree;
        Program = program;
        DeterministicAnchor = deterministicAnchor;
        DeterministicGuards = deterministicGuards;
        FallbackVerifier = fallbackVerifier;
        StructuralVerifier = structuralVerifier;
        SimplePatternPlan = simplePatternPlan;
        StructuralIdentifierFamilyPlan = asyncIdentifierFamilyPlan;
        StructuralTokenWindowPlan = tokenWindowPlan;
        StructuralRepeatedSegmentPlan = repeatedSegmentPlan;
        StructuralQuotedRelationPlan = quotedRelationPlan;
        OrderedLiteralWindowPlan = orderedLiteralWindowPlan;
        LiteralUtf8 = literalUtf8;
        FallbackReason = fallbackReason;
        FallbackDirectFamily = fallbackDirectFamily;
    }

    public string ExecutionPattern { get; }

    public Utf8ExecutionBackend Backend { get; }

    public NativeExecutionKind NativeKind { get; }

    public Utf8ExecutionTree? Tree { get; }

    public Utf8ExecutionProgram? Program { get; }

    public Utf8DeterministicAnchorSearch DeterministicAnchor { get; }

    public Utf8DeterministicVerifierGuards DeterministicGuards { get; }

    public Utf8FallbackVerifierPlan FallbackVerifier { get; }

    public Utf8StructuralVerifierPlan StructuralVerifier { get; }

    public AsciiSimplePatternPlan SimplePatternPlan { get; }

    public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan { get; }

    public AsciiStructuralTokenWindowPlan StructuralTokenWindowPlan { get; }

    public AsciiStructuralRepeatedSegmentPlan StructuralRepeatedSegmentPlan { get; }

    public AsciiStructuralQuotedRelationPlan StructuralQuotedRelationPlan { get; }

    public AsciiOrderedLiteralWindowPlan OrderedLiteralWindowPlan { get; }

    public byte[]? LiteralUtf8 { get; }

    public string? FallbackReason { get; }

    public Utf8FallbackDirectFamilyPlan FallbackDirectFamily { get; }

    public Utf8CompiledPatternCategory CompiledPatternCategory => Utf8CompiledPatternCategories.GetNativeCategory(NativeKind) != Utf8CompiledPatternCategory.None
        ? Utf8CompiledPatternCategories.GetNativeCategory(NativeKind)
        : NativeKind == NativeExecutionKind.AsciiSimplePattern
            ? SimplePatternPlan.CompiledPatternCategory
            : Utf8CompiledPatternCategory.None;
}

