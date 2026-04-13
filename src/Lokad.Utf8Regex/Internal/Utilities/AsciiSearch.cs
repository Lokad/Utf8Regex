using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lokad.Utf8Regex.Internal.Utilities;

internal readonly record struct AsciiAnchorSelection(int Anchor2, int Anchor3)
{
    public bool HasThirdAnchor => Anchor3 > 0;
}

internal static class AsciiSearch
{
    public static bool TryGetDotNetLikeAsciiAnchorSelection(ReadOnlySpan<byte> literal, bool ignoreCase, out AsciiAnchorSelection selection)
    {
        selection = default;
        if (literal.Length <= 1 || literal.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
        {
            return false;
        }

        var ch2Offset = IndexOfAsciiByteWithLowestFrequency(literal, ignoreCase, excludeIndex: -1);
        if (ch2Offset < 0)
        {
            return false;
        }

        var ch3Offset = 0;
        if (literal.Length > 2)
        {
            ch3Offset = IndexOfAsciiByteWithLowestFrequency(literal, ignoreCase, ch2Offset);
            if (ch3Offset < 0)
            {
                ch3Offset = 0;
            }
        }

        if (ch3Offset > 0 && ch3Offset < ch2Offset)
        {
            (ch2Offset, ch3Offset) = (ch3Offset, ch2Offset);
        }

        selection = new AsciiAnchorSelection(ch2Offset, ch3Offset);
        return true;
    }

    public static float GetDotNetLikeAsciiFrequency(byte value)
    {
        return value switch
        {
            (byte)' ' => 8.952f,
            (byte)'0' => 1.199f,
            (byte)'1' => 0.870f,
            (byte)'2' => 0.729f,
            (byte)'3' => 0.491f,
            (byte)'4' => 0.335f,
            (byte)'5' => 0.269f,
            (byte)'6' => 0.435f,
            (byte)'7' => 0.240f,
            (byte)'8' => 0.234f,
            (byte)'9' => 0.196f,
            (byte)'a' => 4.596f,
            (byte)'b' => 1.296f,
            (byte)'c' => 2.081f,
            (byte)'d' => 2.005f,
            (byte)'e' => 6.903f,
            (byte)'f' => 1.494f,
            (byte)'g' => 1.019f,
            (byte)'h' => 1.024f,
            (byte)'i' => 3.750f,
            (byte)'j' => 0.286f,
            (byte)'k' => 0.439f,
            (byte)'l' => 2.913f,
            (byte)'m' => 1.459f,
            (byte)'n' => 3.908f,
            (byte)'o' => 3.230f,
            (byte)'p' => 1.444f,
            (byte)'q' => 0.231f,
            (byte)'r' => 4.220f,
            (byte)'s' => 3.924f,
            (byte)'t' => 5.312f,
            (byte)'u' => 2.112f,
            (byte)'v' => 0.737f,
            (byte)'w' => 0.573f,
            (byte)'x' => 0.992f,
            (byte)'y' => 1.067f,
            (byte)'z' => 0.181f,
            (byte)'_' => 0.797f,
            _ => 27f,
        };
    }

    public static AsciiExactLiteralSearchData CreateExactLiteralSearchData(byte[][] literals)
    {
        if (literals.Length == 0)
        {
            return new AsciiExactLiteralSearchData([], [], int.MaxValue);
        }

        var shortestLength = int.MaxValue;
        Span<byte> distinctFirstBytes = stackalloc byte[Math.Min(literals.Length, 256)];
        var distinctCount = 0;
        var bucketLists = new List<byte[]>[256];

        foreach (var literal in literals)
        {
            if (literal.Length == 0)
            {
                return new AsciiExactLiteralSearchData([], [new AsciiExactLiteralBucket(0, [[]])], 0);
            }

            if (literal.Length < shortestLength)
            {
                shortestLength = literal.Length;
            }

            var firstByte = literal[0];
            if (bucketLists[firstByte] is null)
            {
                bucketLists[firstByte] = [];
                distinctFirstBytes[distinctCount++] = firstByte;
            }

            bucketLists[firstByte]!.Add(literal);
        }

        var firstBytes = distinctFirstBytes[..distinctCount].ToArray();
        var buckets = new AsciiExactLiteralBucket[distinctCount];
        for (var i = 0; i < distinctCount; i++)
        {
            var firstByte = firstBytes[i];
            buckets[i] = new AsciiExactLiteralBucket(firstByte, [.. bucketLists[firstByte]!]);
        }

        return new AsciiExactLiteralSearchData(firstBytes, buckets, shortestLength);
    }

    public static int IndexOfExact(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        return literal.Length switch
        {
            0 => 0,
            1 => input.IndexOf(literal[0]),
            2 => IndexOfExact2(input, literal[0], literal[1]),
            _ => input.IndexOf(literal),
        };
    }

    public static int IndexOfIgnoreCase(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        return literal.Length switch
        {
            0 => 0,
            1 => IndexOfIgnoreCase1(input, literal[0]),
            _ => IndexOfIgnoreCaseMulti(input, literal),
        };
    }

    public static int LastIndexOfExact(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        return literal.Length switch
        {
            0 => input.Length,
            1 => input.LastIndexOf(literal[0]),
            2 => LastIndexOfExact2(input, literal[0], literal[1]),
            _ => input.LastIndexOf(literal),
        };
    }

    public static int LastIndexOfIgnoreCase(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        return literal.Length switch
        {
            0 => input.Length,
            1 => LastIndexOfIgnoreCase1(input, literal[0]),
            _ => LastIndexOfIgnoreCaseMulti(input, literal),
        };
    }

    public static int IndexOfAnyExact(ReadOnlySpan<byte> input, byte[][] literals)
    {
        return IndexOfAnyExact(input, CreateExactLiteralSearchData(literals));
    }

    public static int LastIndexOfAnyExact(ReadOnlySpan<byte> input, byte[][] literals)
    {
        return LastIndexOfAnyExact(input, CreateExactLiteralSearchData(literals));
    }

    public static int IndexOfAnyExact(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        return new PreparedLiteralSetSearch(searchData).IndexOf(input);
    }

    public static int LastIndexOfAnyExact(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        return new PreparedLiteralSetSearch(searchData).LastIndexOf(input);
    }

    public static bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, AsciiExactLiteralSearchData searchData, out int matchedLength)
    {
        return new PreparedLiteralSetSearch(searchData).TryGetMatchedLiteralLength(input, index, out matchedLength);
    }

