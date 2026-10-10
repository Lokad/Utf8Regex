using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
using System.Numerics;

namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8StructuralSearchYieldKind : byte
{
    None = 0,
    Start = 1,
    Window = 2,
}

internal enum Utf8StructuralSearchStageKind : byte
{
    FindLiteralFamily = 0,
    FindAscii = 1,
    FindWindow = 2,
    TransformCandidateStart = 3,
    RequireByteAtOffset = 4,
    RequireLiteralAtOffset = 5,
    RequireMinLength = 6,
    RequireWithinByteSpan = 7,
    RequireWithinLineSpan = 8,
    RequireLeadingBoundary = 9,
    RequireTrailingBoundary = 10,
    RequireTrailingLiteral = 11,
    RequireExactLength = 12,
    BoundMaxLength = 13,
    YieldStart = 14,
    YieldWindow = 15,
}

internal readonly struct Utf8StructuralSearchStage
{
    private readonly record struct Data
    {
        public PreparedSearcher Searcher { get; init; }
        public PreparedWindowSearch WindowSearch { get; init; }
        public Utf8FallbackStartTransform StartTransform { get; init; }
        public Execution.PreparedAsciiFindPlan AsciiFindPlan { get; init; }
        public int ByteOffset { get; init; }
        public bool HasLiteralByte { get; init; }
        public byte LiteralByte { get; init; }
        public bool HasSet { get; init; }
        public AsciiCharClass Set { get; init; }
        public byte[] LiteralUtf8 { get; init; }
        public int MinLength { get; init; }
        public int MaxSpan { get; init; }
        public int MaxLines { get; init; }
        public Utf8BoundaryRequirement BoundaryRequirement { get; init; }
    }

    private Utf8StructuralSearchStage(Utf8StructuralSearchStageKind kind, Data data)
    {
        Kind = kind;
        Searcher = data.Searcher;
        WindowSearch = data.WindowSearch;
        StartTransform = data.StartTransform;
        AsciiFindPlan = data.AsciiFindPlan;
        ByteOffset = data.ByteOffset;
        HasLiteralByte = data.HasLiteralByte;
        LiteralByte = data.LiteralByte;
        HasSet = data.HasSet;
        Set = data.Set;
        LiteralUtf8 = data.LiteralUtf8 ?? [];
        MinLength = data.MinLength;
        MaxSpan = data.MaxSpan;
        MaxLines = data.MaxLines;
        BoundaryRequirement = data.BoundaryRequirement;
    }

    public Utf8StructuralSearchStageKind Kind { get; }

    public PreparedSearcher Searcher { get; }

    public PreparedWindowSearch WindowSearch { get; }

    public Utf8FallbackStartTransform StartTransform { get; }

    public Execution.PreparedAsciiFindPlan AsciiFindPlan { get; }

    public int ByteOffset { get; }

    public bool HasLiteralByte { get; }

    public byte LiteralByte { get; }

    public bool HasSet { get; }

    public AsciiCharClass Set { get; }

    public byte[] LiteralUtf8 { get; }

    public int MinLength { get; }

    public int MaxSpan { get; }

    public int MaxLines { get; }

    public Utf8BoundaryRequirement BoundaryRequirement { get; }

    public static Utf8StructuralSearchStage FindLiteralFamily(PreparedSearcher searcher) =>
        new(Utf8StructuralSearchStageKind.FindLiteralFamily, new Data { Searcher = searcher });

    public static Utf8StructuralSearchStage FindWindow(PreparedWindowSearch windowSearch) =>
        new(Utf8StructuralSearchStageKind.FindWindow, new Data { WindowSearch = windowSearch });

    public static Utf8StructuralSearchStage FindAscii(Execution.PreparedAsciiFindPlan asciiFindPlan) =>
        new(Utf8StructuralSearchStageKind.FindAscii, new Data { AsciiFindPlan = asciiFindPlan });

    public static Utf8StructuralSearchStage TransformCandidateStart(Utf8FallbackStartTransform startTransform) =>
        new(Utf8StructuralSearchStageKind.TransformCandidateStart, new Data { StartTransform = startTransform });

    public static Utf8StructuralSearchStage RequireByteAtOffset(int byteOffset, byte literalByte) =>
        new(Utf8StructuralSearchStageKind.RequireByteAtOffset, new Data { ByteOffset = byteOffset, HasLiteralByte = true, LiteralByte = literalByte });

    public static Utf8StructuralSearchStage RequireAsciiClassAtOffset(int byteOffset, AsciiCharClass set) =>
        new(Utf8StructuralSearchStageKind.RequireByteAtOffset, new Data { ByteOffset = byteOffset, HasSet = true, Set = set });

    public static Utf8StructuralSearchStage RequireLiteralAtOffset(int byteOffset, byte[] literalUtf8) =>
        new(Utf8StructuralSearchStageKind.RequireLiteralAtOffset, new Data { ByteOffset = byteOffset, LiteralUtf8 = literalUtf8 });

    public static Utf8StructuralSearchStage RequireMinLength(int minLength) =>
        new(Utf8StructuralSearchStageKind.RequireMinLength, new Data { MinLength = minLength });

    public static Utf8StructuralSearchStage RequireWithinByteSpan(int maxSpan) =>
        new(Utf8StructuralSearchStageKind.RequireWithinByteSpan, new Data { MaxSpan = maxSpan });

    public static Utf8StructuralSearchStage RequireWithinLineSpan(int maxLines) =>
        new(Utf8StructuralSearchStageKind.RequireWithinLineSpan, new Data { MaxLines = maxLines });

    public static Utf8StructuralSearchStage RequireLeadingBoundary(Utf8BoundaryRequirement boundaryRequirement) =>
        new(Utf8StructuralSearchStageKind.RequireLeadingBoundary, new Data { BoundaryRequirement = boundaryRequirement });

    public static Utf8StructuralSearchStage RequireTrailingBoundary(Utf8BoundaryRequirement boundaryRequirement) =>
        new(Utf8StructuralSearchStageKind.RequireTrailingBoundary, new Data { BoundaryRequirement = boundaryRequirement });

    public static Utf8StructuralSearchStage RequireTrailingLiteral(byte[] literalUtf8) =>
        new(Utf8StructuralSearchStageKind.RequireTrailingLiteral, new Data { LiteralUtf8 = literalUtf8 });

    public static Utf8StructuralSearchStage RequireExactLength(int exactLength) =>
        new(Utf8StructuralSearchStageKind.RequireExactLength, new Data { MinLength = exactLength });

    public static Utf8StructuralSearchStage BoundMaxLength(int maxLength) =>
        new(Utf8StructuralSearchStageKind.BoundMaxLength, new Data { MaxSpan = maxLength });

    public static Utf8StructuralSearchStage YieldStart() =>
        new(Utf8StructuralSearchStageKind.YieldStart, default);

    public static Utf8StructuralSearchStage YieldWindow() =>
        new(Utf8StructuralSearchStageKind.YieldWindow, default);
}

