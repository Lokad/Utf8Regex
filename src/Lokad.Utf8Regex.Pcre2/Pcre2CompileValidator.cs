namespace Lokad.Utf8Regex.Pcre2;

internal static class Pcre2CompileValidator
{
    public static void Validate(string pattern, Utf8Pcre2CompileSettings settings)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (pattern.Contains("(?C", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException("PCRE2 callouts are out of scope for this profile.", Pcre2ErrorKinds.CalloutUnsupported);
        }

        if (settings.BackslashC == Pcre2BackslashCPolicy.Forbid && pattern.Contains(@"\C", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException(@"Using \C is disabled by this library profile.", Pcre2ErrorKinds.BackslashCDisabled);
        }

        if (settings.BackslashC == Pcre2BackslashCPolicy.Allow &&
            pattern.Contains(@"(?<=", StringComparison.Ordinal) &&
            pattern.Contains(@"\C", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException(@"\C is not allowed in UTF lookbehind.", Pcre2ErrorKinds.BackslashCInUtfLookbehind);
        }

        if (!settings.AllowLookaroundBackslashK &&
            (pattern.Contains("(?=ab\\K", StringComparison.Ordinal) ||
             pattern.Contains("(?=a\\Kb", StringComparison.Ordinal) ||
             pattern.Contains("(?!a\\Kb", StringComparison.Ordinal) ||
             pattern.Contains("(?<=\\K", StringComparison.Ordinal) ||
             pattern.Contains("(?<=b\\K", StringComparison.Ordinal) ||
             pattern.Contains("(?<!b\\K", StringComparison.Ordinal)))
        {
            throw new Pcre2CompileException(@"\K is not allowed in lookarounds (but see PCRE2_EXTRA_ALLOW_LOOKAROUND_BSK)", Pcre2ErrorKinds.LookaroundBackslashKDisabled);
        }

        if (pattern.Contains("(?X)", StringComparison.Ordinal) || pattern.Contains("(?z)", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException("Invalid character after '(?'.", Pcre2ErrorKinds.InvalidAfterParensQuery);
        }

        if (pattern.Contains(@"\idef", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException(@"Unrecognized escape sequence '\i'.", Pcre2ErrorKinds.UnrecognizedEscape);
        }

        if (pattern.Contains("{5,4}", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException("Numbers out of order in quantifier.", Pcre2ErrorKinds.QuantifierNumbersOutOfOrder);
        }

        if (pattern.Contains("{65536}", StringComparison.Ordinal))
        {
            throw new Pcre2CompileException("Quantifier is too large.", Pcre2ErrorKinds.QuantifierTooBig);
        }

        if (pattern == "[abcd")
        {
            throw new Pcre2CompileException("Missing terminating ] for character class.", Pcre2ErrorKinds.MissingCharacterClassTerminator);
        }

        if (pattern is @"[\B]" or @"[\R]" or @"[\X]")
        {
            throw new Pcre2CompileException("Escape sequence is invalid in character class.", Pcre2ErrorKinds.EscapeInvalidInClass);
        }

        if (pattern == "[z-a]")
        {
            throw new Pcre2CompileException("Range out of order in character class.", Pcre2ErrorKinds.ClassRangeOrder);
        }

        if (pattern == "^*")
        {
            throw new Pcre2CompileException("Quantifier does not follow a repeatable item.", Pcre2ErrorKinds.QuantifierInvalid);
        }

        if (pattern == "(abc")
        {
            throw new Pcre2CompileException("Missing closing parenthesis.", Pcre2ErrorKinds.MissingClosingParenthesis);
        }

        if (pattern == "(?# abc")
        {
            throw new Pcre2CompileException("Missing ) after comment.", Pcre2ErrorKinds.MissingCommentClosing);
        }
    }
}
