using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.Planning;


internal readonly struct Utf8SearchPlan
{
    private readonly Utf8StructuralSearchPlan[]? _fallbackCandidatePlans;

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
        ProjectionPlan = default;
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
            ? CreateAlternateLiteralUtf16Lengths(alternateLiteralsUtf8)
            : null;
        MultiLiteralSearch = CreateMultiLiteralSearch(kind, alternateLiteralsUtf8);
        // Direct portfolios already own the exact tables used by compatibility
        // consumers. Borrow them instead of acquiring the same payload twice.
        AlternateLiteralSearch = alternateLiteralsUtf8 is { Length: > 0 } && kind != Utf8SearchKind.AsciiFoldedByteLiterals
            ? MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactDirect
                ? MultiLiteralSearch.ExactSearch
                : new PreparedLiteralSetSearch(alternateLiteralsUtf8)
            : null;
        AlternateIgnoreCaseLiteralSearch = MultiLiteralSearch.Kind == PreparedMultiLiteralKind.AsciiIgnoreCase
            ? MultiLiteralSearch.IgnoreCaseSearch
            : null;
        PreparedSearcher = CreatePreparedSearcher(kind, LiteralSearch, MultiLiteralSearch);
        PortfolioKind = DeterminePortfolioKind(kind, PreparedSearcher, MultiLiteralSearch);
        RequiredPrefilterAlternateLiteralsUtf8 = requiredPrefilterAlternateLiteralsUtf8;
        var requiredPrefilterSearcher = CreateRequiredPrefilter(requiredPrefilterLiteralUtf8, requiredPrefilterAlternateLiteralsUtf8);
        var secondaryRequiredPrefilterSearcher = CreateSecondaryRequiredPrefilter(
            secondaryRequiredPrefilterQuotedAsciiSet,
            secondaryRequiredPrefilterQuotedAsciiLength);
        FixedDistanceSets = fixedDistanceSets;
        TrailingLiteralUtf8 = trailingLiteralUtf8;
        var requiredWindowPrefilterPlans =
            CreateRequiredWindowPrefilterPlans(requiredWindowPrefilters) ??
            CreateFallbackRequiredWindowPrefilterPlans(
                requiredPrefilterAlternateLiteralsUtf8,
                requiredPrefilterSearcher,
                secondaryRequiredPrefilterSearcher);
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
            fallbackStartTransform,
            leadingBoundary, trailingBoundary, trailingLiteralUtf8,
            exactRequiredLength, maxPossibleLength);
        StructuralSearchPlan = structuralSearchPlan;
        PrefilterPlan = new Utf8PrefilterPlan(
            requiredPrefilterSearcher,
            secondaryRequiredPrefilterSearcher,
            requiredWindowPrefilterPlans);
        _fallbackCandidatePlans = CreateFallbackCandidatePlans(structuralSearchPlan, requiredWindowPrefilterPlans);
        Distance = distance;
        MinRequiredLength = minRequiredLength;
        ExactRequiredLength = exactRequiredLength;
        MaxPossibleLength = maxPossibleLength;
        LeadingBoundary = leadingBoundary;
        TrailingBoundary = trailingBoundary;
        ProjectionPlan = CreateProjectionPlan(Kind);
    }

    public static Utf8SearchPlan Prepare(Utf8SearchFacts facts)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(1);
            var plan = new Utf8SearchPlan(facts);
            scope.Complete();
            return plan;
        }
        finally { budget.LeaveDepth(); }
    }

    // Derived string-coordinate cache only. The engine discovers byte extents
    // over scalars; this table avoids re-reading each matched literal when a
    // caller requests UTF-16 interoperability coordinates.
    private static int[] CreateAlternateLiteralUtf16Lengths(byte[][] literals)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var lengths = budget.AllocateArray<int>(literals.Length, false);
            for (var index = 0; index < literals.Length; index++)
            {
                budget.ChargeWork(1 + (ulong)literals[index].Length);
                lengths[index] = Utf8Validation.Validate(literals[index]).Utf16Length;
            }
            scope.Complete();
            return lengths;
        }
        finally { budget.LeaveDepth(); }
    }

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

    public byte[][]? RequiredPrefilterAlternateLiteralsUtf8 { get; }

    public Utf8FallbackSearchPlan FallbackSearch => new(PrefilterPlan, _fallbackCandidatePlans);

    public Utf8PrefilterPlan PrefilterPlan { get; }

    public Utf8CandidateSearchPlan NativeCandidateSource => HasPreparedSearcher
        ? Utf8CandidateSearchPlan.FromPreparedSearcher(PreparedSearcher, PortfolioKind)
        : Utf8CandidateSearchPlan.FromStructuralSearch(StructuralSearchPlan, PortfolioKind);

    public Utf8CandidateSearchPlan FallbackCandidateSource =>
        Utf8CandidateSearchPlan.FromStructuralSearchSet(_fallbackCandidatePlans);

    public Utf8ConfirmationPlan ConfirmationPlan => CreateConfirmationPlan();

    public Utf8ProjectionPlan ProjectionPlan { get; }

    public Utf8SearchOperationPlan CountOperation =>
        Utf8SearchStrategySelector.Create(this, ConfirmationPlan, default);

    public Utf8SearchOperationPlan FirstMatchOperation =>
        Utf8SearchStrategySelector.Create(this, ConfirmationPlan, default);

    public Utf8SearchOperationPlan EnumerationOperation =>
        Utf8SearchStrategySelector.Create(this, ConfirmationPlan, ProjectionPlan);

    public PreparedSearcher RequiredPrefilterSearcher => PrefilterPlan.PrimarySearcher;

    public PreparedSearcher SecondaryRequiredPrefilterSearcher => PrefilterPlan.SecondarySearcher;

    public AsciiExactLiteralSearchData? AlternateLiteralSearchData => AlternateLiteralSearch?.SearchData;

    public Utf8FixedDistanceSet[]? FixedDistanceSets { get; }

    public byte[]? TrailingLiteralUtf8 { get; }

    public Utf8StructuralSearchPlan[]? RequiredWindowPrefilterPlans => PrefilterPlan.WindowPlans;

    public int? OrderedWindowMaxGap { get; }

    public bool OrderedWindowSameLine { get; }

    public Utf8FallbackStartTransform FallbackStartTransform { get; }

    public PreparedWindowSearch WindowSearch { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8StructuralSearchPlan[]? FallbackCandidatePlans => _fallbackCandidatePlans;

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

    public bool HasTrailingLiteralRequirement => TrailingLiteralUtf8 is { Length: > 0 };

    public bool HasRequiredPrefilter => PrefilterPlan.HasValue;

    public bool HasWindowSearch => WindowSearch.HasValue;

    public bool HasFallbackCandidates => _fallbackCandidatePlans is { Length: > 0 };

    public bool HasBoundaryRequirements =>
        LeadingBoundary != Utf8BoundaryRequirement.None ||
        TrailingBoundary != Utf8BoundaryRequirement.None;

    private Utf8ConfirmationPlan CreateConfirmationPlan()
    {
        if (!HasPreparedSearcher && FallbackCandidateSource.HasValue)
        {
            return new Utf8ConfirmationPlan(Utf8ConfirmationKind.ScalarVerifier);
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
        if (requiredWindowPrefilters is not { Length: > 0 } windowInfos) return null;
        var budget = Utf8ConstructionScope.CurrentBudget;
        var count = 0;
        foreach (var info in windowInfos)
        {
            budget.ChargeWork(1);
            // Both endpoints must qualify before either child acquires storage.
            if (CanPrepare(info.Leading) && CanPrepare(info.Trailing)) count = checked(count + 1);
        }
        if (count == 0) return null;
        var plans = budget.AllocateArray<Utf8StructuralSearchPlan>(count, false);
        var index = 0;
        foreach (var info in windowInfos)
        {
            budget.ChargeWork(1);
            if (!CanPrepare(info.Leading) || !CanPrepare(info.Trailing)) continue;
            var leading = CreatePreparedSearcher(info.Leading);
            var trailing = CreatePreparedSearcher(info.Trailing);
            plans[index++] = Utf8StructuralSearchPlan.CreateWindowPlanWithOptionalLineSpan(
                new PreparedWindowSearch(leading, trailing, maxGap: null, sameLine: false), info.MaxLines);
        }
        return plans;
    }

    private static bool CanPrepare(Utf8PreparedSearcherInfo info) => info.Kind switch
    {
        Utf8PreparedSearcherInfoKind.LiteralFamily => info.AlternateLiteralsUtf8 is { Length: > 0 },
        Utf8PreparedSearcherInfoKind.QuotedAsciiRun => info.QuotedAsciiSet.HasValue && info.QuotedAsciiLength > 0,
        _ => false,
    };

    private static Utf8StructuralSearchPlan[]? CreateFallbackRequiredWindowPrefilterPlans(
        byte[][]? requiredPrefilterAlternateLiteralsUtf8,
        in PreparedSearcher requiredPrefilterSearcher,
        in PreparedSearcher secondaryRequiredPrefilterSearcher)
    {
        if (requiredPrefilterAlternateLiteralsUtf8 is not { Length: > 0 } literals ||
            !secondaryRequiredPrefilterSearcher.HasValue) return null;
        var plans = Utf8ConstructionScope.CurrentBudget.AllocateArray<Utf8StructuralSearchPlan>(2, false);
        // A separate mandatory literal takes priority over the family in the
        // primary prefilter. Only a primary family supplies this window source.
        var family = requiredPrefilterSearcher.Kind == PreparedSearcherKind.MultiLiteral
            ? requiredPrefilterSearcher
            : new PreparedSearcher(new PreparedMultiLiteralSearch(literals, ignoreCase: false));
        var quoted = secondaryRequiredPrefilterSearcher;
        plans[0] = Utf8StructuralSearchPlan.CreateTransformedWindowPlanWithLineSpan(
            new PreparedWindowSearch(family, quoted, maxGap: null, sameLine: false), 5,
            new Utf8FallbackStartTransform(1, Utf8FallbackStartTransformKind.None));
        plans[1] = Utf8StructuralSearchPlan.CreateWindowPlanWithLineSpan(
            new PreparedWindowSearch(quoted, family, maxGap: null, sameLine: false), 5);
        return plans;
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
            var plans = Utf8ConstructionScope.CurrentBudget.AllocateArray<Utf8StructuralSearchPlan>(1, false);
            plans[0] = structuralSearchPlan;
            return plans;
        }

        return requiredWindowPrefilterPlans is { Length: > 0 }
            ? requiredWindowPrefilterPlans
            : null;
    }

    internal static Utf8AlternateLiteralOverlapKind GetLiteralOverlap(byte[][]? literals)
    {
        Utf8ConstructionScope.ChargeWork(1);
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
                Utf8ConstructionScope.ChargeWork(1);
                var alternate = literals[j];
                if ((overlap & Utf8AlternateLiteralOverlapKind.SameStartPrefix) == 0 &&
                    i != j &&
                    candidate.Length != alternate.Length &&
                    HaveCommonStart(candidate, 0, alternate))
                {
                    overlap |= Utf8AlternateLiteralOverlapKind.SameStartPrefix;
                }

                if ((overlap & Utf8AlternateLiteralOverlapKind.ProperStart) == 0)
                {
                    for (var offset = 1; offset < candidate.Length; offset++)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        if (HaveCommonStart(candidate, offset, alternate))
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

    private static bool HaveCommonStart(byte[] first, int offset, byte[] second)
    {
        // These are preparation-owned literals, never a subject input surface.
        Utf8ConstructionScope.ChargeWork(2ul * (ulong)Math.Min(first.Length - offset, second.Length));
        var suffix = first.AsSpan(offset);
        return suffix.StartsWith(second) || second.AsSpan().StartsWith(suffix);
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
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            Distance = distance;
            ByteSet = byteSet;
            Search = Utf8PreparedByteSearchFactory.CreateSet(byteSet);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    public int Distance { get; }

    public AsciiCharClass ByteSet { get; }

    public PreparedByteSet Search { get; }

    public bool Contains(byte value) => ByteSet.Contains(value);

    internal static Utf8FixedDistanceSet FromMask(int distance, AsciiCharClass byteSet) => new(distance, byteSet);

    public static Utf8FixedDistanceSet FromBytes(int distance, byte[]? chars, bool negated)
        => new(distance, AsciiCharClass.FromBytes(chars ?? [], negated));

}
