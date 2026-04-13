using System.Text;
using System.Text.Unicode;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8InputAnalyzer
{
    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal enum ThrowIfInvalidOnlyMode : byte
    {
        StrictUtf8GetCharCount = 0,
        Utf8IsValid = 1,
        ValidationCore = 2,
    }

    public static bool IsAscii(ReadOnlySpan<byte> input)
    {
        return input.IndexOfAnyExceptInRange((byte)0x00, (byte)0x7F) < 0;
    }

    public static Utf8InputAnalysis Analyze(ReadOnlySpan<byte> input)
    {
        var validation = ValidateOnly(input);
        return new Utf8InputAnalysis(
            validation,
            Utf8BoundaryMap.Create(input, validation));
    }

    public static Utf8ValidationResult ValidateOnly(ReadOnlySpan<byte> input)
    {
        var firstNonAscii = input.IndexOfAnyExceptInRange((byte)0x00, (byte)0x7F);
        if (firstNonAscii < 0)
        {
            return new Utf8ValidationResult(
                input.Length,
                input.Length,
                isAscii: true,
                containsSupplementaryScalars: false);
        }

        if (TryGetUtf16LengthFast(input, firstNonAscii, out var fastValidation))
        {
            return fastValidation;
        }

        if (!Utf8ValidationCore.TryValidate(input, computeUtf16Length: true, out var validation, out var errorOffset))
        {
            throw Utf8Validation.CreateInvalidUtf8Exception(errorOffset);
        }

        return validation;
    }

    public static void ThrowIfInvalidOnly(ReadOnlySpan<byte> input)
    {
        switch (SelectThrowIfInvalidOnlyMode(input))
        {
            case ThrowIfInvalidOnlyMode.StrictUtf8GetCharCount:
                if (TryThrowIfInvalidOnlyFast(input))
                {
                    return;
                }

                break;

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

    public static Utf8LeadByteSampleShape DescribeLeadByteSample(ReadOnlySpan<byte> input, int maxBytes = 4096)
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
        if (!MightContainThreeOrFourByteSequence(input))
        {
            return ThrowIfInvalidOnlyMode.StrictUtf8GetCharCount;
        }

        return ShouldUseUtf8IsValidFastPath(input)
            ? ThrowIfInvalidOnlyMode.Utf8IsValid
            : ThrowIfInvalidOnlyMode.ValidationCore;
    }

    private static bool MightContainThreeOrFourByteSequence(ReadOnlySpan<byte> input)
    {
        var length = Math.Min(input.Length, 256);
        for (var i = 0; i < length; i++)
        {
            if (input[i] >= 0xE0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldUseUtf8IsValidFastPath(ReadOnlySpan<byte> input)
    {
        var length = Math.Min(input.Length, 256);
        var twoByteLeadCount = 0;
        var threeByteLeadCount = 0;
        for (var i = 0; i < length; i++)
        {
            var value = input[i];
            if (value is >= 0xC2 and < 0xE0)
            {
                twoByteLeadCount++;
                continue;
            }

            if (value is >= 0xE0 and < 0xF0)
            {
                threeByteLeadCount++;
            }
        }

        return twoByteLeadCount == 0 && threeByteLeadCount >= 8;
    }

    private static bool TryGetUtf16LengthFast(ReadOnlySpan<byte> input, int firstNonAscii, out Utf8ValidationResult validation)
    {
        try
        {
            var utf16Length = s_strictUtf8.GetCharCount(input);
            validation = new Utf8ValidationResult(
                input.Length,
                utf16Length,
                isAscii: false,
                containsSupplementaryScalars: input[firstNonAscii..].IndexOfAnyInRange((byte)0xF0, (byte)0xF4) >= 0);
            return true;
        }
        catch (DecoderFallbackException)
        {
            validation = default;
            return false;
        }
    }

    private static bool TryThrowIfInvalidOnlyFast(ReadOnlySpan<byte> input)
    {
        try
        {
            _ = s_strictUtf8.GetCharCount(input);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
