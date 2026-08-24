namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct AsciiSimplePatternToken
{
    public AsciiSimplePatternToken(byte literal)
    {
        Kind = AsciiSimplePatternTokenKind.Literal;
        Literal = literal;
        CharClass = default;
        RequiresAsciiInput = false;
        ScalarClassKind = Utf8SimplePatternScalarClassKind.None;
    }

    public AsciiSimplePatternToken(AsciiCharClass charClass)
        : this(charClass, requiresAsciiInput: false)
    {
    }

    public AsciiSimplePatternToken(AsciiCharClass charClass, bool requiresAsciiInput)
        : this(charClass, requiresAsciiInput, Utf8SimplePatternScalarClassKind.None)
    {
    }

    public AsciiSimplePatternToken(
        AsciiCharClass charClass,
        bool requiresAsciiInput,
        Utf8SimplePatternScalarClassKind scalarClassKind)
    {
        Kind = AsciiSimplePatternTokenKind.CharClass;
        Literal = 0;
        CharClass = charClass;
        RequiresAsciiInput = requiresAsciiInput;
        ScalarClassKind = scalarClassKind;
    }

    private AsciiSimplePatternToken(AsciiSimplePatternTokenKind kind)
    {
        Kind = kind;
        Literal = 0;
        CharClass = default;
        RequiresAsciiInput = false;
        ScalarClassKind = Utf8SimplePatternScalarClassKind.None;
    }

    public AsciiSimplePatternTokenKind Kind { get; }

    public byte Literal { get; }

    public AsciiCharClass CharClass { get; }

    public bool RequiresAsciiInput { get; }

    public Utf8SimplePatternScalarClassKind ScalarClassKind { get; }

    public static AsciiSimplePatternToken Dot { get; } = new(AsciiSimplePatternTokenKind.Dot);
}

internal enum Utf8SimplePatternScalarClassKind : byte
{
    None = 0,
    UnicodeWhitespace = 1,
}

internal enum AsciiSimplePatternTokenKind : byte
{
    Literal = 0,
    Dot = 1,
    CharClass = 2,
}
