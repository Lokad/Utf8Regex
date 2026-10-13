using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2;

internal static class Pcre2CaptureNameValidator
{
    // A declining optimization/parser must not admit .NET's explicit numeric
    // definitions through the foundation. PCRE2 names are aliases for ordinary
    // slots and must start with a non-digit (including Unicode Nd in UTF mode).
    internal static void Validate(string pattern, Pcre2CompileOptions options, Pcre2NewlineConvention newline)
    {
        Utf8ConstructionScope.ChargeWork((ulong)pattern.Length);
        var extended = (options & (Pcre2CompileOptions.Extended | Pcre2CompileOptions.ExtendedMore)) != 0;
        var scopes = new Stack<bool>();
        var quoted = false;
        var inClass = false;
        var classHasAtom = false;
        var classStart = false;
        for (var offset = 0; offset < pattern.Length; offset++)
        {
            var character = pattern[offset];
            if (quoted)
            {
                if (character == '\\' && offset + 1 < pattern.Length && pattern[offset + 1] == 'E')
                { quoted = false; offset++; }
                else if (inClass) classHasAtom = true;
                continue;
            }
            if (character == '\\')
            {
                if (offset + 1 < pattern.Length && pattern[offset + 1] == 'Q') quoted = true;
                else if (inClass) classHasAtom = true;
                offset++;
                continue;
            }
            if (inClass)
            {
                if (classStart && character == '^') { classStart = false; continue; }
                classStart = false;
                if (character == ']' && classHasAtom) { inClass = false; continue; }
                // A POSIX class/collation token's inner ] does not end the class.
                if (character == '[' && offset + 1 < pattern.Length && pattern[offset + 1] is ':' or '.' or '=')
                {
                    var marker = pattern[offset + 1];
                    for (var end = offset + 2; end + 1 < pattern.Length; end++)
                    {
                        // An unterminated token can make later '[' positions
                        // revisit this suffix. Admit each bounded rescan block.
                        if (((end - offset - 2) & 2047) == 0)
                            Utf8ConstructionScope.ChargeWork((ulong)Math.Min(2048, pattern.Length - end - 1));
                        if (pattern[end] == marker && pattern[end + 1] == ']') { offset = end + 1; break; }
                    }
                }
                classHasAtom = true;
                continue;
            }
            if (character == '[') { inClass = true; classHasAtom = false; classStart = true; continue; }
            if (extended && character == '#')
            {
                for (; offset < pattern.Length; offset++)
                {
                    character = pattern[offset];
                    var atNewline = newline switch
                    {
                        Pcre2NewlineConvention.Cr => character == '\r',
                        Pcre2NewlineConvention.Crlf => character == '\r' && offset + 1 < pattern.Length && pattern[offset + 1] == '\n',
                        Pcre2NewlineConvention.Any => character is '\r' or '\n' or '\v' or '\f' or '\u0085' or '\u2028' or '\u2029',
                        Pcre2NewlineConvention.AnyCrlf => character is '\r' or '\n',
                        Pcre2NewlineConvention.Nul => character == '\0',
                        _ => character == '\n',
                    };
                    if (atNewline) break;
                }
                continue;
            }
            if (character == ')') { if (scopes.Count != 0) extended = scopes.Pop(); continue; }
            if (character != '(') continue;
            var tail = pattern.AsSpan(offset);
            if (tail.StartsWith("(?#", StringComparison.Ordinal))
            {
                var end = pattern.IndexOf(')', offset + 3);
                if (end < 0) return; // The structural validator owns this error.
                offset = end;
                continue;
            }
            if (tail.StartsWith("(*", StringComparison.Ordinal))
            {
                var end = FindVerbEnd(pattern, offset + 2);
                if (end >= 0)
                {
                    var content = pattern.AsSpan(offset + 2, end - offset - 2);
                    Utf8ConstructionScope.ChargeWork((ulong)content.Length);
                    var colon = content.IndexOf(':');
                    var verb = colon < 0 ? content : content[..colon];
                    // Named control-verb arguments are text, not regex bodies.
                    if (verb.IsEmpty || verb.Equals("MARK", StringComparison.OrdinalIgnoreCase) ||
                        verb.Equals("PRUNE", StringComparison.OrdinalIgnoreCase) || verb.Equals("THEN", StringComparison.OrdinalIgnoreCase) ||
                        verb.Equals("SKIP", StringComparison.OrdinalIgnoreCase) || verb.Equals("COMMIT", StringComparison.OrdinalIgnoreCase) ||
                        verb.Equals("FAIL", StringComparison.OrdinalIgnoreCase) || verb.Equals("F", StringComparison.OrdinalIgnoreCase) ||
                        verb.Equals("ACCEPT", StringComparison.OrdinalIgnoreCase))
                    { offset = end; continue; }
                }
            }
            var nameStart = tail.StartsWith("(?P<", StringComparison.Ordinal) ? offset + 4
                : tail.StartsWith("(?<", StringComparison.Ordinal) || tail.StartsWith("(?'", StringComparison.Ordinal) ? offset + 3 : -1;
            if (nameStart >= 0 && nameStart < pattern.Length && Rune.TryGetRuneAt(pattern, nameStart, out var first) && Rune.IsDigit(first))
                throw new Pcre2CompileException("PCRE2 capture names must start with a non-digit.", Pcre2ErrorKind.InvalidAfterParensQuery);

            // Track x/xx settings by group scope, including global directives.
            // The real parser retains ownership of other options and syntax.
            if (tail.StartsWith("(?", StringComparison.Ordinal))
            {
                var end = offset + 2;
                var nextExtended = extended;
                var enable = true;
                var reset = end < pattern.Length && pattern[end] == '^';
                if (reset) { nextExtended = false; end++; }
                var optionStart = end;
                for (; end < pattern.Length; end++)
                {
                    var flag = pattern[end];
                    if (flag == '-') { enable = false; continue; }
                    if (!char.IsAsciiLetter(flag)) break;
                    if (flag == 'x') nextExtended = enable;
                }
                if ((end > optionStart || reset) && end < pattern.Length && pattern[end] is ':' or ')')
                {
                    if (pattern[end] == ':') scopes.Push(extended);
                    extended = nextExtended;
                    offset = end;
                    continue;
                }
            }
            scopes.Push(extended);
        }
    }

    private static int FindVerbEnd(string pattern, int start)
    {
        // Unknown or unterminated verbs need not advance the outer scanner.
        // Keep SIMD IndexOf, bounding each repeated search under the same owner.
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
