namespace Lokad.Utf8Regex.Internal.Execution;

internal delegate bool Utf8LinePrefixVerifier(ReadOnlySpan<byte> line, out bool requiresFallback);

internal static class Utf8LinePrefixExecutor
{
    public static int CountMatchingLines(
        ReadOnlySpan<byte> input,
        Utf8AsciiLiteralFinder prefixFinder,
        bool trimLeadingAsciiWhitespace,
        Utf8LinePrefixVerifier? verifier,
        out bool requiresFallback)
    {
        requiresFallback = false;
        if (prefixFinder.Literal.IsEmpty)
        {
            return 0;
        }

        var count = 0;
        var searchFrom = 0;
        while (prefixFinder.TryFindNext(input, searchFrom, out var candidateIndex))
        {
            var lineStart = FindLineStart(input, candidateIndex);
            var prefixStart = lineStart;
            if (trimLeadingAsciiWhitespace)
            {
                while ((uint)prefixStart < (uint)input.Length &&
                    input[prefixStart] != (byte)'\n' &&
                    IsAsciiWhitespace(input[prefixStart]))
                {
                    prefixStart++;
                }
            }

            if (prefixStart != candidateIndex)
            {
                searchFrom = candidateIndex + 1;
                continue;
            }

            var lineEnd = FindLineEnd(input, candidateIndex);
            var line = input[prefixStart..lineEnd];
            if (verifier is null)
            {
                count++;
            }
            else if (verifier(line, out var lineRequiresFallback))
            {
                count++;
            }
            else if (lineRequiresFallback)
            {
                requiresFallback = true;
                return count;
            }

            searchFrom = lineEnd >= input.Length ? input.Length : lineEnd + 1;
        }

        return count;
    }

    public static int CountLinesWithPrefix(ReadOnlySpan<byte> input, ReadOnlySpan<byte> linePrefix, bool trimLeadingAsciiWhitespace)
    {
        if (linePrefix.IsEmpty)
        {
            return 0;
        }

        var count = 0;
        var lineStart = 0;
        while ((uint)lineStart <= (uint)input.Length)
        {
            var lineEnd = lineStart;
            while ((uint)lineEnd < (uint)input.Length && input[lineEnd] != (byte)'\n')
            {
                lineEnd++;
            }

            var prefixStart = lineStart;
            if (trimLeadingAsciiWhitespace)
            {
                while ((uint)prefixStart < (uint)lineEnd && IsAsciiWhitespace(input[prefixStart]))
                {
                    prefixStart++;
                }
            }

            if ((uint)(lineEnd - prefixStart) >= (uint)linePrefix.Length &&
                input[prefixStart..].StartsWith(linePrefix))
            {
                count++;
            }

            if (lineEnd >= input.Length)
            {
                break;
            }

            lineStart = lineEnd + 1;
        }

        return count;
    }

    private static int FindLineStart(ReadOnlySpan<byte> input, int index)
    {
        while (index > 0 && input[index - 1] != (byte)'\n')
        {
            index--;
        }

        return index;
    }

    private static int FindLineEnd(ReadOnlySpan<byte> input, int index)
    {
        while ((uint)index < (uint)input.Length && input[index] != (byte)'\n')
        {
            index++;
        }

        return index;
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or 0x0B or 0x0C;
    }
}
