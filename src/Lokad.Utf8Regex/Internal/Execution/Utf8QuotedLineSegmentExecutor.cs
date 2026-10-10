using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8QuotedLineSegmentExecutor
{
    // The owned proof supplies an exact ASCII prefix, complete Unicode \s,
    // an optional non-quote literal, and a raw [^"\\]* body. Subjects arrive
    // strictly validated. LF is allowed in whitespace and inside the quotes.
    public static bool TryCountViaLineWalk(ReadOnlySpan<byte> input, ReadOnlySpan<byte> requiredPrefix, ReadOnlySpan<byte> optionalSegment, out int count)
    {
        count = 0;
        if (requiredPrefix.IsEmpty) return false;
        if (input.Length < requiredPrefix.Length) return true;
        var searchFrom = 0;
        while (searchFrom <= input.Length - requiredPrefix.Length)
        {
            var relative = input[searchFrom..].IndexOf(requiredPrefix);
            if (relative < 0) break;
            var candidate = searchFrom + relative;
            searchFrom = candidate + 1;
            if (candidate > 0 && input[candidate - 1] != (byte)'\n') continue;
            var index = candidate + requiredPrefix.Length;
            if (!ConsumeWhitespace(input, ref index)) continue;
            if (!optionalSegment.IsEmpty && input[index..].StartsWith(optionalSegment))
            {
                index += optionalSegment.Length;
                if (!ConsumeWhitespace(input, ref index)) continue;
            }
            if (index >= input.Length || input[index] != (byte)'"') continue;
            index++;
            // ASCII stops cannot occur inside a multibyte scalar.
            var stop = input[index..].IndexOfAny((byte)'"', (byte)'\\');
            if (stop < 0 || input[index + stop] != (byte)'"') continue;
            index += stop + 1;
            var lineBreak = input[index..].IndexOf((byte)'\n');
            var end = lineBreak < 0 ? input.Length : index + lineBreak;
            count++;
            searchFrom = end < input.Length ? end + 1 : input.Length;
        }
        return true;
    }

    private static bool ConsumeWhitespace(ReadOnlySpan<byte> input, ref int index)
    {
        var start = index;
        while (index < input.Length && Rune.DecodeFromUtf8(input[index..], out var scalar, out var consumed) == OperationStatus.Done && Rune.IsWhiteSpace(scalar))
            index += consumed;
        return index > start;
    }
}
