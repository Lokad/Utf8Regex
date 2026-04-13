using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8BackendInstructionExecutor
{
    public static Utf8ValueMatch ProjectLiteralFamilyMatch(
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        int[]? alternateLiteralUtf16Lengths,
        int consumedBytes,
        int consumedUtf16,
        in PreparedSearchMatch match,
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
            out nextConsumedBytes,
            out nextConsumedUtf16);
    }

    public static Utf8ValueMatch ProjectMatch(
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        int consumedBytes,
        int consumedUtf16,
        int matchIndex,
        int matchedByteLength,
        int matchedUtf16Length,
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
            out nextConsumedBytes,
            out nextConsumedUtf16);
    }

    public static bool TryFindNextLiteralFamilyMatch(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        Utf8ExecutionBudget? budget,
        out PreparedSearchMatch match)
    {
        return Utf8SearchPortfolioRuntime.TryFindNextLiteralFamilyMatch(
            plan,
            program,
            input,
            ref state,
            budget,
            out match);
    }

    public static int CountLiteralFamily(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget)
    {
        return Utf8SearchPortfolioRuntime.CountLiteralFamily(plan, program, input, budget);
    }

    public static bool IsMatchLiteralFamily(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget,
        bool rightToLeft)
    {
        return Utf8SearchPortfolioRuntime.IsMatchLiteralFamily(plan, program, input, budget, rightToLeft);
    }

    public static Utf8ValueMatch MatchLiteralFamily(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
        ReadOnlySpan<byte> input,
        int[]? alternateLiteralUtf16Lengths,
        Utf8ExecutionBudget? budget,
        bool rightToLeft)
    {
        if (rightToLeft)
        {
            budget?.Step(input);
            if (!Utf8SearchExecutor.TryFindLastMatch(plan, input, input.Length, out var rightToLeftMatch))
            {
                return Utf8ValueMatch.NoMatch;
            }

            var utf16Lengths = alternateLiteralUtf16Lengths;
            var matchedUtf16Length = utf16Lengths is { Length: > 0 } &&
                (uint)rightToLeftMatch.LiteralId < (uint)utf16Lengths.Length
                ? utf16Lengths[rightToLeftMatch.LiteralId]
                : Utf8Validation.Validate(input.Slice(rightToLeftMatch.Index, rightToLeftMatch.Length)).Utf16Length;

            return ProjectMatch(
                program,
                input,
                0,
                0,
                rightToLeftMatch.Index,
                rightToLeftMatch.Length,
                matchedUtf16Length,
                out _,
                out _);
        }

        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        if (!TryFindNextLiteralFamilyMatch(plan, program, input, ref state, budget, out var match))
        {
            return Utf8ValueMatch.NoMatch;
        }

        return ProjectLiteralFamilyMatch(program, input, alternateLiteralUtf16Lengths, 0, 0, match, out _, out _);
    }

    public static bool TryFindNextFallbackVerifiedMatch(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
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
        while (Utf8FallbackSearchExecutor.TryFindNextCandidate(plan, input, startIndex, out var candidate))
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

    public static bool TryFindNextCompiledFallbackMatch(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram program,
        Utf8VerifierRuntime verifierRuntime,
        Utf8ExecutionProgram executionProgram,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        Utf8ExecutionBudget? budget,
        out Utf8ValueMatch match)
    {
        _ = program;
        match = Utf8ValueMatch.NoMatch;
        while (Utf8FallbackSearchExecutor.TryFindNextCandidate(plan, input, startIndex, out var candidate))
        {
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            if (!IsScalarBoundaryByteOffset(input, candidate.StartIndex))
            {
                startIndex = candidate.StartIndex + 1;
                continue;
            }

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

            startIndex = candidate.StartIndex + 1;
        }

        return false;
    }

    public static bool IsMatchStructuralIdentifierFamily(
        Utf8RegexPlan regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget)
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
        Utf8RegexPlan regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget)
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
        Utf8RegexPlan regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget)
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
        Utf8RegexPlan regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget)
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
        Utf8RegexPlan regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget)
    {
        return AsciiOrderedLiteralWindowExecutor.Count(
            input,
            regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan,
            regexPlan.SearchPlan,
            budget);
    }

    public static Utf8ValueMatch MatchOrderedLiteralWindow(
        Utf8RegexPlan regexPlan,
        ReadOnlySpan<byte> input,
        Utf8ExecutionBudget? budget)
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

    private static bool IsScalarBoundaryByteOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        return (uint)byteOffset <= (uint)input.Length &&
            (byteOffset == 0 || byteOffset == input.Length || (input[byteOffset] & 0xC0) != 0x80);
    }
}
