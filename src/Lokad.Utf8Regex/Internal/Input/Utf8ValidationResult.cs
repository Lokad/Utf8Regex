namespace Lokad.Utf8Regex.Internal.Input;

internal readonly struct Utf8ValidationResult
{
    public Utf8ValidationResult(int byteLength, bool isAscii, bool containsSupplementaryScalars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteLength);

        ByteLength = byteLength;
        IsAscii = isAscii;
        ContainsSupplementaryScalars = containsSupplementaryScalars;
    }

    public int ByteLength { get; }

    public bool IsAscii { get; }

    public bool ContainsSupplementaryScalars { get; }

}
