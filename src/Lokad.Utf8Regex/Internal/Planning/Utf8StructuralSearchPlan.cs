using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8StructuralSearchYieldKind : byte
{
    None = 0, Start = 1, Window = 2
}

internal readonly struct Utf8StructuralSearchPlan
{
    internal readonly struct Data
    {
        public Utf8StructuralSearchYieldKind YieldKind { get; init; }
        public readonly PreparedSearcher Searcher;
        public readonly PreparedWindowSearch WindowSearch;
        public Utf8FixedDistanceSet[]? FixedDistanceSets { get; init; }
        public Utf8FallbackStartTransform StartTransform { get; init; }
        public Utf8BoundaryRequirement LeadingBoundary { get; init; }
        public Utf8BoundaryRequirement TrailingBoundary { get; init; }
        public byte[]? TrailingLiteral { get; init; }
        public int ExactLength { get; init; }
        public int MaxLength { get; init; }
        public int MaxLines { get; init; }

        public Data(in PreparedSearcher searcher, in PreparedWindowSearch windowSearch)
        {
            Searcher = searcher;
            WindowSearch = windowSearch;
        }
    }

    private sealed class Storage
    {
        internal readonly Data Payload;

        internal Storage(in Data data) => Payload = data;
    }

    private static readonly Data s_empty;
    private readonly Storage? _storage;

    private Utf8StructuralSearchPlan(Storage storage) => _storage = storage;

    public ref readonly Data Payload => ref (_storage is { } storage ? ref storage.Payload : ref s_empty);
    public bool HasValue => _storage is not null;
    public Utf8StructuralSearchYieldKind YieldKind => Payload.YieldKind;
    public bool RequiresCandidateEndCoverage => YieldKind == Utf8StructuralSearchYieldKind.Window || Payload.ExactLength > 0;

    public static Utf8StructuralSearchPlan Create(
        Utf8SearchKind kind, int distance, bool canGuideFallbackStarts, in PreparedSearcher preparedSearcher,
        in PreparedWindowSearch windowSearch, Utf8FixedDistanceSet[]? fixedDistanceSets, Utf8FallbackStartTransform fallbackStartTransform,
        Utf8BoundaryRequirement leadingBoundary = default, Utf8BoundaryRequirement trailingBoundary = default,
        byte[]? trailingLiteralUtf8 = null, int? exactLength = null, int? maxLength = null)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(1);
            var transform = kind is Utf8SearchKind.FixedDistanceAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiChar
                ? fallbackStartTransform.WithAdditionalOffset(distance) : fallbackStartTransform;
            var useSearcher = preparedSearcher.HasValue && (canGuideFallbackStarts || transform.HasValue ||
                kind is Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiLiteral or Utf8SearchKind.AsciiFoldedByteLiteral or
                    Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.AsciiFoldedByteLiterals or Utf8SearchKind.ExactUtf8Literals);
            var useSets = !useSearcher && kind == Utf8SearchKind.FixedDistanceAsciiSets && fixedDistanceSets is { Length: > 0 };
            Utf8StructuralSearchPlan result;
            if (useSearcher || useSets)
            {
                result = Build(new Data(useSearcher ? preparedSearcher : default, default)
                {
                    YieldKind = Utf8StructuralSearchYieldKind.Start,
                    FixedDistanceSets = useSets ? fixedDistanceSets : null,
                    StartTransform = useSets ? fallbackStartTransform : transform,
                    LeadingBoundary = leadingBoundary,
                    TrailingBoundary = trailingBoundary,
                    TrailingLiteral = trailingLiteralUtf8,
                    ExactLength = Math.Max(0, exactLength.GetValueOrDefault()),
                    MaxLength = exactLength > 0 ? 0 : Math.Max(0, maxLength.GetValueOrDefault()),
                });
            }
            else if (windowSearch.HasValue)
            {
                result = CreateWindowPlan(windowSearch, leadingBoundary: leadingBoundary, trailingBoundary: trailingBoundary);
            }
            else return default;

            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    public static Utf8StructuralSearchPlan CreateStartPlan(in PreparedSearcher searcher)
        => searcher.HasValue ? Build(new Data(searcher, default) { YieldKind = Utf8StructuralSearchYieldKind.Start }) : default;

    public static Utf8StructuralSearchPlan CreateWindowPlan(
        in PreparedWindowSearch windowSearch,
        int? maxLines = null,
        Utf8FallbackStartTransform startTransform = default,
        Utf8BoundaryRequirement leadingBoundary = default,
        Utf8BoundaryRequirement trailingBoundary = default)
        => windowSearch.HasValue ? Build(new Data(default, windowSearch)
        {
            YieldKind = Utf8StructuralSearchYieldKind.Window,
            StartTransform = startTransform,
            LeadingBoundary = leadingBoundary,
            TrailingBoundary = trailingBoundary,
            MaxLines = windowSearch.SameLine ? 1 : Math.Max(0, maxLines.GetValueOrDefault()),
        }) : default;

    // Searchers and literals keep their existing loans. The plan owns a single
    // descriptor with one candidate source and the requirements applied to it.
    private static Utf8StructuralSearchPlan Build(in Data data)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(1);
            var bytes = Utf8ConstructionBudget.ObjectBytes<Data>();
            budget.CheckBytes(bytes);
            budget.ChargeWork(2);
            budget.ReserveBytes(bytes);
            var storage = new Storage(in data);
            scope.Complete();
            return new(storage);
        }
        finally { budget.LeaveDepth(); }
    }
}

internal readonly record struct Utf8StructuralCandidate(
    int StartIndex, int EndIndex, int MatchLength, int LiteralId, int TrailingIndex, int TrailingMatchLength, int TrailingLiteralId)
{
    public static Utf8StructuralCandidate Start(
        int startIndex, int endIndex, int matchLength, int literalId) =>
        new(startIndex, endIndex, matchLength, literalId, -1, -1, -1);

    public static Utf8StructuralCandidate Window(
        int startIndex, int endIndex, int matchLength, int literalId, int trailingIndex, int trailingMatchLength, int trailingLiteralId) =>
        new(
            startIndex,
            endIndex,
            matchLength,
            literalId,
            trailingIndex,
            trailingMatchLength,
            trailingLiteralId);
}

internal readonly record struct Utf8StructuralSearchState(PreparedSearchScanState SearchState, PreparedWindowScanState WindowState);
