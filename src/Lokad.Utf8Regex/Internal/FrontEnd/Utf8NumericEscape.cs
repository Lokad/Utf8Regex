namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Shared lexical facts for the owned front end and VM parser. A bare decimal
// run is checked against the final capture table before octal fallback; zero
// escapes and class members go directly through the octal prefix reader.
internal static class Utf8NumericEscape
{
    internal static bool TryReadOctal(ReadOnlySpan<char> text, out int value, out int consumed)
    {
        value = 0;
        consumed = 0;
        while (consumed < text.Length && consumed < 3 && text[consumed] is >= '0' and <= '7')
        {
            value = value * 8 + text[consumed] - '0';
            consumed++;
        }
        value &= 255;
        return consumed != 0;
    }

    internal static bool TryReadBareOctal(string digits, out int value, out int consumed)
    {
        value = 0;
        consumed = 0;
        // .NET checks the complete decimal run for overflow even when its
        // eventual meaning would have been an octal prefix plus literal rest.
        return digits.Length > 1 && Utf8CaptureCatalog.TryParseNumber(digits, out _) &&
            TryReadOctal(digits.AsSpan(), out value, out consumed);
    }
}
