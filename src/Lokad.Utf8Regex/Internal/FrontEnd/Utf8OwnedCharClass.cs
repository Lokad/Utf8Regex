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

    // Probe 2026-09-26: the fork reports backslash-s as the exact four-char payload
    // 0,0,1,100 and backslash-S as 0,0,1,65436, identically under default and
    // ECMAScript options. The equality checks below mirror the fork switch, which only
    // fires on those full payloads: negated bracket forms and subtraction wires whose
    // first segment is a space payload both fall through to the generic category
    // decode, exactly like the fork.
    private static readonly string SpaceClassPayload = new string(new char[] { (char)0, (char)0, (char)1, (char)100 });

    private static readonly string NotSpaceClassPayload = new string(new char[] { (char)0, (char)0, (char)1, (char)65436 });

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

    public static bool CharInClass(char ch, string set)
    {
        static bool MatchSubtracted(char innerCh, string innerSet)
        {
            ValidateSubtractedPayload(innerSet);
            var segmentMatches = new List<bool>();
            var pos = 0;
            while (pos < innerSet.Length)
            {
                var negated = innerSet[pos + FlagsIndex] != 0;
                var setLength = innerSet[pos + SetLengthIndex];
                var categoryLength = innerSet[pos + CategoryLengthIndex];
                var segmentStart = pos + SetStartIndex;
                var segmentEnd = segmentStart + setLength;
                var matched = false;
                for (var i = segmentStart; i < segmentEnd; i += 2)
                {
                    if (innerCh >= innerSet[i] && innerCh < innerSet[i + 1])
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched && categoryLength > 0)
                {
                    var categories = new System.Text.StringBuilder().Append(innerSet, segmentEnd, categoryLength).ToString();
                    matched = CategorySetContainsChar(innerCh, categories.AsSpan());
                }

                segmentMatches.Add(negated ? !matched : matched);
                pos = segmentEnd + categoryLength;
            }

            var result = false;
            for (var i = segmentMatches.Count - 1; i >= 0; i--)
            {
                result = segmentMatches[i] && !result;
            }

            return result;
        }

        static void ValidateSubtractedPayload(string innerSet)
        {
            var pos = 0;
            while (pos < innerSet.Length)
            {
                if (innerSet.Length - pos < SetStartIndex)
                {
                    throw new ArgumentException("Invalid character class payload.", "charClass");
                }

                var setLength = innerSet[pos + SetLengthIndex];
                var categoryLength = innerSet[pos + CategoryLengthIndex];
                var segmentEnd = pos + SetStartIndex + setLength;
                for (var i = pos + SetStartIndex; i < segmentEnd; i += 2)
                {
                    var last = (char)(innerSet[i + 1] - 1);
                    if (last < innerSet[i])
                    {
                        throw new ArgumentOutOfRangeException("last");
                    }
                }

                _ = new System.Text.StringBuilder().Append(innerSet, segmentEnd, categoryLength).ToString();
                pos = segmentEnd + categoryLength;
            }
        }

        if (IsSubtraction(set))
        {
            return MatchSubtracted(ch, set);
        }

        return IsNegated(set) ? !CharInClassBase(ch, set) : CharInClassBase(ch, set);
    }

    public static bool CharInClassBase(char ch, string set)
    {
        if (set.Length < SetStartIndex)
        {
            throw new ArgumentException("Invalid character class payload.", nameof(set));
        }

        if (set == AnyClass)
        {
            return true;
        }

        if (set == SpaceClassPayload)
        {
            return ch is (char)32 or (char)9 or (char)13 or (char)10 or (char)12 or (char)11;
        }

        if (set == NotSpaceClassPayload)
        {
            return ch is not ((char)32 or (char)9 or (char)13 or (char)10 or (char)12 or (char)11);
        }

        var setLength = set[SetLengthIndex];
        var categoryLength = set[CategoryLengthIndex];
        var setEnd = SetStartIndex + setLength;
        for (var i = SetStartIndex; i < setEnd; i += 2)
        {
            if (ch >= set[i] && ch < set[i + 1])
            {
                return true;
            }
        }

        return categoryLength > 0 && CategorySetContainsChar(ch, set.AsSpan(setEnd, categoryLength));
    }

    private static bool CategorySetContainsChar(char ch, ReadOnlySpan<char> categories)
    {
        static ushort DecodeCategoryCode(char category)
        {
            return category > 0x7FFF ? (ushort)~category : category;
        }

        var unicodeCategory = (ushort)((int)char.GetUnicodeCategory(ch) + 1);
        for (var i = 0; i < categories.Length; i++)
        {
            var current = categories[i];
            if (current == 0 || current == char.MaxValue)
            {
                var invertedGroup = current == char.MaxValue;
                var terminator = invertedGroup ? char.MaxValue : (char)0;
                var matchedAny = false;
                while (++i < categories.Length && categories[i] != terminator)
                {
                    if (unicodeCategory == DecodeCategoryCode(categories[i]))
                    {
                        matchedAny = true;
                    }
                }

                if (invertedGroup ? !matchedAny : matchedAny)
                {
                    return true;
                }

                continue;
            }

            var code = DecodeCategoryCode(current);
            if (current > 0x7FFF ? unicodeCategory != code : unicodeCategory == code)
            {
                return true;
            }
        }

        return false;
    }

}