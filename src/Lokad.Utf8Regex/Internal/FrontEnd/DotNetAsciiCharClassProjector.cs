using Lokad.Utf8Regex.Internal.Execution;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>
/// The sole projection boundary from the vendored .NET RegexCharClass encoding
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

        if (TryProjectKnownWhitespace(runtimeSet, out byteSet))
        {
            return true;
        }

        if (!RuntimeFrontEnd.RegexCharClass.IsAscii(runtimeSet))
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

        if (runtimeSet is null || !TryGetCategoryPayload(runtimeSet, out var categoryPayload))
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

        var setLength = runtimeSet[RuntimeFrontEnd.RegexCharClass.SetLengthIndex];
        var setEnd = RuntimeFrontEnd.RegexCharClass.SetStartIndex + setLength;
        for (var index = RuntimeFrontEnd.RegexCharClass.SetStartIndex; index < setEnd; index += 2)
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

        byteSet = new AsciiCharClass(low, high, RuntimeFrontEnd.RegexCharClass.IsNegated(runtimeSet));
        return true;
    }

    private static AsciiCharClass CreateFromRuntimeSet(string runtimeSet)
    {
        ulong low = 0;
        ulong high = 0;
        for (var value = 0; value < 128; value++)
        {
            if (RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)value, runtimeSet))
            {
                Add(ref low, ref high, (byte)value);
            }
        }

        return new AsciiCharClass(low, high, RuntimeFrontEnd.RegexCharClass.IsNegated(runtimeSet));
    }

    private static bool TryProjectKnownWhitespace(string runtimeSet, out AsciiCharClass byteSet)
    {
        switch (runtimeSet)
        {
            case RuntimeFrontEnd.RegexCharClass.SpaceClass:
            case RuntimeFrontEnd.RegexCharClass.ECMASpaceClass:
                byteSet = AsciiCharClass.FromPredicate(Utf8AsciiBytePredicates.IsSixByteWhitespace);
                return true;

            case RuntimeFrontEnd.RegexCharClass.NotSpaceClass:
            case RuntimeFrontEnd.RegexCharClass.NotECMASpaceClass:
                byteSet = AsciiCharClass.FromPredicate(Utf8AsciiBytePredicates.IsSixByteWhitespace, negated: true);
                return true;

            default:
                byteSet = AsciiCharClass.Empty;
                return false;
        }
    }

    private static bool TryGetCategoryPayload(string runtimeSet, out string categoryPayload)
    {
        categoryPayload = string.Empty;
        if (runtimeSet.Length < RuntimeFrontEnd.RegexCharClass.SetStartIndex)
        {
            return false;
        }

        var setLength = runtimeSet[RuntimeFrontEnd.RegexCharClass.SetLengthIndex];
        var categoryLength = runtimeSet[RuntimeFrontEnd.RegexCharClass.CategoryLengthIndex];
        var setEnd = RuntimeFrontEnd.RegexCharClass.SetStartIndex + setLength;
        if (runtimeSet.Length < setEnd + categoryLength)
        {
            return false;
        }

        for (var index = RuntimeFrontEnd.RegexCharClass.SetStartIndex; index < setEnd; index += 2)
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
        CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.DigitClass) ||
        CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.NotDigitClass) ||
        CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.SpaceClass) ||
        CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.NotSpaceClass) ||
        CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.WordClass) ||
        CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.NotWordClass);

    private static bool MatchesKnownProjectedCategory(char value, string categoryPayload)
    {
        if (CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.DigitClass))
        {
            return Utf8AsciiBytePredicates.IsDigit((byte)value);
        }

        if (CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.NotDigitClass))
        {
            return !Utf8AsciiBytePredicates.IsDigit((byte)value);
        }

        if (CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.SpaceClass))
        {
            return Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value);
        }

        if (CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.NotSpaceClass))
        {
            return !Utf8AsciiBytePredicates.IsSixByteWhitespace((byte)value);
        }

        if (CategoryEquals(categoryPayload, RuntimeFrontEnd.RegexCharClass.WordClass))
        {
            return RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar(value);
        }

        return !RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar(value);
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
