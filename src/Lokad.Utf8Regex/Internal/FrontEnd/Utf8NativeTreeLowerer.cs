using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8NativeTreeLowerer
{
    public static bool TryLower(Utf8AnalyzedRegex analyzedRegex, Utf8ExecutionTree? tree, Utf8ExecutionProgram? program, Utf8DeterministicAnchorSearch deterministicAnchor, Utf8DeterministicVerifierGuards deterministicGuards, Utf8FallbackVerifierPlan fallbackVerifier, out Utf8RegexPlan regexPlan)
    {
        if (TryLowerLiteral(analyzedRegex, tree, program, deterministicAnchor, deterministicGuards, fallbackVerifier, out regexPlan))
        {
            return true;
        }

        regexPlan = default;
        return false;
    }

    private static bool TryLowerLiteral(Utf8AnalyzedRegex analyzedRegex, Utf8ExecutionTree? tree, Utf8ExecutionProgram? program, Utf8DeterministicAnchorSearch deterministicAnchor, Utf8DeterministicVerifierGuards deterministicGuards, Utf8FallbackVerifierPlan fallbackVerifier, out Utf8RegexPlan regexPlan)
    {
        if (analyzedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase) ||
            analyzedRegex.LiteralUtf8 is not { } literalUtf8 ||
            tree is null ||
            program is null)
        {
            regexPlan = default;
            return false;
        }

        var searchKind = analyzedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCase
            ? Utf8SearchKind.AsciiLiteralIgnoreCase
            : Utf8SearchKind.ExactAsciiLiteral;

        regexPlan = new Utf8RegexPlan(
            new Utf8ExecutionPlan(
                analyzedRegex.ExecutionPattern,
                analyzedRegex.ExecutionKind,
                tree,
                program,
                deterministicAnchor,
                deterministicGuards,
                fallbackVerifier,
                default,
                analyzedRegex.SimplePatternPlan,
                analyzedRegex.StructuralIdentifierFamilyPlan,
                analyzedRegex.StructuralTokenWindowPlan,
                analyzedRegex.StructuralRepeatedSegmentPlan,
                analyzedRegex.StructuralQuotedRelationPlan,
                analyzedRegex.OrderedLiteralWindowPlan,
                literalUtf8,
                analyzedRegex.FallbackReason,
                analyzedRegex.FallbackDirectFamily),
            new Utf8SearchPlan(
                searchKind,
                literalUtf8,
                trailingLiteralUtf8: analyzedRegex.SearchInfo.TrailingLiteralUtf8,
                leadingBoundary: analyzedRegex.SearchInfo.LeadingBoundary,
                trailingBoundary: analyzedRegex.SearchInfo.TrailingBoundary));
        return true;
    }
}

