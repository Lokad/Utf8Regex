using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Utilities;

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
    private Utf8StructuralSearchStage(
        Utf8StructuralSearchStageKind kind,
        PreparedSearcher searcher = default,
        PreparedWindowSearch windowSearch = default,
        Utf8FallbackStartTransform startTransform = default,
        Execution.PreparedAsciiFindPlan asciiFindPlan = default,
        int byteOffset = 0,
        byte? literalByte = null,
        string? set = null,
        byte[]? literalUtf8 = null,
        int minLength = 0,
        int maxSpan = 0,
        int maxLines = 0,
        Utf8BoundaryRequirement boundaryRequirement = Utf8BoundaryRequirement.None)
    {
        Kind = kind;
        Searcher = searcher;
        WindowSearch = windowSearch;
        StartTransform = startTransform;
        AsciiFindPlan = asciiFindPlan;
        ByteOffset = byteOffset;
        LiteralByte = literalByte;
        Set = set;
        LiteralUtf8 = literalUtf8;
        MinLength = minLength;
        MaxSpan = maxSpan;
        MaxLines = maxLines;
        BoundaryRequirement = boundaryRequirement;
    }

    public Utf8StructuralSearchStageKind Kind { get; }

    public PreparedSearcher Searcher { get; }

    public PreparedWindowSearch WindowSearch { get; }

    public Utf8FallbackStartTransform StartTransform { get; }

    public Execution.PreparedAsciiFindPlan AsciiFindPlan { get; }

    public int ByteOffset { get; }

    public byte? LiteralByte { get; }

    public string? Set { get; }

    public byte[]? LiteralUtf8 { get; }

    public int MinLength { get; }

    public int MaxSpan { get; }

    public int MaxLines { get; }

    public Utf8BoundaryRequirement BoundaryRequirement { get; }

    public static Utf8StructuralSearchStage FindLiteralFamily(PreparedSearcher searcher) =>
        new(Utf8StructuralSearchStageKind.FindLiteralFamily, searcher: searcher);

    public static Utf8StructuralSearchStage FindWindow(PreparedWindowSearch windowSearch) =>
        new(Utf8StructuralSearchStageKind.FindWindow, windowSearch: windowSearch);

    public static Utf8StructuralSearchStage FindAscii(Execution.PreparedAsciiFindPlan asciiFindPlan) =>
        new(Utf8StructuralSearchStageKind.FindAscii, asciiFindPlan: asciiFindPlan);

    public static Utf8StructuralSearchStage TransformCandidateStart(Utf8FallbackStartTransform startTransform) =>
        new(Utf8StructuralSearchStageKind.TransformCandidateStart, startTransform: startTransform);

    public static Utf8StructuralSearchStage RequireByteAtOffset(int byteOffset, byte literalByte) =>
        new(Utf8StructuralSearchStageKind.RequireByteAtOffset, byteOffset: byteOffset, literalByte: literalByte);

    public static Utf8StructuralSearchStage RequireSetAtOffset(int byteOffset, string set) =>
        new(Utf8StructuralSearchStageKind.RequireByteAtOffset, byteOffset: byteOffset, set: set);

    public static Utf8StructuralSearchStage RequireLiteralAtOffset(int byteOffset, byte[] literalUtf8) =>
        new(Utf8StructuralSearchStageKind.RequireLiteralAtOffset, byteOffset: byteOffset, literalUtf8: literalUtf8);

    public static Utf8StructuralSearchStage RequireMinLength(int minLength) =>
        new(Utf8StructuralSearchStageKind.RequireMinLength, minLength: minLength);

    public static Utf8StructuralSearchStage RequireWithinByteSpan(int maxSpan) =>
        new(Utf8StructuralSearchStageKind.RequireWithinByteSpan, maxSpan: maxSpan);

    public static Utf8StructuralSearchStage RequireWithinLineSpan(int maxLines) =>
        new(Utf8StructuralSearchStageKind.RequireWithinLineSpan, maxLines: maxLines);

    public static Utf8StructuralSearchStage RequireLeadingBoundary(Utf8BoundaryRequirement boundaryRequirement) =>
        new(Utf8StructuralSearchStageKind.RequireLeadingBoundary, boundaryRequirement: boundaryRequirement);

    public static Utf8StructuralSearchStage RequireTrailingBoundary(Utf8BoundaryRequirement boundaryRequirement) =>
        new(Utf8StructuralSearchStageKind.RequireTrailingBoundary, boundaryRequirement: boundaryRequirement);

    public static Utf8StructuralSearchStage RequireTrailingLiteral(byte[] literalUtf8) =>
        new(Utf8StructuralSearchStageKind.RequireTrailingLiteral, literalUtf8: literalUtf8);

    public static Utf8StructuralSearchStage RequireExactLength(int exactLength) =>
        new(Utf8StructuralSearchStageKind.RequireExactLength, minLength: exactLength);

    public static Utf8StructuralSearchStage BoundMaxLength(int maxLength) =>
        new(Utf8StructuralSearchStageKind.BoundMaxLength, maxSpan: maxLength);

    public static Utf8StructuralSearchStage YieldStart() =>
        new(Utf8StructuralSearchStageKind.YieldStart);

    public static Utf8StructuralSearchStage YieldWindow() =>
        new(Utf8StructuralSearchStageKind.YieldWindow);
}

