using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8BackendInstructionExecutor
{

    public static bool TryFindNextLiteralFamilyMatch(
        in Utf8SearchPlan plan, in Utf8SearchOperationPlan program, ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state, Utf8ExecutionDeadline budget, out PreparedSearchMatch match)
    {
        // Unmetered production cursors and friend diagnostics share this search.
        // Controlled cursors keep their retained invocation in the overload below.
        if (!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            var breach = TryFindNextLiteralFamilyMatch(plan, program, input, ref state,
                ref invocation, out match, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal-family execution exceeded {breach}.");
            return found;
        }
        if (plan.HasAlternateLiteralPrefixOverlap || plan.HasAlternateLiteralProperStartOverlap)
        {
            return Utf8SearchPortfolioRuntime.TryFindNextOverlappingLiteralFamilyMatch(
                in plan,
                in program,
                input,
                ref state,
                budget,
                out match);
        }

        return Utf8SearchPortfolioRuntime.TryFindNextLiteralFamilyMatch(
            in plan,
            in program,
            input,
            ref state,
            budget,
            out match);
    }

    public static int CountLiteralFamily(
        Utf8SearchPlan plan, Utf8SearchOperationPlan program, ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return Utf8SearchPortfolioRuntime.CountLiteralFamily(plan, program, input, budget);
    }

    internal static Utf8ExecutionLimitKind TryFindNextLiteralFamilyMatch(
        in Utf8SearchPlan plan, in Utf8SearchOperationPlan program, ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
    {
        match = default;
        found = false;
        var pendingState = state;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            found = TryFindNextLiteralFamilyMatch(plan, program, input, ref pendingState,
                invocation.Budget.Deadline, out match);
        }
        else
        {
            var breach = Utf8SearchExecutor.TryFindLiteralFamily(plan, input, state.NextStart,
                input.Length, false, ref invocation, out match, out found);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var next = found ? match.Index + match.Length : input.Length;
            pendingState = new PreparedMultiLiteralScanState(next, next, 0);
        }
        state = pendingState;
        return Utf8ExecutionLimitKind.None;
    }

    public static bool IsMatchLiteralFamily(
        Utf8SearchPlan plan, Utf8SearchOperationPlan program, ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        return Utf8SearchPortfolioRuntime.IsMatchLiteralFamily(plan, program, input, budget, rightToLeft);
    }

    internal static Utf8ExecutionLimitKind TryCountNativeLiteralMatches(in Utf8SearchPlan plan, ReadOnlySpan<byte> input, bool reverse, ref Utf8SliceInvocation invocation, out int count)
    {
        if (!reverse && input.Length >= 128 && plan.BoundaryFamilyCounter is { } boundaryCounter)
            return boundaryCounter.TryCountWithBoundaries(input, ref invocation, out count);
        if (!reverse && !invocation.IsUnmetered &&
            plan.AlternateLiteralsUtf8 is not { Length: > 0 } &&
            plan.LiteralSearch is { Length: > 0 } search &&
            !plan.HasBoundaryRequirements && !plan.HasTrailingLiteralRequirement)
        {
            return TryCountSingleLiteral(search, input, ref invocation, out count, out _);
        }
        count = 0;
        var total = 0;
        var low = 0;
        var high = input.Length;
        while (true)
        {
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(plan, input, low, high, reverse,
                ref invocation, out var match, out var found);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!found) { count = total; return Utf8ExecutionLimitKind.None; }
            total++;
            if (reverse) high = match.Index;
            else low = match.Index + match.Length;
        }
    }

    // Count has no range to publish. On a folded alias, keep completed
    // matches and return the earliest unresolved start to the scalar VM.
    internal static Utf8ExecutionLimitKind TryCountSingleLiteral(in PreparedSubstringSearch search,
        ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation, out int count, out int resume,
        bool stopAtKelvin = false)
    {
        count = 0;
        resume = -1;
        var foundCount = 0;
        var start = 0;
        var last = input.Length - search.Length;
        while (start <= (stopAtKelvin ? input.Length - 1 : last))
        {
            var breach = Utf8SearchExecutor.TryFindLiteralCandidate(search, input, start, last,
                false, ref invocation, out var candidate, stopAtKelvin);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (candidate < 0) break;
            if (stopAtKelvin && input[candidate] == "K"u8[0])
            {
                // An earlier ASCII comparison can have failed where this
                // first alias starts. Keep that overlap without replaying
                // starts consumed by a completed match.
                resume = Math.Max(start, candidate - search.Length + 1);
                while ((input[resume] & 0xC0) == 0x80) resume++;
                break;
            }
            foundCount++;
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            start = candidate + search.Length;
        }
        var terminal = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (terminal == Utf8ExecutionLimitKind.None) count = foundCount;
        return terminal;
    }

    public static Utf8CaptureRange MatchLiteralFamily(
        Utf8SearchPlan plan, Utf8SearchOperationPlan program, ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        if (rightToLeft || !invocation.IsUnmetered || invocation.CheckpointHook is not null)
        {
            var breach = Utf8SearchExecutor.TryFindLiteralFamily(plan, input, 0, input.Length,
                rightToLeft, ref invocation, out var selected, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal-family execution exceeded {breach}.");
            if (!found) return default(Utf8CaptureRange);
            return Utf8SliceBufferWork.CreateMatchRange(selected.Index, (selected.Index) + (selected.Length), ref invocation);
        }

        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var kind = TryFindNextLiteralFamilyMatch(plan, program, input, ref state, ref invocation, out var match, out var exists);
        if (kind != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal-family execution exceeded {kind}.");
        if (!exists)
        {
            return default(Utf8CaptureRange);
        }

        return Utf8SliceBufferWork.CreateMatchRange(match.Index, (match.Index) + (match.Length), ref invocation);
    }

}
