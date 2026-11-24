using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Input;

internal readonly record struct Utf8BytePosition(int Value)
{
    internal static Utf8BytePosition CreateChecked(int byteLength, int value, string parameterName)
    {
        if ((uint)value > (uint)byteLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return new Utf8BytePosition(value);
    }

    // Caller-valid text only needs the requested boundary checked.
    internal static Utf8BytePosition CreateScalarChecked(ReadOnlySpan<byte> input, int value, string parameterName)
    {
        var position = CreateChecked(input.Length, value, parameterName);
        if (value != 0 && !IsScalarBoundary(input, value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The requested byte offset is not aligned to a UTF-8 scalar boundary.");
        }

        return position;
    }

    internal static bool IsScalarBoundary(ReadOnlySpan<byte> input, int value)
        => (uint)value <= (uint)input.Length &&
            (value == input.Length || (input[value] & 0xc0) != 0x80);

    internal bool IsScalarBoundary(ReadOnlySpan<byte> input)
        => Value == 0 || IsScalarBoundary(input, Value);

    // PCRE2-INTEGRATION-POINT: advance a caller-admitted byte position after an empty match.
    internal bool TryAdvanceScalar(ReadOnlySpan<byte> input, out Utf8BytePosition next)
    {
        if (Value >= input.Length)
        {
            next = default;
            return false;
        }

        var status = Rune.DecodeFromUtf8(input[Value..], out _, out var consumed);
        if (status != OperationStatus.Done)
            throw new InvalidOperationException("Caller-valid UTF-8 subject contained an invalid scalar boundary.");

        next = new Utf8BytePosition(Value + consumed);
        return true;
    }
}

