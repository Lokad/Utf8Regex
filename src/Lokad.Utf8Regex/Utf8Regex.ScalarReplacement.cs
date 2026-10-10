using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex;

/// <summary>Owned replacement over original scalar-aligned UTF-8 byte ranges.</summary>
public sealed partial class Utf8Regex
{
    private Utf8SliceProgram? GetOwnedReplacementProgram(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement, CancellationToken token)
    {
        if ((GetOwnedSliceProgram(input, token) ?? (input.IsEmpty ? _ownedSliceProgram : null)) is { } program) return program;
        // Native replacement routes need complete scalar endpoints. Unsupported
        // native cursor shapes use the admitted scalar program with the original token.
        if (UsesRightToLeft() || !CanUseScalarAlignedNativeReplacement(input) || _ownedSliceProgram.HasContiguousAnchor ||
             // These structural shapes have no qualified native match cursor.
             _preparedRegex.ExecutionKind is NativeExecutionKind.AsciiStructuralTokenWindow or
                NativeExecutionKind.AsciiStructuralRepeatedSegment or NativeExecutionKind.AsciiStructuralQuotedRelation ||
             RequiresScalarSimplePatternReplacement(replacement, input))
            return _ownedSliceProgram;
        return null;
    }

    private Utf8SliceReplacementResult OwnedSliceReplace(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        Utf8AnalyzedReplacement replacement, Utf8SliceReplacementResultKind resultKind, Span<byte> destination, CancellationToken token)
    {
        var state = 0;
        return OwnedSliceReplace(input, program, new Utf8SliceTemplateExpansion(replacement, _program.CaptureCatalog),
            ref state, resultKind, destination, token);
    }

    private Utf8SliceReplacementResult OwnedSliceReplace<TState, TExpansion>(ReadOnlySpan<byte> input,
        Utf8SliceProgram program, TExpansion expansion, ref TState state, Utf8SliceReplacementResultKind resultKind,
        Span<byte> destination, CancellationToken token)
        where TExpansion : struct, IUtf8SliceReplacementExpansion<TState>
    {
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        ref var invocation = ref scope.Invocation;
        var slots = new Utf8SliceSlots([], []);
        ulong mapReservation = 0;
        Utf8NonBacktrackingSearchState? batch = null;
        var boundary = 0;
        try
        {
            var validation = Utf8Validation.Validate(input, ref invocation);
            if (program.IsNonBacktracking)
            {
                var breach = Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out batch);
                if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned replacement preparation exceeded {breach}.");
            }
            var map = default(Utf8BoundaryMap);
            var mapCreated = false;
            var writer = scope.CreateBuffer();
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var position = program.RightToLeft ? input.Length : 0;
            var edgeByte = position;
            var anchor = position;
            while (position >= 0)
            {
                invocation.AnchorOriginByte = anchor;
                var outcome = batch is null
                    ? Utf8SliceRunner.TryFindFirst(program, input, position, slots.Starts, slots.Ends, true, ref invocation)
                    : batch.FindFirst(program, ref boundary, slots.Starts, slots.Ends, true, ref invocation);
                try
                {
                    if (outcome.Breach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Owned replacement execution exceeded the invocation budget.");
                    if (!outcome.Found) break;
                    if (outcome.CaptureStarts is null || outcome.CaptureLengths is null)
                        throw new InvalidOperationException("Owned replacement match is missing capture histories.");
                    // Only callbacks need derived UTF-16 coordinates. Templates
                    // splice original byte ranges and never build a subject map.
                    if (expansion.RequiresContext && !mapCreated)
                    {
                        map = Utf8BoundaryMap.Create(input, validation, ref invocation, out mapReservation);
                        mapCreated = true;
                    }
                    if (!program.RightToLeft) writer.Append(input.Slice(edgeByte, outcome.StartByte - edgeByte));
                    expansion.Append(input, map, outcome, writer, ref state, ref invocation);
                    scope.Retain(writer);
                    if (program.RightToLeft)
                    {
                        writer.Append(input.Slice(outcome.EndByte, edgeByte - outcome.EndByte));
                        writer.MarkPiece();
                        edgeByte = outcome.StartByte;
                    }
                    else edgeByte = outcome.EndByte;
                    position = outcome.EndByte > outcome.StartByte ? edgeByte : Utf8SliceRunner.AdvancePastScalar(input, edgeByte, program.RightToLeft);
                    if (outcome.EndByte > outcome.StartByte) anchor = edgeByte;
                }
                finally { Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref invocation); }
            }
            if (program.RightToLeft)
            {
                writer.Append(input.Slice(0, edgeByte));
                writer.MarkPiece();
                writer.ReversePieces();
            }
            else writer.Append(input[edgeByte..]);
            switch (resultKind)
            {
                case Utf8SliceReplacementResultKind.Text:
                    return new(writer.ToValidatedString(), [], System.Buffers.OperationStatus.Done, 0);
                case Utf8SliceReplacementResultKind.Bytes:
                    return new(string.Empty, writer.ToArray(), System.Buffers.OperationStatus.Done, 0);
                case Utf8SliceReplacementResultKind.Destination:
                    var status = writer.CopyUtf8(destination, out var written);
                    return new(string.Empty, [], status, written);
                default:
                    throw new InvalidOperationException("Unknown owned output kind.");
            }
        }
        finally
        {
            batch?.Release(ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
            invocation.Ledger.Release(mapReservation);
            scope.Release();
        }
    }

