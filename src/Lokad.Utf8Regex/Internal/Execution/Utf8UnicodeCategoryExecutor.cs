using Lokad.Utf8Regex.Internal.Search;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

// PCRE2-INTEGRATION-POINT: flavor-neutral category counting over caller-valid UTF-8.
internal static class Utf8UnicodeCategoryExecutor
{
    private static readonly PreparedByteSetState s_asciiMathSymbols = PreparedByteSetState.FromValues("+<=>|~"u8);

    // The caller proves a complete one-scalar language whose ASCII mask and
    // Unicode category table cover the predicate. Keep scalar boundaries and
    // charge the original invocation before each bounded block.
    internal static Utf8ExecutionLimitKind TryCount(ReadOnlySpan<byte> input,
        uint categories, ulong asciiLow, ulong asciiHigh,
        ref Utf8SliceInvocation invocation, out int count)
    {
        count = 0;
        var admission = invocation.TryReserveWorking(0);
        if (admission != Utf8ExecutionLimitKind.None) return admission;
        var mathSymbols = categories == 1u << (int)UnicodeCategory.MathSymbol &&
            asciiLow == s_asciiMathSymbols.Mask0 && asciiHigh == s_asciiMathSymbols.Mask1;

        for (var offset = 0; offset < input.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var length = Utf8SliceBufferWork.Utf8BlockLength(input[offset..]);
            var block = input.Slice(offset, length);
            count += mathSymbols ? CountMathSymbols(block) : CountCategories(block, categories, asciiLow, asciiHigh);
            offset += length;
        }
        var publication = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return publication;
    }

    private static int CountCategories(ReadOnlySpan<byte> input, uint categories, ulong asciiLow, ulong asciiHigh)
    {
        var count = 0;
        var index = 0;
        while (index < input.Length)
        {
            var value = input[index];
            if (value < 128)
            {
                var mask = value < 64 ? asciiLow : asciiHigh;
                count += (int)((mask >> (value & 63)) & 1UL);
                ++index;
            }
            else
            {
                var category = ReadCategory(input, ref index);
                count += (int)((categories >> (int)category) & 1u);
            }
        }
        return count;
    }

    private static int CountMathSymbols(ReadOnlySpan<byte> input)
    {
        // UTF-8 lead/continuation bytes are >=128 and cannot equal an ASCII
        // symbol. Count all such bytes once, instead of resetting the SIMD
        // counter and tail search for every intervening ASCII fragment.
        var count = CountAsciiMathSymbols(input);
        var index = 0;
        while (index < input.Length)
        {
            var nextNonAscii = input[index..].IndexOfAnyExceptInRange((byte)0x00, (byte)0x7F);
            if (nextNonAscii < 0)
            {
                break;
            }

            index += nextNonAscii;
            if (ReadCategory(input, ref index) == UnicodeCategory.MathSymbol) ++count;
        }
        return count;
    }

    private static UnicodeCategory ReadCategory(ReadOnlySpan<byte> input, ref int index)
    {
        // The subject is caller-valid and the block ends at a scalar boundary.
        _ = Rune.DecodeFromUtf8(input[index..], out var scalar, out var width);
        index += width;
        return Rune.GetUnicodeCategory(scalar);
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
            var relative = s_asciiMathSymbols.IndexOf(input[index..]);
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

        var vectors = MemoryMarshal.Cast<byte, Vector256<byte>>(input[index..]);
        for (var block = 0; block < vectors.Length; block++)
        {
            var current = vectors[block];
            var matches = Vector256.Equals(current, plus);
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, less));
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, equal));
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, greater));
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, pipe));
            matches = Vector256.BitwiseOr(matches, Vector256.Equals(current, tilde));
            count += BitOperations.PopCount(matches.ExtractMostSignificantBits());
        }

        index += vectors.Length * Vector256<byte>.Count;

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

        var vectors = MemoryMarshal.Cast<byte, Vector128<byte>>(input[index..]);
        for (var block = 0; block < vectors.Length; block++)
        {
            var current = vectors[block];
            var matches = Vector128.Equals(current, plus);
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, less));
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, equal));
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, greater));
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, pipe));
            matches = Vector128.BitwiseOr(matches, Vector128.Equals(current, tilde));
            count += BitOperations.PopCount(matches.ExtractMostSignificantBits());
        }

        index += vectors.Length * Vector128<byte>.Count;

        return count;
    }
}
