using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Replacement;

/// <summary>Expands a template directly from scalar match/capture byte ranges.</summary>
internal readonly struct Utf8SliceTemplateExpansion(Utf8AnalyzedReplacement replacement, Utf8CaptureCatalog captureCatalog)
{
    public void Append(ReadOnlySpan<byte> input, Utf8SliceSearchOutcome outcome,
        in Utf8SliceSlots slots, Utf8SliceByteOutputBuffer writer, ref Utf8SliceInvocation invocation)
    {
        foreach (var instruction in replacement.Plan.Instructions)
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    writer.Append(instruction.LiteralUtf8);
                    break;
                case Utf8ReplacementInstructionKind.WholeMatch:
                    writer.Append(input.Slice(outcome.StartByte, outcome.EndByte - outcome.StartByte));
                    break;
                case Utf8ReplacementInstructionKind.Group:
                    AppendGroup(captureCatalog.OrdinalFromNumber(instruction.GroupNumber), input, outcome, slots, writer);
                    break;
                case Utf8ReplacementInstructionKind.LastGroup:
                    AppendGroup(captureCatalog.GroupNumbers.Length - 1, input, outcome, slots, writer);
                    break;
                case Utf8ReplacementInstructionKind.LeftPortion:
                    writer.Append(input[..outcome.StartByte]);
                    break;
                case Utf8ReplacementInstructionKind.RightPortion:
                    writer.Append(input[outcome.EndByte..]);
                    break;
                case Utf8ReplacementInstructionKind.WholeString:
                    writer.Append(input);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported replacement instruction kind.");
            }
        }
    }

    private static void AppendGroup(int group, ReadOnlySpan<byte> input, Utf8SliceSearchOutcome outcome,
        in Utf8SliceSlots slots, Utf8SliceByteOutputBuffer writer)
    {
        if (group == 0)
        {
            writer.Append(input.Slice(outcome.StartByte, outcome.EndByte - outcome.StartByte));
            return;
        }
        var slot = group - 1;
        if ((uint)slot >= (uint)slots.Count) return;
        var start = slots.Starts[slot];
        var end = slots.Ends[slot];
        if (start < 0 || end < 0) return;
        writer.Append(input.Slice(Math.Min(start, end), Math.Abs(end - start)));
    }
}

internal enum Utf8SliceReplacementResultKind { Bytes, Destination }

/// <summary>One completed output form; destination storage remains caller-owned.</summary>
internal readonly record struct Utf8SliceReplacementResult(byte[] Bytes, System.Buffers.OperationStatus Status, int BytesWritten);
