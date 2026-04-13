using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8FixedTemplateReplaceEngine
{
    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionBudget? budget = null)
    {
        var state = BuildFixedTokenState(input, template, structuralLinearProgram, budget);
        if (state is null)
        {
            return input.ToArray();
        }

        var fixedState = state.Value;
        try
        {
            var output = new byte[fixedState.OutputLength];
            WriteReplacement(input, template, fixedState.MatchPositions, fixedState.MatchCount, output);
            return output;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(fixedState.MatchPositions);
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionBudget? budget = null)
    {
        var state = BuildFixedTokenState(input, template, structuralLinearProgram, budget);
        if (state is null)
        {
            return TryCopyToDestination(input, destination, out bytesWritten);
        }

        var fixedState = state.Value;
        try
        {
            if (fixedState.OutputLength > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            WriteReplacement(input, template, fixedState.MatchPositions, fixedState.MatchCount, destination);
            bytesWritten = fixedState.OutputLength;
            return true;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(fixedState.MatchPositions);
        }
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        Utf8ExecutionBudget? budget = null)
    {
        var state = BuildState(input, template, findFirst, findNext, budget);
        if (state is null)
        {
            return input.ToArray();
        }

        var fixedState = state.Value;
        var output = new byte[fixedState.OutputLength];
        WriteReplacement(input, template, findNext, fixedState.FirstIndex, output, budget);
        return output;
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionBudget? budget = null)
    {
        var state = BuildState(input, template, findFirst, findNext, budget);
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

        WriteReplacement(input, template, findNext, fixedState.FirstIndex, destination, budget);
        bytesWritten = fixedState.OutputLength;
        return true;
    }

    private static FixedLengthState? BuildState(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Func<ReadOnlySpan<byte>, int> findFirst,
        Func<ReadOnlySpan<byte>, int, int> findNext,
        Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        var firstIndex = findFirst(input);
        if (firstIndex < 0)
        {
            return null;
        }

        var outputLength = input.Length;
        var delta = template.ReplacementLength - template.MatchLength;
        var position = 0;
        var currentMatch = firstIndex;

        while (currentMatch >= 0)
        {
            budget?.Step(input);
            checked
            {
                outputLength += delta;
            }

            position = currentMatch + template.MatchLength;
            currentMatch = position <= input.Length - template.MatchLength
                ? findNext(input, position)
                : -1;
        }

        return new FixedLengthState(firstIndex, outputLength);
    }

    private static FixedMatchPositionsState? BuildFixedTokenState(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionBudget? budget)
    {
        var positions = ArrayPool<int>.Shared.Rent(16);
        var count = 0;
        var searchStart = 0;

        try
        {
            while (searchStart <= input.Length)
            {
                var matchIndex = Utf8AsciiFixedTokenLinearExecutor.FindNext(structuralLinearProgram, input, searchStart, budget, out var matchLength);
                if (matchIndex < 0)
                {
                    break;
                }

                if (count == positions.Length)
                {
                    var grown = ArrayPool<int>.Shared.Rent(positions.Length * 2);
                    Array.Copy(positions, grown, count);
                    ArrayPool<int>.Shared.Return(positions);
                    positions = grown;
                }

                positions[count++] = matchIndex;
                searchStart = matchIndex + Math.Max(matchLength, 1);
            }

            if (count == 0)
            {
                ArrayPool<int>.Shared.Return(positions);
                return null;
            }

            var outputLength = checked(input.Length + (template.ReplacementLength - template.MatchLength) * count);
            return new FixedMatchPositionsState(positions, count, outputLength);
        }
        catch
        {
            ArrayPool<int>.Shared.Return(positions);
            throw;
        }
    }

    private static void WriteReplacement(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        Func<ReadOnlySpan<byte>, int, int> findNext,
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
            written += AppendTemplate(destination[written..], input, template, currentMatch);

            position = currentMatch + template.MatchLength;
            currentMatch = position <= input.Length - template.MatchLength
                ? findNext(input, position)
                : -1;
        }

        CopySlice(input, position, input.Length - position, destination, written);
    }

    private static void WriteReplacement(
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        int[] matchPositions,
        int matchCount,
        Span<byte> destination)
    {
        var position = 0;
        var written = 0;
        for (var i = 0; i < matchCount; i++)
        {
            var currentMatch = matchPositions[i];
            written += CopySlice(input, position, currentMatch - position, destination, written);
            written += AppendTemplate(destination[written..], input, template, currentMatch);
            position = currentMatch + template.MatchLength;
        }

        CopySlice(input, position, input.Length - position, destination, written);
    }

    private static int AppendTemplate(
        Span<byte> destination,
        ReadOnlySpan<byte> input,
        Utf8FixedTemplateReplacement template,
        int matchIndex)
    {
        var written = 0;
        foreach (var segment in template.Segments)
        {
            switch (segment.Kind)
            {
                case Utf8FixedTemplateReplacementSegmentKind.Literal:
                    if (segment.LiteralUtf8 is { Length: > 0 } literalUtf8)
                    {
                        literalUtf8.CopyTo(destination[written..]);
                        written += literalUtf8.Length;
                    }
                    break;

                case Utf8FixedTemplateReplacementSegmentKind.MatchSlice:
                    written += CopySlice(input, matchIndex + segment.MatchOffset, segment.Length, destination, written);
                    break;
            }
        }

        return written;
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

    private readonly record struct FixedMatchPositionsState(int[] MatchPositions, int MatchCount, int OutputLength);
}
