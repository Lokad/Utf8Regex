using System.Buffers;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2AsciiLiteralAlternationRepeatDirectProgram : IPcre2DirectProgram
{
    internal Pcre2AsciiLiteralAlternationRepeatDirectProgram(
        Utf8Regex regex,
        byte[][] alternatives,
        byte[] suffix)
    {
        Regex = regex;
        Alternatives = alternatives;
        Suffix = suffix;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget; budget.EnterDepth();
        try
        {
            budget.ChargeWork((ulong)alternatives.Length);
            ulong a = 0, b = 0, c = 0, d = 0;
            foreach (var alternative in alternatives)
            {
                var value = alternative[0]; var bit = 1ul << (value & 63);
                switch (value >> 6)
                {
                    case 0: a |= bit; break;
                    case 1: b |= bit; break;
                    case 2: c |= bit; break;
                    default: d |= bit; break;
                }
            }
            FirstBytes = PreparedByteSet.CreateOwned(a, b, c, d);
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2AsciiLiteralAlternationRepeat;

    internal Utf8Regex Regex { get; }

    internal byte[][] Alternatives { get; }

    internal byte[] Suffix { get; }

    internal PreparedByteSet FirstBytes { get; }

    internal int Count(ReadOnlySpan<byte> input, int start)
    {
        var endingCount = checked(input.Length + 1);
        int[]? rentedEndings = null;
        Span<int> endings = endingCount <= 256
            ? stackalloc int[endingCount]
            : rentedEndings = ArrayPool<int>.Shared.Rent(endingCount);

        try
        {
            for (var position = input.Length; position >= start; position--)
            {
                var matchEnd = -1;
                foreach (var alternative in Alternatives)
                {
                    var next = position + alternative.Length;
                    if (next <= input.Length &&
                        input[position..next].SequenceEqual(alternative) &&
                        endings[next] >= 0)
                    {
                        matchEnd = endings[next];
                        break;
                    }
                }

                if (matchEnd < 0 &&
                    position <= input.Length - Suffix.Length &&
                    input[position..(position + Suffix.Length)].SequenceEqual(Suffix))
                {
                    matchEnd = position + Suffix.Length;
                }

                endings[position] = matchEnd;
            }

            var count = 0;
            var cursor = start;
            while (cursor < input.Length)
            {
                var relativeCandidate = FirstBytes.IndexOf(input[cursor..]);
                if (relativeCandidate < 0)
                {
                    break;
                }

                var candidate = cursor + relativeCandidate;
                var matchEnd = -1;
                foreach (var alternative in Alternatives)
                {
                    var next = candidate + alternative.Length;
                    if (next <= input.Length &&
                        input[candidate..next].SequenceEqual(alternative) &&
                        endings[next] >= 0)
                    {
                        matchEnd = endings[next];
                        break;
                    }
                }

                if (matchEnd >= 0)
                {
                    count++;
                    cursor = matchEnd;
                }
                else
                {
                    cursor = candidate + 1;
                }
            }

            return count;
        }
        finally
        {
            if (rentedEndings is not null)
            {
                ArrayPool<int>.Shared.Return(rentedEndings);
            }
        }
    }
}

internal static class Pcre2AsciiLiteralAlternationRepeatAnalyzer
{
    internal static Pcre2AsciiLiteralAlternationRepeatDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Utf8Regex regex)
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

        var alternatives = alternation.Alternatives.Select(ExtractAsciiLiteral).ToArray();
        var suffix = sequence.Children[1..].Select(ExtractAsciiLiteralByte).ToArray();
        return new Pcre2AsciiLiteralAlternationRepeatDirectProgram(
            regex,
            alternatives,
            suffix);
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

    internal static byte[] ExtractAsciiLiteral(IPcre2BacktrackingNode node)
    {
        if (node is Pcre2TokenBacktrackingNode token)
        {
            return [ExtractAsciiLiteralByte(token)];
        }

        return ((Pcre2SequenceBacktrackingNode)node).Children
            .Select(ExtractAsciiLiteralByte)
            .ToArray();
    }

    internal static byte ExtractAsciiLiteralByte(IPcre2BacktrackingNode node) =>
        (byte)((Pcre2TokenBacktrackingNode)node).Token.Literal.Value;
}
