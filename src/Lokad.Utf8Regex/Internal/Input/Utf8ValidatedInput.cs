using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

/// <summary>Borrowed caller-valid UTF-8 and checked byte positions.</summary>
internal readonly ref struct Utf8ValidatedInput
{
    private readonly ReadOnlySpan<byte> _bytes;

    private Utf8ValidatedInput(ReadOnlySpan<byte> bytes) => _bytes = bytes;

    // PCRE2-INTEGRATION-POINT: the adapter requires caller-valid text.
    internal static Utf8ValidatedInput BorrowBytes(ReadOnlySpan<byte> bytes) => new(bytes);

    public ReadOnlySpan<byte> Bytes => _bytes;

    public int ByteLength => _bytes.Length;


    public Utf8BytePosition GetBytePosition(int value, string parameterName)
        => GetBytePosition(_bytes, value, parameterName);

    // The subject is caller-valid. Only the requested boundary is inspected.
    internal static Utf8BytePosition GetBytePosition(ReadOnlySpan<byte> input, int value, string parameterName)
    {
        var position = Utf8BytePosition.CreateChecked(input.Length, value, parameterName);
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

    public bool IsScalarBoundary(Utf8BytePosition position)
        => position.Value == 0 || IsScalarBoundary(_bytes, position.Value);

    public bool TryAdvanceScalar(Utf8BytePosition position, out Utf8BytePosition next)
    {
        if (position.Value >= _bytes.Length)
        {
            next = default;
            return false;
        }

        var status = Rune.DecodeFromUtf8(_bytes[position.Value..], out _, out var consumed);
        if (status != OperationStatus.Done)
        {
            throw new InvalidOperationException("Caller-valid UTF-8 subject contained an invalid scalar boundary.");
        }

        next = new Utf8BytePosition(position.Value + consumed);
        return true;
    }

}

