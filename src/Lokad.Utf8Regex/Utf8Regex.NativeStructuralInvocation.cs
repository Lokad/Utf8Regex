using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Admits native structural execution; callers request only their needed subject facts.</summary>
public sealed partial class Utf8Regex
{
    private bool TryStartNativeStructuralInvocation(CancellationToken token, bool allowUnmetered,
        out Utf8SliceInvocation invocation)
    {
        invocation = default;
        if (!allowUnmetered && MatchTimeout == System.Text.RegularExpressions.Regex.InfiniteMatchTimeout && !token.CanBeCanceled &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null)
            return false;
        var eligible = _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
            _preparedRegex.StructuralSearchPlan.HasValue && _preparedRegex.StructuralVerifier.AsciiProgram.LinearProgram.HasValue ||
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow &&
            _preparedRegex.StructuralLinearProgram.OrderedLiteralWindowPlan.HasValue;
        if (!eligible ||
            UsesRightToLeft() || (Options & System.Text.RegularExpressions.RegexOptions.NonBacktracking) != 0 ||
            _requiresKelvinSignFallback ||
            _ownedSliceProgram is { IgnoreCase: true } or { HasBalancing: true } or
                { HasSharedCaptureDeclarations: true } or { HasExplicitCaptureNumbers: true })
            return false;
        invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token) { InputValidation = InputValidation };
        return true;
    }
}
