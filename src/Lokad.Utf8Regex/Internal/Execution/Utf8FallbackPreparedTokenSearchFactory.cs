namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackPreparedTokenSearchFactory
{
    public static PreparedAsciiDelimitedTokenSearch CreateDelimitedTokenSearch(in Utf8FallbackDirectFamilyPlan plan)
        => plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount &&
            plan.LiteralUtf8 is { Length: > 0 } delimiter && plan.SecondaryLiteralUtf8 is { Length: 1 } secondary &&
            !plan.TokenMasks.Head.IsEmpty && !plan.TokenMasks.Middle.IsEmpty && !plan.TokenMasks.Tail.IsEmpty
                ? new PreparedAsciiDelimitedTokenSearch(delimiter, secondary, plan.TokenMasks) : default;

    public static PreparedAsciiLiteralStructuredTokenSearch CreateLiteralStructuredTokenSearch(in Utf8FallbackDirectFamilyPlan plan)
        => plan.Kind == Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount &&
            plan.LiteralUtf8 is { Length: > 0 } literal &&
            !plan.TokenMasks.Head.IsEmpty && !plan.TokenMasks.Middle.IsEmpty && !plan.TokenMasks.Tail.IsEmpty
                ? new PreparedAsciiLiteralStructuredTokenSearch(literal, plan.SecondaryLiteralUtf8, plan.TertiaryLiteralUtf8, plan.TokenMasks) : default;
}
