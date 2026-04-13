using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    private static bool TryConsumeOptionalGroup(string pattern, ref int index, params string[] tokens)
    {
        var original = index;
        if (!TryConsume(pattern, ref index, "(") && !TryConsume(pattern, ref index, "(?:"))
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (!TryConsume(pattern, ref index, token))
            {
                index = original;
                return false;
            }
        }

        if (!TryConsume(pattern, ref index, ")?"))
        {
            index = original;
            return false;
        }

        return true;
    }

    private static bool TryConsume(string pattern, ref int index, string token)
    {
        if ((uint)(pattern.Length - index) < (uint)token.Length ||
            !pattern.AsSpan(index, token.Length).SequenceEqual(token))
        {
            return false;
        }

        index += token.Length;
        return true;
    }

    private static bool TryConsumeCapturedOrRaw(string pattern, ref int index, string token)
    {
        return TryConsume(pattern, ref index, token) ||
            TryConsume(pattern, ref index, $"({token})") ||
            TryConsume(pattern, ref index, $"(?:{token})");
    }

    private static bool TryReadCharClass(string pattern, ref int index, out string chars)
    {
        chars = string.Empty;
        if ((uint)index >= (uint)pattern.Length || pattern[index] != '[')
        {
            return false;
        }

        index++;
        Span<bool> present = stackalloc bool[128];
        while ((uint)index < (uint)pattern.Length)
        {
            if (pattern[index] == ']')
            {
                index++;
                chars = BuildAsciiCharSet(present);
                return chars.Length > 0;
            }

            if ((uint)index + 1 < (uint)pattern.Length &&
                pattern[index] == '\\' &&
                pattern[index + 1] == 'w')
            {
                index += 2;
                AddAsciiWordChars(present);
                continue;
            }

            if (!TryReadClassAtom(pattern, ref index, out var first))
            {
                return false;
            }

            if ((uint)index + 1 < (uint)pattern.Length && pattern[index] == '-' && pattern[index + 1] != ']')
            {
                index++;
                if (!TryReadClassAtom(pattern, ref index, out var last) || first > last)
                {
                    return false;
                }

                for (var ch = first; ch <= last; ch++)
                {
                    if (ch >= 128)
                    {
                        return false;
                    }

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

    private static bool TryReadClassAtom(string pattern, ref int index, out char ch)
    {
        ch = '\0';
        if ((uint)index >= (uint)pattern.Length)
        {
            return false;
        }

        ch = pattern[index++];
        if (ch != '\\')
        {
            return true;
        }

        if ((uint)index >= (uint)pattern.Length)
        {
            return false;
        }

        ch = pattern[index++];
        return true;
    }

    private static bool IsAsciiLetterClass(string chars, bool allowLowercaseOnly = false)
    {
        return HasExactChars(chars, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ") ||
            (allowLowercaseOnly && HasExactChars(chars, "abcdefghijklmnopqrstuvwxyz"));
    }

    private static bool IsAsciiIgnoreCaseOptionSet(RegexOptions options)
    {
        return options == RegexOptions.IgnoreCase ||
               options == (RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasExactChars(string actual, string expected)
    {
        if (actual.Length != expected.Length)
        {
            return false;
        }

        Span<bool> present = stackalloc bool[128];
        foreach (var ch in actual)
        {
            present[ch] = true;
        }

        foreach (var ch in expected)
        {
            if (!present[ch])
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] EncodeAsciiCharSet(string chars)
    {
        var bytes = new byte[chars.Length];
        for (var i = 0; i < chars.Length; i++)
        {
            bytes[i] = (byte)chars[i];
        }

        return bytes;
    }

    private static string BuildAsciiCharSet(ReadOnlySpan<bool> present)
    {
        var chars = new char[CountSet(present)];
        var index = 0;
        for (var i = 0; i < present.Length; i++)
        {
            if (present[i])
            {
                chars[index++] = (char)i;
            }
        }

        return new string(chars);
    }

    private static void AddAsciiWordChars(Span<bool> present)
    {
        for (var ch = (byte)'0'; ch <= (byte)'9'; ch++)
        {
            present[ch] = true;
        }

        for (var ch = (byte)'A'; ch <= (byte)'Z'; ch++)
        {
            present[ch] = true;
        }

        for (var ch = (byte)'a'; ch <= (byte)'z'; ch++)
        {
            present[ch] = true;
        }

        present[(byte)'_'] = true;
    }

    private static void AddAsciiWhitespace(Span<bool> present)
    {
        present[(byte)' '] = true;
        present[(byte)'\t'] = true;
        present[(byte)'\n'] = true;
        present[(byte)'\r'] = true;
        present[(byte)'\f'] = true;
        present[(byte)'\v'] = true;
    }

    private static int CountSet(ReadOnlySpan<bool> present)
    {
        var count = 0;
        foreach (var value in present)
        {
            if (value)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryDecodeAsciiLiteral(string pattern, out byte[] bytes)
    {
        bytes = [];
        var buffer = new byte[pattern.Length];
        var count = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch == '\\')
            {
                i++;
                if ((uint)i >= (uint)pattern.Length)
                {
                    bytes = [];
                    return false;
                }

                ch = pattern[i] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    _ => pattern[i],
                };
            }

            if (ch > 0x7F)
            {
                bytes = [];
                return false;
            }

            buffer[count++] = (byte)ch;
        }

        bytes = buffer[..count];
        return count > 0;
    }

    private static bool TryParseLeadingAnyRunTrailingAsciiLiteral(string pattern, RegexOptions options, out byte[] literalUtf8)
    {
        literalUtf8 = [];
        if (options != RegexOptions.None && options != RegexOptions.CultureInvariant)
        {
            return false;
        }

        var index = 0;
        if (!TryConsume(pattern, ref index, ".*"))
        {
            return false;
        }

        string literalPattern;
        if (TryConsume(pattern, ref index, "("))
        {
            if (pattern.Length == 0 || pattern[^1] != ')' || index >= pattern.Length - 1)
            {
                return false;
            }

            literalPattern = pattern[index..^1];
        }
        else if (TryConsume(pattern, ref index, "(?:"))
        {
            if (pattern.Length == 0 || pattern[^1] != ')' || index >= pattern.Length - 1)
            {
                return false;
            }

            literalPattern = pattern[index..^1];
        }
        else
        {
            literalPattern = pattern[index..];
        }

        return TryDecodeStrictAsciiRegexLiteral(literalPattern, out literalUtf8);
    }

    private static bool TryDecodeStrictAsciiRegexLiteral(string pattern, out byte[] bytes)
    {
        bytes = [];
        if (pattern.Length == 0)
        {
            return false;
        }

        var buffer = new byte[pattern.Length];
        var count = 0;
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch == '\\')
            {
                i++;
                if ((uint)i >= (uint)pattern.Length)
                {
                    bytes = [];
                    return false;
                }

                ch = pattern[i];
                if (!IsEscapedAsciiRegexLiteral(ch))
                {
                    bytes = [];
                    return false;
                }
            }
            else if (IsRegexMetaChar(ch))
            {
                bytes = [];
                return false;
            }

            if (ch > 0x7F)
            {
                bytes = [];
                return false;
            }

            buffer[count++] = (byte)ch;
        }

        bytes = buffer[..count];
        return count > 0;
    }

    private static bool IsEscapedAsciiRegexLiteral(char ch)
        => ch is '\\' or '.' or '+' or '*' or '?' or '|' or '(' or ')' or '[' or ']' or '{' or '}' or '^' or '$';

    private static bool IsRegexMetaChar(char ch)
        => ch is '\\' or '.' or '+' or '*' or '?' or '|' or '(' or ')' or '[' or ']' or '{' or '}' or '^' or '$';
}
