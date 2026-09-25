namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned character-class wire queries: pure decoders over the set payload
// encoding (negate flag, range length, category length, half-open range pairs,
// category payload, chained subtraction segments), verified row by row against
// the fork over live-parsed payloads. Matching (CharInClass), category payload
// constants, and builders migrate with the matching core, not here.
internal static class Utf8OwnedCharClass
{
    public const int FlagsIndex = 0;

    public const int SetLengthIndex = 1;

    public const int CategoryLengthIndex = 2;

    public const int SetStartIndex = 3;

    private const string AnyClass = "\x00\x01\x00\x00";

    public static bool IsNegated(string set) => set.Length > FlagsIndex && set[FlagsIndex] != 0;

    public static bool IsSubtraction(string set) =>
        set.Length > SetStartIndex +
        set[CategoryLengthIndex] +
        set[SetLengthIndex];

    public static bool IsEmpty(string set) => set == "\x00\x00\x00";

    public static bool IsSingleton(string set)
    {
        if (set.Length != SetStartIndex + 2 || IsNegated(set) || set[CategoryLengthIndex] != 0)
        {
            return false;
        }

        return set[SetStartIndex + 1] - set[SetStartIndex] == 1;
    }

    public static char SingletonChar(string set)
    {
        if (!IsSingleton(set))
        {
            throw new ArgumentException("Set is not a singleton.", nameof(set));
        }

        return set[SetStartIndex];
    }

    public static bool TryGetSingleRange(string set, out char first, out char last)
    {
        if (set.Length == SetStartIndex + 2 && !IsNegated(set) && set[CategoryLengthIndex] == 0)
        {
            first = set[SetStartIndex];
            last = (char)(set[SetStartIndex + 1] - 1);
            return true;
        }

        first = default;
        last = default;
        return false;
    }

    public static bool TryGetDoubleRange(string set, out (char First, char Last) firstRange, out (char First, char Last) secondRange)
    {
        if (set.Length == SetStartIndex + 4 && !IsNegated(set) && set[CategoryLengthIndex] == 0)
        {
            firstRange = (set[SetStartIndex], (char)(set[SetStartIndex + 1] - 1));
            secondRange = (set[SetStartIndex + 2], (char)(set[SetStartIndex + 3] - 1));
            return true;
        }

        firstRange = default;
        secondRange = default;
        return false;
    }

    public static bool TryGetOnlyCategories(string set, out string categories)
    {
        if (set[SetLengthIndex] == 0 && set[CategoryLengthIndex] > 0)
        {
            categories = set.AsSpan(SetStartIndex, set[CategoryLengthIndex]).ToString();
            return true;
        }

        categories = string.Empty;
        return false;
    }

    public static bool CanEasilyEnumerateSetContents(string set) =>
        !IsNegated(set) &&
        !IsSubtraction(set) &&
        set[CategoryLengthIndex] == 0 &&
        (set[SetLengthIndex] & 1) == 0;

    public static bool TryGetSetCharCount(string set, out int count)
    {
        if (!CanEasilyEnumerateSetContents(set))
        {
            count = 0;
            return false;
        }

        count = 0;
        for (var i = SetStartIndex; i < SetStartIndex + set[SetLengthIndex]; i += 2)
        {
            var upperExclusive = set[i + 1] == char.MinValue ? char.MaxValue + 1 : set[i + 1];
            count += upperExclusive - set[i];
        }

        return true;
    }

    public static char[] GetSetChars(string set)
    {
        if (!CanEasilyEnumerateSetContents(set))
        {
            throw new ArgumentException("Set cannot be enumerated cheaply.", nameof(set));
        }

        _ = TryGetSetCharCount(set, out var count);
        var chars = new List<char>(count);
        for (var i = SetStartIndex; i < SetStartIndex + set[SetLengthIndex]; i += 2)
        {
            var upperExclusive = set[i + 1] == char.MinValue ? char.MaxValue + 1 : set[i + 1];
            for (var ch = (int)set[i]; ch < upperExclusive; ch++)
            {
                chars.Add((char)ch);
            }
        }

        return [.. chars];
    }

    public static bool IsAscii(string set)
    {
        if (set == AnyClass)
        {
            return true;
        }

        for (var i = SetStartIndex; i < SetStartIndex + set[SetLengthIndex]; i += 2)
        {
            if (set[i + 1] > 0x80)
            {
                return false;
            }
        }

        return set[CategoryLengthIndex] == 0;
    }

    public static bool ParticipatesInCaseConversion(char ch) =>
        char.ToLowerInvariant(ch) != char.ToUpperInvariant(ch);
}