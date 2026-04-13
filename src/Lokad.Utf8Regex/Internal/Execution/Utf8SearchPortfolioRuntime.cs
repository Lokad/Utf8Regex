using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchPortfolioRuntime
{
    private const int LargeAutomatonEarliestHybridChunkBytes = 512;
    private const int LargeAutomatonEarliestHybridNegativeProbeBytes = 2048;
    private const int LargeAutomatonHybridDemotionWindowCount = 16;
    private const int LargeAutomatonHybridMinSkippedWindows = 4;

    public static bool TryFindNextLiteralFamilyMatch(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        Utf8ExecutionBudget? budget,
        out PreparedSearchMatch match)
    {
        match = default;
        if (!plan.NativeSearch.HasPreparedSearcher)
        {
            return false;
        }

        while (true)
        {
            budget?.Step(input);
            if (!plan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out match))
            {
                return false;
            }

            if (program.Confirmation.Kind == Utf8ConfirmationKind.None &&
                program.Strategy.Kind is Utf8SearchMetaStrategyKind.DirectSearch or Utf8SearchMetaStrategyKind.HybridSearch)
            {
                return true;
            }

            if (Utf8ConfirmationExecutor.IsMatch(plan, program.Confirmation, input, match.Index, match.Length))
            {
                return true;
            }
        }
    }

    public static bool IsMatchLiteralFamily(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget,
        bool rightToLeft)
    {
        if (!plan.NativeSearch.HasPreparedSearcher)
        {
            return false;
        }

        if (rightToLeft)
        {
            budget?.Step(input);
            return Utf8SearchExecutor.TryFindLastMatch(plan, input, input.Length, out _);
        }

        if (program.Confirmation.Kind == Utf8ConfirmationKind.None &&
            program.Strategy.Kind is Utf8SearchMetaStrategyKind.DirectSearch or Utf8SearchMetaStrategyKind.HybridSearch)
        {
            budget?.Step(input);
            return plan.NativeSearch.PreparedSearcher.TryFindFirstMatch(input, out _);
        }

        var state = new PreparedSearchScanState(0, default);
        while (true)
        {
            budget?.Step(input);
            if (!plan.NativeSearch.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
            {
                return false;
            }

            if (Utf8ConfirmationExecutor.IsMatch(plan, program.Confirmation, input, match.Index, match.Length))
            {
                return true;
            }
        }
    }

    public static int CountLiteralFamily(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget)
    {
        if (!plan.NativeSearch.HasPreparedSearcher)
        {
            return 0;
        }

        if (budget is null &&
            program.Confirmation.Kind == Utf8ConfirmationKind.None &&
            program.Strategy.Kind == Utf8SearchMetaStrategyKind.HybridSearch &&
            plan.MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("literal_family_hybrid_earliest_automaton");
            return CountLargeAutomatonFamilyViaEarliestHybrid(plan.MultiLiteralSearch, input, program.Strategy.ObservabilityKind);
        }

        if (program.Confirmation.Kind == Utf8ConfirmationKind.None)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("literal_family_prepared_non_overlapping");
            var count = 0;
            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            while (true)
            {
                budget?.Step(input);
                if (!plan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out _, out _))
                {
                    return count;
                }

                count++;
            }
        }

        if (plan.NativeSearch.PreparedSearcher.Kind == PreparedSearcherKind.MultiLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("literal_family_prepared_with_requirements_length_only");
            return budget is null
                ? CountPreparedWithRequirementsLengthOnlyNoBudget(plan, program.Confirmation, input)
                : CountPreparedWithRequirementsLengthOnly(input, plan, program.Confirmation, budget);
        }

        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("literal_family_prepared_with_requirements_match");
        return budget is null
            ? CountPreparedWithRequirementsNoBudget(plan, program.Confirmation, input)
            : CountPreparedWithRequirements(input, plan, program.Confirmation, budget);
    }

    private static int CountPreparedWithRequirements(ReadOnlySpan<byte> input, Utf8SearchPlan plan, Utf8ConfirmationPlan confirmation, Utf8ExecutionBudget budget)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (true)
        {
            budget.Step(input);
            if (!plan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out var match))
            {
                return count;
            }

            if (Utf8ConfirmationExecutor.IsMatch(plan, confirmation, input, match.Index, match.Length))
            {
                count++;
            }
        }
    }

    private static int CountPreparedWithRequirementsLengthOnly(ReadOnlySpan<byte> input, Utf8SearchPlan plan, Utf8ConfirmationPlan confirmation, Utf8ExecutionBudget budget)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (true)
        {
            budget.Step(input);
            if (!plan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var index, out var matchedLength))
            {
                return count;
            }

            if (Utf8ConfirmationExecutor.IsMatch(plan, confirmation, input, index, matchedLength))
            {
                count++;
            }
        }
    }

    private static int CountPreparedWithRequirementsLengthOnlyNoBudget(Utf8SearchPlan plan, Utf8ConfirmationPlan confirmation, ReadOnlySpan<byte> input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (true)
        {
            if (!plan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var index, out var matchedLength))
            {
                return count;
            }

            if (Utf8ConfirmationExecutor.IsMatch(plan, confirmation, input, index, matchedLength))
            {
                count++;
            }
        }
    }

    private static int CountPreparedWithRequirementsNoBudget(Utf8SearchPlan plan, Utf8ConfirmationPlan confirmation, ReadOnlySpan<byte> input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (true)
        {
            if (!plan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out var match))
            {
                return count;
            }

            if (Utf8ConfirmationExecutor.IsMatch(plan, confirmation, input, match.Index, match.Length))
            {
                count++;
            }
        }
    }

    private static int CountLargeAutomatonFamilyViaEarliestHybrid(
        PreparedMultiLiteralSearch search,
        ReadOnlySpan<byte> input,
        Utf8SearchObservabilityKind observabilityKind)
    {
        var earliest = search.EarliestSearch;
        var automaton = search.AutomatonSearch;
        if (earliest.ShortestLength == int.MaxValue || earliest.LongestLength == 0)
        {
            return 0;
        }

        var negativeProbeLength = Math.Min(input.Length, LargeAutomatonEarliestHybridNegativeProbeBytes);
        if (negativeProbeLength >= earliest.ShortestLength)
        {
            var initialProbe = input[..negativeProbeLength];
            var initialState = new PreparedMultiLiteralScanState(0, 0, 0);
            if (!earliest.TryFindNextNonOverlappingLength(initialProbe, ref initialState, out _, out _))
            {
                if (observabilityKind == Utf8SearchObservabilityKind.Effectiveness)
                {
                    Utf8SearchDiagnosticsSession.Current?.CountPrefilterWindow(promoted: false, input.Length);
                }

                return CountLargeAutomatonFamilyViaEarliest(search, input);
            }
        }

        var count = 0;
        var position = 0;
        var windows = 0;
        var skippedWindows = 0;
        while (position <= input.Length - earliest.ShortestLength)
        {
            var chunkEnd = Math.Min(input.Length, position + LargeAutomatonEarliestHybridChunkBytes);
            var probeEnd = Math.Min(input.Length, chunkEnd + earliest.LongestLength - 1);
            var probeWindow = input[position..probeEnd];
            var chunkByteCount = chunkEnd - position;
            var probeState = new PreparedMultiLiteralScanState(0, 0, 0);
            if (!earliest.TryFindNextNonOverlappingLength(probeWindow, ref probeState, out var probeIndex, out _) ||
                probeIndex >= chunkEnd - position)
            {
                if (observabilityKind == Utf8SearchObservabilityKind.Effectiveness)
                {
                    Utf8SearchDiagnosticsSession.Current?.CountPrefilterWindow(promoted: false, chunkByteCount);
                }
                windows++;
                skippedWindows++;
                position = chunkEnd;
                continue;
            }

            if (observabilityKind == Utf8SearchObservabilityKind.Effectiveness)
            {
                Utf8SearchDiagnosticsSession.Current?.CountPrefilterWindow(promoted: true, chunkByteCount);
            }
            windows++;
            if (windows >= LargeAutomatonHybridDemotionWindowCount &&
                skippedWindows < LargeAutomatonHybridMinSkippedWindows)
            {
                if (observabilityKind == Utf8SearchObservabilityKind.Effectiveness)
                {
                    Utf8SearchDiagnosticsSession.Current?.CountEngineDemotion();
                }
                return count + CountLargeAutomatonFamilyViaAutomatonFrom(search, input, position);
            }

            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            var nextPosition = chunkEnd;
            while (automaton.TryFindNextNonOverlappingMatch(probeWindow, ref state, out var matchIndex, out _, out _))
            {
                if (matchIndex >= chunkEnd - position)
                {
                    nextPosition = position + matchIndex;
                    break;
                }

                count++;
                nextPosition = Math.Max(chunkEnd, position + state.NextStart);
            }

            position = nextPosition;
        }

        return count;
    }

    private static int CountLargeAutomatonFamilyViaEarliest(PreparedMultiLiteralSearch search, ReadOnlySpan<byte> input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (search.EarliestSearch.TryFindNextNonOverlappingLength(input, ref state, out _, out _))
        {
            count++;
        }

        return count;
    }

    private static int CountLargeAutomatonFamilyViaAutomatonFrom(PreparedMultiLiteralSearch search, ReadOnlySpan<byte> input, int position)
    {
        if ((uint)position >= (uint)input.Length)
        {
            return 0;
        }

        var count = 0;
        var state = new PreparedMultiLiteralScanState(position, position, 0);
        while (search.AutomatonSearch.TryFindNextNonOverlappingMatch(input, ref state, out _, out _, out _))
        {
            count++;
        }

        return count;
    }

}
