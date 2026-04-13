namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackPreparedTokenSearchFactory
{
    public static PreparedAsciiDelimitedTokenSearch CreateDelimitedTokenSearch(in Utf8FallbackDirectFamilyPlan plan)
    {
        return plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount &&
            plan.LiteralUtf8 is { Length: > 0 } delimiterUtf8 &&
            plan.SecondaryLiteralUtf8 is { Length: 1 } secondaryDelimiterUtf8 &&
            plan.HeadCharSetUtf8 is { Length: > 0 } headCharSetUtf8 &&
            plan.MiddleCharSetUtf8 is { Length: > 0 } middleCharSetUtf8 &&
            plan.TailCharSetUtf8 is { Length: > 0 } tailCharSetUtf8
                ? new PreparedAsciiDelimitedTokenSearch(
                    delimiterUtf8,
                    secondaryDelimiterUtf8,
                    headCharSetUtf8,
                    middleCharSetUtf8,
                    tailCharSetUtf8)
                : default;
    }

    public static PreparedAsciiLiteralStructuredTokenSearch CreateLiteralStructuredTokenSearch(in Utf8FallbackDirectFamilyPlan plan)
    {
        return plan.Kind == Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount &&
            plan.LiteralUtf8 is { Length: > 0 } structuredLiteralUtf8 &&
            plan.HeadCharSetUtf8 is { Length: > 0 } structuredHeadCharSetUtf8 &&
            plan.MiddleCharSetUtf8 is { Length: > 0 } structuredMiddleCharSetUtf8 &&
            plan.TailCharSetUtf8 is { Length: > 0 } structuredTailCharSetUtf8
                ? new PreparedAsciiLiteralStructuredTokenSearch(
                    structuredLiteralUtf8,
                    structuredHeadCharSetUtf8,
                    structuredMiddleCharSetUtf8,
                    structuredTailCharSetUtf8,
                    plan.SecondaryLiteralUtf8 ?? [],
                    ReadOptionalTailCharSet(plan.ExtraCharSetUtf8, first: true),
                    plan.TertiaryLiteralUtf8 ?? [],
                    ReadOptionalTailCharSet(plan.ExtraCharSetUtf8, first: false))
                : default;
    }

    public static ReadOnlySpan<byte> ReadOptionalTailCharSet(byte[]? combined, bool first)
    {
        if (combined is not { Length: > 0 })
        {
            return [];
        }

        var separator = Array.IndexOf(combined, (byte)0);
        if (separator < 0)
        {
            return first ? combined : [];
        }

        return first
            ? combined.AsSpan(0, separator)
            : combined.AsSpan(separator + 1);
    }
}
