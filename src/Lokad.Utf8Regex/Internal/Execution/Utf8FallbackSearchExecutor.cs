using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackSearchExecutor
{
    public static bool TryFindNextVerifiedMatch(
        Utf8SearchPlan searchPlan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult verification)
    {
        verification = default;
        if (searchPlan.FallbackSearch.CandidatePlans is not { Length: > 0 } candidatePlans)
        {
            return false;
        }

        using var cursor = new Utf8CandidatePortfolioCursor(candidatePlans, input, startIndex);
        while (cursor.TryGetNextScalarBoundary(out var candidate))
        {
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (verifierRuntime.FallbackCandidateVerifier.TryVerify(input, candidate, validation, ref boundaryMap, ref decoded, out verification))
            {
                return true;
            }

            cursor.AdvancePast(candidate.StartIndex + 1);
        }

        return false;
    }

    public static int CountCandidates(Utf8SearchPlan searchPlan, ReadOnlySpan<byte> input, bool requireScalarBoundary)
    {
        if (searchPlan.FallbackSearch.CandidatePlans is not { Length: > 0 } candidatePlans)
        {
            return 0;
        }

        var count = 0;
        using var cursor = new Utf8CandidatePortfolioCursor(candidatePlans, input, 0);
        while (requireScalarBoundary
            ? cursor.TryGetNextScalarBoundary(out _)
            : cursor.TryGetNext(out _))
        {
            count++;
        }

        return count;
    }
}
