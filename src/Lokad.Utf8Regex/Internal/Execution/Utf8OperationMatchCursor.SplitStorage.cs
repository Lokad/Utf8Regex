using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref partial struct Utf8OperationMatchCursor
{
    internal ulong DebugPeakWorkingBytes => _sliceInvocation.Budget.PeakWorkingBytes;

    // The split's coordinate map outlives match EOF: Current and copied
    // views still refer to it. Keep it charged for the shared invocation.
    // Count one needs only this invocation holder, without match discovery.
    internal static Utf8OperationMatchCursor CreateSplitCursor(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        Utf8BoundaryMap map, Utf8SliceInvocation invocation, bool tailOnly)
    {
        var cursor = tailOnly
            ? new Utf8OperationMatchCursor(input, map, invocation)
            : new Utf8OperationMatchCursor(program, input, map, invocation);
        cursor._retainBoundaryMap = true;
        cursor._sliceInvocation.Budget.TryNoteWorkingBytes(cursor._sliceInvocation.Ledger.ReservedBytes);
        return cursor;
    }

    private Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8BoundaryMap map, Utf8SliceInvocation invocation)
        : this(input, map, invocation.Budget.Deadline)
    {
        _sliceInvocation = invocation;
        _mode = EnumeratorMode.Exhausted;
    }

    internal void AbortOwnedSplitMatches()
    {
        _nonBacktrackingState = null;
        _sliceProgram = null;
    }

    internal void AbortOwnedSplitSetup(ref ulong mapBytes)
    {
        _nonBacktrackingState?.Release(ref _sliceInvocation);
        _nonBacktrackingState = null;
        _sliceInvocation.Ledger.Release(mapBytes);
        mapBytes = 0;
        _boundaryMap = null;
        _sliceProgram = null;
    }
}
