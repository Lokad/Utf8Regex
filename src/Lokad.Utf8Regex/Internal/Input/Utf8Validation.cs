using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8Validation
{
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

    internal static ArgumentException CreateInvalidUtf8Exception(int byteOffset)
        => new(
            $"The input must be well-formed UTF-8. Invalid data starts at byte offset {byteOffset}.",
            nameof(byteOffset));
}
