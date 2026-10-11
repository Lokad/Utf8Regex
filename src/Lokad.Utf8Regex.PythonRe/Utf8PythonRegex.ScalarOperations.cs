using Lokad.Utf8Regex.Internal.Execution;

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
        var program = kind == 2 ? _lazyScalarFullProgram.Value : ScalarProgram;
        return new PythonReScalarMatchCursor(input, program, _coreGroupNumbersByPythonGroup,
            startOffsetInBytes, MatchTimeout, _translation.Pattern, cancellationToken, InvocationLimits);
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
                var groups = cursor.SnapshotGroups();
                var names = cursor.SnapshotNameEntries(_nameEntries);
                result = new Utf8PythonMatchContext(input, groups, names, cursor.LastIndex);
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
            var count = _translation.CaptureGroupCount;
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
                cursor.Checkpoint();
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
            var count = _translation.CaptureGroupCount;
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

    private void SplitScalar(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes,
        CancellationToken cancellationToken, bool stringsOnly, out Utf8PythonSplitItem[] items, out string?[] values)
    {
        items = [];
        values = [];
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        Utf8InvocationResultBuffer<Utf8PythonSplitItem> parts = default;
        try
        {
            var last = startOffsetInBytes;
            var splits = 0;
            while ((maxSplit == 0 || splits < maxSplit) && cursor.MoveNext())
            {
                cursor.AddResult(ref parts, new Utf8PythonSplitItem { ValueText = cursor.GetText(last, cursor.StartOffsetInBytes) });
                for (var i = 1; i <= _translation.CaptureGroupCount; i++)
                {
                    cursor.AddResult(ref parts, new Utf8PythonSplitItem
                    {
                        IsCapture = true, CaptureGroupNumber = i,
                        ValueText = cursor.TryGetCaptureByteRange(i, out var start, out var end) ? cursor.GetText(start, end) : null,
                    });
                    cursor.Checkpoint();
                }
                last = cursor.EndOffsetInBytes;
                splits++;
            }
            cursor.AddResult(ref parts, new Utf8PythonSplitItem { ValueText = cursor.GetText(last, input.Length) });
            Utf8PythonSplitItem[] completedItems = [];
            string?[] completedValues = [];
            if (stringsOnly)
            {
                completedValues = cursor.AllocateResultArray<string?>(parts.Count);
                for (var i = 0; i < completedValues.Length; i++)
                {
                    if ((i & 255) == 0) cursor.Checkpoint();
                    completedValues[i] = parts[i].ValueText;
                }
            }
            else completedItems = cursor.CompleteResults(ref parts);
            cursor.Checkpoint();
            items = completedItems;
            values = completedValues;
            return;
        }
        finally { cursor.ReleaseResults(ref parts); cursor.Dispose(); }
    }

    private void SubstituteScalar<TState>(ReadOnlySpan<byte> input, TState state,
        PythonReScalarReplacementAppender<TState> appendReplacement, int count, int startOffsetInBytes,
        CancellationToken cancellationToken, bool textOutput, bool validateOutput,
        out Utf8PythonSubnUtf8Result bytesResult, out Utf8PythonSubnResult textResult)
    {
        bytesResult = default;
        textResult = default;
        var cursor = CreateScalarCursor(input, startOffsetInBytes, cancellationToken);
        Utf8SliceOutputWriter writer = default;
        try
        {
            writer = cursor.CreateOutputWriter();
            var last = 0;
            var replaced = 0;
            while ((count == 0 || replaced < count) && cursor.MoveNext())
            {
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
            cursor.AppendBytes(ref writer, input[last..]);
            byte[] completedBytes = [];
            string completedText = string.Empty;
            if (textOutput) completedText = cursor.CompleteOutputText(ref writer);
            else completedBytes = cursor.CompleteOutputBytes(ref writer, validateOutput);
            cursor.Checkpoint();
            if (textOutput) textResult = new Utf8PythonSubnResult { ResultText = completedText, ReplacementCount = replaced };
            else bytesResult = new Utf8PythonSubnUtf8Result { ResultBytes = completedBytes, ReplacementCount = replaced };
        }
        finally { cursor.ReleaseOutput(ref writer); cursor.Dispose(); }
    }
}
