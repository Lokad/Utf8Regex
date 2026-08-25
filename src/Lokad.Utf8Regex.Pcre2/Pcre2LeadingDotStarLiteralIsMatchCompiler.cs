using System.Text;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2LeadingDotStarLiteralIsMatchDirectProgram : IPcre2DirectProgram
{
    internal Pcre2LeadingDotStarLiteralIsMatchDirectProgram(
        byte[] literal,
        Pcre2BacktrackingProgram fallback)
    {
        Literal = literal;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2LeadingDotStarLiteralIsMatch;

    internal byte[] Literal { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2LeadingDotStarLiteralIsMatchAnalyzer
{
    internal static Pcre2LeadingDotStarLiteralIsMatchDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Pcre2BacktrackingProgram fallback)
    {
        if (request.Options != Pcre2CompileOptions.None ||
            request.Settings.Newline != Pcre2NewlineConvention.Default ||
            root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 2 } sequence ||
            sequence.Children[0] is not Pcre2RepeatBacktrackingNode
            {
                Body: Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Any,
                        Options: Pcre2CharacterOptions.None,
                    },
                },
                Minimum: 0,
                Maximum: int.MaxValue,
                Preference: not Pcre2RepeatPreference.Possessive,
            })
        {
            return null;
        }

        var literal = new StringBuilder();
        for (var i = 1; i < sequence.Children.Length; i++)
        {
            if (!TryAppendExactLiteral(sequence.Children[i], literal))
            {
                return null;
            }
        }

        return literal.Length == 0
            ? null
            : new Pcre2LeadingDotStarLiteralIsMatchDirectProgram(
                Encoding.UTF8.GetBytes(literal.ToString()),
                fallback);

        static bool TryAppendExactLiteral(IPcre2BacktrackingNode node, StringBuilder builder)
        {
            switch (node)
            {
                case Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Literal,
                        Options: Pcre2CharacterOptions.None,
                    } token,
                }:
                    builder.Append(token.Literal.ToString());
                    return true;
                case Pcre2SequenceBacktrackingNode sequence:
                    return sequence.Children.All(child => TryAppendExactLiteral(child, builder));
                case Pcre2CaptureBacktrackingNode capture:
                    return TryAppendExactLiteral(capture.Body, builder);
                default:
                    return false;
            }
        }
    }
}

internal static class Pcre2LeadingDotStarLiteralIsMatchRunner
{
    internal static bool IsMatch(
        Pcre2LeadingDotStarLiteralIsMatchDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        // Unanchored search may choose the exact suffix as its candidate, and
        // the leading repeat may consume zero scalars. Existence is therefore
        // equivalent to finding the non-empty suffix at or after the start.
        return input[startOffsetInBytes..].IndexOf(program.Literal) >= 0;
    }
}
