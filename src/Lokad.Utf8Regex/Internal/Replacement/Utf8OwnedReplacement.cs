using System.Globalization;

namespace Lokad.Utf8Regex.Internal.Replacement;

// Owned replacement template parsing, pinned against legacy instances over the
// probe catalog. Dollar introduces whole match, portions, last group, whole
// string, numbered and named group references while anything else stays
// literal, with maximal literal runs, greedy digits, braced names validated by
// a word character head, int overflow falling back to literal, and unclosed
// braces degrading to literal runs. Group resolution validates numbered groups
// against the valid numbers, maps names through the parallel arrays, demotes
// unknown references to their normalized literal text with adjacent literals
// merged brace free, and extracts literal text when nothing substitutes.
// Replacement never throws: every shape degrades to literal text. Token kind
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
    {
        Kind = kind;
        Literal = literal;
        GroupNumber = groupNumber;
        IsBraceEnclosed = isBraceEnclosed;
    }

    public Utf8ReplacementTokenKind Kind { get; }

    public string? Literal { get; }

    public int GroupNumber { get; }

    public bool IsBraceEnclosed { get; }

    public string? GroupName => Kind == Utf8ReplacementTokenKind.Group && GroupNumber < 0 ? Literal : null;
}


internal sealed class Utf8ReplacementPattern
{
    public Utf8ReplacementPattern(Utf8ReplacementToken[] tokens)
    {
        Tokens = tokens;
    }

    public Utf8ReplacementToken[] Tokens { get; }

