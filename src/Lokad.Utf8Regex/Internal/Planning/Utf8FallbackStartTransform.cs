namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8FallbackStartTransformKind : byte
{
    None = 0,
    TrimLeadingAsciiWhitespace = 1,
}

internal readonly struct Utf8FallbackStartTransform
{
    public Utf8FallbackStartTransform(int offset, Utf8FallbackStartTransformKind kind = Utf8FallbackStartTransformKind.None)
    {
        Offset = offset;
        Kind = kind;
    }

    public int Offset { get; }

    public Utf8FallbackStartTransformKind Kind { get; }

    public bool HasValue => Offset != 0 || Kind != Utf8FallbackStartTransformKind.None;

    public Utf8FallbackStartTransform WithAdditionalOffset(int offset)
    {
        if (offset == 0)
        {
            return this;
        }

        return new Utf8FallbackStartTransform(Offset + offset, Kind);
    }

    public int Apply(ReadOnlySpan<byte> input, int matchIndex)
    {
        var start = matchIndex - Offset;
        if ((uint)start > (uint)input.Length)
        {
            return -1;
        }

        if (Kind == Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace)
        {
            while (start > 0 && IsAsciiWhitespace(input[start - 1]))
            {
                start--;
            }
        }

        return start;
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C;
    }
}
