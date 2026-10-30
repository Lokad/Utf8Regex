using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2;

// Native PCRE2 reads patterns lexically. Guards skip classes, quotes, comments,
// escapes and recognized literal verb labels. Regex-body constructs still need
// validation. Extended comments and inline option scopes use the same helpers
// as declaration and structural admission.
internal static class Pcre2PatternCodeScan
{
    public static bool ContainsCodeText(string pattern, string text,
        Pcre2CompileOptions options, Pcre2NewlineConvention newline)
        => ContainsCodeText(new Utf8ConstructionPattern(pattern), text, options, newline);

    public static bool ContainsCodeText(Utf8ConstructionPattern pattern, string text,
        Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        // Admit the full lexical extent, including skipped quotes/comments.
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        budget.ChargeWork((ulong)pattern.Length);
        var quoted = false;
        var inCharacterClass = false;
        var classHasAtom = false;
        var classStart = false;
        byte extended = (options & Pcre2CompileOptions.ExtendedMore) != 0 ? (byte)2 : (options & Pcre2CompileOptions.Extended) != 0 ? (byte)1 : (byte)0;
        using var scopes = new Utf8ConstructionBuffer<byte>(budget);
        var nextCheckpoint = 0;
        for (var offset = 0; offset < pattern.Length; offset++)
        {
            if (offset >= nextCheckpoint)
            {
                budget.ChargeWork(0);
                nextCheckpoint = offset + 2048;
            }
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
                    offset++;
                    SkipEscapeOperand(pattern, ref offset, pattern[offset]);
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

                classHasAtom = true;
                if (character == '[') TrySkipPosixClassToken(pattern, ref offset);
                continue;
            }

            if (TrySkipExtendedComment(pattern, ref offset, extended != 0, newline)) continue;

            if (character == '\\')
            {
                if (pattern.StartsWithAscii(offset, text))
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
                SkipEscapeOperand(pattern, ref offset, pattern[offset]);
                continue;
            }

            if (character == '[')
            {
                if (pattern.StartsWithAscii(offset, text))
                {
                    return true;
                }

                inCharacterClass = true;
                classHasAtom = false;
                classStart = true;
                continue;
            }

            if (character == '(' && pattern.StartsWithAscii(offset, "(?#"))
            {
                var commentEnd = Pcre2PatternCodeScan.FindClosingParenthesis(pattern, offset + 3);
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

            if (pattern.StartsWithAscii(offset, text))
            {
                return true;
            }

            if (character == '(') EnterExtendedGroup(pattern, ref offset, ref extended, scopes);
            else if (character == ')' && scopes.TryPop(out var previous)) extended = previous;
        }

        return false;
    }

    // Offset points at the escape letter, and leaves at the last operand unit.
    // Quotes are handled by the caller; payload characters never become regex
    // syntax. The actual parser still validates the operand's meaning.
    internal static void SkipEscapeOperand(Utf8ConstructionPattern pattern, ref int offset, char escape)
    {
        if (escape == 'c' && offset + 1 < pattern.Length) { offset++; return; }
        if (escape is not ('x' or 'o' or 'g' or 'k' or 'p' or 'P' or 'N') || offset + 1 >= pattern.Length) return;
        var opening = pattern[offset + 1];
        var closing = opening == '{' ? '}' : escape is 'g' or 'k' && opening == '<' ? '>' :
            escape is 'g' or 'k' && opening == '\'' ? '\'' : '\0';
        if (closing == '\0') return;
        // Malformed operands can revisit suffixes. Admit/check bounded blocks
        // even though the outer lexical pass already admits its text extent.
        for (var cursor = offset + 2; cursor < pattern.Length;)
        {
            var count = Math.Min(2048, pattern.Length - cursor);
            Utf8ConstructionScope.ChargeWork((ulong)count);
            var end = pattern.IndexOf(closing, cursor, count);
            if (end >= 0) { offset = end; return; }
            cursor += count;
        }
    }

