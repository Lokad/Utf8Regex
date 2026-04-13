namespace Lokad.Utf8Regex;

public readonly struct Utf8ValueMatch
{
    private readonly long _utf16Range;
    private readonly long _byteRange;

    public Utf8ValueMatch(
        bool success,
        bool isByteAligned,
        int indexInUtf16,
        int lengthInUtf16,
        int indexInBytes = 0,
        int lengthInBytes = 0)
    {
        _utf16Range = success ? PackRange(indexInUtf16, lengthInUtf16) : -1;
        _byteRange = success && isByteAligned ? PackRange(indexInBytes, lengthInBytes) : -1;
    }

    public bool Success => _utf16Range >= 0;

    public bool IsByteAligned => _byteRange >= 0;

    public int IndexInUtf16 => Success ? UnpackStart(_utf16Range) : 0;

    public int LengthInUtf16 => Success ? UnpackLength(_utf16Range) : 0;

    public int IndexInBytes => IsByteAligned
        ? UnpackStart(_byteRange)
        : throw new InvalidOperationException("The match is not aligned to valid UTF-8 byte boundaries.");

    public int LengthInBytes => IsByteAligned
        ? UnpackLength(_byteRange)
        : throw new InvalidOperationException("The match is not aligned to valid UTF-8 byte boundaries.");

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
