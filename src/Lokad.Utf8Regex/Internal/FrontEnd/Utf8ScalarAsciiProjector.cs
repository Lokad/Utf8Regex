using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Derives native ASCII tokens from the canonical scalar class data.
/// The mask is an ASCII intersection; its scalar kind qualifies any wider lane.</summary>
internal static class Utf8ScalarAsciiProjector
{
    // Native structural families run only within their qualified ASCII input
    // envelope. Keep their definition proof distinct from Unicode shorthands.
    internal static bool TryProjectAsciiDefinition(Utf8ScalarClass? scalarClass, out AsciiCharClass charClass)
    {
        charClass = default;
        if (scalarClass is null || scalarClass.Data.Length < 3 || scalarClass.Data[2] != 0) return false;
        if (TryCreateToken(scalarClass, out var token) && token.Kind == AsciiSimplePatternTokenKind.CharClass)
        {
            charClass = token.CharClass;
            return true;
        }
        // ASCII range subtraction is still a qualified native definition. Fold
        // its right-associated chain without an array of intermediate masks.
        var data = scalarClass.Data.AsSpan();
        ulong unresolvedLow = ulong.MaxValue, unresolvedHigh = ulong.MaxValue;
        ulong acceptedLow = 0, acceptedHigh = 0;
        var position = 0;
        var depth = 0;
        while (position < data.Length)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (data.Length - position < 3 || data[position + 2] != 0) return false;
            var negated = data[position] != 0;
            var count = data[position + 1];
            position += 3;
            if (count < 0 || (count & 1) != 0 || count > data.Length - position) return false;
            ulong low = 0, high = 0;
            for (var index = 0; index < count; index += 2)
            {
                Utf8ConstructionScope.ChargeWork(1);
                var start = data[position + index];
                var end = data[position + index + 1];
                if (start < 0 || end > 128 || start >= end) return false;
                for (var value = start; value < end; value++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    if (value < 64) low |= 1UL << value;
                    else high |= 1UL << (value - 64);
                }
            }
            position += count;
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

    internal static bool TryProjectAsciiIntersection(Utf8ScalarClass? scalarClass, out AsciiCharClass charClass)
    {
        if (TryCreateToken(scalarClass, out var token))
        {
            charClass = token.Kind == AsciiSimplePatternTokenKind.Dot
                ? new AsciiCharClass(ulong.MaxValue, ulong.MaxValue) : token.CharClass;
            return true;
        }
        return TryProjectAsciiDefinition(scalarClass, out charClass);
    }

    internal static bool IsPositiveWhitespace(Utf8ScalarClass? scalarClass) => scalarClass is not null &&
        (scalarClass.Data.AsSpan().SequenceEqual([0, 0, 1, 100]) ||
         scalarClass.Data.AsSpan().SequenceEqual([0, 4, 0, 9, 14, 32, 33]));

    // Fixed byte distances and folded literals require the entire class to be
    // a positive ASCII definition, rather than merely its ASCII intersection.
    internal static bool TryProjectPositiveAsciiDefinition(Utf8ScalarClass? scalarClass, out AsciiCharClass charClass)
    {
        charClass = default;
        if (scalarClass is null || scalarClass.Data.Length < 3) return false;
        var data = scalarClass.Data;
        Utf8ConstructionScope.ChargeWork(1);
        return data[0] == 0 && data[2] == 0 && 3L + data[1] == data.Length &&
            TryProjectAsciiDefinition(scalarClass, out charClass);
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
                !TryProjectRanges(data.Slice(position, rangeCount), out var ranges) ||
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

    private static bool TryProjectRanges(ReadOnlySpan<int> ranges, out AsciiCharClass charClass)
    {
        charClass = default;
        if ((ranges.Length & 1) != 0) return false;
        ulong low = 0, high = 0;
        for (var index = 0; index < ranges.Length; index += 2)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var start = ranges[index];
            var end = ranges[index + 1];
            if (start < 0 || start >= end || end > 0x110000) return false;
            for (var value = start; value < Math.Min(end, 128); value++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (value < 64) low |= 1UL << value;
                else high |= 1UL << (value - 64);
            }
        }
        charClass = new AsciiCharClass(low, high);
        return true;
    }

    private static bool TryProjectPredicates(ReadOnlySpan<int> predicates, out AsciiCharClass charClass)
    {
        charClass = default;
        ulong low = 0, high = 0;
        uint categoryMask = 0;
        const ulong whitespace = (1UL << 9) | (1UL << 10) | (1UL << 11) | (1UL << 12) | (1UL << 13) | (1UL << 32);
        for (var index = 0; index < predicates.Length; index++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var code = predicates[index];
            if (code is 100 or -100)
            {
                low |= code > 0 ? whitespace : ~whitespace;
                if (code < 0) high = ulong.MaxValue;
            }
            else if (code == Utf8ScalarClass.RangePredicateCode)
            {
                if (predicates.Length - index < 3) return false;
                var negated = predicates[++index] != 0;
                var count = predicates[++index];
                if (count < 0 || count > predicates.Length - index - 1 ||
                    !TryProjectRanges(predicates.Slice(index + 1, count), out var ranges)) return false;
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

    internal static bool TryProjectStructuralClass(Utf8ScalarClass? scalarClass, out AsciiCharClass charClass)
    {
        if (TryProjectAsciiDefinition(scalarClass, out charClass)) return true;
        if (scalarClass is null || scalarClass.Data.Length != 4 || scalarClass.Data[0] != 0 ||
            scalarClass.Data[1] != 0 || scalarClass.Data[2] != 1 ||
            scalarClass.Data[3] is not (100 or -100) || !TryCreateToken(scalarClass, out var token)) return false;
        charClass = token.CharClass;
        return true;
    }

    internal static bool TryCreateToken(Utf8ScalarClass? scalarClass, out AsciiSimplePatternToken token)
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
            token = AsciiSimplePatternToken.Dot;
            return true;
        }
        for (var index = 0; index < ranges.Length; index += 2)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (ranges[index] < 0 || ranges[index + 1] > 128 || ranges[index] >= ranges[index + 1]) return false;
        }
        if (!TryGetShorthand(categories, out var shorthand, out var categoryNegated)) return false;

        var effectiveNegation = negated != categoryNegated;
        // A pure double negation denotes its positive shorthand. Mixed ASCII
        // exclusions must remain in the mask, e.g. [^\D0] excludes zero.
        var collapse = ranges.IsEmpty && negated && categoryNegated;
        var whitespace = ranges.IsEmpty && shorthand == 2;
        var maskNegated = whitespace ? effectiveNegation : collapse ? false : negated;
        ulong low = 0, high = 0;
        for (var value = 0; value < 128; value++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var categoryHit = shorthand switch
            {
                0 => Utf8AsciiBytePredicates.IsDigit((byte)value),
                1 => Utf8AsciiBytePredicates.IsWord((byte)value),
                2 => Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value),
                _ => false,
            };
            var hit = whitespace || collapse ? categoryHit :
                shorthand >= 0 && (categoryHit != categoryNegated) || InRanges(value, ranges);
            if (hit)
            {
                if (value < 64) low |= 1UL << value;
                else high |= 1UL << (value - 64);
            }
        }
        var kind = shorthand < 0
            ? negated ? Utf8SimplePatternScalarClassKind.AsciiClosedNegation : Utf8SimplePatternScalarClassKind.None
            : effectiveNegation
                ? shorthand switch
                {
                    0 => Utf8SimplePatternScalarClassKind.NegatedDigit,
                    1 => Utf8SimplePatternScalarClassKind.NegatedWord,
                    _ => Utf8SimplePatternScalarClassKind.NegatedWhitespace,
                }
                : whitespace ? Utf8SimplePatternScalarClassKind.UnicodeWhitespace : Utf8SimplePatternScalarClassKind.None;
        token = new AsciiSimplePatternToken(new AsciiCharClass(low, high, maskNegated),
            requiresAsciiInput: shorthand >= 0 || negated, kind);
        return true;
    }

    private static bool InRanges(int value, ReadOnlySpan<int> ranges)
    {
        for (var index = 0; index < ranges.Length; index += 2)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (value >= ranges[index] && value < ranges[index + 1]) return true;
        }
        return false;
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
