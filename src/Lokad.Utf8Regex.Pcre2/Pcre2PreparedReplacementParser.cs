using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2;

// The program contains no subject coordinates or retained template text. Errors
// are instructions, preserving no-match and unselected-fallback laziness.
internal readonly record struct Pcre2PreparedReplacementPlan(
    Pcre2ReplacementInstruction[] Instructions, Pcre2ReplacementFragment[] Fragments);
internal readonly record struct Pcre2ReplacementFragment(int Normal, int Quoted);
internal readonly record struct Pcre2ReplacementPreparation(
    Pcre2SimpleReplacementPlan? Simple, Pcre2PreparedReplacementPlan? Prepared);
internal enum Pcre2ReplacementOpcode : byte
{
    Return, Literal, Reference, Whole, Prefix, Suffix, Subject, Plus,
    Quote, EndQuote, EndCase, Upper, Lower, UpperNext, LowerNext,
    Conditional, Fallback, Error, AngleUnclosed, BackslashDigits
}
internal enum Pcre2ReplacementSelectorKind : byte { Number, Name, Mark }
internal readonly record struct Pcre2ReplacementInstruction(
    Pcre2ReplacementOpcode Kind, Pcre2ReplacementSelectorKind Selector, int Argument,
    byte[] Payload, int NextNormal, int NextQuoted, int ThenFragment, int ElseFragment);

internal static class Pcre2PreparedReplacementParser
{
    internal static Pcre2PreparedReplacementPlan Parse(string replacement, Pcre2SubstitutionOptions options)
        => Parse(new Utf8ConstructionPattern(replacement), options);

    // Native sources have already passed full strict validation at entry.
    internal static Pcre2PreparedReplacementPlan Parse(Utf8ConstructionPattern replacement, Pcre2SubstitutionOptions options)
    {
        Pcre2ReplacementPreparationControl.Check();
        using var scope = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(scope.Budget);
        using var code = Utf8ConstructionBuffer<Pcre2ReplacementInstruction>.Create(scope.Budget, true);
        var compiler = new Pcre2ReplacementCompiler(replacement, options, storage, code,
            storage.Buffer<Pcre2ReplacementCompileFragment>(), storage.Buffer<Pcre2ReplacementCompileTask>());
        var result = compiler.Build();
        Pcre2ReplacementPreparationControl.Check();
        storage.Complete(); scope.Complete();
        return result;
    }

    private readonly record struct Pcre2ReplacementCompileFragment(int Start, int End, int[] Normal, int[] Quoted);
    private readonly record struct Pcre2ReplacementCompileTask(int Fragment, int Position, bool Quoted, int Instruction);

