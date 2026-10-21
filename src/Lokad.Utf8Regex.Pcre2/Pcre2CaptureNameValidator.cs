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
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        budget.ChargeWork((ulong)pattern.Length);
        byte extended = (options & Pcre2CompileOptions.ExtendedMore) != 0 ? (byte)2 : (options & Pcre2CompileOptions.Extended) != 0 ? (byte)1 : (byte)0;
        using var scopes = new Utf8ConstructionBuffer<byte>(budget);
        var quoted = false;
        var inClass = false;
        var classHasAtom = false;
        var classStart = false;
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
                { quoted = false; offset++; }
                else if (inClass) { classHasAtom = true; classStart = false; }
                continue;
            }
            if (character == '\\')
            {
                if (offset + 1 < pattern.Length && pattern[offset + 1] == 'Q') quoted = true;
                else if (inClass && offset + 1 < pattern.Length && pattern[offset + 1] != 'E')
                { classHasAtom = true; classStart = false; }
                if (++offset < pattern.Length) Pcre2PatternCodeScan.SkipEscapeOperand(pattern, ref offset, pattern[offset]);
                continue;
            }
            if (inClass)
            {
                if (extended == 2 && character is ' ' or '\t') continue;
                if (classStart && character == '^') { classStart = false; continue; }
                classStart = false;
                if (character == ']' && classHasAtom) { inClass = false; continue; }
                if (character == '[') Pcre2PatternCodeScan.TrySkipPosixClassToken(pattern, ref offset);
                classHasAtom = true;
                continue;
            }
            if (character == '[') { inClass = true; classHasAtom = false; classStart = true; continue; }
            if (Pcre2PatternCodeScan.TrySkipExtendedComment(pattern, ref offset, extended != 0, newline)) continue;
            if (character == ')') { if (scopes.Count != 0) extended = scopes.Pop(); continue; }
            if (character != '(') continue;
            var groupStart = offset;
            var tail = pattern.AsSpan(offset);
            if (tail.StartsWith("(?#", StringComparison.Ordinal))
            {
                var end = Pcre2PatternCodeScan.FindClosingParenthesis(pattern, offset + 3);
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

            Pcre2PatternCodeScan.EnterExtendedGroup(pattern, ref offset, ref extended, scopes);
        }
    }

    // Strict decoding precedes validation. UTF-16 length is only a cheap lower
    // bound; the selected eight-bit PCRE2 profile limits UTF-8 code units.
    internal static bool FitsUtf8Limit(ReadOnlySpan<char> text, int maximumBytes)
        => text.Length <= maximumBytes && Encoding.UTF8.GetByteCount(text) <= maximumBytes;
}
