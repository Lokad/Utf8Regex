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
    public Utf8ReplacementToken(Utf8ReplacementTokenKind kind, string? literal, int groupNumber, bool isBraceEnclosed)
        : this(kind, literal, groupNumber, isBraceEnclosed, null)
    {
    }

    public Utf8ReplacementToken(Utf8ReplacementTokenKind kind, string? literal, int groupNumber, bool isBraceEnclosed, string? referenceText)
    {
        Kind = kind;
        Literal = literal;
        GroupNumber = groupNumber;
        IsBraceEnclosed = isBraceEnclosed;
        ReferenceText = referenceText;
    }

    public Utf8ReplacementTokenKind Kind { get; }

    public string? Literal { get; }

    public int GroupNumber { get; }

    public bool IsBraceEnclosed { get; }

    // Undefined numeric references remain literal, including their leading zeros.
    public string? ReferenceText { get; }

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
    {
        var tokens = storage.Buffer<Utf8ReplacementToken>();
        var literal = storage.Text();
        var position = 0;
        while (position < replacement.Length)
        {
            storage.Budget.ChargeWork(1);
            var current = replacement[position];
            if (current != '$') { literal.Append(current); position++; continue; }
            if (position + 1 >= replacement.Length) { literal.Append('$'); position++; continue; }
            var next = replacement[position + 1];
            if (next == '$') { literal.Append('$'); position += 2; continue; }
            if (TryScanSpecial(next, out var special))
            {
                FlushLiteral(tokens, literal); tokens.Add(special); position += 2; continue;
            }
            if (IsAsciiDigit(next))
            {
                position = AppendNumberedGroup(storage, replacement, position, tokens, literal, validGroupNumbers, ecma);
                continue;
            }
            if (next == '{')
            {
                position = AppendBracedGroup(storage, replacement, position, tokens, literal); continue;
            }
            literal.Append('$'); position++;
        }
        FlushLiteral(tokens, literal);
        return Create(storage, storage.Copy(tokens, true));
    }

    private static void FlushLiteral(Utf8ConstructionBuffer<Utf8ReplacementToken> tokens, Utf8ConstructionText literal)
    {
        if (literal.Length == 0) return;
        tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Literal, literal.ToText(), -1, false));
        literal.Clear();
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

    private static int AppendNumberedGroup(Utf8ConstructionSession storage, string replacement, int position,
        Utf8ConstructionBuffer<Utf8ReplacementToken> tokens, Utf8ConstructionText literal,
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
                literal.Append('$'); return position + 1;
            }
            end++;
            if (ecma && ContainsNumber(storage.Budget, validGroupNumbers, (int)value))
            { matchedEnd = end; matchedNumber = (int)value; }
        }
        if (ecma)
        {
            if (matchedEnd < 0) { literal.Append('$'); return position + 1; }
            value = matchedNumber; end = matchedEnd;
        }
        FlushLiteral(tokens, literal);
        tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, null, (int)value, false,
            storage.Budget.CopyText(replacement.AsSpan(position, end - position))));
        return end;
    }

    private static int AppendBracedGroup(Utf8ConstructionSession storage, string replacement, int position,
        Utf8ConstructionBuffer<Utf8ReplacementToken> tokens, Utf8ConstructionText literal)
    {
        var close = position + 2;
        while (close < replacement.Length)
        {
            storage.Budget.ChargeWork(1);
            if (replacement[close] == '}') break;
            close++;
        }
        if (close == replacement.Length) { literal.Append('$').Append('{'); return position + 2; }
        var content = replacement.AsSpan(position + 2, close - position - 2);
        if (TryParseGroupNumber(storage.Budget, content, out var number))
        {
            FlushLiteral(tokens, literal);
            tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, null, number, true,
                storage.Budget.CopyText(replacement.AsSpan(position, close - position + 1))));
            return close + 1;
        }
        if (content.Length > 0 && IsWordStart(content[0]))
        {
            FlushLiteral(tokens, literal);
            tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, storage.Budget.CopyText(content), -1, true));
            return close + 1;
        }
        literal.Append(replacement.AsSpan(position, close - position + 1));
        return close + 1;
    }

    private static bool TryParseGroupNumber(Utf8ConstructionBudget budget, ReadOnlySpan<char> content, out int number)
    {
        number = 0;
        if (content.IsEmpty) return false;
        var value = 0L;
        foreach (var c in content)
        {
            budget.ChargeWork(1);
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
        foreach (var token in Tokens)
        {
            storage.Budget.ChargeWork(1);
            var normalized = NormalizeGroupReference(storage, token, validGroupNumbers, validGroupNames);
            if (normalized.Kind == Utf8ReplacementTokenKind.Literal)
            {
                if (pending is { } previous)
                {
                    if (merged is null)
                    {
                        merged = storage.Text(); merged.Append(previous.Literal ?? string.Empty);
                    }
                    merged.Append(normalized.Literal ?? string.Empty);
                }
                else pending = normalized;
            }
            else
            {
                FlushPending(storage, resolved, ref pending, ref merged); resolved.Add(normalized);
            }
        }
        FlushPending(storage, resolved, ref pending, ref merged);
        return resolved;

    }

    private static void FlushPending(Utf8ConstructionSession storage, Utf8ConstructionBuffer<Utf8ReplacementToken> resolved,
        ref Utf8ReplacementToken? pending, ref Utf8ConstructionText? merged)
    {
        if (pending is not { } literal) return;
        storage.Budget.ChargeWork(1);
        resolved.Add(merged is null ? literal :
            new Utf8ReplacementToken(Utf8ReplacementTokenKind.Literal, merged.ToText(), -1, false));
        pending = null;
        if (merged is not null) { merged.Clear(); merged = null; }
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
        var name = storage.Text().Append("${").Append(token.Literal).Append('}').ToText();
        return new Utf8ReplacementToken(Utf8ReplacementTokenKind.Literal, name, -1, true);
    }

}
