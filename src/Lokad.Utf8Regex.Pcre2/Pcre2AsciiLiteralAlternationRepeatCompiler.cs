namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2AsciiLiteralAlternationRepeatDirectProgram : IPcre2DirectProgram
{
    internal Pcre2AsciiLiteralAlternationRepeatDirectProgram(
        Utf8Regex regex,
        Pcre2BacktrackingProgram fallback)
    {
        Regex = regex;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2AsciiLiteralAlternationRepeat;

    internal Utf8Regex Regex { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2AsciiLiteralAlternationRepeatAnalyzer
{
    internal static Pcre2AsciiLiteralAlternationRepeatDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Utf8Regex regex,
        Pcre2BacktrackingProgram fallback)
    {
        if (request.Options != Pcre2CompileOptions.None ||
            root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 2 } sequence ||
            sequence.Children[0] is not Pcre2RepeatBacktrackingNode
            {
                Minimum: 1,
                Maximum: int.MaxValue,
                Preference: Pcre2RepeatPreference.Greedy,
                Body: Pcre2AlternationBacktrackingNode { Alternatives.Length: >= 2 } alternation,
            } ||
            !alternation.Alternatives.All(IsNonEmptyAsciiLiteral) ||
            !sequence.Children[1..].All(IsAsciiLiteralToken))
        {
            return null;
        }

        return new Pcre2AsciiLiteralAlternationRepeatDirectProgram(regex, fallback);
    }

    internal static bool IsNonEmptyAsciiLiteral(IPcre2BacktrackingNode node)
    {
        if (node is Pcre2TokenBacktrackingNode token)
        {
            return IsAsciiLiteralToken(token);
        }

        return node is Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence &&
            sequence.Children.All(IsAsciiLiteralToken);
    }

    internal static bool IsAsciiLiteralToken(IPcre2BacktrackingNode node) =>
        node is Pcre2TokenBacktrackingNode
        {
            Token:
            {
                Kind: Pcre2CharacterTokenKind.Literal,
                Options: Pcre2CharacterOptions.None,
                Literal.IsAscii: true,
            },
        };
}