    // Construction-only position tables and tasks are admitted scratch. Every
    // scan charges the enclosing owner; preparation and execution are iterative.
    private ref struct Pcre2ReplacementCompiler(Utf8ConstructionPattern source, Pcre2SubstitutionOptions options,
        Utf8ConstructionSession storage, Utf8ConstructionBuffer<Pcre2ReplacementInstruction> code,
        Utf8ConstructionBuffer<Pcre2ReplacementCompileFragment> fragments,
        Utf8ConstructionBuffer<Pcre2ReplacementCompileTask> tasks)
    {
        private readonly Utf8ConstructionPattern text = source;
        private bool Extended => (options & Pcre2SubstitutionOptions.Extended) != 0;
        private void Work() { Pcre2ReplacementPreparationControl.Check(); storage.Budget.ChargeWork(1); }
        private static Pcre2ReplacementInstruction Op(Pcre2ReplacementOpcode kind)
            => new(kind, default, 0, [], 0, 0, -1, -1);
        private static Pcre2ReplacementInstruction Error(Pcre2ErrorKind kind)
            => Op(Pcre2ReplacementOpcode.Error) with { Argument = (int)kind };

        internal Pcre2PreparedReplacementPlan Build()
        {
            code.Add(Op(Pcre2ReplacementOpcode.Return));
            _ = Fragment(0, text.Length);
            for (var index = 0; index < tasks.Count; index++)
            {
                Work();
                var task = tasks[index];
                var descriptor = fragments[task.Fragment];
                var next = task.Position;
                var instruction = task.Quoted ? ParseQuoted(task.Position, descriptor.End, out next)
                    : ParseNormal(task.Position, descriptor.End, out next);
                if (instruction.Kind != Pcre2ReplacementOpcode.Return)
                {
                    if (instruction.Kind is Pcre2ReplacementOpcode.Conditional or Pcre2ReplacementOpcode.Fallback)
                        instruction = instruction with { NextNormal = Node(task.Fragment, next, false), NextQuoted = Node(task.Fragment, next, true) };
                    else if (instruction.Kind == Pcre2ReplacementOpcode.Quote || task.Quoted && instruction.Kind != Pcre2ReplacementOpcode.EndQuote)
                        instruction = instruction with { NextQuoted = Node(task.Fragment, next, true) };
                    else instruction = instruction with { NextNormal = Node(task.Fragment, next, false) };
                }
                code[task.Instruction] = instruction;
            }
            var roots = storage.Allocate<Pcre2ReplacementFragment>(fragments.Count);
            for (var index = 0; index < roots.Length; index++)
            {
                Work(); var fragment = fragments[index];
                roots[index] = new(fragment.Normal[0], fragment.Quoted[0]);
            }
            // Admit the exact final instruction extent while scratch code is
            // still live. The enclosing scope rolls both loans back on failure.
            storage.Budget.Reserve((ulong)code.Count);
            var instructions = storage.Allocate<Pcre2ReplacementInstruction>(code.Count);
            storage.Budget.ChargeWork((ulong)code.Count);
            code.Span.CopyTo(instructions);
            return new(instructions, roots);
        }

        private int Fragment(int start, int end)
        {
            Work();
            var count = checked(end - start + 1);
            var normal = storage.Allocate<int>(count, false);
            var quoted = storage.Allocate<int>(count, false);
            storage.Budget.ChargeWork((ulong)count * 2);
            normal.AsSpan().Fill(-1); quoted.AsSpan().Fill(-1);
            var id = fragments.Count;
            fragments.Add(new(start, end, normal, quoted));
            _ = Node(id, start, false); _ = Node(id, start, true);
            return id;
        }

        private int Node(int fragment, int position, bool quoted)
        {
            Work();
            var descriptor = fragments[fragment];
            var entries = quoted ? descriptor.Quoted : descriptor.Normal;
            var offset = position - descriptor.Start;
            if (position == descriptor.End) return entries[offset] = 0;
            if (entries[offset] >= 0) return entries[offset];
            var index = code.Count;
            code.Add(Op(Pcre2ReplacementOpcode.Return));
            entries[offset] = index;
            tasks.Add(new(fragment, position, quoted, index));
            return index;
        }

        private Pcre2ReplacementInstruction Literal(ReadOnlySpan<char> value)
            => Op(Pcre2ReplacementOpcode.Literal) with { Payload = storage.EncodeRepairedUtf8(value, default(Pcre2ReplacementTextControl)) };

        private Pcre2ReplacementInstruction Literal(int start, int length)
            => Op(Pcre2ReplacementOpcode.Literal) with { Payload = Pcre2ReplacementPreparationControl.EncodeSource(storage, text, start, length) };

        private Pcre2ReplacementInstruction Scalar(int value)
        {
            if (!Rune.TryCreate(value, out var scalar)) return Error(Pcre2ErrorKind.BadReplacementEscape);
            var bytes = storage.Allocate<byte>(scalar.Utf8SequenceLength);
            storage.Budget.ChargeWork((ulong)bytes.Length);
            scalar.EncodeToUtf8(bytes);
            return Op(Pcre2ReplacementOpcode.Literal) with { Payload = bytes };
        }

        private Pcre2ReplacementInstruction ParseQuoted(int position, int end, out int next)
        {
            next = position;
            if (position == end) return Op(Pcre2ReplacementOpcode.Return);
            if (Extended && text[position] == '\\' && position + 1 < end && text[position + 1] == 'E')
            { next = position + 2; return Op(Pcre2ReplacementOpcode.EndQuote); }
            while (next < end)
            {
                Work();
                if (Extended && text[next] == '\\' && next + 1 < end && text[next + 1] == 'E') break;
                next++;
            }
            return Literal(position, next - position);
        }

        private Pcre2ReplacementInstruction ParseNormal(int position, int end, out int next)
        {
            next = position;
            if (position == end) return Op(Pcre2ReplacementOpcode.Return);
            if (text[position] == '$') return Dollar(position, end, out next);
            if (Extended && text[position] == '\\') return Escape(position, end, out next);
            while (next < end && text[next] != '$' && (!Extended || text[next] != '\\')) { Work(); next++; }
            return Literal(position, next - position);
        }

        private Pcre2ReplacementInstruction Dollar(int position, int end, out int next)
        {
            next = position + 1;
            if (next == end) return Literal("$".AsSpan());
            var ch = text[next];
            next++;
            switch (ch)
            {
                case '$': return Literal("$".AsSpan());
                case '&': return Op(Pcre2ReplacementOpcode.Whole);
                case '`': return Op(Pcre2ReplacementOpcode.Prefix);
                case '\'': return Op(Pcre2ReplacementOpcode.Suffix);
                case '_': return Op(Pcre2ReplacementOpcode.Subject);
                case '+': return next < end && text[next] == '+' ? Error(Pcre2ErrorKind.InvalidReplacementReference) : Op(Pcre2ReplacementOpcode.Plus);
                case '{': return Braced(next, end, out next);
                case '<': return Angle(next, end, false, out next);
                case '*' when position + 6 <= end && text.StartsWithAscii(position + 1, "*MARK"):
                    storage.Budget.ChargeWork(5); next = position + 6;
                    return Op(Pcre2ReplacementOpcode.Reference) with { Selector = Pcre2ReplacementSelectorKind.Mark };
            }
            if (char.IsAsciiDigit(ch))
            {
                while (next < end && char.IsAsciiDigit(text[next])) { Work(); next++; }
                return Selector(position + 1, next - position - 1, Pcre2ReplacementOpcode.Reference);
            }
            if (NameStart(ch))
            {
                while (next < end && NamePart(text[next])) { Work(); next++; }
                return Selector(position + 1, next - position - 1, Pcre2ReplacementOpcode.Reference);
            }
            next = position + 1;
            return Literal("$".AsSpan());
        }

        private Pcre2ReplacementInstruction Braced(int start, int end, out int next)
        {
            var closing = CloseBrace(start, end);
            next = closing < 0 ? end : closing + 1;
            if (start == end) return Error(Pcre2ErrorKind.MalformedReplacementPattern);
            if (closing < 0) return Error(char.IsAsciiDigit(text[start]) ? Pcre2ErrorKind.MissingClosingReplacementBrace : Pcre2ErrorKind.MalformedReplacementPattern);
            if (closing == start) return Error(Pcre2ErrorKind.MalformedReplacementPattern);
            var split = -1;
            if (Extended)
                for (var index = start; index + 1 < closing; index++)
                {
                    Work();
                    if (text[index] == ':' && text[index + 1] is '+' or '-') { split = index; break; }
                    if (!NamePart(text[index]) && text[index] != '*') break;
                }
            if (split > start)
            {
                var conditional = text[split + 1] == '+';
                var instruction = Selector(start, split - start, conditional ? Pcre2ReplacementOpcode.Conditional : Pcre2ReplacementOpcode.Fallback);
                var branchStart = split + 2;
                if (!conditional) return instruction with { ElseFragment = Fragment(branchStart, closing) };
                var separator = BranchSeparator(branchStart, closing);
                return instruction with { ThenFragment = Fragment(branchStart, separator < 0 ? closing : separator),
                    ElseFragment = separator < 0 ? -1 : Fragment(separator + 1, closing) };
            }
            var valid = true;
            for (var index = start; index < closing; index++) { Work(); if (!NamePart(text[index])) valid = false; }
            if (!valid && text[start] != '*')
                return Error(Pcre2ErrorKind.MalformedReplacementPattern);
            return Selector(start, closing - start, Pcre2ReplacementOpcode.Reference);
        }

        private int CloseBrace(int start, int end)
        {
            var depth = 0; var quoted = false;
            for (var index = start; index < end; index++)
            {
                Work(); var ch = text[index];
                if (ch == '\\' && index + 1 < end)
                {
                    var escaped = text[++index];
                    if (quoted) { if (escaped == 'E') quoted = false; continue; }
                    if (escaped == 'Q') { quoted = true; continue; }
                    if (escaped == 'c' && index + 1 < end) { index++; Work(); continue; }
                    if (escaped is 'o' or 'x' or 'N' && index + 1 < end && text[index + 1] == '{')
                    { while (++index < end && text[index] != '}') Work(); if (index == end) return -1; }
                    continue;
                }
                if (quoted) continue;
                if (ch == '$' && index + 1 < end && text[index + 1] == '{') { depth++; index++; }
                else if (ch == '}') { if (depth == 0) return index; depth--; }
            }
            return -1;
        }

        private int BranchSeparator(int start, int end)
        {
            var depth = 0; var quoted = false;
            for (var index = start; index < end; index++)
            {
                Work(); var ch = text[index];
                if (ch == '\\' && index + 1 < end)
                {
                    var escaped = text[++index];
                    if (quoted) { if (escaped == 'E') quoted = false; }
                    else if (escaped == 'Q') quoted = true;
                    else if (escaped == 'c' && index + 1 < end) { index++; Work(); }
                    continue;
                }
                if (quoted) continue;
                if (ch == '$' && index + 1 < end && text[index + 1] == '{') { depth++; index++; }
                else if (ch == '}' && depth > 0) depth--;
                else if (ch == ':' && depth == 0) return index;
            }
            return -1;
        }

        private Pcre2ReplacementInstruction Selector(int start, int length, Pcre2ReplacementOpcode kind)
        {
            if (length == 5 && text.StartsWithAscii(start, "*MARK"))
            { storage.Budget.ChargeWork((ulong)length); return Op(kind) with { Selector = Pcre2ReplacementSelectorKind.Mark }; }
            var number = 0; var digits = length != 0; var overflow = false;
            for (var index = start; index < start + length; index++)
            {
                Work();
                var ch = text[index];
                if (!char.IsAsciiDigit(ch)) { digits = false; break; }
                var digit = ch - '0';
                if (number > (int.MaxValue - digit) / 10) overflow = true;
                else if (!overflow) number = number * 10 + digit;
            }
            if (digits) return Op(kind) with { Argument = overflow ? -1 : number };
            return Op(kind) with { Selector = Pcre2ReplacementSelectorKind.Name, Payload = Pcre2ReplacementPreparationControl.EncodeSource(storage, text, start, length) };
        }

        private Pcre2ReplacementInstruction Angle(int start, int end, bool escaped, out int next)
        {
            var closing = start;
            while (closing < end && text[closing] != '>') { Work(); closing++; }
            next = closing == end ? end : closing + 1;
            if (closing == end) return escaped ? Error(Pcre2ErrorKind.BadReplacementEscape) : Op(Pcre2ReplacementOpcode.AngleUnclosed);
            if (closing == start || text[start] == '*') return Error(escaped ? Pcre2ErrorKind.BadReplacementEscape : Pcre2ErrorKind.MalformedReplacementPattern);
            return escaped ? Selector(start, closing - start, Pcre2ReplacementOpcode.Reference)
                : Op(Pcre2ReplacementOpcode.Reference) with { Selector = Pcre2ReplacementSelectorKind.Name, Payload = Pcre2ReplacementPreparationControl.EncodeSource(storage, text, start, closing - start) };
        }

        private Pcre2ReplacementInstruction Escape(int position, int end, out int next)
        {
            next = position + 1;
            if (next == end) return Literal("\\".AsSpan());
            var ch = text[next++];
            switch (ch)
            {
                case 'Q': return Op(Pcre2ReplacementOpcode.Quote);
                case 'E': return Op(Pcre2ReplacementOpcode.EndCase);
                case 'U': return Op(Pcre2ReplacementOpcode.Upper);
                case 'L': return Op(Pcre2ReplacementOpcode.Lower);
                case 'u': return Op(Pcre2ReplacementOpcode.UpperNext);
                case 'l': return Op(Pcre2ReplacementOpcode.LowerNext);
                case 'a': return Scalar(7);
                case 'b': return Scalar(8);
                case 'e': return Scalar(27);
                case 'f': return Scalar(12);
                case 'n': return Scalar(10);
                case 'r': return Scalar(13);
                case 't': return Scalar(9);
                case 'v': return Scalar(11);
                case '\\': return Literal("\\".AsSpan());
                case 'g':
                    if (next < end && text[next] == '<') return Angle(next + 1, end, true, out next);
                    return Error(Pcre2ErrorKind.BadReplacementEscape);
                case 'o': return NumericEscape(next, end, 8, true, out next);
                case 'x': return NumericEscape(next, end, 16, false, out next);
                case 'N':
                    if (next + 3 < end && text.StartsWithAscii(next, "{U+"))
                        return UnicodeEscape(next + 3, end, out next);
                    return Error(Pcre2ErrorKind.BadReplacementEscape);
                case 'c':
                    if (next < end && text[next] is >= ' ' and <= '~')
                    { var control = text[next++]; return Scalar((control is >= 'a' and <= 'z' ? control - 32 : control) ^ 64); }
                    return Error(Pcre2ErrorKind.BadReplacementEscape);
            }
            if (char.IsAsciiDigit(ch))
            {
                var start = position + 1;
                while (next < end && char.IsAsciiDigit(text[next])) { Work(); next++; }
                if (ch == '0')
                {
                    var octalEnd = start;
                    while (octalEnd < end && octalEnd - start < 3 && text[octalEnd] is >= '0' and <= '7') { Work(); octalEnd++; }
                    next = octalEnd;
                    return NumericValue(start, octalEnd - start, 8);
                }
                var reference = Selector(start, next - start, Pcre2ReplacementOpcode.BackslashDigits);
                // Decimal IDs 8/9 and one-digit IDs always denote references.
                // Other runs may be an octal prefix when that group is absent.
                if (ch is '8' or '9' || next - start == 1) return reference with { Kind = Pcre2ReplacementOpcode.Reference };
                var octal = start;
                while (octal < next && octal - start < 3 && text[octal] is >= '0' and <= '7') { Work(); octal++; }
                var fallback = Fragment(octal, next);
                var value = 0;
                for (var index = start; index < octal; index++) { Work(); value = value * 8 + text[index] - '0'; }
                return reference with { Payload = Scalar(value).Payload, ElseFragment = fallback };
            }
            if (char.IsAsciiLetter(ch)) return Error(Pcre2ErrorKind.BadReplacementEscape);
            _ = text.DecodeScalar(position + 1, out _, out var consumed);
            next = position + 1 + Math.Max(1, consumed);
            return Literal(position + 1, next - position - 1);
        }

        private Pcre2ReplacementInstruction UnicodeEscape(int first, int end, out int next)
        {
            var closing = first;
            while (closing < end && text[closing] != '}') { Work(); closing++; }
            next = closing < end ? closing + 1 : end;
            return closing == end ? Error(Pcre2ErrorKind.BadReplacementEscape) : NumericValue(first, closing - first, 16);
        }

        private Pcre2ReplacementInstruction NumericEscape(int start, int end, int radix, bool requireBraces, out int next)
        {
            if (start < end && text[start] == '{')
            {
                var closing = start + 1;
                while (closing < end && text[closing] != '}') { Work(); closing++; }
                next = closing < end ? closing + 1 : end;
                return closing == end ? Error(Pcre2ErrorKind.BadReplacementEscape) : NumericValue(start + 1, closing - start - 1, radix);
            }
            next = start;
            if (requireBraces) return Error(Pcre2ErrorKind.BadReplacementEscape);
            while (next < end && next - start < 2 && Digit(text[next]) is >= 0 and < 16) { Work(); next++; }
            return NumericValue(start, next - start, radix);
        }

        private Pcre2ReplacementInstruction NumericValue(int start, int length, int radix)
        {
            if (length == 0) return Error(Pcre2ErrorKind.BadReplacementEscape);
            var value = 0;
            for (var index = start; index < start + length; index++)
            {
                Work(); var digit = Digit(text[index]);
                if (digit < 0 || digit >= radix || value > (0x10ffff - digit) / radix) return Error(Pcre2ErrorKind.BadReplacementEscape);
                value = value * radix + digit;
            }
            return Scalar(value);
        }

        private static int Digit(char value) => value switch
        { >= '0' and <= '9' => value - '0', >= 'a' and <= 'f' => value - 'a' + 10, >= 'A' and <= 'F' => value - 'A' + 10, _ => -1 };
        private static bool NameStart(char value) => char.IsAsciiLetter(value) || value == '_';
        private static bool NamePart(char value) => char.IsAsciiLetterOrDigit(value) || value == '_';
    }
}
