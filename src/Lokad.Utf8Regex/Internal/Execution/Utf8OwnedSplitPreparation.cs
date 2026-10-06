using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8OwnedSplitPreparation
{
    internal static Utf8ValueSplitEnumerator Create(Utf8SliceProgram program, ReadOnlySpan<byte> input, int count,
        scoped ref Utf8SliceInvocation invocation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var effectiveCount = count == 0 ? int.MaxValue : count;
        ulong mapBytes = 0;
        ulong decodedBytes = 0;
        var cursor = default(Utf8OperationMatchCursor);
        var cursorCreated = false;
        var transferred = false;
        var map = default(Utf8BoundaryMap);
        try
        {
            invocation.ShareStorageForCursor();
            var validation = Utf8Validation.Validate(input, ref invocation);
            map = Utf8BoundaryMap.Create(input, validation, ref invocation, out mapBytes);
            var decoded = Utf8DecodedInput.Create(input, validation, ref invocation, out decodedBytes);
            map = map.WithStorageLease(invocation.Ledger.CreateLease(checked(mapBytes + decodedBytes)));
            cursor = Utf8OperationMatchCursor.CreateSplitCursor(program, input, map, invocation,
                mapBytes, decodedBytes, effectiveCount == 1);
            cursorCreated = true;
            cursor.CheckOwnedSliceCursorBoundary();
            var enumerator = new Utf8ValueSplitEnumerator(input, decoded, cursor, map,
                effectiveCount, validation.Utf16Length, program.RightToLeft);
            transferred = true;
            return enumerator;
        }
        finally
        {
            if (!transferred)
            {
                if (cursorCreated) cursor.AbortOwnedSplitSetup();
                else if (map.StorageLease is { } lease) lease.AbortBeforePublication();
                else
                {
                    invocation.Ledger.Release(decodedBytes);
                    invocation.Ledger.Release(mapBytes);
                }
            }
        }
    }
}
