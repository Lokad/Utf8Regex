using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8StructuralSearchExecutor
{
    public static bool TryFindNextCandidate(
        this Utf8StructuralSearchPlan plan,
        ReadOnlySpan<byte> input,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;

        if (plan.Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        return plan.YieldKind switch
        {
            Utf8StructuralSearchYieldKind.Start => TryFindNextStartCandidate(input, stages, ref state, out candidate),
            Utf8StructuralSearchYieldKind.Window => TryFindNextWindowCandidate(input, stages, ref state, out candidate),
            _ => false,
        };
    }

    public static bool TryFindLastCandidate(
        this Utf8StructuralSearchPlan plan,
        ReadOnlySpan<byte> input,
        int endIndex,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;

        if ((uint)endIndex > (uint)input.Length)
        {
            endIndex = input.Length;
        }

        if (plan.Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        return plan.YieldKind switch
        {
            Utf8StructuralSearchYieldKind.Start => TryFindLastStartCandidate(input, stages, endIndex, out candidate),
            _ => false,
        };
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

                    if (stage.HasLiteralByte && input[index] != stage.LiteralByte)
                    {
                        return false;
                    }

                    if (stage.HasSet && !FrontEnd.Runtime.RegexCharClass.CharInClass((char)input[index], stage.Set))
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
        return DotNetUtf8WordBoundary.IsBoundary(input, byteOffset);
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

}
