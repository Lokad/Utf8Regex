using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledFallbackMatchRouter
{
    public static bool TryFindNextMatch(
        Utf8StructuralSearchPlan[] candidatePlans,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8SliceInvocation invocation,
        out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (!Utf8FallbackSearchExecutor.TryFindNextVerifiedMatch(candidatePlans, verifierRuntime,
            input, validation, startIndex, ref invocation, out _, out var verification)) return false;
        Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
        var breach = Utf8ProjectionExecutor.TryProjectScalarVerification(input, validation, verification, ref invocation, out match);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Scalar match projection exceeded {breach}.");
        return true;
    }
}
