using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex;

/// <summary>Owned replacement over original UTF-16 coordinates.</summary>
public sealed partial class Utf8Regex
{
    private Utf8SliceProgram? GetOwnedReplacementProgram(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement)
    {
        if ((GetOwnedSliceProgram(input) ?? (input.IsEmpty ? _unitSliceProgram : null)) is { } program) return program;
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
            return _unitSliceProgram;
        }
        return null;
    }

    private Utf8SliceUtf16Result OwnedSliceReplace(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        Utf8AnalyzedReplacement replacement, Utf8SliceUtf16ResultKind resultKind, Span<byte> destination, CancellationToken token)
    {
        var state = 0;
        return OwnedUnitReplace(input, program, new Utf8SliceTemplateExpansion(replacement, program.GroupCount),
            ref state, resultKind, destination, token);
    }

    private Utf8SliceUtf16Result OwnedUnitReplace<TState, TExpansion>(ReadOnlySpan<byte> input,
        Utf8SliceProgram program, TExpansion expansion, ref TState state, Utf8SliceUtf16ResultKind resultKind,
        Span<byte> destination, CancellationToken token)
        where TExpansion : struct, IUtf8SliceUtf16Expansion<TState>
    {
        var validation = Utf8Validation.Validate(input);
        var map = Utf8BoundaryMap.Create(input, validation);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        var subject = Utf8SliceUnitSubject.Create(input, map, ref invocation);
        var slots = new Utf8SliceSlots([], []);
        var writer = new Utf8SliceUtf16OutputWriter();
        var decodedReservation = (ulong)map.Utf16Length * 2;
        var decodedAdmitted = false;
        try
        {
            if (!invocation.Ledger.TryReserve(decodedReservation))
                throw new InvalidOperationException("Owned replacement input exceeded the invocation storage budget.");
            decodedAdmitted = true;
            var decoded = Encoding.UTF8.GetString(input);
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var unit = program.RightToLeft ? map.Utf16Length : 0;
            var edge = unit;
            var anchor = unit;
            while (unit >= 0 && unit <= map.Utf16Length)
            {
                invocation.AnchorOriginByte = subject.Map.Resolve(anchor).ByteOffset;
                var outcome = Utf8SliceRunner.TryFindFirstDetailed(program, subject.Input, subject.Map,
                    unit, slots.Starts, slots.Ends, ref invocation);
                try
                {
                    if (outcome.Breach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Owned replacement execution exceeded the invocation budget.");
                    if (!outcome.Found) break;
                    if (outcome.CaptureStarts is null || outcome.CaptureLengths is null)
                        throw new InvalidOperationException("Owned replacement match is missing capture histories.");
                    if (!program.RightToLeft)
                        writer.Append(decoded.AsSpan(edge, outcome.StartUnit - edge), ref invocation);
                    expansion.Append(input, decoded, map, subject.Map, outcome, ref writer, ref state, ref invocation);
                    if (program.RightToLeft)
                    {
                        writer.Append(decoded.AsSpan(outcome.EndUnit, edge - outcome.EndUnit), ref invocation);
                        writer.MarkPiece(ref invocation);
                        edge = outcome.StartUnit;
                        unit = outcome.EndUnit > outcome.StartUnit ? outcome.StartUnit : outcome.EndUnit - 1;
                    }
                    else
                    {
                        edge = outcome.EndUnit;
                        unit = outcome.EndUnit > outcome.StartUnit ? outcome.EndUnit : outcome.StartUnit + 1;
                    }
                    if (outcome.EndUnit > outcome.StartUnit) anchor = edge;
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
            subject.Release(ref invocation);
            if (decodedAdmitted) invocation.Ledger.Release(decodedReservation);
        }
    }
}
