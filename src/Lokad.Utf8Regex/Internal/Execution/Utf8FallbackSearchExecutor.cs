using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackSearchExecutor
{
    public static bool TryFindNextCandidate(
        Utf8SearchPlan searchPlan,
        ReadOnlySpan<byte> input,
        int startIndex,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        if (!searchPlan.FallbackSearch.HasCandidates)
        {
            return false;
        }

        var states = new Utf8StructuralSearchState[searchPlan.FallbackSearch.CandidatePlans!.Length];
        var candidates = new Utf8StructuralCandidate[searchPlan.FallbackSearch.CandidatePlans.Length];
        var available = new bool[searchPlan.FallbackSearch.CandidatePlans.Length];
        for (var i = 0; i < searchPlan.FallbackSearch.CandidatePlans.Length; i++)
        {
            states[i] = CreateCandidateState(startIndex);
            available[i] = TryAdvanceCandidate(input, searchPlan.FallbackSearch.CandidatePlans[i], ref states[i], startIndex, out candidates[i]);
        }

        var candidateIndex = FindEarliestCandidateIndex(candidates, available);
        if (candidateIndex < 0)
        {
            return false;
        }

        candidate = candidates[candidateIndex];
        return true;
    }

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
        while (TryFindNextCandidate(searchPlan, input, startIndex, out var candidate))
        {
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            if (!IsScalarBoundaryByteOffset(input, candidate.StartIndex))
            {
                startIndex = candidate.StartIndex + 1;
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (verifierRuntime.FallbackCandidateVerifier.TryVerify(input, candidate, validation, ref boundaryMap, ref decoded, out verification))
            {
                return true;
            }

            startIndex = candidate.StartIndex + 1;
        }

        return false;
    }

    public static int CountCandidates(Utf8SearchPlan searchPlan, ReadOnlySpan<byte> input, bool requireScalarBoundary)
    {
        if (!searchPlan.FallbackSearch.HasCandidates)
        {
            return 0;
        }

        var count = 0;
        foreach (var plan in searchPlan.FallbackSearch.CandidatePlans!)
        {
            var state = CreateCandidateState(0);
            while (plan.TryFindNextCandidate(input, ref state, out var candidate))
            {
                if (requireScalarBoundary && !IsScalarBoundaryByteOffset(input, candidate.StartIndex))
                {
                    continue;
                }

                count++;
            }
        }

        return count;
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
