using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;


/// <summary>Shared scalar discovery for Python global operations.</summary>
public sealed partial class Utf8PythonRegex
{
    private PythonReScalarMatchCursor CreateScalarCursor(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, int kind = 0, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        ValidateScalarStartOffset(input, startOffsetInBytes);
        var invocation = CreateInvocation(cancellationToken, workingMemoryAllowance);
        return new PythonReScalarMatchCursor(input, ScalarProgram,
            startOffsetInBytes, MatchTimeout, _utf8Regex, this, invocation, prepareSearch: kind == 0);
    }

    private static void ValidateScalarStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (!Utf8BytePosition.IsScalarBoundary(input, startOffsetInBytes))
            throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
    }

    private bool IsMatchScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken)
    {
        var program = ScalarProgram;
        var prepared = _utf8Regex;
        var useLiteral = (HasFiniteExecutionLimits || cancellationToken.CanBeCanceled ||
            MatchTimeout != Timeout.InfiniteTimeSpan || program.GroupCount != 0) &&
            prepared?.CanFindNativeLiteralForResult == true;
        if ((useLiteral || program.HasCaptureFreeAsciiBranch) && !program.CanMatchEmpty &&
            !program.HasContiguousAnchor && !program.RightToLeft && !program.IgnoreCase)
        {
            ValidateScalarStartOffset(input, startOffsetInBytes);
            var invocation = CreateInvocation(cancellationToken);
            try
            {
                Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                if (useLiteral)
                {
                    if (!prepared!.TryFindNativeLiteralForResult(input, startOffsetInBytes,
                        ref invocation, out var found, out _, out _))
                        throw new InvalidOperationException("Prepared literal discovery declined its eligible route.");
                    return found;
                }

                // Use Python's own instructions for both the bounded proof and
                // full verification when the proof leaves a candidate undecided.
                if (program.RejectsByFinalAscii(input) ||
                    prepared?.RejectMissingExactRequiredLiteral(input, ref invocation) == true)
                {
                    Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                    return false;
                }
                var result = Utf8SliceRunner.TryFindFirst(program, input, startOffsetInBytes,
                    [], [], false, ref invocation, existenceOnly: true);
                if (result.Breach != Utf8ExecutionLimitKind.None)
                    throw invocation.Budget.CreateResourceFailure();
                Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                return result.Found;
            }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
                invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            {
                throw invocation.Budget.CreateResourceFailure();
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input, Pattern);
            }
        }
        ValidateScalarStartOffset(input, startOffsetInBytes);
        // A one-shot search needs no cursor return or copy of its inline state.
        var state = default(PythonReScalarMatchState);
        InitializeInvocation(out state.WorkingInvocation, cancellationToken);
        state.Initialize(input, program, startOffsetInBytes, MatchTimeout, this, prepared);
        try
        {
            var found = state.MoveNext(input, existenceOnly: true);
            state.Checkpoint();
            return found;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            state.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw state.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw state.TimeoutFailure(input); }
        finally { state.Dispose(); }
    }

    private Utf8PythonValueMatch MatchValueScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, CancellationToken cancellationToken, scoped Span<Utf8PythonCaptureRange> captures = default,
        IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        ValidateScalarStartOffset(input, startOffsetInBytes);
        // A single result borrows the input here. Construct the state in its
        // local so returning it through a value cursor does not copy it.
        var state = default(PythonReScalarMatchState);
        InitializeInvocation(out state.WorkingInvocation, cancellationToken, workingMemoryAllowance);
        state.Initialize(input, ScalarProgram, startOffsetInBytes, MatchTimeout, this, _utf8Regex, prepareSearch: kind == 0);
        try
        {
            var completed = kind == 0 ? state.MoveNext(input) : state.MatchAtCurrentPosition(input, requireFullInput: kind == 2);
            var result = completed ? Utf8PythonValueMatch.Create(input, state.SnapshotValueData()) : default;
            for (var number = 0; number < captures.Length; number++)
            {
                if ((number & 255) == 0) state.Checkpoint();
                captures[number] = !completed ? default : state.TryGetCaptureByteRange(number, out var start, out var end)
                    ? new Utf8PythonCaptureRange { Number = number, Success = true, StartOffsetInBytes = start, EndOffsetInBytes = end }
                    : new Utf8PythonCaptureRange { Number = number };
            }
            state.Checkpoint();
            return result;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            state.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw state.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw state.TimeoutFailure(input); }
        finally { state.Dispose(); }
    }

    private const int SmallSplitLimit = 8;

    private Utf8PythonCaptureRange[] SplitRangesScalar(ReadOnlySpan<byte> input, int maxSplit,
        int startOffsetInBytes, CancellationToken cancellationToken)
    {
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        ref var invocation = ref cursor.WorkingInvocation;
        var groups = CaptureGroupCount;
        Utf8InvocationResultBuffer<Utf8PythonCaptureRange> parts = default;
        Utf8InvocationResultLedger results = default;
        try
        {
            var last = startOffsetInBytes;
            if (groups == 0 && maxSplit is > 0 and <= SmallSplitLimit)
            {
                if (cursor.MoveNext())
                    return CompleteSmallSplit(ref cursor, maxSplit, last, input.Length, ref results);
            }
            else
            {
                var splits = 0;
                while ((maxSplit == 0 || splits < maxSplit) && cursor.MoveNext())
                {
                    parts.Add(new Utf8PythonCaptureRange
                    { Success = true, StartOffsetInBytes = last, EndOffsetInBytes = cursor.StartOffsetInBytes }, ref invocation);
                    for (var number = 1; number <= groups; number++)
                        parts.Add(cursor.TryGetCaptureByteRange(number, out var start, out var end)
                            ? new Utf8PythonCaptureRange { Number = number, Success = true, StartOffsetInBytes = start, EndOffsetInBytes = end }
                            : new Utf8PythonCaptureRange { Number = number }, ref invocation);
                    last = cursor.EndOffsetInBytes;
                    splits++;
                }
            }
            var tail = new Utf8PythonCaptureRange
            { Success = true, StartOffsetInBytes = last, EndOffsetInBytes = input.Length };
            Utf8PythonCaptureRange[] completed;
            if (parts.Count == 0)
            {
                completed = results.AllocateArray<Utf8PythonCaptureRange>(1, ref invocation);
                completed[0] = tail;
            }
            else
            {
                parts.Add(tail, ref invocation);
                completed = parts.ToArray(ref results, ref invocation);
            }
            cursor.Checkpoint();
            return completed;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            cursor.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw cursor.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw cursor.TimeoutFailure(); }
        finally
        {
            parts.Release(ref invocation);
            results.Release(ref invocation);
            cursor.Dispose();
        }
    }

    // A capture-free split with a small cap can admit just its exact result.
    // The ordinary cursor already found the first separator.
    private static Utf8PythonCaptureRange[] CompleteSmallSplit(ref PythonReScalarMatchCursor cursor,
        int maxSplit, int last, int end, ref Utf8InvocationResultLedger results)
    {
        Span<Utf8PythonCaptureRange> scratch = stackalloc Utf8PythonCaptureRange[SmallSplitLimit];
        var count = 0;
        do
        {
            cursor.Checkpoint();
            scratch[count++] = new Utf8PythonCaptureRange
            { Success = true, StartOffsetInBytes = last, EndOffsetInBytes = cursor.StartOffsetInBytes };
            last = cursor.EndOffsetInBytes;
        } while (count < maxSplit && cursor.MoveNext());

        var completed = results.AllocateArray<Utf8PythonCaptureRange>(count + 1, ref cursor.WorkingInvocation);
        scratch[..count].CopyTo(completed);
        completed[count] = new Utf8PythonCaptureRange { Success = true, StartOffsetInBytes = last, EndOffsetInBytes = end };
        cursor.Checkpoint();
        return completed;
    }

    private void SubstituteScalar(ReadOnlySpan<byte> input, PythonReReplacementPlan plan,
        int count, int startOffsetInBytes, scoped in Utf8SliceInvocation invocation,
        out Utf8PythonSubnUtf8Result bytesResult,
        out Utf8PythonRetainedSubnResult? retained, bool retain = false)
    {
        bytesResult = default;
        retained = null;
        // The substitution entry checks the byte start before preparing the template.
        var cursor = new PythonReScalarMatchCursor(input, ScalarProgram,
            startOffsetInBytes, MatchTimeout, _utf8Regex, this, invocation);
        Utf8SliceOutputWriter writer = default;
        try
        {
            var last = 0;
            var replaced = 0;
            while ((count == 0 || replaced < count) && cursor.MoveNext())
            {
                // A miss needs only its final owned copy, with no output staging.
                if (replaced == 0) writer = cursor.CreateOutputWriter();
                cursor.AppendBytes(ref writer, input[last..cursor.StartOffsetInBytes]);
                plan.AppendToUtf8(input, ref cursor, ref writer);
                last = cursor.EndOffsetInBytes;
                replaced++;
            }
            if (replaced != 0) cursor.AppendBytes(ref writer, input[last..]);
            var publicationMarker = cursor.ResultReservation;
            var completedBytes = replaced == 0 ? cursor.GetBytes(0, input.Length)
                : cursor.CompleteOutputBytes(ref writer);
            cursor.Checkpoint();
            bytesResult = new Utf8PythonSubnUtf8Result { ResultBytes = completedBytes, ReplacementCount = replaced };
            if (retain)
            {
                var receipt = cursor.RetainSubstitution(bytesResult);
                cursor.Checkpoint();
                receipt.SetTransferredBytes(cursor.TransferResultsSince(publicationMarker));
                retained = receipt;
            }
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            cursor.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw cursor.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw cursor.TimeoutFailure(); }
        finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
    }
}
