using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledFallbackMatchRouter
{
    public static bool TryFindNextMatch(
        Utf8StructuralSearchPlan[] candidatePlans,
        Utf8VerifierRuntime verifierRuntime,
        Utf8ExecutionProgram program,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        Utf8ExecutionDeadline budget,
        out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (candidatePlans.Length == 0)
        {
            return false;
        }

        using var cursor = new Utf8CandidatePortfolioCursor(candidatePlans, input, startIndex);
        while (cursor.TryGetNextScalarBoundary(out var candidate))
        {
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (Utf8ExecutionInterpreter.TryMatchPrefix(input, program, candidate.StartIndex, captures: null, budget, out var matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                match = new Utf8ValueMatch(true, true, candidate.StartIndex, matchedLength, candidate.StartIndex, matchedLength);
                return true;
            }

            if (verifierRuntime.FallbackCandidateVerifier.TryVerify(input, candidate, validation, ref boundaryMap, ref decoded, out var verification))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                match = new Utf8ValueMatch(
                    verification.Success,
                    verification.IsByteAligned,
                    verification.IndexInUtf16,
                    verification.LengthInUtf16,
                    verification.IndexInBytes,
                    verification.LengthInBytes);
                return true;
            }

            cursor.AdvancePast(candidate.StartIndex + 1);
        }

        return false;
    }
}
