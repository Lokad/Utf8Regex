namespace Lokad.Utf8Regex.Internal.Diagnostics;

internal static class Utf8MatchInspection
{
    public static int ProjectWholeMatchOnly(int matchedLength)
    {
        var match = new Utf8ValueMatch(true, true, 0, matchedLength, 0, matchedLength);
        return match.IndexInUtf16 + match.LengthInUtf16;
    }

    public static int ProjectByteAlignedMatchOnly(int index, int matchedLength)
    {
        var match = new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
        return match.IndexInUtf16 + match.LengthInUtf16;
    }
}
