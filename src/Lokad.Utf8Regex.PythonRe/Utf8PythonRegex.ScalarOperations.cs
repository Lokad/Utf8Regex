using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;

internal delegate void PythonReScalarReplacementAppender<TState>(ReadOnlySpan<byte> input, TState state, scoped ref PythonReScalarMatchCursor cursor, scoped ref Utf8SliceOutputWriter writer);

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
            startOffsetInBytes, MatchTimeout, _utf8Regex, Pattern, invocation);
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
        // The cursor validates the whole subject inside the owning invocation.
    }

    private bool IsMatchScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken)
    {
        // Keep capture-free unmetered calls on their established cursor:
        // validation-only was slower for that ASCII route in the public comparison.
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
                // Keep validation and the existing finder under one authority.
                Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
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
        finally { cursor.Dispose(); }
    }

    private Utf8PythonMatchContext MatchContextScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, CancellationToken cancellationToken)
    {
        var cursor = CreateScalarCursor(input, startOffsetInBytes, kind, cancellationToken);
        try
        {
            Utf8PythonMatchContext result = default;
            if (kind == 0 ? cursor.MoveNext() : cursor.MatchAtCurrentPosition())
            {
                var lastIndex = cursor.GetLastIndex();
                var groups = cursor.SnapshotGroups();
                var names = cursor.SnapshotNameEntries(_nameEntries);
                result = new Utf8PythonMatchContext(input, groups, names, lastIndex);
            }
            cursor.Checkpoint();
            return result;
        }
        finally { cursor.Dispose(); }
    }

    private Utf8PythonDetailedMatchData MatchDataScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, CancellationToken cancellationToken)
    {
        var cursor = CreateScalarCursor(input, startOffsetInBytes, kind, cancellationToken);
        try
        {
            var result = (kind == 0 ? cursor.MoveNext() : cursor.MatchAtCurrentPosition())
                ? cursor.SnapshotDetailed(_nameEntries) : default;
            cursor.Checkpoint();
            return result;
        }
        finally { cursor.Dispose(); }
    }

    private void FindIterScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonDetailedMatchData[] matches)
    {
        matches = [];
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        Utf8InvocationResultBuffer<Utf8PythonDetailedMatchData> list = default;
        try
        {
            while (cursor.MoveNext())
            {
                cursor.AddResult(ref list, cursor.SnapshotDetailed(_nameEntries));
                cursor.Checkpoint();
            }
            var completed = cursor.CompleteResults(ref list);
            cursor.Checkpoint();
            matches = completed;
            return;
        }
        finally { cursor.ReleaseResults(ref list); cursor.Dispose(); }
    }

    private void FindAllScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonMatchData[] matches)
    {
        matches = [];
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        Utf8InvocationResultBuffer<Utf8PythonMatchData> list = default;
        try
        {
            while (cursor.MoveNext())
            {
                cursor.EnsureMap();
                var group = cursor.SnapshotValueData();
                cursor.AddResult(ref list, new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = group.StartOffsetInBytes, EndOffsetInBytes = group.EndOffsetInBytes,
                    StartOffsetInUtf16 = group.StartOffsetInUtf16, EndOffsetInUtf16 = group.EndOffsetInUtf16,
                    ValueText = cursor.GetText(group.StartOffsetInBytes, group.EndOffsetInBytes),
                });
                cursor.Checkpoint();
            }
            var completed = cursor.CompleteResults(ref list);
            cursor.Checkpoint();
            matches = completed;
            return;
        }
        finally { cursor.ReleaseResults(ref list); cursor.Dispose(); }
    }

    private void FindAllScalarStrings(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonFindAllResult result)
    {
        result = default;
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        Utf8InvocationResultBuffer<string> scalars = default;
        Utf8InvocationResultBuffer<string[]> tuples = default;
        try
        {
            var count = CaptureGroupCount;
            while (cursor.MoveNext())
            {
                if (count <= 1)
                {
                    var success = cursor.TryGetCaptureByteRange(count, out var start, out var end);
                    cursor.AddResult(ref scalars, !success ? string.Empty : count == 1 && _optionalExactCaptureFindAllValue is { } exact
                        ? exact : cursor.GetText(start, end));
                }
                else
                {
                    var tuple = cursor.AllocateResultArray<string>(count);
                    for (var i = 0; i < count; i++)
                    {
                        tuple[i] = cursor.TryGetCaptureByteRange(i + 1, out var start, out var end)
                            ? cursor.GetText(start, end) : string.Empty;
                        cursor.Checkpoint();
                    }
                    cursor.AddResult(ref tuples, tuple);
                }
                // The next MoveNext checks the same invocation before discovery;
                // staged values stay private until the final publication check.
            }
            var completed = new Utf8PythonFindAllResult
            {
                Shape = count == 0 ? Utf8PythonFindAllShape.FullMatch : count == 1 ? Utf8PythonFindAllShape.SingleGroup : Utf8PythonFindAllShape.GroupTuple,
                ScalarValues = cursor.CompleteResults(ref scalars), TupleValues = cursor.CompleteResults(ref tuples),
            };
            cursor.Checkpoint();
            result = completed;
            return;
        }
        finally { cursor.ReleaseResults(ref scalars); cursor.ReleaseResults(ref tuples); cursor.Dispose(); }
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
                ScalarValues = cursor.CompleteResults(ref scalars), TupleValues = cursor.CompleteResults(ref tuples),
            };
            cursor.Checkpoint();
            result = completed;
            return;
        }
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
        finally { cursor.ReleaseResults(ref parts); cursor.Dispose(); }
    }

    private void SplitScalar(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes,
        CancellationToken cancellationToken, bool stringsOnly, out Utf8PythonSplitItem[] items, out string?[] values)
    {
        items = [];
        values = [];
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        Utf8InvocationResultBuffer<Utf8PythonSplitItem> parts = default;
        // String output needs only references; detailed output keeps capture metadata.
        // Both use the same cursor, result admission and interruption authority.
        Utf8InvocationResultBuffer<string?> stringParts = default;
        try
        {
            var last = startOffsetInBytes;
            var splits = 0;
            while ((maxSplit == 0 || splits < maxSplit) && cursor.MoveNext())
            {
                var text = cursor.GetText(last, cursor.StartOffsetInBytes);
                if (stringsOnly) cursor.AddResult(ref stringParts, text);
                else cursor.AddResult(ref parts, new Utf8PythonSplitItem { ValueText = text });
                for (var i = 1; i <= CaptureGroupCount; i++)
                {
                    var capture = cursor.TryGetCaptureByteRange(i, out var start, out var end) ? cursor.GetText(start, end) : null;
                    if (stringsOnly) cursor.AddResult(ref stringParts, capture);
                    else cursor.AddResult(ref parts, new Utf8PythonSplitItem
                    {
                        IsCapture = true, CaptureGroupNumber = i,
                        ValueText = capture,
                    });
                    cursor.Checkpoint();
                }
                last = cursor.EndOffsetInBytes;
                splits++;
            }
            var tail = cursor.GetText(last, input.Length);
            if (stringsOnly) cursor.AddResult(ref stringParts, tail);
            else cursor.AddResult(ref parts, new Utf8PythonSplitItem { ValueText = tail });
            Utf8PythonSplitItem[] completedItems = [];
            string?[] completedValues = [];
            if (stringsOnly) completedValues = cursor.CompleteResults(ref stringParts);
            else completedItems = cursor.CompleteResults(ref parts);
            cursor.Checkpoint();
            items = completedItems;
            values = completedValues;
            return;
        }
        finally { cursor.ReleaseResults(ref stringParts); cursor.ReleaseResults(ref parts); cursor.Dispose(); }
    }

    private void SubstituteScalar<TState>(ReadOnlySpan<byte> input, TState state,
        PythonReScalarReplacementAppender<TState> appendReplacement, int count, int startOffsetInBytes,
        Utf8SliceInvocation invocation, bool textOutput, bool validateOutput,
        out Utf8PythonSubnUtf8Result bytesResult, out Utf8PythonSubnResult textResult)
    {
        bytesResult = default;
        textResult = default;
        ValidateScalarStartOffset(input, startOffsetInBytes);
        var cursor = new PythonReScalarMatchCursor(input, ScalarProgram,
            startOffsetInBytes, MatchTimeout, _utf8Regex, Pattern, invocation);
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
                var resultMarker = cursor.ResultReservation;
                try
                {
                    appendReplacement(input, state, ref cursor, ref writer);
                    cursor.Checkpoint();
                }
                finally { cursor.ReleaseResultsSince(resultMarker); }
                last = cursor.EndOffsetInBytes;
                replaced++;
            }
            if (replaced != 0) cursor.AppendBytes(ref writer, input[last..]);
            byte[] completedBytes = [];
            string completedText = string.Empty;
            if (textOutput) completedText = replaced == 0 ? cursor.GetText(0, input.Length)
                : cursor.CompleteOutputText(ref writer);
            else completedBytes = replaced == 0 ? cursor.GetBytes(0, input.Length)
                : cursor.CompleteOutputBytes(ref writer, validateOutput);
            cursor.Checkpoint();
            if (textOutput) textResult = new Utf8PythonSubnResult { ResultText = completedText, ReplacementCount = replaced };
            else bytesResult = new Utf8PythonSubnUtf8Result { ResultBytes = completedBytes, ReplacementCount = replaced };
        }
        finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
    }
}
