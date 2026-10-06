using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Transfers the caller invocation into deterministic enumeration.</summary>
public sealed partial class Utf8Regex
{
    private bool TryCreateNativeDeterministicEnumerator(ReadOnlySpan<byte> input, CancellationToken token,
        out Utf8ValueMatchEnumerator enumerator)
    {
        enumerator = default;
        if (MatchTimeout == System.Text.RegularExpressions.Regex.InfiniteMatchTimeout && !token.CanBeCanceled &&
                Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null ||
            _preparedRegex.ExecutionKind != NativeExecutionKind.AsciiSimplePattern ||
            !_preparedRegex.StructuralLinearProgram.DeterministicProgram.HasValue ||
            UsesRightToLeft() || (Options & System.Text.RegularExpressions.RegexOptions.NonBacktracking) != 0 ||
            RequiresCultureSensitiveFallback || _requiresKelvinSignFallback || _unitSliceProgram is null ||
            _unitSliceProgram is { HasSurrogateLiteral: true } or { IgnoreCase: true } or { HasBalancing: true })
            return false;
        try
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
            var validation = Utf8Validation.Validate(input, ref invocation);
            // A byte-unsafe program needs UTF-16 units on mixed subjects. Its
            // entry invocation survives this semantic transfer without a reset.
            var cursor = validation.IsAscii || _preparedRegex.StructuralLinearProgram.AllowsUtf8ByteSafe
                ? new Utf8OperationMatchCursor(input, _preparedRegex, invocation, validation.IsAscii)
                : new Utf8OperationMatchCursor(_unitSliceProgram, input, Utf8BoundaryMap.Create(input, validation), invocation);
            enumerator = new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, Pattern, MatchTimeout);
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }
}
