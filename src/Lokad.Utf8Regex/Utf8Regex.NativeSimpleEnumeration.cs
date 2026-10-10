using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Transfers the caller invocation into simple-pattern enumeration.</summary>
public sealed partial class Utf8Regex
{
    private bool TryCreateNativeSimpleEnumerator(ReadOnlySpan<byte> input, CancellationToken token,
        out Utf8ValueMatchEnumerator enumerator)
    {
        enumerator = default;
        if (MatchTimeout == System.Text.RegularExpressions.Regex.InfiniteMatchTimeout && !token.CanBeCanceled &&
                Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null ||
            _preparedRegex.ExecutionKind != NativeExecutionKind.AsciiSimplePattern ||
            _preparedRegex.SimplePatternPlan.Branches is not { Length: > 0 } ||
            UsesRightToLeft() || (Options & System.Text.RegularExpressions.RegexOptions.NonBacktracking) != 0 ||
            _requiresKelvinSignFallback ||
            _ownedSliceProgram is { IgnoreCase: true } or { HasBalancing: true })
            return false;
        try
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
            var validation = Utf8Validation.Validate(input, ref invocation);
            // Mixed subjects need scalar discovery unless the native proof
            // guarantees complete UTF-8 boundaries. Keep the same invocation.
            var byteSafe = _preparedRegex.StructuralLinearProgram.DeterministicProgram.HasValue
                ? _preparedRegex.StructuralLinearProgram.AllowsUtf8ByteSafe : _preparedRegex.SimplePatternPlan.IsUtf8ByteSafe;
            var cursor = !_preparedRegex.Features.HasLookarounds && (validation.IsAscii || byteSafe)
                ? new Utf8OperationMatchCursor(input, _preparedRegex, invocation, validation.IsAscii)
                : Utf8OperationMatchCursor.CreateMapped(_ownedSliceProgram, input, validation, ref invocation, false, out _);
            enumerator = new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, Pattern, MatchTimeout);
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }
}
