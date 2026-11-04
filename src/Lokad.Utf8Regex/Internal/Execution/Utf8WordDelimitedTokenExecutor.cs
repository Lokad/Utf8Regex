using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8WordDelimitedTokenExecutor
{
    private static readonly PreparedByteSetState s_head = PreparedByteSetState.FromValues(
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz.+-"u8);
    private static readonly PreparedByteSetState s_body = PreparedByteSetState.FromValues(
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz.-"u8);

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
                var length = Math.Min(2048, tokenStart - searchFrom);
                var blockStart = tokenStart - length;
                var relative = s_head.LastIndexOfAnyExcept(input.Slice(blockStart, length));
                tokenStart = relative < 0 ? blockStart : blockStart + relative + 1;
                if (relative < 0) continue;
                // The closest non-ASCII byte is a scalar's final byte. Keep
                // the established Unicode word predicate at that boundary.
                if (!TryConsumePreviousHead(input, ref tokenStart)) break;
            }
            if (tokenStart == delimiterIndex)
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var tokenEnd = delimiterIndex + 1;
            while (tokenEnd < input.Length)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(2048, input.Length - tokenEnd);
                var relative = s_body.IndexOfAnyExcept(input.Slice(tokenEnd, length));
                tokenEnd += relative < 0 ? length : relative;
                if (relative < 0) continue;
                if (!TryConsumeNextBody(input, ref tokenEnd, out _, out _)) break;
            }
            // Dot is ASCII, so it cannot occur inside a validated UTF-8 scalar.
            // Excluding the first/last byte therefore proves nonempty scalar
            // neighbours without maintaining per-character secondary state.
            var hasSecondaryWithTail = false;
            var bodyStart = delimiterIndex + 1;
            var interiorLength = tokenEnd - bodyStart - 2;
            for (var scanned = 0; scanned < interiorLength;)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var length = Math.Min(2048, interiorLength - scanned);
                if (input.Slice(bodyStart + 1 + scanned, length).Contains((byte)'.')) { hasSecondaryWithTail = true; break; }
                scanned += length;
            }
            if (hasSecondaryWithTail) { count++; searchFrom = tokenEnd; }
            else searchFrom = delimiterIndex + 1;
        }
        invocation.Budget.ThrowIfCancellationRequested();
        invocation.Budget.CheckDeadline();
        return Utf8ExecutionLimitKind.None;
    }


    internal static bool TryConsumePreviousHead(ReadOnlySpan<byte> input, ref int index)
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

    internal static bool TryConsumeNextBody(
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
