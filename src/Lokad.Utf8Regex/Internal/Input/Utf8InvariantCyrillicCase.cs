namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8InvariantCyrillicCase
{
    public static bool TryGetPair(char value, out char upper, out char lower)
    {
        if (value is >= '\u0410' and <= '\u042F')
        {
            upper = value;
            lower = (char)(value + 0x20);
            return true;
        }

        if (value is >= '\u0430' and <= '\u044F')
        {
            upper = (char)(value - 0x20);
            lower = value;
            return true;
        }

        if (value is '\u0401' or '\u0451')
        {
            upper = '\u0401';
            lower = '\u0451';
            return true;
        }

        upper = default;
        lower = default;
        return false;
    }

    public static ushort EncodeTwoByteScalar(char value)
    {
        var first = (byte)(0xC0 | value >> 6);
        var second = (byte)(0x80 | value & 0x3F);
        return (ushort)(first | second << 8);
    }

    public static bool TryGetCounterpartByte(ReadOnlySpan<byte> literal, int offset, out byte counterpart)
    {
        var scalarOffset = offset;
        var isSecondByte = false;
        if ((literal[offset] & 0xC0) == 0x80)
        {
            scalarOffset--;
            isSecondByte = true;
        }

        if ((uint)scalarOffset >= (uint)(literal.Length - 1) ||
            literal[scalarOffset] is < 0xC2 or >= 0xE0 ||
            (literal[scalarOffset + 1] & 0xC0) != 0x80)
        {
            counterpart = default;
            return false;
        }

        var value = (char)(((literal[scalarOffset] & 0x1F) << 6) | (literal[scalarOffset + 1] & 0x3F));
        if (!TryGetPair(value, out var upper, out var lower))
        {
            counterpart = default;
            return false;
        }

        var other = value == upper ? lower : upper;
        var encoded = EncodeTwoByteScalar(other);
        counterpart = isSecondByte ? (byte)(encoded >> 8) : (byte)encoded;
        return true;
    }
}
