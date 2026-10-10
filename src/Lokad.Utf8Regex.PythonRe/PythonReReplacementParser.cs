using System.Buffers;
using System.Globalization;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

// Template grammar is text interoperability. Published literals are complete
// UTF-8 scalar fragments; matching and substitution use their byte payloads.
internal static class PythonReReplacementParser
{
    public static PythonReReplacementPlan Parse(string replacement, int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(scope.Budget);
        var tokens = storage.Buffer<PythonReReplacementToken>();
        var literal = storage.Text();
        var index = 0;
        while (index < replacement.Length)
        {
            storage.Budget.ChargeWork(1);
            var ch = replacement[index++];
            if (ch != '\\') { literal.Append(ch); continue; }
            if (index >= replacement.Length)
                throw new PythonRePatternException("bad escape (end of pattern)", index - 1);
            var escaped = replacement[index++];
            switch (escaped)
            {
                case '\\': literal.Append('\\'); break;
                case 'n': literal.Append('\n'); break;
                case 'r': literal.Append('\r'); break;
                case 't': literal.Append('\t'); break;
                case 'v': literal.Append('\v'); break;
                case 'f': literal.Append('\f'); break;
                case 'a': literal.Append('\a'); break;
                case 'b': literal.Append('\b'); break;
                case 'g':
                    FlushLiteral(storage, tokens, literal);
                    tokens.Add(ParseNamedOrNumberedGroup(storage, replacement, ref index, captureGroupCount, namedGroups));
                    break;
                case '0': literal.Append(ParseOctalEscape(storage, replacement, ref index, escaped)); break;
                case >= '1' and <= '9':
                    FlushLiteral(storage, tokens, literal);
                    tokens.Add(ParseNumberedGroupOrOctalEscape(storage, replacement, ref index, escaped, captureGroupCount));
                    break;
                default:
                    if (char.IsAsciiLetter(escaped))
                        throw new PythonRePatternException(storage.Text().Append("bad escape \\").Append(escaped).ToText());
                    literal.Append('\\').Append(escaped);
                    break;
            }
        }
        FlushLiteral(storage, tokens, literal);
        var result = new PythonReReplacementPlan(storage.Copy(tokens), CollectLiteralBytes(storage, tokens));
        storage.Complete();
        scope.Complete();
        return result;
    }

    private static PythonReReplacementToken ParseNamedOrNumberedGroup(Utf8ConstructionSession storage,
        string replacement, ref int index, int captureGroupCount, IReadOnlyDictionary<string, int> namedGroups)
    {
        storage.Budget.ChargeWork(1);
        if (index >= replacement.Length || replacement[index] != '<')
            throw new PythonRePatternException("missing <");
        var start = ++index;
        while (index < replacement.Length && replacement[index] != '>')
        { storage.Budget.ChargeWork(1); index++; }
        if (index >= replacement.Length)
            throw new PythonRePatternException(index == start ? "missing group name" : "missing >, unterminated name", start);
        var name = replacement.AsSpan(start, index - start);
        index++;
        if (name.IsEmpty) throw new PythonRePatternException("missing group name", start);
        var numeric = true;
        foreach (var ch in name)
        {
            storage.Budget.ChargeWork(1);
            if (!char.IsAsciiDigit(ch)) { numeric = false; break; }
        }
        if (numeric)
        {
            var number = int.Parse(name, CultureInfo.InvariantCulture);
            ValidateGroupReference(storage, number, captureGroupCount);
            return PythonReReplacementToken.ForGroupNumber(number);
        }
        var lookup = storage.Budget.CopyText(name);
        if (!PythonReGroupNameValidator.IsValid(lookup))
            throw new PythonRePatternException(storage.Text().Append("bad character in group name '").Append(lookup).Append('\'').ToText(), start);
        storage.Budget.ChargeWork((ulong)name.Length + 1);
        if (!namedGroups.TryGetValue(lookup, out var groupNumber))
            throw new IndexOutOfRangeException(storage.Text().Append("unknown group name '").Append(lookup).Append('\'').ToText());
        return PythonReReplacementToken.ForGroupNumber(groupNumber);
    }

    private static char ParseOctalEscape(Utf8ConstructionSession storage, string replacement, ref int index, char firstDigit)
    {
        var value = firstDigit - '0';
        for (var digits = 0; digits < 2 && index < replacement.Length && replacement[index] is >= '0' and <= '7'; digits++)
        { storage.Budget.ChargeWork(1); value = value * 8 + replacement[index++] - '0'; }
        return (char)value;
    }

    private static PythonReReplacementToken ParseNumberedGroupOrOctalEscape(Utf8ConstructionSession storage,
        string replacement, ref int index, char firstDigit, int captureGroupCount)
    {
        storage.Budget.ChargeWork(1);
        var start = index - 1;
        var number = firstDigit - '0';
        if (index < replacement.Length && char.IsAsciiDigit(replacement[index]))
        {
            var second = replacement[index++];
            storage.Budget.ChargeWork(1);
            number = number * 10 + second - '0';
            if (firstDigit is >= '0' and <= '7' && second is >= '0' and <= '7' &&
                index < replacement.Length && replacement[index] is >= '0' and <= '7')
            {
                var value = (firstDigit - '0') * 64 + (second - '0') * 8 + replacement[index++] - '0';
                storage.Budget.ChargeWork(1);
                if (value > 0xff)
                    throw new PythonRePatternException(storage.Text().Append("octal escape value \\").Append(replacement.AsSpan(start, 3)).Append(" outside of range 0-0o377").ToText());
                Span<char> octal = stackalloc char[1];
                octal[0] = (char)value;
                return PythonReReplacementToken.ForLiteral(EncodeFragment(storage, octal));
            }
        }
        ValidateGroupReference(storage, number, captureGroupCount);
        return PythonReReplacementToken.ForGroupNumber(number);
    }

