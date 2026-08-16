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

            case Utf8FallbackDirectFamilyKind.AsciiIpv4Token:
                return Utf8AsciiIpv4TokenExecutor.TryFindAsciiIpv4Token(input, startIndex, out matchIndex, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AsciiUriToken:
                return Utf8AsciiUriTokenExecutor.TryFindAsciiUriToken(input, startIndex, out matchIndex, out matchedLength);

            case Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken:
                return Utf8AsciiBoundedDateTokenExecutor.TryFindAsciiBoundedDateToken(input, startIndex, plan, out matchIndex, out matchedLength);

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

        return plan.Kind switch
        {
            Utf8FallbackDirectFamilyKind.AsciiIdentifierToken => Utf8AsciiTokenFinderExecutor.TryFindAsciiIdentifierTokenWithoutValidation(input, startIndex, out matchIndex, out matchedLength),
            _ => Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation,
        };
    }

    public static bool TryCountTokens(
        ReadOnlySpan<byte> input,
        bool isAscii,
        in Utf8FallbackDirectFamilyPlan plan,
        out int count,
        out Utf8ExecutionRoute diagnosticsRoute)
    {
        count = 0;
        diagnosticsRoute = Utf8ExecutionRoute.None;

        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.AsciiIdentifierToken when isAscii:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiIdentifierToken;
                count = CountAsciiIdentifierTokens(input);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiIpv4Token when isAscii:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiIpv4Token;
                count = Utf8AsciiIpv4TokenExecutor.CountAsciiIpv4Tokens(input);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiUriToken when isAscii:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiUriToken;
                count = Utf8AsciiUriTokenExecutor.CountAsciiUriTokens(input);
                return true;

            case Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken when isAscii:
                diagnosticsRoute = Utf8ExecutionRoute.FallbackDirectAsciiBoundedDateToken;
                count = Utf8AsciiBoundedDateTokenExecutor.CountAsciiBoundedDateTokens(input, plan);
                return true;

            default:
                return false;
        }
    }

    private static int CountAsciiIdentifierTokens(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        while (Utf8AsciiTokenFinderExecutor.TryFindAsciiIdentifierToken(input, startIndex, out var matchIndex, out var matchedLength))
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }
}
