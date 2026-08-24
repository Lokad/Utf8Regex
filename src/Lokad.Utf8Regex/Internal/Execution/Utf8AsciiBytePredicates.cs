using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiBytePredicates
{
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
    public static bool MatchesKnownClass(byte value, AsciiCharClassPredicateKind predicateKind) =>
        predicateKind switch
        {
            AsciiCharClassPredicateKind.Digit => IsDigit(value),
            AsciiCharClassPredicateKind.AsciiLetter => IsLetter(value),
            AsciiCharClassPredicateKind.AsciiLetterOrDigit => IsLetterOrDigit(value),
            AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore => IsWord(value),
            AsciiCharClassPredicateKind.AsciiHexDigit => IsHexDigit(value),
            _ => false,
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSixByteWhitespace(byte value) =>
        value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v';
}
