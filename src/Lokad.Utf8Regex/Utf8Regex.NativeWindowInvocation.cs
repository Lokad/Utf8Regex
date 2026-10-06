using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex;

/// <summary>Transfers the caller invocation into native window enumeration.</summary>
public sealed partial class Utf8Regex
{
    private bool TryCreateNativeWindowEnumerator(ReadOnlySpan<byte> input, CancellationToken token,
        out Utf8ValueMatchEnumerator enumerator)
    {
        enumerator = default;
        if (_preparedRegex.ExecutionKind != NativeExecutionKind.AsciiOrderedLiteralWindow) return false;
        try
        {
            if (!TryStartNativeStructuralInvocation(input, token, out var invocation, out var validation)) return false;
            // Mixed subjects require UTF-16 gap widths and Unicode separator
            // semantics. Preserve this invocation through the existing unit VM;
            // boundary-map acquisition still needs aggregate admission bounds.
            var cursor = validation.IsAscii
                ? new Utf8OperationMatchCursor(input, _preparedRegex, invocation)
                : new Utf8OperationMatchCursor(_unitSliceProgram ?? throw new InvalidOperationException("A window unit program is required."),
                    input, Utf8BoundaryMap.Create(input, validation), invocation);
            enumerator = new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, Pattern, MatchTimeout);
            return true;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }
}
