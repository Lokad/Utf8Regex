using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>
/// The sole projection boundary from the owned character-class wire encoding
/// to the flavor-neutral ASCII byte-set carrier.
/// </summary>
internal static class DotNetAsciiCharClassProjector
{
    public static bool TryProjectWholeClass(string? runtimeSet, out AsciiCharClass byteSet)
    {
        if (string.IsNullOrEmpty(runtimeSet))
        {
            byteSet = AsciiCharClass.Empty;
            return false;
        }

        if (Utf8OwnedCharClass.IsSubtraction(runtimeSet))
        {
            byteSet = AsciiCharClass.Empty;
            return false;
        }

        if (TryProjectKnownAsciiWhitespace(runtimeSet, out byteSet))
        {
            return true;
        }

        if (!Utf8OwnedCharClass.IsAscii(runtimeSet))
        {
            byteSet = AsciiCharClass.Empty;
            return false;
        }

        byteSet = CreateFromRuntimeSet(runtimeSet);
        return true;
    }

    public static bool TryProjectAsciiIntersection(string? runtimeSet, out AsciiCharClass byteSet)
    {
        if (TryProjectWholeClass(runtimeSet, out byteSet))
        {
            return true;
        }

        if (runtimeSet is null ||
            Utf8OwnedCharClass.IsSubtraction(runtimeSet) ||
            !TryGetCategoryPayload(runtimeSet, out var categoryPayload))
        {
            byteSet = AsciiCharClass.Empty;
            return false;
        }

        if (!IsKnownProjectedCategory(categoryPayload))
        {
            byteSet = AsciiCharClass.Empty;
            return false;
        }

        ulong low = 0;
        ulong high = 0;
        for (var value = 0; value < 128; value++)
        {
            if (MatchesKnownProjectedCategory((char)value, categoryPayload))
            {
                Add(ref low, ref high, (byte)value);
            }
        }

        var setLength = runtimeSet[Utf8OwnedCharClass.SetLengthIndex];
        var setEnd = Utf8OwnedCharClass.SetStartIndex + setLength;
        for (var index = Utf8OwnedCharClass.SetStartIndex; index < setEnd; index += 2)
        {
            var range = CreateInclusiveRange(runtimeSet[index], runtimeSet[index + 1]);
            if (range is not { } asciiRange)
            {
                continue;
            }

            for (var value = asciiRange.Low; value <= asciiRange.High; value++)
            {
                Add(ref low, ref high, value);
            }
        }

        byteSet = new AsciiCharClass(low, high, Utf8OwnedCharClass.IsNegated(runtimeSet));
        return true;
    }

    public static bool RequiresAsciiInput(string? runtimeSet)
    {
        if (runtimeSet == Utf8OwnedCharClass.AnyClass ||
            (runtimeSet is not null && Utf8OwnedCharClass.IsNegated(runtimeSet)))
        {
            return true;
        }

        return runtimeSet is not null &&
            TryGetCategoryPayload(runtimeSet, out var categoryPayload) &&
            IsKnownProjectedCategory(categoryPayload);
    }

    private static AsciiCharClass CreateFromRuntimeSet(string runtimeSet)
    {
        ulong low = 0;
        ulong high = 0;
        for (var value = 0; value < 128; value++)
        {
            if (Internal.FrontEnd.Utf8OwnedCharClass.CharInClassBase((char)value, runtimeSet))
            {
                Add(ref low, ref high, (byte)value);
            }
        }

        return new AsciiCharClass(low, high, Utf8OwnedCharClass.IsNegated(runtimeSet));
    }

