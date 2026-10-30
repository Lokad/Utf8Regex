using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Transfers the caller invocation into native structural enumeration.</summary>
public sealed partial class Utf8Regex
{
    private bool TryCreateNativeStructuralEnumerator(ReadOnlySpan<byte> input, CancellationToken token,
        out Utf8ValueMatchEnumerator enumerator)
    {
        enumerator = default;
        var family = _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily;
        if (!family && _preparedRegex.ExecutionKind != NativeExecutionKind.AsciiOrderedLiteralWindow) return false;
        try
        {
            // The unlimited family cursor replaces BCL global discovery as well.
            // Values retain their existing cheap unmetered kernels.
            if (!TryStartNativeStructuralInvocation(input, token, family, out var invocation, out var validation)) return false;
            // Mixed subjects require scalar gap widths and Unicode separator
            // semantics. Preserve the same invocation through the scalar VM.
            var native = validation.IsAscii && (!family ||
                !_preparedRegex.StructuralIdentifierFamilyPlan.HasPrefixOverlap &&
                !_preparedRegex.SearchPlan.HasAlternateLiteralPrefixOverlap);
            var cursor = native
                ? new Utf8OperationMatchCursor(input, _preparedRegex, invocation)
                : Utf8OperationMatchCursor.CreateMapped(_ownedSliceProgram,
                    input, validation, ref invocation, false, out _);
            enumerator = new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, this);
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }
}
