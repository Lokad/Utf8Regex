namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiDelimitedTokenExecutor
{
    public static bool TryFindDelimitedToken(ReadOnlySpan<byte> input, int startIndex,
        ReadOnlySpan<byte> delimiterUtf8, ReadOnlySpan<byte> secondaryDelimiterUtf8,
        ReadOnlySpan<byte> headCharSetUtf8, ReadOnlySpan<byte> middleCharSetUtf8, ReadOnlySpan<byte> tailCharSetUtf8,
        out int matchIndex, out int matchedLength)
    {
        matchIndex = -1; matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length || delimiterUtf8.IsEmpty || secondaryDelimiterUtf8.Length != 1 ||
            headCharSetUtf8.IsEmpty || middleCharSetUtf8.IsEmpty || tailCharSetUtf8.IsEmpty) return false;
        using var scratch = Utf8ConstructionScope.Enter();
        var search = new PreparedAsciiDelimitedTokenSearch(delimiterUtf8, secondaryDelimiterUtf8, headCharSetUtf8, middleCharSetUtf8, tailCharSetUtf8);
        return search.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
    }

    public static int CountDelimitedTokens(ReadOnlySpan<byte> input, ReadOnlySpan<byte> delimiterUtf8,
        ReadOnlySpan<byte> secondaryDelimiterUtf8, ReadOnlySpan<byte> headCharSetUtf8,
        ReadOnlySpan<byte> middleCharSetUtf8, ReadOnlySpan<byte> tailCharSetUtf8)
    {
        if (delimiterUtf8.IsEmpty || secondaryDelimiterUtf8.Length != 1 || headCharSetUtf8.IsEmpty || middleCharSetUtf8.IsEmpty || tailCharSetUtf8.IsEmpty) return 0;
        using var scratch = Utf8ConstructionScope.Enter();
        var search = new PreparedAsciiDelimitedTokenSearch(delimiterUtf8, secondaryDelimiterUtf8, headCharSetUtf8, middleCharSetUtf8, tailCharSetUtf8);
        return search.Count(input);
    }

    public static int CountDelimitedTokens(ReadOnlySpan<byte> input, PreparedAsciiDelimitedTokenSearch search) => search.Count(input);

    public static bool TryFindDelimitedToken(ReadOnlySpan<byte> input, int startIndex, PreparedAsciiDelimitedTokenSearch search,
        out int matchIndex, out int matchedLength) => search.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
}
