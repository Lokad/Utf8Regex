using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Planning;

internal readonly struct Utf8StructuralSearchPlan
{
    internal readonly struct Data
    {
        public readonly PreparedSearcher Searcher;
        public Utf8FixedDistanceSet[]? FixedDistanceSets { get; init; }
        public Utf8FallbackStartTransform StartTransform { get; init; }
        public Utf8BoundaryRequirement LeadingBoundary { get; init; }
        public Utf8BoundaryRequirement TrailingBoundary { get; init; }
        public byte[]? TrailingLiteral { get; init; }
        public int ExactLength { get; init; }
        public int MaxLength { get; init; }

        public Data(in PreparedSearcher searcher)
        {
            Searcher = searcher;
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
    public bool RequiresCandidateEndCoverage => Payload.ExactLength > 0;

    public static Utf8StructuralSearchPlan Create(
        Utf8SearchKind kind, int distance, bool canGuideFallbackStarts, in PreparedSearcher preparedSearcher,
        Utf8FixedDistanceSet[]? fixedDistanceSets, Utf8FallbackStartTransform fallbackStartTransform,
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
            if (!useSearcher && !useSets)
            {
                return default;
            }

            var data = new Data(useSearcher ? preparedSearcher : default)
            {
                FixedDistanceSets = useSets ? fixedDistanceSets : null,
                StartTransform = useSets ? fallbackStartTransform : transform,
                LeadingBoundary = leadingBoundary,
                TrailingBoundary = trailingBoundary,
                TrailingLiteral = trailingLiteralUtf8,
                ExactLength = Math.Max(0, exactLength.GetValueOrDefault()),
                MaxLength = exactLength > 0 ? 0 : Math.Max(0, maxLength.GetValueOrDefault()),
            };
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

internal readonly record struct Utf8StructuralCandidate(int StartIndex, int EndIndex, int MatchLength, int LiteralId);
