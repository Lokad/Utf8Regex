namespace Lokad.Utf8Regex.Internal.Execution;

// Unicode 16.0 simple case pairs above the BMP; all 282 mappings are one-to-one
// and stay supplementary. Data: UnicodeData.txt, fields 12/13, copyright 2024
// Unicode, Inc. https://www.unicode.org/Public/16.0.0/ucd/UnicodeData.txt
// See UNICODE-LICENSE.txt. Fixed read-only data needs no runtime/host casing.
internal static class Utf8SupplementaryCase
{
    internal static int Lower(int scalar) => Map(scalar, false);
    internal static int Upper(int scalar) => Map(scalar, true);

    private static int Map(int scalar, bool upper)
    {
        var ranges = Ranges;
        if (scalar < ranges[0] + (upper ? ranges[2] : 0) ||
            scalar >= ranges[^2] + (upper ? ranges[^1] : 0)) return scalar;
        var low = 0;
        var high = ranges.Length / 3 - 1;
        while (low <= high)
        {
            var middle = (low + high) >> 1;
            var index = middle * 3;
            var delta = ranges[index + 2];
            var shift = upper ? delta : 0;
            if (scalar < ranges[index] + shift) high = middle - 1;
            else if (scalar >= ranges[index + 1] + shift) low = middle + 1;
            else return upper ? scalar - delta : scalar + delta;
        }
        return scalar;
    }

    // Upper start, exclusive end, lower-minus-upper. Both source and shifted
    // target runs are ordered/nonoverlapping; gaps are unchanged scalars.
    private static ReadOnlySpan<int> Ranges =>
    [
        0x10400, 0x10428, 0x28,
        0x104B0, 0x104D4, 0x28,
        0x10570, 0x1057B, 0x27,
        0x1057C, 0x1058B, 0x27,
        0x1058C, 0x10593, 0x27,
        0x10594, 0x10596, 0x27,
        0x10C80, 0x10CB3, 0x40,
        0x10D50, 0x10D66, 0x20,
        0x118A0, 0x118C0, 0x20,
        0x16E40, 0x16E60, 0x20,
        0x1E900, 0x1E922, 0x22,
    ];
}
