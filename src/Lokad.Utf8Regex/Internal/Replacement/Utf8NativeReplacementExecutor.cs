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
            return EmitAllocated(input, plan, outputLength, ref ledger, cursor.WorkingBufferControl);
        }
        finally
        {
            ledger.Dispose(cursor.WorkingBufferControl);
        }
    }

    private static bool CanExecute(Utf8ReplacementInstruction instruction)
    {
        return instruction.Kind switch
        {
            Utf8ReplacementInstructionKind.Literal => true,
            Utf8ReplacementInstructionKind.Group => instruction.GroupNumber == 0,
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
            ledger.Add(new Utf8ReplacementRange(current.IndexInBytes, current.LengthInBytes), cursor.WorkingBufferControl);
            outputLength.ReplaceRange(
                current.LengthInBytes,
                GetReplacementLength(input, plan, current));
        }

        return outputLength;
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
        ref Utf8ReplacementRangeLedger ledger, scoped Utf8InvocationBufferControl control)
    {
        var output = Utf8InvocationOutput.AllocateBytes(outputLength.Value, control, out var reservation);
        try
        {
            if (ledger.Count == 0) Utf8InvocationOutput.Copy(input, output, control);
            else Emit(input, plan, output, ledger.WrittenRanges, control);
            return output;
        }
        finally { control.ReleaseWorking(reservation); }
    }

    private static void Emit(
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Span<byte> destination,
        ReadOnlySpan<Utf8ReplacementRange> ranges, scoped Utf8InvocationBufferControl control)
    {
        var sourcePosition = 0;
        var sink = new Utf8ReplacementOutputSink(destination);
        foreach (var match in ranges)
        {
            sink.AppendSlice(input, sourcePosition, match.Start - sourcePosition, control);
            AppendReplacement(ref sink, input, plan, match, control);
            sourcePosition = match.Start + match.Length;
        }

        sink.Append(input[sourcePosition..], control);
    }

    private static void AppendReplacement(
        ref Utf8ReplacementOutputSink sink,
        ReadOnlySpan<byte> input,
        Utf8ReplacementPlan plan,
        Utf8ReplacementRange match, scoped Utf8InvocationBufferControl control)
    {
        foreach (var instruction in plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    if (instruction.LiteralUtf8.Length > 0)
                    {
                        sink.Append(instruction.LiteralUtf8, control);
                    }
                    break;

                case Utf8ReplacementInstructionKind.Group when instruction.GroupNumber == 0:
                case Utf8ReplacementInstructionKind.WholeMatch:
                    sink.AppendSlice(input, match.Start, match.Length, control);
                    break;

                case Utf8ReplacementInstructionKind.LeftPortion:
                    sink.Append(input[..match.Start], control);
                    break;

                case Utf8ReplacementInstructionKind.RightPortion:
                    sink.Append(input[(match.Start + match.Length)..], control);
                    break;

                case Utf8ReplacementInstructionKind.WholeString:
                    sink.Append(input, control);
                    break;

                default:
                    throw new InvalidOperationException("Unsupported native replacement instruction kind.");
            }
        }
    }

}
