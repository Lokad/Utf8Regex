namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiRepeatedDigitGroupExecutor
{
    public static bool TryFind(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternRepeatedDigitGroupPlan plan,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if (!plan.HasValue || input.Length < plan.MinimumLength)
        {
            return false;
        }

        var searchFrom = 0;
        while (searchFrom <= input.Length - plan.MinimumLength)
        {
            var separatorSearchStart = searchFrom + plan.GroupDigitCount;
            if (separatorSearchStart > input.Length - 1)
            {
                return false;
            }

            var separatorRelative = input[separatorSearchStart..].IndexOfAny(plan.SeparatorBytes);
            if (separatorRelative < 0)
            {
                return false;
            }

            var separatorIndex = separatorSearchStart + separatorRelative;
            var candidateStart = separatorIndex - plan.GroupDigitCount;
            if (candidateStart < searchFrom)
            {
                searchFrom = separatorIndex + 1;
                continue;
            }

            if (TryMatchAt(input, candidateStart, plan, out matchedLength))
            {
                matchIndex = candidateStart;
                return true;
            }

            searchFrom = candidateStart + 1;
        }

        return false;
    }

    public static bool TryMatchWhole(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternRepeatedDigitGroupPlan plan,
        out int matchedLength,
        out bool needsValidation)
    {
        matchedLength = 0;
        needsValidation = input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0;
        if (needsValidation)
        {
            return false;
        }

        if (!plan.HasValue || input.Length < plan.MinimumLength || input.Length > plan.MaximumLength)
        {
            return false;
        }

        if (CanUseSmallWholeMatchFastPath(plan) &&
            TryMatchWholeSmall(input, plan, out matchedLength))
        {
            return true;
        }

        if (!TryMatchAt(input, 0, plan, out matchedLength))
        {
            return false;
        }

        return matchedLength == input.Length;
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, int startIndex, AsciiSimplePatternRepeatedDigitGroupPlan plan, out int matchedLength)
    {
        matchedLength = 0;
        var index = startIndex;
        for (var group = 0; group < plan.RepeatedGroupCount; group++)
        {
            if (!TryConsumeDigits(input, ref index, plan.GroupDigitCount) ||
                !TryConsumeAny(input, ref index, plan.SeparatorBytes))
            {
                return false;
            }
        }

        var trailingDigits = 0;
        while (index < input.Length &&
            trailingDigits < plan.TrailingMaxDigits &&
            IsAsciiDigit(input[index]))
        {
            index++;
            trailingDigits++;
        }

        if (trailingDigits < plan.TrailingMinDigits)
        {
            return false;
        }

        matchedLength = index - startIndex;
        return true;
    }

    private static bool CanUseSmallWholeMatchFastPath(AsciiSimplePatternRepeatedDigitGroupPlan plan)
    {
        return plan.RepeatedGroupCount <= 4 &&
            plan.GroupDigitCount is >= 1 and <= 4 &&
            plan.TrailingMinDigits >= 1 &&
            plan.TrailingMaxDigits <= 4 &&
            plan.SeparatorBytes.Length is 1 or 2;
    }

    private static bool TryMatchWholeSmall(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternRepeatedDigitGroupPlan plan,
        out int matchedLength)
    {
        matchedLength = 0;
        var index = 0;
        for (var group = 0; group < plan.RepeatedGroupCount; group++)
        {
            if (!TryConsumeSmallDigitRun(input, ref index, plan.GroupDigitCount) ||
                !TryConsumeSmallSeparator(input, ref index, plan.SeparatorBytes))
            {
                return false;
            }
        }

        var trailingDigits = input.Length - index;
        if (trailingDigits < plan.TrailingMinDigits || trailingDigits > plan.TrailingMaxDigits)
        {
            return false;
        }

        if (!TryConsumeSmallDigitRun(input, ref index, trailingDigits) || index != input.Length)
        {
            return false;
        }

        matchedLength = index;
        return true;
    }

    private static bool TryConsumeDigits(ReadOnlySpan<byte> input, ref int index, int count)
    {
        if (index + count > input.Length)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!IsAsciiDigit(input[index + i]))
            {
                return false;
            }
        }

        index += count;
        return true;
    }

    private static bool TryConsumeSmallDigitRun(ReadOnlySpan<byte> input, ref int index, int count)
    {
        if (index + count > input.Length)
        {
            return false;
        }

        switch (count)
        {
            case 1:
                if (!IsAsciiDigit(input[index]))
                {
                    return false;
                }
                break;

            case 2:
                if (!IsAsciiDigit(input[index]) ||
                    !IsAsciiDigit(input[index + 1]))
                {
                    return false;
                }
                break;

            case 3:
                if (!IsAsciiDigit(input[index]) ||
                    !IsAsciiDigit(input[index + 1]) ||
                    !IsAsciiDigit(input[index + 2]))
                {
                    return false;
                }
                break;

            case 4:
                if (!IsAsciiDigit(input[index]) ||
                    !IsAsciiDigit(input[index + 1]) ||
                    !IsAsciiDigit(input[index + 2]) ||
                    !IsAsciiDigit(input[index + 3]))
                {
                    return false;
                }
                break;

            default:
                return TryConsumeDigits(input, ref index, count);
        }

        index += count;
        return true;
    }

    private static bool TryConsumeAny(ReadOnlySpan<byte> input, ref int index, ReadOnlySpan<byte> values)
    {
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        if (values.IndexOf(input[index]) < 0)
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool TryConsumeSmallSeparator(ReadOnlySpan<byte> input, ref int index, ReadOnlySpan<byte> values)
    {
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        var value = input[index];
        var isMatch = values.Length == 1
            ? value == values[0]
            : value == values[0] || value == values[1];
        if (!isMatch)
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool IsAsciiDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';
}
