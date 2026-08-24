using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8WordDelimitedTokenExecutor
{
    public static int CountWellFormed(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var searchFrom = 0;
        while ((uint)searchFrom < (uint)input.Length)
        {
            var relative = input[searchFrom..].IndexOf((byte)'@');
            if (relative < 0)
            {
                break;
            }

            var delimiterIndex = searchFrom + relative;
            var tokenStart = delimiterIndex;
            while (tokenStart > searchFrom && TryConsumePreviousHead(input, ref tokenStart))
            {
            }

            if (tokenStart == delimiterIndex)
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var tokenEnd = delimiterIndex + 1;
            var bodyScalarCount = 0;
            var lastSecondaryIndex = -1;
            var hasSecondaryWithTail = false;
            while (TryConsumeNextBody(input, ref tokenEnd, out var scalarStart, out var isSecondary))
            {
                if (lastSecondaryIndex > delimiterIndex + 1)
                {
                    hasSecondaryWithTail = true;
                }

                if (isSecondary)
                {
                    lastSecondaryIndex = scalarStart;
                }

                bodyScalarCount++;
            }

            if (bodyScalarCount > 0 && hasSecondaryWithTail)
            {
                count++;
                searchFrom = tokenEnd;
            }
            else
            {
                searchFrom = delimiterIndex + 1;
            }
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
            var relative = input[searchFrom..].IndexOf((byte)'@');
            if (relative < 0)
            {
                break;
            }

            candidates++;
            var delimiterIndex = searchFrom + relative;
            var tokenStart = delimiterIndex;
            while (tokenStart > searchFrom && TryConsumePreviousHead(input, ref tokenStart))
            {
            }

            if (tokenStart == delimiterIndex)
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var tokenEnd = delimiterIndex + 1;
            var bodyScalarCount = 0;
            var lastSecondaryIndex = -1;
            var hasSecondaryWithTail = false;
            while (TryConsumeNextBody(input, ref tokenEnd, out var scalarStart, out var isSecondary))
            {
                if (lastSecondaryIndex > delimiterIndex + 1)
                {
                    hasSecondaryWithTail = true;
                }

                if (isSecondary)
                {
                    lastSecondaryIndex = scalarStart;
                }

                bodyScalarCount++;
            }

            if (bodyScalarCount > 0 && hasSecondaryWithTail)
            {
                matches++;
                searchFrom = tokenEnd;
            }
            else
            {
                searchFrom = delimiterIndex + 1;
            }
        }

        return new Utf8SelectedCountKernelMetrics(
            "FallbackDirect/Utf8WordDelimitedTokenCount",
            "@ delimiter probes",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
    }

    private static bool TryConsumePreviousHead(ReadOnlySpan<byte> input, ref int index)
    {
        var value = input[index - 1];
        if (value < 0x80)
        {
            if (!IsAsciiHead(value))
            {
                return false;
            }

            index--;
            return true;
        }

        var scalarStart = index - 1;
        while (scalarStart > 0 && IsContinuationByte(input[scalarStart]))
        {
            scalarStart--;
        }

        if (Rune.DecodeFromUtf8(input[scalarStart..index], out var scalar, out var consumed) != OperationStatus.Done ||
            consumed != index - scalarStart ||
            !DotNetUtf8WordChar.IsWord(scalar))
        {
            return false;
        }

        index = scalarStart;
        return true;
    }

    private static bool TryConsumeNextBody(
        ReadOnlySpan<byte> input,
        ref int index,
        out int scalarStart,
        out bool isSecondary)
    {
        scalarStart = index;
        isSecondary = false;
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        var value = input[index];
        if (value < 0x80)
        {
            if (!IsAsciiBody(value))
            {
                return false;
            }

            isSecondary = value == (byte)'.';
            index++;
            return true;
        }

        if (Rune.DecodeFromUtf8(input[index..], out var scalar, out var consumed) != OperationStatus.Done ||
            !DotNetUtf8WordChar.IsWord(scalar))
        {
            return false;
        }

        index += consumed;
        return true;
    }

    private static bool IsAsciiHead(byte value)
        => Utf8AsciiBytePredicates.IsWord(value) || value is (byte)'.' or (byte)'+' or (byte)'-';

    private static bool IsAsciiBody(byte value)
        => Utf8AsciiBytePredicates.IsWord(value) || value is (byte)'.' or (byte)'-';

    private static bool IsContinuationByte(byte value) => (value & 0xC0) == 0x80;
}
