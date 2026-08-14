namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8ReplacementInstruction
{
    private Utf8ReplacementInstruction(
        Utf8ReplacementInstructionKind kind,
        byte[] literalUtf8,
        int groupNumber,
        bool isBraceEnclosed)
    {
        Kind = kind;
        LiteralUtf8 = literalUtf8;
        GroupNumber = groupNumber;
        IsBraceEnclosed = isBraceEnclosed;
    }

    public Utf8ReplacementInstructionKind Kind { get; }

    public byte[] LiteralUtf8 { get; }

    public int GroupNumber { get; }

    public bool IsBraceEnclosed { get; }

    public static Utf8ReplacementInstruction Literal(byte[] literalUtf8) =>
        new(Utf8ReplacementInstructionKind.Literal, literalUtf8, -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction Group(int groupNumber, bool isBraceEnclosed) =>
        new(Utf8ReplacementInstructionKind.Group, [], groupNumber, isBraceEnclosed);

    public static Utf8ReplacementInstruction WholeMatch() =>
        new(Utf8ReplacementInstructionKind.WholeMatch, [], -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction LeftPortion() =>
        new(Utf8ReplacementInstructionKind.LeftPortion, [], -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction RightPortion() =>
        new(Utf8ReplacementInstructionKind.RightPortion, [], -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction LastGroup() =>
        new(Utf8ReplacementInstructionKind.LastGroup, [], -1, isBraceEnclosed: false);

    public static Utf8ReplacementInstruction WholeString() =>
        new(Utf8ReplacementInstructionKind.WholeString, [], -1, isBraceEnclosed: false);
}
