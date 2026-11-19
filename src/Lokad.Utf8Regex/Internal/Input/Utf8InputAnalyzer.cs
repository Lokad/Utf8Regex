namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8InputAnalyzer
{
    public static bool IsAscii(ReadOnlySpan<byte> input)
    {
        return System.Text.Ascii.IsValid(input);
    }
}
