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
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out int count)
    {
        // Anchored literal prefixes match at most once (at offset 0), so the count is the
        // prefix hit itself. Byte-exact on every input for the sensitive kind: astral
        // scalars never start with ASCII bytes. The folded kind compares per byte under
        // invariant folding (Kelvin stays fenced in CountCore upstream); both ride the
        // shared prefix executor, which switches on the kind.
        if (directFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix &&
            directFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            count = Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, directFamily, out _)
                ? 1
                : 0;

            return true;
        }

        // Anchored literal suffixes match at most once (at the input end), so the
        // count is the tail hit itself. Byte-exact on every input for the sensitive
        // kinds, like prefixes. Folded kinds compare per byte under invariant folding
        // (Kelvin stays fenced in CountCore upstream).
        if ((Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(directFamily.Kind) ||
            directFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
            directFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            count = Utf8AsciiAnchoredSuffixFamilyExecutor.TryMatchSuffix(input, directFamily, out _, out _)
                ? 1
                : 0;

            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.UnicodeLetterBoundedCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectUnicodeLetterBounded);
            return Utf8UnicodeLetterBoundedRepeatExecutor.TryCountLetters(input,
                directFamily.MinCount, directFamily.MaxCount, out count);
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.UnicodeLetterCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectUnicodeLetter);
            // A default or mismatched validation descriptor supplies no proof
            // that four-byte scalars are absent. Preserve them in direct counts.
            count = Utf8UnicodeLetterBoundedRepeatExecutor.CountLetters(input,
                validation.ContainsSupplementaryScalars || validation.ByteLength != input.Length);
            return true;
        }

        if (directFamily.Kind == Utf8FallbackDirectFamilyKind.UnicodeCategoryCount)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectUnicodeCategory);
            count = Utf8UnicodeCategoryExecutor.CountCategory(
                input,
                validation.ContainsSupplementaryScalars || validation.ByteLength != input.Length,
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
