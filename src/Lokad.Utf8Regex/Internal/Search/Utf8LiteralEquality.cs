using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lokad.Utf8Regex.Internal.Search;

internal enum Utf8LiteralComparisonKind : byte
{
    Scalar = 0,
    FastShort = 1,
}

internal static class Utf8LiteralEquality
{
    /// <summary>
    /// Bounds-checked exact comparison at a byte offset. Complexity is O(m) in
    /// the literal length; literals through sixteen bytes use at most two loads.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAt(
        ReadOnlySpan<byte> input,
        int index,
        ReadOnlySpan<byte> literal,
        Utf8LiteralComparisonKind comparisonKind)
    {
        if ((uint)index > (uint)(input.Length - literal.Length))
        {
            return false;
        }

        var candidate = input.Slice(index, literal.Length);
        return comparisonKind == Utf8LiteralComparisonKind.FastShort
            ? EqualsFastShort(candidate, literal)
            : candidate.SequenceEqual(literal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAtKnownPrefix(
        ReadOnlySpan<byte> input,
        int index,
        ReadOnlySpan<byte> literal,
        int knownPrefixLength,
        Utf8LiteralComparisonKind comparisonKind)
    {
        if ((uint)knownPrefixLength > (uint)literal.Length ||
            (uint)index > (uint)(input.Length - literal.Length))
        {
            return false;
        }

        return EqualsAt(
            input,
            index + knownPrefixLength,
            literal[knownPrefixLength..],
            comparisonKind);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualsFastShort(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> literal)
    {
        ref var candidateRef = ref MemoryMarshal.GetReference(candidate);
        ref var literalRef = ref MemoryMarshal.GetReference(literal);
        switch (literal.Length)
        {
            case 4:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) ==
                    Unsafe.ReadUnaligned<uint>(ref literalRef);
            case 5:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                    Unsafe.Add(ref candidateRef, 4) == Unsafe.Add(ref literalRef, 4);
            case 6:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                    Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref candidateRef, 4)) ==
                    Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref literalRef, 4));
            case 7:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                    Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref candidateRef, 4)) ==
                    Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref literalRef, 4)) &&
                    Unsafe.Add(ref candidateRef, 6) == Unsafe.Add(ref literalRef, 6);
            case 8:
                return Unsafe.ReadUnaligned<ulong>(ref candidateRef) ==
                    Unsafe.ReadUnaligned<ulong>(ref literalRef);
            case > 8 and <= 16:
                if (Unsafe.ReadUnaligned<ulong>(ref candidateRef) !=
                    Unsafe.ReadUnaligned<ulong>(ref literalRef))
                {
                    return false;
                }

                var tailOffset = literal.Length - sizeof(ulong);
                return Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref candidateRef, tailOffset)) ==
                    Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref literalRef, tailOffset));
            default:
                return candidate.SequenceEqual(literal);
        }
    }
}
