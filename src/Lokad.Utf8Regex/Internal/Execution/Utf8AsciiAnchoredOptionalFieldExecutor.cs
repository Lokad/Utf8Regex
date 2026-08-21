using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiAnchoredOptionalFieldExecutor
{
    public static bool TryMatchWhole(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternAnchoredOptionalFieldPlan plan,
        bool allowTrailingNewline,
        out int matchedLength,
        out bool needsValidation)
    {
        static bool TryMatchCore(ReadOnlySpan<byte> input, AsciiSimplePatternAnchoredOptionalFieldPlan plan)
        {
            var minimumLength = plan.HeadMinCount + 1 + 1 + plan.TailCount;
            var maximumLength = plan.HeadMaxCount + 1 + 1 + 1 + 1 + plan.TailCount;
            if (input.Length < minimumLength || input.Length > maximumLength)
            {
                return false;
            }

            var index = 0;
            while (index < plan.HeadMaxCount &&
                index < input.Length &&
                plan.HeadClass.Contains(input[index]))
            {
                index++;
            }

            if (index < plan.HeadMinCount ||
                (uint)index >= (uint)input.Length ||
                !plan.FirstRequiredClass.Contains(input[index]))
            {
                return false;
            }

            index++;
            var optionalStart = index;
            if ((uint)index < (uint)input.Length && plan.OptionalClass.Contains(input[index]))
            {
                index++;
                if (TryMatchSuffix(input, index, plan))
                {
                    return true;
                }

                index = optionalStart;
            }

            return TryMatchSuffix(input, index, plan);
        }

        static bool TryMatchShortAsciiCore(
            ReadOnlySpan<byte> input,
            AsciiSimplePatternAnchoredOptionalFieldPlan plan)
        {
            if (input.Length is < 5 or > 8 ||
                !Utf8AsciiBytePredicates.IsLetter(input[^1]) ||
                !Utf8AsciiBytePredicates.IsLetter(input[^2]) ||
                !Utf8AsciiBytePredicates.IsDigit(input[^3]))
            {
                return false;
            }

            var prefixLength = input.Length - 3;
            if (prefixLength > 0 && input[prefixLength - 1] == plan.OptionalLiteral)
            {
                prefixLength--;
            }

            var index = 0;
            if (!Utf8AsciiBytePredicates.IsLetter(input[index++]))
            {
                return false;
            }

            if (index < prefixLength && Utf8AsciiBytePredicates.IsLetter(input[index]))
            {
                index++;
            }

            if (index >= prefixLength || !Utf8AsciiBytePredicates.IsDigit(input[index++]))
            {
                return false;
            }

            if (index < prefixLength && Utf8AsciiBytePredicates.IsLetterOrDigit(input[index]))
            {
                index++;
            }

            return index == prefixLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryMatchSuffix(
            ReadOnlySpan<byte> input,
            int index,
            AsciiSimplePatternAnchoredOptionalFieldPlan plan)
        {
            if ((uint)index < (uint)input.Length && input[index] == plan.OptionalLiteral)
            {
                index++;
            }

            if ((uint)index >= (uint)input.Length || !plan.SecondRequiredClass.Contains(input[index]))
            {
                return false;
            }

            index++;
            if (input.Length - index != plan.TailCount)
            {
                return false;
            }

            for (var i = 0; i < plan.TailCount; i++)
            {
                if (!plan.TailClass.Contains(input[index + i]))
                {
                    return false;
                }
            }

            return true;
        }

        matchedLength = 0;
        needsValidation = false;
        if (!plan.HasValue)
        {
            return false;
        }

        if (plan.CanUseShortAsciiWholeMatcher)
        {
            if (TryMatchShortAsciiCore(input, plan))
            {
                matchedLength = input.Length;
                return true;
            }

            if (allowTrailingNewline &&
                input.Length > 0 &&
                input[^1] == (byte)'\n' &&
                TryMatchShortAsciiCore(input[..^1], plan))
            {
                matchedLength = input.Length - 1;
                return true;
            }

            needsValidation = input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0;
            return false;
        }

        if (TryMatchCore(input, plan))
        {
            matchedLength = input.Length;
            return true;
        }

        if (allowTrailingNewline &&
            input.Length > 0 &&
            input[^1] == (byte)'\n' &&
            TryMatchCore(input[..^1], plan))
        {
            matchedLength = input.Length - 1;
            return true;
        }

        needsValidation = input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0;
        return false;
    }
}
