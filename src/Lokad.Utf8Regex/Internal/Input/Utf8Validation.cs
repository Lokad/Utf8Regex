using System.Buffers;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Unicode;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8Validation
{
    public static void ThrowIfInvalidOnly(ReadOnlySpan<byte> input)
    {
        if (!TryValidateOnly(input, out var errorOffset))
            throw CreateInvalidUtf8Exception(errorOffset);
    }

    internal static bool TryValidateOnly(ReadOnlySpan<byte> input, out int errorOffset)
    {
        var firstNonAscii = input.IndexOfAnyExceptInRange((byte)0, (byte)0x7f);
        if (firstNonAscii < 0)
        {
            errorOffset = -1;
            return true;
        }

        var suffix = input[firstNonAscii..];
        var valid = Avx2.IsSupported || Vector512.IsHardwareAccelerated && Avx512BW.IsSupported
            ? Utf8ValidationCore.IsValidLookup(suffix)
            : Utf8.IsValid(suffix);
        errorOffset = valid ? -1 : FindFirstInvalidByte(input, firstNonAscii);
        return errorOffset < 0;
    }

    private static int FindFirstInvalidByte(ReadOnlySpan<byte> input, int offset)
    {
        while (offset < input.Length)
        {
            if (Rune.DecodeFromUtf8(input[offset..], out _, out var width) != OperationStatus.Done)
                return offset;
            offset += width;
        }
        return -1;
    }

    internal static ArgumentException CreateInvalidUtf8Exception(int byteOffset)
        => new(
            $"The input must be well-formed UTF-8. Invalid data starts at byte offset {byteOffset}.",
            nameof(byteOffset));
}
