using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8QuotedLineSegmentExecutor
{
    public static int CountOrFallback(ReadOnlySpan<byte> input, Utf8FallbackDirectFamilyPlan family, Regex fallbackRegex)
    {
        if (family.LiteralUtf8 is not { Length: > 0 } requiredPrefix)
        {
            return fallbackRegex.Count(System.Text.Encoding.UTF8.GetString(input));
        }

        return CountViaLineWalk(input, requiredPrefix, family.SecondaryLiteralUtf8, fallbackRegex);
    }

    public static int CountViaCandidateScan(ReadOnlySpan<byte> input, ReadOnlySpan<byte> requiredPrefix, ReadOnlySpan<byte> optionalSegment, out bool requiresFallback)
    {
        var prefixFinder = new Utf8AsciiLiteralFinder(requiredPrefix);
        var optionalSegmentBytes = optionalSegment.ToArray();
        return Utf8LinePrefixExecutor.CountMatchingLines(
            input,
            prefixFinder,
            trimLeadingAsciiWhitespace: false,
            (ReadOnlySpan<byte> line, out bool lineRequiresFallback) => TryMatchSingleLineAnchoredQuotedSegment(line, optionalSegmentBytes, out lineRequiresFallback),
            out requiresFallback);
    }

    public static int CountViaLineWalk(ReadOnlySpan<byte> input, ReadOnlySpan<byte> requiredPrefix, ReadOnlySpan<byte> optionalSegment, Regex fallbackRegex)
    {
        if (requiredPrefix.IsEmpty)
        {
            return fallbackRegex.Count(System.Text.Encoding.UTF8.GetString(input));
        }

        var count = 0;
        var searchFrom = 0;
        while ((uint)searchFrom <= (uint)(input.Length - requiredPrefix.Length))
        {
            var relativePrefix = input[searchFrom..].IndexOf(requiredPrefix);
            if (relativePrefix < 0)
            {
                break;
            }

            var lineStart = searchFrom + relativePrefix;
            if (lineStart > 0 && input[lineStart - 1] != (byte)'\n')
            {
                searchFrom = lineStart + 1;
                continue;
            }

            var lineBreakOffset = input[lineStart..].IndexOf((byte)'\n');
            var lineEnd = lineBreakOffset >= 0 ? lineStart + lineBreakOffset : input.Length;

            var requiresFallback = false;
            if ((uint)(lineEnd - lineStart) >= (uint)requiredPrefix.Length &&
                TryMatchSingleLineAnchoredQuotedSegment(input[lineStart..lineEnd], optionalSegment, out requiresFallback))
            {
                count++;
                searchFrom = lineEnd >= input.Length ? input.Length : lineEnd + 1;
            }
            else if (requiresFallback)
            {
                return fallbackRegex.Count(System.Text.Encoding.UTF8.GetString(input));
            }
            else
            {
                searchFrom = lineEnd >= input.Length ? input.Length : lineEnd + 1;
            }
        }

        return count;
    }

    private static bool TryMatchSingleLineAnchoredQuotedSegment(ReadOnlySpan<byte> line, ReadOnlySpan<byte> optionalSegment, out bool requiresFallback)
    {
        requiresFallback = false;
        var index = 0;
        index += ReadLeadingAsciiToken(line[index..]);
        if (index <= 0)
        {
            return false;
        }

        if (!TryConsumeAsciiWhitespace(line, ref index, requireOne: true))
        {
            return false;
        }

        if (!optionalSegment.IsEmpty && line[index..].StartsWith(optionalSegment))
        {
            index += optionalSegment.Length;
            if (!TryConsumeAsciiWhitespace(line, ref index, requireOne: true))
            {
                return false;
            }
        }

        if ((uint)index >= (uint)line.Length || line[index] != (byte)'"')
        {
            return false;
        }

        index++;
        while ((uint)index < (uint)line.Length)
        {
            var value = line[index];
            if (value == (byte)'"')
            {
                index++;
                return true;
            }

            if (value == (byte)'\\')
            {
                return false;
            }

            if (value >= 0x80)
            {
                requiresFallback = true;
                return false;
            }

            index++;
        }

        requiresFallback = true;
        return false;
    }

    private static int ReadLeadingAsciiToken(ReadOnlySpan<byte> input)
    {
        var index = 0;
        while ((uint)index < (uint)input.Length)
        {
            var value = input[index];
            if ((uint)((byte)(value | 0x20) - (byte)'a') > (uint)('z' - 'a'))
            {
                break;
            }

            index++;
        }

        return index;
    }

    private static bool TryConsumeAsciiWhitespace(ReadOnlySpan<byte> line, ref int index, bool requireOne)
    {
        var start = index;
        while ((uint)index < (uint)line.Length && IsAsciiWhitespace(line[index]))
        {
            index++;
        }

        return !requireOne || index > start;
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or 0x0B or 0x0C;
    }
}
