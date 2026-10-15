using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2;

internal static class Pcre2CaptureNameValidator
{
    // Admit declarations before backend selection. A declining optimization or
    // UTF-16 metadata path must not loosen the selected UTF-8 name limits or
    // admit explicit numeric definitions. Names alias ordinary capture slots.
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
                else if (inClass) { classHasAtom = true; classStart = false; }
                continue;
            }
            if (character == '\\')
            {
                if (offset + 1 < pattern.Length && pattern[offset + 1] == 'Q') quoted = true;
                else if (inClass && offset + 1 < pattern.Length && pattern[offset + 1] != 'E')
                { classHasAtom = true; classStart = false; }
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
            var groupStart = offset;
            var tail = pattern.AsSpan(offset);
            if (tail.StartsWith("(?#", StringComparison.Ordinal))
            {
                var end = pattern.IndexOf(')', offset + 3);
                if (end < 0) return; // The structural validator owns this error.
                offset = end;
                continue;
            }
            if (Pcre2PatternCodeScan.TrySkipControlVerbLabel(pattern, ref offset))
            {
                var content = pattern.AsSpan(groupStart + 2, offset - groupStart - 2);
                var colon = content.IndexOf(':');
                if (colon >= 0 && !FitsUtf8Limit(content[(colon + 1)..], 255))
                    throw new Pcre2CompileException("PCRE2 verb labels cannot exceed 255 UTF-8 bytes.",
                        Pcre2ErrorKind.InvalidAfterParensQuery);
                continue;
            }
            var nameStart = tail.StartsWith("(?P<", StringComparison.Ordinal) ? offset + 4
                : tail.StartsWith("(?'", StringComparison.Ordinal) ||
                  tail.StartsWith("(?<", StringComparison.Ordinal) && tail.Length > 3 && tail[3] is not ('=' or '!') ? offset + 3 : -1;
            if (nameStart >= 0 && nameStart < pattern.Length && Rune.TryGetRuneAt(pattern, nameStart, out var first) && Rune.IsDigit(first))
                throw new Pcre2CompileException("PCRE2 capture names must start with a non-digit.", Pcre2ErrorKind.InvalidAfterParensQuery);
            if (nameStart >= 0)
            {
                // At most 129 UTF-16 units suffice to prove an overlong UTF-8
                // name. Never rescan an unbounded suffix for a missing delimiter.
                var name = pattern.AsSpan(nameStart, Math.Min(129, pattern.Length - nameStart));
                Utf8ConstructionScope.ChargeWork((ulong)name.Length);
                var end = name.IndexOf(pattern[nameStart - 1] == '\'' ? '\'' : '>');
                if (end >= 0 ? !FitsUtf8Limit(name[..end], 128) : name.Length == 129)
                    throw new Pcre2CompileException("PCRE2 capture names cannot exceed 128 UTF-8 bytes.",
                        Pcre2ErrorKind.InvalidAfterParensQuery);
            }

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

    // Strict decoding precedes validation. UTF-16 length is only a cheap lower
    // bound; the selected eight-bit PCRE2 profile limits UTF-8 code units.
    internal static bool FitsUtf8Limit(ReadOnlySpan<char> text, int maximumBytes)
        => text.Length <= maximumBytes && Encoding.UTF8.GetByteCount(text) <= maximumBytes;
}
