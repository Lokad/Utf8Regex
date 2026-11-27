using System.Buffers.Binary;
using System.Numerics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Derives native ASCII tokens from the canonical scalar class data.
/// The mask is an ASCII intersection, not a proof about non-ASCII subjects.</summary>
internal static class Utf8ScalarAsciiProjector
{
    // Fixed byte distances and folded literals require the entire class to be
    // a positive ASCII definition, rather than merely its ASCII intersection.
    internal static bool TryProjectPositiveAsciiDefinition(Utf8ScalarClass? scalarClass, out AsciiCharClass charClass)
    {
        charClass = default;
        if (scalarClass is null || scalarClass.Data.Length < 3) return false;
        var data = scalarClass.Data;
        Utf8ConstructionScope.ChargeWork(1);
        return data[0] == 0 && data[2] == 0 && 3L + data[1] == data.Length &&
            TryProjectRanges(data.AsSpan(3), 128, out charClass);
    }

    // A byte candidate must cover the complete lexical case fiber. Wider
    // equivalents (for example Kelvin sign for K) require the scalar lane.
    // Non-invariant preparation covers every supported culture behavior.
    internal static bool TryCloseAsciiCaseFiber(AsciiCharClass definition, bool folded,
        RegexOptions options, out AsciiCharClass charClass)
    {
        charClass = definition;
        if (!folded) return !definition.IsEmpty;
        if (!TryCloseAsciiCaseFiber(definition.ActualLowMask, definition.ActualHighMask,
            0, (options & RegexOptions.CultureInvariant) != 0 ? 0 : 2, out var low, out var high))
        {
            charClass = default;
            return false;
        }
        charClass = new AsciiCharClass(low, high);
        return true;
    }

