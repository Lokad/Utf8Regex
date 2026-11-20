using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8FallbackReplay
{
    private static bool TryFindNextVerifiedMatch(
        Utf8StructuralSearchPlan plan,
        Utf8PreparedRegex prepared,
        ReadOnlySpan<byte> input,
        int startIndex,
        ref Utf8SliceInvocation invocation,
        out Utf8ScalarVerificationResult verification)
    {
        verification = default;
        var entryBreach = invocation.TryChargeInputStep();
        if (entryBreach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Scalar candidate search exceeded {entryBreach}.");
        if (!plan.HasValue || (uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        var state = new PreparedSearchScanState(startIndex, default);
        var minimumStart = startIndex;
        while (plan.TryFindNextCandidate(input, ref state, out var candidate))
        {
            if (candidate.StartIndex < minimumStart || !Utf8CandidateVerificationReplay.IsBoundary(input, candidate.StartIndex))
                continue;

            var breach = Utf8CandidateVerificationReplay.TryVerifyCandidate(prepared, input, candidate, ref invocation, out verification);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Scalar candidate verification exceeded {breach}.");
            if (verification.Success)
            {
                return true;
            }

            minimumStart = candidate.StartIndex + 1;
        }

        return false;
    }

    public static int CountCandidates(Utf8SearchPlan searchPlan, ReadOnlySpan<byte> input, bool requireScalarBoundary)
    {
        var plan = searchPlan.StructuralSearchPlan;
        var state = default(PreparedSearchScanState);
        var count = 0;
        while (plan.TryFindNextCandidate(input, ref state, out var candidate))
        {
            if (!requireScalarBoundary || Utf8CandidateVerificationReplay.IsBoundary(input, candidate.StartIndex))
                count++;
        }

        return count;
    }

    internal static bool IsMatch(Utf8Regex regex, ReadOnlySpan<byte> input)
    {
        var plan = regex.Inspection.SearchPlan;
        if (!plan.HasStructuralCandidates || Utf8PrefilterReplay.Rejects(plan, input)) return false;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, regex.MatchTimeout, default);
        invocation.AnchorOriginByte = 0;
        return TryFindNextVerifiedMatch(plan.StructuralSearchPlan, regex.Inspection.PreparedRegex,
            input, 0, ref invocation, out _);
    }

    internal static int Count(Utf8Regex regex, ReadOnlySpan<byte> input)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, regex.MatchTimeout, default);
        invocation.AnchorOriginByte = 0;
        var count = 0;
        var position = 0;
        var plan = regex.Inspection.StructuralSearchPlan;
        while ((uint)position <= (uint)input.Length && TryFindNextVerifiedMatch(plan,
            regex.Inspection.PreparedRegex, input, position, ref invocation, out var match))
        {
            count++;
            invocation.AnchorOriginByte = match.IndexInBytes + match.LengthInBytes;
            position = match.LengthInBytes == 0 ? Utf8SliceRunner.AdvancePastScalar(input, match.IndexInBytes, false)
                : match.IndexInBytes + match.LengthInBytes;
        }
        return count;
    }
}