internal readonly struct Utf8StructuralSearchPlan
{
    private readonly Utf8StructuralSearchStage[]? _stages;

    public Utf8StructuralSearchPlan(Utf8StructuralSearchYieldKind yieldKind, Utf8StructuralSearchStage[] stages)
    {
        YieldKind = yieldKind;
        _stages = stages;
    }

    public Utf8StructuralSearchYieldKind YieldKind { get; }

    public Utf8StructuralSearchStage[] Stages => _stages ?? [];

    public bool HasValue => Stages is { Length: > 0 };

    public static Utf8StructuralSearchPlan Create(
        Utf8SearchKind kind, int distance, bool canGuideFallbackStarts,
        PreparedSearcher preparedSearcher, PreparedWindowSearch windowSearch,
        Utf8FixedDistanceSet[]? fixedDistanceSets, Utf8FallbackStartTransform fallbackStartTransform)
        => Create(kind, distance, canGuideFallbackStarts, null, preparedSearcher,
            windowSearch, fixedDistanceSets, fallbackStartTransform);

    public static Utf8StructuralSearchPlan Create(
        Utf8SearchKind kind, int distance, bool canGuideFallbackStarts, byte[]? literalUtf8,
        PreparedSearcher preparedSearcher, PreparedWindowSearch windowSearch,
        Utf8FixedDistanceSet[]? fixedDistanceSets, Utf8FallbackStartTransform fallbackStartTransform)
        => Create(kind, distance, canGuideFallbackStarts, literalUtf8, preparedSearcher,
            windowSearch, fixedDistanceSets, fallbackStartTransform, default, default, null, null, null);

    public static Utf8StructuralSearchPlan Create(
        Utf8SearchKind kind, int distance, bool canGuideFallbackStarts, byte[]? literalUtf8,
        PreparedSearcher preparedSearcher, PreparedWindowSearch windowSearch,
        Utf8FixedDistanceSet[]? fixedDistanceSets, Utf8FallbackStartTransform fallbackStartTransform,
        Utf8BoundaryRequirement leadingBoundary,
        Utf8BoundaryRequirement trailingBoundary,
        byte[]? trailingLiteralUtf8, int? exactLength, int? maxLength)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(1);
            var transform = kind is Utf8SearchKind.FixedDistanceAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiChar
                ? fallbackStartTransform.WithAdditionalOffset(distance) : fallbackStartTransform;
            Utf8StructuralSearchStage first;
            Utf8StructuralSearchYieldKind yieldKind;
            if (preparedSearcher.HasValue && (canGuideFallbackStarts || transform.HasValue ||
                kind is Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.AsciiFoldedByteLiteral or
                    Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.AsciiFoldedByteLiterals or Utf8SearchKind.ExactUtf8Literals))
            {
                first = Utf8StructuralSearchStage.FindLiteralFamily(preparedSearcher);
                yieldKind = Utf8StructuralSearchYieldKind.Start;
            }
            else if (kind == Utf8SearchKind.FixedDistanceAsciiLiteral && literalUtf8 is { Length: > 0 })
            {
                first = Utf8StructuralSearchStage.FindAscii(PreparedAsciiFindPlan.CreateFixedDistanceLiteral(literalUtf8, distance));
                transform = fallbackStartTransform;
                yieldKind = Utf8StructuralSearchYieldKind.Start;
            }
            else if (kind == Utf8SearchKind.FixedDistanceAsciiSets && fixedDistanceSets is { Length: > 0 })
            {
                first = Utf8StructuralSearchStage.FindAscii(PreparedAsciiFindPlan.CreateFixedDistanceSet(fixedDistanceSets));
                transform = fallbackStartTransform;
                yieldKind = Utf8StructuralSearchYieldKind.Start;
            }
            else if (windowSearch.HasValue)
            {
                first = Utf8StructuralSearchStage.FindWindow(windowSearch);
                transform = default;
                yieldKind = Utf8StructuralSearchYieldKind.Window;
            }
            else return default;

            var result = Build(first, yieldKind, transform, leadingBoundary, trailingBoundary,
                trailingLiteralUtf8, exactLength, maxLength, null);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    public static Utf8StructuralSearchPlan CreateStartPlan(PreparedSearcher searcher)
        => CreateStartPlan(searcher, null);

    public static Utf8StructuralSearchPlan CreateStartPlan(PreparedSearcher searcher, int? exactLength)
        => searcher.HasValue ? Build(Utf8StructuralSearchStage.FindLiteralFamily(searcher),
            Utf8StructuralSearchYieldKind.Start, default, default, default, null, exactLength, null, null) : default;

    public static Utf8StructuralSearchPlan CreateWindowPlan(PreparedWindowSearch windowSearch)
        => CreateWindowPlan(windowSearch, null, default);

    public static Utf8StructuralSearchPlan CreateWindowPlanWithOptionalLineSpan(PreparedWindowSearch windowSearch, int? maxLines)
        => CreateWindowPlan(windowSearch, maxLines, default);

    public static Utf8StructuralSearchPlan CreateWindowPlanWithLineSpan(PreparedWindowSearch windowSearch, int maxLines)
        => CreateWindowPlan(windowSearch, maxLines, default);

    public static Utf8StructuralSearchPlan CreateTransformedWindowPlanWithLineSpan(
        PreparedWindowSearch windowSearch, int maxLines, Utf8FallbackStartTransform startTransform)
        => CreateWindowPlan(windowSearch, maxLines, startTransform);

    private static Utf8StructuralSearchPlan CreateWindowPlan(
        PreparedWindowSearch windowSearch, int? maxLines, Utf8FallbackStartTransform startTransform)
        => windowSearch.HasValue ? Build(Utf8StructuralSearchStage.FindWindow(windowSearch),
            Utf8StructuralSearchYieldKind.Window, startTransform, default, default, null, null, null, maxLines) : default;

    // Construct the selected plan once, including its bounds and assertions.
    // Searchers and literal payloads retain their own loans; this owns only the
    // final stage vector. Native stages share byte admission, not scalar VM slots.
    private static Utf8StructuralSearchPlan Build(
        Utf8StructuralSearchStage first, Utf8StructuralSearchYieldKind yieldKind,
        Utf8FallbackStartTransform transform, Utf8BoundaryRequirement leading,
        Utf8BoundaryRequirement trailing, byte[]? trailingLiteral,
        int? exactLength, int? maxLength, int? maxLines)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(1);
            var start = yieldKind == Utf8StructuralSearchYieldKind.Start;
            var gapValue = first.WindowSearch.MaxGap.GetValueOrDefault();
            var lineValue = maxLines.GetValueOrDefault();
            var exactValue = exactLength.GetValueOrDefault();
            var maxValue = maxLength.GetValueOrDefault();
            var trailingBytes = trailingLiteral ?? [];
            var gap = !start && gapValue > 0;
            var sameLine = !start && first.WindowSearch.SameLine;
            var lines = !start && lineValue > 0;
            var literal = start && trailingBytes.Length > 0;
            var exact = start && exactValue > 0;
            var bound = start && !exact && maxValue > 0;
            var count = 2 + (transform.HasValue ? 1 : 0) + (gap ? 1 : 0) + (sameLine ? 1 : 0) + (lines ? 1 : 0)
                + (leading != Utf8BoundaryRequirement.None ? 1 : 0) + (trailing != Utf8BoundaryRequirement.None ? 1 : 0)
                + (literal ? 1 : 0) + (exact ? 1 : 0) + (bound ? 1 : 0);
            var stages = budget.AllocateArray<Utf8StructuralSearchStage>(count, false);
            var index = 0;
            stages[index++] = first;
            if (transform.HasValue) stages[index++] = Utf8StructuralSearchStage.TransformCandidateStart(transform);
            if (gap) stages[index++] = Utf8StructuralSearchStage.RequireWithinByteSpan(gapValue);
            if (sameLine) stages[index++] = Utf8StructuralSearchStage.RequireWithinLineSpan(1);
            if (lines) stages[index++] = Utf8StructuralSearchStage.RequireWithinLineSpan(lineValue);
            if (leading != Utf8BoundaryRequirement.None) stages[index++] = Utf8StructuralSearchStage.RequireLeadingBoundary(leading);
            if (trailing != Utf8BoundaryRequirement.None) stages[index++] = Utf8StructuralSearchStage.RequireTrailingBoundary(trailing);
            if (literal) stages[index++] = Utf8StructuralSearchStage.RequireTrailingLiteral(trailingBytes);
            if (exact) stages[index++] = Utf8StructuralSearchStage.RequireExactLength(exactValue);
            if (bound) stages[index++] = Utf8StructuralSearchStage.BoundMaxLength(maxValue);
            stages[index] = start ? Utf8StructuralSearchStage.YieldStart() : Utf8StructuralSearchStage.YieldWindow();
            scope.Complete();
            return new(yieldKind, stages);
        }
        finally { budget.LeaveDepth(); }
    }

    // The original plan may still be retained by its caller. Borrow it and all
    // guard payloads, and transfer one new vector rather than a chain of copies.
    public Utf8StructuralSearchPlan WithVerificationGuards(Utf8DeterministicVerifierGuards guards)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Start || Stages is not { Length: > 0 } stages) return this;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var yieldIndex = FindLastYield(stages, Utf8StructuralSearchStageKind.YieldStart, budget);
            if (yieldIndex < 0) return this;
            var count = guards.PrefixGuards?.Length ?? 0;
            if (guards.PrefixGuards is { } prefix)
                foreach (var guard in prefix)
                {
                    budget.ChargeWork(1);
                    if (guard.Literal is null && guard.CharClass is null)
                        throw new InvalidOperationException("A deterministic byte guard requires a literal or set.");
                }
            var fixedBytes = guards.FixedLiteralUtf8 ?? [];
            var literal = fixedBytes.Length > 0;
            if (literal) count = checked(count + 1);
            if (guards.FixedDistanceSets is { } sets)
                foreach (var set in sets)
                { budget.ChargeWork(1); if (!set.ByteSet.Negated && !set.ByteSet.IsEmpty) count = checked(count + 1); }
            if (guards.MinRequiredLength > 0) count = checked(count + 1);
            if (count == 0) return this;
            var enriched = budget.AllocateArray<Utf8StructuralSearchStage>(checked(stages.Length + count), false);
            budget.ChargeWork((ulong)stages.Length);
            Array.Copy(stages, 0, enriched, 0, yieldIndex);
            var index = yieldIndex;
            if (guards.PrefixGuards is { } prefixGuards)
                foreach (var guard in prefixGuards)
                {
                    if (guard.Literal is { } value) enriched[index++] = Utf8StructuralSearchStage.RequireByteAtOffset(guard.Offset, value);
                    else if (guard.CharClass is { } set) enriched[index++] = Utf8StructuralSearchStage.RequireAsciiClassAtOffset(guard.Offset, set);
                    else throw new InvalidOperationException("A deterministic byte guard requires a literal or set.");
                }
            if (literal) enriched[index++] = Utf8StructuralSearchStage.RequireLiteralAtOffset(guards.FixedLiteralOffset, fixedBytes);
            if (guards.FixedDistanceSets is { } fixedSets)
                foreach (var set in fixedSets)
                {
                    budget.ChargeWork(1);
                    if (set.ByteSet.Negated || set.ByteSet.IsEmpty) continue;
                    // Canonical masks answer cardinality and the sole byte without
                    // acquiring a projected member array.
                    enriched[index++] = set.ByteSet.Count == 1
                        ? Utf8StructuralSearchStage.RequireByteAtOffset(set.Distance, set.ByteSet.LowMask != 0
                            ? (byte)BitOperations.TrailingZeroCount(set.ByteSet.LowMask)
                            : (byte)(64 + BitOperations.TrailingZeroCount(set.ByteSet.HighMask)))
                        : Utf8StructuralSearchStage.RequireAsciiClassAtOffset(set.Distance, set.ByteSet);
                }
            if (guards.MinRequiredLength > 0) enriched[index++] = Utf8StructuralSearchStage.RequireMinLength(guards.MinRequiredLength);
            Array.Copy(stages, yieldIndex, enriched, index, stages.Length - yieldIndex);
            scope.Complete();
            return new(YieldKind, enriched);
        }
        finally { budget.LeaveDepth(); }
    }

    private static int FindLastYield(Utf8StructuralSearchStage[] stages, Utf8StructuralSearchStageKind kind, Utf8ConstructionBudget budget)
    {
        for (var index = stages.Length - 1; index >= 0; index--)
        { budget.ChargeWork(1); if (stages[index].Kind == kind) return index; }
        return -1;
    }

    public bool ProducesBoundedCandidates
    {
        get
        {
            if (YieldKind == Utf8StructuralSearchYieldKind.Window)
            {
                return true;
            }

            if (Stages is not { Length: > 0 } stages)
            {
                return false;
            }

            foreach (var stage in stages)
            {
                if (stage.Kind is Utf8StructuralSearchStageKind.RequireExactLength or Utf8StructuralSearchStageKind.BoundMaxLength)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool RequiresCandidateEndCoverage
    {
        get
        {
            if (YieldKind == Utf8StructuralSearchYieldKind.Window)
            {
                return true;
            }

            if (Stages is not { Length: > 0 } stages)
            {
                return false;
            }

            foreach (var stage in stages)
            {
                if (stage.Kind == Utf8StructuralSearchStageKind.RequireExactLength)
                {
                    return true;
                }
            }

            return false;
        }
    }


}

internal readonly record struct Utf8StructuralCandidate(
    int StartIndex,
    int EndIndex,
    int MatchLength,
    int LiteralId,
    int TrailingIndex,
    int TrailingMatchLength,
    int TrailingLiteralId)
{
    public static Utf8StructuralCandidate Start(
        int startIndex,
        int endIndex,
        int matchLength,
        int literalId) =>
        new(startIndex, endIndex, matchLength, literalId, -1, -1, -1);

    public static Utf8StructuralCandidate Window(
        int startIndex,
        int endIndex,
        int matchLength,
        int literalId,
        int trailingIndex,
        int trailingMatchLength,
        int trailingLiteralId) =>
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
