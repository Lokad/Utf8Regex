using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Input;

/// <summary>
/// Operation-local authority that a complete subject is well-formed UTF-8.
/// Projection state and decoded text are materialized only when requested.
/// </summary>
internal ref struct Utf8ValidatedInput
{
    private readonly ReadOnlySpan<byte> _bytes;
    private readonly Utf8ValidationResult _validation;
    private Utf8BoundaryMap? _boundaryMap;
    private string? _decoded;

    private Utf8ValidatedInput(ReadOnlySpan<byte> bytes, Utf8ValidationResult validation)
    {
        _bytes = bytes;
        _validation = validation;
        _boundaryMap = null;
        _decoded = null;
    }

    public ReadOnlySpan<byte> Bytes => _bytes;

    public Utf8ValidationResult Validation => _validation;

    public int ByteLength => _validation.ByteLength;

    public int Utf16Length => _validation.Utf16Length;

    public bool IsAscii => _validation.IsAscii;

    public Utf8BoundaryMap BoundaryMap => GetRandomAccessMap();

    // ASCII byte and UTF-16 coordinates are identical, so consumers can use
    // a null map as the direct-projection representation.
    public Utf8BoundaryMap? Utf16ProjectionMap => IsAscii ? null : GetRandomAccessMap();

    public static Utf8ValidatedInput Create(ReadOnlySpan<byte> input)
        => new(input, Utf8InputAnalyzer.ValidateOnly(input));

    public Utf8BytePosition GetBytePosition(int value, string parameterName)
    {
        var position = Utf8BytePosition.CreateChecked(_validation, value, parameterName);
        if (!IsScalarBoundary(position))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The requested byte offset is not aligned to a UTF-8 scalar boundary.");
        }

        return position;
    }

    public Utf16Position GetUtf16Position(int value, string parameterName)
        => Utf16Position.CreateChecked(_validation, value, parameterName);

    public Utf8ByteRange GetByteRange(int start, int length, string startParameterName, string lengthParameterName)
    {
        var range = Utf8ByteRange.CreateChecked(
            _validation,
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
        _boundaryMap ??= Utf8BoundaryMap.Create(_bytes, _validation);
        return _boundaryMap.Value;
    }

    public string GetDecodedString()
    {
        _decoded ??= Encoding.UTF8.GetString(_bytes);
        return _decoded;
    }

    public Utf8ProjectionCursor CreateProjectionCursor() => new(_bytes, _validation);

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
            _validation.ContainsSupplementaryScalars);
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
    {
        _input = input;
        _isAscii = validation.IsAscii;
        _byteOffset = 0;
        _utf16Offset = 0;
    }

    internal int BytesConsumed => _byteOffset;

    public Utf16Position Project(Utf8BytePosition position)
    {
        if (position.Value < _byteOffset || position.Value > _input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Projection offsets must be in increasing order.");
        }

        if (_isAscii)
        {
            _byteOffset = position.Value;
            _utf16Offset = position.Value;
            return new Utf16Position(_utf16Offset);
        }

        while (_byteOffset < position.Value)
        {
            var status = Rune.DecodeFromUtf8(_input[_byteOffset..position.Value], out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "The requested byte offset splits a UTF-8 scalar.");
            }

            _byteOffset += consumed;
            _utf16Offset += rune.Utf16SequenceLength;
        }

        return new Utf16Position(_utf16Offset);
    }
}
