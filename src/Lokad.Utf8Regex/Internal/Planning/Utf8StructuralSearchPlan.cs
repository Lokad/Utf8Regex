using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
using System.Buffers;
using System.Text;

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
        public string Set { get; init; }
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
        Set = data.Set ?? string.Empty;
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

    public string Set { get; }

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

    public static Utf8StructuralSearchStage RequireSetAtOffset(int byteOffset, string set) =>
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
        Utf8SearchKind kind,
        int distance,
        bool canGuideFallbackStarts,
        PreparedSearcher preparedSearcher,
        PreparedWindowSearch windowSearch,
        Utf8FixedDistanceSet[]? fixedDistanceSets,
        Utf8FallbackStartTransform fallbackStartTransform)
    {
        return Create(
            kind,
            distance,
            canGuideFallbackStarts,
            literalUtf8: null,
            preparedSearcher,
            windowSearch,
            fixedDistanceSets,
            fallbackStartTransform);
    }

    public static Utf8StructuralSearchPlan Create(
        Utf8SearchKind kind,
        int distance,
        bool canGuideFallbackStarts,
        byte[]? literalUtf8,
        PreparedSearcher preparedSearcher,
        PreparedWindowSearch windowSearch,
        Utf8FixedDistanceSet[]? fixedDistanceSets,
        Utf8FallbackStartTransform fallbackStartTransform)
    {
        var effectiveStartTransform = kind switch
        {
            Utf8SearchKind.FixedDistanceAsciiLiteral or Utf8SearchKind.FixedDistanceAsciiChar
                => fallbackStartTransform.WithAdditionalOffset(distance),
            _ => fallbackStartTransform,
        };

        if (preparedSearcher.HasValue &&
            (canGuideFallbackStarts ||
             effectiveStartTransform.HasValue ||
             kind is Utf8SearchKind.ExactAsciiLiteral or
                 Utf8SearchKind.AsciiLiteralIgnoreCase or
                 Utf8SearchKind.ExactAsciiLiterals or
                 Utf8SearchKind.AsciiLiteralIgnoreCaseLiterals or
                 Utf8SearchKind.ExactUtf8Literals))
        {
            return CreateStart(preparedSearcher, effectiveStartTransform);
        }

        if (kind == Utf8SearchKind.FixedDistanceAsciiLiteral &&
            literalUtf8 is { Length: > 0 } fixedLiteral)
        {
            return CreateAsciiFind(
                Execution.PreparedAsciiFindPlan.CreateFixedDistanceLiteral(fixedLiteral, distance),
                fallbackStartTransform);
        }

        if (kind == Utf8SearchKind.FixedDistanceAsciiSets &&
            fixedDistanceSets is { Length: > 0 })
        {
            return CreateAsciiFind(
                Execution.PreparedAsciiFindPlan.CreateFixedDistanceSet(fixedDistanceSets),
                fallbackStartTransform);
        }

        if (windowSearch.HasValue)
        {
            return CreateWindow(windowSearch, default);
        }

        return default;
    }

    public static Utf8StructuralSearchPlan CreateStartPlan(PreparedSearcher searcher)
    {
        return searcher.HasValue
            ? CreateStart(searcher, default)
            : default;
    }

    public static Utf8StructuralSearchPlan CreateWindowPlan(
        PreparedWindowSearch windowSearch,
        int? maxLines = null,
        Utf8FallbackStartTransform startTransform = default)
    {
        if (!windowSearch.HasValue)
        {
            return default;
        }

        var plan = CreateWindow(windowSearch, startTransform);
        if (maxLines is > 0)
        {
            plan = plan.WithLineSpan(maxLines.Value);
        }

        return plan;
    }

    public Utf8StructuralSearchPlan WithPrefixGuards(Execution.Utf8DeterministicByteGuard[]? prefixGuards)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Start ||
            Stages is not { Length: > 0 } stages ||
            prefixGuards is not { Length: > 0 })
        {
            return this;
        }

        var yieldIndex = Array.FindLastIndex(stages, static stage => stage.Kind == Utf8StructuralSearchStageKind.YieldStart);
        if (yieldIndex < 0)
        {
            return this;
        }

        var enriched = new Utf8StructuralSearchStage[stages.Length + prefixGuards.Length];
        Array.Copy(stages, 0, enriched, 0, yieldIndex);
        for (var i = 0; i < prefixGuards.Length; i++)
        {
            var guard = prefixGuards[i];
            if (guard.Literal is { } literal)
            {
                enriched[yieldIndex + i] = Utf8StructuralSearchStage.RequireByteAtOffset(guard.Offset, literal);
            }
            else if (guard.Set is { } set)
            {
                enriched[yieldIndex + i] = Utf8StructuralSearchStage.RequireSetAtOffset(guard.Offset, set);
            }
            else
            {
                throw new InvalidOperationException("A deterministic byte guard requires a literal or set.");
            }
        }

        Array.Copy(stages, yieldIndex, enriched, yieldIndex + prefixGuards.Length, stages.Length - yieldIndex);
        return new Utf8StructuralSearchPlan(YieldKind, enriched);
    }

    public Utf8StructuralSearchPlan WithFixedLiteral(byte[]? literalUtf8, int byteOffset)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Start ||
            Stages is not { Length: > 0 } stages ||
            literalUtf8 is not { Length: > 0 })
        {
            return this;
        }

        return InsertBeforeYield(stages, Utf8StructuralSearchStage.RequireLiteralAtOffset(byteOffset, literalUtf8));
    }

    public Utf8StructuralSearchPlan WithFixedSets(Utf8FixedDistanceSet[]? fixedDistanceSets)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Start ||
            Stages is not { Length: > 0 } stages ||
            fixedDistanceSets is not { Length: > 0 })
        {
            return this;
        }

        var enriched = stages;
        foreach (var set in fixedDistanceSets)
        {
            if (set.ByteSet.Negated || set.ByteSet.GetPositiveMatchBytes() is not { Length: > 0 } chars)
            {
                continue;
            }

            Utf8StructuralSearchPlan updated;
            if (chars.Length == 1)
            {
                updated = InsertBeforeYield(enriched, Utf8StructuralSearchStage.RequireByteAtOffset(set.Distance, chars[0]));
            }
            else
            {
                var setText = new string(chars.Select(static ch => (char)ch).ToArray());
                updated = InsertBeforeYield(enriched, Utf8StructuralSearchStage.RequireSetAtOffset(set.Distance, setText));
            }

            if (updated.Stages is { } updatedStages)
            {
                enriched = updatedStages;
            }
        }

        return new Utf8StructuralSearchPlan(YieldKind, enriched);
    }

    public Utf8StructuralSearchPlan WithMinLength(int minLength)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Start ||
            Stages is not { Length: > 0 } stages ||
            minLength <= 0)
        {
            return this;
        }

        return InsertBeforeYield(stages, Utf8StructuralSearchStage.RequireMinLength(minLength));
    }

    public Utf8StructuralSearchPlan WithBoundaryRequirements(
        Utf8BoundaryRequirement leadingBoundary,
        Utf8BoundaryRequirement trailingBoundary)
    {
        if (Stages is not { Length: > 0 } stages ||
            (leadingBoundary == Utf8BoundaryRequirement.None && trailingBoundary == Utf8BoundaryRequirement.None))
        {
            return this;
        }

        var enriched = this;
        if (leadingBoundary != Utf8BoundaryRequirement.None)
        {
            if (enriched.Stages is not { } leadingStages)
            {
                throw new InvalidOperationException("A boundary-enriched structural plan requires stages.");
            }

            enriched = enriched.InsertBeforeYield(leadingStages, Utf8StructuralSearchStage.RequireLeadingBoundary(leadingBoundary));
        }

        if (trailingBoundary != Utf8BoundaryRequirement.None)
        {
            if (enriched.Stages is not { } trailingStages)
            {
                throw new InvalidOperationException("A boundary-enriched structural plan requires stages.");
            }

            enriched = enriched.InsertBeforeYield(trailingStages, Utf8StructuralSearchStage.RequireTrailingBoundary(trailingBoundary));
        }

        return enriched;
    }

    public Utf8StructuralSearchPlan WithLineSpan(int maxLines)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Window ||
            Stages is not { Length: > 0 } stages ||
            maxLines <= 0)
        {
            return this;
        }

        return InsertBeforeYield(stages, Utf8StructuralSearchStage.RequireWithinLineSpan(maxLines));
    }

    public Utf8StructuralSearchPlan WithTrailingLiteral(byte[]? trailingLiteralUtf8)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Start ||
            Stages is not { Length: > 0 } stages ||
            trailingLiteralUtf8 is not { Length: > 0 })
        {
            return this;
        }

        return InsertBeforeYield(stages, Utf8StructuralSearchStage.RequireTrailingLiteral(trailingLiteralUtf8));
    }

    public Utf8StructuralSearchPlan WithExactLength(int? exactLength)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Start ||
            Stages is not { Length: > 0 } stages ||
            exactLength is not int value ||
            value <= 0)
        {
            return this;
        }

        return InsertBeforeYield(stages, Utf8StructuralSearchStage.RequireExactLength(value));
    }

    public Utf8StructuralSearchPlan WithMaxLength(int? maxLength)
    {
        if (YieldKind != Utf8StructuralSearchYieldKind.Start ||
            Stages is not { Length: > 0 } stages ||
            maxLength is not int value ||
            value <= 0 ||
            RequiresCandidateEndCoverage)
        {
            return this;
        }

        return InsertBeforeYield(stages, Utf8StructuralSearchStage.BoundMaxLength(value));
    }

    private static Utf8StructuralSearchPlan CreateStart(PreparedSearcher searcher, Utf8FallbackStartTransform startTransform)
    {
        var stageCount = startTransform.HasValue ? 3 : 2;
        var stages = new Utf8StructuralSearchStage[stageCount];
        stages[0] = Utf8StructuralSearchStage.FindLiteralFamily(searcher);
        var stageIndex = 1;
        if (startTransform.HasValue)
        {
            stages[stageIndex++] = Utf8StructuralSearchStage.TransformCandidateStart(startTransform);
        }

        stages[stageIndex] = Utf8StructuralSearchStage.YieldStart();
        return new Utf8StructuralSearchPlan(Utf8StructuralSearchYieldKind.Start, stages);
    }

    private static Utf8StructuralSearchPlan CreateAsciiFind(Execution.PreparedAsciiFindPlan findPlan, Utf8FallbackStartTransform startTransform)
    {
        var stageCount = startTransform.HasValue ? 3 : 2;
        var stages = new Utf8StructuralSearchStage[stageCount];
        stages[0] = Utf8StructuralSearchStage.FindAscii(findPlan);
        var stageIndex = 1;
        if (startTransform.HasValue)
        {
            stages[stageIndex++] = Utf8StructuralSearchStage.TransformCandidateStart(startTransform);
        }

        stages[stageIndex] = Utf8StructuralSearchStage.YieldStart();
        return new Utf8StructuralSearchPlan(Utf8StructuralSearchYieldKind.Start, stages);
    }

    private static Utf8StructuralSearchPlan CreateWindow(PreparedWindowSearch windowSearch, Utf8FallbackStartTransform startTransform)
    {
        var stages = new List<Utf8StructuralSearchStage>(4)
        {
            Utf8StructuralSearchStage.FindWindow(windowSearch),
        };

        if (startTransform.HasValue)
        {
            stages.Add(Utf8StructuralSearchStage.TransformCandidateStart(startTransform));
        }

        if (windowSearch.MaxGap is int maxGap && maxGap > 0)
        {
            stages.Add(Utf8StructuralSearchStage.RequireWithinByteSpan(maxGap));
        }

        if (windowSearch.SameLine)
        {
            stages.Add(Utf8StructuralSearchStage.RequireWithinLineSpan(1));
        }

        stages.Add(Utf8StructuralSearchStage.YieldWindow());
        return new Utf8StructuralSearchPlan(Utf8StructuralSearchYieldKind.Window, [.. stages]);
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

    private static bool TryCreateFixedSetCandidateSearcher(Utf8FixedDistanceSet[]? fixedDistanceSets, out PreparedSearcher searcher, out int distance)
    {
        searcher = default;
        distance = 0;
        if (fixedDistanceSets is not { Length: > 0 } sets)
        {
            return false;
        }

        var primary = sets[0];
        if (primary.ByteSet.Negated)
        {
            return false;
        }

        if (primary.ByteSet.GetPositiveMatchBytes() is not { Length: > 0 } chars)
        {
            return false;
        }

        distance = primary.Distance;
        searcher = chars.Length == 1
            ? new PreparedSearcher(new PreparedSubstringSearch([chars[0]], ignoreCase: false), ignoreCase: false)
            : new PreparedSearcher(new PreparedMultiLiteralSearch([.. chars.Select(static ch => new byte[] { ch })], ignoreCase: false));
        return true;
    }

    private Utf8StructuralSearchPlan InsertBeforeYield(Utf8StructuralSearchStage[] stages, Utf8StructuralSearchStage stage)
    {
        var yieldKind = YieldKind == Utf8StructuralSearchYieldKind.Window
            ? Utf8StructuralSearchStageKind.YieldWindow
            : Utf8StructuralSearchStageKind.YieldStart;
        var yieldIndex = Array.FindLastIndex(stages, existing => existing.Kind == yieldKind);
        if (yieldIndex < 0)
        {
            return this;
        }

        var enriched = new Utf8StructuralSearchStage[stages.Length + 1];
        Array.Copy(stages, 0, enriched, 0, yieldIndex);
        enriched[yieldIndex] = stage;
        Array.Copy(stages, yieldIndex, enriched, yieldIndex + 1, stages.Length - yieldIndex);
        return new Utf8StructuralSearchPlan(YieldKind, enriched);
    }
}

internal readonly record struct Utf8StructuralCandidate(
    int StartIndex,
    int EndIndex = -1,
    int MatchLength = -1,
    int LiteralId = -1,
    int TrailingIndex = -1,
    int TrailingMatchLength = -1,
    int TrailingLiteralId = -1);

internal readonly record struct Utf8StructuralSearchState(PreparedSearchScanState SearchState, PreparedWindowScanState WindowState);
