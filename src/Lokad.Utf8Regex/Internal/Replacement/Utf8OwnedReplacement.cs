using System.Globalization;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Replacement;

// Owned replacement template parsing, pinned against legacy instances over the
// probe catalog. Dollar introduces whole match, portions, last group, whole
// string, numbered and named group references while anything else stays
// literal, with maximal literal runs, greedy digits, braced names validated by
// a word character head, int overflow falling back to literal, and unclosed
// braces degrading to literal runs. Group resolution validates numbered groups
// against the valid numbers, maps names through the parallel arrays, demotes
// unknown references to their original literal text with adjacent literals
// merged brace free, and extracts literal text when nothing substitutes.
// Invalid replacement syntax degrades to literal text; resource admission can throw. Token kind
// names follow the public documented replacement syntax. Every rule above was
// read off probe behavior.
internal enum Utf8ReplacementTokenKind : byte
{
    Literal,
    Group,
    WholeMatch,
    LeftPortion,
    RightPortion,
    LastGroup,
    WholeString,
}

internal readonly struct Utf8ReplacementToken
{
    public Utf8ReplacementToken(Utf8ReplacementTokenKind kind, object? literal, int groupNumber, bool isBraceEnclosed)
        : this(kind, literal, groupNumber, isBraceEnclosed, null)
    {
    }

    public Utf8ReplacementToken(Utf8ReplacementTokenKind kind, object? literal, int groupNumber, bool isBraceEnclosed, object? referenceText)
    {
        Kind = kind;
        LiteralPayload = literal;
        GroupNumber = groupNumber;
        IsBraceEnclosed = isBraceEnclosed;
        ReferenceText = referenceText;
    }

    public Utf8ReplacementTokenKind Kind { get; }

    public object? LiteralPayload { get; }
    public string? Literal => LiteralPayload as string;

    public int GroupNumber { get; }

    public bool IsBraceEnclosed { get; }

    // Undefined numeric references remain literal, including their leading zeros.
    public object? ReferenceText { get; }

}


internal sealed class Utf8ReplacementPattern
{
    private Utf8ReplacementPattern(Utf8ReplacementToken[] tokens, bool substitutions, bool groups, bool names, bool special)
    {
        Tokens = tokens;
        ContainsSubstitutions = substitutions;
        ContainsGroupReferences = groups;
        ContainsNamedGroups = names;
        ContainsSpecialSubstitutions = special;
    }

    public Utf8ReplacementToken[] Tokens { get; }
    public bool ContainsSubstitutions { get; }
    public bool ContainsGroupReferences { get; }
    public bool ContainsNamedGroups { get; }
    public bool ContainsSpecialSubstitutions { get; }

    private static Utf8ReplacementPattern Create(Utf8ConstructionSession storage, Utf8ReplacementToken[] tokens)
    {
        var substitutions = false;
        var groups = false;
        var names = false;
        var special = false;
        foreach (var token in tokens)
        {
            storage.Budget.ChargeWork(1);
            substitutions |= token.Kind != Utf8ReplacementTokenKind.Literal;
            groups |= token.Kind == Utf8ReplacementTokenKind.Group;
            names |= token.Kind == Utf8ReplacementTokenKind.Group && token.GroupNumber < 0 && token.Literal is not null;
            special |= token.Kind is Utf8ReplacementTokenKind.WholeMatch or Utf8ReplacementTokenKind.LeftPortion or
                Utf8ReplacementTokenKind.RightPortion or Utf8ReplacementTokenKind.LastGroup or Utf8ReplacementTokenKind.WholeString;
        }
        return storage.Budget.AllocateObject((tokens, substitutions, groups, names, special), static state =>
            new Utf8ReplacementPattern(state.tokens, state.substitutions, state.groups, state.names, state.special));
    }

    public static Utf8ReplacementPattern Parse(string replacement) => Parse(replacement, [], false);

