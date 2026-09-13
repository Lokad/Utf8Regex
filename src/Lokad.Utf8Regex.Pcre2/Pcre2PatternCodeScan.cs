namespace Lokad.Utf8Regex.Pcre2;

// PLAN.md C11: native PCRE2 reads patterns lexically, so raw substring scans must skip character classes, \Q...\E quotes, and (?#...) comments, plus escaped characters: [(?C], \Q(?C\E, (?#(?C)), and \(?C are literals, while only a code-position (?C is a callout. Extended (#) comments are not tracked here; that residual gap is shared with the lookaround and backslash-C scans.
internal static class Pcre2PatternCodeScan
{
    public static bool ContainsCodeText(string pattern, string text)
    {
        return Scan(pattern, text, false);
    }

    public static bool ContainsNonClassCodeText(string pattern, string text)
    {
        return Scan(pattern, text, true);
    }

    private static bool Scan(string pattern, string text, bool matchInsideClass)
    {
        var quoted = false;
        var inCharacterClass = false;
        var classHasAtom = false;
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
                if (character == '\\' && offset + 1 < pattern.Length)
                {
                    if (matchInsideClass && pattern.AsSpan(offset).StartsWith(text, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    offset++;
                    classHasAtom = true;
                    continue;
                }

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
                continue;
            }

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

            if (pattern.AsSpan(offset).StartsWith(text, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
