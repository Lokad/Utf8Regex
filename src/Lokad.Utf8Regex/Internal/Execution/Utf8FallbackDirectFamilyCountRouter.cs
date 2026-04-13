using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackDirectFamilyCountRouter
{
    public static bool TryCount(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        in Utf8FallbackDirectFamilyPlan directFamily,
        Utf8EmittedTokenFamilyMatcher? emittedTokenFamilyMatcher,
        Utf8AsciiLiteralFinder linePrefixFinder,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        Regex fallbackRegex,
        out int count)
    {
        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_direct_anchored_quoted_line_segment");
            count = Utf8QuotedLineSegmentExecutor.CountOrFallback(input, directFamily, fallbackRegex);
            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.LinePrefixCount &&
            directFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_direct_line_prefix");
            count = Utf8LinePrefixExecutor.CountMatchingLines(
                input,
                linePrefixFinder,
                directFamily.TrimLeadingAsciiWhitespace,
                verifier: null,
                out _);
            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.UnicodeLetterBoundedCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_direct_unicode_letter_bounded");
            count = Utf8UnicodeLetterBoundedRepeatExecutor.CountLettersOrFallback(
                input,
                minCount: directFamily.MinCount,
                maxCount: directFamily.MaxCount,
                fallbackRegex);
            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.UnicodeLetterCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_direct_unicode_letter");
            count = Utf8UnicodeLetterBoundedRepeatExecutor.CountLetters(input, validation.ContainsSupplementaryScalars);
            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.UnicodeCategoryCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_direct_unicode_category");
            count = Utf8UnicodeLetterBoundedRepeatExecutor.CountCategory(input, directFamily.UnicodeCategory, validation.ContainsSupplementaryScalars);
            return true;
        }

        if (Utf8FallbackDirectFamilyCategories.IsPreparedTokenCountFamily(directFamily.Kind) &&
            Utf8AsciiDirectFamilyCountExecutor.TryCount(
                input,
                isAscii: false,
                directFamily,
                emittedTokenFamilyMatcher,
                delimitedTokenSearch,
                literalStructuredTokenSearch,
                out count,
                out var preparedDiagnosticsRoute))
        {
            if (preparedDiagnosticsRoute is not null)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(preparedDiagnosticsRoute);
            }

            return true;
        }

        if (Utf8AsciiDirectFamilyCountExecutor.TryCount(
            input,
            validation.IsAscii || (validation.ByteLength == 0 && Utf8InputAnalyzer.IsAscii(input)),
            directFamily,
            emittedTokenFamilyMatcher,
            delimitedTokenSearch,
            literalStructuredTokenSearch,
            out count,
            out var diagnosticsRoute))
        {
            if (diagnosticsRoute is not null)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(diagnosticsRoute);
            }

            return true;
        }

        count = 0;
        return false;
    }
}
