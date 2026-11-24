using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    /// <summary>Finds a scalar match and optionally writes its last capture ranges into caller-owned slots.</summary>
    /// <remarks>A null start selects zero forward or the selected end in right-to-left mode. Compact slots include zero; GroupNumberFromCaptureSlot resolves each group number. Short buffers do not change matching; excess entries remain untouched. Pre-cancellation leaves output unchanged; failure after capture entry clears written slots. The optional end retains prefix/lookbehind context. Capture history remains available through CreateMatchCursor. Keep borrowed bytes valid and unchanged.</remarks>
    public Utf8CaptureRange Match(ReadOnlySpan<byte> input, int? startOffsetInBytes = null,
        Span<Utf8CaptureRange> captures = default, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null, int? endOffsetInBytes = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var programMemory = _programMemory;
        if (startOffsetInBytes is null && captures.IsEmpty &&
            workingMemoryAllowance is null && endOffsetInBytes is null && programMemory is null)
        {
            try { return MatchCore(input, cancellationToken); }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        }
        programMemory?.AcquireUse();
        try
        {
            var start = startOffsetInBytes ?? (UsesRightToLeft() ? endOffsetInBytes ?? input.Length : 0);
            return MatchCaptureSlotsCore(input, start, captures, cancellationToken,
                workingMemoryAllowance ?? programMemory?.Allowance, endOffsetInBytes);
        }
        finally { programMemory?.ReleaseUse(); }
    }

    private Utf8CaptureRange MatchCaptureSlotsCore(ReadOnlySpan<byte> input, int startOffsetInBytes,
        Span<Utf8CaptureRange> captures, CancellationToken cancellationToken,
        IUtf8MemoryAllowance? workingMemoryAllowance, int? endOffsetInBytes)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = captures[..Math.Min(captures.Length, _program.CaptureCatalog.GroupNumbers.Length)];
        // This synchronous call passes the invocation by reference through
        // search, capture replay and publication.
        Utf8SliceInvocation.InitializeExclusive(out var invocation, InvocationLimits, MatchTimeout,
            cancellationToken, workingMemoryAllowance);
        var slots = Utf8SliceSlots.Empty;
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var end = endOffsetInBytes ?? input.Length;
            if (!Utf8ValidatedInput.IsScalarBoundary(input, end))
                throw new ArgumentOutOfRangeException(nameof(endOffsetInBytes));
            var subject = input[..end];
            if (!Utf8ValidatedInput.IsScalarBoundary(subject, startOffsetInBytes))
                throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
            var program = _ownedSliceProgram;
            invocation.AnchorOriginByte = startOffsetInBytes;
            Utf8CaptureRange result;
            if (program.GroupCount == 0 && CanUseNativeLiteralSearch(program, maximumAlternatives: 8))
            {
                // Reuse bounded literal-family search for explicit byte matches.
                var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
                    subject, startOffsetInBytes, subject.Length, false, ref invocation, out var selected, out var found);
                if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                result = found ? new(selected.Index, selected.Index + selected.Length) : default;
            }
            else if (program.IsNonBacktracking && output.Length > 1 && subject.Length - startOffsetInBytes >= 64 &&
                program.NonBacktrackingOrderedClosures.Length != 0)
            {
                var selected = Utf8SliceRunner.TryFindFirst(program, subject, startOffsetInBytes,
                    [], [], false, ref invocation);
                if (selected.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                result = default;
                if (selected.Found)
                {
                    slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
                    var replay = Utf8SliceRunner.ReplayOrderedRegularRange(program, subject, selected, slots, ref invocation);
                    if (replay.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                    result = new(selected.StartByte, selected.EndByte);
                }
            }
            else
            {
                var slotCount = program.GroupCount;
                if (output.Length <= 1)
                    slotCount = GetValueOperationCaptureSlotCount(program);
                else if (!program.IsNonBacktracking && !program.ReadsCaptures)
                    slotCount = output.Length - 1;
                slots = Utf8SliceRunner.RentSlots(slotCount, ref invocation);
                var outcome = Utf8SliceRunner.TryFindFirst(program, subject, startOffsetInBytes,
                    slots.Starts, slots.Ends, false, ref invocation, _preparedRegex.SearchPlan);
                if (outcome.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                result = outcome.Found ? new(outcome.StartByte, outcome.EndByte) : default;
            }
            for (var ordinal = 0; ordinal < output.Length; ordinal++)
            {
                if ((ordinal & 255) == 0) Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                if (!result.Success || ordinal == 0)
                {
                    output[ordinal] = result;
                    continue;
                }
                // A forward search can capture inside a mirrored lookbehind.
                // Its execution slots contain opening and closing edges.
                var opening = slots.Starts[ordinal - 1];
                var closing = slots.Ends[ordinal - 1];
                output[ordinal] = opening < 0 || closing < 0 ? default :
                    new(Math.Min(opening, closing), Math.Max(opening, closing));
            }
            FinishFundedInvocation(ref invocation);
            return result;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { output.Clear(); throw invocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            output.Clear();
            throw CreateMatchTimeoutException(workingMemoryAllowance is null ? input : default,
                Pattern, MatchTimeout, invocation.Budget.Limits);
        }
        catch { output.Clear(); throw; }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
            invocation.Ledger.Release(invocation.Ledger.ReservedBytes);
        }
    }
}
