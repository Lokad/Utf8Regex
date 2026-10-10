namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiPreparedTokenFamilyExecutor
{
    public static bool TryFindToken(ReadOnlySpan<byte> input, int startIndex, in Utf8FallbackDirectFamilyPlan plan,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch, out int matchIndex, out int matchedLength)
    {
        if (plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount)
        {
            if (delimitedTokenSearch.HasValue)
                return delimitedTokenSearch.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
            using var scratch = Utf8ConstructionScope.Enter();
            var search = Utf8FallbackPreparedTokenSearchFactory.CreateDelimitedTokenSearch(plan);
            return search.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
        }
        if (plan.Kind == Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount)
        {
            if (literalStructuredTokenSearch.HasValue)
                return literalStructuredTokenSearch.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
            using var scratch = Utf8ConstructionScope.Enter();
            var search = Utf8FallbackPreparedTokenSearchFactory.CreateLiteralStructuredTokenSearch(plan);
            return search.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
        }
        matchIndex = 0; matchedLength = 0; return false;
    }

    public static int CountTokens(ReadOnlySpan<byte> input, in Utf8FallbackDirectFamilyPlan plan,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch)
    {
        if (plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount)
        {
            if (delimitedTokenSearch.HasValue) return delimitedTokenSearch.Count(input);
            using var scratch = Utf8ConstructionScope.Enter();
            var search = Utf8FallbackPreparedTokenSearchFactory.CreateDelimitedTokenSearch(plan);
            return search.Count(input);
        }
        if (plan.Kind == Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount)
        {
            if (literalStructuredTokenSearch.HasValue) return literalStructuredTokenSearch.Count(input);
            using var scratch = Utf8ConstructionScope.Enter();
            var search = Utf8FallbackPreparedTokenSearchFactory.CreateLiteralStructuredTokenSearch(plan);
            return search.Count(input);
        }
        return 0;
    }
}
