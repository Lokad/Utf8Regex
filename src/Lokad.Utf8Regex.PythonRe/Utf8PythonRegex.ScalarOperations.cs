using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;


/// <summary>Shared scalar discovery for Python global operations.</summary>
public sealed partial class Utf8PythonRegex
{
    private PythonReScalarMatchCursor CreateScalarCursor(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken)
        => CreateScalarCursor(input, startOffsetInBytes, 0, cancellationToken);

    private PythonReScalarMatchCursor CreateScalarCursor(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, CancellationToken cancellationToken)
    {
        ValidateScalarStartOffset(input, startOffsetInBytes);
        // Capture the invocation before cold full-match preparation. Preparation
        // keeps its fixed backstop, but its elapsed time cannot reset the deadline.
        var invocation = CreateInvocation(cancellationToken);
        var program = kind == 2 ? GetScalarFullProgram(input, invocation.Budget.Deadline, cancellationToken) : ScalarProgram;
        return new PythonReScalarMatchCursor(input, program,
            startOffsetInBytes, MatchTimeout, _utf8Regex, this, invocation, prepareSearch: kind == 0);
    }

    private Utf8SliceProgram GetScalarFullProgram(ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline deadline, CancellationToken cancellationToken)
    {
        if (_lazyScalarFullProgram.IsValueCreated)
            return _lazyScalarFullProgram.Value;

        if (deadline.IsInfinite && !cancellationToken.CanBeCanceled)
        {
            if (ResourcePolicy.MaxWorkingBytes is not { } maximumBytes)
                return _lazyScalarFullProgram.Value;
            // Restrict only execution-triggered cold preparation. Explicit
            // Compile completes Lazy.Value under its separate eager owner.
            // No interruption callback is needed on this uncontrolled lane.
            using var construction = Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes);
            var program = _lazyScalarFullProgram.Value;
            construction.Complete();
            return program;
        }

