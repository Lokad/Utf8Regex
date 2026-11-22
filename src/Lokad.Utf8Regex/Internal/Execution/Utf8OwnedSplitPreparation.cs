using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8OwnedSplitPreparation
{
    internal static Utf8ValueSplitEnumerator Create(Utf8SliceProgram program, ReadOnlySpan<byte> input, int count,
        scoped ref Utf8SliceInvocation invocation, Utf8PreparedRegex? literals = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var effectiveCount = count == 0 ? int.MaxValue : count;
        invocation.ShareStorageForCursor();
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        var cursor = literals is not null && effectiveCount != 1
            ? Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(literals, input, invocation, byteFloor: 0)
            : Utf8OperationMatchCursor.CreateSplitCursor(program, input, invocation, effectiveCount == 1);
        try
        {
            cursor.CheckOwnedSliceCursorBoundary();
            return new Utf8ValueSplitEnumerator(input, cursor, effectiveCount, program.RightToLeft);
        }
        catch
        {
            cursor.AbortOwnedSplitSetup();
            throw;
        }
    }
}
