namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiDirectFamilyExecutor
{
    public static bool TryFindMatch(
        ReadOnlySpan<byte> input,
        in Utf8FallbackDirectFamilyPlan plan,
        Utf8EmittedTokenFamilyMatcher? emittedTokenFamilyMatcher,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out int matchIndex,
        out int matchedLength)
    {
        if (emittedTokenFamilyMatcher is not null)
        {
            return emittedTokenFamilyMatcher.TryFindNext(input, 0, out matchIndex, out matchedLength);
        }

        return TryFindMatch(input, plan, delimitedTokenSearch, literalStructuredTokenSearch, out matchIndex, out matchedLength);
    }

    public static bool TryFindMatch(
        ReadOnlySpan<byte> input,
        in Utf8FallbackDirectFamilyPlan plan,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = 0;
        matchedLength = 0;

        if (Utf8FallbackDirectFamilyCategories.IsAnchoredPrefixMatchFamily(plan.Kind))
        {
            return Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, plan, out matchedLength);
        }

        if (Utf8FallbackDirectFamilyCategories.IsAnchoredWholeMatchFamily(plan.Kind))
        {
            return Utf8AsciiAnchoredWholeFamilyExecutor.TryMatchWhole(input, plan, out matchedLength);
        }

        if (Utf8FallbackDirectFamilyCategories.IsTokenFindFamily(plan.Kind))
        {
            return Utf8AsciiTokenFamilyExecutor.TryFindToken(input, 0, plan, out matchIndex, out matchedLength);
        }

        if (Utf8FallbackDirectFamilyCategories.IsPreparedTokenFindFamily(plan.Kind))
        {
            return Utf8AsciiPreparedTokenFamilyExecutor.TryFindToken(
                input,
                0,
                plan,
                delimitedTokenSearch,
                literalStructuredTokenSearch,
                out matchIndex,
                out matchedLength);
        }

        if (plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount)
        {
            return Utf8AsciiDottedDecimalQuadExecutor.TryFindNext(input, 0, out matchIndex, out matchedLength);
        }

        if (plan.Kind == Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral &&
            plan.LiteralUtf8 is { Length: > 0 } trailingLiteralUtf8)
        {
            return Utf8AsciiLeadingAnyRunTrailingLiteralExecutor.TryFindMatch(
                input,
                trailingLiteralUtf8,
                out matchIndex,
                out matchedLength);
        }

        return false;
    }
}
