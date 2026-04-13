using System.Text;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal sealed class RegexReplacementPattern
{
    public RegexReplacementPattern(RegexReplacementToken[] tokens)
    {
        Tokens = tokens;
    }

    public RegexReplacementToken[] Tokens { get; }

    public bool ContainsSubstitutions
    {
        get
        {
            foreach (var token in Tokens)
            {
                if (token.Kind != RegexReplacementTokenKind.Literal)
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
                if (token.IsGroupReference)
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
                if (token.Kind == RegexReplacementTokenKind.Group && token.GroupName is not null)
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
                if (token.IsSpecialSubstitution)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public RegexReplacementPattern ResolveGroupReferences(
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
    {
        if (Tokens.Length == 0)
        {
            return this;
        }

        var resolved = new List<RegexReplacementToken>(Tokens.Length);
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
        return changed ? new RegexReplacementPattern([.. resolved]) : this;
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
            if (token.Kind != RegexReplacementTokenKind.Literal)
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

    public bool TryGetLiteralText(
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? literalText)
    {
        literalText = null;
        if (Tokens.Length == 0)
        {
            literalText = string.Empty;
            return true;
        }

        var builder = new StringBuilder();
        foreach (var token in Tokens)
        {
            if (token.Kind == RegexReplacementTokenKind.Literal)
            {
                builder.Append(token.Literal);
                continue;
            }

                if (token.Kind == RegexReplacementTokenKind.Group)
                {
                    if (token.GroupNumber >= 0)
                    {
                    if (Contains(validGroupNumbers, token.GroupNumber))
                    {
                        return false;
                    }

                    if (token.IsBraceEnclosed)
                    {
                        builder.Append("${");
                        builder.Append(token.GroupNumber);
                        builder.Append('}');
                    }
                    else
                    {
                        builder.Append('$');
                        builder.Append(token.GroupNumber);
                    }

                    continue;
                }

                var groupName = token.GroupName;
                if (groupName is not null && Contains(validGroupNames, groupName))
                {
                    return false;
                }

                builder.Append("${");
                builder.Append(groupName);
                builder.Append('}');
                continue;
            }

            return false;
        }

        literalText = builder.ToString();
        return true;
    }

    private static bool Contains(ReadOnlySpan<int> values, int value)
    {
        foreach (var item in values)
        {
            if (item == value)
            {
                return true;
            }
        }

        return false;
    }

    private static RegexReplacementToken NormalizeGroupReference(
        RegexReplacementToken token,
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
    {
        if (token.Kind != RegexReplacementTokenKind.Group)
        {
            return token;
        }

        if (token.GroupNumber >= 0)
        {
            return Contains(validGroupNumbers, token.GroupNumber)
                ? token
                : new RegexReplacementToken(
                    RegexReplacementTokenKind.Literal,
                    GetOriginalGroupLiteral(token),
                    isBraceEnclosed: token.IsBraceEnclosed);
        }

        var groupName = token.GroupName;
        if (groupName is null)
        {
            return token;
        }

        for (var i = 0; i < validGroupNames.Length && i < validGroupNumbers.Length; i++)
        {
            if (string.Equals(validGroupNames[i], groupName, StringComparison.Ordinal))
            {
                return new RegexReplacementToken(
                    RegexReplacementTokenKind.Group,
                    groupNumber: validGroupNumbers[i],
                    isBraceEnclosed: true);
            }
        }

        return new RegexReplacementToken(
            RegexReplacementTokenKind.Literal,
            GetOriginalGroupLiteral(token),
            isBraceEnclosed: token.IsBraceEnclosed);
    }

    private static string GetOriginalGroupLiteral(RegexReplacementToken token)
    {
        if (token.GroupNumber >= 0)
        {
            return token.IsBraceEnclosed
                ? "${" + token.GroupNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"
                : "$" + token.GroupNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return "${" + token.GroupName + "}";
    }

    private static void AppendResolvedToken(List<RegexReplacementToken> tokens, RegexReplacementToken token)
    {
        if (token.Kind != RegexReplacementTokenKind.Literal || tokens.Count == 0)
        {
            tokens.Add(token);
            return;
        }

        var previous = tokens[^1];
        if (previous.Kind != RegexReplacementTokenKind.Literal)
        {
            tokens.Add(token);
            return;
        }

        tokens[^1] = new RegexReplacementToken(
            RegexReplacementTokenKind.Literal,
            (previous.Literal ?? string.Empty) + (token.Literal ?? string.Empty));
    }

    private static bool Contains(ReadOnlySpan<string> values, string value)
    {
        foreach (var item in values)
        {
            if (string.Equals(item, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
