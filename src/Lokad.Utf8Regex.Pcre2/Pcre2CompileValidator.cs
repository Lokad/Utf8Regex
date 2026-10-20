using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2;

internal static class Pcre2CompileValidator
{
    // Call only between pattern items, outside classes, quoted text and escapes.
    // A comment ends at the first ')'; it contributes no atom or capture slot.
    internal static bool TrySkipParenthesizedComment(string pattern, ref int offset)
    {
        if (!pattern.AsSpan(offset).StartsWith("(?#", StringComparison.Ordinal)) return false;
        var end = pattern.IndexOf(')', offset + 3);
        if (end < 0) throw new Pcre2CompileException("Missing ) after comment.", Pcre2ErrorKind.MissingCommentClosing);
        offset = end + 1;
        return true;
    }

    private const Pcre2CompileOptions SupportedOptions =
        Pcre2CompileOptions.Caseless |
        Pcre2CompileOptions.Multiline |
        Pcre2CompileOptions.DotAll |
        Pcre2CompileOptions.Extended |
        Pcre2CompileOptions.ExtendedMore |
        Pcre2CompileOptions.Anchored |
        Pcre2CompileOptions.EndAnchored |
        Pcre2CompileOptions.DollarEndOnly |
        Pcre2CompileOptions.Ungreedy |
        Pcre2CompileOptions.NoAutoCapture |
        Pcre2CompileOptions.Ucp |
        Pcre2CompileOptions.FirstLine;

    internal static void ValidateArguments(
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings)
    {
        if ((options & ~SupportedOptions) != Pcre2CompileOptions.None)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The value contains an unknown PCRE2 compile-option bit.");
        }

        if (compileSettings.Newline is not (
            Pcre2NewlineConvention.Default or
            Pcre2NewlineConvention.Cr or
            Pcre2NewlineConvention.Lf or
            Pcre2NewlineConvention.Crlf or
            Pcre2NewlineConvention.Any or
            Pcre2NewlineConvention.AnyCrlf or
            Pcre2NewlineConvention.Nul))
        {
            throw new ArgumentOutOfRangeException(nameof(compileSettings), "The newline convention is not defined.");
        }

        if (compileSettings.Bsr is not (
            Pcre2BsrConvention.Default or
            Pcre2BsrConvention.AnyCrlf or
            Pcre2BsrConvention.Unicode))
        {
            throw new ArgumentOutOfRangeException(nameof(compileSettings), "The BSR convention is not defined.");
        }