internal readonly struct Utf8StructuralSearchPlan
{
    public Utf8StructuralSearchPlan(Utf8StructuralSearchYieldKind yieldKind, Utf8StructuralSearchStage[]? stages)
    {
        YieldKind = yieldKind;
        Stages = stages;
    }

    public Utf8StructuralSearchYieldKind YieldKind { get; }

    public Utf8StructuralSearchStage[]? Stages { get; }

    public bool HasValue => Stages is { Length: > 0 };

    public bool TryFindNextCandidate(
        ReadOnlySpan<byte> input,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;

        if (Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        return YieldKind switch
        {
            Utf8StructuralSearchYieldKind.Start => TryFindNextStartCandidate(input, stages, ref state, out candidate),
            Utf8StructuralSearchYieldKind.Window => TryFindNextWindowCandidate(input, stages, ref state, out candidate),
            _ => false,
        };
    }

    public bool TryFindLastCandidate(
        ReadOnlySpan<byte> input,
        int endIndex,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;

        if ((uint)endIndex > (uint)input.Length)
        {
            endIndex = input.Length;
        }

        if (Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        return YieldKind switch
        {
            Utf8StructuralSearchYieldKind.Start => TryFindLastStartCandidate(input, stages, endIndex, out candidate),
            _ => false,
        };
    }

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
            enriched[yieldIndex + i] = guard.Literal is { } literal
                ? Utf8StructuralSearchStage.RequireByteAtOffset(guard.Offset, literal)
                : Utf8StructuralSearchStage.RequireSetAtOffset(guard.Offset, guard.Set!);
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
            if (set.Negated || set.HasRange || set.Chars is not { Length: > 0 } chars)
            {
                continue;
            }

            if (chars.Length == 1)
            {
                enriched = InsertBeforeYield(enriched, Utf8StructuralSearchStage.RequireByteAtOffset(set.Distance, chars[0])).Stages!;
            }
            else
            {
                var setText = new string(chars.Select(static ch => (char)ch).ToArray());
                enriched = InsertBeforeYield(enriched, Utf8StructuralSearchStage.RequireSetAtOffset(set.Distance, setText)).Stages!;
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
            enriched = enriched.InsertBeforeYield(enriched.Stages!, Utf8StructuralSearchStage.RequireLeadingBoundary(leadingBoundary));
        }

        if (trailingBoundary != Utf8BoundaryRequirement.None)
        {
            enriched = enriched.InsertBeforeYield(enriched.Stages!, Utf8StructuralSearchStage.RequireTrailingBoundary(trailingBoundary));
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

    private static bool TryFindNextStartCandidate(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        var searcher = default(PreparedSearcher);
        var asciiFindPlan = default(Execution.PreparedAsciiFindPlan);
        var startTransform = default(Utf8FallbackStartTransform);

        foreach (var stage in stages)
        {
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.FindLiteralFamily:
                    searcher = stage.Searcher;
                    break;
                case Utf8StructuralSearchStageKind.FindAscii:
                    asciiFindPlan = stage.AsciiFindPlan;
                    break;
                case Utf8StructuralSearchStageKind.TransformCandidateStart:
                    startTransform = stage.StartTransform;
                    break;
            }
        }

        if (!searcher.HasValue && !asciiFindPlan.HasValue)
        {
            return false;
        }

        var searchState = state.SearchState;
        while (true)
        {
            int rawStartIndex;
            int matchedLength;
            int literalId;
            if (searcher.HasValue)
            {
                if (!searcher.TryFindNextOverlappingMatch(input, ref searchState, out var match))
                {
                    state = new Utf8StructuralSearchState(searchState, default);
                    return false;
                }

                rawStartIndex = match.Index;
                matchedLength = match.Length;
                literalId = match.LiteralId;
            }
            else
            {
                if (!Execution.Utf8AsciiFindExecutor.TryFindNextFixedDistanceCandidate(
                        input,
                        asciiFindPlan,
                        searchState.NextStart,
                        out rawStartIndex,
                        out matchedLength))
                {
                    state = new Utf8StructuralSearchState(searchState, default);
                    return false;
                }

                literalId = 0;
                searchState = new PreparedSearchScanState(rawStartIndex + 1, default);
            }

            var startIndex = startTransform.Apply(input, rawStartIndex);
            if (startIndex < 0)
            {
                continue;
            }

            var requirementBaseIndex = GetRequirementBaseIndex(input, startIndex, startTransform);
            if (!SatisfiesStartRequirements(input, stages, requirementBaseIndex, matchedLength))
            {
                continue;
            }

            state = new Utf8StructuralSearchState(searchState, default);
            var endIndex = GetExactCandidateEnd(stages, startIndex, input.Length);
            candidate = new Utf8StructuralCandidate(startIndex, endIndex, matchedLength, literalId);
            return true;
        }
    }

    private static bool TryFindNextWindowCandidate(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        var windowSearch = default(PreparedWindowSearch);
        var startTransform = default(Utf8FallbackStartTransform);

        foreach (var stage in stages)
        {
            if (stage.Kind == Utf8StructuralSearchStageKind.FindWindow)
            {
                windowSearch = stage.WindowSearch;
                continue;
            }

            if (stage.Kind == Utf8StructuralSearchStageKind.TransformCandidateStart)
            {
                startTransform = stage.StartTransform;
            }
        }

        if (!windowSearch.HasValue)
        {
            return false;
        }

        var windowState = state.WindowState;
        while (windowSearch.TryFindNextWindow(input, ref windowState, out var window))
        {
            if (!SatisfiesWindowRequirements(input, stages, window))
            {
                continue;
            }

            var startIndex = startTransform.Apply(input, window.Leading.Index);
            if (startIndex < 0)
            {
                continue;
            }

            state = new Utf8StructuralSearchState(default, windowState);
            candidate = new Utf8StructuralCandidate(
                startIndex,
                window.Trailing.Index + window.Trailing.Length,
                window.Leading.Length,
                window.Leading.LiteralId,
                window.Trailing.Index,
                window.Trailing.Length,
                window.Trailing.LiteralId);
            return true;
        }

        state = new Utf8StructuralSearchState(default, windowState);
        return false;
    }

    private static bool TryFindLastStartCandidate(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages,
        int endIndex,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        var searcher = default(PreparedSearcher);
        var asciiFindPlan = default(Execution.PreparedAsciiFindPlan);
        var startTransform = default(Utf8FallbackStartTransform);

        foreach (var stage in stages)
        {
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.FindLiteralFamily:
                    searcher = stage.Searcher;
                    break;
                case Utf8StructuralSearchStageKind.FindAscii:
                    asciiFindPlan = stage.AsciiFindPlan;
                    break;
                case Utf8StructuralSearchStageKind.TransformCandidateStart:
                    startTransform = stage.StartTransform;
                    break;
            }
        }

        if (!searcher.HasValue && !asciiFindPlan.HasValue)
        {
            return false;
        }

        if (searcher.HasValue)
        {
            var searchLength = endIndex;
            while (searchLength > 0)
            {
                if (!searcher.TryFindLastMatch(input[..searchLength], out var match))
                {
                    return false;
                }

                var startIndex = startTransform.Apply(input, match.Index);
                if (startIndex >= 0)
                {
                    var requirementBaseIndex = GetRequirementBaseIndex(input, startIndex, startTransform);
                    if (SatisfiesStartRequirements(input, stages, requirementBaseIndex, match.Length))
                    {
                        var candidateEndIndex = GetExactCandidateEnd(stages, startIndex, input.Length);
                        candidate = new Utf8StructuralCandidate(startIndex, candidateEndIndex, match.Length, match.LiteralId);
                        return true;
                    }
                }

                searchLength = match.Index;
            }

            return false;
        }

        var lastStart = -1;
        var lastLength = 0;
        var searchFrom = 0;
        while (Execution.Utf8AsciiFindExecutor.TryFindNextFixedDistanceCandidate(input[..endIndex], asciiFindPlan, searchFrom, out var candidateStart, out var matchLength))
        {
            var startIndex = startTransform.Apply(input, candidateStart);
            if (startIndex >= 0)
            {
                var requirementBaseIndex = GetRequirementBaseIndex(input, startIndex, startTransform);
                if (SatisfiesStartRequirements(input, stages, requirementBaseIndex, matchLength))
                {
                    lastStart = startIndex;
                    lastLength = matchLength;
                }
            }

            searchFrom = candidateStart + 1;
        }

        if (lastStart >= 0)
        {
            var candidateEndIndex = GetExactCandidateEnd(stages, lastStart, input.Length);
            candidate = new Utf8StructuralCandidate(lastStart, candidateEndIndex, lastLength, 0);
            return true;
        }

        return false;
    }

    private static bool SatisfiesStartRequirements(ReadOnlySpan<byte> input, Utf8StructuralSearchStage[] stages, int startIndex, int matchLength)
    {
        foreach (var stage in stages)
        {
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.RequireByteAtOffset:
                    var index = startIndex + stage.ByteOffset;
                    if ((uint)index >= (uint)input.Length)
                    {
                        return false;
                    }

                    if (stage.LiteralByte is { } literal && input[index] != literal)
                    {
                        return false;
                    }

                    if (stage.Set is { } set && !FrontEnd.Runtime.RegexCharClass.CharInClass((char)input[index], set))
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireLiteralAtOffset:
                    if (stage.LiteralUtf8 is not { Length: > 0 } literalUtf8)
                    {
                        break;
                    }

                    var literalOffset = startIndex + stage.ByteOffset;
                    if (literalOffset < 0 || literalOffset > input.Length - literalUtf8.Length)
                    {
                        return false;
                    }

                    if (!input.Slice(literalOffset, literalUtf8.Length).SequenceEqual(literalUtf8))
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireMinLength:
                    if (startIndex > input.Length - stage.MinLength)
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireLeadingBoundary:
                    if (!MatchesBoundaryRequirement(stage.BoundaryRequirement, input, startIndex))
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireTrailingBoundary:
                    if (!MatchesBoundaryRequirement(stage.BoundaryRequirement, input, startIndex + matchLength))
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireTrailingLiteral:
                    if (stage.LiteralUtf8 is not { Length: > 0 } trailingLiteralUtf8)
                    {
                        break;
                    }

                    var trailingOffset = startIndex + matchLength;
                    if (trailingOffset < 0 || trailingOffset > input.Length - trailingLiteralUtf8.Length)
                    {
                        return false;
                    }

                    if (!input.Slice(trailingOffset, trailingLiteralUtf8.Length).SequenceEqual(trailingLiteralUtf8))
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireExactLength:
                    if (startIndex > input.Length - stage.MinLength)
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
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

    private static bool SatisfiesWindowRequirements(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages,
        PreparedWindowMatch window)
    {
        var span = window.Trailing.Index + window.Trailing.Length - window.Leading.Index;
        foreach (var stage in stages)
        {
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.RequireWithinByteSpan:
                    if (span > stage.MaxSpan)
                    {
                        return false;
                    }
                    break;

                case Utf8StructuralSearchStageKind.RequireWithinLineSpan:
                    if (!SatisfiesLineSpan(input, window.Leading.Index, window.Trailing.Index, stage.MaxLines))
                    {
                        return false;
                    }
                    break;

                case Utf8StructuralSearchStageKind.RequireLeadingBoundary:
                    if (!MatchesBoundaryRequirement(stage.BoundaryRequirement, input, window.Leading.Index))
                    {
                        return false;
                    }
                    break;

                case Utf8StructuralSearchStageKind.RequireTrailingBoundary:
                    if (!MatchesBoundaryRequirement(stage.BoundaryRequirement, input, window.Trailing.Index + window.Trailing.Length))
                    {
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    private static bool SatisfiesLineSpan(ReadOnlySpan<byte> input, int startIndex, int endIndex, int maxLines)
    {
        if (maxLines <= 0)
        {
            return false;
        }

        var lineCount = 1;
        for (var i = startIndex; i < endIndex; i++)
        {
            if (input[i] is (byte)'\r' or (byte)'\n')
            {
                lineCount++;
                if (lineCount > maxLines)
                {
                    return false;
                }

                if (input[i] == (byte)'\r' &&
                    i + 1 < endIndex &&
                    input[i + 1] == (byte)'\n')
                {
                    i++;
                }
            }
        }

        return true;
    }

    private static bool MatchesBoundaryRequirement(Utf8BoundaryRequirement requirement, ReadOnlySpan<byte> input, int byteOffset)
    {
        return requirement switch
        {
            Utf8BoundaryRequirement.None => true,
            Utf8BoundaryRequirement.Boundary => IsWordBoundary(input, byteOffset),
            Utf8BoundaryRequirement.NonBoundary => !IsWordBoundary(input, byteOffset),
            _ => false,
        };
    }

    private static bool IsWordBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var previousIsWord = byteOffset > 0 &&
            TryGetAdjacentBoundaryChar(input[..byteOffset], previous: true, out var previousChar) &&
            FrontEnd.Runtime.RegexCharClass.IsBoundaryWordChar(previousChar);
        var nextIsWord = byteOffset < input.Length &&
            TryGetAdjacentBoundaryChar(input[byteOffset..], previous: false, out var nextChar) &&
            FrontEnd.Runtime.RegexCharClass.IsBoundaryWordChar(nextChar);
        return previousIsWord != nextIsWord;
    }

    private static bool TryGetAdjacentBoundaryChar(ReadOnlySpan<byte> input, bool previous, out char ch)
    {
        ch = '\0';
        if (input.IsEmpty)
        {
            return false;
        }

        if (previous)
        {
            var status = Rune.DecodeLastFromUtf8(input, out var rune, out _);
            if (status != OperationStatus.Done)
            {
                return false;
            }

            if (!rune.IsBmp)
            {
                return true;
            }

            ch = (char)rune.Value;
            return true;
        }

        var nextStatus = Rune.DecodeFromUtf8(input, out var nextRune, out _);
        if (nextStatus != OperationStatus.Done)
        {
            return false;
        }

        if (!nextRune.IsBmp)
        {
            return true;
        }

        ch = (char)nextRune.Value;
        return true;
    }

    private static int GetRequirementBaseIndex(ReadOnlySpan<byte> input, int startIndex, Utf8FallbackStartTransform startTransform)
    {
        if (startTransform.Kind != Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace)
        {
            return startIndex;
        }

        while ((uint)startIndex < (uint)input.Length &&
               input[startIndex] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C)
        {
            startIndex++;
        }

        return startIndex;
    }

    private static int GetExactCandidateEnd(Utf8StructuralSearchStage[] stages, int startIndex, int inputLength)
    {
        foreach (var stage in stages)
        {
            if (stage.Kind == Utf8StructuralSearchStageKind.RequireExactLength)
            {
                var endIndex = startIndex + stage.MinLength;
                return endIndex <= inputLength ? endIndex : -1;
            }

            if (stage.Kind == Utf8StructuralSearchStageKind.BoundMaxLength)
            {
                var boundedEndIndex = startIndex + stage.MaxSpan;
                return boundedEndIndex <= inputLength ? boundedEndIndex : inputLength;
            }
        }

        return -1;
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
        if (primary.Negated || primary.HasRange)
        {
            return false;
        }

        if (primary.Chars is not { Length: > 0 } chars)
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