    public static bool MatchesIgnoreCase(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> literal)
    {
        for (var i = 0; i < literal.Length; i++)
        {
            if (FoldCase(candidate[i]) != FoldCase(literal[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesFoldedIgnoreCase(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> foldedLiteral)
    {
        if (foldedLiteral.Length is >= 9 and <= 16)
        {
            return MatchesFoldedIgnoreCaseLength9To16(candidate, foldedLiteral);
        }

        var i = 0;
        while (i <= foldedLiteral.Length - sizeof(ulong))
        {
            var candidateBlock = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(candidate), i));
            var literalBlock = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(foldedLiteral), i));
            if (candidateBlock != literalBlock)
            {
                for (var j = 0; j < sizeof(ulong); j++)
                {
                    if (FoldCase(candidate[i + j]) != foldedLiteral[i + j])
                    {
                        return false;
                    }
                }
            }

            i += sizeof(ulong);
        }

        for (; i < foldedLiteral.Length; i++)
        {
            if (FoldCase(candidate[i]) != foldedLiteral[i])
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesFoldedIgnoreCaseLength9To16(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> foldedLiteral)
    {
        var secondReadOffset = foldedLiteral.Length - sizeof(ulong);
        return UInt64OrdinalIgnoreCaseAscii(
                   Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(candidate)),
                   Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(foldedLiteral))) &&
               UInt64OrdinalIgnoreCaseAscii(
                   Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(candidate), secondReadOffset)),
                   Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(foldedLiteral), secondReadOffset)));
    }

    public static byte FoldCase(byte value)
    {
        if ((uint)(value - 'A') <= 'Z' - 'A')
        {
            return (byte)(value + 32);
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UInt64OrdinalIgnoreCaseAscii(ulong valueA, ulong valueB)
    {
        if (((valueA | valueB) & ~0x7F7F_7F7F_7F7F_7F7Ful) != 0)
        {
            return false;
        }

        var letterMaskA = (((valueA + 0x3F3F_3F3F_3F3F_3F3Ful) ^ (valueA + 0x2525_2525_2525_2525ul)) & 0x8080_8080_8080_8080ul) >> 2;
        var letterMaskB = (((valueB + 0x3F3F_3F3F_3F3F_3F3Ful) ^ (valueB + 0x2525_2525_2525_2525ul)) & 0x8080_8080_8080_8080ul) >> 2;
        return (valueA | letterMaskA) == (valueB | letterMaskB);
    }
    private static int IndexOfExact2(ReadOnlySpan<byte> input, byte first, byte second)
    {
        var index = 0;
        while (index < input.Length - 1)
        {
            var relative = input[index..].IndexOf(first);
            if (relative < 0)
            {
                return -1;
            }

            index += relative;
            if (index + 1 < input.Length && input[index + 1] == second)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static int LastIndexOfExact2(ReadOnlySpan<byte> input, byte first, byte second)
    {
        var end = input.Length - 1;
        while (end > 0)
        {
            var relative = input[..end].LastIndexOf(first);
            if (relative < 0)
            {
                return -1;
            }

            if (relative + 1 < input.Length && input[relative + 1] == second)
            {
                return relative;
            }

            end = relative;
        }

        return -1;
    }

    private static int IndexOfIgnoreCase1(ReadOnlySpan<byte> input, byte value)
    {
        if (!TryGetCaseVariants(value, out var lower, out var upper))
        {
            return input.IndexOf(value);
        }

        return input.IndexOfAny(lower, upper);
    }

    private static int IndexOfIgnoreCaseMulti(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        if (literal.Length > input.Length)
        {
            return -1;
        }

        if (literal.Length is >= 9 and <= 10)
        {
            return IndexOfIgnoreCaseMultiFolded(input, literal);
        }

        if (!TryGetCaseVariants(literal[0], out var firstLower, out var firstUpper))
        {
            var index = 0;
            while (index <= input.Length - literal.Length)
            {
                var relative = input[index..].IndexOf(firstLower);
                if (relative < 0)
                {
                    return -1;
                }

                index += relative;
                if (MatchesIgnoreCaseTail(input, index, literal))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        var start = 0;
        while (start <= input.Length - literal.Length)
        {
            var relative = input[start..].IndexOfAny(firstLower, firstUpper);
            if (relative < 0)
            {
                return -1;
            }

            start += relative;
            if (MatchesIgnoreCaseTail(input, start, literal))
            {
                return start;
            }

            start++;
        }

        return -1;
    }

    private static int IndexOfIgnoreCaseMultiFolded(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        Span<byte> foldedLiteral = stackalloc byte[literal.Length];
        for (var i = 0; i < literal.Length; i++)
        {
            foldedLiteral[i] = FoldCase(literal[i]);
        }

        if (!TryGetCaseVariants(literal[0], out var firstLower, out var firstUpper))
        {
            var index = 0;
            while (index <= input.Length - literal.Length)
            {
                var relative = input[index..].IndexOf(firstLower);
                if (relative < 0)
                {
                    return -1;
                }

                index += relative;
                if (MatchesFoldedIgnoreCase(input.Slice(index, literal.Length), foldedLiteral))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        var start = 0;
        while (start <= input.Length - literal.Length)
        {
            var relative = input[start..].IndexOfAny(firstLower, firstUpper);
            if (relative < 0)
            {
                return -1;
            }

            start += relative;
            if (MatchesFoldedIgnoreCase(input.Slice(start, literal.Length), foldedLiteral))
            {
                return start;
            }

            start++;
        }

        return -1;
    }


    private static int LastIndexOfIgnoreCase1(ReadOnlySpan<byte> input, byte value)
    {
        if (!TryGetCaseVariants(value, out var lower, out var upper))
        {
            return input.LastIndexOf(value);
        }

        return input.LastIndexOfAny(lower, upper);
    }

    private static int LastIndexOfIgnoreCaseMulti(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        if (literal.Length > input.Length)
        {
            return -1;
        }

        if (!TryGetCaseVariants(literal[0], out var firstLower, out var firstUpper))
        {
            for (var end = input.Length - literal.Length + 1; end > 0;)
            {
                var relative = input[..end].LastIndexOf(firstLower);
                if (relative < 0)
                {
                    return -1;
                }

                if (MatchesIgnoreCaseTail(input, relative, literal))
                {
                    return relative;
                }

                end = relative;
            }

            return -1;
        }

        for (var end = input.Length - literal.Length + 1; end > 0;)
        {
            var relative = input[..end].LastIndexOfAny(firstLower, firstUpper);
            if (relative < 0)
            {
                return -1;
            }

            if (MatchesIgnoreCaseTail(input, relative, literal))
            {
                return relative;
            }

            end = relative;
        }

        return -1;
    }

    private static bool MatchesIgnoreCaseTail(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal)
    {
        if (index + literal.Length > input.Length)
        {
            return false;
        }

        for (var i = 1; i < literal.Length; i++)
        {
            if (FoldCase(input[index + i]) != FoldCase(literal[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryGetCaseVariants(byte value, out byte lower, out byte upper)
    {
        lower = FoldCase(value);
        if ((uint)(lower - 'a') > 'z' - 'a')
        {
            upper = value;
            return false;
        }

        upper = (byte)(lower - 32);
        return true;
    }

    private static int IndexOfAsciiByteWithLowestFrequency(ReadOnlySpan<byte> literal, bool ignoreCase, int excludeIndex)
    {
        var minFrequency = float.MaxValue;
        var minIndex = -1;
        for (var i = 1; i < literal.Length; i++)
        {
            if (i == excludeIndex)
            {
                continue;
            }

            var folded = FoldCase(literal[i]);
            var frequency = GetDotNetLikeAsciiFrequency(folded);
            if (ignoreCase && (uint)(folded - (byte)'a') <= (byte)('z' - 'a'))
            {
                frequency += GetDotNetLikeAsciiFrequency((byte)(folded - 32));
            }

            if (i <= 2)
            {
                frequency *= 1.5f;
            }

            if (frequency <= minFrequency)
            {
                minFrequency = frequency;
                minIndex = i;
            }
        }

        return minIndex;
    }

}

internal readonly struct AsciiExactLiteralSearchData
{
    public AsciiExactLiteralSearchData(byte[] firstBytes, AsciiExactLiteralBucket[] buckets, int shortestLength)
    {
        FirstBytes = firstBytes;
        FirstByteSearchValues = SearchValues.Create(firstBytes);
        Buckets = buckets;
        BucketIndexMap = CreateBucketIndexMap(buckets);
        ShortestLength = shortestLength;
    }

    public byte[] FirstBytes { get; }

    public SearchValues<byte> FirstByteSearchValues { get; }

    public AsciiExactLiteralBucket[] Buckets { get; }

    public int[] BucketIndexMap { get; }

    public int ShortestLength { get; }

    private static int[] CreateBucketIndexMap(AsciiExactLiteralBucket[] buckets)
    {
        var map = new int[256];
        Array.Fill(map, -1);

        for (var i = 0; i < buckets.Length; i++)
        {
            map[buckets[i].FirstByte] = i;
        }

        return map;
    }
}

internal readonly struct AsciiExactLiteralBucket
{
    public AsciiExactLiteralBucket(byte firstByte, byte[][] literals)
    {
        FirstByte = firstByte;
        Literals = literals;
        CommonPrefix = GetCommonPrefix(literals);
        SecondByteCandidates = GetDistinctSecondBytes(literals);
        SecondByteSearchValues = SecondByteCandidates is { Length: > 1 } secondBytes
            ? SearchValues.Create(secondBytes)
            : null;
        PrefixDiscriminator = CreatePrefixDiscriminator(literals, CommonPrefix.Length);
    }

    public byte FirstByte { get; }

    public byte[][] Literals { get; }

    public byte[] CommonPrefix { get; }

    public byte[]? SecondByteCandidates { get; }

    public SearchValues<byte>? SecondByteSearchValues { get; }

    public AsciiExactLiteralPrefixDiscriminator PrefixDiscriminator { get; }

    private static byte[] GetCommonPrefix(byte[][] literals)
    {
        if (literals.Length == 0)
        {
            return [];
        }

        var prefixLength = literals[0].Length;
        for (var i = 1; i < literals.Length && prefixLength > 1; i++)
        {
            prefixLength = CommonPrefixLength(literals[0], literals[i], prefixLength);
        }

        return prefixLength <= 1 ? [] : literals[0][..prefixLength];
    }

    private static int CommonPrefixLength(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, int maxLength)
    {
        var length = Math.Min(Math.Min(left.Length, right.Length), maxLength);
        var i = 0;
        while (i < length && left[i] == right[i])
        {
            i++;
        }

        return i;
    }

    private static byte[]? GetDistinctSecondBytes(byte[][] literals)
    {
        if (literals.Length <= 1)
        {
            return null;
        }

        Span<byte> secondBytes = stackalloc byte[Math.Min(literals.Length, 256)];
        var count = 0;
        foreach (var literal in literals)
        {
            if (literal.Length <= 1)
            {
                return null;
            }

            var secondByte = literal[1];
            var seen = false;
            for (var i = 0; i < count; i++)
            {
                if (secondBytes[i] == secondByte)
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                secondBytes[count++] = secondByte;
            }
        }

        return count <= 1 ? null : secondBytes[..count].ToArray();
    }

    private static AsciiExactLiteralPrefixDiscriminator CreatePrefixDiscriminator(byte[][] literals, int prefixLength)
    {
        if (literals.Length <= 1)
        {
            return default;
        }

        var shortestLength = literals.Min(static literal => literal.Length);
        if (prefixLength <= 0 || prefixLength >= shortestLength)
        {
            return default;
        }

        Span<bool> seen = stackalloc bool[256];
        var map = new byte[256][];
        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            var value = literal[prefixLength];
            if (seen[value])
            {
                return default;
            }

            seen[value] = true;
            map[value] = literal;
        }

        return new AsciiExactLiteralPrefixDiscriminator(prefixLength, map);
    }
}

internal readonly struct AsciiExactLiteralPrefixDiscriminator
{
    public AsciiExactLiteralPrefixDiscriminator(int offset, byte[][] literalsByByte)
    {
        Offset = offset;
        LiteralsByByte = literalsByByte;
    }

    public int Offset { get; }

    public byte[][]? LiteralsByByte { get; }

    public bool HasValue => LiteralsByByte is not null;
}
