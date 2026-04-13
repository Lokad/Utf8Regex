using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8SearchKernel
{
    public static int IndexOfLiteral(ReadOnlySpan<byte> input, PreparedSubstringSearch search)
    {
        return search.IndexOf(input);
    }

    public static int IndexOfLiteral(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal, bool ignoreCase)
    {
        return ignoreCase
            ? AsciiSearch.IndexOfIgnoreCase(input, literal)
            : AsciiSearch.IndexOfExact(input, literal);
    }

    public static int LastIndexOfLiteral(ReadOnlySpan<byte> input, PreparedSubstringSearch search)
    {
        return search.LastIndexOf(input);
    }

    public static int LastIndexOfLiteral(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal, bool ignoreCase)
    {
        return ignoreCase
            ? AsciiSearch.LastIndexOfIgnoreCase(input, literal)
            : AsciiSearch.LastIndexOfExact(input, literal);
    }

    public static int IndexOfAnyLiteral(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        return AsciiSearch.IndexOfAnyExact(input, searchData);
    }

    public static int IndexOfAnyLiteral(ReadOnlySpan<byte> input, PreparedLiteralSetSearch search)
    {
        return search.IndexOf(input);
    }

    public static int IndexOfAnyLiteral(ReadOnlySpan<byte> input, PreparedMultiLiteralSearch search)
    {
        return search.IndexOf(input);
    }

    public static int IndexOfAnyIgnoreCaseLiteral(ReadOnlySpan<byte> input, PreparedAsciiIgnoreCaseLiteralSetSearch search)
    {
        return search.IndexOf(input);
    }

    public static int LastIndexOfAnyLiteral(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        return AsciiSearch.LastIndexOfAnyExact(input, searchData);
    }

    public static int LastIndexOfAnyLiteral(ReadOnlySpan<byte> input, PreparedLiteralSetSearch search)
    {
        return search.LastIndexOf(input);
    }

    public static int LastIndexOfAnyLiteral(ReadOnlySpan<byte> input, PreparedMultiLiteralSearch search)
    {
        return search.LastIndexOf(input);
    }

    public static int LastIndexOfAnyIgnoreCaseLiteral(ReadOnlySpan<byte> input, PreparedAsciiIgnoreCaseLiteralSetSearch search)
    {
        return search.LastIndexOf(input);
    }

    public static bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, AsciiExactLiteralSearchData searchData, out int matchedLength)
    {
        return AsciiSearch.TryGetMatchedLiteralLength(input, index, searchData, out matchedLength);
    }

    public static bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, PreparedLiteralSetSearch search, out int matchedLength)
    {
        return search.TryGetMatchedLiteralLength(input, index, out matchedLength);
    }

    public static bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, PreparedMultiLiteralSearch search, out int matchedLength)
    {
        return search.TryGetMatchedLiteralLength(input, index, out matchedLength);
    }

    public static bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, PreparedAsciiIgnoreCaseLiteralSetSearch search, out int matchedLength)
    {
        return search.TryGetMatchedLiteralLength(input, index, out matchedLength);
    }
}
