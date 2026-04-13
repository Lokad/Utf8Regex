namespace Lokad.Utf8Regex.Internal.Input;

internal readonly struct Utf8ValidationResult
{
    public Utf8ValidationResult(int byteLength, int utf16Length, bool isAscii, bool containsSupplementaryScalars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteLength);
        ArgumentOutOfRangeException.ThrowIfNegative(utf16Length);

        ByteLength = byteLength;
        Utf16Length = utf16Length;
        IsAscii = isAscii;
        ContainsSupplementaryScalars = containsSupplementaryScalars;
    }

    public int ByteLength { get; }

    public int Utf16Length { get; }

    public bool IsAscii { get; }

    public bool ContainsSupplementaryScalars { get; }

    public Utf16Boundary EndBoundary => Utf16Boundary.ScalarBoundary(ByteLength, Utf16Length);
}
