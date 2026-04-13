using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ByteSafeLinearExecutor
{
    public static bool CanExecute(Utf8RegexPlan regexPlan)
    {
        return regexPlan.ExecutionTree is not null &&
            HasCompiledByteSafeVerifier(regexPlan.StructuralVerifier) &&
            HasCandidateSource(regexPlan);
    }

    private static bool HasCompiledByteSafeVerifier(Utf8StructuralVerifierPlan plan)
    {
        return plan.ByteSafeLazyDfaProgram.HasValue || plan.ByteSafeLinearProgram.HasValue;
    }

    public static bool HasCandidateSource(Utf8RegexPlan regexPlan)
    {
        return regexPlan.DeterministicAnchor.HasValue ||
            HasSelectiveStructuralStartPlan(regexPlan.StructuralSearchPlan);
    }

    public static int FindNext(
        ReadOnlySpan<byte> input,
        Utf8RegexPlan regexPlan,
        Utf8StructuralVerifierRuntime verifierRuntime,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;

        if (regexPlan.DeterministicAnchor.HasValue)
        {
            var anchor = regexPlan.DeterministicAnchor;
            for (var candidate = FindNextAnchorCandidate(input, anchor, startIndex);
                 candidate >= 0;
                 candidate = FindNextAnchorCandidate(input, anchor, candidate + 1))
            {
                var matchStart = candidate - anchor.Offset;
                if ((uint)matchStart > (uint)input.Length)
                {
                    continue;
                }

                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                if (verifierRuntime is Utf8ByteSafeLinearVerifierRuntime byteSafeVerifier &&
                    byteSafeVerifier.Plan.ByteSafeGuards.HasValue &&
                    !byteSafeVerifier.Plan.ByteSafeGuards.Matches(input, matchStart))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountFixedCheckReject();
                    continue;
                }

                budget?.Step(input);
                Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
                if (verifierRuntime.TryMatch(input, matchStart, 0, budget, out matchedLength))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                    return matchStart;
                }
            }

            return -1;
        }

        if (!HasSelectiveStructuralStartPlan(regexPlan.StructuralSearchPlan))
        {
            return -1;
        }

        var state = CreateCandidateState(startIndex);
        while (regexPlan.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate))
        {
            if (!IsScalarBoundaryByteOffset(input, candidate.StartIndex))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (verifierRuntime.TryMatch(input, candidate.StartIndex, 0, budget, out matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return candidate.StartIndex;
            }
        }

        return -1;
    }

    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        Utf8RegexPlan regexPlan,
        Utf8StructuralVerifierRuntime verifierRuntime,
        Utf8ExecutionBudget? budget)
    {
        return FindNext(input, regexPlan, verifierRuntime, 0, budget, out _) >= 0;
    }

    public static int Count(
        ReadOnlySpan<byte> input,
        Utf8RegexPlan regexPlan,
        Utf8StructuralVerifierRuntime verifierRuntime,
        Utf8ExecutionBudget? budget)
    {
        if (regexPlan.DeterministicAnchor.HasValue)
        {
            return CountWithDeterministicAnchor(input, regexPlan.DeterministicAnchor, verifierRuntime, budget);
        }

        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, regexPlan, verifierRuntime, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountWithDeterministicAnchor(
        ReadOnlySpan<byte> input,
        Utf8DeterministicAnchorSearch anchor,
        Utf8StructuralVerifierRuntime verifierRuntime,
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var state = new PreparedSearchScanState(0, default);
        while (anchor.Searcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            var matchStart = match.Index - anchor.Offset;
            if ((uint)matchStart > (uint)input.Length)
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            if (verifierRuntime is Utf8ByteSafeLinearVerifierRuntime byteSafeVerifier &&
                byteSafeVerifier.Plan.ByteSafeGuards.HasValue &&
                !byteSafeVerifier.Plan.ByteSafeGuards.Matches(input, matchStart))
            {
                Utf8SearchDiagnosticsSession.Current?.CountFixedCheckReject();
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!verifierRuntime.TryMatch(input, matchStart, 0, budget, out var matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            state = CreateResumedAnchorState(anchor.Searcher, matchStart + Math.Max(matchedLength, 1));
        }

        return count;
    }

    private static int FindNextAnchorCandidate(ReadOnlySpan<byte> input, Utf8DeterministicAnchorSearch anchor, int startIndex)
    {
        if ((uint)startIndex >= (uint)input.Length)
        {
            return -1;
        }

        var relative = anchor.Searcher.FindFirst(input[startIndex..]);
        return relative < 0 ? -1 : startIndex + relative;
    }

    private static PreparedSearchScanState CreateResumedAnchorState(PreparedSearcher searcher, int nextStart)
    {
        return searcher.Kind == PreparedSearcherKind.MultiLiteral
            ? new PreparedSearchScanState(nextStart, new PreparedMultiLiteralScanState(nextStart, nextStart, 0))
            : new PreparedSearchScanState(nextStart, default);
    }

    private static Utf8StructuralSearchState CreateCandidateState(int startIndex)
    {
        return new Utf8StructuralSearchState(
            new PreparedSearchScanState(startIndex, default),
            new PreparedWindowScanState(startIndex, new PreparedSearchScanState(startIndex, default)));
    }

    private static bool IsScalarBoundaryByteOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        return (uint)byteOffset <= (uint)input.Length &&
            (byteOffset == 0 || byteOffset == input.Length || (input[byteOffset] & 0xC0) != 0x80);
    }

    private static bool HasSelectiveStructuralStartPlan(Utf8StructuralSearchPlan plan)
    {
        if (!(plan.HasValue && plan.YieldKind == Utf8StructuralSearchYieldKind.Start) ||
            plan.Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        foreach (var stage in stages)
        {
            if (stage.Kind == Utf8StructuralSearchStageKind.TransformCandidateStart)
            {
                return true;
            }
        }

        return false;
    }
}

internal static class Utf8ByteSafeInterpreterExecutor
{
    public static bool CanExecute(Utf8RegexPlan regexPlan) => Utf8ByteSafeLinearExecutor.CanExecute(regexPlan);

    public static int FindNext(
        ReadOnlySpan<byte> input,
        Utf8RegexPlan regexPlan,
        Utf8StructuralVerifierRuntime verifierRuntime,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        return Utf8ByteSafeLinearExecutor.FindNext(input, regexPlan, verifierRuntime, startIndex, budget, out matchedLength);
    }

    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        Utf8RegexPlan regexPlan,
        Utf8StructuralVerifierRuntime verifierRuntime,
        Utf8ExecutionBudget? budget)
    {
        return Utf8ByteSafeLinearExecutor.IsMatch(input, regexPlan, verifierRuntime, budget);
    }

    public static int Count(
        ReadOnlySpan<byte> input,
        Utf8RegexPlan regexPlan,
        Utf8StructuralVerifierRuntime verifierRuntime,
        Utf8ExecutionBudget? budget)
    {
        return Utf8ByteSafeLinearExecutor.Count(input, regexPlan, verifierRuntime, budget);
    }
}
