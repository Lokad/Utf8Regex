using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8LiteralReplaceEngine
{
    public delegate bool TryFindNextMatch(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchLength);

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        int matchLength,
        Utf8ExecutionDeadline budget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(matchLength);
        if (matchLength == 0)
        {
            return input.ToArray();
        }

        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildFixedLengthRanges(
                input,
                replacement.Length,
                findFirst,
                findNext,
                matchLength,
                budget,
                ref ledger);
            return EmitAllocated(input, replacement, outputLength, ref ledger);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        TryFindNextMatch tryFindNextMatch,
        Utf8ExecutionDeadline budget)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildVariableLengthRanges(
                input,
                replacement.Length,
                tryFindNextMatch,
                budget,
                ref ledger);
            return EmitAllocated(input, replacement, outputLength, ref ledger);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        int matchLength,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionDeadline budget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(matchLength);
        if (matchLength == 0)
        {
            return Utf8ReplacementOutput.TryCopyUnchanged(input, destination, out bytesWritten);
        }

        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildFixedLengthRanges(
                input,
                replacement.Length,
                findFirst,
                findNext,
                matchLength,
                budget,
                ref ledger);
            return TryEmit(input, replacement, outputLength, destination, ref ledger, out bytesWritten);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        TryFindNextMatch tryFindNextMatch,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionDeadline budget)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildVariableLengthRanges(
                input,
                replacement.Length,
                tryFindNextMatch,
                budget,
                ref ledger);
            return TryEmit(input, replacement, outputLength, destination, ref ledger, out bytesWritten);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    private static Utf8ReplacementOutputLength BuildFixedLengthRanges(
        ReadOnlySpan<byte> input,
        int replacementLength,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        int matchLength,
        Utf8ExecutionDeadline budget,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(input.Length);
        budget.Step();
        var currentMatch = findFirst(input);
        while (currentMatch >= 0)
        {
            budget.Step();
            ledger.Add(new Utf8ReplacementRange(currentMatch, matchLength));
            outputLength.ReplaceRange(matchLength, replacementLength);

            var nextStart = currentMatch + matchLength;
            currentMatch = nextStart <= input.Length - matchLength
                ? findNext(input, nextStart)
                : -1;
        }

        return outputLength;
    }

    private static Utf8ReplacementOutputLength BuildVariableLengthRanges(
        ReadOnlySpan<byte> input,
        int replacementLength,
        TryFindNextMatch tryFindNextMatch,
        Utf8ExecutionDeadline budget,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(input.Length);
        var nextStart = 0;
        while (nextStart <= input.Length)
        {
            budget.Step();
            if (!tryFindNextMatch(input, nextStart, out var matchIndex, out var matchLength))
            {
                break;
            }

            ledger.Add(new Utf8ReplacementRange(matchIndex, matchLength));
            outputLength.ReplaceRange(matchLength, replacementLength);
            nextStart = matchIndex + Math.Max(matchLength, 1);
        }

        return outputLength;
    }

    private static byte[] EmitAllocated(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Utf8ReplacementOutputLength outputLength,
        ref Utf8ReplacementRangeLedger ledger)
    {
        if (ledger.Count == 0)
        {
            return input.ToArray();
        }

        var output = new byte[outputLength.Value];
        Emit(input, replacement, output, ledger.WrittenRanges);
        return output;
    }

    private static bool TryEmit(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Utf8ReplacementOutputLength outputLength,
        Span<byte> destination,
        ref Utf8ReplacementRangeLedger ledger,
        out int bytesWritten)
    {
        if (ledger.Count == 0)
        {
            return Utf8ReplacementOutput.TryCopyUnchanged(input, destination, out bytesWritten);
        }

        if (outputLength.Value > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        Emit(input, replacement, destination, ledger.WrittenRanges);
        bytesWritten = outputLength.Value;
        return true;
    }

    private static void Emit(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Span<byte> destination,
        ReadOnlySpan<Utf8ReplacementRange> matches)
    {
        var sourcePosition = 0;
        var sink = new Utf8ReplacementOutputSink(destination);
        foreach (var match in matches)
        {
            sink.AppendSlice(input, sourcePosition, match.Start - sourcePosition);
            sink.Append(replacement);
            sourcePosition = match.Start + match.Length;
        }

        sink.Append(input[sourcePosition..]);
    }
}