    private static void ValidateGroupReference(Utf8ConstructionSession storage, int number, int captureGroupCount)
    {
        storage.Budget.ChargeWork(1);
        if (number > captureGroupCount)
            throw new PythonRePatternException(storage.Text().Append("invalid group reference ").Append(number).ToText());
    }

    private static void FlushLiteral(Utf8ConstructionSession storage,
        Utf8ConstructionBuffer<PythonReReplacementToken> tokens, Utf8ConstructionText literal)
    {
        if (literal.Length == 0) return;
        // Borrow the admitted scratch span, so no intermediate literal string is
        // acquired. Fragment boundaries repair unpaired surrogates independently.
        tokens.Add(PythonReReplacementToken.ForLiteral(EncodeFragment(storage, literal.Span)));
        literal.Clear();
    }

    private static byte[] EncodeFragment(Utf8ConstructionSession storage, ReadOnlySpan<char> text)
    {
        var count = 0;
        for (var index = 0; index < text.Length;)
        {
            storage.Budget.ChargeWork(1);
            var rune = ReadScalar(text, ref index);
            count = checked(count + rune.Utf8SequenceLength);
        }
        var result = storage.Allocate<byte>(count);
        var written = 0;
        for (var index = 0; index < text.Length;)
        {
            storage.Budget.ChargeWork(1);
            var rune = ReadScalar(text, ref index);
            storage.Budget.ChargeWork((ulong)rune.Utf8SequenceLength);
            written += rune.EncodeToUtf8(result.AsSpan(written));
        }
        return result;
    }

    private static Rune ReadScalar(ReadOnlySpan<char> text, ref int index)
    {
        var status = Rune.DecodeFromUtf16(text[index..], out var rune, out var consumed);
        index += Math.Max(1, consumed);
        return status == OperationStatus.Done ? rune : Rune.ReplacementChar;
    }

    private static byte[]? CollectLiteralBytes(Utf8ConstructionSession storage, Utf8ConstructionBuffer<PythonReReplacementToken> tokens)
    {
        var length = 0;
        foreach (var token in tokens)
        {
            storage.Budget.ChargeWork(1);
            if (token.Kind != PythonReReplacementTokenKind.Literal) return null;
            length = checked(length + token.RequiredUtf8.Length);
        }
        if (tokens.Count == 1) return tokens[0].RequiredUtf8;
        var result = storage.Allocate<byte>(length);
        var offset = 0;
        foreach (var token in tokens)
        {
            var bytes = token.RequiredUtf8;
            storage.Budget.ChargeWork((ulong)bytes.Length + 1);
            bytes.CopyTo(result, offset);
            offset += bytes.Length;
        }
        return result;
    }
}

// This value borrows the admitted token graph; no duplicate holder loan.
internal readonly record struct PythonReReplacementPlan(PythonReReplacementToken[] Tokens, byte[]? LiteralUtf8)
{
    public bool TryGetLiteralUtf8(out byte[] literal)
    { literal = LiteralUtf8 ?? []; return LiteralUtf8 is not null; }

    public byte[] ExpandToUtf8(ReadOnlySpan<byte> input, PythonReGroupData[] groups)
    {
        if (Tokens.Length == 0) return [];
        // Invocation/result capacity remains a separate common-policy gate.
        var length = 0;
        foreach (var token in Tokens)
        {
            switch (token.Kind)
            {
                case PythonReReplacementTokenKind.Literal: length = checked(length + token.RequiredUtf8.Length); break;
                case PythonReReplacementTokenKind.GroupNumber:
                    if ((uint)token.Number < (uint)groups.Length && groups[token.Number].Success)
                        length = checked(length + (groups[token.Number].EndOffsetInBytes - groups[token.Number].StartOffsetInBytes));
                    break;
            }
        }
        if (length == 0) return [];
        var result = new byte[length];
        var offset = 0;
        foreach (var token in Tokens)
        {
            ReadOnlySpan<byte> value = token.Kind == PythonReReplacementTokenKind.Literal ? token.RequiredUtf8.AsSpan() :
                (uint)token.Number < (uint)groups.Length && groups[token.Number].Success
                    ? input[groups[token.Number].StartOffsetInBytes..groups[token.Number].EndOffsetInBytes] : [];
            value.CopyTo(result.AsSpan(offset));
            offset += value.Length;
        }
        return result;
    }
}

internal readonly record struct PythonReReplacementToken(PythonReReplacementTokenKind Kind, byte[]? Utf8, int Number)
{
    public byte[] RequiredUtf8 => Utf8 ?? throw new InvalidOperationException($"Replacement token {Kind} does not carry literal bytes.");
    public static PythonReReplacementToken ForLiteral(byte[] bytes) => new(PythonReReplacementTokenKind.Literal, bytes, 0);
    public static PythonReReplacementToken ForGroupNumber(int number) => new(PythonReReplacementTokenKind.GroupNumber, null, number);
}

internal enum PythonReReplacementTokenKind { Literal, GroupNumber }
