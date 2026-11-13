using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Input;

/// <summary>
/// Operation-local subject bytes and optional coordinate facts. The engine
/// establishes validity or a caller guarantee for the relevant input range.
/// Derived coordinate projection state is materialized only when requested.
/// </summary>
internal ref struct Utf8ValidatedInput
{
    private readonly ReadOnlySpan<byte> _bytes;
    private Utf8ValidationResult? _validation;
    private Utf8BoundaryMap? _boundaryMap;

    private Utf8ValidatedInput(ReadOnlySpan<byte> bytes, Utf8ValidationResult? validation)
    {
        _bytes = bytes;
        _validation = validation;
        _boundaryMap = null;
    }

    // PCRE2-INTEGRATION-POINT: the adapter has checked its native UTF window,
    // or has an explicit trusted-input contract. Byte execution needs no whole
    // subject description; genuine metadata consumers request it separately.
    internal static Utf8ValidatedInput BorrowBytes(ReadOnlySpan<byte> bytes, bool isAscii = false)
        => new(bytes, isAscii ? new Utf8ValidationResult(bytes.Length, bytes.Length, true, false) : null);

    internal void Describe(ref Utf8SliceInvocation invocation)
    {
        if (_validation.HasValue) return;
        var breach = Utf8Validation.TryDescribeValid(_bytes, ref invocation, out var validation);
        if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
        _validation = validation;
    }

    public ReadOnlySpan<byte> Bytes => _bytes;

    public Utf8ValidationResult Validation => _validation ??
        throw new InvalidOperationException("This byte operation has not requested a subject description.");

    public int ByteLength => _bytes.Length;

    public int Utf16Length => Validation.Utf16Length;

    public bool IsAscii => _validation?.IsAscii == true;

    public Utf8BoundaryMap BoundaryMap => GetRandomAccessMap();

    public static Utf8ValidatedInput Create(ReadOnlySpan<byte> input,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
        => new(input, Utf8Validation.Subject(input, inputValidation));

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

    public Utf16Position GetUtf16Position(int value, string parameterName)
    {
        var position = Utf16Position.CreateChecked(Validation, value, parameterName);
        if (!BoundaryMap.Resolve(value).IsScalarBoundary)
        {
            throw new ArgumentOutOfRangeException(parameterName, value,
                "The requested UTF-16 offset is not aligned to a UTF-8 scalar boundary.");
        }

        return position;
    }

    public Utf8ByteRange GetByteRange(int start, int length, string startParameterName, string lengthParameterName)
    {
        var range = Utf8ByteRange.CreateChecked(
            Validation,
            start,
            length,
            startParameterName,
            lengthParameterName);
        if (!IsScalarBoundary(range.Start))
        {
            throw new ArgumentOutOfRangeException(
                startParameterName,
                start,
                "The requested byte offset is not aligned to a UTF-8 scalar boundary.");
        }

        if (!IsScalarBoundary(range.End))
        {
            throw new ArgumentOutOfRangeException(
                lengthParameterName,
                length,
                "The requested byte range ends inside a UTF-8 scalar.");
        }

        return range;
    }

    public bool IsScalarBoundary(Utf8BytePosition position)
    {
        var value = position.Value;
        return value == _bytes.Length || value == 0 || (_bytes[value] & 0xC0) != 0x80;
    }

    public Utf8BoundaryMap GetRandomAccessMap()
    {
        _boundaryMap ??= Utf8BoundaryMap.Create(_bytes, Validation);
        return _boundaryMap.Value;
    }

    public Utf8ProjectionCursor CreateProjectionCursor() => new(_bytes, IsAscii);

    public Utf16Position Project(Utf8BytePosition position)
    {
        var cursor = CreateProjectionCursor();
        return cursor.Project(position);
    }

    public Utf16Range ProjectRange(Utf8ByteRange range)
    {
        var cursor = CreateProjectionCursor();
        var start = cursor.Project(range.Start);
        var end = cursor.Project(range.End);
        return new Utf16Range(start, end);
    }

    public Utf8ValidationResult GetSuffixValidation(Utf8BytePosition start)
    {
        var startInUtf16 = Project(start);
        return new Utf8ValidationResult(
            ByteLength - start.Value,
            Utf16Length - startInUtf16.Value,
            IsAscii,
            Validation.ContainsSupplementaryScalars);
    }

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
            throw new InvalidOperationException("Validated UTF-8 subject contained an invalid scalar boundary.");
        }

        next = new Utf8BytePosition(position.Value + consumed);
        return true;
    }

    public bool TryRetreatScalar(Utf8BytePosition position, out Utf8BytePosition previous)
    {
        if (position.Value == 0)
        {
            previous = default;
            return false;
        }

        var status = Rune.DecodeLastFromUtf8(_bytes[..position.Value], out _, out var consumed);
        if (status != OperationStatus.Done)
        {
            throw new InvalidOperationException("Validated UTF-8 subject contained an invalid scalar boundary.");
        }

        previous = new Utf8BytePosition(position.Value - consumed);
        return true;
    }
}

internal ref struct Utf8ProjectionCursor
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly bool _isAscii;
    private int _byteOffset;
    private int _utf16Offset;

    public Utf8ProjectionCursor(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
        : this(input, validation.IsAscii) { }

    internal Utf8ProjectionCursor(ReadOnlySpan<byte> input, bool isAscii)
    {
        _input = input;
        _isAscii = isAscii;
        _byteOffset = 0;
        _utf16Offset = 0;
    }

    internal int BytesConsumed => _byteOffset;

    public Utf16Position Project(Utf8BytePosition position)
    {
        if (!TryProject(position, out var result))
            throw new ArgumentOutOfRangeException(nameof(position), "The requested byte offset splits a UTF-8 scalar.");
        return result;
    }

    // PCRE2-INTEGRATION-POINT: native matching can ignore an invalid prefix
    // before a nonzero start. Its byte result remains usable; exact UTF-16
    // coordinates are unavailable when that prefix cannot be decoded.
    internal bool TryProject(Utf8BytePosition position, out Utf16Position result)
    {
        result = default;
        if (position.Value < _byteOffset || position.Value > _input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Projection offsets must be in increasing order.");
        }

        if (_isAscii)
        {
            _byteOffset = position.Value;
            _utf16Offset = position.Value;
            result = new Utf16Position(_utf16Offset);
            return true;
        }

        while (_byteOffset < position.Value)
        {
            var status = Rune.DecodeFromUtf8(_input[_byteOffset..position.Value], out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                return false;
            }

            _byteOffset += consumed;
            _utf16Offset += rune.Utf16SequenceLength;
        }

        result = new Utf16Position(_utf16Offset);
        return true;
    }
}
