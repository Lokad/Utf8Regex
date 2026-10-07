using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref partial struct Utf8OperationMatchCursor
{
    private ulong _splitDecodedReservedBytes;
    internal ulong DebugSplitDecodedReservedBytes => _splitDecodedReservedBytes;
    internal ulong DebugPeakWorkingBytes => _sliceInvocation.Budget.PeakWorkingBytes;

    // The split's map and decoded text outlive match EOF: Current and copied
    // views still refer to them. Keep both charged for the holder lifetime.
    // Count one needs only this invocation holder, without match discovery.
    internal static Utf8OperationMatchCursor CreateSplitCursor(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        Utf8BoundaryMap map, Utf8SliceInvocation invocation, ulong mapBytes, ulong decodedBytes, bool tailOnly)
    {
        var cursor = tailOnly
            ? new Utf8OperationMatchCursor(input, map, invocation)
            : new Utf8OperationMatchCursor(program, input, map, invocation);
        cursor._boundaryMapReservedBytes = mapBytes;
        cursor._retainBoundaryMapReservation = true;
        cursor._splitDecodedReservedBytes = decodedBytes;
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

    internal void AbortOwnedSplitSetup()
    {
        _nonBacktrackingState?.AbortCursorSetup();
        _nonBacktrackingState = null;
        _boundaryMap?.StorageLease?.AbortBeforePublication();
        _boundaryMap = null;
        _boundaryMapReservedBytes = 0;
        _splitDecodedReservedBytes = 0;
        _sliceProgram = null;
    }
}
