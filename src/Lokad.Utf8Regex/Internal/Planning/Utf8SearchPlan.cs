using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.FrontEnd;
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
        var fixedDistanceSets = NullIfEmpty(facts.FixedDistanceSets);
        var trailingLiteralUtf8 = NullIfEmpty(facts.TrailingLiteralUtf8);
        var fallbackStartTransform = facts.FallbackStartTransform;
        var distance = facts.Distance;
        var minRequiredLength = facts.MinRequiredLength;
        var exactRequiredLength = facts.ExactRequiredLength;
        var maxPossibleLength = facts.MaxPossibleLength;
        var leadingBoundary = facts.LeadingBoundary;
        var trailingBoundary = facts.TrailingBoundary;
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
        // A short exact prefilter can share the same prepared needle as
        // candidate discovery. The runner can then reuse its first occurrence.
        _requiredPrefilterSearcher = requiredPrefilterLiteralUtf8 is { Length: <= 64 } &&
            LiteralSearch is { IgnoreCase: false } exact && exact.Needle.AsSpan().SequenceEqual(requiredPrefilterLiteralUtf8)
            ? new PreparedSearcher(exact, ignoreCase: false)
            : CreateRequiredPrefilter(requiredPrefilterLiteralUtf8, requiredPrefilterAlternateLiteralsUtf8);
        FixedDistanceSets = fixedDistanceSets;
        TrailingLiteralUtf8 = trailingLiteralUtf8;
        FallbackStartTransform = fallbackStartTransform;
        var structuralSearchPlan = Utf8StructuralSearchPlan.Create(
            kind,
            distance,
            canGuideFallbackStarts,
            PreparedSearcher,
            fixedDistanceSets,
            fallbackStartTransform,
            leadingBoundary, trailingBoundary, trailingLiteralUtf8,
            exactRequiredLength, maxPossibleLength);
        StructuralSearchPlan = structuralSearchPlan;
        Distance = distance;
        MinRequiredLength = minRequiredLength;
        ExactRequiredLength = exactRequiredLength;
        MaxPossibleLength = maxPossibleLength;
        LeadingBoundary = leadingBoundary;
        TrailingBoundary = trailingBoundary;
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

    private static T[]? NullIfEmpty<T>(T[] values) => values.Length == 0 ? null : values;

    public Utf8SearchKind Kind { get; }

    public byte[]? LiteralUtf8 { get; }

    public bool CanGuideFallbackStarts { get; }

    private Utf8AlternateLiteralOverlapKind AlternateLiteralOverlap { get; }

    public PreparedSubstringSearch? LiteralSearch { get; }

    public byte[][]? AlternateLiteralsUtf8 { get; }

    public PreparedLiteralSetSearch? AlternateLiteralSearch { get; }

    public PreparedAsciiIgnoreCaseLiteralSetSearch? AlternateIgnoreCaseLiteralSearch { get; }

    public PreparedMultiLiteralSearch MultiLiteralSearch { get; }

    public PreparedSearcher PreparedSearcher { get; }

    public Utf8SearchPortfolioKind PortfolioKind { get; }

    private readonly PreparedSearcher _requiredPrefilterSearcher;

    [UnscopedRef]
    public ref readonly PreparedSearcher RequiredPrefilterSearcher => ref _requiredPrefilterSearcher;

    public Utf8ConfirmationPlan ConfirmationPlan => CreateConfirmationPlan();

    public Utf8SearchOperationPlan SearchOperation =>
        Utf8SearchStrategySelector.Create(this, ConfirmationPlan);

    public AsciiExactLiteralSearchData? AlternateLiteralSearchData => AlternateLiteralSearch?.SearchData;

    public Utf8FixedDistanceSet[]? FixedDistanceSets { get; }

    public byte[]? TrailingLiteralUtf8 { get; }

    public Utf8FallbackStartTransform FallbackStartTransform { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

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

    public bool HasRequiredPrefilter => RequiredPrefilterSearcher.HasValue;

    public bool HasBoundaryRequirements =>
        LeadingBoundary != Utf8BoundaryRequirement.None ||
        TrailingBoundary != Utf8BoundaryRequirement.None;

    private Utf8ConfirmationPlan CreateConfirmationPlan()
    {
        if (!HasPreparedSearcher && HasStructuralCandidates)
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

    private static PreparedSearcher CreatePreparedSearcher(
        Utf8SearchKind kind, PreparedSubstringSearch? literalSearch, PreparedMultiLiteralSearch multiLiteralSearch)
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
        Utf8SearchKind kind, PreparedSearcher preparedSearcher, PreparedMultiLiteralSearch multiLiteralSearch)
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
        Utf8SearchKind kind, byte[][]? alternateLiteralsUtf8)
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
        byte[]? requiredPrefilterLiteralUtf8, byte[][]? requiredPrefilterAlternateLiteralsUtf8)
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
        int distance, AsciiCharClass byteSet)
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
