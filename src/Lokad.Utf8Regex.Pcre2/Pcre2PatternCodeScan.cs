using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2;

// Native PCRE2 reads patterns lexically. Guards skip classes, quotes, comments,
// escapes and recognized literal verb labels. Regex-body constructs still need
// validation. Extended (#) comments are not tracked by these code-text guards.
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
        // Admit the full lexical extent, including skipped quotes/comments.
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
        var quoted = false;
        var inCharacterClass = false;
        var classHasAtom = false;
        var classStart = false;
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
