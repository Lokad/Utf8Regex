namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiAnchoredWholeFamilyExecutor
{
    public static bool TryMatchWhole(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan, out int matchedLength)
    {
        matchedLength = 0;
        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail:
                return Utf8AsciiLeadingDigitsTailExecutor.TryMatchWhole(input, plan.LiteralUtf8 ?? [], out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole:
                return Utf8AsciiEmailWholeExecutor.TryMatchWhole(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole:
                return Utf8AsciiSpreadsheetReferenceExecutor.TryMatchCellReferenceWhole(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole:
                return Utf8AsciiSpreadsheetReferenceExecutor.TryMatchRangeReferenceWhole(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole:
                return Utf8AsciiAnchoredUrlExecutor.TryMatchDigitsQueryWhole(input, plan, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole:
                return Utf8AsciiAnchoredUrlExecutor.TryMatchHexQueryWhole(input, plan, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole:
                return Utf8AsciiHexColorExecutor.TryMatchWhole(input, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole:
                return Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, out matchedLength);

            default:
                return false;
        }
    }
}
