using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8UriTokenCountExecutor
{
    public static int CountWellFormed(ReadOnlySpan<byte> input)
    {
        var invocation = new Utf8SliceInvocation(default, Timeout.InfiniteTimeSpan, default);
        return CountWellFormed(input, ref invocation);
    }

    internal static int CountWellFormed(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        Checkpoint(ref invocation);
        var count = 0;
        var searchFrom = 0;
        while ((uint)searchFrom < (uint)input.Length)
        {
            var delimiterIndex = -1;
            if (invocation.IsUnmetered)
            {
                var relative = input[searchFrom..].IndexOf("://"u8);
                if (relative >= 0) delimiterIndex = searchFrom + relative;
            }
            else
            {
                // Two lookahead bytes retain every delimiter crossing a window.
                // Keep searchFrom as the scheme floor, independent of scanFrom.
                for (var scanFrom = searchFrom; scanFrom < input.Length;)
                {
                    Checkpoint(ref invocation);
                    var length = Math.Min(2048, input.Length - scanFrom);
                    var relative = input.Slice(scanFrom, Math.Min(length + 2, input.Length - scanFrom)).IndexOf("://"u8);
                    if (relative >= 0) { delimiterIndex = scanFrom + relative; break; }
                    scanFrom += length;
                }
            }
            if (delimiterIndex < 0) break;
            if (!TryFindSchemeStart(input, searchFrom, delimiterIndex, ref invocation, out _) ||
                !TryConsumeBody(input, delimiterIndex + 3, ref invocation, out var tokenEnd))
            {
                searchFrom = delimiterIndex + 3;
                continue;
            }

            count++;
            searchFrom = tokenEnd;
        }

        return count;
    }

    private static void Checkpoint(ref Utf8SliceInvocation invocation)
    {
        if (invocation.IsUnmetered) return;
        if (invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("URI count breached the invocation budget.");
    }

    public static Utf8SelectedCountKernelMetrics InspectMetrics(ReadOnlySpan<byte> input)
    {
        var invocation = new Utf8SliceInvocation(default, Timeout.InfiniteTimeSpan, default);
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
            if (!TryFindSchemeStart(input, searchFrom, delimiterIndex, ref invocation, out _) ||
                !TryConsumeBody(input, delimiterIndex + 3, ref invocation, out var tokenEnd))
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
        ref Utf8SliceInvocation invocation,
        out int schemeStart)
    {
        schemeStart = delimiterIndex;
        while (schemeStart > minStartIndex)
        {
            Checkpoint(ref invocation);
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

    private static bool TryConsumeBody(ReadOnlySpan<byte> input, int bodyStart, ref Utf8SliceInvocation invocation, out int tokenEnd)
    {
        tokenEnd = bodyStart;
        // Host and continuation each require one complete scalar.
        if (!TryConsumeBodyScalar(input, ref tokenEnd, false, ref invocation) ||
            !TryConsumeBodyScalar(input, ref tokenEnd, true, ref invocation)) return false;
        while (TryConsumeBodyScalar(input, ref tokenEnd, true, ref invocation)) { }

        if ((uint)tokenEnd < (uint)input.Length && input[tokenEnd] == (byte)'?')
        {
            tokenEnd++;
            while (TryConsumeNonWhitespaceScalar(input, ref tokenEnd, (byte)'#', ref invocation))
            {
            }
        }

        if ((uint)tokenEnd < (uint)input.Length && input[tokenEnd] == (byte)'#')
        {
            tokenEnd++;
            while (TryConsumeNonWhitespaceScalar(input, ref tokenEnd, 0, ref invocation))
            {
            }
        }

        return true;
    }

    private static bool TryConsumeBodyScalar(
        ReadOnlySpan<byte> input,
        ref int index,
        bool allowSlash,
        ref Utf8SliceInvocation invocation)
    {
        Checkpoint(ref invocation);
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
        byte excludedAscii,
        ref Utf8SliceInvocation invocation)
    {
        Checkpoint(ref invocation);
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
