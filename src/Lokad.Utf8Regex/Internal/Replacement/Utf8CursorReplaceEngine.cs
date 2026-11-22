using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8CursorReplaceEngine
{
    internal static byte[] Replace(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacement, ref Utf8OperationMatchCursor cursor, out int replacementCount)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        ulong outputReservation = 0;
        try
        {
            var reverse = cursor.IsRightToLeft;
            var outputLength = checked((int)BuildRanges(input.Length, replacement.Length, ref cursor, ref ledger));
            replacementCount = ledger.Count;
            cursor.ChargeResultBytes(Utf8ConstructionBudget.ArrayBytes<byte>(outputLength));
            var output = Utf8InvocationOutput.AllocateBytes(outputLength, cursor.WorkingBufferControl, out outputReservation);
            if (ledger.Count == 0) Utf8InvocationOutput.Copy(input, output, cursor.WorkingBufferControl);
            else Utf8ReplacementOutput.CopyLiteralRanges(input, replacement, output,
                ledger.WrittenRanges, reverse, cursor.WorkingBufferControl);
            return output;
        }
        finally
        {
            cursor.WorkingBufferControl.ReleaseWorking(outputReservation);
            ledger.Dispose(cursor.WorkingBufferControl);
        }
    }

    internal static bool TryReplace(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacement, Span<byte> destination, ref Utf8OperationMatchCursor cursor, out int bytesWritten)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var reverse = cursor.IsRightToLeft;
            var outputLength = BuildRanges(input.Length, replacement.Length, ref cursor, ref ledger);
            if (outputLength > destination.Length)
            {
                Utf8InvocationOutput.Checkpoint(cursor.WorkingBufferControl);
                bytesWritten = 0;
                return false;
            }

            if (ledger.Count == 0) Utf8InvocationOutput.Copy(input, destination, cursor.WorkingBufferControl);
            else Utf8ReplacementOutput.CopyLiteralRanges(input, replacement, destination,
                ledger.WrittenRanges, reverse, cursor.WorkingBufferControl);
            bytesWritten = (int)outputLength;
            return true;
        }
        finally
        {
            ledger.Dispose(cursor.WorkingBufferControl);
        }
    }

    private static long BuildRanges(
        int inputLength, int replacementLength, ref Utf8OperationMatchCursor cursor, ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = (long)inputLength;
        while (cursor.MoveNext())
        {
            var match = cursor.CurrentValueMatch;
            var matchLength = match.EndOffsetInBytes - match.StartOffsetInBytes;
            var growth = ledger.PendingGrowthBytes;
            if (growth != 0) cursor.ChargeResultBytes(growth);
            ledger.Add(new Utf8ReplacementRange(match.StartOffsetInBytes, matchLength), cursor.WorkingBufferControl);
            outputLength += (long)replacementLength - matchLength;
        }

        return outputLength;
    }
}