    public bool ContainsSubstitutions
    {
        get
        {
            foreach (var token in Tokens)
            {
                if (token.Kind != Utf8ReplacementTokenKind.Literal)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool ContainsGroupReferences
    {
        get
        {
            foreach (var token in Tokens)
            {
                if (token.Kind == Utf8ReplacementTokenKind.Group)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool ContainsNamedGroups
    {
        get
        {
            foreach (var token in Tokens)
            {
                if (token.Kind == Utf8ReplacementTokenKind.Group && token.GroupNumber < 0 && token.Literal is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool ContainsSpecialSubstitutions
    {
        get
        {
            foreach (var token in Tokens)
            {
                if (token.Kind is Utf8ReplacementTokenKind.WholeMatch or Utf8ReplacementTokenKind.LeftPortion or Utf8ReplacementTokenKind.RightPortion or Utf8ReplacementTokenKind.LastGroup or Utf8ReplacementTokenKind.WholeString)
                {
                    return true;
                }
            }

            return false;
        }
    }


    public static Utf8ReplacementPattern Parse(string replacement)
    {
        var tokens = new List<Utf8ReplacementToken>();
        var literal = new StringBuilder();
        var position = 0;
        while (position < replacement.Length)
        {
            var current = replacement[position];
            if (current != '$')
            {
                literal.Append(current);
                position++;
                continue;
            }

            if (position + 1 >= replacement.Length)
            {
                literal.Append('$');
                position++;
                continue;
            }

            var next = replacement[position + 1];
            if (next == '$')
            {
                literal.Append('$');
                position += 2;
                continue;
            }

            if (TryScanSpecial(next, out var special))
            {
                FlushLiteral(tokens, literal);
                tokens.Add(special);
                position += 2;
                continue;
            }

            if (IsAsciiDigit(next))
            {
                position = AppendNumberedGroup(replacement, position, tokens, literal);
                continue;
            }

            if (next == '{')
            {
                position = AppendBracedGroup(replacement, position, tokens, literal);
                continue;
            }

            literal.Append('$');
            position++;
        }

        FlushLiteral(tokens, literal);
        return new Utf8ReplacementPattern([.. tokens]);

        static void FlushLiteral(List<Utf8ReplacementToken> tokens, StringBuilder literal)
        {
            if (literal.Length > 0)
            {
                tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Literal, literal.ToString(), -1, false));
                literal.Clear();
            }
        }

        static bool TryScanSpecial(char next, out Utf8ReplacementToken special)
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

            if (kind is null)
            {
                special = default;
                return false;
            }

            special = new Utf8ReplacementToken(kind.Value, null, -1, false);
            return true;
        }

        static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        static int AppendNumberedGroup(string replacement, int position, List<Utf8ReplacementToken> tokens, StringBuilder literal)
        {
            var value = 0L;
            var end = position + 1;
            while (end < replacement.Length && IsAsciiDigit(replacement[end]))
            {
                value = value * 10 + (replacement[end] - '0');
                if (value > int.MaxValue)
                {
                    literal.Append('$');
                    return position + 1;
                }

                end++;
            }

            FlushLiteral(tokens, literal);
            tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, null, (int)value, false));
            return end;
        }

        static int AppendBracedGroup(string replacement, int position, List<Utf8ReplacementToken> tokens, StringBuilder literal)
        {
            var close = replacement.IndexOf('}', position + 2);
            if (close < 0)
            {
                literal.Append('$');
                literal.Append('{');
                return position + 2;
            }

            var content = replacement.Substring(position + 2, close - position - 2);
            if (TryParseGroupNumber(content, out var number))
            {
                FlushLiteral(tokens, literal);
                tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, null, number, true));
                return close + 1;
            }

            if (content.Length > 0 && IsWordStart(content[0]))
            {
                FlushLiteral(tokens, literal);
                tokens.Add(new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, content, -1, true));
                return close + 1;
            }

            literal.Append(replacement.Substring(position, close - position + 1));
            return close + 1;
        }

        static bool TryParseGroupNumber(string content, out int number)
        {
            number = 0;
            if (content.Length == 0)
            {
                return false;
            }

            var value = 0L;
            foreach (var c in content)
            {
                if (!IsAsciiDigit(c))
                {
                    return false;
                }

                value = value * 10 + (c - '0');
                if (value > int.MaxValue)
                {
                    return false;
                }
            }

            number = (int)value;
            return true;
        }

        static bool IsWordStart(char value)
        {
            return char.GetUnicodeCategory(value) switch
            {
                UnicodeCategory.UppercaseLetter => true,
                UnicodeCategory.LowercaseLetter => true,
                UnicodeCategory.TitlecaseLetter => true,
                UnicodeCategory.ModifierLetter => true,
                UnicodeCategory.OtherLetter => true,
                UnicodeCategory.NonSpacingMark => true,
                UnicodeCategory.DecimalDigitNumber => true,
                UnicodeCategory.ConnectorPunctuation => true,
                _ => false,
            };
        }
    }


    public Utf8ReplacementPattern ResolveGroupReferences(ReadOnlySpan<int> validGroupNumbers, ReadOnlySpan<string> validGroupNames)
    {
        if (Tokens.Length == 0)
        {
            return this;
        }

        var resolved = new List<Utf8ReplacementToken>(Tokens.Length);
        var changed = false;
        foreach (var token in Tokens)
        {
            var normalized = NormalizeGroupReference(token, validGroupNumbers, validGroupNames);
            changed |= normalized.Kind != token.Kind ||
                normalized.GroupNumber != token.GroupNumber ||
                !string.Equals(normalized.Literal, token.Literal, StringComparison.Ordinal) ||
                normalized.IsBraceEnclosed != token.IsBraceEnclosed;
            AppendResolvedToken(resolved, normalized);
        }

        changed |= resolved.Count != Tokens.Length;
        return changed ? new Utf8ReplacementPattern([.. resolved]) : this;

        static Utf8ReplacementToken NormalizeGroupReference(Utf8ReplacementToken token, ReadOnlySpan<int> validGroupNumbers, ReadOnlySpan<string> validGroupNames)
        {
            if (token.Kind != Utf8ReplacementTokenKind.Group)
            {
                return token;
            }

            if (token.GroupNumber >= 0)
            {
                foreach (var number in validGroupNumbers)
                {
                    if (number == token.GroupNumber)
                    {
                        return token;
                    }
                }

                var text = token.IsBraceEnclosed
                    ? "${" + token.GroupNumber.ToString() + "}"
                    : "$" + token.GroupNumber.ToString();
                return new Utf8ReplacementToken(Utf8ReplacementTokenKind.Literal, text, -1, token.IsBraceEnclosed);
            }

            if (token.Literal is null)
            {
                return token;
            }

            for (var i = 0; i < validGroupNames.Length && i < validGroupNumbers.Length; i++)
            {
                if (string.Equals(validGroupNames[i], token.Literal, StringComparison.Ordinal))
                {
                    return new Utf8ReplacementToken(Utf8ReplacementTokenKind.Group, null, validGroupNumbers[i], token.IsBraceEnclosed);
                }
            }

            return new Utf8ReplacementToken(Utf8ReplacementTokenKind.Literal, "${" + token.Literal + "}", -1, true);
        }

        static void AppendResolvedToken(List<Utf8ReplacementToken> resolved, Utf8ReplacementToken normalized)
        {
            if (normalized.Kind == Utf8ReplacementTokenKind.Literal &&
                resolved.Count > 0 &&
                resolved[resolved.Count - 1].Kind == Utf8ReplacementTokenKind.Literal)
            {
                var previous = resolved[resolved.Count - 1];
                resolved[resolved.Count - 1] = new Utf8ReplacementToken(
                    Utf8ReplacementTokenKind.Literal,
                    (previous.Literal ?? string.Empty) + (normalized.Literal ?? string.Empty),
                    -1,
                    false);
                return;
            }

            resolved.Add(normalized);
        }
    }


    public bool TryGetLiteralText([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? literalText)
    {
        literalText = null;
        if (Tokens.Length == 0)
        {
            literalText = string.Empty;
            return true;
        }

        foreach (var token in Tokens)
        {
            if (token.Kind != Utf8ReplacementTokenKind.Literal)
            {
                return false;
            }
        }

        var builder = new StringBuilder();
        foreach (var token in Tokens)
        {
            builder.Append(token.Literal);
        }

        literalText = builder.ToString();
        return true;
    }

    public bool TryGetLiteralText(ReadOnlySpan<int> validGroupNumbers, ReadOnlySpan<string> validGroupNames, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? literalText)
    {
        return ResolveGroupReferences(validGroupNumbers, validGroupNames).TryGetLiteralText(out literalText);
    }
}

