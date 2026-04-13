namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiPreparedTokenFamilyExecutor
{
    public static bool TryFindToken(
        ReadOnlySpan<byte> input,
        int startIndex,
        in Utf8FallbackDirectFamilyPlan plan,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = 0;
        matchedLength = 0;

        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount:
                if (delimitedTokenSearch.HasValue)
                {
                    return Utf8AsciiDelimitedTokenExecutor.TryFindDelimitedToken(
                        input,
                        startIndex,
                        delimitedTokenSearch,
                        out matchIndex,
                        out matchedLength);
                }

                return plan.LiteralUtf8 is { Length: > 0 } delimiterUtf8 &&
                    plan.SecondaryLiteralUtf8 is { Length: > 0 } secondaryDelimiterUtf8 &&
                    plan.HeadCharSetUtf8 is { Length: > 0 } headCharSetUtf8 &&
                    plan.MiddleCharSetUtf8 is { Length: > 0 } middleCharSetUtf8 &&
                    plan.TailCharSetUtf8 is { Length: > 0 } tailCharSetUtf8 &&
                    Utf8AsciiDelimitedTokenExecutor.TryFindDelimitedToken(
                        input,
                        startIndex,
                        delimiterUtf8,
                        secondaryDelimiterUtf8,
                        headCharSetUtf8,
                        middleCharSetUtf8,
                        tailCharSetUtf8,
                        out matchIndex,
                        out matchedLength);

            case Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount:
                if (literalStructuredTokenSearch.HasValue)
                {
                    return Utf8AsciiLiteralStructuredTokenExecutor.TryFindStructuredToken(
                        input,
                        startIndex,
                        literalStructuredTokenSearch,
                        out matchIndex,
                        out matchedLength);
                }

                if (plan.LiteralUtf8 is not { Length: > 0 } structuredLiteralUtf8 ||
                    plan.HeadCharSetUtf8 is not { Length: > 0 } structuredHeadCharSetUtf8 ||
                    plan.MiddleCharSetUtf8 is not { Length: > 0 } structuredMiddleCharSetUtf8 ||
                    plan.TailCharSetUtf8 is not { Length: > 0 } structuredTailCharSetUtf8)
                {
                    return false;
                }

                var optionalTail1CharSetUtf8 = Utf8FallbackPreparedTokenSearchFactory.ReadOptionalTailCharSet(plan.ExtraCharSetUtf8, first: true);
                var optionalTail2CharSetUtf8 = Utf8FallbackPreparedTokenSearchFactory.ReadOptionalTailCharSet(plan.ExtraCharSetUtf8, first: false);
                return Utf8AsciiLiteralStructuredTokenExecutor.TryFindStructuredToken(
                    input,
                    startIndex,
                    structuredLiteralUtf8,
                    structuredHeadCharSetUtf8,
                    structuredMiddleCharSetUtf8,
                    structuredTailCharSetUtf8,
                    plan.SecondaryLiteralUtf8 ?? [],
                    optionalTail1CharSetUtf8,
                    plan.TertiaryLiteralUtf8 ?? [],
                    optionalTail2CharSetUtf8,
                    out matchIndex,
                    out matchedLength);

            default:
                return false;
        }
    }

    public static int CountTokens(
        ReadOnlySpan<byte> input,
        in Utf8FallbackDirectFamilyPlan plan,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch)
    {
        switch (plan.Kind)
        {
            case Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount:
                if (delimitedTokenSearch.HasValue)
                {
                    return Utf8AsciiDelimitedTokenExecutor.CountDelimitedTokens(input, delimitedTokenSearch);
                }

                if (plan.LiteralUtf8 is not { Length: > 0 } delimiterUtf8 ||
                    plan.SecondaryLiteralUtf8 is not { Length: > 0 } secondaryDelimiterUtf8 ||
                    plan.HeadCharSetUtf8 is not { Length: > 0 } headCharSetUtf8 ||
                    plan.MiddleCharSetUtf8 is not { Length: > 0 } middleCharSetUtf8 ||
                    plan.TailCharSetUtf8 is not { Length: > 0 } tailCharSetUtf8)
                {
                    return 0;
                }

                return Utf8AsciiDelimitedTokenExecutor.CountDelimitedTokens(
                    input,
                    delimiterUtf8,
                    secondaryDelimiterUtf8,
                    headCharSetUtf8,
                    middleCharSetUtf8,
                    tailCharSetUtf8);

            case Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount:
                if (literalStructuredTokenSearch.HasValue)
                {
                    return Utf8AsciiLiteralStructuredTokenExecutor.CountStructuredTokens(input, literalStructuredTokenSearch);
                }

                if (plan.LiteralUtf8 is not { Length: > 0 } structuredLiteralUtf8 ||
                    plan.HeadCharSetUtf8 is not { Length: > 0 } structuredHeadCharSetUtf8 ||
                    plan.MiddleCharSetUtf8 is not { Length: > 0 } structuredMiddleCharSetUtf8 ||
                    plan.TailCharSetUtf8 is not { Length: > 0 } structuredTailCharSetUtf8)
                {
                    return 0;
                }

                var optionalTail1CharSetUtf8 = Utf8FallbackPreparedTokenSearchFactory.ReadOptionalTailCharSet(plan.ExtraCharSetUtf8, first: true);
                var optionalTail2CharSetUtf8 = Utf8FallbackPreparedTokenSearchFactory.ReadOptionalTailCharSet(plan.ExtraCharSetUtf8, first: false);
                return Utf8AsciiLiteralStructuredTokenExecutor.CountStructuredTokens(
                    input,
                    structuredLiteralUtf8,
                    structuredHeadCharSetUtf8,
                    structuredMiddleCharSetUtf8,
                    structuredTailCharSetUtf8,
                    plan.SecondaryLiteralUtf8 ?? [],
                    optionalTail1CharSetUtf8,
                    plan.TertiaryLiteralUtf8 ?? [],
                    optionalTail2CharSetUtf8);

            default:
                return 0;
        }
    }
}
