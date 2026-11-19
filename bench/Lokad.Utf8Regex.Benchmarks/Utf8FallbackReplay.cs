using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8FallbackReplay
{
    internal static Utf8StructuralSearchPlan[] GetCandidatePlans(in Utf8SearchPlan plan) =>
        plan.HasStructuralCandidates ? [plan.StructuralSearchPlan] : plan.RequiredWindowPrefilterPlans ?? [];

    public static bool TryFindNextVerifiedMatch(
        Utf8StructuralSearchPlan[] candidatePlans,
        Utf8PreparedRegex prepared,
        ReadOnlySpan<byte> input,
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
            var breach = Utf8CandidateVerificationReplay.TryVerifyCandidate(prepared, input, candidate, ref invocation, out verification);
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
        var candidatePlans = GetCandidatePlans(searchPlan);
        if (candidatePlans.Length == 0)
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

    internal static bool IsMatch(Utf8Regex regex, ReadOnlySpan<byte> input)
    {
        var plan = regex.Inspection.SearchPlan;
        if (!plan.HasFallbackCandidates || Utf8PrefilterExecutor.Rejects(plan.PrefilterPlan, input)) return false;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, regex.MatchTimeout, default);
        invocation.AnchorOriginByte = 0;
        return TryFindNextVerifiedMatch(GetCandidatePlans(plan), regex.Inspection.PreparedRegex,
            input, 0, ref invocation, out _, out _);
    }

    internal static int Count(Utf8Regex regex, ReadOnlySpan<byte> input)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, regex.MatchTimeout, default);
        invocation.AnchorOriginByte = 0;
        var count = 0;
        var position = 0;
        var candidatePlans = GetCandidatePlans(regex.Inspection.SearchPlan);
        while ((uint)position <= (uint)input.Length && TryFindNextVerifiedMatch(candidatePlans,
            regex.Inspection.PreparedRegex, input, position, ref invocation, out _, out var match))
        {
            count++;
            invocation.AnchorOriginByte = match.IndexInBytes + match.LengthInBytes;
            position = match.LengthInBytes == 0 ? Utf8SliceRunner.AdvancePastScalar(input, match.IndexInBytes, false)
                : match.IndexInBytes + match.LengthInBytes;
        }
        return count;
    }
}
