namespace Lokad.Utf8Regex.Pcre2;

internal sealed class Pcre2SeparatedRunsIsMatchDirectProgram : IPcre2DirectProgram
{
    internal Pcre2SeparatedRunsIsMatchDirectProgram(
        Pcre2CharacterToken leadingToken,
        byte[] separator,
        Pcre2CharacterToken firstTrailingToken,
        Pcre2CharacterToken secondTrailingToken,
        Pcre2BacktrackingProgram fallback)
    {
        LeadingToken = leadingToken;
        Separator = separator;
        FirstTrailingToken = firstTrailingToken;
        SecondTrailingToken = secondTrailingToken;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2SeparatedRunsIsMatch;

    internal Pcre2CharacterToken LeadingToken { get; }

    internal byte[] Separator { get; }

    internal Pcre2CharacterToken FirstTrailingToken { get; }

    internal Pcre2CharacterToken SecondTrailingToken { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal static class Pcre2SeparatedRunsIsMatchAnalyzer
{
    internal static Pcre2SeparatedRunsIsMatchDirectProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Pcre2BacktrackingProgram fallback)
    {
        if (request.Options != Pcre2CompileOptions.None ||
            request.Settings.Newline != Pcre2NewlineConvention.Default ||
            request.Settings.Bsr != Pcre2BsrConvention.Default ||
            request.Settings.AllowDuplicateNames ||
            request.Settings.BackslashC != Pcre2BackslashCPolicy.Forbid ||
            request.Settings.AllowLookaroundBackslashK ||
            root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 4 } sequence ||
            sequence.Children[0] is not Pcre2RepeatBacktrackingNode
            {
                Body: Pcre2TokenBacktrackingNode { Token: var leadingToken },
                Minimum: 1,
                Maximum: int.MaxValue,
                Preference: not Pcre2RepeatPreference.Possessive,
            } ||
            !IsPlainCharacterToken(leadingToken))
        {
            return null;
        }

        var separator = new List<byte>();
        var childIndex = 1;
        while (childIndex < sequence.Children.Length &&
               sequence.Children[childIndex] is Pcre2TokenBacktrackingNode
               {
                   Token:
                   {
                       Kind: Pcre2CharacterTokenKind.Literal,
                       Literal: var literal,
                       Options: Pcre2CharacterOptions.None,
                   },
               } &&
               literal.IsAscii)
        {
            separator.Add((byte)literal.Value);
            childIndex++;
        }

        if (separator.Count == 0 ||
            childIndex + 1 >= sequence.Children.Length ||
            sequence.Children[childIndex] is not Pcre2RepeatBacktrackingNode
            {
                Body: Pcre2TokenBacktrackingNode { Token: var firstTrailingToken },
                Minimum: 1,
                Maximum: int.MaxValue,
                Preference: not Pcre2RepeatPreference.Possessive,
            } ||
            sequence.Children[childIndex + 1] is not Pcre2RepeatBacktrackingNode
            {
                Body: Pcre2TokenBacktrackingNode { Token: var secondTrailingToken },
                Minimum: 1,
                Maximum: int.MaxValue,
                Preference: not Pcre2RepeatPreference.Possessive,
            } ||
            !IsTokenSubset(firstTrailingToken, secondTrailingToken))
        {
            return null;
        }

        for (var i = childIndex + 2; i < sequence.Children.Length; i++)
        {
            if (sequence.Children[i] is not Pcre2RepeatBacktrackingNode
                {
                    Body: var optionalBody,
                    Minimum: 0,
                    Maximum: 1,
                    Preference: not Pcre2RepeatPreference.Possessive,
                } ||
                !IsPlainConsumableNode(optionalBody))
            {
                return null;
            }
        }

        return new Pcre2SeparatedRunsIsMatchDirectProgram(
            leadingToken,
            [.. separator],
            firstTrailingToken,
            secondTrailingToken,
            fallback);
    }

    internal static bool IsTokenSubset(Pcre2CharacterToken left, Pcre2CharacterToken right)
    {
        if (!IsPlainCharacterToken(left) || !IsPlainCharacterToken(right))
        {
            return false;
        }

        if (left.Kind == Pcre2CharacterTokenKind.Literal)
        {
            return right.Kind == Pcre2CharacterTokenKind.Literal
                ? left.Literal == right.Literal
                : right.CharacterClass.Matches(left.Literal, ucp: false, caseless: false);
        }

        if (left.Kind != Pcre2CharacterTokenKind.CharacterClass ||
            right.Kind != Pcre2CharacterTokenKind.CharacterClass ||
            !left.CharacterClass.Negated ||
            !right.CharacterClass.Negated ||
            left.CharacterClass.Terms.Any(static term => term.Negated) ||
            right.CharacterClass.Terms.Any(static term => term.Negated))
        {
            return false;
        }

        // For positive unions P and Q, complement(P) is a subset of
        // complement(Q) exactly when P covers Q. Requiring every right-hand
        // exclusion term to be structurally covered by a left-hand term is a
        // conservative proof of that relation over the complete scalar range.
        return right.CharacterClass.Terms.All(required =>
            left.CharacterClass.Terms.Any(candidate => Covers(candidate, required)));

        static bool Covers(Pcre2CharacterClassTerm candidate, Pcre2CharacterClassTerm required) =>
            candidate.Kind == required.Kind &&
            (candidate.Kind == Pcre2CharacterClassTermKind.Range
                ? candidate.Range.Low <= required.Range.Low &&
                    candidate.Range.High >= required.Range.High
                : candidate == required);
    }

    internal static bool IsPlainCharacterToken(Pcre2CharacterToken token) =>
        token.Options == Pcre2CharacterOptions.None &&
        token.Kind is Pcre2CharacterTokenKind.Literal or Pcre2CharacterTokenKind.CharacterClass;

    internal static bool IsPlainConsumableNode(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2TokenBacktrackingNode { Token: var token } =>
            token.Options == Pcre2CharacterOptions.None &&
            token.Kind is Pcre2CharacterTokenKind.Literal or
                Pcre2CharacterTokenKind.CharacterClass or
                Pcre2CharacterTokenKind.Any or
                Pcre2CharacterTokenKind.AnyNotNewline or
                Pcre2CharacterTokenKind.NewlineSequence or
                Pcre2CharacterTokenKind.ExtendedGraphemeCluster,
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.All(IsPlainConsumableNode),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.All(IsPlainConsumableNode),
        Pcre2CaptureBacktrackingNode capture => IsPlainConsumableNode(capture.Body),
        Pcre2RepeatBacktrackingNode { Preference: not Pcre2RepeatPreference.Possessive } repeat =>
            IsPlainConsumableNode(repeat.Body),
        _ => false,
    };
}

internal static class Pcre2SeparatedRunsIsMatchRunner
{
    internal static bool IsMatch(
        Pcre2SeparatedRunsIsMatchDirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        var scanOffset = startOffsetInBytes;
        while (scanOffset <= input.Length - program.Separator.Length)
        {
            var relativeSeparator = input[scanOffset..].IndexOf(program.Separator);
            if (relativeSeparator < 0)
            {
                return false;
            }

            var separatorStart = scanOffset + relativeSeparator;
            var leadingStart = separatorStart - 1;
            while (leadingStart > startOffsetInBytes && (input[leadingStart] & 0xC0) == 0x80)
            {
                leadingStart--;
            }

            var trailingStart = separatorStart + program.Separator.Length;
            if (leadingStart >= startOffsetInBytes &&
                Pcre2CharacterRunner.TryMatchToken(
                    program.LeadingToken,
                    program.Fallback.Request,
                    input,
                    leadingStart,
                    startOffsetInBytes,
                    Pcre2MatchOptions.None,
                    out var leadingEnd) &&
                leadingEnd == separatorStart &&
                Pcre2CharacterRunner.TryMatchToken(
                    program.FirstTrailingToken,
                    program.Fallback.Request,
                    input,
                    trailingStart,
                    startOffsetInBytes,
                    Pcre2MatchOptions.None,
                    out var secondTrailingStart) &&
                Pcre2CharacterRunner.TryMatchToken(
                    program.SecondTrailingToken,
                    program.Fallback.Request,
                    input,
                    secondTrailingStart,
                    startOffsetInBytes,
                    Pcre2MatchOptions.None,
                    out _))
            {
                return true;
            }

            scanOffset = separatorStart + 1;
        }

        return false;
    }
}
