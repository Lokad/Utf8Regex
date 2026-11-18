using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex;

/// <summary>Owned replacement over original scalar-aligned UTF-8 byte ranges.</summary>
public sealed partial class Utf8Regex
{
    private Utf8SliceByteOutputScope CreateOwnedOutputScope(Utf8SliceInvocation invocation)
    {
        try { return Utf8SliceByteOutputScope.Enter(invocation); }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw CreateExecutionResourceFailure(ref invocation); }
    }

    private Utf8SliceProgram? GetOwnedReplacementProgram(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement, CancellationToken token)
    {
        // Non-literal shape replacements use the existing byte template engine.
        // Select it before preparation; no legacy capture view or second subject
        // invocation is needed. Genuine literal replacement routes remain below.
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex)
            return _ownedSliceProgram;
        if ((GetOwnedSliceProgram(input, token) ?? (input.IsEmpty ? _ownedSliceProgram : null)) is { } program) return program;
        // The native literal plans preserve scalar endpoints. Stateful cursor
        // shapes use the admitted scalar program with the original token.
        if (UsesRightToLeft() || _ownedSliceProgram.HasContiguousAnchor ||
             replacement.ContainsGroupReferences &&
                (MatchTimeout != Regex.InfiniteMatchTimeout || _hasCaptures && ShouldUseKelvinSignFallback(input)))
            return _ownedSliceProgram;
        return null;
    }

    private Utf8SliceReplacementResult OwnedSliceReplace(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        Utf8AnalyzedReplacement replacement, Utf8SliceReplacementResultKind resultKind, Span<byte> destination, CancellationToken token,
        Utf8ExecutionDeadline deadline)
    {
        var initialInvocation = new Utf8SliceInvocation(InvocationLimits, deadline, token);
        return OwnedSliceReplace(input, program, replacement, resultKind, destination, initialInvocation);
    }

    private Utf8SliceReplacementResult OwnedSliceReplace(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        Utf8AnalyzedReplacement replacement, Utf8SliceReplacementResultKind resultKind, Span<byte> destination,
        Utf8SliceInvocation initialInvocation)
    {
        var expansion = new Utf8SliceTemplateExpansion(replacement, _program.CaptureCatalog);
        var scope = CreateOwnedOutputScope(initialInvocation);
        ref var invocation = ref scope.Invocation;
        var slots = new Utf8SliceSlots([], []);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        Utf8NonBacktrackingSearchState? batch = null;
        var boundary = 0;
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            if (program.IsNonBacktracking)
            {
                var breach = Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out batch);
                if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned replacement preparation exceeded {breach}.");
            }
            var writer = scope.CreateBuffer();
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var position = program.RightToLeft ? input.Length : 0;
            var edgeByte = position;
            var anchor = position;
            var anyMatch = false;
            var guideLiteralStarts = batch is null && ReferenceEquals(program, _ownedSliceProgram) &&
                CanFindNativeLiteralForResult;
            while (position >= 0)
            {
                invocation.AnchorOriginByte = anchor;
                // Reuse complete literal discovery only to skip impossible starts.
                // Scalar verification still owns captures and their histories.
                if (guideLiteralStarts && TryFindNativeLiteralForResult(input, position, ref invocation,
                    out var found, out var candidateStart, out _))
                {
                    if (!found) break;
                    position = candidateStart;
                }
                var outcome = batch is null
                    ? Utf8SliceRunner.TryFindFirst(program, input, position, slots.Starts, slots.Ends, false, ref workspace, ref invocation)
                    : batch.FindFirst(program, ref boundary, slots.Starts, slots.Ends, false, ref invocation);
                try
                {
                    if (outcome.Breach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Owned replacement execution exceeded the invocation budget.");
                    if (!outcome.Found) break;
                    anyMatch = true;
                    if (!program.RightToLeft) writer.Append(input.Slice(edgeByte, outcome.StartByte - edgeByte));
                    expansion.Append(input, outcome, slots, writer, ref invocation);
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
            // Discovery has finished. Avoid retaining scratch into final-output
            // overlap; finally also covers failures and interruption.
            workspace.Release(ref invocation);
            if (!anyMatch)
                return CompleteUnchangedOwnedReplacement(input, resultKind, destination, ref invocation);
            if (program.RightToLeft)
            {
                writer.Append(input.Slice(0, edgeByte));
                writer.MarkPiece();
                writer.ReversePieces();
            }
            else writer.Append(input[edgeByte..]);
            switch (resultKind)
            {
                case Utf8SliceReplacementResultKind.Bytes:
                    return new(writer.ToArray(), System.Buffers.OperationStatus.Done, 0);
                case Utf8SliceReplacementResultKind.Destination:
                    var status = writer.CopyUtf8(destination, out var written);
                    return new([], status, written);
                default:
                    throw new InvalidOperationException("Unknown owned output kind.");
            }
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        finally
        {
            workspace.Release(ref invocation);
            batch?.Release(ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
            scope.Release();
        }
    }

    // Subject preparation is complete. A miss needs only final bytes; keep
    // the same scope/authority without staging an unchanged subject.
    private static Utf8SliceReplacementResult CompleteUnchangedOwnedReplacement(
        ReadOnlySpan<byte> input, Utf8SliceReplacementResultKind resultKind,
        Span<byte> destination, ref Utf8SliceInvocation invocation)
    {
        Utf8InvocationResultLedger results = default;
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            switch (resultKind)
            {
                case Utf8SliceReplacementResultKind.Bytes:
                    var bytes = results.AllocateArray<byte>(input.Length, ref invocation);
                    Utf8SliceBufferWork.Copy(input, bytes, ref invocation);
                    return new(bytes, System.Buffers.OperationStatus.Done, 0);
                case Utf8SliceReplacementResultKind.Destination:
                    if (destination.Length < input.Length)
                        return new([], System.Buffers.OperationStatus.DestinationTooSmall, 0);
                    Utf8SliceBufferWork.Copy(input, destination, ref invocation);
                    return new([], System.Buffers.OperationStatus.Done, input.Length);
                default:
                    throw new InvalidOperationException("Unknown owned output kind.");
            }
        }
        finally { results.Release(ref invocation); }
    }

}
