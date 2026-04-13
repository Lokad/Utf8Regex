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
                return Utf8AsciiPrefixTokenExecutor.TryMatchStringPrefix(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte:
                return plan.LiteralUtf8 is { Length: > 0 } prefixUtf8 &&
                    Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(input, prefixUtf8, plan.TerminatorByte, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail:
                return plan.LiteralUtf8 is { Length: > 0 } requiredPrefixUtf8 &&
                    Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredTrimmedOptionalLiteralPrefixTail(
                        input,
                        requiredPrefixUtf8,
                        plan.SecondaryLiteralUtf8 ?? [],
                        out matchedLength);

            default:
                return false;
        }
    }
}
