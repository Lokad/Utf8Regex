using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Replacement;

/// <summary>Expands one discovered match without transferring invocation ownership.</summary>
internal interface IUtf8SliceUtf16Expansion<TState>
{
    void Append(ReadOnlySpan<byte> input, string decoded, Utf8BoundaryMap originalMap,
        Utf8BoundaryMap executionMap, Utf8SliceDetailedOutcome outcome,
        ref Utf8SliceUtf16OutputWriter writer, ref TState state, ref Utf8SliceInvocation invocation);
}

internal readonly struct Utf8SliceTemplateExpansion(Utf8AnalyzedReplacement replacement, int groupCount)
    : IUtf8SliceUtf16Expansion<int>
{
    public void Append(ReadOnlySpan<byte> input, string decoded, Utf8BoundaryMap originalMap,
        Utf8BoundaryMap executionMap, Utf8SliceDetailedOutcome outcome,
        ref Utf8SliceUtf16OutputWriter writer, ref int state, ref Utf8SliceInvocation invocation)
    {
        foreach (var instruction in replacement.Plan.Instructions)
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    writer.Append(instruction.LiteralText, ref invocation);
                    break;
                case Utf8ReplacementInstructionKind.WholeMatch:
                    writer.Append(decoded.AsSpan(outcome.StartUnit, outcome.EndUnit - outcome.StartUnit), ref invocation);
                    break;
                case Utf8ReplacementInstructionKind.Group:
                    AppendGroup(instruction.GroupNumber, decoded, executionMap, outcome, ref writer, ref invocation);
                    break;
                case Utf8ReplacementInstructionKind.LastGroup:
                    AppendGroup(groupCount, decoded, executionMap, outcome, ref writer, ref invocation);
                    break;
                case Utf8ReplacementInstructionKind.LeftPortion:
                    writer.Append(decoded.AsSpan(0, outcome.StartUnit), ref invocation);
                    break;
                case Utf8ReplacementInstructionKind.RightPortion:
                    writer.Append(decoded.AsSpan(outcome.EndUnit), ref invocation);
                    break;
                case Utf8ReplacementInstructionKind.WholeString:
                    writer.Append(decoded, ref invocation);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported replacement instruction kind.");
            }
        }
    }

    private static void AppendGroup(int group, string decoded, Utf8BoundaryMap executionMap,
        Utf8SliceDetailedOutcome outcome, ref Utf8SliceUtf16OutputWriter writer, ref Utf8SliceInvocation invocation)
    {
        if (group == 0)
        {
            writer.Append(decoded.AsSpan(outcome.StartUnit, outcome.EndUnit - outcome.StartUnit), ref invocation);
            return;
        }
        if (outcome.CaptureStarts is not { } starts || outcome.CaptureLengths is not { } lengths ||
            (uint)(group - 1) >= (uint)starts.Length || starts[group - 1].Length == 0) return;
        var start = starts[group - 1][^1];
        var length = lengths[group - 1][^1];
        var unit = executionMap.GetUtf16OffsetForByteOffset(start);
        var end = executionMap.GetUtf16OffsetForByteOffset(start + length);
        writer.Append(decoded.AsSpan(unit, end - unit), ref invocation);
    }
}

internal readonly struct Utf8SliceEvaluatorExpansion<TState>(Utf16MatchEvaluator<TState> evaluator, string[] groupNames)
    : IUtf8SliceUtf16Expansion<TState>
{
    public void Append(ReadOnlySpan<byte> input, string decoded, Utf8BoundaryMap originalMap,
        Utf8BoundaryMap executionMap, Utf8SliceDetailedOutcome outcome,
        ref Utf8SliceUtf16OutputWriter writer, ref TState state, ref Utf8SliceInvocation invocation)
    {
        var projection = Utf8SliceCaptureProjection.Create(outcome, executionMap, ref invocation);
        try
        {
            var context = projection.CreateContext(input, decoded, originalMap, groupNames, outcome);
            var result = evaluator(in context, ref state);
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            writer.Append(result.AsSpan(), ref invocation);
        }
        finally { projection.Release(ref invocation); }
    }
}
