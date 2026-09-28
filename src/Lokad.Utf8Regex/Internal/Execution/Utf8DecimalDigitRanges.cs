namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8DecimalDigitRanges
{
    // BMP DecimalDigitNumber ranges observed on .NET 10 (exhaustively re-verified
    // against char.GetUnicodeCategory in Utf8DecimalDigitRangesTests). Astral
    // digits stay BCL-routed with astral inputs, so no supplementary ranges ship.
    private static readonly (int Start, int End)[] Ranges =
    [
        (48, 57), // U+0030..U+0039
        (1632, 1641), // U+0660..U+0669
        (1776, 1785), // U+06F0..U+06F9
        (1984, 1993), // U+07C0..U+07C9
        (2406, 2415), // U+0966..U+096F
        (2534, 2543), // U+09E6..U+09EF
        (2662, 2671), // U+0A66..U+0A6F
        (2790, 2799), // U+0AE6..U+0AEF
        (2918, 2927), // U+0B66..U+0B6F
        (3046, 3055), // U+0BE6..U+0BEF
        (3174, 3183), // U+0C66..U+0C6F
        (3302, 3311), // U+0CE6..U+0CEF
        (3430, 3439), // U+0D66..U+0D6F
        (3558, 3567), // U+0DE6..U+0DEF
        (3664, 3673), // U+0E50..U+0E59
        (3792, 3801), // U+0ED0..U+0ED9
        (3872, 3881), // U+0F20..U+0F29
        (4160, 4169), // U+1040..U+1049
        (4240, 4249), // U+1090..U+1099
        (6112, 6121), // U+17E0..U+17E9
        (6160, 6169), // U+1810..U+1819
        (6470, 6479), // U+1946..U+194F
        (6608, 6617), // U+19D0..U+19D9
        (6784, 6793), // U+1A80..U+1A89
        (6800, 6809), // U+1A90..U+1A99
        (6992, 7001), // U+1B50..U+1B59
        (7088, 7097), // U+1BB0..U+1BB9
        (7232, 7241), // U+1C40..U+1C49
        (7248, 7257), // U+1C50..U+1C59
        (42528, 42537), // U+A620..U+A629
        (43216, 43225), // U+A8D0..U+A8D9
        (43264, 43273), // U+A900..U+A909
        (43472, 43481), // U+A9D0..U+A9D9
        (43504, 43513), // U+A9F0..U+A9F9
        (43600, 43609), // U+AA50..U+AA59
        (44016, 44025), // U+ABF0..U+ABF9
        (65296, 65305), // U+FF10..U+FF19
    ];

    public static bool IsDecimalDigit(uint scalar)
    {
        foreach (var range in Ranges)
        {
            if (scalar >= (uint)range.Start && scalar <= (uint)range.End)
            {
                return true;
            }
        }

        return false;
    }
}
