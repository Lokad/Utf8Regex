using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiAnchoredUrlExecutor
{
    public static bool TryMatchDigitsQueryWhole(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, out int matchedLength)
    {
        matchedLength = 0;
        if (ContainsNewLine(input))
        {
            return false;
        }

        if (TryMatchAnyPrefix(input, plan, TryMatchDigitsQuerySuffix))
        {
            matchedLength = input.Length;
            return true;
        }

        return false;
    }

    public static bool TryMatchHexQueryWhole(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, out int matchedLength)
    {
        matchedLength = 0;
        if (ContainsNewLine(input))
        {
            return false;
        }

        if (TryMatchAnyPrefix(input, plan, TryMatchHexQuerySuffix))
        {
            matchedLength = input.Length;
            return true;
        }

        return false;
    }

    private delegate bool SuffixMatcher(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, ref int index);

    private static bool TryMatchAnyPrefix(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, SuffixMatcher matcher)
    {
        return TryMatchAnyPrefix(input, plan.PrimaryPrefixUtf8 ?? [], plan, matcher) ||
               TryMatchAnyPrefix(input, plan.SecondaryPrefixUtf8 ?? [], plan, matcher) ||
               TryMatchAnyPrefix(input, plan.RelativePrefixUtf8 ?? [], plan, matcher);
    }

    private static bool TryMatchAnyPrefix(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix, in Utf8FallbackDirectFamilyPlan plan, SuffixMatcher matcher)
    {
        if (prefix.IsEmpty)
        {
            return false;
        }

        var searchFrom = 0;
        while (TryFindPrefix(input, prefix, searchFrom, out var suffixIndex, out var prefixIndex))
        {
            var local = suffixIndex;
            if (matcher(input, plan, ref local))
            {
                return true;
            }

            searchFrom = prefixIndex + 1;
        }

        return false;
    }

    private static bool TryFindPrefix(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix, int startIndex, out int suffixIndex, out int prefixIndex)
    {
        prefixIndex = AsciiSearch.IndexOfIgnoreCase(input[startIndex..], prefix);
        if (prefixIndex < 0)
        {
            suffixIndex = 0;
            prefixIndex = 0;
            return false;
        }

        prefixIndex += startIndex;
        suffixIndex = prefixIndex + prefix.Length;
        return true;
    }

    private static bool TryMatchDigitsQuerySuffix(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, ref int index)
    {
        if (!TryConsumeOptionalTrigram(input, ref index) ||
            !ConsumeIgnoreCase(input, ref index, plan.RouteMarkerUtf8 ?? []) ||
            !ConsumeDigits(input, ref index))
        {
            return false;
        }

        ConsumeOptionalByte(input, ref index, (byte)'/');
        if (!TryConsumeRequiredParameter(input, plan.RequiredParameterUtf8 ?? [], ref index))
        {
            return false;
        }

        return true;
    }

    private static bool TryMatchHexQuerySuffix(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, ref int index)
    {
        if (!TryConsumeOptionalTrigram(input, ref index) ||
            !ConsumeIgnoreCase(input, ref index, plan.RouteMarkerUtf8 ?? []))
        {
            return false;
        }

        ConsumeHexDigits(input, ref index);
        TryConsumeOptionalParameter(input, plan.OptionalParameterUtf8 ?? [], ref index);
        if (!TryConsumeRequiredParameter(input, plan.RequiredParameterUtf8 ?? [], ref index))
        {
            return false;
        }

        TryConsumeOptionalParameter(input, plan.OptionalParameterUtf8 ?? [], ref index);
        return true;
    }

    private static bool TryConsumeOptionalTrigram(ReadOnlySpan<byte> input, ref int index)
    {
        if ((uint)index >= (uint)input.Length || input[index] != (byte)'/')
        {
            return true;
        }

        if ((uint)(input.Length - index) >= 3 &&
            AsciiSearch.MatchesIgnoreCase(input.Slice(index, 3), "/d/"u8))
        {
            return true;
        }

        if ((uint)(input.Length - index) >= 2 &&
            input[index + 1] != (byte)'/' &&
            input[index + 1] != (byte)'?' &&
            input[index + 1] != (byte)'&')
        {
            var start = ++index;
            while ((uint)index < (uint)input.Length && IsAsciiLetterOrDigit(input[index]))
            {
                index++;
            }

            return index > start;
        }

        return true;
    }

    private static bool TryConsumeOptionalParameter(ReadOnlySpan<byte> input, ReadOnlySpan<byte> parameterUtf8, ref int index)
    {
        if (parameterUtf8.IsEmpty)
        {
            return true;
        }

        var original = index;
        if (TryConsumeParameterMarker(input, parameterUtf8, ref index))
        {
            return ConsumeUntilAny(input, ref index, (byte)'&', (byte)' ');
        }

        index = original;
        return false;
    }

    private static bool TryConsumeRequiredParameter(ReadOnlySpan<byte> input, ReadOnlySpan<byte> parameterUtf8, ref int index)
    {
        return TryConsumeParameterMarker(input, parameterUtf8, ref index) &&
            ConsumeUntilAny(input, ref index, (byte)'&', (byte)' ');
    }

    private static bool TryConsumeParameterMarker(ReadOnlySpan<byte> input, ReadOnlySpan<byte> parameterUtf8, ref int index)
    {
        if (parameterUtf8.IsEmpty)
        {
            return false;
        }

        if ((uint)index >= (uint)input.Length || (input[index] != (byte)'?' && input[index] != (byte)'&'))
        {
            return false;
        }

        index++;
        return ConsumeIgnoreCase(input, ref index, parameterUtf8);
    }

    private static bool ConsumeIgnoreCase(ReadOnlySpan<byte> input, ref int index, ReadOnlySpan<byte> literal)
    {
        if ((uint)(input.Length - index) < (uint)literal.Length ||
            !AsciiSearch.MatchesIgnoreCase(input.Slice(index, literal.Length), literal))
        {
            return false;
        }

        index += literal.Length;
        return true;
    }

    private static bool ConsumeDigits(ReadOnlySpan<byte> input, ref int index)
    {
        var start = index;
        while ((uint)index < (uint)input.Length && (uint)(input[index] - (byte)'0') <= 9)
        {
            index++;
        }

        return index > start;
    }

    private static void ConsumeHexDigits(ReadOnlySpan<byte> input, ref int index)
    {
        while ((uint)index < (uint)input.Length && IsAsciiHex(input[index]))
        {
            index++;
        }
    }

    private static bool ConsumeUntilAny(ReadOnlySpan<byte> input, ref int index, byte stop1, byte stop2)
    {
        var start = index;
        while ((uint)index < (uint)input.Length)
        {
            var value = input[index];
            if (value == stop1 || value == stop2 || value == (byte)'\n' || value == (byte)'\r')
            {
                break;
            }

            index++;
        }

        return index > start;
    }

    private static void ConsumeOptionalByte(ReadOnlySpan<byte> input, ref int index, byte value)
    {
        if ((uint)index < (uint)input.Length && input[index] == value)
        {
            index++;
        }
    }

    private static bool ContainsNewLine(ReadOnlySpan<byte> input)
        => input.IndexOfAny((byte)'\n', (byte)'\r') >= 0;

    private static bool IsAsciiLetterOrDigit(byte value)
    {
        value = AsciiSearch.FoldCase(value);
        return (uint)(value - (byte)'a') <= (byte)('z' - 'a') || (uint)(value - (byte)'0') <= 9;
    }

    private static bool IsAsciiHex(byte value)
    {
        value = AsciiSearch.FoldCase(value);
        return (uint)(value - (byte)'0') <= 9 || (uint)(value - (byte)'a') <= (byte)('f' - 'a');
    }
}
