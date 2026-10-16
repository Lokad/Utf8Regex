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

    internal static Utf8ReplacementInstruction Literal(Utf8ConstructionSession storage, string literalText)
    {
        // Each literal fragment is an independent scalar encoding boundary.
        // Repair unpaired template surrogates before captures can be appended.
        storage.Budget.ChargeWork((ulong)literalText.Length);
        var count = Encoding.UTF8.GetByteCount(literalText);
        var bytes = storage.Allocate<byte>(count, true);
        storage.Budget.ChargeWork((ulong)literalText.Length + (ulong)count);
        Encoding.UTF8.GetBytes(literalText.AsSpan(), bytes);
        var valid = true;
        storage.Budget.ChargeWork((ulong)literalText.Length);
        for (var offset = 0; offset < literalText.Length;)
        {
            if (Rune.DecodeFromUtf16(literalText.AsSpan(offset), out _, out var width) != System.Buffers.OperationStatus.Done)
            { valid = false; break; }
            offset += width;
        }
        return new Utf8ReplacementInstruction(Utf8ReplacementInstructionKind.Literal, bytes,
            valid ? literalText : DecodeLiteral(storage, bytes), -1, false);
    }

    private static string DecodeLiteral(Utf8ConstructionSession storage, byte[] bytes)
        => storage.Budget.DecodeUtf8(bytes, Encoding.UTF8);

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
