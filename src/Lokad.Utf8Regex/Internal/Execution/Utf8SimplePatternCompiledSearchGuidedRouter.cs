using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SimplePatternCompiledSearchGuidedRouter
{
    public static bool TryMatchWithoutValidation(
        AsciiSimplePatternAnchoredBoundedDatePlan anchoredBoundedDatePlan,
        AsciiSimplePatternRepeatedDigitGroupPlan repeatedDigitGroupPlan,
        bool allowsTrailingNewlineBeforeEnd,
        ReadOnlySpan<byte> input,
        out Utf8ValueMatch match)
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
                match = new Utf8ValueMatch(true, true, 0, boundedDateLength, 0, boundedDateLength);
                return true;
            }

            if (!needsValidation)
            {
                match = Utf8ValueMatch.NoMatch;
                return true;
            }
        }

        if (repeatedDigitGroupPlan.HasValue)
        {
            var directGroup = Utf8AsciiRepeatedDigitGroupExecutor.TryMatchWhole(
                input,
                repeatedDigitGroupPlan,
                out var groupedMatchLength,
                out var needsValidation);
            if (directGroup)
            {
                match = new Utf8ValueMatch(true, true, 0, groupedMatchLength, 0, groupedMatchLength);
                return true;
            }

            if (!needsValidation)
            {
                match = Utf8ValueMatch.NoMatch;
                return true;
            }
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public static bool TryIsMatch(
        AsciiSimplePatternRepeatedDigitGroupPlan repeatedDigitGroupPlan,
        AsciiSimplePatternBoundedSuffixLiteralPlan boundedSuffixLiteralPlan,
        AsciiSimplePatternSymmetricLiteralWindowPlan symmetricLiteralWindowPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionBudget? budget,
        out bool isMatch)
    {
        if (repeatedDigitGroupPlan.HasValue && validation.IsAscii)
        {
            isMatch = Utf8AsciiRepeatedDigitGroupExecutor.TryFind(input, repeatedDigitGroupPlan, out _, out _);
            return true;
        }

        if (boundedSuffixLiteralPlan.HasValue)
        {
            isMatch = Utf8AsciiBoundedSuffixLiteralExecutor.IsMatch(input, boundedSuffixLiteralPlan, budget);
            return true;
        }

        if (symmetricLiteralWindowPlan.HasValue)
        {
            isMatch = Utf8AsciiSymmetricLiteralWindowExecutor.IsMatch(input, symmetricLiteralWindowPlan, budget);
            return true;
        }

        isMatch = false;
        return false;
    }

    public static bool TryCount(
        AsciiSimplePatternRepeatedDigitGroupPlan repeatedDigitGroupPlan,
        AsciiSimplePatternBoundedSuffixLiteralPlan boundedSuffixLiteralPlan,
        AsciiSimplePatternSymmetricLiteralWindowPlan symmetricLiteralWindowPlan,
        Utf8CompiledSymmetricLiteralWindowCounter? compiledSymmetricLiteralWindowCounter,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionBudget? budget,
        out int count)
    {
        if (repeatedDigitGroupPlan.HasValue)
        {
            if (validation.IsAscii && Utf8AsciiRepeatedDigitGroupExecutor.TryFind(input, repeatedDigitGroupPlan, out _, out _))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_simple_pattern_repeated_digit_group");
                count = 1;
                return true;
            }

            if (validation.IsAscii)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_simple_pattern_repeated_digit_group");
                count = 0;
                return true;
            }
        }

        if (boundedSuffixLiteralPlan.HasValue)
        {
            count = Utf8AsciiBoundedSuffixLiteralExecutor.Count(input, boundedSuffixLiteralPlan, budget);
            return true;
        }

        if (symmetricLiteralWindowPlan.HasValue)
        {
            if (budget is null && compiledSymmetricLiteralWindowCounter is not null)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_simple_pattern_symmetric_literal_window_compiled");
                count = compiledSymmetricLiteralWindowCounter.Count(input);
                return true;
            }

            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ascii_simple_pattern_symmetric_literal_window");
            count = Utf8AsciiSymmetricLiteralWindowExecutor.Count(input, symmetricLiteralWindowPlan, budget);
            return true;
        }

        count = 0;
        return false;
    }

    public static bool TryMatch(
        AsciiSimplePatternRepeatedDigitGroupPlan repeatedDigitGroupPlan,
        AsciiSimplePatternBoundedSuffixLiteralPlan boundedSuffixLiteralPlan,
        AsciiSimplePatternSymmetricLiteralWindowPlan symmetricLiteralWindowPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionBudget? budget,
        out Utf8ValueMatch match)
    {
        if (repeatedDigitGroupPlan.HasValue && validation.IsAscii)
        {
            if (Utf8AsciiRepeatedDigitGroupExecutor.TryFind(input, repeatedDigitGroupPlan, out var groupedIndex, out var groupedLength))
            {
                match = new Utf8ValueMatch(true, true, groupedIndex, groupedLength, groupedIndex, groupedLength);
                return true;
            }

            match = Utf8ValueMatch.NoMatch;
            return true;
        }

        if (boundedSuffixLiteralPlan.HasValue)
        {
            match = Utf8AsciiBoundedSuffixLiteralExecutor.Match(input, boundedSuffixLiteralPlan, budget);
            return true;
        }

        if (symmetricLiteralWindowPlan.HasValue)
        {
            match = Utf8AsciiSymmetricLiteralWindowExecutor.Match(input, symmetricLiteralWindowPlan, budget);
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }
}
