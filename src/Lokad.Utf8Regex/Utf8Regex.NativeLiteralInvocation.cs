using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Supplies shared invocation state to metered native literal operations.</summary>
public sealed partial class Utf8Regex
{
    // The adapter's canonical-literal result root shares this invocation with
    // validation and counting, instead of starting a second budget at shaping.
    internal int CountForResult(ReadOnlySpan<byte> input, int startOffsetInBytes,
        ref Utf8SliceInvocation invocation)
    {
        _ = Utf8Validation.Validate(input, ref invocation);
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal) ||
            _preparedRegex.LiteralUtf8 is not { Length: > 0 } || UsesRightToLeft())
            return OwnedSliceCountFromByte(input, _ownedSliceProgram, startOffsetInBytes, ref invocation);
        var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(_preparedRegex.SearchPlan,
            input[startOffsetInBytes..], false, ref invocation, out var count);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
        return count;
    }

    // Metered literal value operations start their one deadline before validation.
    // Unlimited operations keep the established specialized kernels. Semantic
    // exclusions match native token enumeration; other profiles retain their VM.
    private bool TryStartNativeLiteralInvocation(ReadOnlySpan<byte> input, CancellationToken token,
        out Utf8SliceInvocation invocation)
    {
        invocation = default;
        if (MatchTimeout == System.Text.RegularExpressions.Regex.InfiniteMatchTimeout && !token.CanBeCanceled &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null)
            return false;
        if (_requiresKelvinSignFallback ||
            _ownedSliceProgram is { HasBalancing: true } or { HasSharedCaptureDeclarations: true } or { HasExplicitCaptureNumbers: true } ||
            _ownedSliceProgram is { IgnoreCase: true } && _preparedRegex.ExecutionKind is not
                (NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) ||
            _preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or
                NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) ||
            _preparedRegex.LiteralUtf8 is { Length: 0 })
            return false;
        // Empty literals use the shared scalar program and its boundary progression.
        invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        _ = Utf8Validation.Validate(input, ref invocation);
        return true;
    }
}
