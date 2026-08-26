using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReAsciiPrefixDigitMatcher
{
    public static int MatchValidated(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix)
    {
        _ = Utf8InputAnalyzer.ValidateOnly(input);
        return MatchUnchecked(input, prefix);
    }

    public static int MatchUnchecked(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix)
    {
        if (!input.StartsWith(prefix))
        {
            return 0;
        }

        var end = prefix.Length;
        var digitStart = end;
        while ((uint)end < (uint)input.Length && input[end] is >= (byte)'0' and <= (byte)'9')
        {
            end++;
        }

        return end == digitStart ? 0 : end;
    }
}
