using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

/// <summary>Retains one invocation through owned offset entry and enumeration.</summary>
public sealed partial class Utf8Regex
{
    private Utf8ValueMatchEnumerator CreateOwnedByteOffsetEnumerator(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken token)
    {
        var program = _ownedSliceProgram;
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            if (!Utf8ValidatedInput.IsScalarBoundary(input, startOffsetInBytes))
                throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
            Utf8OperationMatchCursor cursor;
            if (!HasFiniteExecutionLimits && CanUseNativeOffsetCursor(ref invocation, out var anchored))
            {
                cursor = anchored
                    ? new Utf8OperationMatchCursor(input, _anchoredLiteral.LiteralUtf8 ?? throw new InvalidOperationException("An anchored plan needs its literal."), _anchoredLiteral.Kind, invocation).WithLiteralStartFloor(startOffsetInBytes)
                    : Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(_preparedRegex, input, invocation, startOffsetInBytes);
            }
            else
            {
                invocation.ShareStorageForCursor();
                cursor = new Utf8OperationMatchCursor(program, input, invocation, startOffsetInBytes);
            }
            return new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, this);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }

    private bool CanUseNativeOffsetCursor(ref Utf8SliceInvocation invocation, out bool anchored)
    {
        var program = _ownedSliceProgram;
        // The public offset selector applies its lifetime-policy guard before
        // considering native eligibility.
        var eligible = !UsesRightToLeft() &&
            (Options & RegexOptions.NonBacktracking) == 0 && !_requiresKelvinSignFallback &&
            !program.HasBalancing && !program.HasSharedCaptureDeclarations && !program.HasExplicitCaptureNumbers &&
            !program.HasContiguousAnchor;
        anchored = false;
        if (!eligible) return false;
        // A folded anchor has a complete ASCII case fiber. Only the candidate
        // bytes need comparing, even when other parts of the subject are Unicode.
        anchored = _anchoredLiteral.HasValue && _anchoredLiteral.LiteralUtf8.Length > 0 &&
            (_anchoredLiteral.IsExact || (Options & RegexOptions.CultureInvariant) != 0);
        var native = !program.IgnoreCase &&
            Utf8CompiledOperationCursorFactory.CanCreateInvocationMatchCursor(_preparedRegex, ref invocation);
        return anchored || native;
    }
}
