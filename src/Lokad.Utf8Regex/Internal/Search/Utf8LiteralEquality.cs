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
        // Widen the end calculation: negative offsets and longer literals decline,
        // and matching Slice's end guard lets the JIT remove its redundant check.
        if ((ulong)(uint)index + (uint)literal.Length > (uint)input.Length)
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
            (ulong)(uint)index + (uint)literal.Length > (uint)input.Length)
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
        // Both complete spans are admitted by EqualsAt. Keep the existing
        // primitive reads and overlapping tail, using checked span operations.
        switch (literal.Length)
        {
            case 4:
                return MemoryMarshal.Read<uint>(candidate) ==
                    MemoryMarshal.Read<uint>(literal);
            case 5:
                return MemoryMarshal.Read<uint>(candidate) == MemoryMarshal.Read<uint>(literal) &&
                    candidate[4] == literal[4];
            case 6:
                return MemoryMarshal.Read<uint>(candidate) == MemoryMarshal.Read<uint>(literal) &&
                    MemoryMarshal.Read<ushort>(candidate.Slice(4, sizeof(ushort))) ==
                    MemoryMarshal.Read<ushort>(literal.Slice(4, sizeof(ushort)));
            case 7:
                return MemoryMarshal.Read<uint>(candidate) == MemoryMarshal.Read<uint>(literal) &&
                    MemoryMarshal.Read<ushort>(candidate.Slice(4, sizeof(ushort))) ==
                    MemoryMarshal.Read<ushort>(literal.Slice(4, sizeof(ushort))) &&
                    candidate[6] == literal[6];
            case 8:
                return MemoryMarshal.Read<ulong>(candidate) ==
                    MemoryMarshal.Read<ulong>(literal);
            case > 8 and <= 16:
                if (MemoryMarshal.Read<ulong>(candidate) !=
                    MemoryMarshal.Read<ulong>(literal))
                {
                    return false;
                }

                var tailOffset = literal.Length - sizeof(ulong);
                return MemoryMarshal.Read<ulong>(candidate.Slice(tailOffset, sizeof(ulong))) ==
                    MemoryMarshal.Read<ulong>(literal.Slice(tailOffset, sizeof(ulong)));
            default:
                return candidate.SequenceEqual(literal);
        }
    }
}
