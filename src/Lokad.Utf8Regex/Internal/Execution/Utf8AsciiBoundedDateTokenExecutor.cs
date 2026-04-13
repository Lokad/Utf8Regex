namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiBoundedDateTokenExecutor
{
    public static bool TryMatchWhole(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredBoundedDatePlan plan,
        bool allowTrailingNewline,
        out int matchedLength,
        out bool needsValidation)
    {
        matchedLength = 0;
        needsValidation = input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0;
        if (needsValidation)
        {
            return false;
        }

        var inputLength = input.Length;
        if (allowTrailingNewline &&
            inputLength > 0 &&
            input[^1] == (byte)'\n')
        {
            inputLength--;
        }

        var index = 0;
        if (!TryConsumeDigits(input, ref index, plan.FirstFieldMinCount, plan.FirstFieldMaxCount, inputLength) ||
            !TryConsumeByte(input, ref index, plan.SeparatorByte, inputLength) ||
            !TryConsumeDigits(input, ref index, plan.SecondFieldMinCount, plan.SecondFieldMaxCount, inputLength) ||
            !TryConsumeByte(input, ref index, plan.SecondSeparatorByte, inputLength) ||
            !TryConsumeDigits(input, ref index, plan.ThirdFieldMinCount, plan.ThirdFieldMaxCount, inputLength) ||
            index != inputLength)
        {
            return false;
        }

        matchedLength = inputLength;
        return true;
    }

    public static int CountAsciiBoundedDateTokens(ReadOnlySpan<byte> input, Utf8FallbackDirectFamilyPlan family)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindAsciiBoundedDateToken(input, startIndex, family, out var matchIndex, out var matchedLength))
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public static bool TryFindAsciiBoundedDateToken(ReadOnlySpan<byte> input, int startIndex, Utf8FallbackDirectFamilyPlan family, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length)
        {
            return false;
        }

        var searchFrom = Math.Max(startIndex, 0);
        while (searchFrom <= input.Length - 6)
        {
            var relative = input[searchFrom..].IndexOfAnyInRange((byte)'0', (byte)'9');
            if (relative < 0)
            {
                return false;
            }

            var candidateStart = searchFrom + relative;
            if (TryMatchAt(input, candidateStart, family, out matchedLength))
            {
                matchIndex = candidateStart;
                return true;
            }

            searchFrom = candidateStart + 1;
        }

        return false;
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, int startIndex, Utf8FallbackDirectFamilyPlan family, out int matchedLength)
    {
        matchedLength = 0;

        if (family.RequireLeadingBoundary && !HasLeadingBoundary(input, startIndex))
        {
            return false;
        }

        var index = startIndex;
        if (!TryConsumeDigits(input, ref index, family.FirstFieldMinCount, family.FirstFieldMaxCount) ||
            !TryConsumeByte(input, ref index, family.SeparatorByte) ||
            !TryConsumeDigits(input, ref index, family.SecondFieldMinCount, family.SecondFieldMaxCount) ||
            !TryConsumeByte(input, ref index, family.SecondSeparatorByte) ||
            !TryConsumeDigits(input, ref index, family.ThirdFieldMinCount, family.ThirdFieldMaxCount) ||
            (family.RequireTrailingBoundary && !HasTrailingBoundary(input, index)))
        {
            return false;
        }

        matchedLength = index - startIndex;
        return true;
    }

    private static bool TryConsumeDigits(ReadOnlySpan<byte> input, ref int index, int minCount, int maxCount)
    {
        return TryConsumeDigits(input, ref index, minCount, maxCount, input.Length);
    }

    private static bool TryConsumeDigits(ReadOnlySpan<byte> input, ref int index, int minCount, int maxCount, int inputLength)
    {
        var count = 0;
        while (index < inputLength &&
            count < maxCount &&
            IsAsciiDigit(input[index]))
        {
            index++;
            count++;
        }

        return count >= minCount;
    }

    private static bool TryConsumeByte(ReadOnlySpan<byte> input, ref int index, byte value)
    {
        return TryConsumeByte(input, ref index, value, input.Length);
    }

    private static bool TryConsumeByte(ReadOnlySpan<byte> input, ref int index, byte value, int inputLength)
    {
        if (index >= inputLength || input[index] != value)
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool HasLeadingBoundary(ReadOnlySpan<byte> input, int index)
    {
        return index <= 0 || !IsAsciiWordByte(input[index - 1]);
    }

    private static bool HasTrailingBoundary(ReadOnlySpan<byte> input, int index)
    {
        return (uint)index >= (uint)input.Length || !IsAsciiWordByte(input[index]);
    }

    private static bool IsAsciiDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static bool IsAsciiWordByte(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9' or
            >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'a' and <= (byte)'z' or
            (byte)'_';
    }
}
