namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8ReplacementInstruction
{
    private Utf8ReplacementInstruction(
        Utf8ReplacementInstructionKind kind,
        byte[] literalUtf8,
        string literalText,
        int groupNumber)
    {
        Kind = kind;
        LiteralUtf8 = literalUtf8;
        LiteralText = literalText;
        GroupNumber = groupNumber;
    }

    public Utf8ReplacementInstructionKind Kind { get; }

    public byte[] LiteralUtf8 { get; }

    public string LiteralText { get; }

    public int GroupNumber { get; }

    internal static Utf8ReplacementInstruction Literal(Utf8ConstructionSession storage, string literalText)
    {
        // Each literal fragment is an independent scalar encoding boundary.
        // Repair unpaired template surrogates before captures can be appended.
        storage.Budget.ChargeWork((ulong)literalText.Length);
        var count = 0;
        for (var offset = 0; offset < literalText.Length;)
        {
            if (offset != 0) storage.Budget.ChargeWork(0);
            var length = LiteralChunkLength(literalText.AsSpan(offset));
            count = checked(count + Encoding.UTF8.GetByteCount(literalText.AsSpan(offset, length)));
            offset += length;
        }
        var bytes = storage.Allocate<byte>(count, true);
        storage.Budget.ChargeWork((ulong)literalText.Length + (ulong)count);
        var written = 0;
        for (var offset = 0; offset < literalText.Length;)
        {
            if (offset != 0) storage.Budget.ChargeWork(0);
            var length = LiteralChunkLength(literalText.AsSpan(offset));
            written += Encoding.UTF8.GetBytes(literalText.AsSpan(offset, length), bytes.AsSpan(written));
            offset += length;
        }
        var valid = true;
        storage.Budget.ChargeWork((ulong)literalText.Length);
        var checkpoint = 2048;
        for (var offset = 0; offset < literalText.Length;)
        {
            if (offset >= checkpoint)
            {
                storage.Budget.ChargeWork(0);
                checkpoint = offset + Math.Min(2048, literalText.Length - offset);
            }
            if (Rune.DecodeFromUtf16(literalText.AsSpan(offset), out _, out var width) != System.Buffers.OperationStatus.Done)
            { valid = false; break; }
            offset += width;
        }
        return new Utf8ReplacementInstruction(Utf8ReplacementInstructionKind.Literal, bytes,
            valid ? literalText : DecodeLiteral(storage, bytes), -1);
    }

    private static int LiteralChunkLength(ReadOnlySpan<char> text)
    {
        var length = Math.Min(2048, text.Length);
        // Keep BCL bulk encoding and its repair rules, without splitting a
        // valid pair into two replacement characters at a checkpoint boundary.
        if (length < text.Length && char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length]))
            length++;
        return length;
    }

    private static string DecodeLiteral(Utf8ConstructionSession storage, byte[] bytes)
        => storage.Budget.DecodeUtf8(bytes, Encoding.UTF8);

    public static Utf8ReplacementInstruction Group(int groupNumber) =>
        new(Utf8ReplacementInstructionKind.Group, [], string.Empty, groupNumber);

    public static Utf8ReplacementInstruction WholeMatch() =>
        new(Utf8ReplacementInstructionKind.WholeMatch, [], string.Empty, -1);

    public static Utf8ReplacementInstruction LeftPortion() =>
        new(Utf8ReplacementInstructionKind.LeftPortion, [], string.Empty, -1);

    public static Utf8ReplacementInstruction RightPortion() =>
        new(Utf8ReplacementInstructionKind.RightPortion, [], string.Empty, -1);

    public static Utf8ReplacementInstruction LastGroup() =>
        new(Utf8ReplacementInstructionKind.LastGroup, [], string.Empty, -1);

    public static Utf8ReplacementInstruction WholeString() =>
        new(Utf8ReplacementInstructionKind.WholeString, [], string.Empty, -1);
}
