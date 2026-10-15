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
        return input.IndexOfAnyExceptInRange((byte)0x00, (byte)0x7F) < 0;
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
        switch (SelectThrowIfInvalidOnlyMode(input))
        {
            case ThrowIfInvalidOnlyMode.Utf8IsValid:
                if (Utf8.IsValid(input))
                {
                    return;
                }

                break;

            case ThrowIfInvalidOnlyMode.ValidationCore:
                break;
        }

        if (!Utf8ValidationCore.TryValidate(input, computeUtf16Length: false, out _, out var errorOffset))
        {
            throw Utf8Validation.CreateInvalidUtf8Exception(errorOffset);
        }
    }

    public static Utf8LeadByteSampleShape DescribeLeadByteSample(ReadOnlySpan<byte> input)
        => DescribeLeadByteSample(input, 4096);

    public static Utf8LeadByteSampleShape DescribeLeadByteSample(ReadOnlySpan<byte> input, int maxBytes)
    {
        var sampleLength = Math.Min(input.Length, maxBytes);
        var asciiBytes = 0;
        var firstNonAsciiOffset = -1;
        var twoByteLeads = 0;
        var threeByteLeads = 0;
        var fourByteLeads = 0;

        for (var i = 0; i < sampleLength; i++)
        {
            var value = input[i];
            if (value < 0x80)
            {
                asciiBytes++;
                continue;
            }

            if (firstNonAsciiOffset < 0)
            {
                firstNonAsciiOffset = i;
            }

            if (value is >= 0xC2 and < 0xE0)
            {
                twoByteLeads++;
            }
            else if (value is >= 0xE0 and < 0xF0)
            {
                threeByteLeads++;
            }
            else if (value is >= 0xF0 and < 0xF5)
            {
                fourByteLeads++;
            }
        }

        return new Utf8LeadByteSampleShape(
            sampleLength,
            asciiBytes,
            firstNonAsciiOffset,
            twoByteLeads,
            threeByteLeads,
            fourByteLeads);
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
        var length = Math.Min(input.Length, 256);
        var constrainedOrFourByteLeadCount = 0;
        for (var i = 0; i < length; i++)
        {
            var value = input[i];
            if (value is 0xE0 or 0xED or >= 0xF0)
            {
                constrainedOrFourByteLeadCount++;
            }
        }

        return constrainedOrFourByteLeadCount >= 8;
    }

}
