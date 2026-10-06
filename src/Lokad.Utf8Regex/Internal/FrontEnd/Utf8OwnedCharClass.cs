namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Owned character-class wire queries over the canonical set payload:
// negate flag, range length, category length, half-open range pairs,
// category payload and chained subtraction segments. A final lone range
// endpoint includes every unit through U+FFFF; zero paired upper bounds
// likewise mean 65536. Wire facts and .NET matching are qualified separately.
internal static class Utf8OwnedCharClass
{
    // A range predicate retains its negation over the scalar domain. Expanding
    // a complement into positive BMP ranges would discard every other plane.
    internal const char RangePredicateCode = (char)101;
    internal const char ScalarRangePredicateCode = (char)102;
    internal const string AnyScalarClass = "\u0000\u0000\u0006\u0066\u0002\u0000\u0000\u0000\u0011";

    internal static void AddScalarRangePredicate(List<char> categories, IReadOnlyList<int> ranges)
    {
        categories.Add(ScalarRangePredicateCode);
        categories.Add((char)ranges.Count);
        foreach (var endpoint in ranges)
        {
            categories.Add((char)endpoint);
            categories.Add((char)(endpoint >> 16));
        }
    }

    internal static void AddRangePredicate(List<char> categories, ReadOnlySpan<ushort> ranges, bool negated)
    {
        categories.Add(RangePredicateCode);
        categories.Add(negated ? (char)1 : (char)0);
        categories.Add((char)ranges.Length);
        foreach (var endpoint in ranges) categories.Add((char)endpoint);
    }

    public const int FlagsIndex = 0;

    public const int SetLengthIndex = 1;

    public const int CategoryLengthIndex = 2;

    public const int SetStartIndex = 3;

    public const string AnyClass = "\x00\x01\x00\x00";

    // Character-class wire payloads shared with the fork encoding, transcribed
    // codepoint by codepoint and verified exact by the PayloadConstantsMatchFork
    // battery: backslash-s 0,0,1,100 and backslash-S 0,0,1,65436 were additionally
    // confirmed by a runtime probe under default and ECMAScript options.
    public static readonly string SpaceClass = new string(new char[] { (char)0, (char)0, (char)1, (char)100 });

    public static readonly string NotSpaceClass = new string(new char[] { (char)0, (char)0, (char)1, (char)65436 });

    public static readonly string ECMASpaceClass = new string(new char[] { (char)0, (char)4, (char)0, (char)9, (char)14, (char)32, (char)33 });

    public static readonly string NotECMASpaceClass = new string(new char[] { (char)1, (char)4, (char)0, (char)9, (char)14, (char)32, (char)33 });

    public static readonly string DigitClass = new string(new char[] { (char)0, (char)0, (char)1, (char)9 });

    public static readonly string NotDigitClass = new string(new char[] { (char)0, (char)0, (char)1, (char)65527 });

    public static readonly string WordClass = new string(new char[] { (char)0, (char)0, (char)10, (char)0, (char)2, (char)4, (char)5, (char)3, (char)1, (char)6, (char)9, (char)19, (char)0 });

    public static readonly string NotWordClass = new string(new char[] { (char)0, (char)0, (char)10, (char)0, (char)65534, (char)65532, (char)65531, (char)65533, (char)65535, (char)65530, (char)65527, (char)65517, (char)0 });

    public static readonly string NotNewLineClass = new string(new char[] { (char)1, (char)2, (char)0, (char)10, (char)11 });

    public static bool IsNegated(string set) => set.Length > FlagsIndex && set[FlagsIndex] != 0;

    public static bool IsSubtraction(string set) =>
        set.Length > SetStartIndex +
        set[CategoryLengthIndex] +
        set[SetLengthIndex];

    public static bool IsEmpty(string set) => set == "\x00\x00\x00";

