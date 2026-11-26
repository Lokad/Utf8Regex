using System.Runtime.CompilerServices;
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
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var end = endOffsetInBytes ?? input.Length;
            if (!Utf8BytePosition.IsScalarBoundary(input, end))
                throw new ArgumentOutOfRangeException(nameof(endOffsetInBytes));
            var subject = input[..end];
            if (!Utf8BytePosition.IsScalarBoundary(subject, startOffsetInBytes))
                throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
            var program = _ownedSliceProgram;
            invocation.AnchorOriginByte = startOffsetInBytes;
            // Group zero is the value match. Storage for exposed capture
            // groups belongs to the detailed matcher.
            if (output.Length > 1)
                return MatchDetailedCaptureSlots(program, subject, startOffsetInBytes, output, ref invocation);

            Utf8CaptureRange result;
            if (_canUseLiteralForValueSlots)
            {
                // The bounded finder sees the prefix before the byte floor.
                // The selected end still clips the subject's right context.
                var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
                    subject, startOffsetInBytes, subject.Length, false, ref invocation, out var selected, out var found);
                if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                result = found ? new(selected.Index, selected.Index + selected.Length) : default;
            }
            else
            {
                var outcome = FindOwnedSliceValue(program, subject, startOffsetInBytes,
                    ref invocation, _preparedRegex.SearchPlan);
                if (outcome.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                result = outcome.Found ? new(outcome.StartByte, outcome.EndByte) : default;
            }
            if (!output.IsEmpty) output[0] = result;
            // The final check also covers the single slot. Failure clears it below.
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
        finally { invocation.Ledger.Release(invocation.Ledger.ReservedBytes); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Utf8CaptureRange MatchDetailedCaptureSlots(Utf8SliceProgram program, ReadOnlySpan<byte> subject,
        int startOffsetInBytes, Span<Utf8CaptureRange> output, ref Utf8SliceInvocation invocation)
    {
        if (program.EmittedHistoryOffsets is { } offsets &&
            (program.EmittedMatch ?? program.TieredProof) is { } proof &&
            invocation.IsUnmetered && invocation.CheckpointHook is null &&
            Utf8SliceRunner.TryGetFixedDecimalFollower(program, out var digits, out var first, out var last))
            return MatchEmittedCaptureSlots(subject, startOffsetInBytes, output, offsets, digits, first, last, proof);

        var screenedLiteral = -1;
        ref readonly var search = ref _preparedRegex.SearchPlan;
        var window = program.LeadingClassWindowProof;
        if (window.CanFinishCapturedRuns &&
            RequiredLiteralContinuation is > 0 and var continuation &&
            search.RequiredPrefilterSearcher.LiteralSearch is var delimiter && delimiter.Length == window.LiteralLength &&
            Utf8SliceRunner.TryFindPreparedLiteralRange(program, subject, ref startOffsetInBytes, delimiter,
                ref screenedLiteral, continuation, ref invocation, out var captured, capturedWindow: true))
        {
            if (captured.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
            if (!captured.Found)
            {
                output.Clear();
                FinishFundedInvocation(ref invocation);
                return default;
            }

            var suffixStart = screenedLiteral + delimiter.Length;
            var suffixEnd = captured.EndByte;
            var lastSlot = output.Length;
            var code = program.Instructions;
            if (window.Completion == Utf8SliceClassWindowCompletion.CapturedSharedRun)
            {
                // Both repeats consume the same run. The last internal
                // separator gives the first repeat its greedy endpoint.
                var breach = Utf8SliceRunner.FindPreviousRepeatStart(subject, suffixStart + 1, suffixEnd - 1,
                    code[continuation + 4].RepeatExitRewindStarts, ref invocation, out suffixEnd);
                if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                lastSlot = code[continuation + 7].A + 1;
            }

            if (suffixEnd >= 0)
            {
                // Dense slot order can differ from instruction order.
                var result = output[0] = new(captured.StartByte, captured.EndByte);
                var prefixSlot = code[0].A + 1;
                var suffixSlot = code[continuation].A + 1;
                if (prefixSlot < output.Length) output[prefixSlot] = new(captured.StartByte, screenedLiteral);
                if (suffixSlot < output.Length) output[suffixSlot] = new(suffixStart, suffixEnd);
                if (lastSlot < output.Length) output[lastSlot] = new(suffixEnd + 1, captured.EndByte);
                FinishFundedInvocation(ref invocation);
                program.ObserveCaptureProofWork(ref invocation);
                return result;
            }
        }

        var slots = Utf8SliceSlots.Empty;
        try
        {
            Utf8CaptureRange result;
            if (program.IsNonBacktracking && subject.Length - startOffsetInBytes >= 64 &&
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
                var slotCount = program.IsNonBacktracking || program.ReadsCaptures ? program.GroupCount : output.Length - 1;
                slots = Utf8SliceRunner.RentSlots(slotCount, ref invocation);
                var outcome = Utf8SliceRunner.TryFindFirst(program, subject, startOffsetInBytes,
                    slots.Starts, slots.Ends, false, ref invocation, search, screenedLiteral: screenedLiteral);
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
            if (result.Success) program.ObserveCaptureProofWork(ref invocation);
            return result;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    private static Utf8CaptureRange MatchEmittedCaptureSlots(ReadOnlySpan<byte> subject, int position,
        Span<Utf8CaptureRange> output, int[] offsets, int digits, byte first, byte last, Utf8BoundedProgramEmitter.Match proof)
    {
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[Utf8BoundedProgramEmitter.MaxHistoryCaptures];
        if (!Utf8SliceRunner.TryFindFixedDecimalMatch(subject, position, digits, first, last, proof,
                out var start, out var end, captures))
        {
            output.Clear();
            return default;
        }

        var value = output[0] = new(start, end);
        for (var slot = 1; slot < output.Length; slot++)
        {
            var firstCapture = offsets[slot];
            var afterLastCapture = offsets[slot + 1];
            if (firstCapture == afterLastCapture)
            {
                output[slot] = default;
                continue;
            }
            // Layout offsets include group zero. The scratch contains only
            // nonzero groups and records their ranges relative to match start.
            var capture = captures[afterLastCapture - 2];
            output[slot] = new(start + capture.StartOffsetInBytes, start + capture.EndOffsetInBytes);
        }
        return value;
    }
}
