using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    private const string IdentifierTailChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
    private const string OperatorChars = "=+-^*/.<>~!&|?";

    public static bool TryParseAnchoredPrefixUntilBytePattern(
        string pattern,
        RegexOptions options,
        out byte[]? prefixUtf8,
        out byte terminator)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        prefixUtf8 = null;
        terminator = 0;
        if (options != (RegexOptions.Multiline | RegexOptions.CultureInvariant) ||
            pattern != "\\G///[^\\n]*\\n")
        {
            return false;
        }

        prefixUtf8 = "///"u8.ToArray();
        terminator = (byte)'\n';
        return true;
    }

    public static bool IsAnchoredAsciiIdentifierPrefix(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        var ignoreCase = (options & RegexOptions.IgnoreCase) != 0;
        return options == (RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant) &&
            TryParseAnchoredClassPlusStar(pattern, out var headClass, out var tailClass) &&
            IsAsciiLetterClass(headClass, ignoreCase) &&
            HasExactChars(tailClass, ignoreCase
                ? "abcdefghijklmnopqrstuvwxyz0123456789_"
                : IdentifierTailChars);
    }

    public static bool IsAnchoredAsciiNumberPrefix(string pattern, RegexOptions options)
    {
        if (pattern == @"\G-?[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?" &&
            options == RegexOptions.CultureInvariant)
        {
            return true;
        }

        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if (options != RegexOptions.CultureInvariant ||
            !pattern.StartsWith(@"\G", StringComparison.Ordinal))
        {
            return false;
        }

        var index = 2;
        if (TryConsume(pattern, ref index, "-?"))
        {
        }

        if (!TryConsume(pattern, ref index, "[0-9]+"))
        {
            return false;
        }

        TryConsumeOptionalGroup(pattern, ref index, ".", "[0-9]+");
        TryConsumeOptionalGroup(pattern, ref index, "e", "[+-]?", "[0-9]+");
        return index == pattern.Length;
    }

    public static bool IsAnchoredAsciiOperatorRun(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        return options == RegexOptions.CultureInvariant &&
            TryParseAnchoredClassPlus(pattern, out var operatorClass) &&
            HasExactChars(operatorClass, OperatorChars);
    }

    public static bool TryParseAnchoredAsciiLeadingDigitsTail(string pattern, RegexOptions options, out byte[]? separatorBytesUtf8)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        separatorBytesUtf8 = null;

        if (options is not (RegexOptions.None or RegexOptions.CultureInvariant))
        {
            return false;
        }

        var index = 0;
        if (!TryConsume(pattern, ref index, "^"))
        {
            return false;
        }

        if (!TryConsumeCapturedOrRaw(pattern, ref index, "[0-9]+") &&
            !TryConsumeCapturedOrRaw(pattern, ref index, @"\d+"))
        {
            return false;
        }

        if (!TryReadSingleAsciiLiteralAlternationOrDollar(pattern, ref index, out separatorBytesUtf8))
        {
            separatorBytesUtf8 = null;
            return false;
        }

        if (!TryConsumeCapturedOrRaw(pattern, ref index, ".*") ||
            !TryConsume(pattern, ref index, "$") ||
            index != pattern.Length)
        {
            separatorBytesUtf8 = null;
            return false;
        }

        return true;
    }

    public static bool IsAnchoredQuotedStringPrefix(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        if (options != RegexOptions.CultureInvariant ||
            !pattern.StartsWith("\\G\"", StringComparison.Ordinal) ||
            !pattern.EndsWith('\"'))
        {
            return false;
        }

        var core = pattern.Substring(3, pattern.Length - 4);
        return core is @"([^""\\]|\\.)*" or @"(?:[^""\\]|\\.)*";
    }

    public static bool TryParseAnchoredTrimmedOptionalLiteralPrefixTailPattern(
        string pattern,
        RegexOptions options,
        out byte[]? prefixUtf8,
        out byte[]? optionalPrefixUtf8)
    {
        prefixUtf8 = null;
        optionalPrefixUtf8 = null;

        if (options != RegexOptions.IgnorePatternWhitespace)
        {
            return false;
        }

        var compact = CompactRegexWhitespace(pattern);
        const string expectedPrefix = @"^\s*";
        const string expectedTail = @"\s?(?<doc>.*)$";
        if (!compact.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            !compact.EndsWith(expectedTail, StringComparison.Ordinal))
        {
            return false;
        }

        var middle = compact.Substring(expectedPrefix.Length, compact.Length - expectedPrefix.Length - expectedTail.Length);
        const string optionalGroupPrefix = @"(\[\|\s*)?";
        const string requiredLiteral = "///";
        if (!middle.EndsWith(requiredLiteral, StringComparison.Ordinal))
        {
            return false;
        }

        var optionalPart = middle.Substring(0, middle.Length - requiredLiteral.Length);
        if (optionalPart.Length != 0 && optionalPart != optionalGroupPrefix)
        {
            return false;
        }

        prefixUtf8 = "///"u8.ToArray();
        optionalPrefixUtf8 = optionalPart.Length == 0 ? null : "[|"u8.ToArray();
        return true;
    }

    public static bool TryParseLinePrefixCountFamily(string pattern, RegexOptions options, out byte[]? prefixUtf8, out bool trimLeadingAsciiWhitespace)
    {
        prefixUtf8 = null;
        trimLeadingAsciiWhitespace = false;

        if ((options & RegexOptions.Multiline) == 0)
        {
            return false;
        }

        const string prefix = "^\\s*";
        const string suffix = "(?<title>#.*)$";
        if (!pattern.StartsWith(prefix, StringComparison.Ordinal) ||
            !pattern.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var literal = pattern.Substring(prefix.Length, pattern.Length - prefix.Length - suffix.Length);
        if (literal.Length == 0 || !TryDecodeAsciiLiteral(literal + "#", out prefixUtf8))
        {
            prefixUtf8 = null;
            return false;
        }

        trimLeadingAsciiWhitespace = true;
        return true;
    }

    private static bool TryParseAnchoredQuotedLineSegmentPattern(string pattern, out byte[] requiredPrefixUtf8, out byte[]? optionalSegmentUtf8)
    {
        requiredPrefixUtf8 = [];
        optionalSegmentUtf8 = null;

        const string start = "^";
        const string whitespace = "\\s+";
        const string quotedPayload = "\"(?<path>(\\.|[^\\\\\"]*))\"";
        const string rest = ".*$";
        if (!pattern.StartsWith(start, StringComparison.Ordinal) ||
            !pattern.EndsWith(quotedPayload + rest, StringComparison.Ordinal))
        {
            return false;
        }

        var bodyLength = pattern.Length - start.Length - quotedPayload.Length - rest.Length;
        var body = pattern.Substring(start.Length, bodyLength);
        var whitespaceIndex = body.IndexOf(whitespace, StringComparison.Ordinal);
        if (whitespaceIndex <= 0)
        {
            return false;
        }

        if (!TryDecodeAsciiLiteral(body[..whitespaceIndex], out requiredPrefixUtf8))
        {
            requiredPrefixUtf8 = [];
            return false;
        }

        var remainder = body.Substring(whitespaceIndex + whitespace.Length);
        if (remainder.Length == 0)
        {
            return true;
        }

        if (!TryParseOptionalLiteralWhitespaceGroup(remainder, out optionalSegmentUtf8))
        {
            requiredPrefixUtf8 = [];
            optionalSegmentUtf8 = null;
            return false;
        }

        return true;
    }

    private static bool TryParseOptionalLiteralWhitespaceGroup(string pattern, out byte[]? optionalSegmentUtf8)
    {
        optionalSegmentUtf8 = null;
        if (!pattern.StartsWith("(?<", StringComparison.Ordinal) ||
            !pattern.EndsWith("\\s+)?", StringComparison.Ordinal))
        {
            return false;
        }

        var closeIndex = pattern.IndexOf('>');
        if (closeIndex < 3)
        {
            return false;
        }

        var literalStart = closeIndex + 1;
        var literalLength = pattern.Length - literalStart - "\\s+)?".Length;
        if (literalLength <= 0)
        {
            return false;
        }

        return TryDecodeAsciiLiteral(pattern.Substring(literalStart, literalLength), out optionalSegmentUtf8!);
    }

    private static bool TryReadSingleAsciiLiteralAlternationOrDollar(string pattern, ref int index, out byte[] separatorBytesUtf8)
    {
        separatorBytesUtf8 = [];
        var original = index;
        var usedWrapper = false;
        if (TryConsume(pattern, ref index, "("))
        {
            usedWrapper = true;
        }
        else if (TryConsume(pattern, ref index, "(?:"))
        {
            usedWrapper = true;
        }

        Span<byte> separators = stackalloc byte[8];
        var count = 0;
        var sawDollar = false;
        while (true)
        {
            if ((uint)index >= (uint)pattern.Length)
            {
                index = original;
                separatorBytesUtf8 = [];
                return false;
            }

            if (pattern[index] == '$')
            {
                sawDollar = true;
                index++;
            }
            else
            {
                if (!TryReadSingleAsciiLiteralBranch(pattern, ref index, out var literalByte))
                {
                    index = original;
                    separatorBytesUtf8 = [];
                    return false;
                }

                if (count >= separators.Length)
                {
                    index = original;
                    separatorBytesUtf8 = [];
                    return false;
                }

                separators[count++] = literalByte;
            }

            if ((uint)index >= (uint)pattern.Length || pattern[index] != '|')
            {
                break;
            }

            index++;
        }

        if (usedWrapper && !TryConsume(pattern, ref index, ")"))
        {
            index = original;
            separatorBytesUtf8 = [];
            return false;
        }

        if (!sawDollar && count == 0)
        {
            index = original;
            separatorBytesUtf8 = [];
            return false;
        }

        separatorBytesUtf8 = separators[..count].ToArray();
        return true;
    }

    private static bool TryReadSingleAsciiLiteralBranch(string pattern, ref int index, out byte literalByte)
    {
        literalByte = 0;
        if ((uint)index >= (uint)pattern.Length)
        {
            return false;
        }

        char ch;
        if (pattern[index] == '\\')
        {
            index++;
            if ((uint)index >= (uint)pattern.Length)
            {
                return false;
            }

            ch = pattern[index++];
        }
        else
        {
            ch = pattern[index++];
        }

        if (ch is '|' or ')' || ch > 0x7F)
        {
            return false;
        }

        literalByte = (byte)ch;
        return true;
    }

    private static bool TryParseAnchoredClassPlusStar(string pattern, out string headClass, out string tailClass)
    {
        headClass = string.Empty;
        tailClass = string.Empty;
        if (!pattern.StartsWith(@"\G[", StringComparison.Ordinal))
        {
            return false;
        }

        var index = 2;
        if (!TryReadCharClass(pattern, ref index, out headClass))
        {
            return false;
        }

        if (!TryReadCharClass(pattern, ref index, out tailClass))
        {
            return false;
        }

        return index == pattern.Length - 1 && pattern[index] == '*';
    }

    private static bool TryParseAnchoredClassPlus(string pattern, out string @class)
    {
        @class = string.Empty;
        if (!pattern.StartsWith(@"\G[", StringComparison.Ordinal))
        {
            return false;
        }

        var index = 2;
        if (!TryReadCharClass(pattern, ref index, out @class))
        {
            return false;
        }

        return index == pattern.Length - 1 && pattern[index] == '+';
    }

    private static bool TryParseUnanchoredClassPlusStar(string pattern, out string headClass, out string tailClass)
    {
        headClass = string.Empty;
        tailClass = string.Empty;

        var index = 0;
        if (!TryReadCharClass(pattern, ref index, out headClass))
        {
            return false;
        }

        if (!TryReadCharClass(pattern, ref index, out tailClass))
        {
            return false;
        }

        return index == pattern.Length - 1 && pattern[index] == '*';
    }

    private static string CompactRegexWhitespace(string pattern)
    {
        if (pattern.Length == 0)
        {
            return pattern;
        }

        var builder = new System.Text.StringBuilder(pattern.Length);
        var escaping = false;
        foreach (var ch in pattern)
        {
            if (escaping)
            {
                builder.Append(ch);
                escaping = false;
                continue;
            }

            if (ch == '\\')
            {
                builder.Append(ch);
                escaping = true;
                continue;
            }

            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
