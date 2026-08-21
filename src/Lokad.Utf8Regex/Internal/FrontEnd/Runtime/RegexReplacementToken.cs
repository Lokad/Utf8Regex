namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal readonly struct RegexReplacementToken
{
    public RegexReplacementToken(RegexReplacementTokenKind kind)
        : this(kind, null, -1, false)
    {
    }

    public RegexReplacementToken(RegexReplacementTokenKind kind, string? literal)
        : this(kind, literal, -1, false)
    {
    }

    public RegexReplacementToken(
        RegexReplacementTokenKind kind,
        string? literal,
        bool isBraceEnclosed)
        : this(kind, literal, -1, isBraceEnclosed)
    {
    }

    public RegexReplacementToken(
        RegexReplacementTokenKind kind,
        int groupNumber,
        bool isBraceEnclosed)
        : this(kind, null, groupNumber, isBraceEnclosed)
    {
    }

    public RegexReplacementToken(
        RegexReplacementTokenKind kind,
        string? literal,
        int groupNumber,
        bool isBraceEnclosed)
    {
        Kind = kind;
        Literal = literal;
        GroupNumber = groupNumber;
        IsBraceEnclosed = isBraceEnclosed;
    }

    public RegexReplacementTokenKind Kind { get; }

    public string? Literal { get; }

    public int GroupNumber { get; }

    public bool IsBraceEnclosed { get; }

    public string? GroupName => Kind == RegexReplacementTokenKind.Group && GroupNumber < 0 ? Literal : null;

    public bool IsGroupReference => Kind == RegexReplacementTokenKind.Group;

    public bool IsSpecialSubstitution =>
        Kind is RegexReplacementTokenKind.WholeMatch
            or RegexReplacementTokenKind.LeftPortion
            or RegexReplacementTokenKind.RightPortion
            or RegexReplacementTokenKind.LastGroup
            or RegexReplacementTokenKind.WholeString;
}
