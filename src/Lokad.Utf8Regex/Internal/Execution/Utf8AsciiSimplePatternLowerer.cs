namespace Lokad.Utf8Regex.Internal.Execution;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private const int MaxExpandedBranches = 64;
    private const int MaxLiteralCharClassExpansion = 4;

    public static bool TryCreatePlan(
        Utf8SemanticRegex semanticRegex,
        RegexOptions options,
        out AsciiSimplePatternPlan simplePatternPlan,
        out Utf8SearchPlan searchPlan)
    {
        simplePatternPlan = default;
        searchPlan = default;

        if (semanticRegex.RuntimeTree?.Root is not { } root ||
            root.Kind != RuntimeFrontEnd.RegexNodeKind.Capture ||
            root.ChildCount != 1 ||
            !TryLowerBranches(root.Child(0), out var loweredBranches))
        {
            return false;
        }

        if (loweredBranches.Length == 0 || loweredBranches[0].Tokens.Count == 0)
        {
            return false;
        }

        var isStartAnchored = loweredBranches[0].IsStartAnchored;
        var isEndAnchored = loweredBranches[0].IsEndAnchored;
        var allowsTrailingNewlineBeforeEnd = AllowsTrailingNewlineBeforeEnd(root.Child(0));
        for (var i = 1; i < loweredBranches.Length; i++)
        {
            if (loweredBranches[i].Tokens.Count == 0 ||
                loweredBranches[i].IsStartAnchored != isStartAnchored ||
                loweredBranches[i].IsEndAnchored != isEndAnchored)
            {
                return false;
            }
        }

        var branches = loweredBranches.Select(static branch => branch.Tokens.ToArray()).ToArray();
        var allFixedChecks = loweredBranches.SelectMany(static branch => branch.FixedChecks).ToArray();
        if (!TryGetLengthBounds(branches, out var minLength, out _) || minLength == 0)
        {
            return false;
        }

        var ignoreCase = (options & RegexOptions.IgnoreCase) != 0;
        if (ignoreCase)
        {
            NormalizeBranchesForIgnoreCase(branches);
        }

        ExtractSearchLiterals(branches, out var searchLiteralOffset, out var searchLiterals, out var fixedLiteralChecks);
        if (allFixedChecks.Length > 0)
        {
            if (ignoreCase)
            {
                NormalizeFixedChecksForIgnoreCase(allFixedChecks);
            }

            fixedLiteralChecks = fixedLiteralChecks.Length == 0
                ? allFixedChecks
                : [.. fixedLiteralChecks, .. allFixedChecks];
        }

        simplePatternPlan = new AsciiSimplePatternPlan(
            branches,
            searchLiteralOffset,
            searchLiterals,
            fixedLiteralChecks,
            isStartAnchored,
            isEndAnchored,
            allowsTrailingNewlineBeforeEnd,
            ignoreCase,
            IsUtf8ByteSafe(branches),
            TryExtractCharClassRunPlan(branches, isStartAnchored, isEndAnchored, out var runPlan) ? runPlan : default,
            anchoredValidatorPlan: TryExtractAnchoredValidatorPlan(branches, isStartAnchored, isEndAnchored, ignoreCase, out var anchoredValidatorPlan)
                ? anchoredValidatorPlan
                : default,
            anchoredBoundedDatePlan: TryExtractAnchoredBoundedDatePlan(branches, isStartAnchored, isEndAnchored, out var anchoredBoundedDatePlan)
                ? anchoredBoundedDatePlan
                : default,
            repeatedDigitGroupPlan: TryExtractRepeatedDigitGroupPlan(branches, ignoreCase, out var repeatedDigitGroupPlan)
                ? repeatedDigitGroupPlan
                : default,
            boundedSuffixLiteralPlan: TryExtractBoundedSuffixLiteralPlan(branches, isStartAnchored, isEndAnchored, out var boundedSuffixLiteralPlan)
                ? boundedSuffixLiteralPlan
                : default,
            symmetricLiteralWindowPlan: TryExtractSymmetricLiteralWindowPlan(branches, isStartAnchored, isEndAnchored, out var symmetricLiteralWindowPlan)
                ? symmetricLiteralWindowPlan
                : default);
        searchPlan = searchLiterals.Length switch
        {
            1 => new Utf8SearchPlan(ignoreCase ? Utf8SearchKind.AsciiLiteralIgnoreCase : Utf8SearchKind.ExactAsciiLiteral, searchLiterals[0]),
            > 1 when !ignoreCase && searchLiteralOffset == 0 => new Utf8SearchPlan(Utf8SearchKind.ExactAsciiLiterals, null, alternateLiteralsUtf8: searchLiterals),
            _ => new Utf8SearchPlan(Utf8SearchKind.None, null),
        };

        return true;
    }

    private static bool IsUtf8ByteSafe(AsciiSimplePatternToken[][] branches)
    {
        foreach (var branch in branches)
        {
            foreach (var token in branch)
            {
                if (token.Kind == AsciiSimplePatternTokenKind.Dot)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryGetUniformRepeatedClass(
        AsciiSimplePatternToken[] tokens,
        int start,
        int endExclusive,
        out AsciiCharClass charClass)
    {
        charClass = null!;
        if (endExclusive <= start ||
            tokens[start].Kind != AsciiSimplePatternTokenKind.CharClass ||
            tokens[start].CharClass is not { } firstCharClass)
        {
            return false;
        }

        for (var i = start + 1; i < endExclusive; i++)
        {
            if (tokens[i].Kind != AsciiSimplePatternTokenKind.CharClass ||
                tokens[i].CharClass is not { } nextCharClass ||
                !firstCharClass.HasSameDefinition(nextCharClass))
            {
                return false;
            }
        }

        charClass = firstCharClass;
        return true;
    }

    private static bool TryLowerLiteral(char ch, out AsciiSimplePatternToken token)
    {
        if (ch > 0x7F)
        {
            token = default;
            return false;
        }

        token = new AsciiSimplePatternToken((byte)ch);
        return true;
    }

    private static bool TryLowerLiteralText(string? text, out List<AsciiSimplePatternToken> tokens)
    {
        tokens = [];
        if (text is null)
        {
            return false;
        }

        tokens = new List<AsciiSimplePatternToken>(text.Length);
        foreach (var ch in text)
        {
            if (!TryLowerLiteral(ch, out var token))
            {
                tokens = [];
                return false;
            }

            tokens.Add(token);
        }

        return true;
    }

    private static bool TryGetLengthBounds(AsciiSimplePatternToken[][] branches, out int minLength, out int maxLength)
    {
        minLength = int.MaxValue;
        maxLength = 0;
        foreach (var branch in branches)
        {
            if (branch.Length == 0)
            {
                return false;
            }

            if (branch.Length < minLength)
            {
                minLength = branch.Length;
            }

            if (branch.Length > maxLength)
            {
                maxLength = branch.Length;
            }
        }

        return true;
    }

    private readonly record struct LoweredBranch(
        List<AsciiSimplePatternToken> Tokens,
        List<AsciiFixedLiteralCheck> FixedChecks,
        bool IsStartAnchored,
        bool IsEndAnchored);
}