    // Close the ASCII mask under the construction culture. The result tells
    // lowering whether it must still check wider case equivalents.
    internal static bool CloseAsciiCaseFiber(Span<byte> mask, Utf8ScalarCaseBehavior behavior)
    {
        var asciiOnly = TryCloseAsciiCaseFiber(BinaryPrimitives.ReadUInt64LittleEndian(mask),
            BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]), (int)behavior, (int)behavior, out var low, out var high);
        BinaryPrimitives.WriteUInt64LittleEndian(mask, low);
        BinaryPrimitives.WriteUInt64LittleEndian(mask[8..], high);
        return asciiOnly;
    }

    private static bool TryCloseAsciiCaseFiber(ulong definitionLow, ulong definitionHigh,
        int firstBehavior, int lastBehavior, out ulong low, out ulong high)
    {
        low = definitionLow;
        high = definitionHigh;
        var asciiOnly = true;
        Utf8ConstructionScope.ChargeWork(2);
        for (var word = 0; word < 2; word++)
            for (var bits = word == 0 ? definitionLow : definitionHigh; bits != 0; bits &= bits - 1)
            {
                Utf8ConstructionScope.ChargeWork(1);
                var value = (char)(word * 64 + BitOperations.TrailingZeroCount(bits));
                for (var behavior = firstBehavior; behavior <= lastBehavior; behavior++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents(value, (Utf8ScalarCaseBehavior)behavior))
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        if (equivalent < 64) low |= 1UL << equivalent;
                        else if (equivalent < 128) high |= 1UL << (equivalent - 64);
                        else asciiOnly = false;
                    }
                }
            }
        return asciiOnly && (low | high) != 0;
    }

    // Construction queries may inspect any owned class on the ASCII domain.
    // This does not qualify Unicode execution by the native byte interpreter.
    // All ranges, predicates and right-associated subtraction stay borrowed;
    // constant scratch replaces the former copied wire-segment buffers.
    internal static bool TryProjectAsciiQuery(Utf8ScalarClass? scalarClass, out AsciiCharClass charClass)
    {
        charClass = default;
        if (scalarClass is null || scalarClass.Data.Length == 0) return false;
        var data = scalarClass.Data.AsSpan();
        ulong unresolvedLow = ulong.MaxValue, unresolvedHigh = ulong.MaxValue;
        ulong acceptedLow = 0, acceptedHigh = 0;
        var position = 0;
        var depth = 0;
        while (position < data.Length)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (data.Length - position < 3) return false;
            var negated = data[position] != 0;
            var rangeCount = data[position + 1];
            var predicateCount = data[position + 2];
            position += 3;
            if (rangeCount < 0 || predicateCount < 0 ||
                (long)rangeCount + predicateCount > data.Length - position ||
                !TryProjectRanges(data.Slice(position, rangeCount), 0x110000, out var ranges) ||
                !TryProjectPredicates(data.Slice(position + rangeCount, predicateCount), out var predicates)) return false;
            position += rangeCount + predicateCount;
            var low = ranges.ActualLowMask | predicates.ActualLowMask;
            var high = ranges.ActualHighMask | predicates.ActualHighMask;
            if (negated) { low = ~low; high = ~high; }
            if ((depth++ & 1) != 0)
            {
                acceptedLow |= unresolvedLow & ~low;
                acceptedHigh |= unresolvedHigh & ~high;
            }
            unresolvedLow &= low;
            unresolvedHigh &= high;
        }
        if ((depth & 1) != 0) { acceptedLow |= unresolvedLow; acceptedHigh |= unresolvedHigh; }
        charClass = new AsciiCharClass(acceptedLow, acceptedHigh);
        return true;
    }

    private static bool TryProjectRanges(ReadOnlySpan<int> ranges, int maximumEnd, out AsciiCharClass charClass)
    {
        charClass = default;
        if ((ranges.Length & 1) != 0) return false;
        ulong low = 0, high = 0;
        for (var index = 0; index < ranges.Length; index += 2)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var start = ranges[index];
            var end = ranges[index + 1];
            if (start < 0 || start >= end || end > maximumEnd) return false;
            // Each charged range touches at most two words. A wider definition
            // contributes only its ASCII intersection to an ASCII query.
            if (start < 64) low |= RangeBits(start, Math.Min(end, 64));
            if (end > 64 && start < 128) high |= RangeBits(Math.Max(start - 64, 0), Math.Min(end - 64, 64));
        }
        charClass = new AsciiCharClass(low, high);
        return true;
    }

    // The caller supplies a nonempty half-open interval within one word.
    private static ulong RangeBits(int start, int end) =>
        (ulong.MaxValue << start) & (ulong.MaxValue >> (64 - end));

    private static bool TryProjectPredicates(ReadOnlySpan<int> predicates, out AsciiCharClass charClass)
    {
        charClass = default;
        ulong low = 0, high = 0;
        uint categoryMask = 0;
        for (var index = 0; index < predicates.Length; index++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var code = predicates[index];
            if (code is 100 or -100)
            {
                low |= code > 0 ? Utf8AsciiBytePredicates.WhitespaceMask : ~Utf8AsciiBytePredicates.WhitespaceMask;
                if (code < 0) high = ulong.MaxValue;
            }
            else if (code == Utf8ScalarClass.RangePredicateCode)
            {
                if (predicates.Length - index < 3) return false;
                var negated = predicates[++index] != 0;
                var count = predicates[++index];
                if (count < 0 || count > predicates.Length - index - 1 ||
                    !TryProjectRanges(predicates.Slice(index + 1, count), 0x110000, out var ranges)) return false;
                low |= negated ? ~ranges.ActualLowMask : ranges.ActualLowMask;
                high |= negated ? ~ranges.ActualHighMask : ranges.ActualHighMask;
                index += count;
            }
            else if (code == 0)
            {
                uint positive = 0, negative = 0;
                var start = index;
                while (++index < predicates.Length && predicates[index] != 0)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    var member = predicates[index];
                    if (member is < -30 or > 30) return false;
                    if (member > 0) positive |= 1u << (member - 1);
                    else negative |= 1u << (-member - 1);
                }
                if (index == predicates.Length || index == start + 1) return false;
                categoryMask |= positive == 0 ? ~negative : positive & ~negative;
            }
            else
            {
                if (code is < -30 or > 30) return false;
                categoryMask |= code > 0 ? 1u << (code - 1) : ~(1u << (-code - 1));
            }
        }
        if (categoryMask != 0)
        {
            for (var value = 0; value < 128; value++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if ((categoryMask & (1u << (int)char.GetUnicodeCategory((char)value))) == 0) continue;
                if (value < 64) low |= 1UL << value;
                else high |= 1UL << (value - 64);
            }
        }
        charClass = new AsciiCharClass(low, high);
        return true;
    }

    internal static bool TryCreateToken(Utf8ScalarClass? scalarClass, out AsciiPatternToken token)
    {
        token = default;
        if (scalarClass is null) return false;
        var data = scalarClass.Data.AsSpan();
        if (data.Length < 3) return false;
        var rangeCount = data[1];
        var categoryCount = data[2];
        // Subtraction and other category unions stay with the owned scalar VM.
        // This speculative native family keeps a bounded, explicit proof shape.
        if (rangeCount < 0 || (rangeCount & 1) != 0 || categoryCount < 0 ||
            (long)rangeCount + categoryCount + 3 != data.Length) return false;
        var ranges = data.Slice(3, rangeCount);
        var categories = data.Slice(3 + rangeCount, categoryCount);
        var negated = data[0] != 0;
        if (!negated && categories.IsEmpty && ranges.SequenceEqual([0, 0x110000]))
        {
            token = AsciiPatternToken.Dot;
            return true;
        }
        if (!TryProjectRanges(ranges, 128, out var rangeMask) ||
            !TryGetShorthand(categories, out var shorthand, out var categoryNegated)) return false;

        var effectiveNegation = negated != categoryNegated;
        // A pure double negation denotes its positive shorthand. Mixed ASCII
        // exclusions must remain in the mask, e.g. [^\D0] excludes zero.
        var collapse = ranges.IsEmpty && negated && categoryNegated;
        var whitespace = ranges.IsEmpty && shorthand == 2;
        var maskNegated = whitespace ? effectiveNegation : collapse ? false : negated;
        var (low, high) = shorthand switch
        {
            0 => (Utf8AsciiBytePredicates.DecimalMask, 0UL),
            1 => (Utf8AsciiBytePredicates.WordSearch.Mask0, Utf8AsciiBytePredicates.WordSearch.Mask1),
            2 => (Utf8AsciiBytePredicates.WhitespaceMask, 0UL),
            _ => (0UL, 0UL),
        };
        if (categoryNegated && !whitespace && !collapse)
        {
            low = ~low;
            high = ~high;
        }
        token = new AsciiPatternToken(new AsciiCharClass(low | rangeMask.ActualLowMask, high | rangeMask.ActualHighMask, maskNegated));
        return true;
    }

    private static bool TryGetShorthand(ReadOnlySpan<int> categories, out int shorthand, out bool negated)
    {
        shorthand = -1; negated = false;
        if (categories.IsEmpty) return true;
        Utf8ConstructionScope.ChargeWork((ulong)categories.Length);
        if (categories.Length == 1 && categories[0] is 9 or -9 or 100 or -100)
        {
            shorthand = Math.Abs(categories[0]) == 9 ? 0 : 2;
            negated = categories[0] < 0;
            return true;
        }
        if (categories.SequenceEqual([0, 2, 4, 5, 3, 1, 6, 9, 19, 0]))
        {
            shorthand = 1;
            return true;
        }
        if (categories.SequenceEqual([0, -2, -4, -5, -3, -1, -6, -9, -19, 0]))
        {
            shorthand = 1; negated = true;
            return true;
        }
        return false;
    }
}
