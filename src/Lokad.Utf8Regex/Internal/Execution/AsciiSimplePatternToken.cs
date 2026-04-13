namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiSimplePatternToken
{
    public AsciiSimplePatternToken(byte literal)
    {
        Kind = AsciiSimplePatternTokenKind.Literal;
        Literal = literal;
        CharClass = null;
    }

    public AsciiSimplePatternToken(AsciiCharClass charClass)
    {
        Kind = AsciiSimplePatternTokenKind.CharClass;
        Literal = 0;
        CharClass = charClass;
    }

    private AsciiSimplePatternToken(AsciiSimplePatternTokenKind kind)
    {
        Kind = kind;
        Literal = 0;
        CharClass = null;
    }

    public AsciiSimplePatternTokenKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass? CharClass { get; }

    public static AsciiSimplePatternToken Dot { get; } = new(AsciiSimplePatternTokenKind.Dot);
}

internal enum AsciiSimplePatternTokenKind : byte
{
    Literal = 0,
    Dot = 1,
    CharClass = 2,
}
