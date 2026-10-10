using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8CursorReplaceEngine
{
    internal static byte[] Replace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacement,
        ref Utf8OperationMatchCursor cursor) => Replace(input, replacement, ref cursor, out _);

    internal static byte[] Replace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacement,
        ref Utf8OperationMatchCursor cursor,
        out int replacementCount)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var reverse = cursor.IsRightToLeft;
            var outputLength = BuildRanges(input.Length, replacement.Length, ref cursor, ref ledger);
            replacementCount = ledger.Count;
            if (ledger.Count == 0)
            {
                return input.ToArray();
            }

            var output = new byte[outputLength.Value];
            Emit(input, replacement, output, ledger.WrittenRanges, reverse);
            return output;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    internal static bool TryReplace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacement,
        Span<byte> destination,
        ref Utf8OperationMatchCursor cursor,
        out int bytesWritten)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var reverse = cursor.IsRightToLeft;
            var outputLength = BuildRanges(input.Length, replacement.Length, ref cursor, ref ledger);
            if (ledger.Count == 0)
            {
                return Utf8ReplacementOutput.TryCopyUnchanged(input, destination, out bytesWritten);
            }

            if (outputLength.Value > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            Emit(input, replacement, destination, ledger.WrittenRanges, reverse);
            bytesWritten = outputLength.Value;
            return true;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    private static Utf8ReplacementOutputLength BuildRanges(
        int inputLength,
        int replacementLength,
        ref Utf8OperationMatchCursor cursor,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(inputLength);
        while (cursor.MoveNext())
        {
            var match = cursor.Current;
            if (!match.IsByteAligned)
            {
                throw new InvalidOperationException("A replacement match is not aligned to UTF-8 scalar boundaries.");
            }

            ledger.Add(new Utf8ReplacementRange(match.IndexInBytes, match.LengthInBytes));
            outputLength.ReplaceRange(match.LengthInBytes, replacementLength);
        }

        return outputLength;
    }

    private static void Emit(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacement,
        Span<byte> destination,
        ReadOnlySpan<Utf8ReplacementRange> matches,
        bool reverse)
    {
        var sourcePosition = 0;
        var sink = new Utf8ReplacementOutputSink(destination);
        // Matching order is preserved during discovery. Emit in subject order,
        // including the empty RTL match before a nonempty range with that start.
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[reverse ? matches.Length - index - 1 : index];
            sink.AppendSlice(input, sourcePosition, match.Start - sourcePosition);
            sink.Append(replacement);
            sourcePosition = match.Start + match.Length;
        }

        sink.Append(input[sourcePosition..]);
    }
}
