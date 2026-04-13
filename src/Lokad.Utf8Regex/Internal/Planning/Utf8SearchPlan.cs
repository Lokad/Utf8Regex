namespace Lokad.Utf8Regex.Internal.Planning;

using Lokad.Utf8Regex.Internal.Utilities;

internal readonly struct Utf8SearchPlan
{
    public Utf8SearchPlan(
        Utf8SearchKind kind,
        byte[]? literalUtf8,
        byte[][]? alternateLiteralsUtf8 = null,
        bool canGuideFallbackStarts = false,
        byte[]? requiredPrefilterLiteralUtf8 = null,
        byte[][]? requiredPrefilterAlternateLiteralsUtf8 = null,
        string? secondaryRequiredPrefilterQuotedAsciiSet = null,
        int secondaryRequiredPrefilterQuotedAsciiLength = 0,
        Utf8FixedDistanceSet[]? fixedDistanceSets = null,
        byte[]? trailingLiteralUtf8 = null,
        byte[][]? orderedWindowLeadingLiteralsUtf8 = null,
        byte[]? orderedWindowTrailingLiteralUtf8 = null,
        Utf8WindowSearchInfo[]? requiredWindowPrefilters = null,
        int? orderedWindowMaxGap = null,
        bool orderedWindowSameLine = false,
        Utf8FallbackStartTransform fallbackStartTransform = default,
        int distance = 0,
        int minRequiredLength = 0,
        int? exactRequiredLength = null,
        int? maxPossibleLength = null,
        Utf8BoundaryRequirement leadingBoundary = Utf8BoundaryRequirement.None,
        Utf8BoundaryRequirement trailingBoundary = Utf8BoundaryRequirement.None)
    {
        Kind = kind;
        LiteralUtf8 = literalUtf8;
        CanGuideFallbackStarts = canGuideFallbackStarts;
        LiteralSearch = literalUtf8 is not null
            ? new PreparedSubstringSearch(literalUtf8, kind == Utf8SearchKind.AsciiLiteralIgnoreCase)
            : null;
        AlternateLiteralsUtf8 = alternateLiteralsUtf8;
        AlternateLiteralUtf16Lengths = alternateLiteralsUtf8 is { Length: > 0 } && kind == Utf8SearchKind.ExactUtf8Literals
            ? [.. alternateLiteralsUtf8.Select(static literal => Utf8Validation.Validate(literal).Utf16Length)]
            : null;
        AlternateLiteralSearch = alternateLiteralsUtf8 is { Length: > 0 }
            ? new PreparedLiteralSetSearch(alternateLiteralsUtf8)
            : null;
        AlternateIgnoreCaseLiteralSearch = alternateLiteralsUtf8 is { Length: > 0 } && kind == Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals
            ? new PreparedAsciiIgnoreCaseLiteralSetSearch(alternateLiteralsUtf8)
            : null;
        MultiLiteralSearch = CreateMultiLiteralSearch(kind, alternateLiteralsUtf8);
        PreparedSearcher = CreatePreparedSearcher(kind, LiteralSearch, MultiLiteralSearch);
        PortfolioKind = DeterminePortfolioKind(kind, PreparedSearcher, MultiLiteralSearch);
        RequiredPrefilterLiteralUtf8 = requiredPrefilterLiteralUtf8;
        RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiteralsUtf8;
        var requiredPrefilterSearcher = CreateRequiredPrefilter(requiredPrefilterLiteralUtf8, requiredPrefilterAlternateLiteralsUtf8);
        var secondaryRequiredPrefilterSearcher = CreateSecondaryRequiredPrefilter(
            secondaryRequiredPrefilterQuotedAsciiSet,
            secondaryRequiredPrefilterQuotedAsciiLength);
        FixedDistanceSets = fixedDistanceSets;
        TrailingLiteralUtf8 = trailingLiteralUtf8;
        OrderedWindowLeadingLiteralsUtf8 = orderedWindowLeadingLiteralsUtf8;
        OrderedWindowTrailingLiteralUtf8 = orderedWindowTrailingLiteralUtf8;
        var requiredWindowPrefilterPlans =
            CreateRequiredWindowPrefilterPlans(requiredWindowPrefilters) ??
            CreateFallbackRequiredWindowPrefilterPlans(
                requiredPrefilterAlternateLiteralsUtf8,
                secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength);
        OrderedWindowMaxGap = orderedWindowMaxGap;
        OrderedWindowSameLine = orderedWindowSameLine;
        FallbackStartTransform = fallbackStartTransform;
        WindowSearch = CreateWindowSearch(
            orderedWindowLeadingLiteralsUtf8,
            orderedWindowTrailingLiteralUtf8,
            orderedWindowMaxGap,
            orderedWindowSameLine);
        var structuralSearchPlan = Utf8StructuralSearchPlan.Create(
            kind,
            distance,
            canGuideFallbackStarts,
            literalUtf8,
            PreparedSearcher,
            WindowSearch,
            fixedDistanceSets,
            fallbackStartTransform)
            .WithBoundaryRequirements(leadingBoundary, trailingBoundary)
            .WithTrailingLiteral(trailingLiteralUtf8)
            .WithExactLength(exactRequiredLength)
            .WithMaxLength(maxPossibleLength);
        StructuralSearchPlan = structuralSearchPlan;
        NativeSearch = new Utf8NativeSearchPlan(PreparedSearcher, structuralSearchPlan, PortfolioKind);
        PrefilterPlan = new Utf8PrefilterPlan(
            requiredPrefilterSearcher,
            secondaryRequiredPrefilterSearcher,
            requiredWindowPrefilterPlans);
        FallbackSearch = new Utf8FallbackSearchPlan(
            PrefilterPlan,
            CreateFallbackCandidatePlans(structuralSearchPlan, requiredWindowPrefilterPlans));
        Distance = distance;
        MinRequiredLength = minRequiredLength;
        ExactRequiredLength = exactRequiredLength;
        MaxPossibleLength = maxPossibleLength;
        LeadingBoundary = leadingBoundary;
        TrailingBoundary = trailingBoundary;
    }

    public Utf8SearchKind Kind { get; }

    public byte[]? LiteralUtf8 { get; }

    public bool CanGuideFallbackStarts { get; }

    public PreparedSubstringSearch? LiteralSearch { get; }

    public byte[][]? AlternateLiteralsUtf8 { get; }

    public int[]? AlternateLiteralUtf16Lengths { get; }

    public PreparedLiteralSetSearch? AlternateLiteralSearch { get; }

    public PreparedAsciiIgnoreCaseLiteralSetSearch? AlternateIgnoreCaseLiteralSearch { get; }

    public PreparedMultiLiteralSearch MultiLiteralSearch { get; }

    public PreparedSearcher PreparedSearcher { get; }

    public Utf8SearchPortfolioKind PortfolioKind { get; }

    public Utf8NativeSearchPlan NativeSearch { get; }

    public byte[]? RequiredPrefilterLiteralUtf8 { get; }

    public byte[][]? RequiredPrefilterAlternateLiteralsUtf8 { get; }

    public Utf8FallbackSearchPlan FallbackSearch { get; }

    public Utf8PrefilterPlan PrefilterPlan { get; }

    public Utf8SearchEnginePlan NativeCandidateEngine => NativeSearch.CandidateEngine;

    public Utf8SearchEnginePlan FallbackCandidateEngine => FallbackSearch.CandidateEngine;

    public Utf8SearchMetaStrategyPlan CountStrategy =>
        Utf8SearchStrategySelector.CreateCountStrategy(this);

    public Utf8SearchMetaStrategyPlan FirstMatchStrategy =>
        Utf8SearchStrategySelector.CreateFirstMatchStrategy(this);

    public Utf8SearchMetaStrategyPlan EnumerationStrategy =>
        Utf8SearchStrategySelector.CreateEnumerationStrategy(this);

    public Utf8ConfirmationPlan ConfirmationPlan => CreateConfirmationPlan();

    public Utf8ProjectionPlan ProjectionPlan => CreateProjectionPlan();

    public Utf8ExecutablePipelinePlan CountPipeline =>
        new(CountStrategy, ConfirmationPlan);

    public Utf8ExecutablePipelinePlan FirstMatchPipeline =>
        new(FirstMatchStrategy, ConfirmationPlan);

    public Utf8ExecutablePipelinePlan EnumerationPipeline =>
        new(EnumerationStrategy, ConfirmationPlan, ProjectionPlan);

    public Utf8BackendInstructionProgram CountProgram =>
        Utf8BackendInstructionProgramBuilder.Create(CountPipeline);

    public Utf8BackendInstructionProgram FirstMatchProgram =>
        Utf8BackendInstructionProgramBuilder.Create(FirstMatchPipeline);

    public Utf8BackendInstructionProgram EnumerationProgram =>
        Utf8BackendInstructionProgramBuilder.Create(EnumerationPipeline);

    public PreparedSearcher RequiredPrefilterSearcher => PrefilterPlan.PrimarySearcher;

    public PreparedSearcher SecondaryRequiredPrefilterSearcher => PrefilterPlan.SecondarySearcher;

    public AsciiExactLiteralSearchData? AlternateLiteralSearchData => AlternateLiteralSearch?.SearchData;

    public Utf8FixedDistanceSet[]? FixedDistanceSets { get; }

    public byte[]? TrailingLiteralUtf8 { get; }

    public byte[][]? OrderedWindowLeadingLiteralsUtf8 { get; }

    public byte[]? OrderedWindowTrailingLiteralUtf8 { get; }

    public Utf8StructuralSearchPlan[]? RequiredWindowPrefilterPlans => PrefilterPlan.WindowPlans;

    public int? OrderedWindowMaxGap { get; }

    public bool OrderedWindowSameLine { get; }

    public Utf8FallbackStartTransform FallbackStartTransform { get; }

    public PreparedWindowSearch WindowSearch { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8StructuralSearchPlan[]? FallbackCandidatePlans => FallbackSearch.CandidatePlans;

    public int Distance { get; }

    public int MinRequiredLength { get; }

    public int? ExactRequiredLength { get; }

    public int? MaxPossibleLength { get; }

    public Utf8BoundaryRequirement LeadingBoundary { get; }

    public Utf8BoundaryRequirement TrailingBoundary { get; }

    public bool HasLiteral => LiteralUtf8 is { Length: > 0 };

    public bool HasAlternateLiterals => AlternateLiteralsUtf8 is { Length: > 0 };

    public bool HasFixedDistanceSets => FixedDistanceSets is { Length: > 0 };

    public bool HasTrailingLiteralRequirement => TrailingLiteralUtf8 is { Length: > 0 };

    public bool HasRequiredPrefilter => PrefilterPlan.HasValue;

    public bool HasWindowSearch => WindowSearch.HasValue;

    public bool HasFallbackCandidates => FallbackSearch.HasCandidates;

    public bool HasBoundaryRequirements =>
        LeadingBoundary != Utf8BoundaryRequirement.None ||
        TrailingBoundary != Utf8BoundaryRequirement.None;

    private Utf8ConfirmationPlan CreateConfirmationPlan()
    {
        if (!NativeSearch.HasPreparedSearcher && FallbackCandidateEngine.HasValue)
        {
            return new Utf8ConfirmationPlan(Utf8ConfirmationKind.FallbackVerifier);
        }
        if (HasBoundaryRequirements && HasTrailingLiteralRequirement)
        {
            return new Utf8ConfirmationPlan(Utf8ConfirmationKind.BoundaryAndTrailingLiteral);
        }

        if (HasBoundaryRequirements || HasTrailingLiteralRequirement)
        {
            return new Utf8ConfirmationPlan(Utf8ConfirmationKind.BoundaryRequirements);
        }

        return default;
    }

    private Utf8ProjectionPlan CreateProjectionPlan()
    {
        if (!EnumerationStrategy.Semantics.RequiresProjection)
        {
            return default;
        }

        return Kind switch
        {
            Utf8SearchKind.ExactUtf8Literals
                => new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16Incremental),
            _ => new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16BoundaryMap),
        };
    }

    private static PreparedSearcher CreatePreparedSearcher(
        Utf8SearchKind kind,
        PreparedSubstringSearch? literalSearch,
        PreparedMultiLiteralSearch multiLiteralSearch)
    {
        return kind switch
        {
            Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiChar when literalSearch.HasValue
                => new PreparedSearcher(literalSearch.Value, ignoreCase: false),
            Utf8SearchKind.AsciiLiteralIgnoreCase when literalSearch.HasValue
                => new PreparedSearcher(literalSearch.Value, ignoreCase: true),
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals or Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals when multiLiteralSearch.HasValue
                => new PreparedSearcher(multiLiteralSearch),
            _ => default,
        };
    }

    private static Utf8SearchPortfolioKind DeterminePortfolioKind(
        Utf8SearchKind kind,
        PreparedSearcher preparedSearcher,
        PreparedMultiLiteralSearch multiLiteralSearch)
    {
        return kind switch
        {
            Utf8SearchKind.ExactAsciiLiteral
                when preparedSearcher.Kind == PreparedSearcherKind.ExactLiteral
                => Utf8SearchPortfolioKind.ExactLiteral,
            Utf8SearchKind.AsciiLiteralIgnoreCase
                when preparedSearcher.Kind == PreparedSearcherKind.IgnoreCaseLiteral
                => Utf8SearchPortfolioKind.IgnoreCaseLiteral,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactDirect
                => Utf8SearchPortfolioKind.ExactDirectFamily,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactTrie
                => Utf8SearchPortfolioKind.ExactTrieFamily,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton
                => Utf8SearchPortfolioKind.ExactAutomatonFamily,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactPacked
                => Utf8SearchPortfolioKind.ExactPackedFamily,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactEarliest
                => Utf8SearchPortfolioKind.ExactEarliestFamily,
            Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals
                when multiLiteralSearch.Kind == PreparedMultiLiteralKind.AsciiIgnoreCase
                => Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily,
            _ => Utf8SearchPortfolioKind.None,
        };
    }

    private static PreparedMultiLiteralSearch CreateMultiLiteralSearch(
        Utf8SearchKind kind,
        byte[][]? alternateLiteralsUtf8)
    {
        return kind switch
        {
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals when alternateLiteralsUtf8 is { Length: > 0 }
                => new PreparedMultiLiteralSearch(alternateLiteralsUtf8, ignoreCase: false),
            Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals when alternateLiteralsUtf8 is { Length: > 0 }
                => new PreparedMultiLiteralSearch(alternateLiteralsUtf8, ignoreCase: true),
            _ => default,
        };
    }

    private static PreparedSearcher CreateRequiredPrefilter(
        byte[]? requiredPrefilterLiteralUtf8,
        byte[][]? requiredPrefilterAlternateLiteralsUtf8)
    {
        if (requiredPrefilterLiteralUtf8 is { Length: > 0 } literal)
        {
            return new PreparedSearcher(new PreparedSubstringSearch(literal, ignoreCase: false), ignoreCase: false);
        }

        if (requiredPrefilterAlternateLiteralsUtf8 is { Length: > 0 } literals)
        {
            return new PreparedSearcher(new PreparedMultiLiteralSearch(literals, ignoreCase: false));
        }

        return default;
    }

    private static PreparedSearcher CreateSecondaryRequiredPrefilter(
        string? secondaryRequiredPrefilterQuotedAsciiSet,
        int secondaryRequiredPrefilterQuotedAsciiLength)
    {
        return !string.IsNullOrEmpty(secondaryRequiredPrefilterQuotedAsciiSet) &&
               secondaryRequiredPrefilterQuotedAsciiLength > 0
            ? new PreparedSearcher(new PreparedQuotedAsciiRunSearch(
                secondaryRequiredPrefilterQuotedAsciiSet,
                secondaryRequiredPrefilterQuotedAsciiLength))
            : default;
    }

    private static PreparedWindowSearch CreateWindowSearch(
        byte[][]? orderedWindowLeadingLiteralsUtf8,
        byte[]? orderedWindowTrailingLiteralUtf8,
        int? orderedWindowMaxGap,
        bool orderedWindowSameLine)
    {
        if (orderedWindowLeadingLiteralsUtf8 is not { Length: > 0 } leadingLiterals ||
            orderedWindowTrailingLiteralUtf8 is not { Length: > 0 } trailingLiteral)
        {
            return default;
        }

        return new PreparedWindowSearch(
            new PreparedSearcher(new PreparedMultiLiteralSearch(leadingLiterals, ignoreCase: false)),
            new PreparedSearcher(new PreparedSubstringSearch(trailingLiteral, ignoreCase: false), ignoreCase: false),
            orderedWindowMaxGap,
            orderedWindowSameLine);
    }

    private static Utf8StructuralSearchPlan[]? CreateRequiredWindowPrefilterPlans(Utf8WindowSearchInfo[]? requiredWindowPrefilters)
    {
        if (requiredWindowPrefilters is not { Length: > 0 } windowInfos)
        {
            return null;
        }

        var plans = new List<Utf8StructuralSearchPlan>(windowInfos.Length);
        foreach (var windowInfo in windowInfos)
        {
            if (!windowInfo.HasValue)
            {
                continue;
            }

            var leading = CreatePreparedSearcher(windowInfo.Leading);
            var trailing = CreatePreparedSearcher(windowInfo.Trailing);
            if (!leading.HasValue || !trailing.HasValue)
            {
                continue;
            }

            var plan = Utf8StructuralSearchPlan.CreateWindowPlan(
                new PreparedWindowSearch(leading, trailing, windowInfo.MaxGap, sameLine: false),
                windowInfo.MaxLines);
            if (plan.HasValue)
            {
                plans.Add(plan);
            }
        }

        return plans.Count > 0 ? [.. plans] : null;
    }

    private static Utf8StructuralSearchPlan[]? CreateFallbackRequiredWindowPrefilterPlans(
        byte[][]? requiredPrefilterAlternateLiteralsUtf8,
        string? secondaryRequiredPrefilterQuotedAsciiSet,
        int secondaryRequiredPrefilterQuotedAsciiLength)
    {
        if (requiredPrefilterAlternateLiteralsUtf8 is not { Length: > 0 } literals ||
            string.IsNullOrEmpty(secondaryRequiredPrefilterQuotedAsciiSet) ||
            secondaryRequiredPrefilterQuotedAsciiLength <= 0)
        {
            return null;
        }

        var family = new PreparedSearcher(new PreparedMultiLiteralSearch(literals, ignoreCase: false));
        var quoted = new PreparedSearcher(new PreparedQuotedAsciiRunSearch(
            secondaryRequiredPrefilterQuotedAsciiSet,
            secondaryRequiredPrefilterQuotedAsciiLength));

        return
        [
            Utf8StructuralSearchPlan.CreateWindowPlan(
                new PreparedWindowSearch(family, quoted),
                maxLines: 5,
                startTransform: new Utf8FallbackStartTransform(1)),
            Utf8StructuralSearchPlan.CreateWindowPlan(new PreparedWindowSearch(quoted, family), maxLines: 5),
        ];
    }

    private static PreparedSearcher CreatePreparedSearcher(Utf8PreparedSearcherInfo searcherInfo)
    {
        return searcherInfo.Kind switch
        {
            Utf8PreparedSearcherInfoKind.LiteralFamily when searcherInfo.AlternateLiteralsUtf8 is { Length: > 0 } literals
                => new PreparedSearcher(new PreparedMultiLiteralSearch(literals, ignoreCase: false)),
            Utf8PreparedSearcherInfoKind.QuotedAsciiRun when !string.IsNullOrEmpty(searcherInfo.QuotedAsciiSet) && searcherInfo.QuotedAsciiLength > 0
                => new PreparedSearcher(new PreparedQuotedAsciiRunSearch(searcherInfo.QuotedAsciiSet, searcherInfo.QuotedAsciiLength)),
            _ => default,
        };
    }

    private static Utf8StructuralSearchPlan[]? CreateFallbackCandidatePlans(
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralSearchPlan[]? requiredWindowPrefilterPlans)
    {
        if (structuralSearchPlan.HasValue)
        {
            return [structuralSearchPlan];
        }

        return requiredWindowPrefilterPlans is { Length: > 0 }
            ? requiredWindowPrefilterPlans
            : null;
    }
}

internal readonly struct Utf8FixedDistanceSet
{
    public Utf8FixedDistanceSet(int distance, byte[]? chars, bool negated, byte rangeLow = 0, byte rangeHigh = 0, bool hasRange = false)
    {
        Distance = distance;
        Chars = chars;
        Negated = negated;
        RangeLow = rangeLow;
        RangeHigh = rangeHigh;
        HasRange = hasRange;
    }

    public int Distance { get; }

    public byte[]? Chars { get; }

    public bool Negated { get; }

    public byte RangeLow { get; }

    public byte RangeHigh { get; }

    public bool HasRange { get; }
}
