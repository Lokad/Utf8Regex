using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2AsciiOrderedLiteralWindowDirectProgram : IPcre2DirectProgram
{
    internal Pcre2AsciiOrderedLiteralWindowDirectProgram(
        Utf8Regex regex,
        Pcre2BacktrackingProgram fallback)
    {
        Regex = regex;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2AsciiOrderedLiteralWindow;

    internal Utf8Regex Regex { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2AsciiOrderedLiteralWindowAnalyzer
{
    internal static Pcre2AsciiOrderedLiteralWindowDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Utf8Regex regex,
        Pcre2BacktrackingProgram fallback)
    {
        if (request.Options != Pcre2CompileOptions.None ||
            request.Settings.Newline is not (Pcre2NewlineConvention.Default or Pcre2NewlineConvention.Lf) ||
            regex.Inspection.ExecutionKind != NativeExecutionKind.AsciiOrderedLiteralWindow ||
            root is not Pcre2AlternationBacktrackingNode { Alternatives.Length: 2 } alternation ||
            !alternation.Alternatives.All(IsSupportedBranch))
        {
            return null;
        }

        return new Pcre2AsciiOrderedLiteralWindowDirectProgram(regex, fallback);
    }

    internal static bool IsSupportedBranch(IPcre2BacktrackingNode node)
    {
        if (node is not Pcre2SequenceBacktrackingNode { Children.Length: >= 3 } sequence)
        {
            return false;
        }

        var repeatIndex = -1;
        for (var i = 0; i < sequence.Children.Length; i++)
        {
            if (sequence.Children[i] is Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Literal,
                        Options: Pcre2CharacterOptions.None,
                        Literal: var literal,
                    },
                } &&
                literal.IsAscii &&
                literal.Value != '\n')
            {
                continue;
            }

            if (repeatIndex < 0 &&
                i > 0 &&
                i < sequence.Children.Length - 1 &&
                sequence.Children[i] is Pcre2RepeatBacktrackingNode
                {
                    Minimum: var minimum,
                    Maximum: var maximum,
                    Preference: Pcre2RepeatPreference.Greedy,
                    Body: Pcre2TokenBacktrackingNode
                    {
                        Token:
                        {
                            Kind: Pcre2CharacterTokenKind.Any,
                            Options: Pcre2CharacterOptions.None,
                        },
                    },
                } &&
                minimum >= 0 &&
                maximum >= minimum &&
                maximum != int.MaxValue)
            {
                repeatIndex = i;
                continue;
            }

            return false;
        }

        return repeatIndex > 0 && repeatIndex < sequence.Children.Length - 1;
    }
}
