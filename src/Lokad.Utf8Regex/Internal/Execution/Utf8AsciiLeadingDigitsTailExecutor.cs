namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLeadingDigitsTailExecutor
{
    // The caller validates the complete subject. The grammar proof projects
    // leading and separator classes into ASCII, so a wider prefix must defer to
    // the scalar VM. After a recognized separator, the proved dot tail accepts
    // every scalar except LF, whose byte cannot occur inside valid UTF-8.
    internal static Utf8ExecutionLimitKind TryIsMatchWellFormedWhole(ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> separatorBytes, ref Utf8SliceInvocation invocation,
        out bool handled, out bool isMatch)
    {
        handled = isMatch = false;
        var end = input.Length;
        if (end != 0 && input[end - 1] == (byte)'\n') end--;
        var position = 0;
        var matched = false;
        while (position < end)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var length = Math.Min(2048, end - position);
            var firstOther = input.Slice(position, length).IndexOfAnyExceptInRange((byte)'0', (byte)'9');
            position += firstOther < 0 ? length : firstOther;
            if (firstOther >= 0) break;
        }
        if (position < end && input[position] >= 128)
        {
            var breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            return breach;
        }
        if (position > 0)
        {
            matched = position == end;
            if (!matched && separatorBytes.IndexOf(input[position]) >= 0)
            {
                matched = true;
                position++;
                while (position < end)
                {
                    var breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var length = Math.Min(2048, end - position);
                    if (input.Slice(position, length).IndexOf((byte)'\n') >= 0)
                    {
                        matched = false;
                        break;
                    }
                    position += length;
                }
            }
        }
        var finalBreach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (finalBreach == Utf8ExecutionLimitKind.None)
        {
            handled = true;
            isMatch = matched;
        }
        return finalBreach;
    }

    public static bool TryMatchPrintableAsciiWhole(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> separatorBytes,
        out bool isMatch)
    {
        isMatch = false;
        var index = 0;
        while ((uint)index < (uint)input.Length && IsAsciiDigit(input[index]))
        {
            index++;
        }

        if (index == 0)
        {
            return input.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7F) < 0;
        }

        if (index < input.Length)
        {
            if (separatorBytes.IsEmpty || separatorBytes.IndexOf(input[index]) < 0)
            {
                return input.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7F) < 0;
            }

            index++;
            if (input[index..].IndexOfAnyExceptInRange((byte)0x20, (byte)0x7F) >= 0)
            {
                return false;
            }
        }

        isMatch = true;
        return true;
    }

    public static bool TryMatchWhole(ReadOnlySpan<byte> input, ReadOnlySpan<byte> separatorBytes, out int matchedLength)
    {
        if (input.IsEmpty)
        {
            matchedLength = 0;
            return false;
        }

        var effectiveLength = input.Length;
        if (effectiveLength > 0 && input[effectiveLength - 1] == (byte)'\n')
        {
            effectiveLength--;
        }

        return TryMatchWholeCore(input, separatorBytes, effectiveLength, out matchedLength);
    }

    private static bool TryMatchWholeCore(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> separatorBytes,
        int effectiveLength,
        out int matchedLength)
    {
        matchedLength = 0;
        if (effectiveLength <= 0)
        {
            return false;
        }

        var index = 0;
        while (index < effectiveLength && IsAsciiDigit(input[index]))
        {
            index++;
        }

        if (index == 0)
        {
            return false;
        }

        if (index < effectiveLength)
        {
            if (separatorBytes.IsEmpty)
            {
                return false;
            }

            if (separatorBytes.IndexOf(input[index]) < 0)
            {
                return false;
            }

            index++;
            if (index < effectiveLength &&
                input[index..effectiveLength].IndexOf((byte)'\n') >= 0)
            {
                return false;
            }
        }

        matchedLength = effectiveLength;
        return true;
    }

    private static bool IsAsciiDigit(byte value) => Utf8AsciiBytePredicates.IsDigit(value);
}
