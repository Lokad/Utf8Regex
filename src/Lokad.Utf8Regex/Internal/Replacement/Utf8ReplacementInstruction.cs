namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8ReplacementInstruction
{
    private Utf8ReplacementInstruction(
        Utf8ReplacementInstructionKind kind,
        byte[] literalUtf8,
        string literalText,
        int groupNumber,
        bool isBraceEnclosed)
    {
        Kind = kind;
        LiteralUtf8 = literalUtf8;
        LiteralText = literalText;
        GroupNumber = groupNumber;
        IsBraceEnclosed = isBraceEnclosed;
    }

    public Utf8ReplacementInstructionKind Kind { get; }

    public byte[] LiteralUtf8 { get; }

    public string LiteralText { get; }

    public int GroupNumber { get; }

    public bool IsBraceEnclosed { get; }

    public static Utf8ReplacementInstruction Literal(byte[] literalUtf8) =>
        new(Utf8ReplacementInstructionKind.Literal, literalUtf8, Encoding.UTF8.GetString(literalUtf8), -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction Literal(string literalText) =>
        new(Utf8ReplacementInstructionKind.Literal, Encoding.UTF8.GetBytes(literalText), literalText, -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction Group(int groupNumber, bool isBraceEnclosed) =>
        new(Utf8ReplacementInstructionKind.Group, [], string.Empty, groupNumber, isBraceEnclosed);

    public static Utf8ReplacementInstruction WholeMatch() =>
        new(Utf8ReplacementInstructionKind.WholeMatch, [], string.Empty, -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction LeftPortion() =>
        new(Utf8ReplacementInstructionKind.LeftPortion, [], string.Empty, -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction RightPortion() =>
        new(Utf8ReplacementInstructionKind.RightPortion, [], string.Empty, -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction LastGroup() =>
        new(Utf8ReplacementInstructionKind.LastGroup, [], string.Empty, -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction WholeString() =>
        new(Utf8ReplacementInstructionKind.WholeString, [], string.Empty, -1, isBraceEnclosed: false);
}
