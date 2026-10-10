using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8UriTokenCountExecutor
{
    public static int CountWellFormed(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var searchFrom = 0;
        while ((uint)searchFrom < (uint)input.Length)
        {
            var relative = input[searchFrom..].IndexOf("://"u8);
            if (relative < 0)
            {
                break;
            }

            var delimiterIndex = searchFrom + relative;
            if (!TryFindSchemeStart(input, searchFrom, delimiterIndex, out _) ||
                !TryConsumeBody(input, delimiterIndex + 3, out var tokenEnd))
            {
                searchFrom = delimiterIndex + 3;
                continue;
            }

            count++;
            searchFrom = tokenEnd;
        }

        return count;
    }

    public static Utf8SelectedCountKernelMetrics InspectMetrics(ReadOnlySpan<byte> input)
    {
        var candidates = 0;
        var matches = 0;
        var searchFrom = 0;
        while ((uint)searchFrom < (uint)input.Length)
        {
            var relative = input[searchFrom..].IndexOf("://"u8);
            if (relative < 0)
            {
                break;
            }

            candidates++;
            var delimiterIndex = searchFrom + relative;
            if (!TryFindSchemeStart(input, searchFrom, delimiterIndex, out _) ||
                !TryConsumeBody(input, delimiterIndex + 3, out var tokenEnd))
            {
                searchFrom = delimiterIndex + 3;
                continue;
            }

            matches++;
            searchFrom = tokenEnd;
        }

        return new Utf8SelectedCountKernelMetrics(
            "FallbackDirect/Utf8UriToken",
            ":// delimiter probes",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
    }

    private static bool TryFindSchemeStart(
        ReadOnlySpan<byte> input,
        int minStartIndex,
        int delimiterIndex,
        out int schemeStart)
    {
        schemeStart = delimiterIndex;
        while (schemeStart > minStartIndex)
        {
            var value = input[schemeStart - 1];
            if (value < 0x80)
            {
                if (!Utf8AsciiBytePredicates.IsWord(value))
                {
                    break;
                }

                schemeStart--;
                continue;
            }

            var scalarStart = schemeStart - 1;
            while (scalarStart > minStartIndex && IsContinuationByte(input[scalarStart]))
            {
                scalarStart--;
            }

            if (Rune.DecodeFromUtf8(input[scalarStart..schemeStart], out var scalar, out var consumed) != OperationStatus.Done ||
                consumed != schemeStart - scalarStart ||
                !Utf8ScalarWordChar.IsWord(scalar))
            {
                break;
            }

            schemeStart = scalarStart;
        }

        return schemeStart < delimiterIndex;
    }

    private static bool TryConsumeBody(ReadOnlySpan<byte> input, int bodyStart, out int tokenEnd)
    {
        tokenEnd = bodyStart;
        // Host and continuation each require one complete scalar.
        if (!TryConsumeBodyScalar(input, ref tokenEnd, allowSlash: false) ||
            !TryConsumeBodyScalar(input, ref tokenEnd, allowSlash: true)) return false;
        while (TryConsumeBodyScalar(input, ref tokenEnd, allowSlash: true)) { }

        if ((uint)tokenEnd < (uint)input.Length && input[tokenEnd] == (byte)'?')
        {
            tokenEnd++;
            while (TryConsumeNonWhitespaceScalar(input, ref tokenEnd, excludedAscii: (byte)'#'))
            {
            }
        }

        if ((uint)tokenEnd < (uint)input.Length && input[tokenEnd] == (byte)'#')
        {
            tokenEnd++;
            while (TryConsumeNonWhitespaceScalar(input, ref tokenEnd, excludedAscii: 0))
            {
            }
        }

        return true;
    }

    private static bool TryConsumeBodyScalar(
        ReadOnlySpan<byte> input,
        ref int index,
        bool allowSlash)
    {
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        var value = input[index];
        if (value < 0x80)
        {
            if ((!allowSlash && value == (byte)'/') ||
                value is (byte)'?' or (byte)'#' ||
                char.IsWhiteSpace((char)value))
            {
                return false;
            }

            index++;
            return true;
        }

        if (Rune.DecodeFromUtf8(input[index..], out var scalar, out var consumed) != OperationStatus.Done ||
            Rune.IsWhiteSpace(scalar))
        {
            return false;
        }

        index += consumed;
        return true;
    }

    private static bool TryConsumeNonWhitespaceScalar(
        ReadOnlySpan<byte> input,
        ref int index,
        byte excludedAscii)
    {
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        var value = input[index];
        if (value < 0x80)
        {
            if ((excludedAscii != 0 && value == excludedAscii) || char.IsWhiteSpace((char)value))
            {
                return false;
            }

            index++;
            return true;
        }

        if (Rune.DecodeFromUtf8(input[index..], out var scalar, out var consumed) != OperationStatus.Done ||
            Rune.IsWhiteSpace(scalar))
        {
            return false;
        }

        index += consumed;
        return true;
    }

    private static bool IsContinuationByte(byte value) => (value & 0xC0) == 0x80;
}
