using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8UnicodeLetterBoundedRepeatExecutor
{
    private static readonly byte[] s_asciiCategoryMap = CreateAsciiCategoryMap();
    private static readonly SearchValues<byte> s_asciiMathSymbolSearchValues = SearchValues.Create("+<=>|~"u8);

    public static int CountLetters(ReadOnlySpan<byte> input, bool containsSupplementaryScalars)
    {
        var count = 0;
        var index = 0;
        while (index < input.Length)
        {
            var first = input[index];
            if (first < 0x80)
            {
                if ((uint)((first | 0x20) - (byte)'a') <= (byte)'z' - (byte)'a')
                {
                    count++;
                }

                index++;
                continue;
            }

            if ((first & 0xE0) == 0xC0)
            {
                var ch = (char)(((first & 0x1F) << 6) | (input[index + 1] & 0x3F));
                if (char.IsLetter(ch))
                {
                    count++;
                }

                index += 2;
                continue;
            }

            if ((first & 0xF0) == 0xE0)
            {
                var ch = (char)(((first & 0x0F) << 12) |
                                ((input[index + 1] & 0x3F) << 6) |
                                (input[index + 2] & 0x3F));
                if (char.IsLetter(ch))
                {
                    count++;
                }

                index += 3;
                continue;
            }

            if (!containsSupplementaryScalars)
            {
                index += 4;
                continue;
            }

            var scalar = ((first & 0x07) << 18) |
                         ((input[index + 1] & 0x3F) << 12) |
                         ((input[index + 2] & 0x3F) << 6) |
                         (input[index + 3] & 0x3F);
            if (Rune.IsLetter(new Rune(scalar)))
            {
                count++;
            }

            index += 4;
        }

        return count;
    }

    public static int CountLettersOrFallback(ReadOnlySpan<byte> input, int minCount, int maxCount, Regex fallbackRegex)
    {
        if (TryCountLetters(input, minCount, maxCount, out var count))
        {
            return count;
        }

        return fallbackRegex.Count(Encoding.UTF8.GetString(input));
    }

    private static bool TryCountLetters(ReadOnlySpan<byte> input, int minCount, int maxCount, out int count)
    {
        count = 0;
        var index = 0;
        while (index < input.Length)
        {
            if (!TryDecodeBmpChar(input, ref index, out var ch))
            {
                return false;
            }

            if (!char.IsLetter(ch))
            {
                continue;
            }

            var runLength = 1;
            while (index < input.Length)
            {
                var nextIndex = index;
                if (!TryDecodeBmpChar(input, ref nextIndex, out ch))
                {
                    return false;
                }

                if (!char.IsLetter(ch))
                {
                    break;
                }

                runLength++;
                index = nextIndex;
            }

            count += CountGreedyNonOverlappingMatches(runLength, minCount, maxCount);
        }

        return true;
    }

    public static int CountCategory(ReadOnlySpan<byte> input, UnicodeCategory category, bool containsSupplementaryScalars)
    {
        if (category == UnicodeCategory.MathSymbol)
        {
            return CountMathSymbols(input, containsSupplementaryScalars);
        }

        var count = 0;
        var index = 0;
        var categoryByte = (byte)category;
        while (index < input.Length)
        {
            var first = input[index];
            if (first < 0x80)
            {
                if (s_asciiCategoryMap[first] == categoryByte)
                {
                    count++;
                }

                index++;
                continue;
            }

            if ((first & 0xE0) == 0xC0)
            {
                var ch = (char)(((first & 0x1F) << 6) | (input[index + 1] & 0x3F));
                if (char.GetUnicodeCategory(ch) == category)
                {
                    count++;
                }

                index += 2;
                continue;
            }

            if ((first & 0xF0) == 0xE0)
            {
                var ch = (char)(((first & 0x0F) << 12) |
                                ((input[index + 1] & 0x3F) << 6) |
                                (input[index + 2] & 0x3F));
                if (char.GetUnicodeCategory(ch) == category)
                {
                    count++;
                }

                index += 3;
                continue;
            }

            if (!containsSupplementaryScalars)
            {
                index += 4;
                continue;
            }

            var scalar = ((first & 0x07) << 18) |
                         ((input[index + 1] & 0x3F) << 12) |
                         ((input[index + 2] & 0x3F) << 6) |
                         (input[index + 3] & 0x3F);
            if (Rune.GetUnicodeCategory(new Rune(scalar)) == category)
            {
                count++;
            }

            index += 4;
        }

        return count;
    }

    private static int CountMathSymbols(ReadOnlySpan<byte> input, bool containsSupplementaryScalars)
    {
        var count = 0;
        var index = 0;
        while (index < input.Length)
        {
            var nextNonAscii = input[index..].IndexOfAnyExceptInRange((byte)0x00, (byte)0x7F);
            var asciiEnd = nextNonAscii < 0 ? input.Length : index + nextNonAscii;
            count += CountAsciiMathSymbols(input[index..asciiEnd]);
            if (nextNonAscii < 0)
            {
                break;
            }

            index = asciiEnd;
            var first = input[index];
            if ((first & 0xE0) == 0xC0)
            {
                var ch = (char)(((first & 0x1F) << 6) | (input[index + 1] & 0x3F));
                if (char.GetUnicodeCategory(ch) == UnicodeCategory.MathSymbol)
                {
                    count++;
                }

                index += 2;
            }
            else if ((first & 0xF0) == 0xE0)
            {
                var ch = (char)(((first & 0x0F) << 12) |
                                ((input[index + 1] & 0x3F) << 6) |
                                (input[index + 2] & 0x3F));
                if (char.GetUnicodeCategory(ch) == UnicodeCategory.MathSymbol)
                {
                    count++;
                }

                index += 3;
            }
            else
            {
                if (containsSupplementaryScalars)
                {
                    var scalar = ((first & 0x07) << 18) |
                                 ((input[index + 1] & 0x3F) << 12) |
                                 ((input[index + 2] & 0x3F) << 6) |
                                 (input[index + 3] & 0x3F);
                    if (Rune.GetUnicodeCategory(new Rune(scalar)) == UnicodeCategory.MathSymbol)
                    {
                        count++;
                    }
                }

                index += 4;
            }
        }

        return count;
    }

    private static int CountAsciiMathSymbols(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var index = 0;
        if (Vector256.IsHardwareAccelerated && input.Length >= Vector256<byte>.Count)
        {
            count += CountAsciiMathSymbolsVector256(input, ref index);
        }
        else if (Vector128.IsHardwareAccelerated && input.Length >= Vector128<byte>.Count)
        {
            count += CountAsciiMathSymbolsVector128(input, ref index);
        }

        while (index < input.Length)
        {
            var relative = input[index..].IndexOfAny(s_asciiMathSymbolSearchValues);
            if (relative < 0)
            {
                break;
            }

            count++;
            index += relative + 1;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountAsciiMathSymbolsVector256(ReadOnlySpan<byte> input, ref int index)
    {
        var count = 0;
        var plus = Vector256.Create((byte)'+');
        var less = Vector256.Create((byte)'<');
        var equal = Vector256.Create((byte)'=');
        var greater = Vector256.Create((byte)'>');
        var pipe = Vector256.Create((byte)'|');
        var tilde = Vector256.Create((byte)'~');

        ref var inputRef = ref MemoryMarshal.GetReference(input);
        var lastVectorStart = input.Length - Vector256<byte>.Count;
        while (index <= lastVectorStart)
        {
            var current = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref inputRef, index));
            var matches = Vector256.Equals(current, plus);
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, less));
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, equal));
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, greater));
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, pipe));
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, tilde));
            count += BitOperations.PopCount(matches.ExtractMostSignificantBits());
            index += Vector256<byte>.Count;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountAsciiMathSymbolsVector128(ReadOnlySpan<byte> input, ref int index)
    {
        var count = 0;
        var plus = Vector128.Create((byte)'+');
        var less = Vector128.Create((byte)'<');
        var equal = Vector128.Create((byte)'=');
        var greater = Vector128.Create((byte)'>');
        var pipe = Vector128.Create((byte)'|');
        var tilde = Vector128.Create((byte)'~');

        ref var inputRef = ref MemoryMarshal.GetReference(input);
        var lastVectorStart = input.Length - Vector128<byte>.Count;
        while (index <= lastVectorStart)
        {
            var current = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref inputRef, index));
            var matches = Vector128.Equals(current, plus);
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, less));
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, equal));
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, greater));
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, pipe));
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, tilde));
            count += BitOperations.PopCount(matches.ExtractMostSignificantBits());
            index += Vector128<byte>.Count;
        }

        return count;
    }

    private static byte[] CreateAsciiCategoryMap()
    {
        var map = new byte[128];
        for (var i = 0; i < map.Length; i++)
        {
            map[i] = (byte)char.GetUnicodeCategory((char)i);
        }

        return map;
    }

    private static int CountGreedyNonOverlappingMatches(int runLength, int minCount, int maxCount)
    {
        var count = 0;
        while (runLength >= minCount)
        {
            var consumed = Math.Min(runLength, maxCount);
            count++;
            runLength -= consumed;
        }

        return count;
    }

    private static bool TryDecodeBmpChar(ReadOnlySpan<byte> input, ref int index, out char ch)
    {
        ch = '\0';
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        var first = input[index];
        if (first < 0x80)
        {
            ch = (char)first;
            index++;
            return true;
        }

        if ((first & 0xE0) == 0xC0)
        {
            if (index + 1 >= input.Length)
            {
                return false;
            }

            ch = (char)(((first & 0x1F) << 6) | (input[index + 1] & 0x3F));
            index += 2;
            return true;
        }

        if ((first & 0xF0) == 0xE0)
        {
            if (index + 2 >= input.Length)
            {
                return false;
            }

            ch = (char)(((first & 0x0F) << 12) |
                        ((input[index + 1] & 0x3F) << 6) |
                        (input[index + 2] & 0x3F));
            index += 3;
            return true;
        }

        return false;
    }
}