    internal static Utf8ReplacementPattern Parse(string replacement, ReadOnlySpan<int> validGroupNumbers, bool ecma)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);
        var result = Parse(storage, replacement, validGroupNumbers, ecma);
        storage.Complete(); owner.Complete();
        return result;
    }

    internal static Utf8ReplacementPattern Parse(Utf8ConstructionSession storage, string replacement,
        ReadOnlySpan<int> validGroupNumbers, bool ecma)
        => Parse(storage, new Utf8ConstructionPattern(replacement), validGroupNumbers, ecma);

    // Native callers validate the complete source before route selection.
    internal static Utf8ReplacementPattern Parse(Utf8ConstructionSession storage, Utf8ConstructionPattern replacement,
        ReadOnlySpan<int> validGroupNumbers, bool ecma)
    {
        var tokens = storage.Buffer<Utf8ReplacementToken>();
        var literal = replacement.IsUtf8 ? null : storage.Text();
        var nativeLiteral = replacement.IsUtf8 ? storage.Buffer<byte>() : null;
        var position = 0;
        while (position < replacement.Length)
        {
            storage.Budget.ChargeWork(1);
            var current = replacement[position];
            if (current != '$')
            {
                var maximum = Math.Min(2048, replacement.Length - position);
                var dollar = replacement.IndexOf('$', position, maximum);
                var length = dollar < 0 ? maximum : dollar - position;
                storage.Budget.ChargeWork((ulong)(length - 1));
                AppendLiteralRange(replacement, position, length, literal, nativeLiteral);
                position += length; continue;
            }
            if (position + 1 >= replacement.Length) { AppendAscii('$', literal, nativeLiteral); position++; continue; }
            var next = replacement[position + 1];
            if (next == '$') { AppendAscii('$', literal, nativeLiteral); position += 2; continue; }
            if (TryScanSpecial(next, out var special))
            {
                FlushLiteral(storage, tokens, literal, nativeLiteral); tokens.Add(special); position += 2; continue;
            }
            if (IsAsciiDigit(next))
            {
                position = AppendNumberedGroup(storage, replacement, position, tokens, literal, nativeLiteral, validGroupNumbers, ecma);
                continue;
            }
            if (next == '{')
            {
                position = AppendBracedGroup(storage, replacement, position, tokens, literal, nativeLiteral); continue;
            }
            AppendAscii('$', literal, nativeLiteral); position++;
        }
        FlushLiteral(storage, tokens, literal, nativeLiteral);
        return Create(storage, storage.Copy(tokens, true));
    }

    private static void FlushLiteral(Utf8ConstructionSession storage, Utf8ConstructionBuffer<Utf8ReplacementToken> tokens,
        Utf8ConstructionText? literal, Utf8ConstructionBuffer<byte>? nativeLiteral)
    {
        if (nativeLiteral is not null)
        {
            if (nativeLiteral.Count == 0) return;
            tokens.Add(new(Utf8ReplacementTokenKind.Literal, storage.Copy(nativeLiteral), -1, false));
            nativeLiteral.Clear();
        }
        else if (literal is { Length: > 0 })
        {
            tokens.Add(new(Utf8ReplacementTokenKind.Literal, literal.ToText(), -1, false)); literal.Clear();
        }
    }

    private static void AppendAscii(char value, Utf8ConstructionText? literal, Utf8ConstructionBuffer<byte>? nativeLiteral)
    { if (nativeLiteral is not null) nativeLiteral.Add((byte)value); else literal!.Append(value); }

    private static void AppendLiteralRange(Utf8ConstructionPattern source, int start, int length,
        Utf8ConstructionText? literal, Utf8ConstructionBuffer<byte>? nativeLiteral)
    {
        if (nativeLiteral is not null) nativeLiteral.AddRange(source.Utf8.Slice(start, length));
        else literal!.Append(source.Text.AsSpan(start, length));
    }

    private static object CopySource(Utf8ConstructionSession storage, Utf8ConstructionPattern source, int start, int length)
    {
        if (!source.IsUtf8) return source.CopyText(start, length);
        var result = storage.Allocate<byte>(length);
        for (var offset = 0; offset < length;)
        {
            var count = Math.Min(2048, length - offset);
            storage.Budget.ChargeWork((ulong)count);
            source.Utf8.Slice(start + offset, count).CopyTo(result.AsSpan(offset, count)); offset += count;
        }
        return result;
    }

    private static bool TryScanSpecial(char next, out Utf8ReplacementToken special)
    {
        Utf8ReplacementTokenKind? kind = next switch
        {
            '&' => Utf8ReplacementTokenKind.WholeMatch,
            '`' => Utf8ReplacementTokenKind.LeftPortion,
            '\'' => Utf8ReplacementTokenKind.RightPortion,
            '+' => Utf8ReplacementTokenKind.LastGroup,
            '_' => Utf8ReplacementTokenKind.WholeString,
            _ => null,
        };
        special = kind is null ? default : new Utf8ReplacementToken(kind.Value, null, -1, false);
        return kind is not null;
    }

    private static bool IsAsciiDigit(char value) => value >= '0' && value <= '9';

    private static int AppendNumberedGroup(Utf8ConstructionSession storage, Utf8ConstructionPattern replacement, int position,
        Utf8ConstructionBuffer<Utf8ReplacementToken> tokens, Utf8ConstructionText? literal, Utf8ConstructionBuffer<byte>? nativeLiteral,
        ReadOnlySpan<int> validGroupNumbers, bool ecma)
    {
        var value = 0L;
        var end = position + 1;
        var matchedEnd = -1;
        var matchedNumber = -1;
        while (end < replacement.Length && IsAsciiDigit(replacement[end]))
        {
            storage.Budget.ChargeWork(1);
            value = value * 10 + (replacement[end] - '0');
            if (value > int.MaxValue)
            {
                if (ecma) break;
                AppendAscii('$', literal, nativeLiteral); return position + 1;
            }
            end++;
            if (ecma && ContainsNumber(storage.Budget, validGroupNumbers, (int)value))
            { matchedEnd = end; matchedNumber = (int)value; }
        }
        if (ecma)
        {
            if (matchedEnd < 0) { AppendAscii('$', literal, nativeLiteral); return position + 1; }
            value = matchedNumber; end = matchedEnd;
        }
        FlushLiteral(storage, tokens, literal, nativeLiteral);
        tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, null, (int)value, false,
            CopySource(storage, replacement, position, end - position)));
        return end;
    }

    private static int AppendBracedGroup(Utf8ConstructionSession storage, Utf8ConstructionPattern replacement, int position,
        Utf8ConstructionBuffer<Utf8ReplacementToken> tokens, Utf8ConstructionText? literal, Utf8ConstructionBuffer<byte>? nativeLiteral)
    {
        var close = position + 2;
        while (close < replacement.Length)
        {
            storage.Budget.ChargeWork(1);
            if (replacement[close] == '}') break;
            close++;
        }
        if (close == replacement.Length)
        { AppendAscii('$', literal, nativeLiteral); AppendAscii('{', literal, nativeLiteral); return position + 2; }
        var start = position + 2; var length = close - start;
        if (TryParseGroupNumber(storage.Budget, replacement, start, length, out var number))
        {
            FlushLiteral(storage, tokens, literal, nativeLiteral);
            tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, null, number, true,
                CopySource(storage, replacement, position, close - position + 1)));
            return close + 1;
        }
        // The capture-name grammar retains its existing UTF-16 head rule. Only
        // the selected name lexeme projects text; native literals never do.
        var head = length == 0 ? '\0' : replacement[start];
        if (replacement.IsUtf8 && length > 0)
        {
            _ = replacement.DecodeScalar(start, out var scalar, out _);
            head = scalar.IsBmp ? (char)scalar.Value : '\ud800';
        }
        if (length > 0 && IsWordStart(head))
        {
            FlushLiteral(storage, tokens, literal, nativeLiteral);
            tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, replacement.CopyText(start, length), -1, true,
                replacement.IsUtf8 ? CopySource(storage, replacement, position, close - position + 1) : null));
            return close + 1;
        }
        AppendLiteralRange(replacement, position, close - position + 1, literal, nativeLiteral);
        return close + 1;
    }

    private static bool TryParseGroupNumber(Utf8ConstructionBudget budget, Utf8ConstructionPattern source,
        int start, int length, out int number)
    {
        number = 0;
        if (length == 0) return false;
        var value = 0L;
        for (var index = start; index < start + length; index++)
        {
            budget.ChargeWork(1);
            var c = source[index];
            if (!IsAsciiDigit(c)) return false;
            value = value * 10 + (c - '0');
            if (value > int.MaxValue) return false;
        }
        number = (int)value; return true;
    }

    private static bool IsWordStart(char value) => char.GetUnicodeCategory(value) is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or
        UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.NonSpacingMark or
        UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;

    private static bool ContainsNumber(Utf8ConstructionBudget budget, ReadOnlySpan<int> numbers, int candidate)
    {
        foreach (var number in numbers)
        {
            budget.ChargeWork(1);
            if (number == candidate) return true;
        }
        return false;
    }

    // Resolve once into scratch storage. The analyzer lowers these tokens directly,
    // keeping the original pattern flags without acquiring a second pattern holder.
    internal Utf8ConstructionBuffer<Utf8ReplacementToken> ResolveTokens(Utf8ConstructionSession storage,
        ReadOnlySpan<int> validGroupNumbers, ReadOnlySpan<string> validGroupNames)
    {
        var resolved = storage.Buffer<Utf8ReplacementToken>();
        Utf8ReplacementToken? pending = null;
        Utf8ConstructionText? merged = null;
        Utf8ConstructionBuffer<byte>? mergedNative = null;
        foreach (var token in Tokens)
        {
            storage.Budget.ChargeWork(1);
            var normalized = NormalizeGroupReference(storage, token, validGroupNumbers, validGroupNames);
            if (normalized.Kind == Utf8ReplacementTokenKind.Literal)
            {
                if (pending is { } previous)
                {
                    if (previous.LiteralPayload is byte[] bytes)
                    {
                        if (mergedNative is null) { mergedNative = storage.Buffer<byte>(); mergedNative.AddRange(bytes); }
                        mergedNative.AddRange((byte[])normalized.LiteralPayload!);
                    }
                    else
                    {
                        if (merged is null) { merged = storage.Text(); merged.Append(previous.Literal ?? string.Empty); }
                        merged.Append(normalized.Literal ?? string.Empty);
                    }
                }
                else pending = normalized;
            }
            else
            {
                FlushPending(storage, resolved, ref pending, ref merged, ref mergedNative); resolved.Add(normalized);
            }
        }
        FlushPending(storage, resolved, ref pending, ref merged, ref mergedNative);
        return resolved;

    }

    private static void FlushPending(Utf8ConstructionSession storage, Utf8ConstructionBuffer<Utf8ReplacementToken> resolved,
        ref Utf8ReplacementToken? pending, ref Utf8ConstructionText? merged, ref Utf8ConstructionBuffer<byte>? mergedNative)
    {
        if (pending is not { } literal) return;
        storage.Budget.ChargeWork(1);
        resolved.Add(mergedNative is not null ? new(Utf8ReplacementTokenKind.Literal, storage.Copy(mergedNative), -1, false)
            : merged is null ? literal : new(Utf8ReplacementTokenKind.Literal, merged.ToText(), -1, false));
        pending = null;
        if (merged is not null) { merged.Clear(); merged = null; }
        if (mergedNative is not null) { mergedNative.Clear(); mergedNative = null; }
    }

    private static Utf8ReplacementToken NormalizeGroupReference(Utf8ConstructionSession storage, Utf8ReplacementToken token,
        ReadOnlySpan<int> validGroupNumbers, ReadOnlySpan<string> validGroupNames)
    {
        if (token.Kind != Utf8ReplacementTokenKind.Group) return token;
        if (token.GroupNumber >= 0)
        {
            if (ContainsNumber(storage.Budget, validGroupNumbers, token.GroupNumber)) return token;
            var text = token.ReferenceText;
            if (text is null)
            {
                var builder = storage.Text();
                builder.Append(token.IsBraceEnclosed ? "${" : "$" ).Append(token.GroupNumber);
                if (token.IsBraceEnclosed) builder.Append('}');
                text = builder.ToText();
            }
            return new Utf8ReplacementToken(Utf8ReplacementTokenKind.Literal, text, -1, token.IsBraceEnclosed);
        }
        if (token.Literal is null) return token;
        for (var i = 0; i < validGroupNames.Length && i < validGroupNumbers.Length; i++)
        {
            storage.Budget.ChargeWork(1 + (ulong)Math.Min(validGroupNames[i]?.Length ?? 0, token.Literal.Length));
            if (string.Equals(validGroupNames[i], token.Literal, StringComparison.Ordinal))
                return new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, null, validGroupNumbers[i], token.IsBraceEnclosed);
        }
        if (token.ReferenceText is byte[] original)
            return new(Utf8ReplacementTokenKind.Literal, original, -1, true);
        var name = storage.Text().Append("${").Append(token.Literal).Append('}').ToText();
        return new Utf8ReplacementToken(Utf8ReplacementTokenKind.Literal, name, -1, true);
    }

}
