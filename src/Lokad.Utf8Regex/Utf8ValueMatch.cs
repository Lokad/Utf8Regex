namespace Lokad.Utf8Regex;

/// <summary>Represents match coordinates without retaining the matched UTF-8 input.</summary>
public readonly struct Utf8ValueMatch
{
    private readonly long _utf16Range;
    private readonly long _byteRange;

    /// <summary>Creates a match without a contiguous UTF-8 byte range.</summary>
    public Utf8ValueMatch(
        bool success,
        bool isByteAligned,
        int indexInUtf16,
        int lengthInUtf16)
        : this(success, isByteAligned, indexInUtf16, lengthInUtf16, 0, 0)
    {
    }

    /// <summary>Creates a match with both UTF-16 and optional UTF-8 byte coordinates.</summary>
    /// <param name="success">Whether the value represents a successful match.</param>
    /// <param name="isByteAligned">Whether the match boundaries form a valid UTF-8 byte range.</param>
    /// <param name="indexInUtf16">The zero-based UTF-16 code-unit offset.</param>
    /// <param name="lengthInUtf16">The length in UTF-16 code units.</param>
    /// <param name="indexInBytes">The zero-based offset in the original UTF-8 input.</param>
    /// <param name="lengthInBytes">The length in bytes.</param>
    public Utf8ValueMatch(
        bool success,
        bool isByteAligned,
        int indexInUtf16,
        int lengthInUtf16,
        int indexInBytes,
        int lengthInBytes)
    {
        _utf16Range = success ? PackRange(indexInUtf16, lengthInUtf16) : -1;
        _byteRange = success && isByteAligned ? PackRange(indexInBytes, lengthInBytes) : -1;
    }

    /// <summary>Gets whether this value represents a successful match.</summary>
    public bool Success => _utf16Range >= 0;

    /// <summary>Gets whether a successful match has UTF-8 scalar-aligned byte coordinates.</summary>
    public bool IsByteAligned => _byteRange >= 0;

    /// <summary>Gets the match start as a zero-based UTF-16 code-unit offset, or zero after no match.</summary>
    public int IndexInUtf16 => Success ? UnpackStart(_utf16Range) : 0;

    /// <summary>Gets the match length in UTF-16 code units, or zero after no match.</summary>
    public int LengthInUtf16 => Success ? UnpackLength(_utf16Range) : 0;

    /// <summary>Gets the match start as a zero-based byte offset in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">No UTF-8 byte range is available.</exception>
    public int IndexInBytes => IsByteAligned
        ? UnpackStart(_byteRange)
        : throw new InvalidOperationException("The match is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the match length in bytes in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">No UTF-8 byte range is available.</exception>
    public int LengthInBytes => IsByteAligned
        ? UnpackLength(_byteRange)
        : throw new InvalidOperationException("The match is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Attempts to get the match range in the original UTF-8 input.</summary>
    /// <param name="indexInBytes">The zero-based byte offset, or zero when unavailable.</param>
    /// <param name="lengthInBytes">The byte length, or zero when unavailable.</param>
    /// <returns><see langword="true"/> when the match has UTF-8 scalar-aligned byte coordinates.</returns>
    public bool TryGetByteRange(out int indexInBytes, out int lengthInBytes)
    {
        if (!IsByteAligned)
        {
            indexInBytes = 0;
            lengthInBytes = 0;
            return false;
        }

        indexInBytes = UnpackStart(_byteRange);
        lengthInBytes = UnpackLength(_byteRange);
        return true;
    }

    /// <summary>Gets the canonical unsuccessful match value.</summary>
    public static Utf8ValueMatch NoMatch => new(false, true, 0, 0, 0, 0);

    private static long PackRange(int start, int length)
    {
        return ((long)(uint)start << 32) | (uint)length;
    }

    private static int UnpackStart(long range)
    {
        return (int)(range >> 32);
    }

    private static int UnpackLength(long range)
    {
        return (int)range;
    }
}
