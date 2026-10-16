using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Supplies shared invocation state to metered native literal operations.</summary>
public sealed partial class Utf8Regex
{
    // A result-producing adapter borrows discovery's actual invocation. Keep
    // complete-subject validation and the prepared literal route inside it;
    // derived coordinates are unnecessary for the borrowed byte range.
    internal bool FindLiteralForResult(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation,
        out int start, out int end, out Utf8ValidationResult validation)
    {
        start = end = -1;
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal) ||
            _preparedRegex.LiteralUtf8 is not { } literal || UsesRightToLeft())
            throw new InvalidOperationException("Literal result discovery requires an exact LTR literal.");
        validation = Utf8Validation.Validate(input, ref invocation);
        var control = new Utf8InvocationBufferControl(ref invocation);
        Utf8InvocationOutput.Checkpoint(control);
        if (literal.Length == 0)
        {
            start = end = 0;
            return true;
        }
        var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
            input, 0, input.Length, false, ref invocation, out var match, out var found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal result discovery exceeded {breach}.");
        Utf8InvocationOutput.Checkpoint(control);
        if (!found) return false;
        start = match.Index;
        end = match.Index + match.Length;
        return true;
    }

    // The adapter's literal result/count root shares this invocation with
    // validation and counting, instead of starting a second budget at shaping.
    internal int CountForResult(ReadOnlySpan<byte> input, int startOffsetInBytes,
        ref Utf8SliceInvocation invocation)
    {
        _ = Utf8Validation.Validate(input, ref invocation);
        if (!CanCountNativeLiterals(_ownedSliceProgram))
            return OwnedSliceCountFromByte(input, _ownedSliceProgram, startOffsetInBytes, ref invocation);
        var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(_preparedRegex.SearchPlan,
            input[startOffsetInBytes..], false, ref invocation, out var count);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
        return count;
    }

    // Shared semantic eligibility, independent of the optional Core owner's
    // policy. An adapter can supply finite limits through its borrowed invocation.
    private bool CanCountNativeLiterals(Utf8SliceProgram program) =>
        !program.RightToLeft && !program.IgnoreCase &&
        !program.HasBalancing && !program.HasSharedCaptureDeclarations &&
        !program.HasExplicitCaptureNumbers && !program.HasContiguousAnchor &&
        !program.SawLookaround && !program.HasConditionalTest &&
        !_requiresKelvinSignFallback && !program.CanMatchEmpty &&
        (_preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal &&
            _preparedRegex.LiteralUtf8 is { Length: > 0 } ||
         _preparedRegex.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            _preparedRegex.SearchPlan.AlternateLiteralsUtf8 is { Length: 2 or 3 }) &&
        !_preparedRegex.SearchPlan.HasBoundaryRequirements && !_preparedRegex.SearchPlan.HasTrailingLiteralRequirement;

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
