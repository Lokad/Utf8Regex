namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8WhiteSpaceRanges
{
    // BMP White_Space ranges observed on .NET 10 (exhaustively re-verified
    // against char.IsWhiteSpace in Utf8ShorthandTableTests). Astral spaces stay
    // BCL-routed with astral inputs, so no supplementary ranges ship.
    private static readonly (int Start, int End)[] Ranges =
    [
        (9, 13), // U+0009..U+000D
        (32, 32), // U+0020..U+0020
        (133, 133), // U+0085..U+0085
        (160, 160), // U+00A0..U+00A0
        (5760, 5760), // U+1680..U+1680
        (8192, 8202), // U+2000..U+200A
        (8232, 8233), // U+2028..U+2029
        (8239, 8239), // U+202F..U+202F
        (8287, 8287), // U+205F..U+205F
        (12288, 12288), // U+3000..U+3000
    ];

    public static bool IsWhiteSpace(uint scalar)
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
