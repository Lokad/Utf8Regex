using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiBytePredicates
{
    internal const ulong DecimalMask = 0x3ffUL << '0';
    internal const ulong WhitespaceMask = (0x1fUL << '\t') | (1UL << ' ');
    // The decimal search checks the scalar at each multibyte lead. No
    // continuation byte can become a start.
    private const ulong MultibyteLeadMask = ((1UL << (0xf4 - 0xc2 + 1)) - 1) << (0xc2 - 0xc0);
    internal static readonly PreparedByteSetState DecimalOrScalarLead =
        PreparedByteSetState.FromMasks(DecimalMask, 0, 0, MultibyteLeadMask);
    internal static readonly PreparedByteSetState WordSearch = PreparedByteSetState.FromValues("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz"u8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDigit(byte value) => (uint)(value - (byte)'0') <= 9;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLetter(byte value) =>
        (uint)((value | 0x20) - (byte)'a') <= (byte)'z' - (byte)'a';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLetterOrDigit(byte value) => IsLetter(value) || IsDigit(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWord(byte value) => IsLetterOrDigit(value) || value == (byte)'_';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexDigit(byte value) =>
        IsDigit(value) || (uint)((value | 0x20) - (byte)'a') <= (byte)'f' - (byte)'a';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSixByteWhitespace(byte value) =>
        value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v';
}
