using System.Globalization;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

// Both borrowed source forms use one grammar. Native literals stay bytes;
// legacy text repairs unpaired surrogates at each literal-fragment boundary.
internal static class PythonReReplacementParser
{
    public static PythonReReplacementPlan Parse(string replacement, int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
        => Parse(new Utf8ConstructionPattern(replacement), captureGroupCount, namedGroups);

    internal static PythonReReplacementPlan Parse(ReadOnlySpan<byte> replacement, int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
        => Parse(new Utf8ConstructionPattern(replacement), captureGroupCount, namedGroups);

    private static PythonReReplacementPlan Parse(Utf8ConstructionPattern replacement, int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(scope.Budget);
        replacement.ValidateUtf8();
        var tokens = storage.Buffer<PythonReReplacementToken>();
        var literal = replacement.IsUtf8 ? null : storage.Text();
        var nativeLiteral = replacement.IsUtf8 ? storage.Buffer<byte>() : null;
        var index = 0;
        try
        {
            while (index < replacement.Length)
            {
                storage.Budget.ChargeWork(1);
                var ch = replacement[index++];
                if (ch != '\\')
                {
                    // Search/copy one bounded literal run instead of dispatching
                    // through the source representation for each character.
                    // Blocks may split a scalar/pair: the same fragment buffer
                    // joins them before encoding/publication.
                    var start = index - 1;
                    var maximum = Math.Min(2048, replacement.Length - start);
                    var escape = replacement.IndexOf('\\', start, maximum);
                    var length = escape < 0 ? maximum : escape - start;
                    storage.Budget.ChargeWork((ulong)(length - 1));
                    if (nativeLiteral is not null) nativeLiteral.AddRange(replacement.Utf8.Slice(start, length));
                    else literal!.Append(replacement.Text.AsSpan(start, length));
                    index = start + length;
                    continue;
                }
                if (index >= replacement.Length)
                    throw new PythonRePatternException("bad escape (end of pattern)", index - 1, PythonReErrorSource.Replacement);
                var escaped = replacement[index++];
                switch (escaped)
                {
                    case '\\': AppendLiteral('\\', literal, nativeLiteral); break;
                    case 'n': AppendLiteral('\n', literal, nativeLiteral); break;
                    case 'r': AppendLiteral('\r', literal, nativeLiteral); break;
                    case 't': AppendLiteral('\t', literal, nativeLiteral); break;
                    case 'v': AppendLiteral('\v', literal, nativeLiteral); break;
                    case 'f': AppendLiteral('\f', literal, nativeLiteral); break;
                    case 'a': AppendLiteral('\a', literal, nativeLiteral); break;
                    case 'b': AppendLiteral('\b', literal, nativeLiteral); break;
                    case 'g':
                        FlushLiteral(storage, tokens, literal, nativeLiteral);
                        tokens.Add(ParseNamedOrNumberedGroup(storage, replacement, ref index, captureGroupCount, namedGroups));
                        break;
                    case '0': AppendLiteral(ParseOctalEscape(storage, replacement, ref index, escaped), literal, nativeLiteral); break;
                    case >= '1' and <= '9':
                        FlushLiteral(storage, tokens, literal, nativeLiteral);
                        tokens.Add(ParseNumberedGroupOrOctalEscape(storage, replacement, ref index, escaped, captureGroupCount));
                        break;
                    default:
                        if (char.IsAsciiLetter(escaped))
                            throw new PythonRePatternException(storage.Text().Append("bad escape \\").Append(escaped).ToText(),
                                index - 2, PythonReErrorSource.Replacement);
                        if (nativeLiteral is not null)
                        { nativeLiteral.Add((byte)'\\'); nativeLiteral.Add((byte)escaped); }
                        else literal!.Append('\\').Append(escaped);
                        break;
                }
            }
            FlushLiteral(storage, tokens, literal, nativeLiteral);
            var result = new PythonReReplacementPlan(storage.Copy(tokens), CollectLiteralBytes(storage, tokens));
            storage.Complete();
            scope.Complete();
            return result;
        }
        catch (PythonRePatternException error) when (replacement.IsUtf8 && error.Position >= 0)
        {
            // Published diagnostics retain UTF-16 coordinates. Project only the
            // failed prefix, without acquiring a decoded template or index.
            var position = 0;
            for (var offset = 0; offset < error.Position;)
            {
                storage.Budget.ChargeWork(1);
                _ = Rune.DecodeFromUtf8(replacement.Utf8[offset..], out var scalar, out var consumed);
                offset += consumed;
                position += scalar.Utf16SequenceLength;
            }
            throw new PythonRePatternException(error.Message, position, error.ErrorSource, error.ErrorKind);
        }
    }

    private static PythonReReplacementToken ParseNamedOrNumberedGroup(Utf8ConstructionSession storage,
        Utf8ConstructionPattern replacement, ref int index, int captureGroupCount, IReadOnlyDictionary<string, int> namedGroups)
    {
        storage.Budget.ChargeWork(1);
        if (index >= replacement.Length || replacement[index] != '<')
            throw new PythonRePatternException("missing <", index, PythonReErrorSource.Replacement);
        var start = ++index;
        while (index < replacement.Length && replacement[index] != '>')
        { storage.Budget.ChargeWork(1); index++; }
        if (index >= replacement.Length)
            throw new PythonRePatternException(index == start ? "missing group name" : "missing >, unterminated name",
                start, PythonReErrorSource.Replacement);
        var length = index - start;
        index++;
        if (length == 0) throw new PythonRePatternException("missing group name", start, PythonReErrorSource.Replacement);
        var numeric = true;
        for (var offset = start; offset < start + length; offset++)
        {
            storage.Budget.ChargeWork(1);
            if (!char.IsAsciiDigit(replacement[offset])) { numeric = false; break; }
        }
        if (numeric)
        {
            var parsed = replacement.IsUtf8
                ? int.TryParse(replacement.Utf8.Slice(start, length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                : int.TryParse(replacement.Text.AsSpan(start, length), NumberStyles.None, CultureInfo.InvariantCulture, out number);
            if (!parsed)
            {
                var errorStart = start;
                while (length > 0 && replacement[start] == '0')
                { storage.Budget.ChargeWork(1); start++; length--; }
                throw new PythonRePatternException(storage.Text().Append("invalid group reference ")
                    .Append(replacement.CopyText(start, length)).ToText(), errorStart, PythonReErrorSource.Replacement);
            }
            ValidateGroupReference(storage, number, captureGroupCount, start);
            return PythonReReplacementToken.ForGroupNumber(number);
        }
        var lookup = replacement.CopyText(start, length);
        if (!PythonReGroupNameValidator.IsValid(lookup))
            throw new PythonRePatternException(storage.Text().Append("bad character in group name '").Append(lookup).Append('\'').ToText(),
                start, PythonReErrorSource.Replacement);
        storage.Budget.ChargeWork((ulong)length + 1);
        if (!namedGroups.TryGetValue(lookup, out var groupNumber))
            throw new IndexOutOfRangeException(storage.Text().Append("unknown group name '").Append(lookup).Append('\'').ToText());
        return PythonReReplacementToken.ForGroupNumber(groupNumber);
    }

    private static char ParseOctalEscape(Utf8ConstructionSession storage, Utf8ConstructionPattern replacement, ref int index, char firstDigit)
    {
        var value = firstDigit - '0';
        for (var digits = 0; digits < 2 && index < replacement.Length && replacement[index] is >= '0' and <= '7'; digits++)
        { storage.Budget.ChargeWork(1); value = value * 8 + replacement[index++] - '0'; }
        return (char)value;
    }

    private static PythonReReplacementToken ParseNumberedGroupOrOctalEscape(Utf8ConstructionSession storage,
        Utf8ConstructionPattern replacement, ref int index, char firstDigit, int captureGroupCount)
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
                    throw new PythonRePatternException(storage.Text().Append("octal escape value \\").Append(replacement.CopyText(start, 3)).Append(" outside of range 0-0o377").ToText(),
                        start - 1, PythonReErrorSource.Replacement);
                Span<char> octal = stackalloc char[1];
                octal[0] = (char)value;
                return PythonReReplacementToken.ForLiteral(storage.EncodeRepairedUtf8(octal));
            }
        }
        ValidateGroupReference(storage, number, captureGroupCount, start);
        return PythonReReplacementToken.ForGroupNumber(number);
    }

    private static void ValidateGroupReference(Utf8ConstructionSession storage, int number, int captureGroupCount, int position)
    {
        storage.Budget.ChargeWork(1);
        if (number > captureGroupCount)
            throw new PythonRePatternException(storage.Text().Append("invalid group reference ").Append(number).ToText(),
                position, PythonReErrorSource.Replacement);
    }

    private static void FlushLiteral(Utf8ConstructionSession storage,
        Utf8ConstructionBuffer<PythonReReplacementToken> tokens, Utf8ConstructionText? literal,
        Utf8ConstructionBuffer<byte>? nativeLiteral)
    {
        if (nativeLiteral is not null)
        {
            if (nativeLiteral.Count == 0) return;
            tokens.Add(PythonReReplacementToken.ForLiteral(storage.Copy(nativeLiteral)));
            nativeLiteral.Clear();
            return;
        }
        if (literal is null) throw new InvalidOperationException("Missing template literal storage.");
        if (literal.Length == 0) return;
        // Borrow the admitted scratch span, so no intermediate literal string is
        // acquired. Fragment boundaries repair unpaired surrogates independently.
        tokens.Add(PythonReReplacementToken.ForLiteral(storage.EncodeRepairedUtf8(literal.Span)));
        literal.Clear();
    }

    private static void AppendLiteral(char value, Utf8ConstructionText? literal,
        Utf8ConstructionBuffer<byte>? nativeLiteral)
    {
        if (nativeLiteral is null) { literal!.Append(value); return; }
        Span<byte> encoded = stackalloc byte[3];
        var length = new Rune(value).EncodeToUtf8(encoded);
        nativeLiteral.AddRange(encoded[..length]);
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

    internal void AppendToUtf8(ReadOnlySpan<byte> input,
        scoped ref PythonReScalarMatchCursor cursor, scoped ref Utf8SliceOutputWriter writer)
    {
        cursor.Checkpoint();
        foreach (var token in Tokens)
        {
            cursor.Checkpoint();
            ReadOnlySpan<byte> value = token.Kind == PythonReReplacementTokenKind.Literal ? token.RequiredUtf8.AsSpan() :
                cursor.TryGetCaptureByteRange(token.Number, out var start, out var end) ? input[start..end] : [];
            cursor.AppendBytes(ref writer, value);
        }
        cursor.Checkpoint();
    }

}

internal readonly record struct PythonReReplacementToken(PythonReReplacementTokenKind Kind, byte[]? Utf8, int Number)
{
    public byte[] RequiredUtf8 => Utf8 ?? throw new InvalidOperationException($"Replacement token {Kind} does not carry literal bytes.");
    public static PythonReReplacementToken ForLiteral(byte[] bytes) => new(PythonReReplacementTokenKind.Literal, bytes, 0);
    public static PythonReReplacementToken ForGroupNumber(int number) => new(PythonReReplacementTokenKind.GroupNumber, null, number);
}

internal enum PythonReReplacementTokenKind { Literal, GroupNumber }
