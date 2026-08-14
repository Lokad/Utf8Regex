using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8FixedMatchReplaceEngine
{
    public delegate int FindNextMatch(ReadOnlySpan<byte> input, int startIndex);

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionDeadline budget)
    {
        if (structuralLinearProgram.Kind != Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            throw new ArgumentException("Structural linear fixed replacement requires an ASCII fixed-token program.", nameof(structuralLinearProgram));
        }

        var matchLength = structuralLinearProgram.SimplePatternPlan.MinLength;
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildDeterministicState(
                input,
                replacement.Length,
                matchLength,
                structuralLinearProgram,
                budget,
                ref ledger);
            if (ledger.Count == 0)
            {
                return input.ToArray();
            }

            var output = new byte[outputLength.Value];
            WriteReplacement(input, replacement, output, ledger.WrittenRanges);
            return output;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionDeadline budget)
    {
        if (structuralLinearProgram.Kind != Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            throw new ArgumentException("Structural linear fixed replacement requires an ASCII fixed-token program.", nameof(structuralLinearProgram));
        }

        var matchLength = structuralLinearProgram.SimplePatternPlan.MinLength;
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildDeterministicState(
                input,
                replacement.Length,
                matchLength,
                structuralLinearProgram,
                budget,
                ref ledger);
            if (ledger.Count == 0)
            {
                return Utf8ReplacementOutput.TryCopyUnchanged(input, destination, out bytesWritten);
            }

            if (outputLength.Value > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            WriteReplacement(input, replacement, destination, ledger.WrittenRanges);
            bytesWritten = outputLength.Value;
            return true;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        int matchLength,
        FindNextMatch findNextMatch,
        Utf8ExecutionDeadline budget)
    {
        if (matchLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(matchLength));
        }

        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildState(
                input,
                replacement.Length,
                matchLength,
                findNextMatch,
                budget,
                ref ledger);
            if (ledger.Count == 0)
            {
                return input.ToArray();
            }

            var output = new byte[outputLength.Value];
            WriteReplacement(input, replacement, output, ledger.WrittenRanges);
            return output;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        int matchLength,
        FindNextMatch findNextMatch,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionDeadline budget)
    {
        if (matchLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(matchLength));
        }

        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildState(
                input,
                replacement.Length,
                matchLength,
                findNextMatch,
                budget,
                ref ledger);
            if (ledger.Count == 0)
            {
                return Utf8ReplacementOutput.TryCopyUnchanged(input, destination, out bytesWritten);
            }

            if (outputLength.Value > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            WriteReplacement(input, replacement, destination, ledger.WrittenRanges);
            bytesWritten = outputLength.Value;
            return true;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    private static Utf8ReplacementOutputLength BuildState(
        ReadOnlySpan<byte> input,
        int replacementLength,
        int matchLength,
        FindNextMatch findNextMatch,
        Utf8ExecutionDeadline budget,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var start = 0;
        var outputLength = new Utf8ReplacementOutputLength(input.Length);

        while (start <= input.Length - matchLength)
        {
            budget.Step();
            var matchIndex = findNextMatch(input, start);
            if (matchIndex < 0)
            {
                break;
            }

            ledger.Add(new Utf8ReplacementRange(matchIndex, matchLength));
            outputLength.ReplaceRange(matchLength, replacementLength);

            start = matchIndex + matchLength;
        }

        return outputLength;
    }

    private static Utf8ReplacementOutputLength BuildDeterministicState(
        ReadOnlySpan<byte> input,
        int replacementLength,
        int matchLength,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionDeadline budget,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(input.Length);
        var scanState = new Utf8AsciiDeterministicScanState(0, structuralLinearProgram.DeterministicProgram.SearchLiteralOffset);

        while (scanState.NextStartIndex <= input.Length - matchLength)
        {
            if (!Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicFixedWidthMatch(
                    structuralLinearProgram,
                    input,
                    ref scanState,
                    budget,
                    out var matchIndex))
            {
                break;
            }

            ledger.Add(new Utf8ReplacementRange(matchIndex, matchLength));
            outputLength.ReplaceRange(matchLength, replacementLength);
        }

        return outputLength;
    }

    private static void WriteReplacement(
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
