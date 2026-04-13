using System.Buffers;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8WholeMatchTemplateReplaceEngine
{
    public delegate bool TryFindNextMatch(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchLength);

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        TryFindNextMatch tryFindNextMatch,
        Utf8ExecutionBudget? budget = null)
    {
        var template = CreateTemplate(plan);
        var state = BuildState(input, template, tryFindNextMatch, budget);
        if (state.Count == 0)
        {
            Return(state);
            return input.ToArray();
        }

        var output = new byte[state.OutputLength];
        WriteReplacement(input, template, output, state);
        Return(state);
        return output;
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        TryFindNextMatch tryFindNextMatch,
        Span<byte> destination,
        out int bytesWritten,
        Utf8ExecutionBudget? budget = null)
    {
        var template = CreateTemplate(plan);
        var state = BuildState(input, template, tryFindNextMatch, budget);
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

        WriteReplacement(input, template, destination, state);
        bytesWritten = state.OutputLength;
        Return(state);
        return true;
    }

    public static bool CanExecute(Utf8ReplacementPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var instruction in plan.Instructions)
        {
            if (instruction.Kind != Utf8ReplacementInstructionKind.Literal &&
                instruction.Kind != Utf8ReplacementInstructionKind.WholeMatch &&
                !(instruction.Kind == Utf8ReplacementInstructionKind.Group && instruction.GroupNumber == 0))
            {
                return false;
            }
        }

        return true;
    }

    private static Template CreateTemplate(Utf8ReplacementPlan plan)
    {
        var segments = new Segment[plan.Instructions.Count];
        var literalBytesPerMatch = 0;
        var wholeMatchCopiesPerMatch = 0;

        for (var i = 0; i < plan.Instructions.Count; i++)
        {
            var instruction = plan.Instructions[i];
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    var literal = instruction.LiteralUtf8 ?? [];
                    segments[i] = new Segment(literal);
                    literalBytesPerMatch += literal.Length;
                    break;

                case Utf8ReplacementInstructionKind.WholeMatch:
                case Utf8ReplacementInstructionKind.Group when instruction.GroupNumber == 0:
                    segments[i] = new Segment(CopyWholeMatch: true);
                    wholeMatchCopiesPerMatch++;
                    break;

                default:
                    throw new InvalidOperationException("Unsupported whole-match template instruction.");
            }
        }

        return new Template(segments, literalBytesPerMatch, wholeMatchCopiesPerMatch);
    }

    private static VariableLengthState BuildState(
        ReadOnlySpan<byte> input,
        Template template,
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
                outputLength += template.LiteralBytesPerMatch + ((template.WholeMatchCopiesPerMatch - 1) * currentLength);
            }

            var position = currentMatch + currentLength;
            if (!tryFindNextMatch(input, position, out currentMatch, out currentLength))
            {
                break;
            }
        }

        return new VariableLengthState(positions, lengths, count, outputLength);
    }

    private static void WriteReplacement(
        ReadOnlySpan<byte> input,
        Template template,
        Span<byte> destination,
        VariableLengthState state)
    {
        var position = 0;
        var written = 0;
        for (var i = 0; i < state.Count; i++)
        {
            var matchIndex = state.Positions[i];
            var matchLength = state.Lengths[i];
            written += CopySlice(input, position, matchIndex - position, destination, written);
            written += AppendTemplate(destination[written..], input, template, matchIndex, matchLength);
            position = matchIndex + matchLength;
        }

        CopySlice(input, position, input.Length - position, destination, written);
    }

    private static int AppendTemplate(
        Span<byte> destination,
        ReadOnlySpan<byte> input,
        Template template,
        int matchIndex,
        int matchLength)
    {
        var written = 0;
        foreach (var segment in template.Segments)
        {
            if (segment.CopyWholeMatch)
            {
                written += CopySlice(input, matchIndex, matchLength, destination, written);
            }
            else if (segment.LiteralUtf8 is { Length: > 0 } literal)
            {
                literal.CopyTo(destination[written..]);
                written += literal.Length;
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

    private readonly record struct Segment(byte[]? LiteralUtf8 = null, bool CopyWholeMatch = false);
    private readonly record struct Template(Segment[] Segments, int LiteralBytesPerMatch, int WholeMatchCopiesPerMatch);
    private readonly record struct VariableLengthState(int[] Positions, int[] Lengths, int Count, int OutputLength);
}
