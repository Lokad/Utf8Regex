using System.Buffers;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8FixedMatchReplaceEngine
{
    public delegate int FindNextMatch(ReadOnlySpan<byte> input, int startIndex);

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionBudget? budget = null)
    {
        if (structuralLinearProgram.Kind != Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            throw new ArgumentException("Structural linear fixed replacement requires an ASCII fixed-token program.", nameof(structuralLinearProgram));
        }

        var matchLength = structuralLinearProgram.SimplePatternPlan.MinLength;
        var state = BuildDeterministicState(input, replacement.Length, matchLength, structuralLinearProgram, budget);
        if (state.Count == 0)
        {
            Return(state);
            return input.ToArray();
        }

        var output = new byte[state.OutputLength];
        WriteReplacement(input, replacement, matchLength, output, state);
        Return(state);
        return output;
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionBudget? budget = null)
    {
        if (structuralLinearProgram.Kind != Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
        {
            throw new ArgumentException("Structural linear fixed replacement requires an ASCII fixed-token program.", nameof(structuralLinearProgram));
        }

        var matchLength = structuralLinearProgram.SimplePatternPlan.MinLength;
        var state = BuildDeterministicState(input, replacement.Length, matchLength, structuralLinearProgram, budget);
        if (state.Count == 0)
        {
            Return(state);
            return TryCopyToDestination(input, destination, out bytesWritten);
        }

        if (state.OutputLength > destination.Length)
        {
            bytesWritten = 0;
            Return(state);
            return false;
        }

        WriteReplacement(input, replacement, matchLength, destination, state);
        bytesWritten = state.OutputLength;
        Return(state);
        return true;
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        int matchLength,
        FindNextMatch findNextMatch,
        Utf8ExecutionBudget? budget = null)
    {
        if (matchLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(matchLength));
        }

        var state = BuildState(input, replacement.Length, matchLength, findNextMatch, budget);
        if (state.Count == 0)
        {
            Return(state);
            return input.ToArray();
        }

        var output = new byte[state.OutputLength];
        WriteReplacement(input, replacement, matchLength, output, state);
        Return(state);
        return output;
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        int matchLength,
        FindNextMatch findNextMatch,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionBudget? budget = null)
    {
        if (matchLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(matchLength));
        }

        var state = BuildState(input, replacement.Length, matchLength, findNextMatch, budget);
        if (state.Count == 0)
        {
            Return(state);
            return TryCopyToDestination(input, destination, out bytesWritten);
        }

        if (state.OutputLength > destination.Length)
        {
            bytesWritten = 0;
            Return(state);
            return false;
        }

        WriteReplacement(input, replacement, matchLength, destination, state);
        bytesWritten = state.OutputLength;
        Return(state);
        return true;
    }

    private static State BuildState(
        ReadOnlySpan<byte> input,
        int replacementLength,
        int matchLength,
        FindNextMatch findNextMatch,
        Utf8ExecutionBudget? budget)
    {
        var positions = ArrayPool<int>.Shared.Rent(16);
        var count = 0;
        var start = 0;
        var outputLength = input.Length;

        while (start <= input.Length - matchLength)
        {
            budget?.Step(input);
            var matchIndex = findNextMatch(input, start);
            if (matchIndex < 0)
            {
                break;
            }

            if (count == positions.Length)
            {
                var grown = ArrayPool<int>.Shared.Rent(positions.Length * 2);
                Array.Copy(positions, grown, positions.Length);
                ArrayPool<int>.Shared.Return(positions);
                positions = grown;
            }

            positions[count++] = matchIndex;
            checked
            {
                outputLength += replacementLength - matchLength;
            }

            start = matchIndex + matchLength;
        }

        return new State(positions, count, outputLength);
    }

    private static State BuildDeterministicState(
        ReadOnlySpan<byte> input,
        int replacementLength,
        int matchLength,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionBudget? budget)
    {
        var positions = ArrayPool<int>.Shared.Rent(16);
        var count = 0;
        var outputLength = input.Length;
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

            if (count == positions.Length)
            {
                var grown = ArrayPool<int>.Shared.Rent(positions.Length * 2);
                Array.Copy(positions, grown, positions.Length);
                ArrayPool<int>.Shared.Return(positions);
                positions = grown;
            }

            positions[count++] = matchIndex;
            checked
            {
                outputLength += replacementLength - matchLength;
            }
        }

        return new State(positions, count, outputLength);
    }

    private static void WriteFixedTokenReplacement(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Span<byte> destination,
        Utf8ExecutionBudget? budget)
    {
        var matchLength = structuralLinearProgram.SimplePatternPlan.MinLength;
        var scanState = new Utf8AsciiDeterministicScanState(0, structuralLinearProgram.DeterministicProgram.SearchLiteralOffset);
        var sourcePosition = 0;
        var destinationPosition = 0;
        while (scanState.NextStartIndex <= input.Length - matchLength &&
            Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicFixedWidthMatch(
                structuralLinearProgram,
                input,
                ref scanState,
                budget,
                out var matchIndex))
        {
            input[sourcePosition..matchIndex].CopyTo(destination[destinationPosition..]);
            destinationPosition += matchIndex - sourcePosition;
            replacement.CopyTo(destination[destinationPosition..]);
            destinationPosition += replacement.Length;
            sourcePosition = matchIndex + matchLength;
        }

        input[sourcePosition..].CopyTo(destination[destinationPosition..]);
    }

    private static void WriteReplacement(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        int matchLength,
        Span<byte> destination,
        State state)
    {
        var sourcePosition = 0;
        var written = 0;
        for (var i = 0; i < state.Count; i++)
        {
            var matchIndex = state.Positions[i];
            written += CopySlice(input, sourcePosition, matchIndex - sourcePosition, destination, written);
            replacement.CopyTo(destination[written..]);
            written += replacement.Length;
            sourcePosition = matchIndex + matchLength;
        }

        CopySlice(input, sourcePosition, input.Length - sourcePosition, destination, written);
    }

    private static bool TryCopyToDestination(ReadOnlySpan<byte> input, Span<byte> destination, out int bytesWritten)
    {
        if (input.Length > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        input.CopyTo(destination);
        bytesWritten = input.Length;
        return true;
    }

    private static int CopySlice(ReadOnlySpan<byte> input, int start, int length, Span<byte> destination, int destinationOffset)
    {
        if (length == 0)
        {
            return 0;
        }

        input.Slice(start, length).CopyTo(destination[destinationOffset..]);
        return length;
    }

    private static void Return(State state)
    {
        ArrayPool<int>.Shared.Return(state.Positions);
    }

    private readonly record struct State(int[] Positions, int Count, int OutputLength);
}
