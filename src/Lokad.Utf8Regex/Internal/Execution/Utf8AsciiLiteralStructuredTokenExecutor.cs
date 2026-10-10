namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLiteralStructuredTokenExecutor
{
    public static bool TryFindStructuredToken(ReadOnlySpan<byte> input, int startIndex, PreparedAsciiLiteralStructuredTokenSearch search,
        out int matchIndex, out int matchedLength) => search.TryFindNext(input, startIndex, out matchIndex, out matchedLength);

    public static bool TryFindStructuredToken(ReadOnlySpan<byte> input, int startIndex,
        ReadOnlySpan<byte> literalUtf8, ReadOnlySpan<byte> headCharSetUtf8, ReadOnlySpan<byte> firstBodyCharSetUtf8, ReadOnlySpan<byte> secondBodyCharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter1Utf8, ReadOnlySpan<byte> optionalTail1CharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter2Utf8, ReadOnlySpan<byte> optionalTail2CharSetUtf8, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1; matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length || literalUtf8.IsEmpty || headCharSetUtf8.IsEmpty ||
            firstBodyCharSetUtf8.IsEmpty || secondBodyCharSetUtf8.IsEmpty || optionalDelimiter1Utf8.Length > 1 || optionalDelimiter2Utf8.Length > 1) return false;
        using var scratch = Utf8ConstructionScope.Enter();
        var search = new PreparedAsciiLiteralStructuredTokenSearch(literalUtf8, headCharSetUtf8, firstBodyCharSetUtf8, secondBodyCharSetUtf8,
            optionalDelimiter1Utf8, optionalTail1CharSetUtf8, optionalDelimiter2Utf8, optionalTail2CharSetUtf8);
        return search.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
    }

    public static int CountStructuredTokens(ReadOnlySpan<byte> input, PreparedAsciiLiteralStructuredTokenSearch search) => search.Count(input);

    public static int CountStructuredTokens(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literalUtf8,
        ReadOnlySpan<byte> headCharSetUtf8, ReadOnlySpan<byte> firstBodyCharSetUtf8, ReadOnlySpan<byte> secondBodyCharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter1Utf8, ReadOnlySpan<byte> optionalTail1CharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter2Utf8, ReadOnlySpan<byte> optionalTail2CharSetUtf8)
    {
        if (literalUtf8.IsEmpty || headCharSetUtf8.IsEmpty || firstBodyCharSetUtf8.IsEmpty || secondBodyCharSetUtf8.IsEmpty ||
            optionalDelimiter1Utf8.Length > 1 || optionalDelimiter2Utf8.Length > 1) return 0;
        using var scratch = Utf8ConstructionScope.Enter();
        var search = new PreparedAsciiLiteralStructuredTokenSearch(literalUtf8, headCharSetUtf8, firstBodyCharSetUtf8, secondBodyCharSetUtf8,
            optionalDelimiter1Utf8, optionalTail1CharSetUtf8, optionalDelimiter2Utf8, optionalTail2CharSetUtf8);
        return search.Count(input);
    }
}
