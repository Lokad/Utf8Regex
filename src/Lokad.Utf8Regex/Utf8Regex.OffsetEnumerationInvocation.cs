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
            _ = Utf8BytePosition.CreateChecked(input.Length, startOffsetInBytes, nameof(startOffsetInBytes));
            if (startOffsetInBytes < input.Length && (input[startOffsetInBytes] & 0xC0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
            Utf8OperationMatchCursor cursor;
            if (!HasFiniteExecutionLimits && CanUseNativeOffsetCursor(input, ref invocation, out var anchored))
            {

                cursor = anchored
                    ? new Utf8OperationMatchCursor(input, _anchoredLiteral.LiteralUtf8 ?? throw new InvalidOperationException("An anchored plan needs its literal."), _anchoredLiteral.Kind, invocation).WithStartFloor(startOffsetInBytes)
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

    private bool CanUseNativeOffsetCursor(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation, out bool anchored)
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
        // Only live ASCII-dependent cursors need input facts. Literal discovery
        // and the general scalar program use original bytes without this pass.
        var needsFacts =
            _anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix or
                Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or
                Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd;
        var isAscii = needsFacts && Utf8InputAnalyzer.Describe(input, ref invocation).IsAscii;
        anchored = eligible && _anchoredLiteral.LiteralUtf8 is { Length: > 0 } &&
            (_anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix or
                Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffixStrictEnd ||
             isAscii && (Options & RegexOptions.CultureInvariant) != 0 &&
                _anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix or
                    Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd);
        var native = eligible && !program.IgnoreCase &&
            Utf8CompiledOperationCursorFactory.CanCreateInvocationMatchCursor(_preparedRegex, ref invocation);
        return anchored || native;
    }
}
