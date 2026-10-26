using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

/// <summary>Retains one invocation through owned offset entry and enumeration.</summary>
public sealed partial class Utf8Regex
{
    private static Utf8BoundaryMap PrepareOffsetInput(ReadOnlySpan<byte> input, int utf16Offset,
        ref Utf8SliceInvocation invocation, out Utf8ValidationResult validation, out int startByte, out ulong reservation)
    {
        validation = Utf8Validation.Validate(input, ref invocation);
        var start = Utf16Position.CreateChecked(validation, utf16Offset, nameof(utf16Offset));
        var map = Utf8BoundaryMap.Create(input, validation, ref invocation, out reservation);
        var boundary = map.Resolve(start.Value);
        if (!boundary.IsScalarBoundary)
            throw new ArgumentOutOfRangeException(nameof(utf16Offset), utf16Offset,
                "The requested UTF-16 offset is not aligned to a UTF-8 scalar boundary.");
        startByte = boundary.ByteOffset;
        return map;
    }

    private Utf8ValueMatchEnumerator CreateOwnedOffsetEnumerator(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken token)
    {
        var program = _ownedSliceProgram;
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        ulong reservation = 0;
        try
        {
            var map = PrepareOffsetInput(input, utf16Offset, ref invocation,
                out var validation, out var startByte, out reservation);
            Utf8OperationMatchCursor cursor;
            if (!HasFiniteExecutionLimits && CanUseNativeOffsetCursor(validation, ref invocation, out var anchored))
            {
                map = default;
                invocation.Ledger.Release(reservation);
                reservation = 0;
                cursor = anchored
                    ? new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8 ?? throw new InvalidOperationException("An anchored plan needs its literal."),
                        _fallbackDirectFamily.Kind, validation, invocation).WithStartFloor(startByte, utf16Offset)
                    : Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(_preparedRegex, input, validation, invocation, startByte, utf16Offset);
            }
            else
            {
                var ownedReservation = reservation;
                reservation = 0;
                cursor = Utf8OperationMatchCursor.CreateMappedFromUnit(program, input, ref map, ref invocation, ownedReservation, utf16Offset);
            }
            return new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, Pattern, MatchTimeout);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        finally { if (reservation != 0) invocation.Ledger.Release(reservation); }
    }

    private bool CanUseNativeOffsetCursor(Utf8ValidationResult validation,
        ref Utf8SliceInvocation invocation, out bool anchored)
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

    // One-shot compatibility entries borrow the same controlled anchored cursor
    // as enumeration. Raw shortcuts below remain for genuinely unmetered calls.
    private bool TryMatchControlledAnchoredOffset(ReadOnlySpan<byte> input, Utf8ValidationResult validation,
        int startByte, int utf16Offset, scoped ref Utf8SliceInvocation invocation, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (HasFiniteExecutionLimits || !CanUseNativeOffsetCursor(validation, ref invocation, out var anchored) || !anchored)
            return false;
        var literal = _fallbackDirectFamily.LiteralUtf8
            ?? throw new InvalidOperationException("An anchored plan needs its literal.");
        var cursor = new Utf8OperationMatchCursor(input, literal, _fallbackDirectFamily.Kind, validation, invocation)
            .WithStartFloor(startByte, utf16Offset);
        if (cursor.MoveNext()) match = cursor.CurrentValueMatch;
        return true;
    }

    // Python bulk collection borrows this invocation through validation, byte-floor
    // projection, discovery and shaping. Native search keeps its existing algorithm.
    internal Utf8ValueMatchEnumerator EnumerateMatchesForResult(ReadOnlySpan<byte> input,
        int startOffsetInBytes, scoped ref Utf8SliceInvocation invocation)
    {
        ulong reservation = 0;
        try
        {
            var validation = Utf8Validation.Validate(input, ref invocation);
            _ = Utf8BytePosition.CreateChecked(validation, startOffsetInBytes, nameof(startOffsetInBytes));
            if (startOffsetInBytes < input.Length && (input[startOffsetInBytes] & 0xC0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
            var startUnit = validation.IsAscii ? startOffsetInBytes : startOffsetInBytes == 0 ? 0 :
                Utf8Validation.Validate(input[..startOffsetInBytes], ref invocation).Utf16Length;
            Utf8OperationMatchCursor cursor;
            if (CanUseNativeOffsetCursor(validation, ref invocation, out var anchored))
                cursor = anchored
                    ? new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8 ?? throw new InvalidOperationException("An anchored plan needs its literal."),
                        _fallbackDirectFamily.Kind, validation, invocation).WithStartFloor(startOffsetInBytes, startUnit)
                    : Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(_preparedRegex, input, validation, invocation, startOffsetInBytes, startUnit);
            else
            {
                var map = Utf8BoundaryMap.Create(input, validation, ref invocation, out reservation);
                var ownedReservation = reservation;
                reservation = 0;
                cursor = Utf8OperationMatchCursor.CreateMappedFromUnit(_ownedSliceProgram, input,
                    ref map, ref invocation, ownedReservation, startUnit);
            }
            return new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, Pattern, MatchTimeout);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern, MatchTimeout, invocation.Budget.Limits);
        }
        finally { if (reservation != 0) invocation.Ledger.Release(reservation); }
    }
}