    private byte[] OwnedSliceReplaceEvaluator<TState>(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        TState state, Utf8MatchEvaluator<TState> evaluator, CancellationToken token)
    {
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        ref var invocation = ref scope.Invocation;
        var slots = new Utf8SliceSlots([], []);
        ulong mapReservation = 0;
        Utf8NonBacktrackingSearchState? batch = null;
        var boundary = 0;
        try
        {
            var validation = Utf8Validation.Validate(input, ref invocation);
            if (program.IsNonBacktracking)
            {
                var breach = Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out batch);
                if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned evaluator preparation exceeded {breach}.");
            }
            var map = Utf8BoundaryMap.Create(input, validation, ref invocation, out mapReservation);
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var writer = new Utf8ReplacementWriter(scope.CreateBuffer());
            var position = program.RightToLeft ? input.Length : 0;
            var edgeByte = position;
            var anchor = position;
            while (position >= 0)
            {
                invocation.AnchorOriginByte = anchor;
                var outcome = batch is null
                    ? Utf8SliceRunner.TryFindFirst(program, input, position, slots.Starts, slots.Ends, true, ref invocation)
                    : batch.FindFirst(program, ref boundary, slots.Starts, slots.Ends, true, ref invocation);
                try
                {
                    if (outcome.Breach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Owned replacement execution exceeded the invocation budget.");
                    if (!outcome.Found) break;
                    if (!program.RightToLeft) writer.Append(input.Slice(edgeByte, outcome.StartByte - edgeByte));
                    var projection = Utf8SliceCaptureProjection.Create(outcome, map, ref invocation);
                    try
                    {
                        var context = projection.CreateContext(input, map, _program.CaptureCatalog);
                        evaluator(in context, ref writer, ref state);
                        Utf8SliceBufferWork.Checkpoint(ref invocation);
                    }
                    finally { projection.Release(ref invocation); }
                    scope.Retain(writer.OwnedOutput);
                    if (program.RightToLeft)
                    {
                        writer.Append(input.Slice(outcome.EndByte, edgeByte - outcome.EndByte));
                        writer.GetOwnedOutput(scope).MarkPiece();
                        edgeByte = outcome.StartByte;
                    }
                    else
                    {
                        edgeByte = outcome.EndByte;
                    }
                    position = outcome.EndByte > outcome.StartByte ? edgeByte : Utf8SliceRunner.AdvancePastScalar(input, edgeByte, program.RightToLeft);
                    if (outcome.EndByte > outcome.StartByte) anchor = edgeByte;
                }
                finally { Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref invocation); }
            }
            if (program.RightToLeft)
            {
                writer.Append(input.Slice(0, edgeByte));
                writer.GetOwnedOutput(scope).MarkPiece();
                writer.GetOwnedOutput(scope).ReversePieces();
            }
            else writer.Append(input[edgeByte..]);
            return writer.GetOwnedOutput(scope).ToArray();
        }
        finally
        {
            batch?.Release(ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
            invocation.Ledger.Release(mapReservation);
            scope.Release();
        }
    }
}
