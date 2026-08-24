using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiUriTokenExecutor
{
    // For bytes below 64, bit n says whether byte n belongs to the corresponding
    // ASCII character class. Bytes 64--127 are accepted by all four negated classes.
    private const ulong BodyStartLowerAsciiMask = 0x7FFF7FF6FFFFC1FF;
    private const ulong BodyContinuationLowerAsciiMask = 0x7FFFFFF6FFFFC1FF;
    private const ulong QueryLowerAsciiMask = 0xFFFFFFF6FFFFC1FF;
    private const ulong FragmentLowerAsciiMask = 0xFFFFFFFEFFFFC1FF;

    public static bool TryMatchWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        var delimiterIndex = input.IndexOf("://"u8);
        return delimiterIndex >= 0 &&
            TryMatchAtDelimiter(input, 0, delimiterIndex, out _, out matchedLength) &&
            matchedLength == input.Length;
    }

    public static int CountAsciiUriTokens(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindAsciiUriToken(input, startIndex, out var matchIndex, out var matchedLength))
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public static Utf8SelectedCountKernelMetrics InspectMetrics(ReadOnlySpan<byte> input)
    {
        var candidates = 0;
        var matches = 0;
        var startIndex = 0;
        while ((uint)startIndex < (uint)input.Length)
        {
            var searchFrom = startIndex;
            var foundMatch = false;
            while ((uint)searchFrom < (uint)input.Length)
            {
                var relative = input[searchFrom..].IndexOf((byte)':');
                if (relative < 0)
                {
                    break;
                }

                var delimiterIndex = searchFrom + relative;
                searchFrom = delimiterIndex + 1;
                if (input.Length - delimiterIndex < 3 ||
                    input[delimiterIndex + 1] != (byte)'/' ||
                    input[delimiterIndex + 2] != (byte)'/')
                {
                    continue;
                }

                candidates++;
                searchFrom += 2;
                if (!TryMatchAtDelimiter(input, startIndex, delimiterIndex, out var matchIndex, out var matchedLength))
                {
                    continue;
                }

                matches++;
                startIndex = matchIndex + Math.Max(matchedLength, 1);
                foundMatch = true;
                break;
            }

            if (!foundMatch)
            {
                break;
            }
        }

        return new Utf8SelectedCountKernelMetrics(
            "FallbackDirect/AsciiUriToken",
            ":// delimiter probes",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
    }

    public static bool TryFindAsciiUriToken(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        var searchFrom = startIndex;
        while (searchFrom < input.Length)
        {
            var relative = input[searchFrom..].IndexOf((byte)':');
            if (relative < 0)
            {
                return false;
            }

            var delimiterIndex = searchFrom + relative;
            searchFrom = delimiterIndex + 1;
            if (input.Length - delimiterIndex < 3 ||
                input[delimiterIndex + 1] != (byte)'/' ||
                input[delimiterIndex + 2] != (byte)'/')
            {
                continue;
            }

            searchFrom += 2;
            var bodyStart = delimiterIndex + 3;
            if (input.Length - bodyStart < 2 ||
                !IsAsciiUriBodyStart(input[bodyStart]) ||
                input[bodyStart + 1] == (byte)' ' ||
                !IsAsciiUriBodyContinuation(input[bodyStart + 1]) ||
                !TryMatchAfterRequiredBodyPair(
                    input,
                    startIndex,
                    delimiterIndex,
                    bodyStart + 2,
                    out matchIndex,
                    out matchedLength))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryMatchAtDelimiter(ReadOnlySpan<byte> input, int minStartIndex, int delimiterIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        var index = delimiterIndex + 3;
        if (input.Length - index < 2 ||
            !IsAsciiUriBodyStart(input[index]) ||
            !IsAsciiUriBodyContinuation(input[index + 1]))
        {
            return false;
        }

        return TryMatchAfterRequiredBodyPair(
            input,
            minStartIndex,
            delimiterIndex,
            index + 2,
            out matchIndex,
            out matchedLength);
    }

    private static bool TryMatchAfterRequiredBodyPair(
        ReadOnlySpan<byte> input,
        int minStartIndex,
        int delimiterIndex,
        int index,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        var schemeStart = delimiterIndex;
        while (schemeStart > minStartIndex && IsAsciiWordChar(input[schemeStart - 1]))
        {
            schemeStart--;
        }

        if (schemeStart == delimiterIndex)
        {
            return false;
        }

        if (schemeStart > 0 && input[schemeStart - 1] >= 0x80)
        {
            return false;
        }

        if (Vector256.IsHardwareAccelerated)
        {
            var questionMark = Vector256.Create((byte)'?');
            var hashMark = Vector256.Create((byte)'#');
            var space = Vector256.Create((byte)' ');
            while (input.Length - index >= Vector256<byte>.Count)
            {
                var values = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(input[index..]));
                var stopMask = values.ExtractMostSignificantBits() |
                    Vector256.Equals(values, questionMark).ExtractMostSignificantBits() |
                    Vector256.Equals(values, hashMark).ExtractMostSignificantBits() |
                    Vector256.LessThanOrEqual(values, space).ExtractMostSignificantBits();
                if (stopMask != 0)
                {
                    // The vector test also stops on ASCII controls below space
                    // that the regex accepts. The scalar loop resumes at that
                    // byte and distinguishes those uncommon values exactly.
                    index += BitOperations.TrailingZeroCount(stopMask);
                    break;
                }

                index += Vector256<byte>.Count;
            }
        }

        while ((uint)index < (uint)input.Length && IsAsciiUriBodyContinuation(input[index]))
        {
            index++;
        }

        if ((uint)index < (uint)input.Length)
        {
            if (input[index] == (byte)'?')
            {
                index++;
                while ((uint)index < (uint)input.Length && IsAsciiUriQueryByte(input[index]))
                {
                    index++;
                }
            }

            if ((uint)index < (uint)input.Length && input[index] == (byte)'#')
            {
                index++;
                while ((uint)index < (uint)input.Length && IsAsciiUriFragmentByte(input[index]))
                {
                    index++;
                }
            }
        }

        matchIndex = schemeStart;
        matchedLength = index - schemeStart;
        return true;
    }

    private static bool IsAsciiWordChar(byte value)
    {
        return Utf8AsciiBytePredicates.IsWord(value);
    }

    private static bool IsAsciiUriBodyStart(byte value)
    {
        return IsInAsciiClass(value, BodyStartLowerAsciiMask);
    }

    private static bool IsAsciiUriBodyContinuation(byte value)
    {
        return IsInAsciiClass(value, BodyContinuationLowerAsciiMask);
    }

    private static bool IsAsciiUriQueryByte(byte value)
    {
        return IsInAsciiClass(value, QueryLowerAsciiMask);
    }

    private static bool IsAsciiUriFragmentByte(byte value)
    {
        return IsInAsciiClass(value, FragmentLowerAsciiMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInAsciiClass(byte value, ulong lowerAsciiMask)
    {
        return value < 64
            ? ((lowerAsciiMask >> value) & 1) != 0
            : value < 0x80;
    }
}
