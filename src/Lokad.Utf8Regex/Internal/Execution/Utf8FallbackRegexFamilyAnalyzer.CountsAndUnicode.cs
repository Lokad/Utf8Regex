using System.Globalization;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    public static bool TryParseUnicodeLetterBoundedRepeat(string pattern, RegexOptions options, out int minCount, out int maxCount)
    {
        minCount = 0;
        maxCount = 0;

        if (!pattern.StartsWith(@"\p{L}{", StringComparison.Ordinal) ||
            !pattern.EndsWith('}'))
        {
            return false;
        }

        if ((options & (RegexOptions.RightToLeft | RegexOptions.NonBacktracking)) != 0)
        {
            return false;
        }

        var commaIndex = pattern.IndexOf(',', 6);
        if (commaIndex < 0)
        {
            return false;
        }

        if (!int.TryParse(pattern.AsSpan(6, commaIndex - 6), out minCount) ||
            !int.TryParse(pattern.AsSpan(commaIndex + 1, pattern.Length - commaIndex - 2), out maxCount))
        {
            return false;
        }

        return minCount > 0 && maxCount >= minCount;
    }

    public static bool TryParseAsciiUntilByteStarCount(string pattern, RegexOptions options, out byte terminatorByte)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        terminatorByte = 0;
        if (options != RegexOptions.None)
        {
            return false;
        }

        if (!pattern.StartsWith("[^", StringComparison.Ordinal) ||
            !pattern.EndsWith("]*", StringComparison.Ordinal))
        {
            return false;
        }

        if (pattern.Length == 6 && pattern[2] == '\\')
        {
            terminatorByte = pattern[3] switch
            {
                'n' => (byte)'\n',
                'r' => (byte)'\r',
                't' => (byte)'\t',
                _ => (byte)0,
            };
            return terminatorByte != 0;
        }

        if (pattern.Length == 5)
        {
            terminatorByte = pattern[2] switch
            {
                '\n' => (byte)'\n',
                '\r' => (byte)'\r',
                '\t' => (byte)'\t',
                _ => (byte)0,
            };
            return terminatorByte != 0;
        }

        return false;
    }

    public static bool TryParseUnicodeLetterCount(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        return options == RegexOptions.None &&
            pattern == @"\p{L}";
    }

    public static bool TryParseUnicodeCategoryCount(string pattern, RegexOptions options, out UnicodeCategory category)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        category = UnicodeCategory.OtherNotAssigned;

        if (options is not (RegexOptions.None or RegexOptions.CultureInvariant) ||
            pattern.Length != 6 ||
            !pattern.StartsWith(@"\p{", StringComparison.Ordinal) ||
            pattern[5] != '}')
        {
            return false;
        }

        category = pattern.Substring(3, 2) switch
        {
            "Lu" => UnicodeCategory.UppercaseLetter,
            "Ll" => UnicodeCategory.LowercaseLetter,
            "Lt" => UnicodeCategory.TitlecaseLetter,
            "Lm" => UnicodeCategory.ModifierLetter,
            "Lo" => UnicodeCategory.OtherLetter,
            "Mn" => UnicodeCategory.NonSpacingMark,
            "Mc" => UnicodeCategory.SpacingCombiningMark,
            "Me" => UnicodeCategory.EnclosingMark,
            "Nd" => UnicodeCategory.DecimalDigitNumber,
            "Nl" => UnicodeCategory.LetterNumber,
            "No" => UnicodeCategory.OtherNumber,
            "Pc" => UnicodeCategory.ConnectorPunctuation,
            "Pd" => UnicodeCategory.DashPunctuation,
            "Ps" => UnicodeCategory.OpenPunctuation,
            "Pe" => UnicodeCategory.ClosePunctuation,
            "Pi" => UnicodeCategory.InitialQuotePunctuation,
            "Pf" => UnicodeCategory.FinalQuotePunctuation,
            "Po" => UnicodeCategory.OtherPunctuation,
            "Sm" => UnicodeCategory.MathSymbol,
            "Sc" => UnicodeCategory.CurrencySymbol,
            "Sk" => UnicodeCategory.ModifierSymbol,
            "So" => UnicodeCategory.OtherSymbol,
            "Zs" => UnicodeCategory.SpaceSeparator,
            "Zl" => UnicodeCategory.LineSeparator,
            "Zp" => UnicodeCategory.ParagraphSeparator,
            "Cc" => UnicodeCategory.Control,
            "Cf" => UnicodeCategory.Format,
            "Cs" => UnicodeCategory.Surrogate,
            "Co" => UnicodeCategory.PrivateUse,
            "Cn" => UnicodeCategory.OtherNotAssigned,
            _ => UnicodeCategory.OtherNotAssigned,
        };

        return category != UnicodeCategory.OtherNotAssigned || pattern.AsSpan(3, 2).SequenceEqual("Cn");
    }

    public static bool TryParseAsciiWordBoundedRepeat(string pattern, RegexOptions options, out int minCount)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        minCount = 0;
        if (options != RegexOptions.None)
        {
            return false;
        }

        var core = pattern switch
        {
            @"\w{10,}" => pattern,
            @"\b\w{10,}\b" => @"\w{10,}",
            _ => null,
        };

        if (core is null ||
            !core.StartsWith(@"\w{", StringComparison.Ordinal) ||
            !core.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        var commaIndex = core.IndexOf(',', 3);
        if (commaIndex < 0 ||
            !int.TryParse(core.AsSpan(3, commaIndex - 3), out minCount))
        {
            minCount = 0;
            return false;
        }

        return minCount > 0;
    }
}
