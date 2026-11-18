namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8ReplacementInstruction
{
    private Utf8ReplacementInstruction(
        Utf8ReplacementInstructionKind kind,
        byte[] literalUtf8,
        int groupNumber)
    {
        Kind = kind;
        LiteralUtf8 = literalUtf8;
        GroupNumber = groupNumber;
    }

    public Utf8ReplacementInstructionKind Kind { get; }

    public byte[] LiteralUtf8 { get; }

    public int GroupNumber { get; }

    internal static Utf8ReplacementInstruction Literal(byte[] admittedUtf8)
        => new(Utf8ReplacementInstructionKind.Literal, admittedUtf8, -1);

    public static Utf8ReplacementInstruction Group(int groupNumber) =>
        new(Utf8ReplacementInstructionKind.Group, [], groupNumber);

    public static Utf8ReplacementInstruction WholeMatch() =>
        new(Utf8ReplacementInstructionKind.WholeMatch, [], -1);

    public static Utf8ReplacementInstruction LeftPortion() =>
        new(Utf8ReplacementInstructionKind.LeftPortion, [], -1);

    public static Utf8ReplacementInstruction RightPortion() =>
        new(Utf8ReplacementInstructionKind.RightPortion, [], -1);

    public static Utf8ReplacementInstruction LastGroup() =>
        new(Utf8ReplacementInstructionKind.LastGroup, [], -1);

    public static Utf8ReplacementInstruction WholeString() =>
        new(Utf8ReplacementInstructionKind.WholeString, [], -1);
}
