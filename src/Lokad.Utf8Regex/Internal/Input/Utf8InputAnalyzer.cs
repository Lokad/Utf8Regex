using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Unicode;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8InputAnalyzer
{
    public static bool IsAscii(ReadOnlySpan<byte> input)
    {
        return System.Text.Ascii.IsValid(input);
    }

    public static Utf8ValidationResult ValidateOnly(ReadOnlySpan<byte> input)
        => ValidateOnly(input, out _);

    public static Utf8ValidationResult ValidateOnly(ReadOnlySpan<byte> input, out bool containsKelvinSign)
    {
        var firstNonAscii = input.IndexOfAnyExceptInRange((byte)0x00, (byte)0x7F);
        if (firstNonAscii < 0)
        {
            containsKelvinSign = false;
            return new Utf8ValidationResult(
                input.Length,
                isAscii: true);
        }

        if (!Utf8ValidationCore.TryValidate(
                input[firstNonAscii..],
                out _,
                out var errorOffset,
                out containsKelvinSign))
        {
            throw Utf8Validation.CreateInvalidUtf8Exception(firstNonAscii + errorOffset);
        }

        return new Utf8ValidationResult(
            input.Length,
            isAscii: false);
    }

    public static void ThrowIfInvalidOnly(ReadOnlySpan<byte> input)
    {
        if (!TryValidateOnly(input, out var errorOffset))
        {
            throw Utf8Validation.CreateInvalidUtf8Exception(errorOffset);
        }
    }

    internal static bool TryValidateOnly(ReadOnlySpan<byte> input, out int errorOffset)
        => TryValidateOnly(input, out errorOffset, out _);

    internal static bool TryValidateOnly(ReadOnlySpan<byte> input, out int errorOffset, out bool isAscii)
    {
        // The discovered ASCII prefix is already valid. Start the ordinary UTF-8
        // checker at its first non-ASCII byte rather than scanning that prefix twice.
        // Non-ASCII validity needs no UTF-16 counts or density classification.
        var firstNonAscii = input.IndexOfAnyExceptInRange((byte)0x00, (byte)0x7F);
        isAscii = firstNonAscii < 0;
        var remaining = isAscii ? ReadOnlySpan<byte>.Empty : input[firstNonAscii..];
        if (isAscii ||
            (Avx2.IsSupported || Vector512.IsHardwareAccelerated && Avx512BW.IsSupported
                ? Utf8ValidationCore.IsValidLookup(remaining)
                : Utf8.IsValid(remaining)))
        {
            errorOffset = -1;
            return true;
        }
        // Only invalid inputs need the scalar decoder's first-error authority.
        var valid = Utf8ValidationCore.TryValidate(remaining, out _, out errorOffset);
        if (!valid) errorOffset += firstNonAscii;
        return valid;
    }

}
