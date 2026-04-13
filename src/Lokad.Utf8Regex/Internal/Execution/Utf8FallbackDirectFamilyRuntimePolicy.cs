using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackDirectFamilyRuntimePolicy
{
    public static bool SupportsAsciiWellFormedOnlyMatch(in Utf8FallbackDirectFamilyPlan directFamily)
        => directFamily.SupportsAsciiDefinitiveIsMatch;

    public static bool SupportsWellFormedOnlyMatch(in Utf8FallbackDirectFamilyPlan directFamily)
        => directFamily.SupportsDefinitiveIsMatch;

    public static bool SkipRequiredPrefilterForMatch(in Utf8FallbackDirectFamilyPlan directFamily)
        => directFamily.FindMode == Utf8FallbackFindModeKind.MatchAtStart &&
        directFamily.SupportsDefinitiveIsMatch;

    public static bool SupportsThrowIfInvalidOnlyCount(in Utf8FallbackDirectFamilyPlan directFamily)
        => directFamily.SupportsThrowIfInvalidOnlyCount;

    public static bool SkipRequiredPrefilterForCount(in Utf8FallbackDirectFamilyPlan directFamily)
        => directFamily.SkipsRequiredPrefilterForCount;

    public static bool TryMatchWithoutValidation(
        ReadOnlySpan<byte> input,
        in Utf8FallbackDirectFamilyPlan directFamily,
        in PreparedAsciiDelimitedTokenSearch delimitedTokenSearch,
        in PreparedAsciiLiteralStructuredTokenSearch literalStructuredTokenSearch,
        out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (!directFamily.SupportsAsciiTryMatchWithoutValidation)
        {
            return false;
        }

        if (!Utf8InputAnalyzer.IsAscii(input))
        {
            return false;
        }

        if (!Utf8AsciiDirectFamilyExecutor.TryFindMatch(
                input,
                directFamily,
                delimitedTokenSearch,
                literalStructuredTokenSearch,
                out var matchIndex,
                out var matchedLength))
        {
            match = Utf8ValueMatch.NoMatch;
            return true;
        }

        match = new Utf8ValueMatch(true, true, matchIndex, matchedLength, matchIndex, matchedLength);
        return true;
    }
}
