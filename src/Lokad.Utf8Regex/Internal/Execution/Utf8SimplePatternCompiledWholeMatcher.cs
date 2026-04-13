namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SimplePatternCompiledWholeMatcher
{
    internal static string GetDirectAnchoredFixedAlternationDebugSummary(Utf8RegexPlan regexPlan, ReadOnlySpan<byte> input)
    {
        if (!Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedAlternationSimplePattern(regexPlan))
        {
            return "unavailable";
        }

        var parts = new string[regexPlan.SimplePatternPlan.Branches.Length];
        for (var i = 0; i < regexPlan.SimplePatternPlan.Branches.Length; i++)
        {
            var branch = regexPlan.SimplePatternPlan.Branches[i];
            var candidate = MatchesDirectAnchoredFixedAlternationCandidate(regexPlan, input, branch.Length);
            var matched = candidate && TryMatchDirectFixedBranch(regexPlan, input, branch, out _);
            parts[i] = $"{branch.Length}:{(candidate ? "candidate" : "skip")}:{(matched ? "match" : "miss")}";
        }

        return string.Join(",", parts);
    }

    public static bool TryMatchDirectAnchoredFixedLengthSimplePattern(Utf8RegexPlan regexPlan, ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (!Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedLengthSimplePattern(regexPlan))
        {
            return false;
        }

        var branch = regexPlan.SimplePatternPlan.Branches[0];
        if (!MatchesDirectAnchoredFixedLengthCandidate(regexPlan, input, branch.Length))
        {
            return false;
        }

        if (TryMatchUniformPositiveCharClassBranch(input, branch, out matchedLength))
        {
            return true;
        }

        return TryMatchDirectFixedBranch(regexPlan, input, branch, out matchedLength);
    }

    public static bool TryMatchDirectAnchoredFixedAlternationSimplePattern(Utf8RegexPlan regexPlan, ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (!Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedAlternationSimplePattern(regexPlan))
        {
            return false;
        }

        foreach (var branch in regexPlan.SimplePatternPlan.Branches)
        {
            if (!MatchesDirectAnchoredFixedAlternationCandidate(regexPlan, input, branch.Length))
            {
                continue;
            }

            if (TryMatchDirectFixedBranch(regexPlan, input, branch, out matchedLength))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryMatchAnchoredValidator(
        Utf8RegexPlan regexPlan,
        Utf8EmittedAnchoredValidatorMatcher? emittedAnchoredValidatorMatcher,
        bool allowTrailingNewline,
        ReadOnlySpan<byte> input,
        out int matchedLength)
    {
        if (regexPlan.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue)
        {
            return Utf8AsciiBoundedDateTokenExecutor.TryMatchWhole(
                input,
                regexPlan.SimplePatternPlan.AnchoredBoundedDatePlan,
                allowTrailingNewline,
                out matchedLength,
                out _);
        }

        if (TryMatchDirectAnchoredFixedLengthSimplePattern(regexPlan, input, out matchedLength))
        {
            return true;
        }

        var headTailPlan = regexPlan.SimplePatternPlan.AnchoredHeadTailRunPlan;
        if (headTailPlan.HasValue &&
            Utf8AsciiAnchoredValidatorExecutor.TryMatchWhole(input, headTailPlan, allowTrailingNewline, out matchedLength))
        {
            return true;
        }

        var plan = regexPlan.SimplePatternPlan.AnchoredValidatorPlan;

        if (emittedAnchoredValidatorMatcher is not null)
        {
            matchedLength = emittedAnchoredValidatorMatcher.MatchWhole(input);
            return matchedLength >= 0;
        }

        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWhole(input, plan, allowTrailingNewline, out matchedLength);
    }

    public static Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult TryMatchAnchoredValidatorWithoutValidation(
        Utf8RegexPlan regexPlan,
        bool allowTrailingNewline,
        ReadOnlySpan<byte> input,
        out int matchedLength)
    {
        if (regexPlan.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue)
        {
            var matched = Utf8AsciiBoundedDateTokenExecutor.TryMatchWhole(
                input,
                regexPlan.SimplePatternPlan.AnchoredBoundedDatePlan,
                allowTrailingNewline,
                out matchedLength,
                out var needsValidation);
            if (matched)
            {
                return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match;
            }

            return needsValidation
                ? Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation
                : Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NoMatch;
        }

        if (Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedLengthSimplePattern(regexPlan))
        {
            if (TryMatchDirectAnchoredFixedLengthSimplePattern(regexPlan, input, out matchedLength))
            {
                return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match;
            }

            if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            {
                matchedLength = 0;
                return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation;
            }

            matchedLength = 0;
            return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NoMatch;
        }

        var headTailPlan = regexPlan.SimplePatternPlan.AnchoredHeadTailRunPlan;
        if (headTailPlan.HasValue)
        {
            return Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeWithoutValidation(
                input,
                headTailPlan,
                allowTrailingNewline,
                out matchedLength);
        }

        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeWithoutValidation(
            input,
            regexPlan.SimplePatternPlan.AnchoredValidatorPlan,
            allowTrailingNewline,
            out matchedLength);
    }

    private static bool MatchesDirectAnchoredFixedLengthCandidate(Utf8RegexPlan regexPlan, ReadOnlySpan<byte> input, int branchLength)
    {
        return regexPlan.SearchPlan.Kind switch
        {
            Utf8SearchKind.TrailingAnchorFixedLengthEnd => input.Length == branchLength,
            Utf8SearchKind.TrailingAnchorFixedLengthEndZ =>
                input.Length == branchLength ||
                (input.Length == branchLength + 1 && input[branchLength] == (byte)'\n'),
            _ => Utf8SearchExecutor.FindNext(regexPlan.SearchPlan, input, 0) == 0,
        };
    }

    private static bool MatchesDirectAnchoredFixedAlternationCandidate(Utf8RegexPlan regexPlan, ReadOnlySpan<byte> input, int branchLength)
    {
        return regexPlan.SimplePatternPlan.AllowsTrailingNewlineBeforeEnd
            ? input.Length == branchLength || (input.Length == branchLength + 1 && input[branchLength] == (byte)'\n')
            : input.Length == branchLength;
    }

    private static bool TryMatchUniformPositiveCharClassBranch(ReadOnlySpan<byte> input, AsciiSimplePatternToken[] branch, out int matchedLength)
    {
        matchedLength = 0;
        if (branch.Length == 0 ||
            branch[0].Kind != AsciiSimplePatternTokenKind.CharClass ||
            branch[0].CharClass is not { Negated: false } firstClass)
        {
            return false;
        }

        for (var i = 1; i < branch.Length; i++)
        {
            if (branch[i].Kind != AsciiSimplePatternTokenKind.CharClass ||
                branch[i].CharClass is not { Negated: false } nextClass ||
                !firstClass.HasSameDefinition(nextClass))
            {
                return false;
            }
        }

        var allowed = firstClass.GetPositiveMatchBytes();
        if (allowed.Length == 0 || input[..branch.Length].IndexOfAnyExcept(allowed) >= 0)
        {
            return false;
        }

        matchedLength = branch.Length;
        return true;
    }

    private static bool TryMatchDirectFixedBranch(Utf8RegexPlan regexPlan, ReadOnlySpan<byte> input, AsciiSimplePatternToken[] branch, out int matchedLength)
    {
        matchedLength = 0;
        if (TryMatchUniformPositiveCharClassBranch(input, branch, out matchedLength))
        {
            return true;
        }

        for (var i = 0; i < branch.Length; i++)
        {
            var token = branch[i];
            var value = input[i];
            switch (token.Kind)
            {
                case AsciiSimplePatternTokenKind.Literal:
                    if (regexPlan.SimplePatternPlan.IgnoreCase)
                    {
                        value = Internal.Utilities.AsciiSearch.FoldCase(value);
                    }

                    if (value != token.Literal)
                    {
                        return false;
                    }

                    break;

                case AsciiSimplePatternTokenKind.Dot:
                    break;

                case AsciiSimplePatternTokenKind.CharClass when token.CharClass is not null:
                    if (!token.CharClass.Contains(regexPlan.SimplePatternPlan.IgnoreCase ? Internal.Utilities.AsciiSearch.FoldCase(value) : value))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        matchedLength = branch.Length;
        return true;
    }
}
