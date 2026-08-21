using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8AsciiSearchHeuristics
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetAnchorRarityScore(byte value)
    {
        value = (byte)char.ToUpperInvariant((char)value);
        return value switch
        {
            (byte)'Q' or (byte)'J' or (byte)'X' or (byte)'Z' => 10,
            (byte)'K' or (byte)'V' or (byte)'B' or (byte)'P' or (byte)'Y' or (byte)'G' or (byte)'W' => 8,
            (byte)'F' or (byte)'M' or (byte)'U' or (byte)'C' or (byte)'L' or (byte)'D' => 6,
            (byte)'R' or (byte)'H' or (byte)'S' or (byte)'N' or (byte)'I' or (byte)'O' => 4,
            (byte)'A' or (byte)'T' or (byte)'E' => 2,
            _ when value is >= (byte)'0' and <= (byte)'9' => 5,
            (byte)'_' => 3,
            _ => 1,
        };
    }
}