    private static bool TryProjectKnownAsciiWhitespace(string runtimeSet, out AsciiCharClass byteSet)
    {
        if (runtimeSet == Utf8OwnedCharClass.SpaceClass ||
            runtimeSet == Utf8OwnedCharClass.ECMASpaceClass)
        {
            byteSet = AsciiCharClass.FromPredicate(Utf8AsciiBytePredicates.IsSixByteWhitespace);
            return true;
        }

        if (runtimeSet == Utf8OwnedCharClass.NotSpaceClass ||
            runtimeSet == Utf8OwnedCharClass.NotECMASpaceClass)
        {
            byteSet = AsciiCharClass.FromPredicate(Utf8AsciiBytePredicates.IsSixByteWhitespace, negated: true);
            return true;
        }

        byteSet = AsciiCharClass.Empty;
        return false;
    }

    private static bool TryGetCategoryPayload(string runtimeSet, out string categoryPayload)
    {
        categoryPayload = string.Empty;
        if (runtimeSet.Length < Utf8OwnedCharClass.SetStartIndex)
        {
            return false;
        }

        var setLength = runtimeSet[Utf8OwnedCharClass.SetLengthIndex];
        var categoryLength = runtimeSet[Utf8OwnedCharClass.CategoryLengthIndex];
        if ((setLength & 1) != 0)
        {
            return false;
        }

        var setEnd = Utf8OwnedCharClass.SetStartIndex + setLength;
        if (runtimeSet.Length < setEnd + categoryLength)
        {
            return false;
        }

        for (var index = Utf8OwnedCharClass.SetStartIndex; index < setEnd; index += 2)
        {
            if (runtimeSet[index + 1] > 0x80)
            {
                return false;
            }
        }

        categoryPayload = categoryLength == 0
            ? string.Empty
            : runtimeSet.Substring(setEnd, categoryLength);
        return true;
    }

    private static bool IsKnownProjectedCategory(string categoryPayload) =>
        CategoryEquals(categoryPayload, Utf8OwnedCharClass.DigitClass) ||
        CategoryEquals(categoryPayload, Utf8OwnedCharClass.NotDigitClass) ||
        CategoryEquals(categoryPayload, Utf8OwnedCharClass.SpaceClass) ||
        CategoryEquals(categoryPayload, Utf8OwnedCharClass.NotSpaceClass) ||
        CategoryEquals(categoryPayload, Utf8OwnedCharClass.WordClass) ||
        CategoryEquals(categoryPayload, Utf8OwnedCharClass.NotWordClass);

    private static bool MatchesKnownProjectedCategory(char value, string categoryPayload)
    {
        if (CategoryEquals(categoryPayload, Utf8OwnedCharClass.DigitClass))
        {
            return Utf8AsciiBytePredicates.IsDigit((byte)value);
        }

        if (CategoryEquals(categoryPayload, Utf8OwnedCharClass.NotDigitClass))
        {
            return !Utf8AsciiBytePredicates.IsDigit((byte)value);
        }

        if (CategoryEquals(categoryPayload, Utf8OwnedCharClass.SpaceClass))
        {
            return Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value);
        }

        if (CategoryEquals(categoryPayload, Utf8OwnedCharClass.NotSpaceClass))
        {
            return !Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value);
        }

        if (CategoryEquals(categoryPayload, Utf8OwnedCharClass.WordClass))
        {
            return Utf8OwnedCharClass.IsBoundaryWordChar(value);
        }

        return !Utf8OwnedCharClass.IsBoundaryWordChar(value);
    }

    private static bool CategoryEquals(string categoryPayload, string runtimeSet) =>
        TryGetCategoryPayload(runtimeSet, out var expected) && categoryPayload == expected;

    private static Utf8InclusiveByteRange? CreateInclusiveRange(char start, char endExclusive)
    {
        if (start >= 0x80 || endExclusive == 0 || start >= endExclusive)
        {
            return null;
        }

        var high = Math.Min(endExclusive - 1, (char)0x7F);
        return Utf8InclusiveByteRange.Create((byte)start, (byte)high);
    }

    private static void Add(ref ulong low, ref ulong high, byte value)
    {
        if (value < 64)
        {
            low |= 1UL << value;
        }
        else
        {
            high |= 1UL << (value - 64);
        }
    }
}
