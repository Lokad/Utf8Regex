using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiTokenFamilyExecutor
{
    public static bool TryFindToken(
        ReadOnlySpan<byte> input,
        int startIndex,
        in Utf8FallbackDirectFamilyPlan plan,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = 0;
        matchedLength = 0;

        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.AsciiIdentifierToken:
                return Utf8AsciiTokenFinderExecutor.TryFindAsciiIdentifierToken(input, startIndex, out matchIndex, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns:
                if (plan.LiteralUtf8 is not { Length: 1 } infixLiteralUtf8)
                {
                    return false;
                }

                return Utf8AsciiLiteralBetweenNegatedRunsExecutor.TryFind(
                    input,
                    startIndex,
                    infixLiteralUtf8[0],
                    plan.SeparatorByte,
                    plan.SecondSeparatorByte,
                    out matchIndex,
                    out matchedLength);

            case Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount:
                return Utf8AsciiIpv4TokenExecutor.TryFindAsciiIpv4Token(input, startIndex, out matchIndex, out matchedLength);

            case Utf8FallbackDirectFamilyKind.Utf8UriToken:
                return Utf8AsciiUriTokenExecutor.TryFindAsciiUriToken(input, startIndex, out matchIndex, out matchedLength);

            default:
                return false;
        }
    }

    public static Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult TryFindTokenWithoutValidation(
        ReadOnlySpan<byte> input,
        int startIndex,
        in Utf8FallbackDirectFamilyPlan plan,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        if (plan.Kind is Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns or
            Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount or
            Utf8FallbackDirectFamilyKind.Utf8UriToken)
        {
            if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            {
                return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation;
            }

            return TryFindToken(
                input,
                startIndex,
                plan,
                out matchIndex,
                out matchedLength)
                ? Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match
                : Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NoMatch;
        }

        return plan.Kind switch
        {
            Utf8FallbackDirectFamilyKind.AsciiIdentifierToken => Utf8AsciiTokenFinderExecutor.TryFindAsciiIdentifierTokenWithoutValidation(input, startIndex, out matchIndex, out matchedLength),
            _ => Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation,
        };
    }

    public static Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult TryIsMatchWithoutValidation(
        ReadOnlySpan<byte> input,
        int startIndex,
        in Utf8FallbackDirectFamilyPlan plan)
    {
        if (plan.Kind != Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns)
        {
            var result = TryFindTokenWithoutValidation(input, startIndex, plan, out _, out var matchedLength);
            return result == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match && matchedLength <= 0
                ? Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NoMatch
                : result;
        }

        if (plan.LiteralUtf8 is not { Length: 1 } literalUtf8)
        {
            return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NoMatch;
        }

        if (!Utf8AsciiLiteralBetweenNegatedRunsExecutor.TryIsMatchWithoutValidation(
            input,
            startIndex,
            literalUtf8[0],
            plan.SeparatorByte,
            plan.SecondSeparatorByte,
            out var isMatch))
        {
            return Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation;
        }

        return isMatch
            ? Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match
            : Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NoMatch;
    }

}
