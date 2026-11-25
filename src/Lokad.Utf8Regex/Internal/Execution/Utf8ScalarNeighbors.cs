using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Utf8Regex.Internal.Planning;

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

internal static class Utf8ScalarWordBoundary
{
    /// <summary>
    /// Applies a planned boundary requirement using the shared Unicode-scalar word-boundary policy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool MatchesRequirement(
        Utf8BoundaryRequirement requirement,
        ReadOnlySpan<byte> input,
        int byteOffset)
    {
        return requirement switch
        {
            Utf8BoundaryRequirement.None => true,
            Utf8BoundaryRequirement.Boundary => IsBoundary(input, byteOffset),
            Utf8BoundaryRequirement.NonBoundary => !IsBoundary(input, byteOffset),
            _ => false,
        };
    }

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
    // PCRE2-INTEGRATION-POINT: only the common ASCII membership, not Unicode policy.
    public static bool TryGetAsciiBoundary(ReadOnlySpan<byte> input, int byteOffset, out bool isBoundary)
    {
        if ((uint)byteOffset > (uint)input.Length)
        {
            ThrowOffsetOutOfRange();
        }

        // NUL is ASCII and non-word, just like an absent neighbor.
        var previous = byteOffset > 0 ? input[byteOffset - 1] : (byte)0;
        var next = byteOffset < input.Length ? input[byteOffset] : (byte)0;
        if ((previous | next) >= 0x80)
        {
            isBoundary = false;
            return false;
        }

        isBoundary = Utf8AsciiBytePredicates.IsWord(previous) != Utf8AsciiBytePredicates.IsWord(next);
        return true;
    }

    private static bool IsUnicodeBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var previousIsWord = Utf8ScalarNeighbors.TryGetPrevious(input, byteOffset, out var previous) && IsWord(previous);
        var nextIsWord = Utf8ScalarNeighbors.TryGetNext(input, byteOffset, out var next) && IsWord(next);
        return previousIsWord != nextIsWord;
    }

    private static bool IsWord(Rune scalar) =>
        Utf8SliceScalarClasses.IsWord(scalar, includeJoiners: true, ecma: false);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOffsetOutOfRange() => throw new ArgumentOutOfRangeException("byteOffset");
}
