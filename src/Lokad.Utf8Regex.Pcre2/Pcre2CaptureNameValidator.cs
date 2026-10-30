using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2;

internal static class Pcre2CaptureNameValidator
{
    // Admit declarations before backend selection. A declining optimization or
    // UTF-16 metadata path must not loosen the selected UTF-8 name limits or
    // admit explicit numeric definitions. Names alias ordinary capture slots.
    internal static void Validate(string pattern, Pcre2CompileOptions options, Pcre2NewlineConvention newline)
        => Validate(new Utf8ConstructionPattern(pattern), options, newline);

    internal static void Validate(Utf8ConstructionPattern pattern, Pcre2CompileOptions options, Pcre2NewlineConvention newline)
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
            if (pattern.StartsWithAscii(offset, "(?#"))
            {
                var end = Pcre2PatternCodeScan.FindClosingParenthesis(pattern, offset + 3);
                if (end < 0) return; // The structural validator owns this error.
                offset = end;
                continue;
            }
            if (Pcre2PatternCodeScan.TrySkipControlVerbLabel(pattern, ref offset))
            {
                var start = groupStart + 2;
                var colon = pattern.IndexOf(':', start, offset - start);
                if (colon >= 0 && !FitsUtf8Limit(pattern, colon + 1, offset - colon - 1, 255))
                    throw new Pcre2CompileException("PCRE2 verb labels cannot exceed 255 UTF-8 bytes.",
                        Pcre2ErrorKind.InvalidAfterParensQuery);
                continue;
            }
            var nameStart = pattern.StartsWithAscii(offset, "(?P<") ? offset + 4
                : pattern.StartsWithAscii(offset, "(?'") ||
                  pattern.StartsWithAscii(offset, "(?<") && pattern.Length - offset > 3 && pattern[offset + 3] is not ('=' or '!') ? offset + 3 : -1;
            if (nameStart >= 0 && nameStart < pattern.Length && pattern.DecodeScalar(nameStart, out var first, out _) == System.Buffers.OperationStatus.Done && Rune.IsDigit(first))
                throw new Pcre2CompileException("PCRE2 capture names must start with a non-digit.", Pcre2ErrorKind.InvalidAfterParensQuery);
            if (nameStart >= 0)
            {
                // At most 129 source units prove an overlong UTF-8 name.
                // Native units are bytes; never decode or rescan the suffix.
                var count = Math.Min(129, pattern.Length - nameStart);
                Utf8ConstructionScope.ChargeWork((ulong)count);
                var end = pattern.IndexOf(pattern[nameStart - 1] == '\'' ? '\'' : '>', nameStart, count);
                if (end >= 0 ? !FitsUtf8Limit(pattern, nameStart, end - nameStart, 128) : count == 129)
                    throw new Pcre2CompileException("PCRE2 capture names cannot exceed 128 UTF-8 bytes.",
                        Pcre2ErrorKind.InvalidAfterParensQuery);
            }

            Pcre2PatternCodeScan.EnterExtendedGroup(pattern, ref offset, ref extended, scopes);
        }
    }

    private static bool FitsUtf8Limit(Utf8ConstructionPattern pattern, int start, int length, int maximumBytes)
        => pattern.IsUtf8 ? length <= maximumBytes : FitsUtf8Limit(pattern.Text.AsSpan(start, length), maximumBytes);

    // Text input uses UTF-16 length as a cheap lower
    // bound; the selected eight-bit PCRE2 profile limits UTF-8 code units.
    internal static bool FitsUtf8Limit(ReadOnlySpan<char> text, int maximumBytes)
        => text.Length <= maximumBytes && Encoding.UTF8.GetByteCount(text) <= maximumBytes;
}
