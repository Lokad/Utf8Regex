using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Replacement;

/// <summary>Expands one scalar match into bytes without transferring invocation ownership.</summary>
internal interface IUtf8SliceReplacementExpansion<TState>
{
    bool RequiresContext { get; }
    void Append(ReadOnlySpan<byte> input, Utf8BoundaryMap map, Utf8SliceSearchOutcome outcome,
        Utf8SliceByteOutputBuffer writer, ref TState state, ref Utf8SliceInvocation invocation);
}

internal readonly struct Utf8SliceTemplateExpansion(Utf8AnalyzedReplacement replacement, Utf8SliceProgram program)
    : IUtf8SliceReplacementExpansion<int>
{
    public bool RequiresContext => false;

    public void Append(ReadOnlySpan<byte> input, Utf8BoundaryMap map, Utf8SliceSearchOutcome outcome,
        Utf8SliceByteOutputBuffer writer, ref int state, ref Utf8SliceInvocation invocation)
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
                    AppendGroup(program.CaptureCatalog?.OrdinalFromNumber(instruction.GroupNumber) ?? instruction.GroupNumber, input, outcome, writer);
                    break;
                case Utf8ReplacementInstructionKind.LastGroup:
                    AppendGroup(program.GroupCount, input, outcome, writer);
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

    private static void AppendGroup(int group, ReadOnlySpan<byte> input, Utf8SliceSearchOutcome outcome, Utf8SliceByteOutputBuffer writer)
    {
        if (group == 0)
        {
            writer.Append(input.Slice(outcome.StartByte, outcome.EndByte - outcome.StartByte));
            return;
        }
        if (outcome.CaptureStarts is not { } starts || outcome.CaptureLengths is not { } lengths ||
            (uint)(group - 1) >= (uint)starts.Length || starts[group - 1].Length == 0) return;
        writer.Append(input.Slice(starts[group - 1][^1], lengths[group - 1][^1]));
    }
}

internal readonly struct Utf8SliceEvaluatorExpansion<TState>(Utf16MatchEvaluator<TState> evaluator, Utf8SliceProgram program)
    : IUtf8SliceReplacementExpansion<TState>
{
    public bool RequiresContext => true;

    public void Append(ReadOnlySpan<byte> input, Utf8BoundaryMap map, Utf8SliceSearchOutcome outcome,
        Utf8SliceByteOutputBuffer writer, ref TState state, ref Utf8SliceInvocation invocation)
    {
        var projection = Utf8SliceCaptureProjection.Create(outcome, map, ref invocation);
        try
        {
            var context = projection.CreateContext(input, null, map, program);
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
