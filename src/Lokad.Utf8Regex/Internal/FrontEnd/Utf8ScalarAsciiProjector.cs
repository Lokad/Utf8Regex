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
        if (scalarClass is null || scalarClass.Data.Length < 3 || scalarClass.Data[2] != 0 ||
            !TryCreateToken(scalarClass, out var token) || token.Kind != AsciiSimplePatternTokenKind.CharClass) return false;
        charClass = token.CharClass;
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
