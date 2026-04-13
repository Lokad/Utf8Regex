using Lokad.Utf8Regex.Internal.Diagnostics;

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
        Utf8ExecutionBudget? budget,
        out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (candidatePlans.Length == 0)
        {
            return false;
        }

        var states = new Utf8StructuralSearchState[candidatePlans.Length];
        var candidates = new Utf8StructuralCandidate[candidatePlans.Length];
        var available = new bool[candidatePlans.Length];
        for (var i = 0; i < candidatePlans.Length; i++)
        {
            states[i] = CreateCandidateState(startIndex);
            available[i] = TryAdvanceCandidate(input, candidatePlans[i], ref states[i], startIndex, out candidates[i]);
        }

        while (true)
        {
            var candidateIndex = FindEarliestCandidateIndex(candidates, available);
            if (candidateIndex < 0)
            {
                return false;
            }

            var candidate = candidates[candidateIndex];
            available[candidateIndex] = TryAdvanceCandidate(input, candidatePlans[candidateIndex], ref states[candidateIndex], startIndex, out candidates[candidateIndex]);

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            if (!IsScalarBoundaryByteOffset(input, candidate.StartIndex))
            {
                continue;
            }

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
        }
    }

    private static Utf8StructuralSearchState CreateCandidateState(int startIndex)
    {
        return new Utf8StructuralSearchState(
            new PreparedSearchScanState(startIndex, default),
            new PreparedWindowScanState(startIndex, new PreparedSearchScanState(startIndex, default)));
    }

    private static bool TryAdvanceCandidate(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchPlan plan,
        ref Utf8StructuralSearchState state,
        int minStartIndex,
        out Utf8StructuralCandidate candidate)
    {
        while (plan.TryFindNextCandidate(input, ref state, out candidate))
        {
            if (candidate.StartIndex >= minStartIndex)
            {
                return true;
            }
        }

        candidate = default;
        return false;
    }

    private static int FindEarliestCandidateIndex(Utf8StructuralCandidate[] candidates, bool[] available)
    {
        var bestIndex = -1;
        for (var i = 0; i < candidates.Length; i++)
        {
            if (!available[i])
            {
                continue;
            }

            if (bestIndex < 0 ||
                candidates[i].StartIndex < candidates[bestIndex].StartIndex ||
                (candidates[i].StartIndex == candidates[bestIndex].StartIndex &&
                 CompareCandidateEnds(candidates[i].EndIndex, candidates[bestIndex].EndIndex) < 0))
            {
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int CompareCandidateEnds(int leftEnd, int rightEnd)
    {
        if (leftEnd < 0)
        {
            return rightEnd < 0 ? 0 : 1;
        }

        if (rightEnd < 0)
        {
            return -1;
        }

        return leftEnd.CompareTo(rightEnd);
    }

    private static bool IsScalarBoundaryByteOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        return (uint)byteOffset <= (uint)input.Length &&
            (byteOffset == 0 || byteOffset == input.Length || (input[byteOffset] & 0xC0) != 0x80);
    }
}
