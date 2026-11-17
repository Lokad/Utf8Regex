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
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token) { InputValidation = InputValidation };
        try
        {
            var validation = Utf8Validation.Subject(input, ref invocation);
            _ = Utf8BytePosition.CreateChecked(validation, startOffsetInBytes, nameof(startOffsetInBytes));
            if (startOffsetInBytes < input.Length && (input[startOffsetInBytes] & 0xC0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
            Utf8OperationMatchCursor cursor;
            if (!HasFiniteExecutionLimits && CanUseNativeOffsetCursor(validation, ref invocation, out var anchored))
            {

                cursor = anchored
                    ? new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8 ?? throw new InvalidOperationException("An anchored plan needs its literal."), _fallbackDirectFamily.Kind, validation, invocation).WithStartFloor(startOffsetInBytes)
                    : Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(_preparedRegex, input, validation, invocation, startOffsetInBytes);
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

    private bool CanUseNativeOffsetCursor(Utf8ValidationResult validation, ref Utf8SliceInvocation invocation, out bool anchored)
    {
        var program = _ownedSliceProgram;
        // Borrowed adapter invocations keep native eligibility even when their
        // Core preparation carries finite policy. Public Core offset selectors
        // apply their own lifetime-policy guard at the call boundary.
        var eligible = !UsesRightToLeft() &&
            (Options & RegexOptions.NonBacktracking) == 0 && !_requiresKelvinSignFallback &&
            !program.HasBalancing && !program.HasSharedCaptureDeclarations && !program.HasExplicitCaptureNumbers &&
            !program.HasContiguousAnchor;
        anchored = eligible && _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            (_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix or
                Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd ||
             validation.IsAscii && (Options & RegexOptions.CultureInvariant) != 0 &&
                _fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix or
                    Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd);
        var native = eligible && !program.IgnoreCase &&
            Utf8CompiledOperationCursorFactory.CanCreateInvocationMatchCursor(_preparedRegex, validation, ref invocation);
        return anchored || native;
    }

    // Python bulk collection borrows this invocation through validation, byte-floor
    // discovery and publication. Native search keeps its existing algorithm.
    internal Utf8ValueMatchEnumerator EnumerateMatchesForResult(ReadOnlySpan<byte> input, int startOffsetInBytes, scoped ref Utf8SliceInvocation invocation)
    {
        try
        {
            var validation = Utf8Validation.Subject(input, ref invocation);
            _ = Utf8BytePosition.CreateChecked(validation, startOffsetInBytes, nameof(startOffsetInBytes));
            if (startOffsetInBytes < input.Length && (input[startOffsetInBytes] & 0xC0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
            Utf8OperationMatchCursor cursor;
            if (CanUseNativeOffsetCursor(validation, ref invocation, out var anchored))
            {

                cursor = anchored
                    ? new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8 ?? throw new InvalidOperationException("An anchored plan needs its literal."), _fallbackDirectFamily.Kind, validation, invocation).WithStartFloor(startOffsetInBytes)
                    : Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(_preparedRegex, input, validation, invocation, startOffsetInBytes);
            }
            else
            {
                invocation.ShareStorageForCursor();
                cursor = new Utf8OperationMatchCursor(_ownedSliceProgram, input, invocation, startOffsetInBytes);
            }
            return new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, this);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(invocation.Ledger.HasMemoryAllowance ? default : input,
                Pattern, MatchTimeout, invocation.Budget.Limits);
        }
    }
}
