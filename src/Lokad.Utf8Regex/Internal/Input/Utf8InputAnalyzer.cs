using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Unicode;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8InputAnalyzer
{
    public static bool IsAscii(ReadOnlySpan<byte> input)
    {
        return System.Text.Ascii.IsValid(input);
    }

    public static Utf8ValidatedInput Analyze(ReadOnlySpan<byte> input)
        => Utf8ValidatedInput.Create(input);

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
                input.Length,
                isAscii: true,
                containsSupplementaryScalars: false);
        }

        if (!Utf8ValidationCore.TryValidate(
                input[firstNonAscii..],
                out var validation,
                out var errorOffset,
                out containsKelvinSign))
        {
            throw Utf8Validation.CreateInvalidUtf8Exception(firstNonAscii + errorOffset);
        }

        return new Utf8ValidationResult(
            input.Length,
            firstNonAscii + validation.Utf16Length,
            isAscii: false,
            validation.ContainsSupplementaryScalars);
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
        // ASCII is a complete validity proof; avoid sampling and draining it again.
        // Non-ASCII validity needs no UTF-16 counts or density classification.
        // Only invalid inputs fall back to the metadata decoder for first-error offsets.
        isAscii = IsAscii(input);
        if (isAscii ||
            (Avx2.IsSupported || Vector512.IsHardwareAccelerated && Avx512BW.IsSupported
                ? Utf8ValidationCore.IsValidLookup(input)
                : Utf8.IsValid(input)))
        {
            errorOffset = -1;
            return true;
        }
        return Utf8ValidationCore.TryValidate(input, out _, out errorOffset);
    }

}
