namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SimplePatternCompiledSearchGuidedRouter
{
    public static bool TryMatchWithoutValidation(
        AsciiSimplePatternAnchoredBoundedDatePlan anchoredBoundedDatePlan, bool allowsTrailingNewlineBeforeEnd, ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (anchoredBoundedDatePlan.HasValue)
        {
            var directDate = Utf8AsciiBoundedDateTokenExecutor.TryMatchWhole(
                input,
                anchoredBoundedDatePlan,
                allowsTrailingNewlineBeforeEnd,
                out var boundedDateLength,
                out var needsValidation);
            if (directDate)
            {
                match = new Utf8CaptureRange(0, 0 + (boundedDateLength));
                return true;
            }

            if (!needsValidation)
            {
                match = default(Utf8CaptureRange);
                return true;
            }
        }

        match = default(Utf8CaptureRange);
        return false;
    }
}
