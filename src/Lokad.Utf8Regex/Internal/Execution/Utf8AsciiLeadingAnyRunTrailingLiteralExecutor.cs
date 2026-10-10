namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLeadingAnyRunTrailingLiteralExecutor
{
    public static bool IsMatch(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literalUtf8)
    {
        return literalUtf8.Length > 0 && input.IndexOf(literalUtf8) >= 0;
    }

    public static bool TryFindMatch(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literalUtf8, out int matchIndex, out int matchedLength)
    {
        matchIndex = 0;
        matchedLength = 0;

        if (literalUtf8.Length == 0)
        {
            return false;
        }

        var lineStart = 0;
        while (lineStart < input.Length)
        {
            var newline = input[lineStart..].IndexOf((byte)'\n');
            var lineEnd = newline < 0 ? input.Length : lineStart + newline;
            // Dot cannot cross LF, but the literal can consume it. Limit the
            // candidate start to this line and let the literal extend past LF.
            var windowLength = (int)Math.Min((long)input.Length - lineStart, (long)lineEnd - lineStart + literalUtf8.Length);
            var trailingIndex = input.Slice(lineStart, windowLength).LastIndexOf(literalUtf8);
            if (trailingIndex >= 0)
            {
                matchIndex = lineStart; matchedLength = trailingIndex + literalUtf8.Length; return true;
            }
            if (lineEnd == input.Length) return false;
            lineStart = lineEnd + 1;
        }
        return false;
    }
}
