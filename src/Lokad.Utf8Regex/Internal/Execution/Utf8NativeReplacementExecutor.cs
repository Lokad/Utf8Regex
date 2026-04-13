using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8NativeReplacementExecutor
{
    public delegate bool TryFindNextMatch(ReadOnlySpan<byte> input, int startIndex, out Utf8NativeReplacementMatch match);

    public static bool CanExecute(Utf8ReplacementPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var instruction in plan.Instructions)
        {
            if (!CanExecute(instruction))
            {
                return false;
            }
        }

        return true;
    }

    public static byte[] Replace(ReadOnlySpan<byte> input, Utf8ReplacementPlan plan, TryFindNextMatch tryFindNextMatch)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var state = BuildReplacementState(input, plan, tryFindNextMatch);
        if (state is null)
        {
            return input.ToArray();
        }

        var replacementState = state.Value;
        var output = new byte[replacementState.OutputLength];
        WriteReplacement(input, plan, tryFindNextMatch, replacementState.FirstMatch, output);
        return output;
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        TryFindNextMatch tryFindNextMatch,
        Span<byte> destination,
        out int bytesWritten)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var state = BuildReplacementState(input, plan, tryFindNextMatch);
        if (state is null)
        {
            return TryCopyToDestination(input, destination, out bytesWritten);
        }

        var replacementState = state.Value;
        if (replacementState.OutputLength > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        WriteReplacement(input, plan, tryFindNextMatch, replacementState.FirstMatch, destination);
        bytesWritten = replacementState.OutputLength;
        return true;
    }

    public static byte[] Replace(ReadOnlySpan<byte> input, Utf8ReplacementPlan plan, PreparedSearcher searcher)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var state = BuildReplacementState(input, plan, searcher);
        if (state is null)
        {
            return input.ToArray();
        }

        var replacementState = state.Value;
        var output = new byte[replacementState.OutputLength];
        WriteReplacement(input, plan, searcher, output);
        return output;
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        PreparedSearcher searcher,
        Span<byte> destination,
        out int bytesWritten)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var state = BuildReplacementState(input, plan, searcher);
        if (state is null)
        {
            return TryCopyToDestination(input, destination, out bytesWritten);
        }

        var replacementState = state.Value;
        if (replacementState.OutputLength > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        WriteReplacement(input, plan, searcher, destination);
        bytesWritten = replacementState.OutputLength;
        return true;
    }

    private static bool CanExecute(Utf8ReplacementInstruction instruction)
    {
        return instruction.Kind switch
        {
            Utf8ReplacementInstructionKind.Literal => true,
            Utf8ReplacementInstructionKind.Group => instruction.GroupName is null && instruction.GroupNumber >= 0,
            Utf8ReplacementInstructionKind.WholeMatch => true,
            Utf8ReplacementInstructionKind.LeftPortion => true,
            Utf8ReplacementInstructionKind.RightPortion => true,
            Utf8ReplacementInstructionKind.WholeString => true,
            _ => false,
        };
    }

    private static int GetReplacementLength(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Utf8NativeReplacementMatch match)
    {
        var length = 0;
        foreach (var instruction in plan.Instructions)
        {
            checked
            {
                length += GetInstructionLength(input, instruction, match);
            }
        }

        return length;
    }

    private static int AppendReplacement(
        Span<byte> destination,
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Utf8NativeReplacementMatch match)
    {
        var written = 0;
        foreach (var instruction in plan.Instructions)
        {
            written += AppendInstruction(destination[written..], input, instruction, match);
        }

        return written;
    }

    private static ReplacementState? BuildReplacementState(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        TryFindNextMatch tryFindNextMatch)
    {
        if (!tryFindNextMatch(input, 0, out var currentMatch))
        {
            return null;
        }

        var firstMatch = SnapshotMatch(currentMatch);
        var outputLength = input.Length;
        var position = 0;
        while (currentMatch.Index >= 0)
        {
            checked
            {
                outputLength += GetReplacementLength(input, plan, currentMatch) - currentMatch.Length;
            }

            position = currentMatch.Index + currentMatch.Length;
            if (!tryFindNextMatch(input, position, out currentMatch))
            {
                break;
            }
        }

        return new ReplacementState(firstMatch, outputLength);
    }

    private static ReplacementState? BuildReplacementState(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        PreparedSearcher searcher)
    {
        var scanState = new PreparedMultiLiteralScanState(0, 0, 0);
        if (!searcher.TryFindNextNonOverlappingMatch(input, ref scanState, out var preparedMatch))
        {
            return null;
        }

        var firstMatch = new Utf8NativeReplacementMatch(preparedMatch.Index, preparedMatch.Length, LiteralId: preparedMatch.LiteralId);
        var currentMatch = firstMatch;
        var outputLength = input.Length;

        while (currentMatch.Index >= 0)
        {
            checked
            {
                outputLength += GetReplacementLength(input, plan, currentMatch) - currentMatch.Length;
            }

            if (!searcher.TryFindNextNonOverlappingMatch(input, ref scanState, out preparedMatch))
            {
                break;
            }

            currentMatch = new Utf8NativeReplacementMatch(preparedMatch.Index, preparedMatch.Length, LiteralId: preparedMatch.LiteralId);
        }

        return new ReplacementState(firstMatch, outputLength);
    }

    private static void WriteReplacement(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        TryFindNextMatch tryFindNextMatch,
        Utf8NativeReplacementMatch firstMatch,
        Span<byte> destination)
    {
        var position = 0;
        var currentMatch = firstMatch;
        var written = 0;
        while (currentMatch.Index >= 0)
        {
            written += CopySlice(input, position, currentMatch.Index - position, destination, written);
            written += AppendReplacement(destination[written..], input, plan, currentMatch);

            position = currentMatch.Index + currentMatch.Length;
            if (!tryFindNextMatch(input, position, out currentMatch))
            {
                break;
            }
        }

        CopySlice(input, position, input.Length - position, destination, written);
    }

    private static void WriteReplacement(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        PreparedSearcher searcher,
        Span<byte> destination)
    {
        var scanState = new PreparedMultiLiteralScanState(0, 0, 0);
        var position = 0;
        var written = 0;

        while (searcher.TryFindNextNonOverlappingMatch(input, ref scanState, out var preparedMatch))
        {
            var currentMatch = new Utf8NativeReplacementMatch(preparedMatch.Index, preparedMatch.Length, LiteralId: preparedMatch.LiteralId);
            written += CopySlice(input, position, currentMatch.Index - position, destination, written);
            written += AppendReplacement(destination[written..], input, plan, currentMatch);
            position = currentMatch.Index + currentMatch.Length;
        }

        CopySlice(input, position, input.Length - position, destination, written);
    }

    private static void AppendReplacement(
        List<byte> output,
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Utf8NativeReplacementMatch match)
    {
        foreach (var instruction in plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    if (instruction.LiteralUtf8 is { Length: > 0 } literalUtf8)
                    {
                        output.AddRange(literalUtf8);
                    }
                    break;

                case Utf8ReplacementInstructionKind.Group:
                    if (instruction.GroupNumber == 0)
                    {
                        output.AddRange(input.Slice(match.Index, match.Length).ToArray());
                    }
                    else if (match.Captures is not null &&
                             match.Captures.TryGet(instruction.GroupNumber, out var captureStart, out var captureLength))
                    {
                        output.AddRange(input.Slice(captureStart, captureLength).ToArray());
                    }
                    break;

                case Utf8ReplacementInstructionKind.WholeMatch:
                    output.AddRange(input.Slice(match.Index, match.Length).ToArray());
                    break;

                case Utf8ReplacementInstructionKind.LeftPortion:
                    output.AddRange(input[..match.Index].ToArray());
                    break;

                case Utf8ReplacementInstructionKind.RightPortion:
                    output.AddRange(input[(match.Index + match.Length)..].ToArray());
                    break;

                case Utf8ReplacementInstructionKind.WholeString:
                    output.AddRange(input.ToArray());
                    break;

                default:
                    throw new InvalidOperationException("Unsupported native replacement instruction kind.");
            }
        }
    }

    private static int GetInstructionLength(
        ReadOnlySpan<byte> input,
        Utf8ReplacementInstruction instruction,
        Utf8NativeReplacementMatch match)
    {
        return instruction.Kind switch
        {
            Utf8ReplacementInstructionKind.Literal => instruction.LiteralUtf8?.Length ?? 0,
            Utf8ReplacementInstructionKind.Group when instruction.GroupNumber == 0 => match.Length,
            Utf8ReplacementInstructionKind.Group when match.Captures is not null &&
                match.Captures.TryGet(instruction.GroupNumber, out _, out var captureLength) => captureLength,
            Utf8ReplacementInstructionKind.WholeMatch => match.Length,
            Utf8ReplacementInstructionKind.LeftPortion => match.Index,
            Utf8ReplacementInstructionKind.RightPortion => input.Length - (match.Index + match.Length),
            Utf8ReplacementInstructionKind.WholeString => input.Length,
            _ => 0,
        };
    }

    private static int AppendInstruction(
        Span<byte> destination,
        ReadOnlySpan<byte> input,
        Utf8ReplacementInstruction instruction,
        Utf8NativeReplacementMatch match)
    {
        switch (instruction.Kind)
        {
            case Utf8ReplacementInstructionKind.Literal:
                if (instruction.LiteralUtf8 is { Length: > 0 } literalUtf8)
                {
                    literalUtf8.CopyTo(destination);
                    return literalUtf8.Length;
                }
                return 0;

            case Utf8ReplacementInstructionKind.Group:
                if (instruction.GroupNumber == 0)
                {
                    return CopySlice(input, match.Index, match.Length, destination, 0);
                }

                if (match.Captures is not null &&
                    match.Captures.TryGet(instruction.GroupNumber, out var captureStart, out var captureLength))
                {
                    return CopySlice(input, captureStart, captureLength, destination, 0);
                }

                return 0;

            case Utf8ReplacementInstructionKind.WholeMatch:
                return CopySlice(input, match.Index, match.Length, destination, 0);

            case Utf8ReplacementInstructionKind.LeftPortion:
                return CopySlice(input, 0, match.Index, destination, 0);

            case Utf8ReplacementInstructionKind.RightPortion:
                return CopySlice(input, match.Index + match.Length, input.Length - (match.Index + match.Length), destination, 0);

            case Utf8ReplacementInstructionKind.WholeString:
                input.CopyTo(destination);
                return input.Length;

            default:
                throw new InvalidOperationException("Unsupported native replacement instruction kind.");
        }
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

    private static Utf8NativeReplacementMatch SnapshotMatch(Utf8NativeReplacementMatch match)
    {
        return match.Captures is null
            ? match
            : new Utf8NativeReplacementMatch(match.Index, match.Length, match.Captures.Clone(), match.LiteralId);
    }

    private readonly record struct ReplacementState(Utf8NativeReplacementMatch FirstMatch, int OutputLength);
}
