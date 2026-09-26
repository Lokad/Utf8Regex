using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using System.Text.RegularExpressions;

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
        Func<Regex> fallbackRegexFactory,
        out int count)
    {
        // Anchored literal prefixes match at most once (at offset 0), so the count is the
        // prefix hit itself. Byte-exact on every input: astral scalars never start with ASCII
        // bytes, and the extractor admits sensitive ASCII literals only.
        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            directFamily.LiteralUtf8 is { Length: > 0 } anchoredLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            count = anchoredLiteral.Length <= input.Length &&
                input[..anchoredLiteral.Length].SequenceEqual(anchoredLiteral)
                ? 1
                : 0;

            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredQuotedLineSegment);
            count = Utf8QuotedLineSegmentExecutor.CountOrFallback(input, directFamily, fallbackRegexFactory);
            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.LinePrefixCount &&
            directFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectLinePrefix);
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
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectUnicodeLetterBounded);
            count = Utf8UnicodeLetterBoundedRepeatExecutor.CountLettersOrFallback(
                input,
                minCount: directFamily.MinCount,
                maxCount: directFamily.MaxCount,
                fallbackRegexFactory);
            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.UnicodeLetterCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectUnicodeLetter);
            count = Utf8UnicodeLetterBoundedRepeatExecutor.CountLetters(input, validation.ContainsSupplementaryScalars);
            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.UnicodeCategoryCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectUnicodeCategory);
            count = Utf8UnicodeCategoryExecutor.CountCategory(
                input,
                validation.ContainsSupplementaryScalars,
                directFamily.UnicodeCategory);
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
            if (preparedDiagnosticsRoute != Utf8ExecutionRoute.None)
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
            if (diagnosticsRoute != Utf8ExecutionRoute.None)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(diagnosticsRoute);
            }

            return true;
        }

        count = 0;
        return false;
    }
}
