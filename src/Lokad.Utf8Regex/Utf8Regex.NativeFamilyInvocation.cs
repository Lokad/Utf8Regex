using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Supplies shared invocation state to metered native literal-family operations.</summary>
public sealed partial class Utf8Regex
{
    // Metered family value operations start their one deadline before validation.
    // Unlimited operations keep the established specialized kernels. Semantic
    // exclusions match native token enumeration; other profiles retain their VM.
    private bool TryStartNativeFamilyInvocation(ReadOnlySpan<byte> input, CancellationToken token,
        out Utf8SliceInvocation invocation)
    {
        invocation = default;
        if (MatchTimeout == System.Text.RegularExpressions.Regex.InfiniteMatchTimeout && !token.CanBeCanceled &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null)
            return false;
        if (RequiresCultureSensitiveFallback || _requiresKelvinSignFallback ||
            _unitSliceProgram is { HasSurrogateLiteral: true } ||
            _unitSliceProgram is { HasBalancing: true } or { HasSharedCaptureDeclarations: true } or { HasExplicitCaptureNumbers: true } ||
            _unitSliceProgram is { IgnoreCase: true } && _preparedRegex.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals ||
            _preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals))
            return false;
        invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        _ = Utf8Validation.Validate(input, ref invocation);
        return true;
    }
}
