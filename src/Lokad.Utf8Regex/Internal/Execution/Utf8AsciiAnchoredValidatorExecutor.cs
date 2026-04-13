using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiAnchoredValidatorExecutor
{
    internal enum DirectMatchResult
    {
        NeedsValidation = 0,
        NoMatch = 1,
        Match = 2,
    }

    public static bool TryMatchWhole(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredHeadTailRunPlan plan,
        bool allowTrailingNewline,
        out int matchedLength)
    {
        matchedLength = 0;
        if (!plan.HasValue)
        {
            return false;
        }

        if (plan.IsMatch(input))
        {
            matchedLength = input.Length;
            return true;
        }

        if (allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n' &&
            plan.IsMatch(input[..^1]))
        {
            matchedLength = input.Length - 1;
            return true;
        }

        return false;
    }

    public static DirectMatchResult TryMatchWholeWithoutValidation(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredHeadTailRunPlan plan,
        bool allowTrailingNewline,
        out int matchedLength)
    {
        matchedLength = 0;
        if (!plan.HasValue)
        {
            return DirectMatchResult.NeedsValidation;
        }

        var direct = TryMatchWholeWithoutValidationCore(input, plan);
        if (direct == DirectMatchResult.Match)
        {
            matchedLength = input.Length;
            return DirectMatchResult.Match;
        }

        if (direct == DirectMatchResult.NoMatch && !(allowTrailingNewline && input.Length > 0 && input[^1] == (byte)'\n'))
        {
            return DirectMatchResult.NoMatch;
        }

        if (allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n')
        {
            var trimmed = TryMatchWholeWithoutValidationCore(input[..^1], plan);
            if (trimmed == DirectMatchResult.Match)
            {
                matchedLength = input.Length - 1;
                return DirectMatchResult.Match;
            }

            return trimmed;
        }

        return direct;
    }

    public static bool TryMatchWhole(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        bool allowTrailingNewline,
        out int matchedLength)
    {
        matchedLength = 0;
        if (!plan.HasValue)
        {
            return false;
        }

        if (TryMatchWholeCore(input, plan, input.Length, out matchedLength))
        {
            return true;
        }

        if (allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n' &&
            TryMatchWholeCore(input, plan, input.Length - 1, out matchedLength))
        {
            return true;
        }

        matchedLength = 0;
        return false;
    }

    public static DirectMatchResult TryMatchWholeWithoutValidation(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        bool allowTrailingNewline,
        out int matchedLength)
    {
        matchedLength = 0;
        if (!plan.HasValue)
        {
            return DirectMatchResult.NeedsValidation;
        }

        var direct = TryMatchWholeCoreWithoutValidation(input, plan, input.Length);
        if (direct == DirectMatchResult.Match)
        {
            matchedLength = input.Length;
            return DirectMatchResult.Match;
        }

        if (direct == DirectMatchResult.NoMatch && !(allowTrailingNewline && input.Length > 0 && input[^1] == (byte)'\n'))
        {
            return DirectMatchResult.NoMatch;
        }

        if (allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n')
        {
            var trimmed = TryMatchWholeCoreWithoutValidation(input, plan, input.Length - 1);
            if (trimmed == DirectMatchResult.Match)
            {
                matchedLength = input.Length - 1;
                return DirectMatchResult.Match;
            }

            return trimmed;
        }

        return direct;
    }

    public static bool TryMatchWholeFixedPrefixOnly(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        bool allowTrailingNewline,
        out int matchedLength)
    {
        matchedLength = 0;
        if (!plan.HasValue)
        {
            return false;
        }

        if (TryMatchFixedPrefixCore(input, plan, input.Length, out matchedLength))
        {
            return true;
        }

        if (allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n' &&
            TryMatchFixedPrefixCore(input, plan, input.Length - 1, out matchedLength))
        {
            return true;
        }

        matchedLength = 0;
        return false;
    }

    public static bool TryMatchWholeFirstBoundedSegmentOnly(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        bool allowTrailingNewline,
        out int matchedLength)
    {
        matchedLength = 0;
        if (!plan.HasValue)
        {
            return false;
        }

        if (TryMatchFirstBoundedSegmentCore(input, plan, input.Length, out matchedLength))
        {
            return true;
        }

        if (allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n' &&
            TryMatchFirstBoundedSegmentCore(input, plan, input.Length - 1, out matchedLength))
        {
            return true;
        }

        matchedLength = 0;
        return false;
    }

    public static bool TryMatchWholeSuffixAfterFirstBounded(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        bool allowTrailingNewline,
        out int matchedLength)
    {
        matchedLength = 0;
        if (!plan.HasValue)
        {
            return false;
        }

        if (TryMatchSuffixAfterFirstBoundedCore(input, plan, input.Length, out matchedLength))
        {
            return true;
        }

        if (allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n' &&
            TryMatchSuffixAfterFirstBoundedCore(input, plan, input.Length - 1, out matchedLength))
        {
            return true;
        }

        matchedLength = 0;
        return false;
    }

    public static string GetSegmentSummary(AsciiSimplePatternAnchoredValidatorPlan plan)
    {
        if (!plan.HasValue)
        {
            return "None";
        }

        return string.Join(
            ",",
            plan.Segments.Select(static segment =>
                segment.IsLiteral
                    ? $"L{segment.Literal.Length}"
                    : $"C{segment.MinLength}-{(segment.MaxLength == int.MaxValue ? "inf" : segment.MaxLength.ToString())}:{segment.PredicateKind}"));
    }

    private static bool TryMatchWholeCore(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        int inputLength,
        out int matchedLength)
    {
        matchedLength = 0;
        var index = 0;
        var segments = plan.Segments;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.IsLiteral)
            {
                if (inputLength - index < segment.Literal.Length ||
                    !MatchesLiteral(input, index, segment.Literal, plan.IgnoreCase))
                {
                    return false;
                }

                index += segment.Literal.Length;
                continue;
            }

            if (segment.CharClass is not { } charClass)
            {
                return false;
            }

            if (segment.MaxLength == int.MaxValue)
            {
                if (i != segments.Length - 1)
                {
                    return false;
                }

                var remaining = inputLength - index;
                if (remaining < segment.MinLength ||
                    !MatchesCharClassRun(input, index, remaining, charClass))
                {
                    return false;
                }

                index = inputLength;
                continue;
            }

            if (!TryChooseBoundedRunLength(input, index, inputLength, segments, i, charClass, segment.MinLength, segment.MaxLength, out var runLength))
            {
                return false;
            }

            index += runLength;
        }

        matchedLength = index;
        return index == inputLength;
    }

    private static bool TryMatchFixedPrefixCore(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        int inputLength,
        out int matchedLength)
    {
        matchedLength = 0;
        var index = 0;
        foreach (var segment in plan.Segments)
        {
            if (segment.IsLiteral)
            {
                if (inputLength - index < segment.Literal.Length ||
                    !MatchesLiteral(input, index, segment.Literal, plan.IgnoreCase))
                {
                    return false;
                }

                index += segment.Literal.Length;
                continue;
            }

            if (segment.CharClass is not { } charClass)
            {
                return false;
            }

            if (segment.MaxLength == int.MaxValue || segment.MinLength != segment.MaxLength)
            {
                matchedLength = index;
                return true;
            }

            if (inputLength - index < segment.MaxLength ||
                !MatchesCharClassRun(input, index, segment.MaxLength, charClass))
            {
                return false;
            }

            index += segment.MaxLength;
        }

        matchedLength = index;
        return true;
    }

    private static bool TryMatchFirstBoundedSegmentCore(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        int inputLength,
        out int matchedLength)
    {
        matchedLength = 0;
        var index = 0;
        var segments = plan.Segments;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.IsLiteral)
            {
                if (inputLength - index < segment.Literal.Length ||
                    !MatchesLiteral(input, index, segment.Literal, plan.IgnoreCase))
                {
                    return false;
                }

                index += segment.Literal.Length;
                continue;
            }

            if (segment.CharClass is not { } charClass)
            {
                return false;
            }

            if (segment.MaxLength == int.MaxValue)
            {
                return false;
            }

            if (segment.MinLength != segment.MaxLength)
            {
                if (!TryChooseBoundedRunLength(input, index, inputLength, segments, i, charClass, segment.MinLength, segment.MaxLength, out var runLength))
                {
                    return false;
                }

                matchedLength = index + runLength;
                return true;
            }

            if (inputLength - index < segment.MaxLength ||
                !MatchesCharClassRun(input, index, segment.MaxLength, charClass))
            {
                return false;
            }

            index += segment.MaxLength;
        }

        return false;
    }

    private static bool TryMatchSuffixAfterFirstBoundedCore(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        int inputLength,
        out int matchedLength)
    {
        matchedLength = 0;
        var index = 0;
        var segments = plan.Segments;
        var seenFirstBounded = false;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.IsLiteral)
            {
                if (inputLength - index < segment.Literal.Length ||
                    !MatchesLiteral(input, index, segment.Literal, plan.IgnoreCase))
                {
                    return false;
                }

                index += segment.Literal.Length;
                continue;
            }

            if (segment.CharClass is not { } charClass)
            {
                return false;
            }

            if (segment.MaxLength == int.MaxValue)
            {
                return false;
            }

            if (segment.MinLength != segment.MaxLength && !seenFirstBounded)
            {
                if (!TryChooseBoundedRunLength(input, index, inputLength, segments, i, charClass, segment.MinLength, segment.MaxLength, out var runLength))
                {
                    return false;
                }

                index += runLength;
                seenFirstBounded = true;
                continue;
            }

            var requiredLength = segment.MinLength == segment.MaxLength ? segment.MaxLength : segment.MaxLength;
            if (segment.MinLength != segment.MaxLength)
            {
                if (!TryChooseBoundedRunLength(input, index, inputLength, segments, i, charClass, segment.MinLength, segment.MaxLength, out var runLength))
                {
                    return false;
                }

                index += runLength;
                continue;
            }

            if (inputLength - index < requiredLength ||
                !MatchesCharClassRun(input, index, requiredLength, charClass))
            {
                return false;
            }

            index += requiredLength;
        }

        if (!seenFirstBounded)
        {
            return false;
        }

        matchedLength = index;
        return index == inputLength;
    }

    private static DirectMatchResult TryMatchWholeWithoutValidationCore(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredHeadTailRunPlan plan)
    {
        if (!plan.HasValue || input.Length < 1 + plan.TailMinLength)
        {
            return DirectMatchResult.NoMatch;
        }

        return TryMatchHeadTailWithoutValidation(input, plan);
    }

    private static DirectMatchResult TryMatchWholeCoreWithoutValidation(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredValidatorPlan plan,
        int inputLength)
    {
        if (TryMatchWholeCore(input, plan, inputLength, out _))
        {
            return DirectMatchResult.Match;
        }

        return ContainsNonAscii(input[..inputLength])
            ? DirectMatchResult.NeedsValidation
            : DirectMatchResult.NoMatch;
    }

    private static bool ContainsNonAscii(ReadOnlySpan<byte> input)
    {
        return input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0;
    }

    private static bool MatchesCharClassRun(ReadOnlySpan<byte> input, int index, int length, AsciiCharClass charClass)
    {
        if (!charClass.Negated)
        {
            var allowed = charClass.GetPositiveMatchBytes();
            return allowed.Length > 0 &&
                input.Slice(index, length).IndexOfAnyExcept(allowed) < 0;
        }

        for (var i = 0; i < length; i++)
        {
            if (!charClass.Contains(input[index + i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryChooseBoundedRunLength(
        ReadOnlySpan<byte> input,
        int index,
        int inputLength,
        AsciiSimplePatternAnchoredValidatorSegment[] segments,
        int segmentIndex,
        AsciiCharClass charClass,
        int minLength,
        int maxLength,
        out int runLength)
    {
        runLength = 0;
        GetAnchoredValidatorSuffixBounds(segments, segmentIndex + 1, out var suffixMinLength, out var suffixMaxLength);

        var remainingLength = inputLength - index;
        var candidateMinLength = Math.Max(minLength, suffixMaxLength == int.MaxValue ? minLength : remainingLength - suffixMaxLength);
        var candidateMaxLength = Math.Min(maxLength, remainingLength - suffixMinLength);
        if (candidateMaxLength < candidateMinLength)
        {
            return false;
        }

        var matchingPrefixLength = CountMatchingCharClassPrefix(input, index, Math.Min(candidateMaxLength, remainingLength), charClass);
        runLength = Math.Min(matchingPrefixLength, candidateMaxLength);
        return runLength >= candidateMinLength;
    }

    private static void GetAnchoredValidatorSuffixBounds(
        AsciiSimplePatternAnchoredValidatorSegment[] segments,
        int startIndex,
        out int minLength,
        out int maxLength)
    {
        minLength = 0;
        maxLength = 0;
        for (var i = startIndex; i < segments.Length; i++)
        {
            var segment = segments[i];
            minLength += segment.MinLength;
            if (maxLength == int.MaxValue || segment.MaxLength == int.MaxValue)
            {
                maxLength = int.MaxValue;
                continue;
            }

            maxLength += segment.MaxLength;
        }
    }

    private static int CountMatchingCharClassPrefix(ReadOnlySpan<byte> input, int index, int maxLength, AsciiCharClass charClass)
    {
        var matchedLength = 0;
        while (matchedLength < maxLength && charClass.Contains(input[index + matchedLength]))
        {
            matchedLength++;
        }

        return matchedLength;
    }

    private static DirectMatchResult TryMatchHeadTailWithoutValidation(ReadOnlySpan<byte> input, AsciiSimplePatternAnchoredHeadTailRunPlan plan)
    {
        if (!plan.HeadCharClass!.Contains(input[0]))
        {
            return input[0] >= 0x80
                ? DirectMatchResult.NeedsValidation
                : DirectMatchResult.NoMatch;
        }

        var tailLength = input.Length - 1;
        if (tailLength < plan.TailMinLength)
        {
            return DirectMatchResult.NoMatch;
        }

        if (!plan.TailCharClass!.Negated && plan.TailSearchValues is { } tailSearchValues)
        {
            for (var i = 1; i < input.Length; i++)
            {
                var value = input[i];
                if (value >= 0x80)
                {
                    return DirectMatchResult.NeedsValidation;
                }

                if (!tailSearchValues.Contains(value))
                {
                    return DirectMatchResult.NoMatch;
                }
            }

            return DirectMatchResult.Match;
        }

        for (var i = 1; i < input.Length; i++)
        {
            var value = input[i];
            if (value >= 0x80)
            {
                return DirectMatchResult.NeedsValidation;
            }

            if (!plan.TailCharClass.Contains(value))
            {
                return DirectMatchResult.NoMatch;
            }
        }

        return DirectMatchResult.Match;
    }

    private static bool MatchesLiteral(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal, bool ignoreCase)
    {
        if (!ignoreCase)
        {
            return input.Slice(index, literal.Length).SequenceEqual(literal);
        }

        for (var i = 0; i < literal.Length; i++)
        {
            if (AsciiSearch.FoldCase(input[index + i]) != literal[i])
            {
                return false;
            }
        }

        return true;
    }
}
