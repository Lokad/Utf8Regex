using Lokad.Utf8Regex.Internal.Diagnostics;

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
        out Utf8ExecutionRoute diagnosticsRoute)
    {
        if (emittedTokenFamilyMatcher is not null && isAscii)
        {
            count = emittedTokenFamilyMatcher.Count(input);
            diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiEmittedTokenFamily;
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
        out Utf8ExecutionRoute diagnosticsRoute)
    {
        count = 0;
        diagnosticsRoute = Utf8ExecutionRoute.None;

        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount when isAscii && plan.MinCount > 0:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiWordBounded;
                count = CountAsciiWordRuns(input, plan.MinCount);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiDottedDecimalQuad;
                count = Utf8AsciiDottedDecimalQuadExecutor.Count(input);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiUntilByteStarCount:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiUntilByteStar;
                count = Utf8AsciiUntilByteStarExecutor.Count(input, plan.TerminatorByte);
                return true;
        }

        if (Utf8FallbackDirectFamilyCategories.IsPreparedTokenCountFamily(plan.Kind))
        {
            if (plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount && !isAscii)
            {
                return false;
            }

            diagnosticsRoute = plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount
                ? Utf8ExecutionRoute.FallbackDirectAsciiDelimitedToken
                : Utf8ExecutionRoute.FallbackDirectAsciiLiteralStructuredToken;
            count = Utf8AsciiPreparedTokenFamilyExecutor.CountTokens(input, plan, delimitedTokenSearch, literalStructuredTokenSearch);
            return true;
        }

        if (Utf8FallbackDirectFamilyCategories.IsTokenCountFamily(plan.Kind))
        {
            if (!Utf8AsciiTokenFamilyExecutor.TryCountTokens(input, isAscii, plan, out count, out diagnosticsRoute))
            {
                count = 0;
                diagnosticsRoute = Utf8ExecutionRoute.None;
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
