using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Replacement;

/// <summary>Expands one scalar match into bytes without transferring invocation ownership.</summary>
internal interface IUtf8SliceReplacementExpansion<TState>
{
    bool RequiresContext { get; }
    void Append(ReadOnlySpan<byte> input, Utf8BoundaryMap map, Utf8SliceSearchOutcome outcome,
        in Utf8SliceSlots slots, Utf8SliceByteOutputBuffer writer, ref TState state, ref Utf8SliceInvocation invocation);
}

internal readonly struct Utf8SliceTemplateExpansion(Utf8AnalyzedReplacement replacement, Utf8CaptureCatalog captureCatalog)
    : IUtf8SliceReplacementExpansion<int>
{
    public bool RequiresContext => false;

    public void Append(ReadOnlySpan<byte> input, Utf8BoundaryMap map, Utf8SliceSearchOutcome outcome,
        in Utf8SliceSlots slots, Utf8SliceByteOutputBuffer writer, ref int state, ref Utf8SliceInvocation invocation)
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
        if ((uint)slot >= (uint)slots.Starts.Length) return;
        var start = slots.Starts[slot];
        var end = slots.Ends[slot];
        if (start < 0 || end < 0) return;
        writer.Append(input.Slice(Math.Min(start, end), Math.Abs(end - start)));
    }
}

internal readonly struct Utf8SliceEvaluatorExpansion<TState>(Utf16MatchEvaluator<TState> evaluator, Utf8CaptureCatalog captureCatalog)
    : IUtf8SliceReplacementExpansion<TState>
{
    public bool RequiresContext => true;

    public void Append(ReadOnlySpan<byte> input, Utf8BoundaryMap map, Utf8SliceSearchOutcome outcome,
        in Utf8SliceSlots slots, Utf8SliceByteOutputBuffer writer, ref TState state, ref Utf8SliceInvocation invocation)
    {
        var projection = Utf8SliceCaptureProjection.Create(outcome, map, ref invocation);
        try
        {
            var context = projection.CreateContext(input, map, captureCatalog);
            var result = evaluator(in context, ref state);
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            writer.Append(result.AsSpan());
        }
        finally { projection.Release(ref invocation); }
    }
}

internal enum Utf8SliceReplacementResultKind { Text, Bytes, Destination }

/// <summary>One completed output form; destination storage remains caller-owned.</summary>
internal readonly record struct Utf8SliceReplacementResult(string Text, byte[] Bytes, System.Buffers.OperationStatus Status, int BytesWritten);
