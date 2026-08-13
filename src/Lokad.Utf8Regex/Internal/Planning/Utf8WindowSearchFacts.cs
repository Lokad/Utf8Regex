namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8PreparedSearcherInfoKind : byte
{
    None = 0,
    LiteralFamily = 1,
    QuotedAsciiRun = 2,
}

internal readonly struct Utf8PreparedSearcherInfo
{
    private Utf8PreparedSearcherInfo(
        Utf8PreparedSearcherInfoKind kind,
        byte[][]? alternateLiteralsUtf8,
        string? quotedAsciiSet,
        int quotedAsciiLength)
    {
        Kind = kind;
        AlternateLiteralsUtf8 = alternateLiteralsUtf8;
        QuotedAsciiSet = quotedAsciiSet;
        QuotedAsciiLength = quotedAsciiLength;
    }

    public Utf8PreparedSearcherInfoKind Kind { get; }

    public byte[][]? AlternateLiteralsUtf8 { get; }

    public string? QuotedAsciiSet { get; }

    public int QuotedAsciiLength { get; }

    public bool HasValue => Kind != Utf8PreparedSearcherInfoKind.None;

    public static Utf8PreparedSearcherInfo LiteralFamily(byte[][] alternateLiteralsUtf8) =>
        new(Utf8PreparedSearcherInfoKind.LiteralFamily, alternateLiteralsUtf8, null, 0);

    public static Utf8PreparedSearcherInfo QuotedAsciiRun(string quotedAsciiSet, int quotedAsciiLength) =>
        new(Utf8PreparedSearcherInfoKind.QuotedAsciiRun, null, quotedAsciiSet, quotedAsciiLength);
}

internal readonly struct Utf8WindowSearchFacts
{
    private Utf8WindowSearchFacts(
        Utf8PreparedSearcherInfo leading,
        Utf8PreparedSearcherInfo trailing,
        int? maxGap,
        int? maxLines)
    {
        Leading = leading;
        Trailing = trailing;
        MaxGap = maxGap;
        MaxLines = maxLines;
    }

    public Utf8PreparedSearcherInfo Leading { get; }

    public Utf8PreparedSearcherInfo Trailing { get; }

    public int? MaxGap { get; }

    public int? MaxLines { get; }

    public bool HasValue => Leading.HasValue && Trailing.HasValue;

    public static Utf8WindowSearchFacts WithinLines(
        Utf8PreparedSearcherInfo leading,
        Utf8PreparedSearcherInfo trailing,
        int maxLines)
        => new(leading, trailing, null, maxLines);
}
