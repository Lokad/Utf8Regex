using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiTokenFinderExecutor
{
    private static readonly SearchValues<byte> s_asciiLetterSearchValues = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);
    private static readonly SearchValues<byte> s_asciiAlphaNumericSearchValues = SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    public static bool TryFindNext(ReadOnlySpan<byte> input, int startIndex, SearchValues<byte> headSearchValues, SearchValues<byte> tailSearchValues, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length)
        {
            return false;
        }

        var relative = input[startIndex..].IndexOfAny(headSearchValues);
        if (relative < 0)
        {
            return false;
        }

        matchIndex = startIndex + relative;
        var tail = input[(matchIndex + 1)..];
        var stop = tail.IndexOfAnyExcept(tailSearchValues);
        matchedLength = stop < 0 ? input.Length - matchIndex : 1 + stop;
        return true;
    }

    public static bool TryFindAsciiIdentifierToken(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchedLength)
    {
        return TryFindNext(input, startIndex, s_asciiLetterSearchValues, s_asciiAlphaNumericSearchValues, out matchIndex, out matchedLength);
    }

    public static Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult TryFindAsciiIdentifierTokenWithoutValidation(
        ReadOnlySpan<byte> input,
        int startIndex,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length)
        {
            return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NoMatch;
        }

        var search = input[startIndex..];
        var relative = search.IndexOfAny(s_asciiLetterSearchValues);
        if (relative < 0)
        {
            return search.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0
                ? Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation
                : Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NoMatch;
        }

        if (relative > 0 && search[..relative].IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
        {
            return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation;
        }

        matchIndex = startIndex + relative;
        var tail = input[(matchIndex + 1)..];
        var stop = tail.IndexOfAnyExcept(s_asciiAlphaNumericSearchValues);
        matchedLength = stop < 0 ? input.Length - matchIndex : 1 + stop;
        return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match;
    }
}
