using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed partial class Utf8VerifierRuntime
{
    // The subject is already strictly validated. Candidate ends can be conservative
    // byte bounds inside a scalar; they never shorten the assertion/matching context.
    // Scratch belongs to the caller's invocation and is released on every exit.
    internal Utf8ExecutionLimitKind TryVerifyCandidate(ReadOnlySpan<byte> input,
        Utf8StructuralCandidate candidate, Utf8ValidationResult validation,
        ref Utf8SliceInvocation invocation, out Utf8ScalarVerificationResult result)
    {
        result = default;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var program = _preparedRegex.ScalarProgram ??
            throw new InvalidOperationException("Candidate verification requires the prepared scalar program.");
        var attempt = program.RightToLeft && candidate.EndIndex >= 0
            ? candidate.EndIndex : candidate.StartIndex;
        if (!IsBoundary(input, candidate.StartIndex) || !IsBoundary(input, attempt))
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
            starts = program.GroupCount == 0 ? [] : Utf8ExactStatePool<int>.Rent(program.GroupCount, invocation.Ledger.IsUnlimited);
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            ends = program.GroupCount == 0 ? [] : Utf8ExactStatePool<int>.Rent(program.GroupCount, invocation.Ledger.IsUnlimited);
            var match = Utf8SliceRunner.TryMatchAt(program, input, attempt, starts, ends,
                startIsScalarBoundary: true, ref invocation);
            if (match.Breach != Utf8ExecutionLimitKind.None) return match.Breach;
            if (!match.IsMatch) return Utf8ExecutionLimitKind.None;
            var low = program.RightToLeft ? match.EndIndex : attempt;
            var high = program.RightToLeft ? attempt : match.EndIndex;
            if (!IsBoundary(input, low) || !IsBoundary(input, high) || high < low ||
                program.RightToLeft && candidate.EndIndex >= 0 && low != candidate.StartIndex)
                return Utf8ExecutionLimitKind.None;

            var plan = _preparedRegex.ScalarVerifier;
            if (plan.RequiresCandidateEndCoverage && candidate.EndIndex >= 0 && high < candidate.EndIndex)
                return Utf8ExecutionLimitKind.None;
            if (plan.RequiresTrailingAnchorCoverage)
            {
                var trailingEnd = (long)candidate.TrailingIndex + candidate.TrailingMatchLength;
                if (candidate.TrailingMatchLength <= 0 || candidate.TrailingIndex < low ||
                    trailingEnd > high || !IsBoundary(input, candidate.TrailingIndex) ||
                    !IsBoundary(input, (int)trailingEnd)) return Utf8ExecutionLimitKind.None;
            }

            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            result = new(true, low, high - low);
            return Utf8ExecutionLimitKind.None;
        }
        finally
        {
            if (starts is not null) Utf8ExactStatePool<int>.Return(starts, invocation.Ledger.IsUnlimited);
            if (ends is not null) Utf8ExactStatePool<int>.Return(ends, invocation.Ledger.IsUnlimited);
            invocation.Ledger.Release(bytes);
        }
    }

    private static bool IsBoundary(ReadOnlySpan<byte> input, int offset)
        => (uint)offset <= (uint)input.Length &&
            (offset == input.Length || (input[offset] & 0xC0) != 0x80);
}

internal readonly record struct Utf8ScalarVerificationResult(bool Success, int IndexInBytes, int LengthInBytes);
