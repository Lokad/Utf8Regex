namespace Lokad.Utf8Regex.Internal.Execution;

internal ref partial struct Utf8OperationMatchCursor
{
    internal ulong DebugPeakWorkingBytes => _sliceInvocation.Budget.PeakWorkingBytes;

    // Count one retains the invocation alone. Other splits consume actual byte
    // outcomes from the same scalar discovery as legacy match enumeration.
    internal static Utf8OperationMatchCursor CreateSplitCursor(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        Utf8SliceInvocation invocation, bool tailOnly)
        => tailOnly ? new Utf8OperationMatchCursor(input, invocation)
            : new Utf8OperationMatchCursor(program, input, null, invocation, program.RightToLeft ? input.Length : 0);

    private Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8SliceInvocation invocation)
    {
        _input = input;
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _budget = invocation.Budget.Deadline;
        _sliceInvocation = invocation;
        _mode = EnumeratorMode.Exhausted;
    }

    internal bool MoveNextSplit(out Utf8CaptureRange range)
    {
        if (_mode == EnumeratorMode.OwnedSlice) return MoveNextOwnedSliceBytes(out range);
        range = default;
        if (!MoveNext()) return false;
        var match = CurrentValueMatch;
        range = new Utf8CaptureRange(match.IndexInBytes, match.IndexInBytes + match.LengthInBytes);
        return true;
    }

    internal void AbortOwnedSplitMatches()
    {
        _nonBacktrackingState = null;
        _sliceProgram = null;
    }

    internal void AbortOwnedSplitSetup()
    {
        _nonBacktrackingState?.Release(ref _sliceInvocation);
        AbortOwnedSplitMatches();
    }
}
