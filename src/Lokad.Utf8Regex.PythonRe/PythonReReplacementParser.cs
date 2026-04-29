using System.Text;

namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReReplacementParser
{
    public static PythonReReplacementPlan Parse(
        string replacement,
        int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(namedGroups);
        var tokens = new List<PythonReReplacementToken>();
        var literal = new StringBuilder();
        var index = 0;
        while (index < replacement.Length)
        {
            var ch = replacement[index++];
            if (ch != '\\')
            {
                literal.Append(ch);
                continue;
            }

            if (index >= replacement.Length)
            {
                throw new PythonRePatternException("bad escape (end of pattern)", index - 1);
            }

            var escaped = replacement[index++];
            switch (escaped)
            {
                case '\\':
                    literal.Append('\\');
                    break;
                case 'n':
                    literal.Append('\n');
                    break;
                case 'r':
                    literal.Append('\r');
                    break;
                case 't':
                    literal.Append('\t');
                    break;
                case 'v':
                    literal.Append('\v');
                    break;
                case 'f':
                    literal.Append('\f');
                    break;
                case 'a':
                    literal.Append('\a');
                    break;
                case 'b':
                    literal.Append('\b');
                    break;
                case 'g':
                    FlushLiteral(tokens, literal);
                    tokens.Add(ParseNamedOrNumberedGroup(replacement, ref index, captureGroupCount, namedGroups));
                    break;
                case '0':
                    literal.Append(ParseOctalEscape(replacement, ref index, escaped));
                    break;
                case >= '1' and <= '9':
                    FlushLiteral(tokens, literal);
                    tokens.Add(ParseNumberedGroupOrOctalEscape(replacement, ref index, escaped, captureGroupCount));
                    break;
                default:
                    if (char.IsAsciiLetter(escaped))
                    {
                        throw new PythonRePatternException($@"bad escape \{escaped}");
                    }

                    literal.Append('\\');
                    literal.Append(escaped);
                    break;
            }
        }

        FlushLiteral(tokens, literal);
        return new PythonReReplacementPlan(tokens.ToArray());
    }

    private static PythonReReplacementToken ParseNamedOrNumberedGroup(
        string replacement,
        ref int index,
        int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
    {
        if (index >= replacement.Length || replacement[index] != '<')
        {
            throw new PythonRePatternException("missing <");
        }

        index++;
        var start = index;
        while (index < replacement.Length && replacement[index] != '>')
        {
            index++;
        }

        if (index >= replacement.Length)
        {
            if (index == start)
            {
                throw new PythonRePatternException("missing group name", start);
            }

            throw new PythonRePatternException("missing >, unterminated name", start);
        }

        var name = replacement[start..index];
        index++;
        if (name.Length == 0)
        {
            throw new PythonRePatternException("missing group name", start);
        }

        if (name.All(char.IsAsciiDigit))
        {
            var number = int.Parse(name, System.Globalization.CultureInfo.InvariantCulture);
            ValidateGroupReference(number, captureGroupCount);
            return PythonReReplacementToken.ForGroupNumber(number);
        }

        ValidateGroupName(name, start);
        if (!namedGroups.TryGetValue(name, out var groupNumber))
        {
            throw new IndexOutOfRangeException($"unknown group name '{name}'");
        }

        return PythonReReplacementToken.ForGroupNumber(groupNumber);
    }

    private static string ParseOctalEscape(string replacement, ref int index, char firstDigit)
    {
        var octal = new StringBuilder();
        octal.Append(firstDigit);
        for (var digits = 0; digits < 2 && index < replacement.Length && replacement[index] is >= '0' and <= '7'; digits++)
        {
            octal.Append(replacement[index++]);
        }

        var value = Convert.ToInt32(octal.ToString(), 8);
        return char.ToString((char)value);
    }

    private static PythonReReplacementToken ParseNumberedGroupOrOctalEscape(
        string replacement,
        ref int index,
        char firstDigit,
        int captureGroupCount)
    {
        var digits = new StringBuilder();
        digits.Append(firstDigit);
        var isoctal = false;

        if (index < replacement.Length && char.IsAsciiDigit(replacement[index]))
        {
            digits.Append(replacement[index++]);
            if (firstDigit is >= '0' and <= '7' &&
                digits[1] is >= '0' and <= '7' &&
                index < replacement.Length &&
                replacement[index] is >= '0' and <= '7')
            {
                digits.Append(replacement[index++]);
                isoctal = true;
                var value = Convert.ToInt32(digits.ToString(), 8);
                if (value > 0xFF)
                {
                    throw new PythonRePatternException($@"octal escape value \{digits} outside of range 0-0o377");
                }

                return PythonReReplacementToken.ForLiteral(char.ToString((char)value));
            }
        }

        if (!isoctal)
        {
            var number = int.Parse(digits.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            ValidateGroupReference(number, captureGroupCount);
            return PythonReReplacementToken.ForGroupNumber(number);
        }

        return PythonReReplacementToken.ForLiteral(string.Empty);
    }

    private static void ValidateGroupReference(int number, int captureGroupCount)
    {
        if (number > captureGroupCount)
        {
            throw new PythonRePatternException($"invalid group reference {number}");
        }
    }

    private static void ValidateGroupName(string name, int position)
    {
        if (!PythonReGroupNameValidator.IsValid(name))
        {
            throw new PythonRePatternException($"bad character in group name '{name}'", position);
        }
    }

    private static void FlushLiteral(List<PythonReReplacementToken> tokens, StringBuilder literal)
    {
        if (literal.Length == 0)
        {
            return;
        }

        tokens.Add(PythonReReplacementToken.ForLiteral(literal.ToString()));
        literal.Clear();
    }
}

internal readonly record struct PythonReReplacementPlan(IReadOnlyList<PythonReReplacementToken> Tokens)
{
    public string ToDotNetReplacementString()
    {
        if (Tokens.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var token in Tokens)
        {
            switch (token.Kind)
            {
                case PythonReReplacementTokenKind.Literal:
                    builder.Append(token.Text!.Replace("$", "$$", StringComparison.Ordinal));
                    break;
                case PythonReReplacementTokenKind.GroupNumber:
                    builder.Append("${").Append(token.Number).Append('}');
                    break;
                case PythonReReplacementTokenKind.GroupName:
                    builder.Append("${").Append(token.Text).Append('}');
                    break;
            }
        }

        return builder.ToString();
    }

    public string Expand(in Utf8PythonMatchContext match)
    {
        if (Tokens.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var token in Tokens)
        {
            switch (token.Kind)
            {
                case PythonReReplacementTokenKind.Literal:
                    builder.Append(token.Text);
                    break;
                case PythonReReplacementTokenKind.GroupNumber:
                    if (match.TryGetGroup(token.Number, out var group) && group.Success)
                    {
                        builder.Append(group.Value.GetValueString());
                    }

                    break;
                case PythonReReplacementTokenKind.GroupName:
                    if (match.TryGetFirstSetGroup(token.Text!, out var named) && named.Success)
                    {
                        builder.Append(named.Value.GetValueString());
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    public byte[] ExpandToUtf8(ReadOnlySpan<byte> input, PythonReGroupData[] groups)
    {
        if (Tokens.Count == 0)
        {
            return [];
        }

        List<byte> bytes = [];
        foreach (var token in Tokens)
        {
            switch (token.Kind)
            {
                case PythonReReplacementTokenKind.Literal:
                    bytes.AddRange(Encoding.UTF8.GetBytes(token.Text!));
                    break;
                case PythonReReplacementTokenKind.GroupNumber:
                    if ((uint)token.Number < (uint)groups.Length && groups[token.Number].Success)
                    {
                        bytes.AddRange(PythonReValueTextExtractor.GetValueBytes(input, groups[token.Number]));
                    }

                    break;
                case PythonReReplacementTokenKind.GroupName:
                    throw new InvalidOperationException("Unexpected group-name replacement token after PythonRe parsing.");
            }
        }

        return bytes.ToArray();
    }
}

internal readonly record struct PythonReReplacementToken(
    PythonReReplacementTokenKind Kind,
    string? Text,
    int Number)
{
    public static PythonReReplacementToken ForLiteral(string text) => new(PythonReReplacementTokenKind.Literal, text, 0);

    public static PythonReReplacementToken ForGroupNumber(int number) => new(PythonReReplacementTokenKind.GroupNumber, null, number);

    public static PythonReReplacementToken ForGroupName(string name) => new(PythonReReplacementTokenKind.GroupName, name, 0);
}

internal enum PythonReReplacementTokenKind
{
    Literal,
    GroupNumber,
    GroupName,
}