        // Keep the checkpoint closure out of the warm path: the C# compiler
        // otherwise allocates its display class before the early return above.
        return PrepareScalarFullProgram(input, deadline, cancellationToken);
    }

    private Utf8SliceProgram PrepareScalarFullProgram(ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline deadline, CancellationToken cancellationToken)
    {
        // Borrow interruption through the existing construction checkpoints.
        // Preserve fixed backstops and any enclosing authority. Working bytes
        // also bound this phase's additional covered preparation capacity.
        using var construction = ResourcePolicy.MaxWorkingBytes is { } maximumBytes
            ? Utf8ConstructionScope.RestrictAdditionalBytes(maximumBytes)
            : Utf8ConstructionScope.Enter();
        var previousCheckpoint = construction.Budget.CheckpointHook;
        construction.Budget.CheckpointHook = steps =>
        {
            previousCheckpoint?.Invoke(steps);
            cancellationToken.ThrowIfCancellationRequested();
            deadline.CheckNow();
        };
        try
        {
            var program = _lazyScalarFullProgram.Value;
            construction.Complete();
            return program;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
        finally { construction.Budget.CheckpointHook = previousCheckpoint; }
    }

    private static void ValidateScalarStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if ((uint)startOffsetInBytes > (uint)input.Length ||
            startOffsetInBytes < input.Length && (input[startOffsetInBytes] & 0xC0) == 0x80)
            throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
        // The cursor borrows the existing valid-text input contract.
    }

    private bool IsMatchScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken)
    {
        // Keep capture-free unmetered calls on their established cursor.
        // Captured booleans can discard the VM's otherwise unused capture state.
        if ((HasFiniteExecutionLimits || cancellationToken.CanBeCanceled ||
             MatchTimeout != Timeout.InfiniteTimeSpan || _scalarProgram.GroupCount != 0) &&
            !_scalarProgram.CanMatchEmpty &&
            !_scalarProgram.HasContiguousAnchor && !_scalarProgram.RightToLeft &&
            !_scalarProgram.IgnoreCase && _utf8Regex?.CanFindNativeLiteralForResult == true)
        {
            ValidateScalarStartOffset(input, startOffsetInBytes);
            var invocation = CreateInvocation(cancellationToken);
            try
            {
                // Complete literal eligibility also covers ordinary captures;
                // a boolean needs neither capture slots nor coordinate metadata.
                // Keep interruption and the existing finder under one authority.
                Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                if (!_utf8Regex.TryFindNativeLiteralForResult(input, startOffsetInBytes,
                    ref invocation, out var found, out _, out _))
                    throw new InvalidOperationException("Prepared literal discovery declined its eligible route.");
                return found;
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
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        try
        {
            var found = cursor.MoveNext();
            cursor.Checkpoint();
            return found;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            cursor.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw cursor.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw cursor.TimeoutFailure(); }
        finally { cursor.Dispose(); }
    }

    private Utf8PythonValueMatch MatchValueScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, CancellationToken cancellationToken)
    {
        var cursor = CreateScalarCursor(input, startOffsetInBytes, kind, cancellationToken);
        try
        {
            var completed = kind == 0 ? cursor.MoveNext() : cursor.MatchAtCurrentPosition();
            var result = completed ? Utf8PythonValueMatch.Create(input, cursor.SnapshotValueData()) : default;
            cursor.Checkpoint();
            return result;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            cursor.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw cursor.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw cursor.TimeoutFailure(); }
        finally { cursor.Dispose(); }
    }

    private void FindAllScalarUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonFindAllUtf8Result result)
    {
        result = default;
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        Utf8InvocationResultBuffer<byte[]> scalars = default;
        Utf8InvocationResultBuffer<byte[][]> tuples = default;
        try
        {
            var count = CaptureGroupCount;
            while (cursor.MoveNext())
            {
                if (count <= 1)
                {
                    var success = cursor.TryGetCaptureByteRange(count, out var start, out var end);
                    cursor.AddResult(ref scalars, success ? cursor.GetBytes(start, end) : []);
                }
                else
                {
                    var tuple = cursor.AllocateResultArray<byte[]>(count);
                    for (var i = 0; i < count; i++)
                    {
                        tuple[i] = cursor.TryGetCaptureByteRange(i + 1, out var start, out var end)
                            ? cursor.GetBytes(start, end) : [];
                        cursor.Checkpoint();
                    }
                    cursor.AddResult(ref tuples, tuple);
                }
                cursor.Checkpoint();
            }
            var completed = new Utf8PythonFindAllUtf8Result
            {
                Shape = count == 0 ? Utf8PythonFindAllShape.FullMatch : count == 1 ? Utf8PythonFindAllShape.SingleGroup : Utf8PythonFindAllShape.GroupTuple,
                ScalarValues = cursor.CompleteResults(ref scalars),
                TupleValues = cursor.CompleteResults(ref tuples)
            };
            cursor.Checkpoint();
            result = completed;
            return;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            cursor.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw cursor.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw cursor.TimeoutFailure(); }
        finally { cursor.ReleaseResults(ref scalars); cursor.ReleaseResults(ref tuples); cursor.Dispose(); }
    }

    private Utf8PythonCaptureRange[] SplitRangesScalar(ReadOnlySpan<byte> input, int maxSplit,
        int startOffsetInBytes, CancellationToken cancellationToken)
    {
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        Utf8InvocationResultBuffer<Utf8PythonCaptureRange> parts = default;
        try
        {
            var last = startOffsetInBytes;
            var splits = 0;
            while ((maxSplit == 0 || splits < maxSplit) && cursor.MoveNext())
            {
                cursor.AddResult(ref parts, new Utf8PythonCaptureRange
                { Success = true, StartOffsetInBytes = last, EndOffsetInBytes = cursor.StartOffsetInBytes });
                for (var number = 1; number <= CaptureGroupCount; number++)
                {
                    cursor.AddResult(ref parts, cursor.TryGetCaptureByteRange(number, out var start, out var end)
                        ? new Utf8PythonCaptureRange { Number = number, Success = true, StartOffsetInBytes = start, EndOffsetInBytes = end }
                        : new Utf8PythonCaptureRange { Number = number });
                    cursor.Checkpoint();
                }
                last = cursor.EndOffsetInBytes;
                splits++;
            }
            cursor.AddResult(ref parts, new Utf8PythonCaptureRange
            { Success = true, StartOffsetInBytes = last, EndOffsetInBytes = input.Length });
            var completed = cursor.CompleteResults(ref parts);
            cursor.Checkpoint();
            return completed;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            cursor.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw cursor.WorkingInvocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw cursor.TimeoutFailure(); }
        finally { cursor.ReleaseResults(ref parts); cursor.Dispose(); }
    }

    private void SubstituteScalar(ReadOnlySpan<byte> input, PythonReReplacementPlan plan,
        int count, int startOffsetInBytes, Utf8SliceInvocation invocation,
        out Utf8PythonSubnUtf8Result bytesResult,
        out Utf8PythonRetainedSubnResult? retained, bool retain = false)
    {
        bytesResult = default;
        retained = null;
        ValidateScalarStartOffset(input, startOffsetInBytes);
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
                cursor.Checkpoint();
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
