using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Persistent scalar discovery for hosts that retain a cursor across calls.</summary>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Creates an incremental cursor over one unchanged, well-formed UTF-8 memory subject.</summary>
    /// <remarks>The memory and its manager must remain valid until exhaustion or disposal. Returned snapshots own their values.</remarks>
    public Utf8PythonMatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input)
        => CreateMatchCursor(input, 0, CancellationToken.None);

    /// <summary>Creates an incremental cursor beginning at a scalar-aligned byte offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The byte offset is outside the subject or inside a scalar.</exception>
    public Utf8PythonMatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int startOffsetInBytes)
        => CreateMatchCursor(input, startOffsetInBytes, CancellationToken.None);

    /// <summary>Creates an incremental cursor retaining one cumulative invocation token and deadline.</summary>
    /// <remarks>Work and result capacity are cumulative through validation, moves and snapshots. Depth bounds live backtracking/continuation frames; working bounds modeled major active capacity and growth overlap. Exclusive scratch releases synchronously; result traffic is never refunded. Sequential advances may cross asynchronous host waits.</remarks>
    /// <exception cref="OperationCanceledException">The invocation token is canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The byte offset is outside the subject or inside a scalar.</exception>
    /// <exception cref="ArgumentException">The subject is malformed UTF-8.</exception>
    public Utf8PythonMatchCursor CreateMatchCursor(ReadOnlyMemory<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken);
        ValidateScalarStartOffset(input.Span, startOffsetInBytes);
        return new Utf8PythonMatchCursor(input, ScalarProgram,
            _nameEntries, startOffsetInBytes, MatchTimeout, Pattern, invocation, _utf8Regex);
    }
}
