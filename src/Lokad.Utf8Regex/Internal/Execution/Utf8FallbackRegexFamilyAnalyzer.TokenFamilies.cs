using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    public static bool TryParseAsciiIdentifierToken(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        return options == RegexOptions.None &&
            TryParseUnanchoredClassPlusStar(pattern, out var headClass, out var tailClass) &&
            IsAsciiLetterClass(headClass) &&
            HasExactChars(tailClass, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
    }

    public static bool IsAsciiDottedDecimalQuadCount(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        const string octet = "(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        return options == RegexOptions.None &&
            pattern == $"(?:{octet}\\.){{3}}{octet}";
    }

    public static bool TryParseAsciiDelimitedTokenCount(
        string pattern,
        RegexOptions options,
        out byte[]? headCharSetUtf8,
        out byte[]? delimiterUtf8,
        out byte[]? middleCharSetUtf8,
        out byte[]? secondaryDelimiterUtf8,
        out byte[]? tailCharSetUtf8)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        headCharSetUtf8 = null;
        delimiterUtf8 = null;
        middleCharSetUtf8 = null;
        secondaryDelimiterUtf8 = null;
        tailCharSetUtf8 = null;

        if (options != RegexOptions.None)
        {
            return false;
        }

        var index = 0;
        if (!TryReadCharClass(pattern, ref index, out var headClass) ||
            !TryConsume(pattern, ref index, "+") ||
            !TryReadAsciiLiteral(pattern, ref index, out delimiterUtf8) ||
            !TryReadCharClass(pattern, ref index, out var middleClass) ||
            !TryConsume(pattern, ref index, "+") ||
            !TryReadAsciiLiteral(pattern, ref index, out secondaryDelimiterUtf8) ||
            !TryReadCharClass(pattern, ref index, out var tailClass) ||
            !TryConsume(pattern, ref index, "+") ||
            index != pattern.Length)
        {
            return false;
        }

        headCharSetUtf8 = EncodeAsciiCharSet(headClass);
        middleCharSetUtf8 = EncodeAsciiCharSet(middleClass);
        tailCharSetUtf8 = EncodeAsciiCharSet(tailClass);
        return delimiterUtf8 is { Length: > 0 } &&
            secondaryDelimiterUtf8 is { Length: > 0 } &&
            headCharSetUtf8.Length > 0 &&
            middleCharSetUtf8.Length > 0 &&
            tailCharSetUtf8.Length > 0;
    }

    public static bool TryParseAsciiLiteralStructuredTokenCount(
        string pattern,
        RegexOptions options,
        out byte[]? headCharSetUtf8,
        out byte[]? literalUtf8,
        out byte[]? firstBodyCharSetUtf8,
        out byte[]? secondBodyCharSetUtf8,
        out byte[]? optionalDelimiterUtf8,
        out byte[]? optionalTailCharSetUtf8,
        out byte[]? finalDelimiterUtf8,
        out byte[]? finalTailCharSetUtf8)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        headCharSetUtf8 = null;
        literalUtf8 = null;
        firstBodyCharSetUtf8 = null;
        secondBodyCharSetUtf8 = null;
        optionalDelimiterUtf8 = [];
        optionalTailCharSetUtf8 = [];
        finalDelimiterUtf8 = [];
        finalTailCharSetUtf8 = [];

        if (options != RegexOptions.None)
        {
            return false;
        }

        var index = 0;
        if (!TryReadAsciiCharSetBytes(pattern, ref index, out headCharSetUtf8) ||
            !TryConsume(pattern, ref index, "+") ||
            !TryReadAsciiLiteral(pattern, ref index, out literalUtf8) ||
            !TryReadAsciiCharSetBytes(pattern, ref index, out firstBodyCharSetUtf8) ||
            !TryConsume(pattern, ref index, "+") ||
            !TryReadAsciiCharSetBytes(pattern, ref index, out secondBodyCharSetUtf8) ||
            !TryConsume(pattern, ref index, "+"))
        {
            return false;
        }

        if (!TryConsumeOptionalLiteralAndStarClass(pattern, ref index, out optionalDelimiterUtf8, out optionalTailCharSetUtf8) ||
            !TryConsumeOptionalLiteralAndStarClass(pattern, ref index, out finalDelimiterUtf8, out finalTailCharSetUtf8) ||
            index != pattern.Length)
        {
            return false;
        }

        return literalUtf8 is { Length: > 0 } &&
            headCharSetUtf8 is { Length: > 0 } &&
            firstBodyCharSetUtf8 is { Length: > 0 } &&
            secondBodyCharSetUtf8 is { Length: > 0 };
    }

    public static bool TryParseAsciiLiteralBetweenNegatedRuns(
        string pattern,
        RegexOptions options,
        out byte[]? literalUtf8,
        out byte excludedHeadByte,
        out byte excludedTailByte)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        literalUtf8 = null;
        excludedHeadByte = 0;
        excludedTailByte = 0;
        if (options != RegexOptions.None)
        {
            return false;
        }

        var index = 0;
        if (!TryReadSingleByteNegatedCharClassPlus(pattern, ref index, out excludedHeadByte) ||
            !TryReadAsciiLiteral(pattern, ref index, out literalUtf8) ||
            literalUtf8 is not { Length: 1 } ||
            !TryReadSingleByteNegatedCharClassPlus(pattern, ref index, out excludedTailByte) ||
            index != pattern.Length)
        {
            literalUtf8 = null;
            excludedHeadByte = 0;
            excludedTailByte = 0;
            return false;
        }

        return true;
    }

    public static bool TryParseAsciiIpv4Token(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        return options == RegexOptions.None &&
            pattern == @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
    }

    public static bool TryParseAsciiBoundedDateToken(
        string pattern,
        RegexOptions options,
        out byte firstFieldMinCount,
        out byte firstFieldMaxCount,
        out byte secondFieldMinCount,
        out byte secondFieldMaxCount,
        out byte thirdFieldMinCount,
        out byte thirdFieldMaxCount,
        out byte separatorByte,
        out byte secondSeparatorByte,
        out bool requireLeadingBoundary,
        out bool requireTrailingBoundary)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        firstFieldMinCount = 0;
        firstFieldMaxCount = 0;
        secondFieldMinCount = 0;
        secondFieldMaxCount = 0;
        thirdFieldMinCount = 0;
        thirdFieldMaxCount = 0;
        separatorByte = 0;
        secondSeparatorByte = 0;
        requireLeadingBoundary = false;
        requireTrailingBoundary = false;

        if (options != RegexOptions.None)
        {
            return false;
        }

        var index = 0;
        requireLeadingBoundary = TryConsume(pattern, ref index, "\\b");
        if (!TryConsumeDigitBound(pattern, ref index, out firstFieldMinCount, out firstFieldMaxCount) ||
            !TryConsumeAsciiDateSeparator(pattern, ref index, out separatorByte) ||
            !TryConsumeDigitBound(pattern, ref index, out secondFieldMinCount, out secondFieldMaxCount) ||
            !TryConsumeAsciiDateSeparator(pattern, ref index, out secondSeparatorByte) ||
            !TryConsumeDigitBound(pattern, ref index, out thirdFieldMinCount, out thirdFieldMaxCount))
        {
            return false;
        }

        requireTrailingBoundary = TryConsume(pattern, ref index, "\\b");
        return index == pattern.Length;
    }

    public static bool TryParseAsciiUriToken(string pattern, RegexOptions options)
    {
        options = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        return options == RegexOptions.None &&
            pattern == @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
    }

    private static bool TryConsumeDigitBound(string pattern, ref int index, out byte minCount, out byte maxCount)
    {
        minCount = 0;
        maxCount = 0;
        if (!TryConsume(pattern, ref index, "\\d{"))
        {
            return false;
        }

        var start = index;
        while (index < pattern.Length && char.IsAsciiDigit(pattern[index]))
        {
            index++;
        }

        if (index == start || !byte.TryParse(pattern[start..index], out minCount))
        {
            return false;
        }

        if (TryConsume(pattern, ref index, "}"))
        {
            maxCount = minCount;
            return true;
        }

        if (!TryConsume(pattern, ref index, ","))
        {
            return false;
        }

        start = index;
        while (index < pattern.Length && char.IsAsciiDigit(pattern[index]))
        {
            index++;
        }

        return index > start &&
            byte.TryParse(pattern[start..index], out maxCount) &&
            TryConsume(pattern, ref index, "}");
    }

    private static bool TryConsumeAsciiDateSeparator(string pattern, ref int index, out byte separatorByte)
    {
        separatorByte = 0;
        if (index >= pattern.Length)
        {
            return false;
        }

        if (pattern[index] == '\\')
        {
            if (index + 2 > pattern.Length)
            {
                return false;
            }

            separatorByte = pattern[index + 1] switch
            {
                '/' => (byte)'/',
                '-' => (byte)'-',
                '.' => (byte)'.',
                _ => (byte)0,
            };

            if (separatorByte == 0)
            {
                return false;
            }

            index += 2;
            return true;
        }

        separatorByte = pattern[index] switch
        {
            '/' => (byte)'/',
            '-' => (byte)'-',
            '.' => (byte)'.',
            _ => (byte)0,
        };

        if (separatorByte == 0)
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool TryReadAsciiCharSetBytes(string pattern, ref int index, out byte[]? charSetUtf8)
    {
        charSetUtf8 = null;
        if ((uint)index >= (uint)pattern.Length || pattern[index] != '[')
        {
            return false;
        }

        index++;
        var negate = false;
        if ((uint)index < (uint)pattern.Length && pattern[index] == '^')
        {
            negate = true;
            index++;
        }

        Span<bool> present = stackalloc bool[128];
        while ((uint)index < (uint)pattern.Length)
        {
            if (pattern[index] == ']')
            {
                index++;
                if (negate)
                {
                    for (var i = 0; i < present.Length; i++)
                    {
                        present[i] = !present[i];
                    }
                }

                charSetUtf8 = EncodeAsciiCharSet(BuildAsciiCharSet(present));
                return charSetUtf8.Length > 0;
            }

            if ((uint)index + 1 < (uint)pattern.Length &&
                pattern[index] == '\\' &&
                pattern[index + 1] == 'w')
            {
                index += 2;
                AddAsciiWordChars(present);
                continue;
            }

            if ((uint)index + 1 < (uint)pattern.Length &&
                pattern[index] == '\\' &&
                pattern[index + 1] == 's')
            {
                index += 2;
                AddAsciiWhitespace(present);
                continue;
            }

            if (!TryReadClassAtom(pattern, ref index, out var first))
            {
                return false;
            }

            if ((uint)index + 1 < (uint)pattern.Length && pattern[index] == '-' && pattern[index + 1] != ']')
            {
                index++;
                if (!TryReadClassAtom(pattern, ref index, out var last) || first > last || last >= 128)
                {
                    return false;
                }

                for (var ch = first; ch <= last; ch++)
                {
                    present[ch] = true;
                }
            }
            else
            {
                if (first >= 128)
                {
                    return false;
                }

                present[first] = true;
            }
        }

        return false;
    }

    private static bool TryConsumeOptionalLiteralAndStarClass(string pattern, ref int index, out byte[]? delimiterUtf8, out byte[]? charSetUtf8)
    {
        delimiterUtf8 = [];
        charSetUtf8 = [];
        var original = index;
        if (!TryConsume(pattern, ref index, "(?:"))
        {
            return true;
        }

        if (!TryReadAsciiLiteral(pattern, ref index, out delimiterUtf8) ||
            delimiterUtf8 is not { Length: 1 } ||
            !TryReadAsciiCharSetBytes(pattern, ref index, out charSetUtf8) ||
            !TryConsume(pattern, ref index, "*") ||
            !TryConsume(pattern, ref index, ")?"))
        {
            index = original;
            delimiterUtf8 = [];
            charSetUtf8 = [];
            return false;
        }

        return true;
    }

    private static bool TryReadSingleByteNegatedCharClassPlus(string pattern, ref int index, out byte excludedByte)
    {
        excludedByte = 0;
        var original = index;
        if (!TryConsume(pattern, ref index, "[^"))
        {
            return false;
        }

        if (!TryReadSingleAsciiLiteralBranch(pattern, ref index, out excludedByte) ||
            !TryConsume(pattern, ref index, "]") ||
            !TryConsume(pattern, ref index, "+"))
        {
            index = original;
            excludedByte = 0;
            return false;
        }

        return true;
    }

    private static bool TryReadAsciiLiteral(string pattern, ref int index, out byte[]? literalUtf8)
    {
        literalUtf8 = null;
        Span<byte> scratch = stackalloc byte[8];
        var count = 0;

        while ((uint)index < (uint)pattern.Length && pattern[index] != '[')
        {
            if (count >= scratch.Length)
            {
                return false;
            }

            if (pattern[index] == '\\')
            {
                index++;
                if ((uint)index >= (uint)pattern.Length)
                {
                    return false;
                }
            }

            var ch = pattern[index++];
            if (ch > 0x7F)
            {
                return false;
            }

            scratch[count++] = (byte)ch;
        }

        if (count == 0)
        {
            return false;
        }

        literalUtf8 = scratch[..count].ToArray();
        return true;
    }
}
