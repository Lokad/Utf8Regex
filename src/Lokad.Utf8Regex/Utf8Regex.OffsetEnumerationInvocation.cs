using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

/// <summary>Retains one invocation through owned UTF-16-offset entry and enumeration.</summary>
public sealed partial class Utf8Regex
{
    private bool TryCreateOwnedOffsetEnumerator(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken token,
        out Utf8ValueMatchEnumerator enumerator)
    {
        enumerator = default;
        if (_ownedSliceProgram is not { } program) return false;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        ulong reservation = 0;
        try
        {
            var validation = Utf8Validation.Validate(input, ref invocation);
            var start = Utf16Position.CreateChecked(validation, utf16Offset, nameof(utf16Offset));
            var map = Utf8BoundaryMap.Create(input, validation, ref invocation, out reservation);
            var boundary = map.Resolve(start.Value);
            if (!boundary.IsScalarBoundary)
                throw new ArgumentOutOfRangeException(nameof(utf16Offset), utf16Offset,
                    "The requested UTF-16 offset is not aligned to a UTF-8 scalar boundary.");
            var eligible = boundary.IsScalarBoundary && !UsesRightToLeft() &&
                (Options & RegexOptions.NonBacktracking) == 0 && !_requiresKelvinSignFallback &&
                !program.HasBalancing && !program.HasSharedCaptureDeclarations && !program.HasExplicitCaptureNumbers &&
                !program.HasContiguousAnchor;
            var anchored = eligible && _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
                _fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix or
                    Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd;
            var native = eligible && !program.IgnoreCase &&
                Utf8CompiledOperationCursorFactory.CanCreateInvocationMatchCursor(_preparedRegex, validation, ref invocation);
            Utf8OperationMatchCursor cursor;
            if (anchored || native)
            {
                map = default;
                invocation.Ledger.Release(reservation);
                reservation = 0;
                cursor = anchored
                    ? new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8 ?? throw new InvalidOperationException("An anchored plan needs its literal."),
                        _fallbackDirectFamily.Kind, validation, invocation).WithStartFloor(boundary.ByteOffset, start.Value)
                    : Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(_preparedRegex, input, validation, invocation, boundary.ByteOffset, start.Value);
            }
            else
            {
                var ownedReservation = reservation;
                reservation = 0;
                cursor = Utf8OperationMatchCursor.CreateMappedFromUnit(program, input, ref map, ref invocation, ownedReservation, start.Value);
            }
            enumerator = new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, Pattern, MatchTimeout);
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        finally { if (reservation != 0) invocation.Ledger.Release(reservation); }
    }
}
