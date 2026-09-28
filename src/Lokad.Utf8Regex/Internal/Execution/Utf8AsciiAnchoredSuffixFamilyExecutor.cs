namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiAnchoredSuffixFamilyExecutor
{
    // Whole-pattern anchored ASCII literal suffixes (`literal$` and `literal\Z`,
    // plus the strict `literal\z` twin): the match, if any, is exactly the literal
    // at the input end, or right before a single trailing line feed for the lax
    // forms. A miss is definitive, so every operation stays clear of the fallback
    // engine. Byte-exact on every input for the sensitive kinds: astral scalars
    // never end with ASCII bytes. Folded kinds compare per byte under invariant
    // folding (Kelvin stays fenced upstream). Coordinates
    // come back in bytes; UTF-16 projection belongs to the caller, which owns
    // input validation there.
    public static bool TryMatchSuffix(
        ReadOnlySpan<byte> input,
        in Utf8FallbackDirectFamilyPlan plan,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        var folded = plan.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd;
        if (!folded &&
            plan.Kind != Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix &&
            plan.Kind != Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd ||
            plan.LiteralUtf8 is not { Length: > 0 } anchoredLiteral)
        {
            return false;
        }

        var end = input.Length;
        var lax = plan.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix;
        if (lax &&
            end > anchoredLiteral.Length &&
            input[end - 1] == (byte)'\n')
        {
            end--;
        }

        var start = end - anchoredLiteral.Length;
        var sliceMatches = start >= 0 && (folded
            ? Utf8AsciiAnchoredPrefixFamilyExecutor.FoldedSequenceEquals(input.Slice(start, anchoredLiteral.Length), anchoredLiteral)
            : input.Slice(start, anchoredLiteral.Length).SequenceEqual(anchoredLiteral));
        if (!sliceMatches)
        {
            return false;
        }

        matchIndex = start;
        matchedLength = anchoredLiteral.Length;
        return true;
    }
}
