namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AnchoredLiteralPrefixExecutor
{
    public static bool TryMatchPrefix(ReadOnlySpan<byte> input, in Utf8AnchoredLiteralPlan plan, out int matchedLength)
    {
        matchedLength = 0;
        switch (plan.Kind)
        {

            case Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix:
                if (plan.LiteralUtf8 is not { Length: > 0 } anchoredLiteral ||
                    anchoredLiteral.Length > input.Length ||
                    !input[..anchoredLiteral.Length].SequenceEqual(anchoredLiteral))
                {
                    return false;
                }

                matchedLength = anchoredLiteral.Length;
                return true;

            case Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix:
                if (plan.LiteralUtf8 is not { Length: > 0 } foldedLiteral ||
                    foldedLiteral.Length > input.Length ||
                    !FoldedPrefixEquals(input, foldedLiteral))
                {
                    return false;
                }

                matchedLength = foldedLiteral.Length;
                return true;

            default:
                return false;
        }
    }

    // Invariant ASCII fold equality for one anchored literal: Kelvin (U+212A) and
    // Turkic dotted/dotless-I divergences cannot collide at the byte level, and both
    // stay fenced upstream of every direct-family consumer.
    private static bool FoldedPrefixEquals(ReadOnlySpan<byte> input, byte[] literal)
    {
        return FoldedSequenceEquals(input[..literal.Length], literal);
    }

    // Shared with the suffix executor: both families fold the same anchored bytes.
    internal static bool FoldedSequenceEquals(ReadOnlySpan<byte> input, byte[] literal)
    {
        if (input.Length < literal.Length)
        {
            return false;
        }

        for (var i = 0; i < literal.Length; i++)
        {
            var candidate = input[i];
            var expected = literal[i];
            if (candidate == expected)
            {
                continue;
            }

            if (ToUpperInvariantAscii(candidate) != ToUpperInvariantAscii(expected))
            {
                return false;
            }
        }

        return true;
    }

    private static byte ToUpperInvariantAscii(byte value)
    {
        return value is >= (byte)'a' and <= (byte)'z' ? (byte)(value - 32) : value;
    }
}
