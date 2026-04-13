namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8FixedTemplateReplacement
{
    public Utf8FixedTemplateReplacement(int matchLength, int replacementLength, Utf8FixedTemplateReplacementSegment[] segments)
    {
        MatchLength = matchLength;
        ReplacementLength = replacementLength;
        Segments = segments;
    }

    public int MatchLength { get; }

    public int ReplacementLength { get; }

    public Utf8FixedTemplateReplacementSegment[] Segments { get; }
}

internal readonly struct Utf8FixedTemplateReplacementSegment
{
    public Utf8FixedTemplateReplacementSegment(byte[] literalUtf8)
    {
        Kind = Utf8FixedTemplateReplacementSegmentKind.Literal;
        LiteralUtf8 = literalUtf8;
        MatchOffset = 0;
        Length = literalUtf8.Length;
    }

    public Utf8FixedTemplateReplacementSegment(int matchOffset, int length)
    {
        Kind = Utf8FixedTemplateReplacementSegmentKind.MatchSlice;
        LiteralUtf8 = null;
        MatchOffset = matchOffset;
        Length = length;
    }

    public Utf8FixedTemplateReplacementSegmentKind Kind { get; }

    public byte[]? LiteralUtf8 { get; }

    public int MatchOffset { get; }

    public int Length { get; }
}

internal enum Utf8FixedTemplateReplacementSegmentKind
{
    Literal,
    MatchSlice,
}
