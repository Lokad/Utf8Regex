using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8StructuralLinearCompiledRouter
{
    public static bool CanUseDirectStructuralIdentifierFamilyRoute(Utf8PreparedRegex regexPlan, RegexOptions options)
    {
        return regexPlan.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
            !UsesRightToLeft(options);
    }

    public static bool TryIsMatch(
        Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget, out bool isMatch)
    {

        if (CanUseDirectStructuralIdentifierFamilyRoute(regexPlan, regexPlan.SemanticRegex.Options))
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

        isMatch = false;
        return false;
    }

    public static bool TryCount(
        Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget, out int count)
    {

        if (CanUseDirectStructuralIdentifierFamilyRoute(regexPlan, regexPlan.SemanticRegex.Options))
        {
            if (regexPlan.SearchPlan.PreparedSearcher.HasValue &&
                Utf8AsciiStructuralIdentifierFamilyExecutor.CanUseSuffixOnlyCountKernel(regexPlan.StructuralIdentifierFamilyPlan))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralFamilySuffix);
                count = Utf8AsciiStructuralIdentifierFamilyExecutor.CountSuffixOnlyViaPreparedSearcher(
                    input,
                    regexPlan.StructuralIdentifierFamilyPlan,
                    regexPlan.SearchPlan,
                    budget);
                return true;
            }

            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralFamilyLinear);
            count = Utf8AsciiStructuralIdentifierFamilyExecutor.Count(
                input,
                regexPlan.StructuralIdentifierFamilyPlan,
                regexPlan.SearchPlan,
                regexPlan.StructuralSearchPlan,
                verifierRuntime.StructuralVerifierRuntime,
                budget);
            return true;
        }

        count = 0;
        return false;
    }

    public static bool TryMatch(
        Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget, out Utf8CaptureRange match)
    {

        if (CanUseDirectStructuralIdentifierFamilyRoute(regexPlan, regexPlan.SemanticRegex.Options))
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
                ? default(Utf8CaptureRange)
                : new Utf8CaptureRange(index, (index) + (matchedLength));
            return true;
        }

        match = default(Utf8CaptureRange);
        return false;
    }

    private static bool UsesRightToLeft(RegexOptions options)
    {
        return (options & RegexOptions.RightToLeft) != 0;
    }
}
