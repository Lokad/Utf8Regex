using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8BackendInstructionExecutor
{
    public static Utf8ValueMatch ProjectLiteralFamilyMatch(
        Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input,
        int[]? alternateLiteralUtf16Lengths,
        int consumedBytes,
        int consumedUtf16,
        in PreparedSearchMatch match,
        ref Utf8SliceInvocation invocation,
        out int nextConsumedBytes,
        out int nextConsumedUtf16)
    {
        return Utf8ProjectionExecutor.ProjectLiteralFamilyMatch(
            program.Projection,
            input,
            alternateLiteralUtf16Lengths,
            consumedBytes,
            consumedUtf16,
            match,
            ref invocation,
            out nextConsumedBytes,
            out nextConsumedUtf16);
    }

    public static Utf8ValueMatch ProjectMatch(
        Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input,
        int consumedBytes,
        int consumedUtf16,
        int matchIndex,
        int matchedByteLength,
        int matchedUtf16Length,
        ref Utf8SliceInvocation invocation,
        out int nextConsumedBytes,
        out int nextConsumedUtf16)
    {
        return Utf8ProjectionExecutor.ProjectMatch(
            program.Projection,
            input,
            consumedBytes,
            consumedUtf16,
            matchIndex,
            matchedByteLength,
            matchedUtf16Length,
            ref invocation,
            out nextConsumedBytes,
            out nextConsumedUtf16);
    }

    public static bool TryFindNextLiteralFamilyMatch(
        in Utf8SearchPlan plan,
        in Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        Utf8ExecutionDeadline budget,
        out PreparedSearchMatch match)
    {
        // Legacy diagnostic callers still supply only a deadline. Each call is
        // bounded; production cursors pass their retained invocation below.
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
        Utf8SearchPlan plan,
        Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget)
    {
        return Utf8SearchPortfolioRuntime.CountLiteralFamily(plan, program, input, budget);
    }

    internal static Utf8ExecutionLimitKind TryFindNextLiteralFamilyMatch(
        in Utf8SearchPlan plan, in Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state,
        ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
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
        Utf8SearchPlan plan,
        Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget,
        bool rightToLeft)
    {
        return Utf8SearchPortfolioRuntime.IsMatchLiteralFamily(plan, program, input, budget, rightToLeft);
    }

    internal static Utf8ExecutionLimitKind TryCountNativeLiteralMatches(in Utf8SearchPlan plan,
        ReadOnlySpan<byte> input, bool reverse, ref Utf8SliceInvocation invocation, out int count)
    {
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

    public static Utf8ValueMatch MatchLiteralFamily(
        Utf8SearchPlan plan,
        Utf8SearchOperationPlan program,
        ReadOnlySpan<byte> input,
        int[]? alternateLiteralUtf16Lengths,
        Utf8ExecutionDeadline budget,
        bool rightToLeft)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        if (rightToLeft || !invocation.IsUnmetered || invocation.CheckpointHook is not null)
        {
            var breach = Utf8SearchExecutor.TryFindLiteralFamily(plan, input, 0, input.Length,
                rightToLeft, ref invocation, out var selected, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal-family execution exceeded {breach}.");
            if (!found) return Utf8ValueMatch.NoMatch;
            return ProjectLiteralFamilyMatch(program, input, alternateLiteralUtf16Lengths,
                0, 0, selected, ref invocation, out _, out _);
        }

        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var kind = TryFindNextLiteralFamilyMatch(plan, program, input, ref state, ref invocation, out var match, out var exists);
        if (kind != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal-family execution exceeded {kind}.");
        if (!exists)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return ProjectLiteralFamilyMatch(program, input, alternateLiteralUtf16Lengths, 0, 0, match, ref invocation, out _, out _);
    }

    public static bool TryFindNextFallbackVerifiedMatch(
        Utf8SearchPlan plan,
        Utf8SearchOperationPlan program,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult verification)
    {
        _ = program;
        verification = default;
        if (plan.FallbackSearch.CandidatePlans is not { Length: > 0 } candidatePlans)
        {
            return false;
        }

        using var cursor = new Utf8CandidatePortfolioCursor(candidatePlans, input, startIndex);
        while (cursor.TryGetNextScalarBoundary(out var candidate))
        {
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (verifierRuntime.FallbackCandidateVerifier.TryVerify(input, candidate, validation, ref boundaryMap, ref decoded, out verification))
            {
                return true;
            }

            cursor.AdvancePast(candidate.StartIndex + 1);
        }

        return false;
    }

    public static bool TryFindNextCompiledFallbackMatch(
        Utf8SearchPlan plan,
        Utf8SearchOperationPlan program,
        Utf8VerifierRuntime verifierRuntime,
        Utf8ExecutionProgram executionProgram,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        Utf8ExecutionDeadline budget,
        out Utf8ValueMatch match)
    {
        _ = program;
        match = Utf8ValueMatch.NoMatch;
        if (plan.FallbackSearch.CandidatePlans is not { Length: > 0 } candidatePlans)
        {
            return false;
        }

        using var cursor = new Utf8CandidatePortfolioCursor(candidatePlans, input, startIndex);
        while (cursor.TryGetNextScalarBoundary(out var candidate))
        {
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (Utf8ExecutionInterpreter.TryMatchPrefix(input, executionProgram, candidate.StartIndex, captures: null, budget, out var matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                match = Utf8ProjectionExecutor.ProjectByteAlignedMatch(candidate.StartIndex, matchedLength);
                return true;
            }

            if (verifierRuntime.FallbackCandidateVerifier.TryVerify(input, candidate, validation, ref boundaryMap, ref decoded, out var verification))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                match = Utf8ProjectionExecutor.ProjectFallbackVerification(verification);
                return true;
            }

            cursor.AdvancePast(candidate.StartIndex + 1);
        }

        return false;
    }

    public static bool IsMatchStructuralIdentifierFamily(
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget)
    {
        return Utf8AsciiStructuralIdentifierFamilyExecutor.FindNext(
            input,
            regexPlan.StructuralIdentifierFamilyPlan,
            regexPlan.SearchPlan,
            regexPlan.StructuralSearchPlan,
            verifierRuntime.StructuralVerifierRuntime,
            0,
            budget,
            out _) >= 0;
    }

    public static int CountStructuralIdentifierFamily(
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget)
    {
        return Utf8AsciiStructuralIdentifierFamilyExecutor.Count(
            input,
            regexPlan.StructuralIdentifierFamilyPlan,
            regexPlan.SearchPlan,
            regexPlan.StructuralSearchPlan,
            verifierRuntime.StructuralVerifierRuntime,
            budget);
    }

    public static Utf8ValueMatch MatchStructuralIdentifierFamily(
        Utf8PreparedRegex regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget)
    {
        var index = Utf8AsciiStructuralIdentifierFamilyExecutor.FindNext(
            input,
            regexPlan.StructuralIdentifierFamilyPlan,
            regexPlan.SearchPlan,
            regexPlan.StructuralSearchPlan,
            verifierRuntime.StructuralVerifierRuntime,
            0,
            budget,
            out var matchedLength);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : Utf8ProjectionExecutor.ProjectByteAlignedMatch(index, matchedLength);
    }

    public static bool IsMatchOrderedLiteralWindow(
        Utf8PreparedRegex regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget)
    {
        return AsciiOrderedLiteralWindowExecutor.FindNext(
            input,
            regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan,
            regexPlan.SearchPlan,
            0,
            budget,
            out _) >= 0;
    }

    public static int CountOrderedLiteralWindow(
        Utf8PreparedRegex regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget)
    {
        return AsciiOrderedLiteralWindowExecutor.Count(
            input,
            regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan,
            regexPlan.SearchPlan,
            budget);
    }

    public static Utf8ValueMatch MatchOrderedLiteralWindow(
        Utf8PreparedRegex regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget)
    {
        var index = AsciiOrderedLiteralWindowExecutor.FindNext(
            input,
            regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan,
            regexPlan.SearchPlan,
            0,
            budget,
            out var matchedLength);
        return index < 0
            ? Utf8ValueMatch.NoMatch
            : Utf8ProjectionExecutor.ProjectByteAlignedMatch(index, matchedLength);
    }

}
