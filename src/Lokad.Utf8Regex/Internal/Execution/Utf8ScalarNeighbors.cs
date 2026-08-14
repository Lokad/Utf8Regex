using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Checked adjacent-scalar mechanics at a known UTF-8 boundary.</summary>
internal static class Utf8ScalarNeighbors // PCRE2-INTEGRATION-POINT
{
    public static bool TryGetPrevious(ReadOnlySpan<byte> input, int byteOffset, out Rune scalar)
    {
        ValidateOffset(input, byteOffset);
        if (byteOffset == 0)
        {
            scalar = default;
            return false;
        }

        if (Rune.DecodeLastFromUtf8(input[..byteOffset], out scalar, out _) != OperationStatus.Done)
        {
            throw new InvalidOperationException("The byte offset is not a valid UTF-8 scalar boundary.");
        }

        return true;
    }

    public static bool TryGetNext(ReadOnlySpan<byte> input, int byteOffset, out Rune scalar)
    {
        ValidateOffset(input, byteOffset);
        if (byteOffset == input.Length)
        {
            scalar = default;
            return false;
        }

        if (Rune.DecodeFromUtf8(input[byteOffset..], out scalar, out _) != OperationStatus.Done)
        {
            throw new InvalidOperationException("The byte offset is not a valid UTF-8 scalar boundary.");
        }

        return true;
    }

    private static void ValidateOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        if ((uint)byteOffset > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }
    }
}

internal static class DotNetUtf8WordBoundary
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        if (TryGetAsciiBoundary(input, byteOffset, out var asciiBoundary))
        {
            return asciiBoundary;
        }

        return IsUnicodeBoundary(input, byteOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetAsciiBoundary(ReadOnlySpan<byte> input, int byteOffset, out bool isBoundary)
    {
        if ((uint)byteOffset > (uint)input.Length)
        {
            ThrowOffsetOutOfRange();
        }

        var hasPrevious = byteOffset > 0;
        var hasNext = byteOffset < input.Length;
        var previous = hasPrevious ? input[byteOffset - 1] : (byte)0;
        var next = hasNext ? input[byteOffset] : (byte)0;
        if (hasPrevious && previous >= 0x80 || hasNext && next >= 0x80)
        {
            isBoundary = false;
            return false;
        }

        var previousIsWord = hasPrevious && Utf8AsciiBytePredicates.IsWord(previous);
        var nextIsWord = hasNext && Utf8AsciiBytePredicates.IsWord(next);
        isBoundary = previousIsWord != nextIsWord;
        return true;
    }

    private static bool IsUnicodeBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var previousIsWord = Utf8ScalarNeighbors.TryGetPrevious(input, byteOffset, out var previous) && IsWord(previous);
        var nextIsWord = Utf8ScalarNeighbors.TryGetNext(input, byteOffset, out var next) && IsWord(next);
        return previousIsWord != nextIsWord;
    }

    private static bool IsWord(Rune scalar) =>
        scalar.IsBmp && RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar((char)scalar.Value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOffsetOutOfRange() => throw new ArgumentOutOfRangeException("byteOffset");
}
