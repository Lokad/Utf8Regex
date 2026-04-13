namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryExtractAnchoredValidatorPlan(
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
        bool ignoreCase,
        out AsciiSimplePatternAnchoredValidatorPlan validatorPlan)
    {
        validatorPlan = default;
        if (!isStartAnchored || !isEndAnchored || branches.Length == 0)
        {
            return false;
        }

        if (branches.Length > 1 && ignoreCase)
        {
            return false;
        }

        List<AsciiSimplePatternAnchoredValidatorSegment>? aggregateSegments = null;
        foreach (var tokens in branches)
        {
            if (tokens.Length == 0)
            {
                return false;
            }

            var segments = new List<AsciiSimplePatternAnchoredValidatorSegment>();
            for (var i = 0; i < tokens.Length;)
            {
                var token = tokens[i];
                if (token.Kind == AsciiSimplePatternTokenKind.Dot)
                {
                    return false;
                }

                if (token.Kind == AsciiSimplePatternTokenKind.Literal)
                {
                    var start = i;
                    i++;
                    while (i < tokens.Length && tokens[i].Kind == AsciiSimplePatternTokenKind.Literal)
                    {
                        i++;
                    }

                    var literal = new byte[i - start];
                    for (var j = 0; j < literal.Length; j++)
                    {
                        literal[j] = tokens[start + j].Literal;
                    }

                    segments.Add(new AsciiSimplePatternAnchoredValidatorSegment(literal));
                    continue;
                }

                if (token.Kind != AsciiSimplePatternTokenKind.CharClass || token.CharClass is not { } charClass)
                {
                    return false;
                }

                var runLength = 1;
                i++;
                while (i < tokens.Length &&
                    tokens[i].Kind == AsciiSimplePatternTokenKind.CharClass &&
                    tokens[i].CharClass is { } nextClass &&
                    charClass.HasSameDefinition(nextClass))
                {
                    runLength++;
                    i++;
                }

                segments.Add(new AsciiSimplePatternAnchoredValidatorSegment(charClass, runLength, runLength));
            }

            if (aggregateSegments is null)
            {
                aggregateSegments = segments;
                continue;
            }

            if (!TryMergeAnchoredValidatorSegments(aggregateSegments, segments, ignoreCase, out var mergedSegments))
            {
                return false;
            }

            aggregateSegments = mergedSegments;
        }

        validatorPlan = new AsciiSimplePatternAnchoredValidatorPlan([.. aggregateSegments!], ignoreCase);
        return validatorPlan.HasValue;
    }

    private static bool TryMergeAnchoredValidatorSegments(
        List<AsciiSimplePatternAnchoredValidatorSegment> aggregate,
        List<AsciiSimplePatternAnchoredValidatorSegment> next,
        bool ignoreCase,
        out List<AsciiSimplePatternAnchoredValidatorSegment> merged)
    {
        merged = [];
        var failed = new HashSet<(int AggregateIndex, int NextIndex)>();
        return TryMergeAnchoredValidatorSegmentsCore(aggregate, 0, next, 0, ignoreCase, merged, failed);
    }

    private static bool TryMergeAnchoredValidatorSegmentsCore(
        List<AsciiSimplePatternAnchoredValidatorSegment> aggregate,
        int aggregateIndex,
        List<AsciiSimplePatternAnchoredValidatorSegment> next,
        int nextIndex,
        bool ignoreCase,
        List<AsciiSimplePatternAnchoredValidatorSegment> merged,
        HashSet<(int AggregateIndex, int NextIndex)> failed)
    {
        if (aggregateIndex == aggregate.Count && nextIndex == next.Count)
        {
            return true;
        }

        if (!failed.Add((aggregateIndex, nextIndex)))
        {
            return false;
        }

        if (aggregateIndex < aggregate.Count &&
            nextIndex < next.Count &&
            TryMergeAnchoredValidatorSegmentPair(aggregate[aggregateIndex], next[nextIndex], ignoreCase, out var mergedSegment))
        {
            merged.Add(mergedSegment);
            if (TryMergeAnchoredValidatorSegmentsCore(aggregate, aggregateIndex + 1, next, nextIndex + 1, ignoreCase, merged, failed))
            {
                return true;
            }

            merged.RemoveAt(merged.Count - 1);
        }

        if (aggregateIndex < aggregate.Count &&
            TryOptionalizeAnchoredValidatorSegment(aggregate[aggregateIndex], ignoreCase, out var optionalAggregate))
        {
            merged.Add(optionalAggregate);
            if (TryMergeAnchoredValidatorSegmentsCore(aggregate, aggregateIndex + 1, next, nextIndex, ignoreCase, merged, failed))
            {
                return true;
            }

            merged.RemoveAt(merged.Count - 1);
        }

        if (nextIndex < next.Count &&
            TryOptionalizeAnchoredValidatorSegment(next[nextIndex], ignoreCase, out var optionalNext))
        {
            merged.Add(optionalNext);
            if (TryMergeAnchoredValidatorSegmentsCore(aggregate, aggregateIndex, next, nextIndex + 1, ignoreCase, merged, failed))
            {
                return true;
            }

            merged.RemoveAt(merged.Count - 1);
        }

        return false;
    }

    private static bool TryMergeAnchoredValidatorSegmentPair(
        AsciiSimplePatternAnchoredValidatorSegment current,
        AsciiSimplePatternAnchoredValidatorSegment incoming,
        bool ignoreCase,
        out AsciiSimplePatternAnchoredValidatorSegment merged)
    {
        if (current.IsLiteral && incoming.IsLiteral)
        {
            if (current.Literal.AsSpan().SequenceEqual(incoming.Literal))
            {
                merged = current;
                return true;
            }

            merged = default;
            return false;
        }

        if (TryGetAnchoredValidatorSegmentCharClass(current, ignoreCase, out var currentClass) &&
            TryGetAnchoredValidatorSegmentCharClass(incoming, ignoreCase, out var incomingClass) &&
            currentClass.HasSameDefinition(incomingClass))
        {
            merged = new AsciiSimplePatternAnchoredValidatorSegment(
                currentClass,
                Math.Min(current.MinLength, incoming.MinLength),
                Math.Max(current.MaxLength, incoming.MaxLength));
            return true;
        }

        merged = default;
        return false;
    }

    private static bool TryOptionalizeAnchoredValidatorSegment(
        AsciiSimplePatternAnchoredValidatorSegment segment,
        bool ignoreCase,
        out AsciiSimplePatternAnchoredValidatorSegment optionalized)
    {
        if (TryGetAnchoredValidatorSegmentCharClass(segment, ignoreCase, out var charClass))
        {
            optionalized = new AsciiSimplePatternAnchoredValidatorSegment(charClass, 0, segment.MaxLength);
            return true;
        }

        optionalized = default;
        return false;
    }

    private static bool TryGetAnchoredValidatorSegmentCharClass(
        AsciiSimplePatternAnchoredValidatorSegment segment,
        bool ignoreCase,
        out AsciiCharClass charClass)
    {
        if (segment.CharClass is not null)
        {
            charClass = segment.CharClass;
            return true;
        }

        if (!ignoreCase && segment.Literal.Length == 1)
        {
            var matches = new bool[128];
            matches[segment.Literal[0]] = true;
            charClass = new AsciiCharClass(matches, negated: false);
            return true;
        }

        charClass = default!;
        return false;
    }

    private static bool TryExtractAnchoredBoundedDatePlan(
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
        out AsciiSimplePatternAnchoredBoundedDatePlan plan)
    {
        plan = default;
        if (!isStartAnchored || !isEndAnchored || branches.Length == 0)
        {
            return false;
        }

        var firstMin = int.MaxValue;
        var firstMax = 0;
        var secondMin = int.MaxValue;
        var secondMax = 0;
        var thirdMin = int.MaxValue;
        var thirdMax = 0;
        byte separator = 0;
        byte secondSeparator = 0;
        var initializedSeparators = false;

        foreach (var branch in branches)
        {
            if (!TryParseAnchoredBoundedDateBranch(
                branch,
                out var firstCount,
                out var secondCount,
                out var thirdCount,
                out var branchSeparator,
                out var branchSecondSeparator))
            {
                return false;
            }

            if (!initializedSeparators)
            {
                separator = branchSeparator;
                secondSeparator = branchSecondSeparator;
                initializedSeparators = true;
            }
            else if (separator != branchSeparator || secondSeparator != branchSecondSeparator)
            {
                return false;
            }

            firstMin = Math.Min(firstMin, firstCount);
            firstMax = Math.Max(firstMax, firstCount);
            secondMin = Math.Min(secondMin, secondCount);
            secondMax = Math.Max(secondMax, secondCount);
            thirdMin = Math.Min(thirdMin, thirdCount);
            thirdMax = Math.Max(thirdMax, thirdCount);
        }

        if (!initializedSeparators ||
            firstMin <= 0 ||
            secondMin <= 0 ||
            thirdMin <= 0 ||
            firstMax > byte.MaxValue ||
            secondMax > byte.MaxValue ||
            thirdMax > byte.MaxValue)
        {
            return false;
        }

        plan = new AsciiSimplePatternAnchoredBoundedDatePlan(
            (byte)firstMin,
            (byte)firstMax,
            (byte)secondMin,
            (byte)secondMax,
            (byte)thirdMin,
            (byte)thirdMax,
            separator,
            secondSeparator);
        return plan.HasValue;
    }

    private static bool TryExtractRepeatedDigitGroupPlan(
        AsciiSimplePatternToken[][] branches,
        bool ignoreCase,
        out AsciiSimplePatternRepeatedDigitGroupPlan plan)
    {
        plan = default;
        if (ignoreCase || branches.Length == 0)
        {
            return false;
        }

        AsciiCharClass? digitClass = null;
        AsciiCharClass? separatorClass = null;
        var repeatedGroupCount = -1;
        var groupDigitCount = -1;
        var trailingMinDigits = int.MaxValue;
        var trailingMaxDigits = 0;
        foreach (var branch in branches)
        {
            if (!TryParseRepeatedDigitGroupBranch(
                branch,
                out var branchDigitClass,
                out var branchSeparatorClass,
                out var branchRepeatedGroupCount,
                out var branchGroupDigitCount,
                out var branchTrailingDigits))
            {
                return false;
            }

            if (digitClass is null)
            {
                digitClass = branchDigitClass;
                separatorClass = branchSeparatorClass;
                repeatedGroupCount = branchRepeatedGroupCount;
                groupDigitCount = branchGroupDigitCount;
            }
            else if (!digitClass.HasSameDefinition(branchDigitClass) ||
                separatorClass is null ||
                !separatorClass.HasSameDefinition(branchSeparatorClass) ||
                repeatedGroupCount != branchRepeatedGroupCount ||
                groupDigitCount != branchGroupDigitCount)
            {
                return false;
            }

            trailingMinDigits = Math.Min(trailingMinDigits, branchTrailingDigits);
            trailingMaxDigits = Math.Max(trailingMaxDigits, branchTrailingDigits);
        }

        if (digitClass is null ||
            separatorClass is null ||
            !digitClass.TryGetKnownPredicateKind(out var digitPredicate) ||
            digitPredicate != AsciiCharClassPredicateKind.Digit)
        {
            return false;
        }

        var separatorBytes = separatorClass.GetPositiveMatchBytes();
        if (separatorBytes.Length == 0 || separatorBytes.Length > 4)
        {
            return false;
        }

        plan = new AsciiSimplePatternRepeatedDigitGroupPlan(
            (byte)repeatedGroupCount,
            (byte)groupDigitCount,
            (byte)trailingMinDigits,
            (byte)trailingMaxDigits,
            separatorBytes);
        return plan.HasValue;
    }

    private static bool TryParseRepeatedDigitGroupBranch(
        AsciiSimplePatternToken[] branch,
        out AsciiCharClass digitClass,
        out AsciiCharClass separatorClass,
        out int repeatedGroupCount,
        out int groupDigitCount,
        out int trailingDigits)
    {
        digitClass = null!;
        separatorClass = null!;
        repeatedGroupCount = 0;
        groupDigitCount = 0;
        trailingDigits = 0;
        if (branch.Length < 6 ||
            branch[0].Kind != AsciiSimplePatternTokenKind.CharClass ||
            branch[0].CharClass is not { } firstDigitClass)
        {
            return false;
        }

        var separatorIndex = 0;
        while (separatorIndex < branch.Length &&
            branch[separatorIndex].Kind == AsciiSimplePatternTokenKind.CharClass &&
            branch[separatorIndex].CharClass is { } sameDigitClass &&
            firstDigitClass.HasSameDefinition(sameDigitClass))
        {
            separatorIndex++;
        }

        if (separatorIndex == 0 ||
            separatorIndex >= branch.Length ||
            branch[separatorIndex].Kind != AsciiSimplePatternTokenKind.CharClass ||
            branch[separatorIndex].CharClass is not { Negated: false } foundSeparatorClass)
        {
            return false;
        }

        digitClass = firstDigitClass;
        separatorClass = foundSeparatorClass;
        groupDigitCount = separatorIndex;

        var cursor = 0;
        while (cursor + groupDigitCount < branch.Length &&
            MatchesRepeatedDigitGroupDigitRun(branch, cursor, digitClass, groupDigitCount) &&
            branch[cursor + groupDigitCount].Kind == AsciiSimplePatternTokenKind.CharClass &&
            branch[cursor + groupDigitCount].CharClass is { } nextSeparatorClass &&
            separatorClass.HasSameDefinition(nextSeparatorClass))
        {
            repeatedGroupCount++;
            cursor += groupDigitCount + 1;
        }

        trailingDigits = branch.Length - cursor;
        if (repeatedGroupCount <= 0 ||
            trailingDigits <= 0 ||
            trailingDigits > groupDigitCount ||
            !MatchesRepeatedDigitGroupDigitRun(branch, cursor, digitClass, trailingDigits))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesRepeatedDigitGroupDigitRun(
        AsciiSimplePatternToken[] branch,
        int start,
        AsciiCharClass digitClass,
        int length)
    {
        if (start + length > branch.Length)
        {
            return false;
        }

        for (var i = 0; i < length; i++)
        {
            if (branch[start + i].Kind != AsciiSimplePatternTokenKind.CharClass ||
                branch[start + i].CharClass is not { } nextDigitClass ||
                !digitClass.HasSameDefinition(nextDigitClass))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseAnchoredBoundedDateBranch(
        AsciiSimplePatternToken[] tokens,
        out int firstCount,
        out int secondCount,
        out int thirdCount,
        out byte separator,
        out byte secondSeparator)
    {
        firstCount = 0;
        secondCount = 0;
        thirdCount = 0;
        separator = 0;
        secondSeparator = 0;

        var index = 0;
        if (!TryConsumeDigitTokenRun(tokens, ref index, out firstCount) ||
            !TryConsumeLiteralByte(tokens, ref index, out separator) ||
            !TryConsumeDigitTokenRun(tokens, ref index, out secondCount) ||
            !TryConsumeLiteralByte(tokens, ref index, out secondSeparator) ||
            !TryConsumeDigitTokenRun(tokens, ref index, out thirdCount))
        {
            return false;
        }

        return index == tokens.Length;
    }

    private static bool TryConsumeDigitTokenRun(AsciiSimplePatternToken[] tokens, ref int index, out int count)
    {
        count = 0;
        while ((uint)index < (uint)tokens.Length &&
            tokens[index].Kind == AsciiSimplePatternTokenKind.CharClass &&
            tokens[index].CharClass is { } charClass &&
            charClass.TryGetKnownPredicateKind(out var predicateKind) &&
            predicateKind == AsciiCharClassPredicateKind.Digit)
        {
            count++;
            index++;
        }

        return count > 0;
    }

    private static bool TryConsumeLiteralByte(AsciiSimplePatternToken[] tokens, ref int index, out byte value)
    {
        value = 0;
        if ((uint)index >= (uint)tokens.Length || tokens[index].Kind != AsciiSimplePatternTokenKind.Literal)
        {
            return false;
        }

        value = tokens[index].Literal;
        index++;
        return true;
    }
}
