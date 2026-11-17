namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiPatternToken
{
    public AsciiPatternToken(byte literal)
    {
        Kind = AsciiPatternTokenKind.Literal;
        Literal = literal;
        CharClass = default;
    }

    public AsciiPatternToken(AsciiCharClass charClass)
    {
        Kind = AsciiPatternTokenKind.CharClass;
        Literal = 0;
        CharClass = charClass;
    }

    private AsciiPatternToken(AsciiPatternTokenKind kind)
    {
        Kind = kind;
        Literal = 0;
        CharClass = default;
    }

    public AsciiPatternTokenKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass CharClass { get; }



    public static AsciiPatternToken Dot { get; } = new(AsciiPatternTokenKind.Dot);
}

internal enum AsciiPatternTokenKind : byte
{
    Literal = 0,
    Dot = 1,
    CharClass = 2,
}
