using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Unicode;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8InputAnalyzer
{
    internal enum ThrowIfInvalidOnlyMode : byte
    {
        Utf8IsValid = 0,
        ValidationCore = 1,
    }

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
                computeUtf16Length: true,
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
        // Other inputs retain the established UTF-8 selector and error offsets.
        isAscii = IsAscii(input);
        if (isAscii ||
            (Avx2.IsSupported || Vector512.IsHardwareAccelerated && Avx512BW.IsSupported
                ? Utf8ValidationCore.IsValidLookup(input)
                : SelectThrowIfInvalidOnlyMode(input) == ThrowIfInvalidOnlyMode.Utf8IsValid && Utf8.IsValid(input)))
        {
            errorOffset = -1;
            return true;
        }
        return Utf8ValidationCore.TryValidate(input, computeUtf16Length: false, out _, out errorOffset);
    }

    public static ThrowIfInvalidOnlyMode SelectThrowIfInvalidOnlyMode(ReadOnlySpan<byte> input)
    {
        return ShouldUseUtf8IsValidFastPath(input)
            ? ThrowIfInvalidOnlyMode.Utf8IsValid
            : ThrowIfInvalidOnlyMode.ValidationCore;
    }

    // Both lanes are exact validity checks, so this gate only selects cost:
    // constrained (E0/ED) and four-byte leads cost the scalar core a range
    // check each while BCL Utf8.IsValid stays vectorized. The 256-byte window
    // bounds the probe to a short constant scan. Re-derive the count with
    // --measure-utf8-validation-profile over the frozen constrained-three-byte
    // profiles; it is a cost trade-off, not a correctness gate.
    private static bool ShouldUseUtf8IsValidFastPath(ReadOnlySpan<byte> input)
    {
        var probe = input[..Math.Min(input.Length, 256)];
        var constrainedOrFourByteLeadCount = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            while (probe.Length >= Vector256<byte>.Count)
            {
                var current = MemoryMarshal.Read<Vector256<byte>>(probe);
                var leads = Vector256.Equals(current, Vector256.Create((byte)0xE0)) |
                    Vector256.Equals(current, Vector256.Create((byte)0xED)) |
                    Vector256.GreaterThanOrEqual(current, Vector256.Create((byte)0xF0));
                constrainedOrFourByteLeadCount += BitOperations.PopCount(leads.ExtractMostSignificantBits());
                if (constrainedOrFourByteLeadCount >= 8) return true;
                probe = probe[Vector256<byte>.Count..];
            }
        }
        for (var i = 0; i < probe.Length; i++)
        {
            var value = probe[i];
            if (value is 0xE0 or 0xED or >= 0xF0)
            {
                // The count cannot decrease; later bytes cannot change this choice.
                if (++constrainedOrFourByteLeadCount == 8) return true;
            }
        }

        return false;
    }

}