        if (compileSettings.BackslashC is not (
            Pcre2BackslashCPolicy.Forbid or
            Pcre2BackslashCPolicy.Allow))
        {
            throw new ArgumentOutOfRangeException(nameof(compileSettings), "The backslash-C policy is not defined.");
        }

    }

    // Pattern-dependent scans stay behind strict decoding/construction admission.
    internal static void ValidatePattern(string pattern, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings)
    {
        if (Pcre2PatternCodeScan.ContainsCodeText(pattern, "(?C", options, compileSettings.Newline))
        {
            throw new Pcre2CompileException("PCRE2 callouts are out of scope for this profile.", Pcre2ErrorKind.CalloutUnsupported);
        }

        if (compileSettings.BackslashC == Pcre2BackslashCPolicy.Forbid && ContainsBackslashC(pattern, false, options, compileSettings.Newline))
        {
            throw new Pcre2CompileException(@"Using \C is disabled by this library profile.", Pcre2ErrorKind.BackslashCDisabled);
        }

        if (compileSettings.BackslashC == Pcre2BackslashCPolicy.Allow &&
            ContainsBackslashC(pattern, true, options, compileSettings.Newline))
        {
            throw new Pcre2CompileException(@"\C is not allowed in UTF lookbehind.", Pcre2ErrorKind.BackslashCInUtfLookbehind);
        }

        if (!compileSettings.AllowLookaroundBackslashK && ContainsLookaroundBackslashK(pattern, options, compileSettings.Newline))
        {
            throw new Pcre2CompileException(@"\K is not allowed in lookarounds (but see PCRE2_EXTRA_ALLOW_LOOKAROUND_BSK)", Pcre2ErrorKind.LookaroundBackslashKDisabled);
        }

        if (ContainsBackslashCInCharacterClass(pattern, options, compileSettings.Newline))
        {
            throw new Pcre2CompileException("Escape sequence is invalid in character class.", Pcre2ErrorKind.EscapeInvalidInClass);
        }

        Pcre2CaptureNameValidator.Validate(pattern, options, compileSettings.Newline);
        ValidateStructure(pattern, options, compileSettings.Newline);
    }

    private static void ValidateStructure(string pattern, Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
        var groupDepth = 0;
        var inCharacterClass = false;
        var classHasAtom = false;
        var classStart = false;
        var quoted = false;
        byte extended = (options & Pcre2CompileOptions.ExtendedMore) != 0 ? (byte)2 : (options & Pcre2CompileOptions.Extended) != 0 ? (byte)1 : (byte)0;
        var extendedScopes = new Stack<byte>();
        var previousWasZeroWidth = true; // No repeatable item at pattern entry.
        for (var offset = 0; offset < pattern.Length; offset++)
        {
            if ((offset & 2047) == 0) Utf8ConstructionScope.ChargeWork(0);
            var character = pattern[offset];
            if (quoted)
            {
                if (character == '\\' && offset + 1 < pattern.Length && pattern[offset + 1] == 'E')
                {
                    quoted = false;
                    offset++;
                }
                else if (inCharacterClass) { classHasAtom = true; classStart = false; }
                else previousWasZeroWidth = false;

                continue;
            }

            if (inCharacterClass)
            {
                if (extended == 2 && character is ' ' or '\t') continue;
                if (classStart && character == '^') { classStart = false; continue; }
                if (character == ']' && classHasAtom)
                {
                    inCharacterClass = false;
                    previousWasZeroWidth = false;
                    continue;
                }

                if (character == '\\' && offset + 1 < pattern.Length)
                {
                    var escape = pattern[++offset];
                    if (escape is 'Q' or 'E')
                    {
                        quoted = escape == 'Q';
                        continue;
                    }
                    if (escape is 'B' or 'R' or 'X')
                    {
                        throw new Pcre2CompileException("Escape sequence is invalid in character class.", Pcre2ErrorKind.EscapeInvalidInClass);
                    }

                    ValidateEscape(pattern, ref offset, escape);
                    classHasAtom = true;
                    classStart = false;
                    continue;
                }

                // Endpoint ordering belongs to the scalar class parser. Raw
                // UTF-16 characters and escape spellings are not endpoint values.
                if (character == '[') Pcre2PatternCodeScan.TrySkipPosixClassToken(pattern, ref offset);
                classHasAtom = true;
                classStart = false;
                continue;
            }

            if (!inCharacterClass && Pcre2PatternCodeScan.TrySkipExtendedComment(pattern, ref offset, extended != 0, newline)) continue;

            if (character == '\\')
            {
                if (offset + 1 >= pattern.Length)
                {
                    continue;
                }

                var escape = pattern[++offset];
                if (escape == 'Q')
                {
                    quoted = true;
                }

                ValidateEscape(pattern, ref offset, escape);
                if (escape is not ('Q' or 'E'))
                    previousWasZeroWidth = escape is 'A' or 'b' or 'B' or 'G' or 'z' or 'Z';
                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                classHasAtom = false;
                classStart = true;
                previousWasZeroWidth = false;
                continue;
            }

            if (character == '(' && TrySkipParenthesizedComment(pattern, ref offset))
            {
                offset--; // The for loop advances from the closing parenthesis.
                continue;
            }

            if (character == '(' && Pcre2PatternCodeScan.TrySkipControlVerbLabel(pattern, ref offset))
            {
                previousWasZeroWidth = false;
                continue;
            }

            if (character == '(')
            {
                ValidateOptionOpening(pattern, offset);
                if (Pcre2PatternCodeScan.EnterExtendedGroup(pattern, ref offset, ref extended, extendedScopes)) continue;
                groupDepth++;
                previousWasZeroWidth = false;
                continue;
            }

            if (character == ')')
            {
                if (extendedScopes.TryPop(out var previous)) extended = previous;
                if (groupDepth > 0)
                {
                    groupDepth--;
                }

                previousWasZeroWidth = false;
                continue;
            }

            if (character == '{' && TryValidateBoundedQuantifier(pattern, ref offset))
            {
                if (previousWasZeroWidth)
                    throw new Pcre2CompileException("Quantifier does not follow a repeatable item.", Pcre2ErrorKind.QuantifierInvalid);
                continue;
            }

            if (character is '*' or '+' or '?' && previousWasZeroWidth)
            {
                throw new Pcre2CompileException("Quantifier does not follow a repeatable item.", Pcre2ErrorKind.QuantifierInvalid);
            }

            previousWasZeroWidth = character is '^' or '$';
        }

        if (inCharacterClass)
        {
            throw new Pcre2CompileException("Missing terminating ] for character class.", Pcre2ErrorKind.MissingCharacterClassTerminator);
        }

        if (groupDepth != 0)
        {
            throw new Pcre2CompileException("Missing closing parenthesis.", Pcre2ErrorKind.MissingClosingParenthesis);
        }
    }

    private static void ValidateEscape(string pattern, ref int offset, char escape)
    {
        // Unknown ASCII letters are errors. Punctuation can be quoted literally;
        // syntax and operand validity for recognized escapes belong to the parser.
        if (char.IsAsciiLetter(escape) && "aAbBcCdDeEfFgGhHkKnNoOpPQrRsStTuUvVwWxXzZ".IndexOf(escape) < 0)
            throw new Pcre2CompileException($"Unrecognized escape sequence '\\{escape}'.", Pcre2ErrorKind.UnrecognizedEscape);
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
            var end = pattern.AsSpan(cursor, count).IndexOf(closing);
            if (end >= 0) { offset = cursor + end; return; }
            cursor += count;
        }
    }

    private static void ValidateOptionOpening(string pattern, int offset)
    {
        if (!pattern.AsSpan(offset).StartsWith("(?", StringComparison.Ordinal) ||
            pattern.AsSpan(offset).StartsWith("(?R)", StringComparison.Ordinal)) return;
        var cursor = offset + 2;
        if (cursor < pattern.Length && pattern[cursor] == '^') cursor++;
        var invalid = false;
        for (; cursor < pattern.Length; cursor++)
        {
            var flag = pattern[cursor];
            if (flag is ':' or ')')
            {
                if (invalid) throw new Pcre2CompileException("Invalid character after '(?'.", Pcre2ErrorKind.InvalidAfterParensQuery);
                return;
            }
            if (flag == '-') continue;
            if (!char.IsAsciiLetter(flag)) return; // Capture, reference or assertion syntax.
            invalid |= "imnsxJUr".IndexOf(flag) < 0;
        }
    }

    private static bool TryValidateBoundedQuantifier(string pattern, ref int offset)
    {
        var cursor = offset + 1;
        SkipSpace();
        if (!ReadNumber(out var minimum)) return false;
        var maximum = minimum;
        SkipSpace();
        if (cursor < pattern.Length && pattern[cursor] == ',')
        {
            cursor++;
            SkipSpace();
            if (!ReadNumber(out maximum)) maximum = int.MaxValue;
            SkipSpace();
        }
        if (cursor >= pattern.Length || pattern[cursor] != '}') return false;
        if (minimum > 65535 || maximum != int.MaxValue && maximum > 65535)
            throw new Pcre2CompileException("Quantifier is too large.", Pcre2ErrorKind.QuantifierTooBig);
        if (maximum < minimum)
            throw new Pcre2CompileException("Numbers out of order in quantifier.", Pcre2ErrorKind.QuantifierNumbersOutOfOrder);
        offset = cursor;
        return true;

        void SkipSpace()
        {
            while (cursor < pattern.Length && pattern[cursor] is ' ' or '\t') cursor++;
        }
        bool ReadNumber(out int value)
        {
            var start = cursor;
            value = 0;
            while (cursor < pattern.Length && pattern[cursor] is >= '0' and <= '9')
            {
                if ((cursor & 2047) == 0) Utf8ConstructionScope.ChargeWork(0);
                value = Math.Min(65536, value * 10 + pattern[cursor++] - '0');
            }
            return cursor != start;
        }
    }

    private static bool ContainsLookaroundBackslashK(string pattern, Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
        var parentheses = new Stack<bool>();
        var lookaroundDepth = 0;
        var inCharacterClass = false;
        var classHasAtom = false;
        var classStart = false;
        var quoted = false;
        byte extended = (options & Pcre2CompileOptions.ExtendedMore) != 0 ? (byte)2 : (options & Pcre2CompileOptions.Extended) != 0 ? (byte)1 : (byte)0;
        var extendedScopes = new Stack<byte>();
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

            if (!inCharacterClass && Pcre2PatternCodeScan.TrySkipExtendedComment(pattern, ref offset, extended != 0, newline)) continue;

            if (character == '\\')
            {
                if (!inCharacterClass && lookaroundDepth != 0 &&
                    offset + 1 < pattern.Length && pattern[offset + 1] == 'K')
                {
                    return true;
                }

                if (offset + 1 < pattern.Length && pattern[offset + 1] == 'Q')
                {
                    quoted = true;
                }
                else if (inCharacterClass && offset + 1 < pattern.Length && pattern[offset + 1] != 'E')
                { classHasAtom = true; classStart = false; }

                offset++;
                continue;
            }

            if (inCharacterClass)
            {
                if (extended == 2 && character is ' ' or '\t') continue;
                if (classStart && character == '^') { classStart = false; continue; }
                classStart = false;
                if (character == ']' && classHasAtom) { inCharacterClass = false; continue; }
                classHasAtom = true;
                if (character == '[') Pcre2PatternCodeScan.TrySkipPosixClassToken(pattern, ref offset);
                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                classHasAtom = false;
                classStart = true;
                continue;
            }

            if (character != '(')
            {
                if (character == ')' && extendedScopes.TryPop(out var previous)) extended = previous;
                if (character == ')' && parentheses.TryPop(out var wasLookaround) && wasLookaround)
                {
                    lookaroundDepth--;
                }
                continue;
            }

            if (pattern.AsSpan(offset).StartsWith("(?#", StringComparison.Ordinal))
            {
                var commentEnd = pattern.IndexOf(')', offset + 3);
                if (commentEnd < 0)
                {
                    return false;
                }

                offset = commentEnd;
                continue;
            }

            if (Pcre2PatternCodeScan.TrySkipControlVerbLabel(pattern, ref offset)) continue;

            var isLookaround = pattern.AsSpan(offset).StartsWith("(?=", StringComparison.Ordinal) ||
                pattern.AsSpan(offset).StartsWith("(?!", StringComparison.Ordinal) ||
                pattern.AsSpan(offset).StartsWith("(?<=", StringComparison.Ordinal) ||
                pattern.AsSpan(offset).StartsWith("(?<!", StringComparison.Ordinal);
            if (Pcre2PatternCodeScan.EnterExtendedGroup(pattern, ref offset, ref extended, extendedScopes)) continue;
            parentheses.Push(isLookaround);
            if (isLookaround)
            {
                lookaroundDepth++;
            }
        }

        return false;
    }

    private static bool ContainsBackslashC(string pattern, bool onlyInLookbehind, Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
        var parentheses = new Stack<bool>();
        var lookbehindDepth = 0;
        var inCharacterClass = false;
        var classHasAtom = false;
        var classStart = false;
        var quoted = false;
        byte extended = (options & Pcre2CompileOptions.ExtendedMore) != 0 ? (byte)2 : (options & Pcre2CompileOptions.Extended) != 0 ? (byte)1 : (byte)0;
        var extendedScopes = new Stack<byte>();
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

            if (!inCharacterClass && Pcre2PatternCodeScan.TrySkipExtendedComment(pattern, ref offset, extended != 0, newline)) continue;

            if (character == '\\')
            {
                if (offset + 1 >= pattern.Length)
                {
                    continue;
                }

                var escape = pattern[++offset];
                if (escape == 'Q')
                {
                    quoted = true;
                    continue;
                }

                if (escape == 'C' && (!onlyInLookbehind || lookbehindDepth != 0))
                {
                    return true;
                }

                if (inCharacterClass && escape != 'E') { classHasAtom = true; classStart = false; }

                continue;
            }

            if (inCharacterClass)
            {
                if (extended == 2 && character is ' ' or '\t') continue;
                if (classStart && character == '^') { classStart = false; continue; }
                classStart = false;
                if (character == ']' && classHasAtom) { inCharacterClass = false; continue; }
                classHasAtom = true;
                if (character == '[') Pcre2PatternCodeScan.TrySkipPosixClassToken(pattern, ref offset);
                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                classHasAtom = false;
                classStart = true;
                continue;
            }

            if (character == ')' && parentheses.TryPop(out var wasLookbehind))
            {
                if (extendedScopes.TryPop(out var previous)) extended = previous;
                if (wasLookbehind)
                {
                    lookbehindDepth--;
                }

                continue;
            }

            if (character != '(')
            {
                continue;
            }

            if (pattern.AsSpan(offset).StartsWith("(?#", StringComparison.Ordinal))
            {
                var commentEnd = pattern.IndexOf(')', offset + 3);
                if (commentEnd < 0)
                {
                    return false;
                }

                offset = commentEnd;
                continue;
            }

            if (Pcre2PatternCodeScan.TrySkipControlVerbLabel(pattern, ref offset)) continue;

            var isLookbehind = pattern.AsSpan(offset).StartsWith("(?<=", StringComparison.Ordinal) ||
                pattern.AsSpan(offset).StartsWith("(?<!", StringComparison.Ordinal);
            if (Pcre2PatternCodeScan.EnterExtendedGroup(pattern, ref offset, ref extended, extendedScopes)) continue;
            parentheses.Push(isLookbehind);
            if (isLookbehind)
            {
                lookbehindDepth++;
            }
        }

        return false;
    }

    private static bool ContainsBackslashCInCharacterClass(string pattern, Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
        var inCharacterClass = false;
        var classHasAtom = false;
        var classStart = false;
        var quoted = false;
        byte extended = (options & Pcre2CompileOptions.ExtendedMore) != 0 ? (byte)2 : (options & Pcre2CompileOptions.Extended) != 0 ? (byte)1 : (byte)0;
        var extendedScopes = new Stack<byte>();
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

            if (!inCharacterClass && Pcre2PatternCodeScan.TrySkipExtendedComment(pattern, ref offset, extended != 0, newline)) continue;

            if (character == '\\')
            {
                if (offset + 1 >= pattern.Length)
                {
                    continue;
                }

                var escape = pattern[++offset];
                if (escape == 'Q')
                {
                    quoted = true;
                }
                else if (inCharacterClass && escape == 'C')
                {
                    return true;
                }

                if (inCharacterClass && escape is not ('Q' or 'E')) { classHasAtom = true; classStart = false; }

                continue;
            }

            if (!inCharacterClass && character == '(' && Pcre2PatternCodeScan.TrySkipControlVerbLabel(pattern, ref offset))
            {
                continue;
            }

            if (!inCharacterClass && character == '(')
            {
                if (TrySkipParenthesizedComment(pattern, ref offset)) { offset--; continue; }
                Pcre2PatternCodeScan.EnterExtendedGroup(pattern, ref offset, ref extended, extendedScopes);
                continue;
            }
            if (!inCharacterClass && character == ')' && extendedScopes.TryPop(out var previous)) extended = previous;

            if (inCharacterClass)
            {
                if (extended == 2 && character is ' ' or '\t') continue;
                if (classStart && character == '^') { classStart = false; continue; }
                classStart = false;
                if (character == ']' && classHasAtom) { inCharacterClass = false; continue; }
                classHasAtom = true;
                if (character == '[') Pcre2PatternCodeScan.TrySkipPosixClassToken(pattern, ref offset);
                continue;
            }
            if (character == '[') { inCharacterClass = true; classHasAtom = false; classStart = true; }
        }

        return false;
    }
}