    public static bool IsSingleton(string set)
    {
        if (set.Length == SetStartIndex + 1 && set[SetLengthIndex] == 1 && !IsNegated(set) && set[CategoryLengthIndex] == 0)
            return set[SetStartIndex] == char.MaxValue;
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
        if (set.Length is SetStartIndex + 1 or SetStartIndex + 2 &&
            set[SetLengthIndex] == set.Length - SetStartIndex && !IsNegated(set) && set[CategoryLengthIndex] == 0)
        {
            first = set[SetStartIndex];
            last = set.Length == SetStartIndex + 1 ? char.MaxValue : (char)(set[SetStartIndex + 1] - 1);
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
            if (i + 1 == SetStartIndex + set[SetLengthIndex] || set[i + 1] == 0 || set[i + 1] > 0x80)
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
                    var high = i + 1 == segmentEnd || innerSet[i + 1] == 0 ? 65536 : innerSet[i + 1];
                    if (innerCh >= innerSet[i] && innerCh < high)
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
                    var last = i + 1 == segmentEnd ? char.MaxValue : (char)(innerSet[i + 1] - 1);
                    if (last < innerSet[i])
                    {
                        throw new ArgumentOutOfRangeException("last");
                    }
                }

                _ = new System.Text.StringBuilder().Append(innerSet, segmentEnd, categoryLength).ToString();
                pos = segmentEnd + categoryLength;
            }
        }

        // [^\s] and [^\S] carry the set-negation flag over the space bodies,
        // which the base decoder only special-cases unflagged. Honor the flag
        // here with the product's six-ASCII-whitespace definition, matching
        // SpaceClass; non-ASCII whitespace stays at that recorded boundary.
        if (IsNegated(set) &&
            set.Length == SetStartIndex + 1 &&
            set[SetLengthIndex] == 0 &&
            set[CategoryLengthIndex] == 1)
        {
            if (set[SetStartIndex] == (char)100)
            {
                return ch is not ((char)32 or (char)9 or (char)13 or (char)10 or (char)12 or (char)11);
            }

            if (set[SetStartIndex] == (char)0xFF9C)
            {
                return ch is ((char)32 or (char)9 or (char)13 or (char)10 or (char)12 or (char)11);
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

        if (set == SpaceClass)
        {
            return ch is (char)32 or (char)9 or (char)13 or (char)10 or (char)12 or (char)11;
        }

        if (set == NotSpaceClass)
        {
            return ch is not ((char)32 or (char)9 or (char)13 or (char)10 or (char)12 or (char)11);
        }

        var setLength = set[SetLengthIndex];
        var categoryLength = set[CategoryLengthIndex];
        var setEnd = SetStartIndex + setLength;
        for (var i = SetStartIndex; i < setEnd; i += 2)
        {
            var high = i + 1 == setEnd || set[i + 1] == 0 ? 65536 : set[i + 1];
            if (ch >= set[i] && ch < high)
            {
                return true;
            }
        }

        return categoryLength > 0 && CategorySetContainsChar(ch, set.AsSpan(setEnd, categoryLength));
    }

    private static bool CategorySetContainsChar(char ch, ReadOnlySpan<char> categories)
    {
        // Negated codes are the unary minus of the positive 1-based codes,
        // not the bitwise complement (which lands one off): the shorthand wire
        // bytes pair 0xFFF7 with digit 9, 0xFF9C with space 100, and 0xFFFE
        // with Ll 2, so decode with two's-complement negation.
        static ushort DecodeCategoryCode(char category)
        {
            return category > 0x7FFF ? (ushort)(~category + 1) : category;
        }

        var unicodeCategory = (ushort)((int)char.GetUnicodeCategory(ch) + 1);
        for (var i = 0; i < categories.Length; i++)
        {
            var current = categories[i];
            if (current == ScalarRangePredicateCode)
            {
                var length = categories[++i];
                var rangeHit = false;
                for (var range = 0; range < length; range += 2)
                {
                    var low = categories[i + 1 + range * 2] | categories[i + 2 + range * 2] << 16;
                    var high = categories[i + 3 + range * 2] | categories[i + 4 + range * 2] << 16;
                    rangeHit |= ch >= low && ch < high;
                }
                i += length * 2;
                if (rangeHit) return true;
                continue;
            }
            if (current == RangePredicateCode)
            {
                var negated = categories[++i] != 0;
                var length = categories[++i];
                var ranges = categories.Slice(i + 1, length);
                var rangeHit = false;
                for (var range = 0; range + 1 < ranges.Length; range += 2)
                    rangeHit |= ch >= ranges[range] && ch < ranges[range + 1];
                rangeHit |= (ranges.Length & 1) != 0 && ch >= ranges[^1];
                i += length;
                if (rangeHit != negated) return true;
                continue;
            }
            // A trailing 0xFFFF is a lone complemented code (such as exclude-Lu),
            // not an inverted group: only a non-final 0xFFFF opens one.
            if (current == 0 || (current == char.MaxValue && i + 1 < categories.Length))
            {
                var invertedGroup = current == char.MaxValue;
                var terminator = invertedGroup ? char.MaxValue : (char)0;
                if (invertedGroup)
                {
                    // The close is the final 0xFFFF: complemented members may
                    // themselves be 0xFFFF (exclude-Lu is -1), so stopping at the
                    // first one would truncate the group and wrongly admit.
                    var close = categories.Length - 1;
                    while (close > i && categories[close] != char.MaxValue)
                    {
                        close--;
                    }

                    var end = close > i ? close : categories.Length;
                    var excludedAny = false;
                    while (++i < end)
                    {
                        if (unicodeCategory == DecodeCategoryCode(categories[i]))
                        {
                            excludedAny = true;
                        }
                    }

                    if (!excludedAny)
                    {
                        return true;
                    }

                    continue;
                }

                // A 0-framed group unions positive members and intersects
                // negated members: all-positive lists include on any hit (\w),
                // all-negated lists exclude on any hit (\W).
                var matchedAny = false;
                var sawPositive = false;
                var negatedMiss = true;
                var memberCount = 0;
                while (++i < categories.Length && categories[i] != terminator)
                {
                    memberCount++;
                    var member = categories[i];
                    if (member > 0x7FFF)
                    {
                        negatedMiss = negatedMiss && unicodeCategory != DecodeCategoryCode(member);
                    }
                    else
                    {
                        sawPositive = true;
                        matchedAny = matchedAny || unicodeCategory == DecodeCategoryCode(member);
                    }
                }

                if (memberCount > 0 && (matchedAny || !sawPositive) && negatedMiss)
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


    public static bool IsECMAWordChar(char ch) =>
        (uint)ch < 128 && (char.IsAsciiLetterOrDigit(ch) || ch == (char)95);

    public static bool IsWordChar(char ch) =>
        (uint)ch < (uint)s_wordCharAsciiLookup.Length ?
            s_wordCharAsciiLookup[ch] != 0 :
            IsWordCategory(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch));

    public static bool IsBoundaryWordChar(char ch)
    {
        const char ZeroWidthNonJoiner = (char)8204;
        const char ZeroWidthJoiner = (char)8205;
        return (uint)ch < (uint)s_wordCharAsciiLookup.Length ?
            s_wordCharAsciiLookup[ch] != 0 :
            (IsWordCategory(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)) ||
                ch == ZeroWidthJoiner ||
                ch == ZeroWidthNonJoiner);
    }

    public static ReadOnlySpan<byte> WordCharAsciiLookup => s_wordCharAsciiLookup;

    private static readonly byte[] s_wordCharAsciiLookup = new byte[128];

    private const int WordCategoriesMask =
        1 << (int)System.Globalization.UnicodeCategory.UppercaseLetter |
        1 << (int)System.Globalization.UnicodeCategory.LowercaseLetter |
        1 << (int)System.Globalization.UnicodeCategory.TitlecaseLetter |
        1 << (int)System.Globalization.UnicodeCategory.ModifierLetter |
        1 << (int)System.Globalization.UnicodeCategory.OtherLetter |
        1 << (int)System.Globalization.UnicodeCategory.NonSpacingMark |
        1 << (int)System.Globalization.UnicodeCategory.DecimalDigitNumber |
        1 << (int)System.Globalization.UnicodeCategory.ConnectorPunctuation;

    static Utf8OwnedCharClass()
    {
        for (var i = 0; i < s_wordCharAsciiLookup.Length; i++)
        {
            var ch = (char)i;
            if (char.IsAsciiLetterOrDigit(ch) || ch == (char)95)
            {
                s_wordCharAsciiLookup[i] = 1;
            }
        }
    }

    private static bool IsWordCategory(System.Globalization.UnicodeCategory category) =>
        (WordCategoriesMask & (1 << (int)category)) != 0;
}
