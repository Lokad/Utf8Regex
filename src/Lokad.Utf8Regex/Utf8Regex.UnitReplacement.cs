using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex;

/// <summary>Owned replacement over original UTF-16 coordinates.</summary>
public sealed partial class Utf8Regex
{
    private Utf8SliceProgram? GetOwnedReplacementProgram(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement, CancellationToken token)
    {
        if ((GetOwnedSliceProgram(input, token) ?? (input.IsEmpty ? _ownedSliceProgram : null)) is { } program) return program;
        // These branches would otherwise construct BCL before reaching the
        // ordinary native/template routes. The owned program carries deadlines
        // and cancellation for every admitted shape.
        if (_ownedSliceProgram is not null &&
            (UsesRightToLeft() || _ownedSliceProgram.HasContiguousAnchor ||
             ShouldUseFallbackForSimplePatternReplacement(replacement, input)))
            return _ownedSliceProgram;
        // Native byte splicers may encode literals separately. Retain a half
        // supplied by the template until it can pair with the adjacent fragment.
        var text = replacement.OriginalText;
        for (var unit = 0; unit < text.Length; unit++)
        {
            if (!char.IsSurrogate(text[unit])) continue;
            if (char.IsHighSurrogate(text[unit]) && unit + 1 < text.Length && char.IsLowSurrogate(text[unit + 1]))
            {
                unit++;
                continue;
            }
            return _ownedSliceProgram;
        }
        return null;
    }

    private Utf8SliceUtf16Result OwnedSliceReplace(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        Utf8AnalyzedReplacement replacement, Utf8SliceUtf16ResultKind resultKind, Span<byte> destination, CancellationToken token)
    {
        var state = 0;
        return OwnedUnitReplace(input, program, new Utf8SliceTemplateExpansion(replacement, program),
            ref state, resultKind, destination, token);
    }

    private Utf8SliceUtf16Result OwnedUnitReplace<TState, TExpansion>(ReadOnlySpan<byte> input,
        Utf8SliceProgram program, TExpansion expansion, ref TState state, Utf8SliceUtf16ResultKind resultKind,
        Span<byte> destination, CancellationToken token)
        where TExpansion : struct, IUtf8SliceUtf16Expansion<TState>
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        var validation = Utf8Validation.Validate(input, ref invocation);
        var map = Utf8BoundaryMap.Create(input, validation, ref invocation, out var mapReservation);
        var slots = new Utf8SliceSlots([], []);
        var writer = new Utf8SliceUtf16OutputWriter();
        var decodedReservation = (ulong)map.Utf16Length * 2;
        var decodedAdmitted = false;
        try
        {
            if (!invocation.Ledger.TryReserve(decodedReservation))
                throw new InvalidOperationException("Owned replacement input exceeded the invocation storage budget.");
            decodedAdmitted = true;
            var decoded = Utf8SliceBufferWork.DecodeUtf8(input, map.Utf16Length, ref invocation);
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var position = program.RightToLeft ? input.Length : 0;
            var edge = program.RightToLeft ? map.Utf16Length : 0;
            var anchor = position;
            while (position >= 0)
            {
                invocation.AnchorOriginByte = anchor;
                var outcome = Utf8SliceRunner.TryFindFirst(program, input,
                    position, slots.Starts, slots.Ends, true, ref invocation);
                try
                {
                    if (outcome.Breach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Owned replacement execution exceeded the invocation budget.");
                    if (!outcome.Found) break;
                    if (outcome.CaptureStarts is null || outcome.CaptureLengths is null)
                        throw new InvalidOperationException("Owned replacement match is missing capture histories.");
                    var startUnit = map.GetUtf16OffsetForByteOffset(outcome.StartByte);
                    var endUnit = map.GetUtf16OffsetForByteOffset(outcome.EndByte);
                    if (!program.RightToLeft)
                        writer.Append(decoded.AsSpan(edge, startUnit - edge), ref invocation);
                    expansion.Append(input, decoded, map, map, outcome, ref writer, ref state, ref invocation);
                    if (program.RightToLeft)
                    {
                        writer.Append(decoded.AsSpan(endUnit, edge - endUnit), ref invocation);
                        writer.MarkPiece(ref invocation);
                        edge = startUnit;
                    }
                    else
                    {
                        edge = endUnit;
                    }
                    var closingEdge = program.RightToLeft ? outcome.StartByte : outcome.EndByte;
                    position = outcome.EndByte > outcome.StartByte ? closingEdge : Utf8SliceRunner.AdvancePastScalar(input, closingEdge, program.RightToLeft);
                    if (outcome.EndByte > outcome.StartByte) anchor = closingEdge;
                }
                finally { Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref invocation); }
            }
            if (program.RightToLeft)
            {
                writer.Append(decoded.AsSpan(0, edge), ref invocation);
                writer.MarkPiece(ref invocation);
                writer.ReversePieces(ref invocation);
            }
            else writer.Append(decoded.AsSpan(edge), ref invocation);
            switch (resultKind)
            {
                case Utf8SliceUtf16ResultKind.Text:
                    return new(writer.ToString(ref invocation), [], System.Buffers.OperationStatus.Done, 0);
                case Utf8SliceUtf16ResultKind.Bytes:
                    return new(string.Empty, writer.ToUtf8(ref invocation), System.Buffers.OperationStatus.Done, 0);
                case Utf8SliceUtf16ResultKind.Destination:
                    var status = writer.CopyUtf8(destination, out var written, ref invocation);
                    return new(string.Empty, [], status, written);
                default:
                    throw new InvalidOperationException("Unknown owned output kind.");
            }
        }
        finally
        {
            writer.Release(ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
            if (decodedAdmitted) invocation.Ledger.Release(decodedReservation);
            invocation.Ledger.Release(mapReservation);
        }
    }

    private byte[] OwnedUnitReplaceEvaluator<TState>(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        TState state, Utf8MatchEvaluator<TState> evaluator, CancellationToken token)
    {
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        ref var invocation = ref scope.Invocation;
        var slots = new Utf8SliceSlots([], []);
        ulong mapReservation = 0;
        try
        {
            var validation = Utf8Validation.Validate(input, ref invocation);
            var map = Utf8BoundaryMap.Create(input, validation, ref invocation, out mapReservation);
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var writer = new Utf8ReplacementWriter(scope.CreateBuffer());
            var position = program.RightToLeft ? input.Length : 0;
            var edgeByte = position;
            var anchor = position;
            while (position >= 0)
            {
                invocation.AnchorOriginByte = anchor;
                var outcome = Utf8SliceRunner.TryFindFirst(program, input,
                    position, slots.Starts, slots.Ends, true, ref invocation);
                try
                {
                    if (outcome.Breach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Owned replacement execution exceeded the invocation budget.");
                    if (!outcome.Found) break;
                    if (!program.RightToLeft) writer.Append(input.Slice(edgeByte, outcome.StartByte - edgeByte));
                    var projection = Utf8SliceCaptureProjection.Create(outcome, map, ref invocation);
                    try
                    {
                        var context = projection.CreateContext(input, null, map, program);
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
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
            invocation.Ledger.Release(mapReservation);
            scope.Release();
        }
    }
}
