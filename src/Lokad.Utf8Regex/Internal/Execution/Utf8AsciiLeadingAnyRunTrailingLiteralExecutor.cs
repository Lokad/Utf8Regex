namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLeadingAnyRunTrailingLiteralExecutor
{
    public static bool TryFindMatch(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literalUtf8, out int matchIndex, out int matchedLength)
    {
        matchIndex = 0;
        matchedLength = 0;

        if (literalUtf8.Length == 0)
        {
            return false;
        }

        var trailingIndex = input.LastIndexOf(literalUtf8);
        if (trailingIndex < 0)
        {
            return false;
        }

        matchedLength = trailingIndex + literalUtf8.Length;
        return true;
    }
}
