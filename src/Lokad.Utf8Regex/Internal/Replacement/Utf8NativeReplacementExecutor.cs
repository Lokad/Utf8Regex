using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8NativeReplacementExecutor
{
    public static bool CanExecute(Utf8ReplacementPlan plan)
    {
        foreach (var instruction in plan.Instructions)
        {
            if (!CanExecute(instruction))
            {
                return false;
            }
        }

        return true;
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        ref Utf8OperationMatchCursor cursor)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildRanges(input, plan, ref cursor, ref ledger);
            return EmitAllocated(input, plan, outputLength, ref ledger);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    public static bool TryReplace(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        ref Utf8OperationMatchCursor cursor,
        Span<byte> destination,
        out int bytesWritten)
    {
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildRanges(input, plan, ref cursor, ref ledger);
            return TryEmit(input, plan, outputLength, destination, ref ledger, out bytesWritten);
        }
        finally
        {
            ledger.Dispose();
        }
    }

    private static bool CanExecute(Utf8ReplacementInstruction instruction)
    {
        return instruction.Kind switch
        {
            Utf8ReplacementInstructionKind.Literal => true,
            Utf8ReplacementInstructionKind.Group => instruction.GroupNumber >= 0,
            Utf8ReplacementInstructionKind.WholeMatch => true,
            Utf8ReplacementInstructionKind.LeftPortion => true,
            Utf8ReplacementInstructionKind.RightPortion => true,
            Utf8ReplacementInstructionKind.WholeString => true,
            _ => false,
        };
    }

    private static Utf8ReplacementOutputLength BuildRanges(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        ref Utf8OperationMatchCursor cursor,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(input.Length);
        while (cursor.MoveNext())
        {
            var current = cursor.Current;
            AddSnapshot(plan, current, ref ledger);
            outputLength.ReplaceRange(
                current.LengthInBytes,
                GetReplacementLength(input, plan, current));
        }

        return outputLength;
    }

    private static void AddSnapshot(
        Utf8ReplacementPlan plan,
        Utf8OperationMatch match,
        ref Utf8ReplacementRangeLedger ledger)
    {
        ledger.Add(new Utf8ReplacementRange(match.IndexInBytes, match.LengthInBytes, match.BranchId));
        foreach (var groupNumber in plan.ReferencedCaptureGroups)
        {
            if (match.CaptureSlots is not null && match.CaptureSlots.TryGet(groupNumber, out var start, out var length))
            {
                ledger.Add(new Utf8ReplacementRange(start, length, groupNumber));
            }
            else
            {
                ledger.Add(new Utf8ReplacementRange(-1, 0, groupNumber));
            }
        }
    }

    private static int GetReplacementLength(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Utf8OperationMatch match)
    {
        var length = 0;
        foreach (var instruction in plan.Instructions)
        {
            checked
            {
                length += instruction.Kind switch
                {
                    Utf8ReplacementInstructionKind.Literal => instruction.LiteralUtf8.Length,
                    Utf8ReplacementInstructionKind.Group when instruction.GroupNumber == 0 => match.LengthInBytes,
                    Utf8ReplacementInstructionKind.Group when match.CaptureSlots is not null &&
                        match.CaptureSlots.TryGet(instruction.GroupNumber, out _, out var captureLength) => captureLength,
                    Utf8ReplacementInstructionKind.WholeMatch => match.LengthInBytes,
                    Utf8ReplacementInstructionKind.LeftPortion => match.IndexInBytes,
                    Utf8ReplacementInstructionKind.RightPortion => input.Length - (match.IndexInBytes + match.LengthInBytes),
                    Utf8ReplacementInstructionKind.WholeString => input.Length,
                    _ => 0,
                };
            }
        }

        return length;
    }

    private static byte[] EmitAllocated(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Utf8ReplacementOutputLength outputLength,
        ref Utf8ReplacementRangeLedger ledger)
    {
        if (ledger.Count == 0)
        {
            return input.ToArray();
        }

        var output = new byte[outputLength.Value];
        Emit(input, plan, output, ledger.WrittenRanges);
        return output;
    }

    private static bool TryEmit(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
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

        Emit(input, plan, destination, ledger.WrittenRanges);
        bytesWritten = outputLength.Value;
        return true;
    }

    private static void Emit(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Span<byte> destination,
        ReadOnlySpan<Utf8ReplacementRange> ranges)
    {
        var stride = plan.ReferencedCaptureGroups.Length + 1;
        var sourcePosition = 0;
        var sink = new Utf8ReplacementOutputSink(destination);
        for (var offset = 0; offset < ranges.Length; offset += stride)
        {
            var snapshot = new Utf8ReplacementMatchSnapshot(
                ranges[offset],
                ranges.Slice(offset + 1, stride - 1));
            sink.AppendSlice(input, sourcePosition, snapshot.Match.Start - sourcePosition);
            AppendReplacement(ref sink, input, plan, snapshot);
            sourcePosition = snapshot.Match.Start + snapshot.Match.Length;
        }

        sink.Append(input[sourcePosition..]);
    }

    private static void AppendReplacement(
        ref Utf8ReplacementOutputSink sink,
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Utf8ReplacementMatchSnapshot match)
    {
        foreach (var instruction in plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    if (instruction.LiteralUtf8.Length > 0)
                    {
                        sink.Append(instruction.LiteralUtf8);
                    }
                    break;

                case Utf8ReplacementInstructionKind.Group when instruction.GroupNumber == 0:
                case Utf8ReplacementInstructionKind.WholeMatch:
                    sink.AppendSlice(input, match.Match.Start, match.Match.Length);
                    break;

                case Utf8ReplacementInstructionKind.Group:
                    if (match.TryGetCapture(instruction.GroupNumber, out var capture))
                    {
                        sink.AppendSlice(input, capture.Start, capture.Length);
                    }
                    break;

                case Utf8ReplacementInstructionKind.LeftPortion:
                    sink.Append(input[..match.Match.Start]);
                    break;

                case Utf8ReplacementInstructionKind.RightPortion:
                    sink.Append(input[(match.Match.Start + match.Match.Length)..]);
                    break;

                case Utf8ReplacementInstructionKind.WholeString:
                    sink.Append(input);
                    break;

                default:
                    throw new InvalidOperationException("Unsupported native replacement instruction kind.");
            }
        }
    }

    private readonly ref struct Utf8ReplacementMatchSnapshot
    {
        private readonly ReadOnlySpan<Utf8ReplacementRange> _captures;

        internal Utf8ReplacementMatchSnapshot(
            Utf8ReplacementRange match,
            ReadOnlySpan<Utf8ReplacementRange> captures)
        {
            Match = match;
            _captures = captures;
        }

        internal Utf8ReplacementRange Match { get; }

        internal bool TryGetCapture(int groupNumber, out Utf8ReplacementRange capture)
        {
            foreach (var candidate in _captures)
            {
                if (candidate.Tag == groupNumber && candidate.IsSet)
                {
                    capture = candidate;
                    return true;
                }
            }

            capture = default;
            return false;
        }
    }
}
