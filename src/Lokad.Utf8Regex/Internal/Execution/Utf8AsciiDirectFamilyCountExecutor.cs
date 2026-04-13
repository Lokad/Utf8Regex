namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiDirectFamilyCountExecutor
{
    public static bool TryCount(
        ReadOnlySpan<byte> input,
        bool isAscii,
        in Utf8FallbackDirectFamilyPlan plan,
        Utf8EmittedTokenFamilyMatcher? emittedTokenFamilyMatcher,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out int count,
        out string? diagnosticsRoute)
    {
        if (emittedTokenFamilyMatcher is not null)
        {
            count = emittedTokenFamilyMatcher.Count(input);
            diagnosticsRoute = "fallback_direct_ascii_emitted_token_family";
            return true;
        }

        return TryCount(input, isAscii, plan, delimitedTokenSearch, literalStructuredTokenSearch, out count, out diagnosticsRoute);
    }

    public static bool TryCount(
        ReadOnlySpan<byte> input,
        bool isAscii,
        in Utf8FallbackDirectFamilyPlan plan,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out int count,
        out string? diagnosticsRoute)
    {
        count = 0;
        diagnosticsRoute = null;

        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount when isAscii && plan.MinCount > 0:
                diagnosticsRoute = "fallback_direct_ascii_word_bounded";
                count = CountAsciiWordRuns(input, plan.MinCount);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount:
                diagnosticsRoute = "fallback_direct_ascii_dotted_decimal_quad";
                count = Utf8AsciiDottedDecimalQuadExecutor.Count(input);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiUntilByteStarCount:
                diagnosticsRoute = "fallback_direct_ascii_until_byte_star";
                count = Utf8AsciiUntilByteStarExecutor.Count(input, plan.TerminatorByte);
                return true;
        }

        if (Utf8FallbackDirectFamilyCategories.IsPreparedTokenCountFamily(plan.Kind))
        {
            diagnosticsRoute = plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount
                ? "fallback_direct_ascii_delimited_token"
                : "fallback_direct_ascii_literal_structured_token";
            count = Utf8AsciiPreparedTokenFamilyExecutor.CountTokens(input, plan, delimitedTokenSearch, literalStructuredTokenSearch);
            return true;
        }

        if (Utf8FallbackDirectFamilyCategories.IsTokenCountFamily(plan.Kind))
        {
            if (!Utf8AsciiTokenFamilyExecutor.TryCountTokens(input, isAscii, plan, out count, out diagnosticsRoute))
            {
                count = 0;
                diagnosticsRoute = null;
                return false;
            }

            return true;
        }

        return false;
    }

    private static int CountAsciiWordRuns(ReadOnlySpan<byte> input, int minCount)
    {
        var lookup = FrontEnd.Runtime.RegexCharClass.WordCharAsciiLookup;
        var count = 0;
        var index = 0;
        while (index < input.Length)
        {
            while (index < input.Length && lookup[input[index]] == 0)
            {
                index++;
            }

            var start = index;
            while (index < input.Length && lookup[input[index]] != 0)
            {
                index++;
            }

            if (index - start >= minCount)
            {
                count++;
            }
        }

        return count;
    }
}
