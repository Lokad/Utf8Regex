using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8WordDelimitedTokenExecutor
{
    public static int CountWellFormed(ReadOnlySpan<byte> input)
    {
        var invocation = new Utf8SliceInvocation(default, Timeout.InfiniteTimeSpan, default);
        var breach = TryCountWellFormed(input, ref invocation, out var count);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Word-delimited count breached the invocation budget.");
        return count;
    }

    internal static Utf8ExecutionLimitKind TryCountWellFormed(ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out int count)
    {
        count = 0;
        var searchFrom = 0;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        while ((uint)searchFrom < (uint)input.Length)
        {
            var delimiterIndex = -1;
            if (invocation.IsUnmetered)
            {
                var relative = input[searchFrom..].IndexOf((byte)'@');
                if (relative >= 0) delimiterIndex = searchFrom + relative;
            }
            else
            {
                // Bound each search without advancing the semantic head floor:
                // a token can begin before the window containing its delimiter.
                for (var scanFrom = searchFrom; scanFrom < input.Length;)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var length = Math.Min(2048, input.Length - scanFrom);
                    var relative = input.Slice(scanFrom, length).IndexOf((byte)'@');
                    if (relative >= 0) { delimiterIndex = scanFrom + relative; break; }
                    scanFrom += length;
                }
            }
            if (delimiterIndex < 0) break;

            var tokenStart = delimiterIndex;
            while (tokenStart > searchFrom)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!TryConsumePreviousHead(input, ref tokenStart)) break;
            }
            if (tokenStart == delimiterIndex)
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var tokenEnd = delimiterIndex + 1;
            var lastSecondaryIndex = -1;
            var hasSecondaryWithTail = false;
            while (true)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!TryConsumeNextBody(input, ref tokenEnd, out var scalarStart, out var isSecondary)) break;
                if (lastSecondaryIndex > delimiterIndex + 1) hasSecondaryWithTail = true;
                if (isSecondary) lastSecondaryIndex = scalarStart;
            }
            // A secondary with both neighbours already proves a nonempty body.
            if (hasSecondaryWithTail) { count++; searchFrom = tokenEnd; }
            else searchFrom = delimiterIndex + 1;
        }
        return Utf8ExecutionLimitKind.None;
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
            !Utf8ScalarWordChar.IsWord(scalar))
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
            !Utf8ScalarWordChar.IsWord(scalar))
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
