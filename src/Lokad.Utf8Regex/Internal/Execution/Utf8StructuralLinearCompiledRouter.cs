using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8StructuralLinearCompiledRouter
{
    public static bool CanUseDirectStructuralIdentifierFamilyRoute(Utf8RegexPlan regexPlan, Regex regex)
    {
        return regexPlan.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
            !UsesRightToLeft(regex);
    }

    public static bool CanUseDirectOrderedLiteralWindowRoute(Utf8RegexPlan regexPlan, Regex regex)
    {
        return regexPlan.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow &&
            !UsesRightToLeft(regex);
    }

    public static bool CanUseEmittedDeterministicMatcher(
        Utf8RegexPlan regexPlan,
        bool emitEnabled,
        Utf8EmittedDeterministicMatcher? emittedDeterministicMatcher,
        Utf8ValidationResult validation,
        Utf8ExecutionBudget? budget)
    {
        return budget is null &&
            emitEnabled &&
            validation.IsAscii &&
            emittedDeterministicMatcher is not null &&
            regexPlan.ExecutionKind != NativeExecutionKind.AsciiStructuralIdentifierFamily;
    }

    public static bool CanUseEmittedKernelMatcher(
        Utf8RegexPlan regexPlan,
        bool emitEnabled,
        Utf8EmittedKernelMatcher? emittedKernelMatcher,
        Utf8ValidationResult validation,
        Utf8ExecutionBudget? budget)
    {
        return budget is null &&
            emitEnabled &&
            emittedKernelMatcher is not null &&
            regexPlan.ExecutionKind is NativeExecutionKind.AsciiStructuralIdentifierFamily or NativeExecutionKind.AsciiOrderedLiteralWindow;
    }

    public static bool TryIsMatch(
        Utf8RegexPlan regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        bool emitEnabled,
        Utf8EmittedDeterministicMatcher? emittedDeterministicMatcher,
        Utf8EmittedKernelMatcher? emittedKernelMatcher,
        PreparedAsciiFindPlan orderedWindowTrailingFindPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionBudget? budget,
        out bool isMatch)
    {
        if (CanUseDirectOrderedLiteralWindowRoute(regexPlan, verifierRuntime.FallbackCandidateVerifier.FallbackRegex))
        {
            isMatch = AsciiOrderedLiteralWindowExecutor.FindNext(
                input,
                regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan,
                regexPlan.SearchPlan,
                orderedWindowTrailingFindPlan,
                0,
                budget,
                out _) >= 0;
            return true;
        }

        if (CanUseEmittedKernelMatcher(regexPlan, emitEnabled, emittedKernelMatcher, validation, budget))
        {
            isMatch = emittedKernelMatcher!.FindNext(input, 0, out _) >= 0;
            return true;
        }

        if (CanUseDirectStructuralIdentifierFamilyRoute(regexPlan, verifierRuntime.FallbackCandidateVerifier.FallbackRegex))
        {
            isMatch = Utf8AsciiStructuralIdentifierFamilyExecutor.FindNext(
                input,
                regexPlan.StructuralIdentifierFamilyPlan,
                regexPlan.SearchPlan,
                regexPlan.StructuralSearchPlan,
                verifierRuntime.StructuralVerifierRuntime,
                0,
                budget,
                out _) >= 0;
            return true;
        }

        if (CanUseEmittedDeterministicMatcher(regexPlan, emitEnabled, emittedDeterministicMatcher, validation, budget))
        {
            isMatch = emittedDeterministicMatcher!.FindNext(input, 0, out _) >= 0;
            return true;
        }

        isMatch = false;
        return false;
    }

    public static bool TryCount(
        Utf8RegexPlan regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        bool emitEnabled,
        Utf8EmittedDeterministicMatcher? emittedDeterministicMatcher,
        Utf8EmittedKernelMatcher? emittedKernelMatcher,
        PreparedAsciiFindPlan orderedWindowTrailingFindPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionBudget? budget,
        out int count)
    {
        if (CanUseDirectOrderedLiteralWindowRoute(regexPlan, verifierRuntime.FallbackCandidateVerifier.FallbackRegex))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ordered_literal_window");
            count = AsciiOrderedLiteralWindowExecutor.Count(
                input,
                regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan,
                regexPlan.SearchPlan,
                orderedWindowTrailingFindPlan,
                budget);
            return true;
        }

        if (CanUseEmittedKernelMatcher(regexPlan, emitEnabled, emittedKernelMatcher, validation, budget))
        {
            var kernelMatcher = emittedKernelMatcher!;
            if (kernelMatcher.Plan.Kind == Utf8EmittedKernelKind.PairedOrderedAsciiWhitespaceLiteralWindow)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_ordered_literal_window");
                count = AsciiOrderedLiteralWindowExecutor.Count(
                    input,
                    regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan,
                    regexPlan.SearchPlan,
                    budget);
                return true;
            }

            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(kernelMatcher.Plan.RouteName);
            count = kernelMatcher.Count(input);
            return true;
        }

        if (CanUseDirectStructuralIdentifierFamilyRoute(regexPlan, verifierRuntime.FallbackCandidateVerifier.FallbackRegex))
        {
            if (regexPlan.SearchPlan.PreparedSearcher.HasValue &&
                Utf8AsciiStructuralIdentifierFamilyExecutor.CanUseSuffixOnlyCountKernel(regexPlan.StructuralIdentifierFamilyPlan))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_structural_family_suffix");
                count = Utf8AsciiStructuralIdentifierFamilyExecutor.CountSuffixOnlyViaPreparedSearcher(
                    input,
                    regexPlan.StructuralIdentifierFamilyPlan,
                    regexPlan.SearchPlan,
                    budget);
                return true;
            }

            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_structural_family_linear");
            count = Utf8AsciiStructuralIdentifierFamilyExecutor.Count(
                input,
                regexPlan.StructuralIdentifierFamilyPlan,
                regexPlan.SearchPlan,
                regexPlan.StructuralSearchPlan,
                verifierRuntime.StructuralVerifierRuntime,
                budget);
            return true;
        }

        if (CanUseEmittedDeterministicMatcher(regexPlan, emitEnabled, emittedDeterministicMatcher, validation, budget))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_structural_linear_emit");
            count = emittedDeterministicMatcher!.Count(input);
            return true;
        }

        count = 0;
        return false;
    }

    public static bool TryMatch(
        Utf8RegexPlan regexPlan,
        Utf8VerifierRuntime verifierRuntime,
        bool emitEnabled,
        Utf8EmittedDeterministicMatcher? emittedDeterministicMatcher,
        Utf8EmittedKernelMatcher? emittedKernelMatcher,
        PreparedAsciiFindPlan orderedWindowTrailingFindPlan,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8ExecutionBudget? budget,
        out Utf8ValueMatch match)
    {
        if (CanUseDirectOrderedLiteralWindowRoute(regexPlan, verifierRuntime.FallbackCandidateVerifier.FallbackRegex))
        {
            var index = AsciiOrderedLiteralWindowExecutor.FindNext(
                input,
                regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan,
                regexPlan.SearchPlan,
                orderedWindowTrailingFindPlan,
                0,
                budget,
                out var matchedLength);
            match = index < 0
                ? Utf8ValueMatch.NoMatch
                : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
            return true;
        }

        if (CanUseEmittedKernelMatcher(regexPlan, emitEnabled, emittedKernelMatcher, validation, budget))
        {
            var emittedIndex = emittedKernelMatcher!.FindNext(input, 0, out var emittedMatchedLength);
            match = emittedIndex < 0
                ? Utf8ValueMatch.NoMatch
                : new Utf8ValueMatch(true, true, emittedIndex, emittedMatchedLength, emittedIndex, emittedMatchedLength);
            return true;
        }

        if (CanUseDirectStructuralIdentifierFamilyRoute(regexPlan, verifierRuntime.FallbackCandidateVerifier.FallbackRegex))
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
            match = index < 0
                ? Utf8ValueMatch.NoMatch
                : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
            return true;
        }

        if (CanUseEmittedDeterministicMatcher(regexPlan, emitEnabled, emittedDeterministicMatcher, validation, budget))
        {
            var index = emittedDeterministicMatcher!.FindNext(input, 0, out var matchedLength);
            match = index < 0
                ? Utf8ValueMatch.NoMatch
                : new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    private static bool UsesRightToLeft(Regex regex)
    {
        return (regex.Options & RegexOptions.RightToLeft) != 0;
    }
}
