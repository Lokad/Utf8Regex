using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2;

// Native PCRE2 reads patterns lexically. Guards skip classes, quotes, comments,
// escapes and recognized literal verb labels. Regex-body constructs still need
// validation. Extended comments and inline option scopes use the same helpers
// as declaration and structural admission.
internal static class Pcre2PatternCodeScan
{
    public static bool ContainsCodeText(string pattern, string text,
        Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        return Scan(pattern, text, false, options, newline);
    }

    public static bool ContainsNonClassCodeText(string pattern, string text,
        Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        return Scan(pattern, text, true, options, newline);
    }

    private static bool Scan(string pattern, string text, bool matchInsideClass,
        Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        // Admit the full lexical extent, including skipped quotes/comments.
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
        var quoted = false;
        var inCharacterClass = false;
        var classHasAtom = false;
        var classStart = false;
        byte extended = (options & Pcre2CompileOptions.ExtendedMore) != 0 ? (byte)2 : (options & Pcre2CompileOptions.Extended) != 0 ? (byte)1 : (byte)0;
        var scopes = new Stack<byte>();
        for (var offset = 0; offset < pattern.Length; offset++)
        {
            var character = pattern[offset];
            if (quoted)
            {
                if (character == '\\' && offset + 1 < pattern.Length && pattern[offset + 1] == 'E')
                {
                    quoted = false;
                    offset++;
                }
                else if (inCharacterClass) { classHasAtom = true; classStart = false; }

                continue;
            }

            if (inCharacterClass)
            {
                if (extended == 2 && character is ' ' or '\t') continue;
                if (character == '\\' && offset + 1 < pattern.Length)
                {
                    if (pattern[offset + 1] is 'Q' or 'E')
                    {
                        quoted = pattern[offset + 1] == 'Q';
                        offset++;
                        continue;
                    }
                    if (matchInsideClass && pattern.AsSpan(offset).StartsWith(text, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    offset++;
                    classHasAtom = true;
                    classStart = false;
                    continue;
                }

                if (classStart && character == '^') { classStart = false; continue; }
                classStart = false;
                if (character == ']' && classHasAtom)
                {
                    inCharacterClass = false;
                    classHasAtom = false;
                    continue;
                }

                if (matchInsideClass && pattern.AsSpan(offset).StartsWith(text, StringComparison.Ordinal))
                {
                    return true;
                }

                classHasAtom = true;
                if (character == '[') TrySkipPosixClassToken(pattern, ref offset);
                continue;
            }

            if (TrySkipExtendedComment(pattern, ref offset, extended != 0, newline)) continue;

            if (character == '\\')
            {
                if (pattern.AsSpan(offset).StartsWith(text, StringComparison.Ordinal))
                {
                    return true;
                }

                if (offset + 1 >= pattern.Length)
                {
                    return false;
                }

                if (pattern[offset + 1] == 'Q')
                {
                    quoted = true;
                }

                offset++;
                continue;
            }

            if (character == '[')
            {
                if (pattern.AsSpan(offset).StartsWith(text, StringComparison.Ordinal))
                {
                    return true;
                }

                inCharacterClass = true;
                classHasAtom = false;
                classStart = true;
                continue;
            }

            if (character == '(' && pattern.AsSpan(offset).StartsWith("(?#", StringComparison.Ordinal))
            {
                var commentEnd = pattern.IndexOf(')', offset + 3);
                if (commentEnd < 0)
                {
                    return false;
                }

                offset = commentEnd;
                continue;
            }

            if (character == '(' && TrySkipControlVerbLabel(pattern, ref offset))
            {
                continue;
            }

            if (pattern.AsSpan(offset).StartsWith(text, StringComparison.Ordinal))
            {
                return true;
            }

            if (character == '(') EnterExtendedGroup(pattern, ref offset, ref extended, scopes);
            else if (character == ')' && scopes.TryPop(out var previous)) extended = previous;
        }

        return false;
    }

    // Call outside classes, quotes, escapes and literal verb labels. Leave the
    // last consumed character to the caller; no comment or terminating newline
    // contents become syntax. Callers advance once after a successful skip.
    internal static bool TrySkipExtendedComment(string pattern, ref int offset, bool extended, Pcre2NewlineConvention newline)
    {
        if (!extended || pattern[offset] != '#') return false;
        var start = offset;
        for (; offset < pattern.Length; offset++)
        {
            if (((offset - start) & 2047) == 0)
                Utf8ConstructionScope.ChargeWork((ulong)Math.Min(2048, pattern.Length - offset));
            var character = pattern[offset];
            var atNewline = newline switch
            {
                Pcre2NewlineConvention.Cr => character == '\r',
                Pcre2NewlineConvention.Crlf => character == '\r' && offset + 1 < pattern.Length && pattern[offset + 1] == '\n',
                Pcre2NewlineConvention.Any => character is '\r' or '\n' or '\v' or '\f' or '\u0085' or '\u2028' or '\u2029',
                Pcre2NewlineConvention.AnyCrlf => character is '\r' or '\n',
                Pcre2NewlineConvention.Nul => character == '\0',
                _ => character == '\n',
            };
            if (!atNewline) continue;
            if (character == '\r' && offset + 1 < pattern.Length && pattern[offset + 1] == '\n' &&
                newline is Pcre2NewlineConvention.Crlf or Pcre2NewlineConvention.Any or Pcre2NewlineConvention.AnyCrlf)
                offset++;
            return true;
        }
        offset--; // Every caller advances once at the end of its for loop.
        return true;
    }

    // Mode 0 disables extended syntax, 1 is x, 2 is xx (also ignores horizontal
    // class whitespace). Return true for a global directive, which opens no group. Scoped
    // directives and ordinary groups save the inherited mode for their ')'.
    internal static bool EnterExtendedGroup(string pattern, ref int offset, ref byte extended, Stack<byte> scopes)
    {
        if (pattern.AsSpan(offset).StartsWith("(?", StringComparison.Ordinal))
        {
            var end = offset + 2;
            var next = extended;
            var enable = true;
            var enabledX = false;
            var enabledMore = false;
            var disabledX = false;
            var reset = end < pattern.Length && pattern[end] == '^';
            if (reset) { next = 0; end++; }
            var start = end;
            for (; end < pattern.Length; end++)
            {
                var flag = pattern[end];
                if (flag == '-') { enable = false; continue; }
                if (!char.IsAsciiLetter(flag)) break;
                if (flag == 'x')
                {
                    var doubled = end + 1 < pattern.Length && pattern[end + 1] == 'x';
                    if (enable) { enabledX = true; enabledMore |= doubled; }
                    else disabledX = true;
                    if (doubled) end++;
                }
            }
            if ((end > start || reset) && end < pattern.Length && pattern[end] is ':' or ')')
            {
                if (enabledX) next = enabledMore ? (byte)2 : (byte)1;
                if (disabledX) next = 0;
                var global = pattern[end] == ')';
                if (!global) scopes.Push(extended);
                extended = next;
                offset = end;
                return global;
            }
        }
        scopes.Push(extended);
        return false;
    }

    internal static bool TrySkipPosixClassToken(string pattern, ref int offset)
    {
        if (pattern[offset] != '[' || offset + 1 >= pattern.Length || pattern[offset + 1] is not (':' or '.' or '=')) return false;
        var marker = pattern[offset + 1];
        for (var end = offset + 2; end + 1 < pattern.Length; end++)
        {
            if (((end - offset - 2) & 2047) == 0)
                Utf8ConstructionScope.ChargeWork((ulong)Math.Min(2048, pattern.Length - end - 1));
            if (pattern[end] == marker && pattern[end + 1] == ']') { offset = end + 1; return true; }
        }
        return false;
    }

    // Offset enters at '(' and, on success, leaves at the closing ')'. The
    // selected profile has no ALT_VERBNAMES: a label is unprocessed text. Do not
    // skip regex-body spellings such as (*atomic:...), even if unserved elsewhere.
    internal static bool TrySkipControlVerbLabel(string pattern, ref int offset)
    {
        if (!pattern.AsSpan(offset).StartsWith("(*", StringComparison.Ordinal)) return false;
        var end = FindVerbEnd(pattern, offset + 2);
        if (end < 0) return false;
        var content = pattern.AsSpan(offset + 2, end - offset - 2);
        Utf8ConstructionScope.ChargeWork((ulong)content.Length);
        var colon = content.IndexOf(':');
        var verb = colon < 0 ? content : content[..colon];
        if (!verb.IsEmpty && !verb.Equals("MARK", StringComparison.OrdinalIgnoreCase) &&
            !verb.Equals("PRUNE", StringComparison.OrdinalIgnoreCase) && !verb.Equals("THEN", StringComparison.OrdinalIgnoreCase) &&
            !verb.Equals("SKIP", StringComparison.OrdinalIgnoreCase) && !verb.Equals("COMMIT", StringComparison.OrdinalIgnoreCase) &&
            !verb.Equals("FAIL", StringComparison.OrdinalIgnoreCase) && !verb.Equals("F", StringComparison.OrdinalIgnoreCase) &&
            !verb.Equals("ACCEPT", StringComparison.OrdinalIgnoreCase)) return false;
        offset = end;
        return true;
    }

    private static int FindVerbEnd(string pattern, int start)
    {
        // Unknown or unterminated verbs can revisit suffixes. Bound each search
        // under the original construction owner while keeping SIMD IndexOf.
        for (var offset = start; offset < pattern.Length;)
        {
            var count = Math.Min(2048, pattern.Length - offset);
            Utf8ConstructionScope.ChargeWork((ulong)count);
            var end = pattern.AsSpan(offset, count).IndexOf(')');
            if (end >= 0) return offset + end;
            offset += count;
        }
        return -1;
    }
}