    // Call outside classes, quotes, escapes and literal verb labels. Leave the
    // last consumed character to the caller; no comment or terminating newline
    // contents become syntax. Callers advance once after a successful skip.
    internal static bool TrySkipExtendedComment(Utf8ConstructionPattern pattern, ref int offset, bool extended, Pcre2NewlineConvention newline)
    {
        if (!extended || pattern[offset] != '#') return false;
        var start = offset;
        for (; offset < pattern.Length; offset++)
        {
            if (((offset - start) & 2047) == 0)
                Utf8ConstructionScope.ChargeWork((ulong)Math.Min(2048, pattern.Length - offset));
            var character = (int)pattern[offset];
            var scalarBytes = 1;
            if (pattern.IsUtf8 && newline == Pcre2NewlineConvention.Any && character >= 0x80)
            {
                // Continuation bytes, including 0x85, are not newlines. The
                // only non-ASCII separators start with C2 (NEL) or E2 (LS/PS).
                character = character is 0xc2 or 0xe2 &&
                    pattern.DecodeScalar(offset, out var scalar, out scalarBytes) == System.Buffers.OperationStatus.Done
                    ? scalar.Value : -1;
            }
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
            offset += scalarBytes - 1;
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
    internal static bool EnterExtendedGroup(Utf8ConstructionPattern pattern, ref int offset, ref byte extended, Utf8ConstructionBuffer<byte> scopes)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        var maximum = Math.Min(budget.MaximumDepth, Pcre2BacktrackingCompiler.MaximumRecursiveDepth);
        var prospective = budget.Depth + (ulong)scopes.Count + 1;
        // Global option directives do not open a frame; admission is applied
        // only once the directive's terminating ':' or ordinary group is known.
        void AdmitGroup()
        {
            budget.ChargeWork(0);
            if (prospective > maximum)
                throw new Utf8ResourceLimitExceededException(Utf8ResourceLimitKind.ConstructionDepth, maximum, prospective);
        }
        if (pattern.StartsWithAscii(offset, "(?"))
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
            var nextCheckpoint = end;
            for (; end < pattern.Length; end++)
            {
                if (end >= nextCheckpoint) { budget.ChargeWork(0); nextCheckpoint = end + 2048; }
                var flag = pattern[end];
                if (flag == '-')
                {
                    if (reset) throw new Pcre2CompileException("A reset-option directive cannot contain '-'.",
                        Pcre2ErrorKind.InvalidAfterParensQuery);
                    enable = false;
                    continue;
                }
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
                if (!global) { AdmitGroup(); scopes.Push(extended); }
                extended = next;
                offset = end;
                return global;
            }
        }
        AdmitGroup();
        scopes.Push(extended);
        return false;
    }

    internal static bool TrySkipPosixClassToken(Utf8ConstructionPattern pattern, ref int offset)
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
    internal static bool TrySkipControlVerbLabel(Utf8ConstructionPattern pattern, ref int offset)
    {
        if (!pattern.StartsWithAscii(offset, "(*")) return false;
        var end = FindClosingParenthesis(pattern, offset + 2);
        if (end < 0) return false;
        var start = offset + 2;
        Utf8ConstructionScope.ChargeWork((ulong)(end - start));
        var colon = pattern.IndexOf(':', start, end - start);
        var length = (colon < 0 ? end : colon) - start;
        if (length != 0 && !IsVerb(pattern, start, length, "MARK") &&
            !IsVerb(pattern, start, length, "PRUNE") && !IsVerb(pattern, start, length, "THEN") &&
            !IsVerb(pattern, start, length, "SKIP") && !IsVerb(pattern, start, length, "COMMIT") &&
            !IsVerb(pattern, start, length, "FAIL") && !IsVerb(pattern, start, length, "F") &&
            !IsVerb(pattern, start, length, "ACCEPT")) return false;
        offset = end;
        return true;
    }

    private static bool IsVerb(Utf8ConstructionPattern pattern, int start, int length, string verb)
        => length == verb.Length && pattern.StartsWithAscii(start, verb, ignoreCase: true);

    internal static int FindClosingParenthesis(Utf8ConstructionPattern pattern, int start)
    {
        // Unknown or unterminated verbs can revisit suffixes. Bound each search
        // under the original construction owner while keeping SIMD IndexOf.
        for (var offset = start; offset < pattern.Length;)
        {
            var count = Math.Min(2048, pattern.Length - offset);
            Utf8ConstructionScope.ChargeWork((ulong)count);
            var end = pattern.IndexOf(')', offset, count);
            if (end >= 0) return end;
            offset += count;
        }
        return -1;
    }
}
