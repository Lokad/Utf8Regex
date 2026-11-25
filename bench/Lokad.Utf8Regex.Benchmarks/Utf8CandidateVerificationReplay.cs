using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8CandidateVerificationReplay
{
    // The subject is caller-valid UTF-8. Candidate ends can be conservative
    // byte bounds inside a scalar; they never shorten the assertion/matching context.
    // Scratch belongs to the caller's invocation and is released on every exit.
    internal static Utf8ExecutionLimitKind TryVerifyCandidate(
        Utf8PreparedRegex prepared, ReadOnlySpan<byte> input,
        PreparedSearchMatch candidate,
        ref Utf8SliceInvocation invocation, out Utf8ScalarVerificationResult result)
    {
        result = default;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var program = prepared.ScalarProgram ??
            throw new InvalidOperationException("Candidate verification requires the prepared scalar program.");
        var plan = prepared.SearchPlan;
        var exactLength = plan.StructuralSearchPlan.Payload.ExactLength;
        var end = GetCandidateEnd(candidate.Index, input.Length, exactLength, plan.MaxPossibleLength.GetValueOrDefault());
        var attempt = program.RightToLeft && end >= 0 ? end : candidate.Index;
        if (!IsBoundary(input, candidate.Index) || !IsBoundary(input, attempt))
            return Utf8ExecutionLimitKind.None;

        var bytes = checked(2 * Utf8ConstructionBudget.ArrayBytes<int>(program.GroupCount));
        breach = invocation.TryReserveWorking(bytes);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        int[]? starts = null;
        int[]? ends = null;
        try
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            starts = program.GroupCount == 0 ? [] : Utf8ExactStatePool<int>.Rent(program.GroupCount, invocation.HasUnlimitedWorkingMemory);
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            ends = program.GroupCount == 0 ? [] : Utf8ExactStatePool<int>.Rent(program.GroupCount, invocation.HasUnlimitedWorkingMemory);
            var match = Utf8SliceRunner.TryMatchAt(program, input, attempt, starts, ends,
                startIsScalarBoundary: true, ref invocation);
            if (match.Breach != Utf8ExecutionLimitKind.None) return match.Breach;
            if (!match.IsMatch) return Utf8ExecutionLimitKind.None;
            var low = program.RightToLeft ? match.EndIndex : attempt;
            var high = program.RightToLeft ? attempt : match.EndIndex;
            if (!IsBoundary(input, low) || !IsBoundary(input, high) || high < low ||
                program.RightToLeft && end >= 0 && low != candidate.Index)
                return Utf8ExecutionLimitKind.None;

            if (exactLength > 0 && end >= 0 && high < end)
                return Utf8ExecutionLimitKind.None;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            result = new(true, low, high - low);
            return Utf8ExecutionLimitKind.None;
        }
        finally
        {
            if (starts is not null) Utf8ExactStatePool<int>.Return(starts, invocation.HasUnlimitedWorkingMemory);
            if (ends is not null) Utf8ExactStatePool<int>.Return(ends, invocation.HasUnlimitedWorkingMemory);
            invocation.Ledger.Release(bytes);
        }
    }

    private static int GetCandidateEnd(int start, int inputLength, int exactLength, int maxLength)
    {
        var available = inputLength - start;
        if (exactLength > 0) return exactLength <= available ? start + exactLength : -1;
        return maxLength > 0 ? start + Math.Min(maxLength, available) : -1;
    }

    internal static bool IsBoundary(ReadOnlySpan<byte> input, int offset)
        => (uint)offset <= (uint)input.Length &&
            (offset == input.Length || (input[offset] & 0xC0) != 0x80);
}

internal readonly record struct Utf8ScalarVerificationResult(bool Success, int IndexInBytes, int LengthInBytes);
