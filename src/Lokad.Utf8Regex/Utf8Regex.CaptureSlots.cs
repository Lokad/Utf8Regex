using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    /// <summary>Finds a match from a scalar-aligned byte start and writes its last captures into caller-owned slots.</summary>
    /// <remarks>Slots follow GetGroupNumbers order, including zero. Short buffers do not change matching; excess entries remain untouched. Written slots clear on no match or failure after entry; pre-cancellation leaves output unchanged. The optional end bound is the matching end, retaining prefix/lookbehind context and original byte coordinates. Capture history remains available through CreateMatchCursor. Caller-owned output requires no result-storage allocation.</remarks>
    public Utf8CaptureRange Match(ReadOnlySpan<byte> input, int startOffsetInBytes,
        Span<Utf8CaptureRange> captures, CancellationToken cancellationToken = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null, int? endOffsetInBytes = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = captures[..Math.Min(captures.Length, _program.CaptureCatalog.GroupNumbers.Length)];
        var invocation = workingMemoryAllowance is null
            ? new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken) : new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken, workingMemoryAllowance);
        var slots = new Utf8SliceSlots([], []);
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var end = endOffsetInBytes ?? input.Length;
            if ((uint)end > (uint)input.Length || end < input.Length && (input[end] & 0xc0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(endOffsetInBytes));
            if ((uint)startOffsetInBytes > (uint)end ||
                startOffsetInBytes < input.Length && (input[startOffsetInBytes] & 0xc0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
            var subject = input[..end];
            var program = _ownedSliceProgram;
            invocation.AnchorOriginByte = startOffsetInBytes;
            Utf8CaptureRange result;
            if (program.GroupCount == 0 && CanUseNativeLiteralSearch(program, maximumAlternatives: 8))
            {
                // Preserve the existing value matcher’s bounded literal-family
                // search when callers migrate from its UTF16-start entry.
                var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
                    subject, startOffsetInBytes, subject.Length, false, ref invocation, out var selected, out var found);
                if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                result = found ? new(selected.Index, selected.Index + selected.Length) : default;
            }
            else
            {
                slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
                var outcome = Utf8SliceRunner.TryFindFirst(program, subject, startOffsetInBytes,
                    slots.Starts, slots.Ends, false, ref invocation, _preparedRegex.SearchPlan);
                if (outcome.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                result = outcome.Found ? new(outcome.StartByte, outcome.EndByte) : default;
            }
            for (var ordinal = 0; ordinal < output.Length; ordinal++)
            {
                if ((ordinal & 255) == 0) Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                output[ordinal] = !result.Success ? default : ordinal == 0 ? result :
                    slots.Starts[ordinal - 1] < 0 || slots.Ends[ordinal - 1] < 0 ? default :
                    new(slots.Starts[ordinal - 1], slots.Ends[ordinal - 1]);
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
