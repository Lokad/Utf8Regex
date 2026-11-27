using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.Planning;


internal readonly struct Utf8SearchPlan
{
    private sealed class PrefilterStorage(PreparedSearcher searcher)
    {
        internal readonly PreparedSearcher Searcher = searcher;
    }

    private static readonly PreparedSearcher s_emptyPrefilter;

    private Utf8SearchPlan(in Utf8SearchFacts facts, Utf8SliceProgram? scalarProgram, Utf8ConstructionBudget budget)
    {
        var kind = facts.Kind;
        var literalUtf8 = NullIfEmpty(facts.LiteralUtf8);
        var alternateLiteralsUtf8 = NullIfEmpty(facts.AlternateLiteralsUtf8);
        var canGuideFallbackStarts = facts.CanGuideFallbackStarts;
        var requiredPrefilterLiteralUtf8 = NullIfEmpty(facts.RequiredPrefilterLiteralUtf8);
        var requiredPrefilterAlternateLiteralsUtf8 = NullIfEmpty(facts.RequiredPrefilterAlternateLiteralsUtf8);
        var trailingLiteralUtf8 = NullIfEmpty(facts.TrailingLiteralUtf8);
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
        PreparedSubstringSearch? literalSearch = literalUtf8 is not null
            ? new PreparedSubstringSearch(literalUtf8, kind == Utf8SearchKind.AsciiFoldedByteLiteral)
            : null;
        if (scalarProgram is { RightToLeft: false, IsNonBacktracking: false, IgnoreCase: false,
                HasContiguousAnchor: false, RepeatCount: > 0, LeadingByteCandidate.First: < 0 or >= 128 } &&
            literalSearch is { IgnoreCase: false, Length: > 1 and <= 64 } prefixSearch)
        {
            _repeatPrefix = Utf8SliceRunner.PrepareRepeatPrefix(scalarProgram, prefixSearch.Needle, budget);
            if (_repeatPrefix.Any.HasValue) _repeatPrefixProgram = scalarProgram;
        }
        AlternateLiteralsUtf8 = alternateLiteralsUtf8;
        if (kind is Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals &&
            leadingBoundary == Utf8BoundaryRequirement.Boundary &&
            trailingBoundary == Utf8BoundaryRequirement.Boundary && trailingLiteralUtf8 is null &&
            alternateLiteralsUtf8 is { } boundaryLiterals)
        {
            BoundaryFamilyCounter = Utf8PackedNibbleLiteralFamilyCountStrategy.CreateBoundaryCounter(boundaryLiterals);
        }
        PreparedSearcher = CreatePreparedSearcher(kind, literalSearch, CreateMultiLiteralSearch(kind, alternateLiteralsUtf8));
        ref readonly var multiLiteralSearch = ref PreparedSearcher.MultiLiteralSearch;
        // Reuse exact tables or trie nodes already prepared from this family.
        AlternateLiteralSearch = alternateLiteralsUtf8 is { Length: > 0 } && kind != Utf8SearchKind.AsciiFoldedByteLiterals
            ? multiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactDirect
                ? multiLiteralSearch.ExactSearch
                : new PreparedLiteralSetSearch(alternateLiteralsUtf8, multiLiteralSearch.TrieSearch)
            : null;
        PortfolioKind = DeterminePortfolioKind(kind, PreparedSearcher.Kind, multiLiteralSearch.Kind);
        // A short exact prefilter can share the same prepared needle as
        // candidate discovery. The runner can then reuse its first occurrence.
        _prefilterSharesCandidate = PreparedSearcher.Kind == PreparedSearcherKind.ExactLiteral &&
            requiredPrefilterLiteralUtf8 is { Length: <= 64 } &&
            PreparedSearcher.LiteralSearch.Needle.AsSpan().SequenceEqual(requiredPrefilterLiteralUtf8);
        if (!_prefilterSharesCandidate)
            _requiredPrefilter = CreateRequiredPrefilter(requiredPrefilterLiteralUtf8, requiredPrefilterAlternateLiteralsUtf8, budget);
        if (scalarProgram is not null && Utf8SliceRunner.PrepareAlignedDelimiterAlternatives(
            scalarProgram, RequiredPrefilterSearcher, budget))
            AlignedClassDelimiterProgram = scalarProgram;
        TrailingLiteralUtf8 = trailingLiteralUtf8;
        Distance = distance;
        MinRequiredLength = minRequiredLength;
        ExactRequiredLength = exactRequiredLength;
        MaxPossibleLength = maxPossibleLength;
        LeadingBoundary = leadingBoundary;
        TrailingBoundary = trailingBoundary;
    }

    public static Utf8SearchPlan Prepare(in Utf8SearchFacts facts, Utf8SliceProgram? scalarProgram = null)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(1);
            var plan = new Utf8SearchPlan(facts, scalarProgram, budget);
            scope.Complete();
            return plan;
        }
        finally { budget.LeaveDepth(); }
    }

    private static T[]? NullIfEmpty<T>(T[]? values) => values is { Length: > 0 } ? values : null;

    public Utf8SearchKind Kind { get; }

    public byte[]? LiteralUtf8 { get; }

    public bool CanGuideFallbackStarts { get; }

    private Utf8AlternateLiteralOverlapKind AlternateLiteralOverlap { get; }

    public PreparedSubstringSearch? LiteralSearch =>
        PreparedSearcher.Kind is PreparedSearcherKind.ExactLiteral or PreparedSearcherKind.IgnoreCaseLiteral
            ? PreparedSearcher.LiteralSearch : null;

    private readonly Utf8SliceProgram? _repeatPrefixProgram;
    private readonly (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd, bool Terminal) _repeatPrefix;

    internal bool HasTerminalRepeatPrefix => _repeatPrefix.Terminal;

    internal PreparedSubstringSearch? GetRepeatPrefixSearch(Utf8SliceProgram program,
        out (Utf8SearchAsciiSet Any, Utf8SearchAsciiSet Last, int DisjointEnd, bool Terminal) prefix)
    {
        // The alignment pairs this exact needle with these lowered instructions.
        if (ReferenceEquals(_repeatPrefixProgram, program))
        {
            prefix = _repeatPrefix;
            return LiteralSearch;
        }
        prefix = default;
        return null;
    }

    public byte[][]? AlternateLiteralsUtf8 { get; }

    internal Utf8PackedNibbleLiteralFamilyCountStrategy? BoundaryFamilyCounter { get; }

    public readonly PreparedLiteralSetSearch? AlternateLiteralSearch;

    public PreparedAsciiIgnoreCaseLiteralSetSearch? AlternateIgnoreCaseLiteralSearch =>
        MultiLiteralSearch.Kind == PreparedMultiLiteralKind.AsciiIgnoreCase ? MultiLiteralSearch.IgnoreCaseSearch : null;

    public readonly PreparedSearcher PreparedSearcher;

    // Borrow the same literal tables used by the selected searcher.
    [UnscopedRef]
    public ref readonly PreparedMultiLiteralSearch MultiLiteralSearch => ref PreparedSearcher.MultiLiteralSearch;

    public Utf8SearchPortfolioKind PortfolioKind { get; }

    private readonly PrefilterStorage? _requiredPrefilter;
    private readonly bool _prefilterSharesCandidate;

    internal static ulong IndependentPrefilterBytes => Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<PreparedSearcher>());

    // The proof applies to these lowered instructions and this searcher's needles.
    internal Utf8SliceProgram? AlignedClassDelimiterProgram { get; }

    [UnscopedRef]
    public ref readonly PreparedSearcher RequiredPrefilterSearcher
    {
        get
        {
            if (_prefilterSharesCandidate) return ref PreparedSearcher;
            if (_requiredPrefilter is { } prefilter) return ref prefilter.Searcher;
            return ref s_emptyPrefilter;
        }
    }

    public Utf8ConfirmationPlan ConfirmationPlan => CreateConfirmationPlan();

    public Utf8SearchOperationPlan SearchOperation =>
        Utf8SearchStrategySelector.Create(this, ConfirmationPlan);

    public byte[]? TrailingLiteralUtf8 { get; }


    public int Distance { get; }

    public int MinRequiredLength { get; }

    public int? ExactRequiredLength { get; }

    public int? MaxPossibleLength { get; }

    public Utf8BoundaryRequirement LeadingBoundary { get; }

    public Utf8BoundaryRequirement TrailingBoundary { get; }

    public bool HasLiteral => LiteralUtf8 is { Length: > 0 };

    public bool HasPreparedSearcher => PreparedSearcher.HasValue;


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
            Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiLiteral when literalSearch.HasValue
                => new PreparedSearcher(literalSearch.Value, ignoreCase: false),
            Utf8SearchKind.AsciiFoldedByteLiteral when literalSearch.HasValue
                => new PreparedSearcher(literalSearch.Value, ignoreCase: true),
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals or Utf8SearchKind.AsciiFoldedByteLiterals when multiLiteralSearch.HasValue
                => new PreparedSearcher(multiLiteralSearch),
            _ => default,
        };
    }

    private static Utf8SearchPortfolioKind DeterminePortfolioKind(
        Utf8SearchKind kind, PreparedSearcherKind searcherKind, PreparedMultiLiteralKind multiLiteralKind)
    {
        return kind switch
        {
            Utf8SearchKind.ExactAsciiLiteral
                when searcherKind == PreparedSearcherKind.ExactLiteral
                => Utf8SearchPortfolioKind.ExactLiteral,
            Utf8SearchKind.AsciiFoldedByteLiteral
                when searcherKind == PreparedSearcherKind.IgnoreCaseLiteral
                => Utf8SearchPortfolioKind.IgnoreCaseLiteral,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralKind == PreparedMultiLiteralKind.ExactDirect
                => Utf8SearchPortfolioKind.ExactDirectFamily,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralKind == PreparedMultiLiteralKind.ExactTrie
                => Utf8SearchPortfolioKind.ExactTrieFamily,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralKind == PreparedMultiLiteralKind.ExactAutomaton
                => Utf8SearchPortfolioKind.ExactAutomatonFamily,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralKind == PreparedMultiLiteralKind.ExactPacked
                => Utf8SearchPortfolioKind.ExactPackedFamily,
            Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals
                when multiLiteralKind == PreparedMultiLiteralKind.ExactEarliest
                => Utf8SearchPortfolioKind.ExactEarliestFamily,
            Utf8SearchKind.AsciiFoldedByteLiterals
                when multiLiteralKind == PreparedMultiLiteralKind.AsciiIgnoreCase
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

    private static PrefilterStorage? CreateRequiredPrefilter(byte[]? literal, byte[][]? literals, Utf8ConstructionBudget budget)
    {
        PreparedSearcher searcher;
        if (literal is not null)
            searcher = new(new PreparedSubstringSearch(literal, ignoreCase: false), ignoreCase: false);
        else if (literals is not null)
            searcher = new(new PreparedMultiLiteralSearch(literals, ignoreCase: false));
        else
            return null;

        return budget.AllocateObject(searcher, static value => new PrefilterStorage(value));
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
