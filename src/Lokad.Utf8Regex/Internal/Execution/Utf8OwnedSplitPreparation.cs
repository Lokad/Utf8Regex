using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8OwnedSplitPreparation
{
    internal static Utf8ValueSplitEnumerator Create(Utf8SliceProgram program, ReadOnlySpan<byte> input, int count,
        scoped ref Utf8SliceInvocation invocation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var effectiveCount = count == 0 ? int.MaxValue : count;
        invocation.ShareStorageForCursor();
        Utf8Validation.CheckSubject(input, ref invocation);
        var cursor = Utf8OperationMatchCursor.CreateSplitCursor(program, input, invocation, effectiveCount == 1);
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
