using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8ValidationCore
{
    // The caller admits the existing mixed-width prepared anchor and512-bit
    // hardware. Prefix/tail validity, scalar fault offsets and complete literal
    // confirmation remain authoritative; no partial count escapes on failure.
    internal static int CountExactValidatedLookup512(ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal, int secondAnchor)
    {
        // ASCII before a non-ASCII literal's first possible start proves both
        // validity and absence. Reuse that proof rather than draining it twice.
        var firstCandidate = literal[0] >= 128 ? input.IndexOfAnyInRange((byte)128, byte.MaxValue) : 0;
        if (firstCandidate < 0) return 0;
        var offset = LookupStart(input, firstCandidate);
        if (offset < 0) ThrowLookupCountError(input);
        var count = 0;
        var next = 0;
        var lastStart = input.Length - literal.Length;
        // LookupStart validates a few complete scalars. Their possible starts
        // still precede the first vector, including a match at byte zero.
        for (var start = firstCandidate; start < offset && start <= lastStart; start++)
        {
            if (start >= next && input.Slice(start, literal.Length).SequenceEqual(literal))
            { count++; next = start + literal.Length; }
        }
        var previousHigh256 = Vector256.Create(LookupPreviousHigh, LookupPreviousHigh);
        var previousLow256 = Vector256.Create(LookupPreviousLow, LookupPreviousLow);
        var currentHigh256 = Vector256.Create(LookupCurrentHigh, LookupCurrentHigh);
        var high = Vector512.Create(previousHigh256, previousHigh256);
        var low = Vector512.Create(previousLow256, previousLow256);
        var currentHigh = Vector512.Create(currentHigh256, currentHigh256);
        var first = Vector512.Create(literal[0]);
        var second = Vector512.Create(literal[secondAnchor]);
        while (offset <= input.Length - Vector512<byte>.Count - secondAnchor)
        {
            // One admission covers the three validity context bytes, candidate
            // starts and their later anchor. All loads remain checked span reads.
            var window = input.Slice(offset - 3, Vector512<byte>.Count + 3 + secondAnchor);
            var current = MemoryMarshal.Read<Vector512<byte>>(window[3..]);
            if (!IsValidLookupBlock512(window, current, high, low, currentHigh))
                ThrowLookupCountError(input);
            var later = MemoryMarshal.Read<Vector512<byte>>(window[(3 + secondAnchor)..]);
            var candidates = Vector512.Equals(current, first).ExtractMostSignificantBits()
                & Vector512.Equals(later, second).ExtractMostSignificantBits();
            while (candidates != 0)
            {
                var start = offset + BitOperations.TrailingZeroCount(candidates);
                candidates &= candidates - 1;
                if (start >= next && start <= lastStart && input.Slice(start, literal.Length).SequenceEqual(literal))
                { count++; next = start + literal.Length; }
            }
            offset += Vector512<byte>.Count;
        }
        if (!LookupTail(input, offset)) ThrowLookupCountError(input);
        // The remaining bounded suffix uses the original checked full-sequence
        // primitive, after its validity is established. Preserve nonoverlap with
        // an earlier candidate whose confirmed literal extends into this suffix.
        next = Math.Max(next, offset);
        while (next <= lastStart)
        {
            var found = input[next..].IndexOf(literal);
            if (found < 0) break;
            count++;
            next += found + literal.Length;
        }
        return count;
    }

    [DoesNotReturn]
    private static void ThrowLookupCountError(ReadOnlySpan<byte> input)
    {
        // Reuse the exact first-error decoder on failure, as ordinary lookup
        // validity already does. The successful whole-input pass is not repeated.
        _ = TryValidate(input, out _, out var errorOffset);
        throw Utf8Validation.CreateInvalidUtf8Exception(errorOffset);
    }
}
