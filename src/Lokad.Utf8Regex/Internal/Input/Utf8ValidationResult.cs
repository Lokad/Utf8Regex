namespace Lokad.Utf8Regex.Internal.Input;

internal readonly struct Utf8ValidationResult
{
    public Utf8ValidationResult(int byteLength, bool isAscii)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteLength);

        ByteLength = byteLength;
        IsAscii = isAscii;
    }

    public int ByteLength { get; }

    public bool IsAscii { get; }


}
