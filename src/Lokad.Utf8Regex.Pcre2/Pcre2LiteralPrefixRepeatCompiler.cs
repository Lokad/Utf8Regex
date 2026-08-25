namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2LiteralPrefixRepeatDirectProgram : IPcre2DirectProgram
{
    internal Pcre2LiteralPrefixRepeatDirectProgram(
        byte[] prefix,
        byte repeatedByte,
        int minimum,
        int maximum,
        Pcre2RepeatPreference preference,
        Pcre2BacktrackingProgram fallback)
    {
        Prefix = prefix;
        RepeatedByte = repeatedByte;
        Minimum = minimum;
        Maximum = maximum;
        Preference = preference;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2LiteralPrefixRepeat;

    internal byte[] Prefix { get; }

    internal byte RepeatedByte { get; }

    internal int Minimum { get; }

    internal int Maximum { get; }

    internal Pcre2RepeatPreference Preference { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2LiteralPrefixRepeatAnalyzer
{
    internal static Pcre2LiteralPrefixRepeatDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Pcre2BacktrackingProgram fallback)
    {
        if (request.Options != Pcre2CompileOptions.None ||
            root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 2 } sequence ||
            sequence.Children[^1] is not Pcre2RepeatBacktrackingNode
            {
                Body: var repeatBody,
                Minimum: var minimum,
                Maximum: var maximum,
                Preference: var preference,
            } ||
            !TryExtractAsciiLiteral(repeatBody, out var repeatedLiteral) ||
            repeatedLiteral.Length != 1)
        {
            return null;
        }

        var prefix = new List<byte>();
        foreach (var child in sequence.Children[..^1])
        {
            if (!TryExtractAsciiLiteral(child, out var literal))
            {
                return null;
            }

            prefix.AddRange(literal);
        }

        return prefix.Count == 0
            ? null
            : new Pcre2LiteralPrefixRepeatDirectProgram(
                prefix.ToArray(),
                repeatedLiteral[0],
                minimum,
                maximum,
                preference,
                fallback);
    }

    internal static bool TryExtractAsciiLiteral(IPcre2BacktrackingNode node, out byte[] literal)
    {
        switch (node)
        {
            case Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Literal,
                        Options: Pcre2CharacterOptions.None,
                        Literal.IsAscii: true,
                    } token,
                }:
                literal = [(byte)token.Literal.Value];
                return true;

            case Pcre2CaptureBacktrackingNode capture:
                return TryExtractAsciiLiteral(capture.Body, out literal);

            case Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence:
                var bytes = new List<byte>(sequence.Children.Length);
                foreach (var child in sequence.Children)
                {
                    if (!TryExtractAsciiLiteral(child, out var childLiteral))
                    {
                        literal = [];
                        return false;
                    }

                    bytes.AddRange(childLiteral);
                }

                literal = bytes.ToArray();
                return true;

            default:
                literal = [];
                return false;
        }
    }
}

internal static class Pcre2LiteralPrefixRepeatRunner
{
    internal static int Count(
        Pcre2LiteralPrefixRepeatDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        var count = 0;
        var nextStart = startOffsetInBytes;
        while (true)
        {
            var match = Match(program, input, nextStart);
            if (!match.Success)
            {
                return count;
            }

            count = checked(count + 1);
            nextStart = match.EndOffsetInBytes;
        }
    }

    internal static bool IsMatch(
        Pcre2LiteralPrefixRepeatDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        var searchFrom = startOffsetInBytes;
        if (input[startOffsetInBytes..].StartsWith(program.Prefix))
        {
            var repeatStart = startOffsetInBytes + program.Prefix.Length;
            if (HasRequiredRepeat(program, input, repeatStart))
            {
                return true;
            }

            searchFrom++;
        }

        while (searchFrom <= input.Length - program.Prefix.Length)
        {
            var relative = program.Prefix.Length == 1
                ? input[searchFrom..].IndexOf(program.Prefix[0])
                : input[searchFrom..].IndexOf(program.Prefix);
            if (relative < 0)
            {
                return false;
            }

            var repeatStart = searchFrom + relative + program.Prefix.Length;
            if (HasRequiredRepeat(program, input, repeatStart))
            {
                return true;
            }

            searchFrom += relative + 1;
        }

        return false;
    }

    internal static Pcre2DirectGlobalMatch Match(
        Pcre2LiteralPrefixRepeatDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        var searchFrom = startOffsetInBytes;
        while (searchFrom <= input.Length - program.Prefix.Length)
        {
            var relative = program.Prefix.Length == 1
                ? input[searchFrom..].IndexOf(program.Prefix[0])
                : input[searchFrom..].IndexOf(program.Prefix);
            if (relative < 0)
            {
                return default;
            }

            var matchStart = searchFrom + relative;
            var repeatStart = matchStart + program.Prefix.Length;
            if (HasRequiredRepeat(program, input, repeatStart))
            {
                var repeatCount = program.Minimum;
                if (program.Preference != Pcre2RepeatPreference.Lazy)
                {
                    var available = Math.Min(program.Maximum, input.Length - repeatStart);
                    while (repeatCount < available && input[repeatStart + repeatCount] == program.RepeatedByte)
                    {
                        repeatCount++;
                    }
                }

                var matchEnd = repeatStart + repeatCount;
                return new Pcre2DirectGlobalMatch(
                    Success: true,
                    StartOffsetInBytes: matchStart,
                    EndOffsetInBytes: matchEnd,
                    ConsumedStartOffsetInBytes: matchStart,
                    ConsumedEndOffsetInBytes: matchEnd,
                    MatchBoundaryWasReset: false);
            }

            searchFrom = matchStart + 1;
        }

        return default;
    }

    internal static bool HasRequiredRepeat(
        Pcre2LiteralPrefixRepeatDirectProgram program,
        ReadOnlySpan<byte> input,
        int repeatStart)
    {
        if (program.Minimum > input.Length - repeatStart)
        {
            return false;
        }

        for (var index = 0; index < program.Minimum; index++)
        {
            if (input[repeatStart + index] != program.RepeatedByte)
            {
                return false;
            }
        }

        return true;
    }
}
