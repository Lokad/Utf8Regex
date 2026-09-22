using Lokad.Utf8Regex.Internal.Input;
namespace Lokad.Utf8Regex;

/// <summary>Provides a stack-only match view with UTF-16 and original UTF-8 projections.</summary>
/// <remarks>The view and any spans obtained from it must not outlive the input supplied to the matching operation.</remarks>
public readonly ref struct Utf8MatchContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly string _decoded;
    private readonly Match? _match;
    private readonly Utf8BoundaryMap _boundaryMap;
    private readonly string[]? _groupNames;
    private readonly bool _ownedHasValue;
    private readonly int _ownedIndex;
    private readonly int _ownedLength;

    internal Utf8MatchContext(ReadOnlySpan<byte> input, string decoded, Match? match, Utf8BoundaryMap boundaryMap, string[]? groupNames)
    {
        _input = input;
        _decoded = decoded;
        _match = match;
        _boundaryMap = boundaryMap;
        _groupNames = groupNames;
    }

    internal Utf8MatchContext(ReadOnlySpan<byte> input, string decoded, Utf8BoundaryMap boundaryMap, string[]? groupNames, int indexInUtf16, int lengthInUtf16)
    {
        _input = input;
        _decoded = decoded;
        _boundaryMap = boundaryMap;
        _groupNames = groupNames;
        _ownedHasValue = true;
        _ownedIndex = indexInUtf16;
        _ownedLength = lengthInUtf16;
    }

    /// <summary>Gets whether the regex found a match.</summary>
    public bool Success => _match?.Success ?? _ownedHasValue;

    /// <summary>Gets the successful match start as a zero-based UTF-16 code-unit offset, or zero otherwise.</summary>
    public int IndexInUtf16 => _match is { Success: true } match ? match.Index : (_ownedHasValue ? _ownedIndex : 0);

    /// <summary>Gets the successful match length in UTF-16 code units, or zero otherwise.</summary>
    public int LengthInUtf16 => _match is { Success: true } match ? match.Length : (_ownedHasValue ? _ownedLength : 0);

    /// <summary>Gets whether both match boundaries map to UTF-8 scalar boundaries.</summary>
    public bool IsByteAligned
    {
        get
        {
            if (_match is not { Success: true } match)
            {
                if (_ownedHasValue)
                {
                    var ownedStart = ResolveBoundary(_ownedIndex);
                    var ownedEnd = ResolveBoundary(_ownedIndex + _ownedLength);
                    return ownedStart.IsScalarBoundary && ownedEnd.IsScalarBoundary;
                }

                return true;
            }

            var start = ResolveBoundary(match.Index);
            var end = ResolveBoundary(match.Index + match.Length);
            return start.IsScalarBoundary && end.IsScalarBoundary;
        }
    }

    /// <summary>Gets the match start as a zero-based byte offset in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The match splits a UTF-8 scalar.</exception>
    public int IndexInBytes => IsByteAligned
        ? ResolveBoundary(IndexInUtf16).ByteOffset
        : throw new InvalidOperationException("The match is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the match length in bytes in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The match splits a UTF-8 scalar.</exception>
    public int LengthInBytes => IsByteAligned
        ? ResolveBoundary(IndexInUtf16 + LengthInUtf16).ByteOffset - IndexInBytes
        : throw new InvalidOperationException("The match is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Attempts to map the match range to the original UTF-8 input.</summary>
    /// <param name="indexInBytes">The zero-based byte offset, or zero when the range is not byte-aligned.</param>
    /// <param name="lengthInBytes">The byte length, or zero when the range is not byte-aligned.</param>
    /// <returns><see langword="true"/> when both match boundaries are UTF-8 scalar boundaries.</returns>
    public bool TryGetByteRange(out int indexInBytes, out int lengthInBytes)
    {
        if (!IsByteAligned)
        {
            indexInBytes = 0;
            lengthInBytes = 0;
            return false;
        }

        indexInBytes = ResolveBoundary(IndexInUtf16).ByteOffset;
        lengthInBytes = ResolveBoundary(IndexInUtf16 + LengthInUtf16).ByteOffset - indexInBytes;
        return true;
    }

    /// <summary>Gets the number of numbered groups, including group zero, or zero when no match is available.</summary>
    public int GroupCount => _match?.Groups.Count ?? (_ownedHasValue ? 1 : 0);

    /// <summary>Gets a numbered group from the match.</summary>
    /// <exception cref="InvalidOperationException">No match is available.</exception>
    /// <remarks>An undefined group number produces an unsuccessful group view, matching .NET group-collection semantics.</remarks>
    public Utf8GroupContext GetGroup(int number)
    {
        if (_match is null)
        {
            if (!_ownedHasValue)
            {
                throw new InvalidOperationException("No match is available.");
            }

            return number == 0 ? new Utf8GroupContext(_input, _decoded, _boundaryMap, _ownedIndex, _ownedLength) : default;
        }

        return new Utf8GroupContext(_input, _decoded, _match.Groups[number], _boundaryMap);
    }

    /// <summary>Gets a named group from the match.</summary>
    /// <exception cref="InvalidOperationException">No match is available.</exception>
    /// <remarks>An undefined group name produces an unsuccessful group view, matching .NET group-collection semantics.</remarks>
    public Utf8GroupContext GetGroup(string name)
    {
        if (_match is null)
        {
            if (!_ownedHasValue)
            {
                throw new InvalidOperationException("No match is available.");
            }

            return name == "0" ? new Utf8GroupContext(_input, _decoded, _boundaryMap, _ownedIndex, _ownedLength) : default;
        }

        return new Utf8GroupContext(_input, _decoded, _match.Groups[name], _boundaryMap);
    }

    /// <summary>Attempts to get a numbered group from the match.</summary>
    /// <param name="number">The group number, where zero denotes the entire match.</param>
    /// <param name="group">The group view, or its default value when the number is unavailable.</param>
    /// <returns><see langword="true"/> when the numbered group exists.</returns>
    public bool TryGetGroup(int number, out Utf8GroupContext group)
    {
        if (_match is null)
        {
            if (_ownedHasValue && number == 0)
            {
                group = new Utf8GroupContext(_input, _decoded, _boundaryMap, _ownedIndex, _ownedLength);
                return true;
            }

            group = default;
            return false;
        }

        if (number < 0 || number >= _match.Groups.Count)
        {
            group = default;
            return false;
        }

        group = new Utf8GroupContext(_input, _decoded, _match.Groups[number], _boundaryMap);
        return true;
    }

    /// <summary>Attempts to get a named group from the match.</summary>
    /// <param name="name">The group name defined by the regex.</param>
    /// <param name="group">The group view, or its default value when the name is unavailable.</param>
    /// <returns><see langword="true"/> when the named group exists.</returns>
    public bool TryGetGroup(string name, out Utf8GroupContext group)
    {
        if (_match is null)
        {
            if (_ownedHasValue && name == "0")
            {
                group = new Utf8GroupContext(_input, _decoded, _boundaryMap, _ownedIndex, _ownedLength);
                return true;
            }

            group = default;
            return false;
        }

        if (_groupNames is null || Array.IndexOf(_groupNames, name) < 0)
        {
            group = default;
            return false;
        }

        group = new Utf8GroupContext(_input, _decoded, _match.Groups[name], _boundaryMap);
        return true;
    }

    /// <summary>Gets the match value as a UTF-16 string, or an empty string after an unsuccessful match.</summary>
    public string GetValueString()
    {
        if (_match is { Success: true } match)
        {
            return match.Value;
        }

        return _ownedHasValue ? _decoded.Substring(_ownedIndex, _ownedLength) : string.Empty;
    }

    /// <summary>Attempts to get the match value from the original UTF-8 input.</summary>
    /// <param name="valueBytes">The match bytes, or a default span when the range is not byte-aligned.</param>
    /// <returns><see langword="true"/> when the match can be projected to bytes.</returns>
    public bool TryGetValueBytes(out ReadOnlySpan<byte> valueBytes)
    {
        if (!TryGetByteRange(out var indexInBytes, out var lengthInBytes))
        {
            valueBytes = default;
            return false;
        }

        valueBytes = _input.Slice(indexInBytes, lengthInBytes);
        return true;
    }

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        return _boundaryMap.Resolve(utf16Offset);
    }
}
