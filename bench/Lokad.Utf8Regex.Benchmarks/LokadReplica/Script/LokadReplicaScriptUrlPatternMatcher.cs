namespace Lokad.Utf8Regex.Benchmarks;

internal static class LokadScriptUrlPatternMatcher
{
    private static ReadOnlySpan<byte> DashboardPrefix => "https://go.lokad.com"u8;
    private static ReadOnlySpan<byte> DashboardTestingPrefix => "https://go.testing.lokad.com"u8;
    private static ReadOnlySpan<byte> DashboardRelativePrefix => "~"u8;
    private static ReadOnlySpan<byte> DashboardMarker => "/d/"u8;
    private static ReadOnlySpan<byte> DashboardTabMarker => "?t="u8;

    private static ReadOnlySpan<byte> DownloadPrefix => "https://go.lokad.com"u8;
    private static ReadOnlySpan<byte> DownloadTestingPrefix => "https://go.testing.lokad.com"u8;
    private static ReadOnlySpan<byte> DownloadRelativePrefix => "~"u8;
    private static ReadOnlySpan<byte> DownloadMarker => "/gateway/BigFiles/Browse/Download?hash="u8;
    private static ReadOnlySpan<byte> PathMarkerQuestion => "?path="u8;
    private static ReadOnlySpan<byte> PathMarkerAmpersand => "&path="u8;
    private static ReadOnlySpan<byte> NameMarkerQuestion => "?name="u8;
    private static ReadOnlySpan<byte> NameMarkerAmpersand => "&name="u8;

    public static bool IsDashboardMatch(ReadOnlySpan<byte> input)
    {
        if (ContainsNewLine(input))
        {
            return false;
        }

        return TryFindCanonicalPrefix(input, DashboardPrefix, out var prefixEnd) && IsDashboardSuffix(input, prefixEnd) ||
               TryFindCanonicalPrefix(input, DashboardTestingPrefix, out prefixEnd) && IsDashboardSuffix(input, prefixEnd) ||
               TryFindCanonicalPrefix(input, DashboardRelativePrefix, out prefixEnd) && IsDashboardSuffix(input, prefixEnd);
    }

    public static bool IsDownloadMatch(ReadOnlySpan<byte> input)
    {
        if (ContainsNewLine(input))
        {
            return false;
        }

        return TryFindCanonicalPrefix(input, DownloadPrefix, out var prefixEnd) && IsDownloadSuffix(input, prefixEnd) ||
               TryFindCanonicalPrefix(input, DownloadTestingPrefix, out prefixEnd) && IsDownloadSuffix(input, prefixEnd) ||
               TryFindCanonicalPrefix(input, DownloadRelativePrefix, out prefixEnd) && IsDownloadSuffix(input, prefixEnd);
    }

    private static bool TryFindCanonicalPrefix(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix, out int prefixEnd)
    {
        var index = input.IndexOf(prefix);
        if (index < 0)
        {
            prefixEnd = 0;
            return false;
        }

        prefixEnd = index + prefix.Length;
        return true;
    }

    private static bool IsDashboardSuffix(ReadOnlySpan<byte> input, int index)
    {
        if (TryConsumeOptionalTrigram(input, ref index) && Consume(input, ref index, DashboardMarker))
        {
            if (!ConsumeDigits(input, ref index))
            {
                return false;
            }

            ConsumeOptionalByte(input, ref index, (byte)'/');
            if (!Consume(input, ref index, DashboardTabMarker))
            {
                return false;
            }

            if (!ConsumeUntilAny(input, ref index, (byte)' ', (byte)'?'))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool IsDownloadSuffix(ReadOnlySpan<byte> input, int index)
    {
        if (TryConsumeOptionalTrigram(input, ref index) && Consume(input, ref index, DownloadMarker))
        {
            ConsumeHexDigits(input, ref index);
            if (TryConsumeOptionalPath(input, ref index))
            {
                // consumed
            }

            if (!TryConsumeName(input, ref index))
            {
                return false;
            }

            TryConsumeOptionalPath(input, ref index);
            return true;
        }

        return false;
    }

    private static bool TryConsumeOptionalTrigram(ReadOnlySpan<byte> input, ref int index)
    {
        if (!ConsumeOptionalByte(input, ref index, (byte)'/'))
        {
            return true;
        }

        return ConsumeAsciiLetterOrDigitRun(input, ref index);
    }

    private static bool TryConsumeOptionalPath(ReadOnlySpan<byte> input, ref int index)
    {
        var original = index;
        if (Consume(input, ref index, PathMarkerQuestion) || Consume(input, ref index, PathMarkerAmpersand))
        {
            return ConsumeUntilAny(input, ref index, (byte)'&', (byte)' ');
        }

        index = original;
        return false;
    }

    private static bool TryConsumeName(ReadOnlySpan<byte> input, ref int index)
    {
        if (!(Consume(input, ref index, NameMarkerQuestion) || Consume(input, ref index, NameMarkerAmpersand)))
        {
            return false;
        }

        return ConsumeUntilAny(input, ref index, (byte)'&', (byte)' ');
    }

    private static bool Consume(ReadOnlySpan<byte> input, ref int index, ReadOnlySpan<byte> literal)
    {
        if ((uint)(input.Length - index) < (uint)literal.Length || !input.Slice(index, literal.Length).SequenceEqual(literal))
        {
            return false;
        }

        index += literal.Length;
        return true;
    }

    private static bool ConsumeDigits(ReadOnlySpan<byte> input, ref int index)
    {
        var start = index;
        while ((uint)index < (uint)input.Length && (uint)(input[index] - '0') <= 9)
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

    private static bool ConsumeAsciiLetterOrDigitRun(ReadOnlySpan<byte> input, ref int index)
    {
        var start = index;
        while ((uint)index < (uint)input.Length && IsAsciiLetterOrDigit(input[index]))
        {
            index++;
        }

        return index > start;
    }

    private static bool ConsumeUntilAny(ReadOnlySpan<byte> input, ref int index, byte stop1, byte stop2)
    {
        var start = index;
        while ((uint)index < (uint)input.Length)
        {
            var value = input[index];
            if (value == stop1 || value == stop2 || value == (byte)'\n')
            {
                break;
            }

            index++;
        }

        return index > start;
    }

    private static bool ConsumeOptionalByte(ReadOnlySpan<byte> input, ref int index, byte value)
    {
        if ((uint)index < (uint)input.Length && input[index] == value)
        {
            index++;
            return true;
        }

        return false;
    }

    private static bool ContainsNewLine(ReadOnlySpan<byte> input)
    {
        return input.IndexOf((byte)'\n') >= 0 || input.IndexOf((byte)'\r') >= 0;
    }

    private static bool IsAsciiLetterOrDigit(byte value)
    {
        value |= 0x20;
        return (uint)(value - 'a') <= ('z' - 'a') || (uint)(value - '0') <= 9;
    }

    private static bool IsAsciiHex(byte value)
    {
        return (uint)(value - '0') <= 9 || (uint)((value | 0x20) - 'a') <= ('f' - 'a');
    }
}
