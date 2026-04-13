namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FallbackReasonClassifier
{
    public static string? Classify(string? fallbackReason, Utf8RegexFeatures features)
    {
        if (!string.Equals(fallbackReason, "unsupported_pattern", StringComparison.Ordinal))
        {
            return fallbackReason;
        }

        if (features.HasBackreferences)
        {
            return "unsupported_backreference";
        }

        if (features.HasConditionals)
        {
            return "unsupported_conditional";
        }

        if (features.HasLookarounds)
        {
            return "unsupported_lookaround";
        }

        if (features.HasAtomicGroups)
        {
            return "unsupported_atomic";
        }

        if (features.HasLoops)
        {
            return "unsupported_loop";
        }

        return fallbackReason;
    }
}
