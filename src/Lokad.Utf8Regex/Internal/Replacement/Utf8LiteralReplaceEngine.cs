using System.Buffers;

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
        Utf8ExecutionBudget? budget = null)
    {
        if (matchLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(matchLength));
        }

        if (matchLength == 0)
        {
            return input.ToArray();
        }

        var state = BuildFixedLengthState(input, replacement, findFirst, findNext, matchLength, budget);
        if (state is null)
        {
            return input.ToArray();
        }

        var fixedState = state.Value;
        var output = new byte[fixedState.OutputLength];
        WriteFixedLengthReplacement(input, replacement, findNext, matchLength, fixedState.FirstIndex, output, budget);
        return output;
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        TryFindNextMatch tryFindNextMatch,
        Utf8ExecutionBudget? budget = null)
    {
        var state = BuildVariableLengthState(input, replacement, tryFindNextMatch, budget);
        if (state.Count == 0)
        {
            Return(state);
            return input.ToArray();
        }

        var output = new byte[state.OutputLength];
        WriteVariableLengthReplacement(input, replacement, output, state);
        Return(state);
        return output;
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        int matchLength,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionBudget? budget = null)
    {
        if (matchLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(matchLength));
        }

        if (matchLength == 0)
        {
            return TryCopyToDestination(input, destination, out bytesWritten);
        }

        var state = BuildFixedLengthState(input, replacement, findFirst, findNext, matchLength, budget);
        if (state is null)
        {
            return TryCopyToDestination(input, destination, out bytesWritten);
        }

        var fixedState = state.Value;
        if (fixedState.OutputLength > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        WriteFixedLengthReplacement(input, replacement, findNext, matchLength, fixedState.FirstIndex, destination, budget);
        bytesWritten = fixedState.OutputLength;
        return true;
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        TryFindNextMatch tryFindNextMatch,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionBudget? budget = null)
    {
        var state = BuildVariableLengthState(input, replacement, tryFindNextMatch, budget);
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

        WriteVariableLengthReplacement(input, replacement, destination, state);
        bytesWritten = state.OutputLength;
        Return(state);
        return true;
    }

    private static FixedLengthState? BuildFixedLengthState(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        int matchLength,
        Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        var firstIndex = findFirst(input);
        if (firstIndex < 0)
        {
            return null;
        }

        var outputLength = input.Length;
        var delta = replacement.Length - matchLength;
        var position = 0;
        var currentMatch = firstIndex;

        while (currentMatch >= 0)
        {
            budget?.Step(input);
            checked
            {
                outputLength += delta;
            }

            position = currentMatch + matchLength;
            currentMatch = position <= input.Length - matchLength
                ? findNext(input, position)
                : -1;
        }

        return new FixedLengthState(firstIndex, outputLength);
    }

    private static void WriteFixedLengthReplacement(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        int matchLength,
        int firstIndex,
        Span<byte> destination,
        Utf8ExecutionBudget? budget)
    {
        var position = 0;
        var currentMatch = firstIndex;
        var written = 0;
        while (currentMatch >= 0)
        {
            budget?.Step(input);
            written += CopySlice(input, position, currentMatch - position, destination, written);
            replacement.CopyTo(destination[written..]);
            written += replacement.Length;

            position = currentMatch + matchLength;
            currentMatch = position <= input.Length - matchLength
                ? findNext(input, position)
                : -1;
        }

        CopySlice(input, position, input.Length - position, destination, written);
    }

    private static VariableLengthState BuildVariableLengthState(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        TryFindNextMatch tryFindNextMatch,
        Utf8ExecutionBudget? budget)
    {
        var positions = ArrayPool<int>.Shared.Rent(16);
        var lengths = ArrayPool<int>.Shared.Rent(16);
        budget?.Step(input);
        if (!tryFindNextMatch(input, 0, out var currentMatch, out var currentLength))
        {
            return new VariableLengthState(positions, lengths, 0, input.Length);
        }

        var outputLength = input.Length;
        var count = 0;
        while (currentMatch >= 0)
        {
            if (count == positions.Length)
            {
                Grow(ref positions, ref lengths);
            }

            positions[count] = currentMatch;
            lengths[count] = currentLength;
            count++;

            budget?.Step(input);
            checked
            {
                outputLength += replacement.Length - currentLength;
            }

            var position = currentMatch + currentLength;
            if (!tryFindNextMatch(input, position, out currentMatch, out currentLength))
            {
                break;
            }
        }

        return new VariableLengthState(positions, lengths, count, outputLength);
    }

    private static void WriteVariableLengthReplacement(
        ReadOnlySpan<byte> input,
        byte[] replacement,
        Span<byte> destination,
        VariableLengthState state)
    {
        var position = 0;
        var written = 0;
        for (var i = 0; i < state.Count; i++)
        {
            var currentMatch = state.Positions[i];
            var currentLength = state.Lengths[i];
            written += CopySlice(input, position, currentMatch - position, destination, written);
            replacement.CopyTo(destination[written..]);
            written += replacement.Length;
            position = currentMatch + currentLength;
        }

        CopySlice(input, position, input.Length - position, destination, written);
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

    private readonly record struct FixedLengthState(int FirstIndex, int OutputLength);

    private static void Grow(ref int[] positions, ref int[] lengths)
    {
        var grownPositions = ArrayPool<int>.Shared.Rent(positions.Length * 2);
        var grownLengths = ArrayPool<int>.Shared.Rent(lengths.Length * 2);
        Array.Copy(positions, grownPositions, positions.Length);
        Array.Copy(lengths, grownLengths, lengths.Length);
        ArrayPool<int>.Shared.Return(positions);
        ArrayPool<int>.Shared.Return(lengths);
        positions = grownPositions;
        lengths = grownLengths;
    }

    private static void Return(VariableLengthState state)
    {
        ArrayPool<int>.Shared.Return(state.Positions);
        ArrayPool<int>.Shared.Return(state.Lengths);
    }

    private readonly record struct VariableLengthState(int[] Positions, int[] Lengths, int Count, int OutputLength);
}
