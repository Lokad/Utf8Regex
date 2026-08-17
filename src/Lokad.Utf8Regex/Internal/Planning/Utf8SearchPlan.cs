using System.Buffers;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.Planning;


internal readonly struct Utf8SearchPlan
{
    private Utf8SearchPlan(Utf8SearchFacts facts)
    {
        var kind = facts.Kind;
        var literalUtf8 = NullIfEmpty(facts.LiteralUtf8);
        var alternateLiteralsUtf8 = NullIfEmpty(facts.AlternateLiteralsUtf8);
        var canGuideFallbackStarts = facts.CanGuideFallbackStarts;
        var requiredPrefilterLiteralUtf8 = NullIfEmpty(facts.RequiredPrefilterLiteralUtf8);
        var requiredPrefilterAlternateLiteralsUtf8 = NullIfEmpty(facts.RequiredPrefilterAlternateLiteralsUtf8);
        var secondaryRequiredPrefilterQuotedAsciiSet = facts.SecondaryRequiredPrefilterQuotedAsciiSet;
        var secondaryRequiredPrefilterQuotedAsciiLength = facts.SecondaryRequiredPrefilterQuotedAsciiLength;
        var fixedDistanceSets = NullIfEmpty(facts.FixedDistanceSets);
        var trailingLiteralUtf8 = NullIfEmpty(facts.TrailingLiteralUtf8);
        var orderedWindowLeadingLiteralsUtf8 = NullIfEmpty(facts.OrderedWindowLeadingLiteralsUtf8);
        var orderedWindowTrailingLiteralUtf8 = NullIfEmpty(facts.OrderedWindowTrailingLiteralUtf8);
        var requiredWindowPrefilters = NullIfEmpty(facts.RequiredWindowPrefilters);
        var orderedWindowMaxGap = facts.OrderedWindowMaxGap;
        var orderedWindowSameLine = facts.OrderedWindowSameLine;
        var fallbackStartTransform = facts.FallbackStartTransform;
        var distance = facts.Distance;
        var minRequiredLength = facts.MinRequiredLength;
        var exactRequiredLength = facts.ExactRequiredLength;
        var maxPossibleLength = facts.MaxPossibleLength;
        var leadingBoundary = facts.LeadingBoundary;
        var trailingBoundary = facts.TrailingBoundary;
        NativeCandidateSource = default;
        FallbackCandidateSource = default;
        ConfirmationPlan = default;
        ProjectionPlan = default;
        CountOperation = default;
        FirstMatchOperation = default;
        EnumerationOperation = default;
        Kind = kind;
        LiteralUtf8 = literalUtf8;
        CanGuideFallbackStarts = canGuideFallbackStarts;
        AlternateLiteralOverlap =
            kind is Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals &&
            (leadingBoundary != Utf8BoundaryRequirement.None ||
             trailingBoundary != Utf8BoundaryRequirement.None ||
             trailingLiteralUtf8 is not null)
            ? GetLiteralOverlap(alternateLiteralsUtf8)
            : Utf8AlternateLiteralOverlapKind.None;
        LiteralSearch = literalUtf8 is not null
            ? new PreparedSubstringSearch(literalUtf8, kind == Utf8SearchKind.AsciiFoldedByteLiteral)
            : null;
        AlternateLiteralsUtf8 = alternateLiteralsUtf8;
        AlternateLiteralUtf16Lengths = alternateLiteralsUtf8 is { Length: > 0 } && kind == Utf8SearchKind.ExactUtf8Literals
            ? [.. alternateLiteralsUtf8.Select(static literal => Utf8Validation.Validate(literal).Utf16Length)]
            : null;
        AlternateLiteralSearch = alternateLiteralsUtf8 is { Length: > 0 } && kind != Utf8SearchKind.AsciiFoldedByteLiterals
            ? new PreparedLiteralSetSearch(alternateLiteralsUtf8)
            : null;
        MultiLiteralSearch = CreateMultiLiteralSearch(kind, alternateLiteralsUtf8);
        AlternateIgnoreCaseLiteralSearch = MultiLiteralSearch.Kind == PreparedMultiLiteralKind.AsciiIgnoreCase
            ? MultiLiteralSearch.IgnoreCaseSearch
            : null;
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
        NativeCandidateSource = HasPreparedSearcher
            ? Utf8CandidateSearchPlan.FromPreparedSearcher(
                PreparedSearcher,
                Utf8SearchSemantics.CandidateScan,
                PortfolioKind)
            : Utf8CandidateSearchPlan.FromStructuralSearch(
                StructuralSearchPlan,
                Utf8SearchSemantics.CandidateScan,
                PortfolioKind);
        FallbackCandidateSource = FallbackSearch.CandidateSource;
        ConfirmationPlan = CreateConfirmationPlan();
        ProjectionPlan = CreateProjectionPlan(Kind);
        CountOperation = Utf8SearchStrategySelector.Create(
            this,
            Utf8SearchSemantics.CountMatches,
            ConfirmationPlan,
            default);
        FirstMatchOperation = Utf8SearchStrategySelector.Create(
            this,
            Utf8SearchSemantics.FirstMatch,
            ConfirmationPlan,
            default);
        EnumerationOperation = Utf8SearchStrategySelector.Create(
            this,
            Utf8SearchSemantics.EnumerateMatches,
            ConfirmationPlan,
            ProjectionPlan);
    }

    public static Utf8SearchPlan Prepare(Utf8SearchFacts facts) => new(facts);

    private static T[]? NullIfEmpty<T>(T[] values) => values.Length == 0 ? null : values;

    public Utf8SearchKind Kind { get; }

    public byte[]? LiteralUtf8 { get; }

    public bool CanGuideFallbackStarts { get; }

    private Utf8AlternateLiteralOverlapKind AlternateLiteralOverlap { get; }

    public PreparedSubstringSearch? LiteralSearch { get; }

    public byte[][]? AlternateLiteralsUtf8 { get; }

    public int[]? AlternateLiteralUtf16Lengths { get; }

    public PreparedLiteralSetSearch? AlternateLiteralSearch { get; }

    public PreparedAsciiIgnoreCaseLiteralSetSearch? AlternateIgnoreCaseLiteralSearch { get; }

    public PreparedMultiLiteralSearch MultiLiteralSearch { get; }

    public PreparedSearcher PreparedSearcher { get; }

    public Utf8SearchPortfolioKind PortfolioKind { get; }

    public byte[]? RequiredPrefilterLiteralUtf8 { get; }

    public byte[][]? RequiredPrefilterAlternateLiteralsUtf8 { get; }

    public Utf8FallbackSearchPlan FallbackSearch { get; }

    public Utf8PrefilterPlan PrefilterPlan { get; }

    public Utf8CandidateSearchPlan NativeCandidateSource { get; }

    public Utf8CandidateSearchPlan FallbackCandidateSource { get; }

    public Utf8ConfirmationPlan ConfirmationPlan { get; }

    public Utf8ProjectionPlan ProjectionPlan { get; }

    public Utf8SearchOperationPlan CountOperation { get; }

    public Utf8SearchOperationPlan FirstMatchOperation { get; }

    public Utf8SearchOperationPlan EnumerationOperation { get; }

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

    public bool HasPreparedSearcher => PreparedSearcher.HasValue;

    public bool HasStructuralCandidates => StructuralSearchPlan.HasValue;

    public bool HasAlternateLiterals => AlternateLiteralsUtf8 is { Length: > 0 };

    public bool HasAlternateLiteralPrefixOverlap =>
        (AlternateLiteralOverlap & Utf8AlternateLiteralOverlapKind.SameStartPrefix) != 0;

    public bool HasAlternateLiteralProperStartOverlap =>
        (AlternateLiteralOverlap & Utf8AlternateLiteralOverlapKind.ProperStart) != 0;

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
        if (!HasPreparedSearcher && FallbackCandidateSource.HasValue)
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

    private static Utf8ProjectionPlan CreateProjectionPlan(Utf8SearchKind kind)
    {
        return kind switch
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
            Utf8SearchKind.AsciiFoldedByteLiteral when literalSearch.HasValue
                => new PreparedSearcher(literalSearch.Value, ignoreCase: true),
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals or Utf8SearchKind.AsciiFoldedByteLiterals when multiLiteralSearch.HasValue
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
            Utf8SearchKind.AsciiFoldedByteLiteral
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
            Utf8SearchKind.AsciiFoldedByteLiterals
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
            Utf8SearchKind.AsciiFoldedByteLiterals when alternateLiteralsUtf8 is { Length: > 0 }
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
        Utf8SearchAsciiSet secondaryRequiredPrefilterQuotedAsciiSet,
        int secondaryRequiredPrefilterQuotedAsciiLength)
    {
        return secondaryRequiredPrefilterQuotedAsciiSet.HasValue &&
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

    private static Utf8StructuralSearchPlan[]? CreateRequiredWindowPrefilterPlans(Utf8WindowSearchFacts[]? requiredWindowPrefilters)
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

            var plan = Utf8StructuralSearchPlan.CreateWindowPlanWithOptionalLineSpan(
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
        Utf8SearchAsciiSet secondaryRequiredPrefilterQuotedAsciiSet,
        int secondaryRequiredPrefilterQuotedAsciiLength)
    {
        if (requiredPrefilterAlternateLiteralsUtf8 is not { Length: > 0 } literals ||
            !secondaryRequiredPrefilterQuotedAsciiSet.HasValue ||
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
            Utf8StructuralSearchPlan.CreateTransformedWindowPlanWithLineSpan(
                new PreparedWindowSearch(family, quoted, maxGap: null, sameLine: false),
                5,
                new Utf8FallbackStartTransform(1, Utf8FallbackStartTransformKind.None)),
            Utf8StructuralSearchPlan.CreateWindowPlanWithLineSpan(
                new PreparedWindowSearch(quoted, family, maxGap: null, sameLine: false),
                5),
        ];
    }

    private static PreparedSearcher CreatePreparedSearcher(Utf8PreparedSearcherInfo searcherInfo)
    {
        return searcherInfo.Kind switch
        {
            Utf8PreparedSearcherInfoKind.LiteralFamily when searcherInfo.AlternateLiteralsUtf8 is { Length: > 0 } literals
                => new PreparedSearcher(new PreparedMultiLiteralSearch(literals, ignoreCase: false)),
            Utf8PreparedSearcherInfoKind.QuotedAsciiRun when searcherInfo.QuotedAsciiSet.HasValue && searcherInfo.QuotedAsciiLength > 0
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

    private static Utf8AlternateLiteralOverlapKind GetLiteralOverlap(byte[][]? literals)
    {
        if (literals is not { Length: > 1 })
        {
            return Utf8AlternateLiteralOverlapKind.None;
        }

        var overlap = Utf8AlternateLiteralOverlapKind.None;
        for (var i = 0; i < literals.Length; i++)
        {
            var candidate = literals[i];
            for (var j = 0; j < literals.Length; j++)
            {
                var alternate = literals[j];
                if ((overlap & Utf8AlternateLiteralOverlapKind.SameStartPrefix) == 0 &&
                    i != j &&
                    candidate.Length != alternate.Length &&
                    (candidate.AsSpan().StartsWith(alternate) || alternate.AsSpan().StartsWith(candidate)))
                {
                    overlap |= Utf8AlternateLiteralOverlapKind.SameStartPrefix;
                }

                if ((overlap & Utf8AlternateLiteralOverlapKind.ProperStart) == 0)
                {
                    for (var offset = 1; offset < candidate.Length; offset++)
                    {
                        var suffix = candidate.AsSpan(offset);
                        if (suffix.StartsWith(alternate) || alternate.AsSpan().StartsWith(suffix))
                        {
                            overlap |= Utf8AlternateLiteralOverlapKind.ProperStart;
                            break;
                        }
                    }
                }

                if (overlap == (Utf8AlternateLiteralOverlapKind.SameStartPrefix | Utf8AlternateLiteralOverlapKind.ProperStart))
                {
                    return overlap;
                }
            }
        }

        return overlap;
    }
}

[Flags]
internal enum Utf8AlternateLiteralOverlapKind : byte
{
    None = 0,
    SameStartPrefix = 1,
    ProperStart = 2,
}

internal readonly struct Utf8FixedDistanceSet
{
    private Utf8FixedDistanceSet(
        int distance,
        AsciiCharClass byteSet)
    {
        Distance = distance;
        ByteSet = byteSet;
        SearchValues = System.Buffers.SearchValues.Create(byteSet.GetMatchBytes());
    }

    public int Distance { get; }

    public AsciiCharClass ByteSet { get; }

    public SearchValues<byte> SearchValues { get; }

    public bool Contains(byte value) => ByteSet.Contains(value);

    public static Utf8FixedDistanceSet FromBytes(int distance, byte[]? chars, bool negated)
        => new(distance, AsciiCharClass.FromBytes(chars ?? [], negated));

    public static Utf8FixedDistanceSet FromBytesAndRange(
        int distance,
        byte[]? chars,
        bool negated,
        byte rangeLow,
        byte rangeHigh)
    {
        var bytes = AsciiCharClass.FromBytes(chars ?? []);
        var range = AsciiCharClass.FromRange(Utf8InclusiveByteRange.Create(rangeLow, rangeHigh));
        return new Utf8FixedDistanceSet(distance, AsciiCharClass.CombinePositive(bytes, range, negated));
    }
}
