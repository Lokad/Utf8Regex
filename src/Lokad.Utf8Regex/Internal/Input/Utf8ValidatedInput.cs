using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

/// <summary>
/// Operation-local caller-valid subject bytes and optional eligibility facts. The engine
/// relies on the caller to guarantee validity for the complete subject.
/// </summary>
internal ref struct Utf8ValidatedInput
{
    private readonly ReadOnlySpan<byte> _bytes;
    private Utf8ValidationResult? _validation;

    private Utf8ValidatedInput(ReadOnlySpan<byte> bytes, Utf8ValidationResult? validation)
    {
        _bytes = bytes;
        _validation = validation;
    }

    // PCRE2-INTEGRATION-POINT: the adapter requires caller-valid text.
    // Byte execution needs no whole
    // subject description; genuine metadata consumers request it separately.
    internal static Utf8ValidatedInput BorrowBytes(ReadOnlySpan<byte> bytes, bool isAscii = false)
        => new(bytes, isAscii ? new Utf8ValidationResult(bytes.Length, true, false) : null);

    public ReadOnlySpan<byte> Bytes => _bytes;

    public Utf8ValidationResult Validation => _validation ??
        throw new InvalidOperationException("This byte operation has not requested a subject description.");

    public int ByteLength => _bytes.Length;


    public bool IsAscii => _validation?.IsAscii == true;


    public static Utf8ValidatedInput Create(ReadOnlySpan<byte> input)
        => new(input, Utf8InputAnalyzer.Describe(input));

    public Utf8BytePosition GetBytePosition(int value, string parameterName)
        => GetBytePosition(_bytes, value, parameterName);

    // Scalar-floor admission needs only bytes, including Boolean operations
    // whose established input contract requires no UTF-16 metadata.
    internal static Utf8BytePosition GetBytePosition(ReadOnlySpan<byte> input, int value, string parameterName)
    {
        var position = Utf8BytePosition.CreateChecked(input.Length, value, parameterName);
        if (value != input.Length && value != 0 && (input[value] & 0xc0) == 0x80)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The requested byte offset is not aligned to a UTF-8 scalar boundary.");
        }

        return position;
    }

    public bool IsScalarBoundary(Utf8BytePosition position)
    {
        var value = position.Value;
        return value == _bytes.Length || value == 0 || (_bytes[value] & 0xC0) != 0x80;
    }

    public Utf8ValidationResult GetSuffixValidation(Utf8BytePosition start) =>
        Utf8InputAnalyzer.Describe(_bytes[start.Value..]);

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

