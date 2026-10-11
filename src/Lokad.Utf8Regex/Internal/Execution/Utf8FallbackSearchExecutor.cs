using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackSearchExecutor
{
    public static bool TryFindNextVerifiedMatch(
        Utf8StructuralSearchPlan[] candidatePlans,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8SliceInvocation invocation,
        out int verifierInvocations,
        out Utf8ScalarVerificationResult verification)
    {
        verifierInvocations = 0;
        verification = default;
        var entryBreach = invocation.TryChargeInputStep();
        if (entryBreach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Scalar candidate search exceeded {entryBreach}.");
        if (candidatePlans.Length == 0 || (uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        using var cursor = new Utf8CandidatePortfolioCursor(candidatePlans, input, startIndex, ref invocation);
        while (cursor.TryGetNextScalarBoundary(out var candidate))
        {
            verifierInvocations++;
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            var breach = verifierRuntime.TryVerifyCandidate(input, candidate, validation, ref invocation, out verification);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Scalar candidate verification exceeded {breach}.");
            if (verification.Success)
            {
                return true;
            }

            cursor.AdvancePast(candidate.StartIndex + 1);
        }

        return false;
    }

    public static int CountCandidates(Utf8SearchPlan searchPlan, ReadOnlySpan<byte> input, bool requireScalarBoundary)
    {
        if (searchPlan.FallbackCandidatePlans is not { Length: > 0 } candidatePlans)
        {
            return 0;
        }

        var count = 0;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
        using var cursor = new Utf8CandidatePortfolioCursor(candidatePlans, input, 0, ref invocation);
        while (requireScalarBoundary
            ? cursor.TryGetNextScalarBoundary(out _)
            : cursor.TryGetNext(out _))
        {
            count++;
        }

        return count;
    }
}
