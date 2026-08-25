namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2MultilinePrefixDirectProgram : IPcre2DirectProgram
{
    internal Pcre2MultilinePrefixDirectProgram(
        byte[] prefix,
        Pcre2BacktrackingProgram fallback)
    {
        Prefix = prefix;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2MultilinePrefix;

    internal byte[] Prefix { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2MultilinePrefixAnalyzer
{
    internal static Pcre2MultilinePrefixDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Pcre2BacktrackingProgram fallback)
    {
        if (request.Options != Pcre2CompileOptions.Multiline ||
            request.Settings.Newline is not (Pcre2NewlineConvention.Default or Pcre2NewlineConvention.Lf) ||
            root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 4 } sequence ||
            sequence.Children[0] is not Pcre2TokenBacktrackingNode
            {
                Token:
                {
                    Kind: Pcre2CharacterTokenKind.BeginningOfLine,
                    Options: Pcre2CharacterOptions.Multiline,
                },
            } ||
            sequence.Children[^2] is not Pcre2RepeatBacktrackingNode
            {
                Minimum: 1,
                Maximum: int.MaxValue,
                Preference: Pcre2RepeatPreference.Greedy,
                Body: Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Any,
                        Options: Pcre2CharacterOptions.Multiline,
                    },
                },
            } ||
            sequence.Children[^1] is not Pcre2TokenBacktrackingNode
            {
                Token:
                {
                    Kind: Pcre2CharacterTokenKind.EndOfLine,
                    Options: Pcre2CharacterOptions.Multiline,
                },
            })
        {
            return null;
        }

        var prefix = new byte[sequence.Children.Length - 3];
        for (var i = 1; i < sequence.Children.Length - 2; i++)
        {
            if (sequence.Children[i] is not Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Literal,
                        Options: Pcre2CharacterOptions.Multiline,
                        Literal: var literal,
                    },
                } ||
                !literal.IsAscii ||
                literal.Value == '\n')
            {
                return null;
            }

            prefix[i - 1] = (byte)literal.Value;
        }

        return new Pcre2MultilinePrefixDirectProgram(prefix, fallback);
    }
}

internal static class Pcre2MultilinePrefixRunner
{
    internal static bool TryFind(
        Pcre2MultilinePrefixDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        out int matchStartOffsetInBytes,
        out int matchEndOffsetInBytes)
    {
        var candidate = startOffsetInBytes;
        if (candidate != 0 && input[candidate - 1] != (byte)'\n')
        {
            var nextNewline = input[candidate..].IndexOf((byte)'\n');
            if (nextNewline < 0)
            {
                matchStartOffsetInBytes = 0;
                matchEndOffsetInBytes = 0;
                return false;
            }

            candidate += nextNewline + 1;
        }

        while (candidate <= input.Length)
        {
            var remaining = input[candidate..];
            if (remaining.StartsWith(program.Prefix))
            {
                var body = remaining[program.Prefix.Length..];
                var newline = body.IndexOf((byte)'\n');
                var bodyLength = newline < 0 ? body.Length : newline;
                if (bodyLength != 0)
                {
                    matchStartOffsetInBytes = candidate;
                    matchEndOffsetInBytes = candidate + program.Prefix.Length + bodyLength;
                    return true;
                }
            }

            var nextNewline = remaining.IndexOf((byte)'\n');
            if (nextNewline < 0)
            {
                break;
            }

            candidate += nextNewline + 1;
        }

        matchStartOffsetInBytes = 0;
        matchEndOffsetInBytes = 0;
        return false;
    }

    internal static int Count(
        Pcre2MultilinePrefixDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        var count = 0;
        var nextStart = startOffsetInBytes;
        while (TryFind(program, input, nextStart, out _, out var matchEnd))
        {
            count = checked(count + 1);
            if (matchEnd == input.Length)
            {
                break;
            }

            nextStart = matchEnd;
        }

        return count;
    }
}
