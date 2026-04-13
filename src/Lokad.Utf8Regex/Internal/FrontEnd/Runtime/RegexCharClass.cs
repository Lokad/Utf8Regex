using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal sealed class RegexCharClass
{
    internal const int FlagsIndex = 0;
    internal const int SetLengthIndex = 1;
    internal const int CategoryLengthIndex = 2;
    internal const int SetStartIndex = 3;
    internal const char LastChar = '\uFFFF';

    internal const string SpaceClass = "\u0000\u0000\u0001\u0064";
    internal const string NotSpaceClass = "\u0000\u0000\u0001\uFF9C";
    internal const string WordClass = "\u0000\u0000\u000A\u0000\u0002\u0004\u0005\u0003\u0001\u0006\u0009\u0013\u0000";
    internal const string NotWordClass = "\u0000\u0000\u000A\u0000\uFFFE\uFFFC\uFFFB\uFFFD\uFFFF\uFFFA\uFFF7\uFFED\u0000";
    internal const string DigitClass = "\u0000\u0000\u0001\u0009";
    internal const string NotDigitClass = "\u0000\u0000\u0001\uFFF7";
    internal const string ControlClass = "\0\0\u0001\u000f";
    internal const string LetterClass = "\0\0\a\0\u0002\u0004\u0005\u0003\u0001\0";
    internal const string LowerClass = "\0\0\u0001\u0002";
    internal const string UpperClass = "\0\0\u0001\u0001";
    internal const string AsciiLetterClass = "\0\u0004\0A[a{";
    internal const string AsciiLetterOrDigitClass = "\0\u0006\00:A[a{";
    internal const string HexDigitClass = "\0\u0006\00:AGag";
    internal const string NotNewLineClass = "\x01\x02\x00\x0A\x0B";
    internal const string NotAnyNewLineClass = "\x01\x06\x00\x0A\x0E\x85\x86\u2028\u202A";
    internal const string AnyNewLineClass = "\x00\x06\x00\x0A\x0E\x85\x86\u2028\u202A";
    internal const string AnyClass = "\x00\x01\x00\x00";

    private const string InternalRegexIgnoreCase = "__InternalRegexIgnoreCase__";
    private const string ECMASpaceRanges = "\u0009\u000E\u0020\u0021";
    private const string ECMAWordRanges = "\u0030\u003A\u0041\u005B\u005F\u0060\u0061\u007B\u0130\u0131";
    private const string ECMADigitRanges = "\u0030\u003A";

    internal const string ECMASpaceClass = "\x00\x04\x00" + ECMASpaceRanges;
    internal const string NotECMASpaceClass = "\x01\x04\x00" + ECMASpaceRanges;
    internal const string ECMAWordClass = "\x00\x0A\x00" + ECMAWordRanges;
    internal const string NotECMAWordClass = "\x01\x0A\x00" + ECMAWordRanges;
    internal const string ECMADigitClass = "\x00\x02\x00" + ECMADigitRanges;
    internal const string NotECMADigitClass = "\x01\x02\x00" + ECMADigitRanges;

    private static readonly Dictionary<string, string> s_definedCategories = CreateDefinedCategories();

    private List<(char First, char Last)>? _rangelist;
    private StringBuilder? _categories;
    private RegexCharClass? _subtractor;
    private bool _negate;
    private RegexCaseBehavior _caseBehavior;

    private static readonly byte[] s_wordCharAsciiLookup = CreateWordCharAsciiLookup();
    private const int WordCategoriesMask =
        1 << (int)UnicodeCategory.UppercaseLetter |
        1 << (int)UnicodeCategory.LowercaseLetter |
        1 << (int)UnicodeCategory.TitlecaseLetter |
        1 << (int)UnicodeCategory.ModifierLetter |
        1 << (int)UnicodeCategory.OtherLetter |
        1 << (int)UnicodeCategory.NonSpacingMark |
        1 << (int)UnicodeCategory.DecimalDigitNumber |
        1 << (int)UnicodeCategory.ConnectorPunctuation;

    public RegexCharClass()
    {
    }

    public bool CanMerge => !_negate && _subtractor is null;

    public static bool IsMergeable(string set) => !IsNegated(set) && !IsSubtraction(set);

    public bool Negate
    {
        set => _negate = value;
    }

    public void AddChar(char c) => AddRange(c, c);

    public void AddCharClass(RegexCharClass cc)
    {
        Debug.Assert(cc.CanMerge && CanMerge);

        if (cc._rangelist is { Count: > 0 })
        {
            EnsureRangeList().AddRange(cc._rangelist);
        }

        if (cc._categories is { Length: > 0 })
        {
            EnsureCategories().Append(cc._categories);
        }
    }

    public bool TryAddCharClass(RegexCharClass cc)
    {
        if (!cc.CanMerge || !CanMerge)
        {
            return false;
        }

        AddCharClass(cc);
        return true;
    }

    public List<(char First, char Last)> EnsureRangeList() => _rangelist ??= new List<(char First, char Last)>();

    public StringBuilder EnsureCategories() => _categories ??= new StringBuilder();

    public void AddRanges(ReadOnlySpan<(char First, char Last)> ranges)
    {
        var list = EnsureRangeList();
        foreach (var range in ranges)
        {
            list.Add(range);
        }
    }

    public void AddSubtraction(RegexCharClass subtraction)
    {
        _subtractor = subtraction;
    }

    public void AddRange(char first, char last)
    {
        if (last < first)
        {
            throw new ArgumentOutOfRangeException(nameof(last));
        }

        EnsureRangeList().Add((first, last));
    }

    public void AddCategoryFromName(string categoryName, bool invert, bool caseInsensitive, string pattern, int currentPos)
    {
        ArgumentNullException.ThrowIfNull(categoryName);

        if (!s_definedCategories.TryGetValue(categoryName, out var category) &&
            !(caseInsensitive && s_definedCategories.TryGetValue(InternalRegexIgnoreCase, out category) && categoryName == "I"))
        {
            throw new ArgumentException($"Unknown category '{categoryName}' at index {currentPos} in '{pattern}'.", nameof(categoryName));
        }

        AddCategory(category, invert);
    }

    public void AddCategory(string category, bool invert)
    {
        ArgumentNullException.ThrowIfNull(category);

        var categories = EnsureCategories();
        if (invert)
        {
            foreach (var ch in category)
            {
                categories.Append((char)(-1 - ch));
            }
        }
        else
        {
            categories.Append(category);
        }
    }

    public void AddCaseEquivalences(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (_rangelist is not { Count: > 0 } ranges)
        {
            return;
        }

        _caseBehavior = RegexCaseEquivalences.GetRegexBehavior(culture);

        var originalCount = ranges.Count;
        for (var i = 0; i < originalCount; i++)
        {
            var (first, last) = ranges[i];
            AddCaseEquivalenceRange(first, last, culture);
        }
    }

    public void AddCaseEquivalences(char first, char last, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        _caseBehavior = RegexCaseEquivalences.GetRegexBehavior(culture);
        AddCaseEquivalenceRange(first, last, culture);
    }

    public void AddCaseEquivalenceRange(char first, char last, CultureInfo? culture = null)
    {
        _ = _caseBehavior;
        AddRange(first, last);

        culture ??= CultureInfo.InvariantCulture;
        for (var ch = first; ; ch++)
        {
            var lower = char.ToLower(ch, culture);
            var upper = char.ToUpper(ch, culture);
            AddRange(lower, lower);
            AddRange(upper, upper);

            if (ch == last)
            {
                break;
            }
        }
    }

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

    public static bool CharInClass(char ch, string set)
    {
        if (IsSubtraction(set))
        {
            return Parse(set).CharInClass(ch);
        }

        return IsNegated(set) ? !CharInClassBase(ch, set) : CharInClassBase(ch, set);
    }

    internal static bool CharInClassBase(char ch, string set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (set.Length < SetStartIndex)
        {
            throw new ArgumentException("Invalid character class payload.", nameof(set));
        }

        if (set == AnyClass)
        {
            return true;
        }

        if (TryMatchPredefinedAsciiClass(ch, set, out var predefinedResult))
        {
            return predefinedResult;
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

    public static bool CanEasilyEnumerateSetContents(string set) =>
        !IsNegated(set) &&
        !IsSubtraction(set) &&
        set[CategoryLengthIndex] == 0 &&
        (set[SetLengthIndex] & 1) == 0;

    public static char[] GetSetChars(string set)
    {
        if (!CanEasilyEnumerateSetContents(set))
        {
            throw new ArgumentException("Set cannot be enumerated cheaply.", nameof(set));
        }

        var chars = new List<char>();
        for (var i = SetStartIndex; i < SetStartIndex + set[SetLengthIndex]; i += 2)
        {
            var first = set[i];
            var last = (char)(set[i + 1] - 1);
            for (var ch = first; ch <= last; ch++)
            {
                chars.Add(ch);
            }
        }

        return [.. chars];
    }

    public static bool SetContainsAsciiOrdinalIgnoreCaseCharacter(string set, char value)
    {
        if (value > 0x7F)
        {
            return false;
        }

        var folded = char.ToLowerInvariant(value);
        return CharInClass(value, set) || CharInClass(folded, set) || CharInClass(char.ToUpperInvariant(value), set);
    }

    public static bool MayOverlap(string left, string right)
    {
        if (CanEasilyEnumerateSetContents(left) && CanEasilyEnumerateSetContents(right))
        {
            foreach (var ch in GetSetChars(left))
            {
                if (CharInClass(ch, right))
                {
                    return true;
                }
            }

            return false;
        }

        for (var ch = char.MinValue; ; ch++)
        {
            if (CharInClass(ch, left) && CharInClass(ch, right))
            {
                return true;
            }

            if (ch == char.MaxValue)
            {
                return false;
            }
        }
    }

    public static ReadOnlySpan<byte> WordCharAsciiLookup => s_wordCharAsciiLookup;

    public static bool IsECMAWordChar(char ch) =>
        (uint)ch < 128 && (char.IsAsciiLetterOrDigit(ch) || ch == '_');

    public static bool IsWordChar(char ch)
    {
        return (uint)ch < (uint)s_wordCharAsciiLookup.Length ?
            s_wordCharAsciiLookup[ch] != 0 :
            IsWordCategory(CharUnicodeInfo.GetUnicodeCategory(ch));
    }

    public static bool IsBoundaryWordChar(char ch)
    {
        const char ZeroWidthNonJoiner = '\u200C';
        const char ZeroWidthJoiner = '\u200D';

        return (uint)ch < (uint)s_wordCharAsciiLookup.Length ?
            s_wordCharAsciiLookup[ch] != 0 :
            (IsWordCategory(CharUnicodeInfo.GetUnicodeCategory(ch)) ||
             ch == ZeroWidthJoiner ||
             ch == ZeroWidthNonJoiner);
    }

    private static bool IsWordCategory(UnicodeCategory category) =>
        (WordCategoriesMask & (1 << (int)category)) != 0;

    public static bool ParticipatesInCaseConversion(char ch) =>
        char.ToLowerInvariant(ch) != char.ToUpperInvariant(ch);

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

    public static string OneToStringClass(char ch)
    {
        var charClass = new RegexCharClass();
        charClass.AddChar(ch);
        return charClass.ToStringClass();
    }

    public static string CharsToStringClass(ReadOnlySpan<char> chars)
    {
        var charClass = new RegexCharClass();
        foreach (var ch in chars)
        {
            charClass.AddChar(ch);
        }

        return charClass.ToStringClass();
    }

    public void AddWord(bool ecma, bool negate)
    {
        AddPredefinedClass(ecma ? ECMAWordClass : WordClass, ecma ? NotECMAWordClass : NotWordClass, negate);
    }

    public void AddSpace(bool ecma, bool negate)
    {
        AddPredefinedClass(ecma ? ECMASpaceClass : SpaceClass, ecma ? NotECMASpaceClass : NotSpaceClass, negate);
    }

    public void AddDigit(bool ecma, bool negate, string pattern, int currentPos)
    {
        _ = pattern;
        _ = currentPos;
        AddPredefinedClass(ecma ? ECMADigitClass : DigitClass, ecma ? NotECMADigitClass : NotDigitClass, negate);
    }

    public string ToStringClass()
    {
        var buffer = new StringBuilder();
        RegexCharClass? current = this;
        do
        {
            var ranges = current._rangelist;
            var categories = current._categories;
            var setLength = ranges is null ? 0 : ranges.Count * 2;
            var categoryLength = categories?.Length ?? 0;

            buffer.Append(current._negate ? (char)1 : (char)0);
            buffer.Append((char)setLength);
            buffer.Append((char)categoryLength);

            if (ranges is not null)
            {
                foreach (var (first, last) in ranges)
                {
                    buffer.Append(first);
                    buffer.Append((char)(last + 1));
                }
            }

            if (categories is not null)
            {
                buffer.Append(categories);
            }

            current = current._subtractor;
        } while (current is not null);

        return buffer.ToString();
    }

    public static RegexCharClass Parse(string charClass)
    {
        ArgumentNullException.ThrowIfNull(charClass);
        if (charClass.Length < SetStartIndex)
        {
            throw new ArgumentException("Invalid character class payload.", nameof(charClass));
        }

        RegexCharClass? outermost = null;
        RegexCharClass? current = null;
        var pos = 0;

        while (pos < charClass.Length)
        {
            if (charClass.Length - pos < SetStartIndex)
            {
                throw new ArgumentException("Invalid character class payload.", nameof(charClass));
            }

            var result = new RegexCharClass
            {
                _negate = charClass[pos + FlagsIndex] != 0,
            };

            var setLength = charClass[pos + SetLengthIndex];
            var categoryLength = charClass[pos + CategoryLengthIndex];
            var setEnd = pos + SetStartIndex + setLength;
            for (var i = pos + SetStartIndex; i < setEnd; i += 2)
            {
                result.AddRange(charClass[i], (char)(charClass[i + 1] - 1));
            }

            if (categoryLength > 0)
            {
                result.EnsureCategories().Append(charClass, setEnd, categoryLength);
            }

            if (outermost is null)
            {
                outermost = result;
            }
            else
            {
                current!.AddSubtraction(result);
            }

            current = result;
            pos = setEnd + categoryLength;
        }

        return outermost!;
    }

    public bool CharInClass(char ch)
    {
        var result = MatchesBaseCharClass(ch);
        result = _negate ? !result : result;
        if (_subtractor is not null && _subtractor.CharInClass(ch))
        {
            result = false;
        }

        return result;
    }

    private bool MatchesBaseCharClass(char ch)
    {
        if (_rangelist is not null)
        {
            foreach (var (first, last) in _rangelist)
            {
                if (ch >= first && ch <= last)
                {
                    return true;
                }
            }
        }

        return _categories is { Length: > 0 } &&
            CategorySetContainsChar(ch, _categories.ToString().AsSpan());
    }

    private void AddPredefinedClass(string normalClass, string negatedClass, bool negate)
    {
        var parsed = Parse(negate ? negatedClass : normalClass);
        TryAddCharClass(parsed);
    }

    private static byte[] CreateWordCharAsciiLookup()
    {
        var lookup = new byte[128];
        for (var i = 0; i < lookup.Length; i++)
        {
            var ch = (char)i;
            if (char.IsAsciiLetterOrDigit(ch) || ch == '_')
            {
                lookup[i] = 1;
            }
        }

        return lookup;
    }

    private static bool TryMatchPredefinedAsciiClass(char ch, string set, out bool result)
    {
        result = set switch
        {
            SpaceClass or ECMASpaceClass => ch is ' ' or '\t' or '\r' or '\n' or '\f' or '\v',
            NotSpaceClass or NotECMASpaceClass => ch is not (' ' or '\t' or '\r' or '\n' or '\f' or '\v'),
            _ => false,
        };

        return set is SpaceClass or ECMASpaceClass or NotSpaceClass or NotECMASpaceClass;
    }

    private static bool CategorySetContainsChar(char ch, ReadOnlySpan<char> categories)
    {
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

    private static ushort DecodeCategoryCode(char category)
    {
        return category > 0x7FFF
            ? (ushort)~category
            : category;
    }

    private static Dictionary<string, string> CreateDefinedCategories()
    {
        var categories = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Lu", EncodeCategory(UnicodeCategory.UppercaseLetter) },
            { "Ll", EncodeCategory(UnicodeCategory.LowercaseLetter) },
            { "Lt", EncodeCategory(UnicodeCategory.TitlecaseLetter) },
            { "Lm", EncodeCategory(UnicodeCategory.ModifierLetter) },
            { "Lo", EncodeCategory(UnicodeCategory.OtherLetter) },
            { "L", EncodeCategoryGroup(UnicodeCategory.LowercaseLetter, UnicodeCategory.ModifierLetter, UnicodeCategory.OtherLetter, UnicodeCategory.TitlecaseLetter, UnicodeCategory.UppercaseLetter) },
            { InternalRegexIgnoreCase, EncodeCategoryGroup(UnicodeCategory.LowercaseLetter, UnicodeCategory.TitlecaseLetter, UnicodeCategory.UppercaseLetter) },
            { "Mn", EncodeCategory(UnicodeCategory.NonSpacingMark) },
            { "Mc", EncodeCategory(UnicodeCategory.SpacingCombiningMark) },
            { "Me", EncodeCategory(UnicodeCategory.EnclosingMark) },
            { "M", EncodeCategoryGroup(UnicodeCategory.NonSpacingMark, UnicodeCategory.SpacingCombiningMark, UnicodeCategory.EnclosingMark) },
            { "Nd", EncodeCategory(UnicodeCategory.DecimalDigitNumber) },
            { "Nl", EncodeCategory(UnicodeCategory.LetterNumber) },
            { "No", EncodeCategory(UnicodeCategory.OtherNumber) },
            { "N", EncodeCategoryGroup(UnicodeCategory.DecimalDigitNumber, UnicodeCategory.LetterNumber, UnicodeCategory.OtherNumber) },
            { "Zs", EncodeCategory(UnicodeCategory.SpaceSeparator) },
            { "Zl", EncodeCategory(UnicodeCategory.LineSeparator) },
            { "Zp", EncodeCategory(UnicodeCategory.ParagraphSeparator) },
            { "Z", EncodeCategoryGroup(UnicodeCategory.SpaceSeparator, UnicodeCategory.LineSeparator, UnicodeCategory.ParagraphSeparator) },
            { "Cc", EncodeCategory(UnicodeCategory.Control) },
            { "Cf", EncodeCategory(UnicodeCategory.Format) },
            { "Cs", EncodeCategory(UnicodeCategory.Surrogate) },
            { "Co", EncodeCategory(UnicodeCategory.PrivateUse) },
            { "Cn", EncodeCategory(UnicodeCategory.OtherNotAssigned) },
            { "C", EncodeCategoryGroup(UnicodeCategory.Control, UnicodeCategory.Format, UnicodeCategory.Surrogate, UnicodeCategory.PrivateUse, UnicodeCategory.OtherNotAssigned) },
            { "Pc", EncodeCategory(UnicodeCategory.ConnectorPunctuation) },
            { "Pd", EncodeCategory(UnicodeCategory.DashPunctuation) },
            { "Ps", EncodeCategory(UnicodeCategory.OpenPunctuation) },
            { "Pe", EncodeCategory(UnicodeCategory.ClosePunctuation) },
            { "Pi", EncodeCategory(UnicodeCategory.InitialQuotePunctuation) },
            { "Pf", EncodeCategory(UnicodeCategory.FinalQuotePunctuation) },
            { "Po", EncodeCategory(UnicodeCategory.OtherPunctuation) },
            { "P", EncodeCategoryGroup(UnicodeCategory.ConnectorPunctuation, UnicodeCategory.DashPunctuation, UnicodeCategory.OpenPunctuation, UnicodeCategory.ClosePunctuation, UnicodeCategory.InitialQuotePunctuation, UnicodeCategory.FinalQuotePunctuation, UnicodeCategory.OtherPunctuation) },
            { "Sm", EncodeCategory(UnicodeCategory.MathSymbol) },
            { "Sc", EncodeCategory(UnicodeCategory.CurrencySymbol) },
            { "Sk", EncodeCategory(UnicodeCategory.ModifierSymbol) },
            { "So", EncodeCategory(UnicodeCategory.OtherSymbol) },
            { "S", EncodeCategoryGroup(UnicodeCategory.MathSymbol, UnicodeCategory.CurrencySymbol, UnicodeCategory.ModifierSymbol, UnicodeCategory.OtherSymbol) },
        };

        return categories;
    }

    private static string EncodeCategory(UnicodeCategory category)
    {
        return new string((char)((int)category + 1), 1);
    }

    private static string EncodeCategoryGroup(params UnicodeCategory[] categories)
    {
        var encoded = new char[categories.Length + 2];
        encoded[0] = (char)0;
        for (var i = 0; i < categories.Length; i++)
        {
            encoded[i + 1] = (char)((int)categories[i] + 1);
        }

        encoded[^1] = (char)0;
        return new string(encoded);
    }
}
