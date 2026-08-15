namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiSimplePatternToken
{
    public AsciiSimplePatternToken(byte literal)
    {
        Kind = AsciiSimplePatternTokenKind.Literal;
        Literal = literal;
        CharClass = default;
        RequiresAsciiInput = false;
    }

    public AsciiSimplePatternToken(AsciiCharClass charClass)
        : this(charClass, requiresAsciiInput: false)
    {
    }

    public AsciiSimplePatternToken(AsciiCharClass charClass, bool requiresAsciiInput)
    {
        Kind = AsciiSimplePatternTokenKind.CharClass;
        Literal = 0;
        CharClass = charClass;
        RequiresAsciiInput = requiresAsciiInput;
    }

    private AsciiSimplePatternToken(AsciiSimplePatternTokenKind kind)
    {
        Kind = kind;
        Literal = 0;
        CharClass = default;
        RequiresAsciiInput = false;
    }

    public AsciiSimplePatternTokenKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass CharClass { get; }

    public bool RequiresAsciiInput { get; }

    public static AsciiSimplePatternToken Dot { get; } = new(AsciiSimplePatternTokenKind.Dot);
}

internal enum AsciiSimplePatternTokenKind : byte
{
    Literal = 0,
    Dot = 1,
    CharClass = 2,
}
