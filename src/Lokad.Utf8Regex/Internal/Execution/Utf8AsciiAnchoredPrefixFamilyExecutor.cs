namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiAnchoredPrefixFamilyExecutor
{
    public static bool TryMatchPrefix(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, out int matchedLength)
    {
        matchedLength = 0;
        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix:
                return Utf8AsciiPrefixTokenExecutor.TryMatchIdentifierPrefixIgnoreCase(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix:
                return Utf8AsciiPrefixTokenExecutor.TryMatchNumberPrefix(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredOperatorRun:
                return Utf8AsciiPrefixTokenExecutor.TryMatchOperatorRunPrefix(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix:
                return Utf8AsciiPrefixTokenExecutor.TryMatchQuotedStringPrefix(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte:
                return plan.LiteralUtf8 is { Length: > 0 } prefixUtf8 &&
                    Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(input, prefixUtf8, plan.TerminatorByte, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail:
                return plan.LiteralUtf8 is { Length: > 0 } requiredPrefixUtf8 &&
                    Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredTrimmedOptionalLiteralPrefixTail(
                        input,
                        requiredPrefixUtf8,
                        plan.SecondaryLiteralUtf8,
                        out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix:
                if (plan.LiteralUtf8 is not { Length: > 0 } anchoredLiteral ||
                    anchoredLiteral.Length > input.Length ||
                    !input[..anchoredLiteral.Length].SequenceEqual(anchoredLiteral))
                {
                    return false;
                }

                matchedLength = anchoredLiteral.Length;
                return true;

            case Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix:
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
