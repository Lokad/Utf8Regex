using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8Validation
{
    private static readonly Encoding s_strictEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static Utf8ValidationResult Validate(ReadOnlySpan<byte> input)
    {
        return Utf8InputAnalyzer.ValidateOnly(input);
    }

    public static void ThrowIfInvalidOnly(ReadOnlySpan<byte> input)
    {
        Utf8InputAnalyzer.ThrowIfInvalidOnly(input);
    }

    public static void ThrowIfInvalid(ReadOnlySpan<byte> input)
    {
        _ = Validate(input);
    }

    public static string DecodeStrict(ReadOnlySpan<byte> input)
    {
        try
        {
            return s_strictEncoding.GetString(input);
        }
        catch (DecoderFallbackException exception)
        {
            throw CreateInvalidUtf8Exception(Math.Max(0, exception.Index));
        }
    }

    internal static ArgumentException CreateInvalidUtf8Exception(int byteOffset)
        => new(
            $"The input must be well-formed UTF-8. Invalid data starts at byte offset {byteOffset}.",
            nameof(byteOffset));
}
