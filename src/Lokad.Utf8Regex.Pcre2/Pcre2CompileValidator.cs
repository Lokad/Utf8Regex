namespace Lokad.Utf8Regex.Pcre2;

internal static class Pcre2CompileValidator
{
    public static void Validate(string pattern, Utf8Pcre2CompileSettings settings)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (pattern.Contains("(?C", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException("PCRE2 callouts are out of scope for this profile.", Pcre2ErrorKind.CalloutUnsupported);
        }

        if (settings.BackslashC == Pcre2BackslashCPolicy.Forbid && ContainsBackslashC(pattern, false))
        {
            throw new Pcre2CompileException(@"Using \C is disabled by this library profile.", Pcre2ErrorKind.BackslashCDisabled);
        }

        if (settings.BackslashC == Pcre2BackslashCPolicy.Allow &&
            ContainsBackslashC(pattern, true))
        {
            throw new Pcre2CompileException(@"\C is not allowed in UTF lookbehind.", Pcre2ErrorKind.BackslashCInUtfLookbehind);
        }

        if (!settings.AllowLookaroundBackslashK && ContainsLookaroundBackslashK(pattern))
        {
            throw new Pcre2CompileException(@"\K is not allowed in lookarounds (but see PCRE2_EXTRA_ALLOW_LOOKAROUND_BSK)", Pcre2ErrorKind.LookaroundBackslashKDisabled);
        }

        if (ContainsBackslashCInCharacterClass(pattern))
        {
            throw new Pcre2CompileException("Escape sequence is invalid in character class.", Pcre2ErrorKind.EscapeInvalidInClass);
        }

        if (pattern.Contains("(?X)", StringComparison.Ordinal) || pattern.Contains("(?z)", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException("Invalid character after '(?'.", Pcre2ErrorKind.InvalidAfterParensQuery);
        }

        if (pattern.Contains(@"\idef", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException(@"Unrecognized escape sequence '\i'.", Pcre2ErrorKind.UnrecognizedEscape);
        }

        if (pattern.Contains("{5,4}", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException("Numbers out of order in quantifier.", Pcre2ErrorKind.QuantifierNumbersOutOfOrder);
        }

        if (pattern.Contains("{65536}", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException("Quantifier is too large.", Pcre2ErrorKind.QuantifierTooBig);
        }

        ValidateStructure(pattern);
    }

    private static void ValidateStructure(string pattern)
    {
        var groupDepth = 0;
        var inCharacterClass = false;
        var classHasAtom = false;
        var previousClassScalar = -1;
        var rangeLow = -1;
        var quoted = false;
        var previousWasZeroWidth = false;
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

                continue;
            }

            if (inCharacterClass)
            {
                if (character == ']' && classHasAtom)
                {
                    inCharacterClass = false;
                    previousClassScalar = -1;
                    rangeLow = -1;
                    previousWasZeroWidth = false;
                    continue;
                }

                if (character == '\\' && offset + 1 < pattern.Length)
                {
                    var escape = pattern[++offset];
                    if (escape is 'B' or 'R' or 'X')
                    {
                        throw new Pcre2CompileException("Escape sequence is invalid in character class.", Pcre2ErrorKind.EscapeInvalidInClass);
                    }

                    CompleteClassAtom(escape, ref classHasAtom, ref previousClassScalar, ref rangeLow);
                    continue;
                }

                if (character == '-' && previousClassScalar >= 0 && offset + 1 < pattern.Length && pattern[offset + 1] != ']')
                {
                    rangeLow = previousClassScalar;
                    previousClassScalar = -1;
                    continue;
                }

                CompleteClassAtom(character, ref classHasAtom, ref previousClassScalar, ref rangeLow);
                continue;
            }

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

                previousWasZeroWidth = escape is 'A' or 'b' or 'B' or 'G' or 'z' or 'Z';
                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                classHasAtom = false;
                previousClassScalar = -1;
                rangeLow = -1;
                previousWasZeroWidth = false;
                continue;
            }

            if (character == '(' && pattern.AsSpan(offset).StartsWith("(?#", StringComparison.Ordinal))
            {
                var commentEnd = pattern.IndexOf(')', offset + 3);
                if (commentEnd < 0)
                {
                    throw new Pcre2CompileException("Missing ) after comment.", Pcre2ErrorKind.MissingCommentClosing);
                }

                offset = commentEnd;
                previousWasZeroWidth = false;
                continue;
            }

            if (character == '(')
            {
                groupDepth++;
                previousWasZeroWidth = false;
                continue;
            }

            if (character == ')')
            {
                if (groupDepth > 0)
                {
                    groupDepth--;
                }

                previousWasZeroWidth = false;
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

    private static void CompleteClassAtom(
        char character,
        ref bool classHasAtom,
        ref int previousClassScalar,
        ref int rangeLow)
    {
        classHasAtom = true;
        if (rangeLow >= 0 && character < rangeLow)
        {
            throw new Pcre2CompileException("Range out of order in character class.", Pcre2ErrorKind.ClassRangeOrder);
        }

        previousClassScalar = character;
        rangeLow = -1;
    }

    private static bool ContainsLookaroundBackslashK(string pattern)
    {
        var parentheses = new Stack<bool>();
        var lookaroundDepth = 0;
        var inCharacterClass = false;
        for (var offset = 0; offset < pattern.Length; offset++)
        {
            var character = pattern[offset];
            if (character == '\\')
            {
                if (!inCharacterClass && lookaroundDepth != 0 &&
                    offset + 1 < pattern.Length && pattern[offset + 1] == 'K')
                {
                    return true;
                }

                offset++;
                continue;
            }

            if (inCharacterClass)
            {
                inCharacterClass = character != ']';
                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                continue;
            }

            if (character != '(')
            {
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

            var isLookaround = pattern.AsSpan(offset).StartsWith("(?=", StringComparison.Ordinal) ||
                pattern.AsSpan(offset).StartsWith("(?!", StringComparison.Ordinal) ||
                pattern.AsSpan(offset).StartsWith("(?<=", StringComparison.Ordinal) ||
                pattern.AsSpan(offset).StartsWith("(?<!", StringComparison.Ordinal);
            parentheses.Push(isLookaround);
            if (isLookaround)
            {
                lookaroundDepth++;
            }
        }

        return false;
    }

    private static bool ContainsBackslashC(string pattern, bool onlyInLookbehind)
    {
        var parentheses = new Stack<bool>();
        var lookbehindDepth = 0;
        var inCharacterClass = false;
        var quoted = false;
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

                continue;
            }

            if (character == '\\')
            {
                if (offset + 1 >= pattern.Length)
                {
                    continue;
                }

                var escape = pattern[++offset];
                if (!inCharacterClass && escape == 'Q')
                {
                    quoted = true;
                    continue;
                }

                if (escape == 'C' && (!onlyInLookbehind || lookbehindDepth != 0))
                {
                    return true;
                }

                continue;
            }

            if (inCharacterClass)
            {
                inCharacterClass = character != ']';
                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                continue;
            }

            if (character == ')' && parentheses.TryPop(out var wasLookbehind))
            {
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

            var isLookbehind = pattern.AsSpan(offset).StartsWith("(?<=", StringComparison.Ordinal) ||
                pattern.AsSpan(offset).StartsWith("(?<!", StringComparison.Ordinal);
            parentheses.Push(isLookbehind);
            if (isLookbehind)
            {
                lookbehindDepth++;
            }
        }

        return false;
    }

    private static bool ContainsBackslashCInCharacterClass(string pattern)
    {
        var inCharacterClass = false;
        var quoted = false;
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

                continue;
            }

            if (character == '\\')
            {
                if (offset + 1 >= pattern.Length)
                {
                    continue;
                }

                var escape = pattern[++offset];
                if (!inCharacterClass && escape == 'Q')
                {
                    quoted = true;
                }
                else if (inCharacterClass && escape == 'C')
                {
                    return true;
                }

                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
            }
            else if (character == ']')
            {
                inCharacterClass = false;
            }
        }

        return false;
    }
}
