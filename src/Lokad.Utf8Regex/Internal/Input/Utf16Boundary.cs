namespace Lokad.Utf8Regex.Internal.Input;

internal readonly struct Utf16Boundary
{
    public Utf16Boundary(int byteOffset, int utf16Offset, byte intraScalarCodeUnitOffset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(utf16Offset);

        if (intraScalarCodeUnitOffset > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intraScalarCodeUnitOffset),
                intraScalarCodeUnitOffset,
                "The intra-scalar code unit offset must be 0 or 1.");
        }

        ByteOffset = byteOffset;
        Utf16Offset = utf16Offset;
        IntraScalarCodeUnitOffset = intraScalarCodeUnitOffset;
    }

    public int ByteOffset { get; }

    public int Utf16Offset { get; }

    public byte IntraScalarCodeUnitOffset { get; }

    public bool IsScalarBoundary => IntraScalarCodeUnitOffset == 0;

    public static Utf16Boundary ScalarBoundary(int byteOffset, int utf16Offset)
        => new(byteOffset, utf16Offset);

    public static Utf16Boundary SurrogateSplitBoundary(int byteOffset, int utf16Offset)
        => new(byteOffset, utf16Offset, 1);
}
