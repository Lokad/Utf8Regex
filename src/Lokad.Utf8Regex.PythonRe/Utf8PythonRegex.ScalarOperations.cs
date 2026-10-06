using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

internal delegate byte[] PythonReScalarReplacementFactory<TState>(ReadOnlySpan<byte> input, TState state, scoped ref PythonReScalarMatchCursor cursor);

/// <summary>Shared scalar discovery for Python global operations.</summary>
public sealed partial class Utf8PythonRegex
{
    private bool TryCreateScalarCursor(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out PythonReScalarMatchCursor cursor)
    {
        cursor = default;
        if (ScalarProgram is not { } program)
            return false;
        ValidateScalarStartOffset(input, startOffsetInBytes);
        cursor = new PythonReScalarMatchCursor(input, program, _managedGroupNumbersByPythonGroup,
            startOffsetInBytes, MatchTimeout, _translation.Pattern, cancellationToken);
        return true;
    }

    private static void ValidateScalarStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if ((uint)startOffsetInBytes > (uint)input.Length ||
            startOffsetInBytes < input.Length && (input[startOffsetInBytes] & 0xC0) == 0x80)
            throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
        // The cursor validates the whole subject inside the owning invocation.
    }

    private bool TryMatchScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, bool materializeValues, CancellationToken cancellationToken,
        out PythonReGroupData[] groups, out Utf8PythonDetailedMatchData detailed)
    {
        groups = [];
        detailed = default;
        var program = kind == 2 ? _lazyScalarFullProgram.Value : ScalarProgram;
        if (program is null) return false;
        ValidateScalarStartOffset(input, startOffsetInBytes);
        var cursor = new PythonReScalarMatchCursor(input, program,
            _managedGroupNumbersByPythonGroup, startOffsetInBytes, MatchTimeout,
            _translation.Pattern, cancellationToken);
        try
        {
            if (kind == 0 ? cursor.MoveNext() : cursor.MatchAtCurrentPosition())
            {
                if (materializeValues) detailed = cursor.SnapshotDetailed(_nameEntries);
                else groups = cursor.SnapshotGroups();
            }
            cursor.Checkpoint();
            return true;
        }
        finally { cursor.Dispose(); }
    }

    private bool TryFindIterScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonDetailedMatchData[] matches)
    {
        matches = [];
        if (!TryCreateScalarCursor(input, startOffsetInBytes, cancellationToken, out var cursor)) return false;
        try
        {
            List<Utf8PythonDetailedMatchData> list = [];
            while (cursor.MoveNext())
            {
                list.Add(cursor.SnapshotDetailed(_nameEntries));
                cursor.Checkpoint();
            }
            matches = list.ToArray();
            cursor.Checkpoint();
            return true;
        }
        finally { cursor.Dispose(); }
    }

    private bool TryFindAllScalar(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonMatchData[] matches)
    {
        matches = [];
        if (!TryCreateScalarCursor(input, startOffsetInBytes, cancellationToken, out var cursor)) return false;
        try
        {
            List<Utf8PythonMatchData> list = [];
            while (cursor.MoveNext())
            {
                var group = cursor.SnapshotGroups()[0];
                list.Add(new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = group.StartOffsetInBytes, EndOffsetInBytes = group.EndOffsetInBytes,
                    StartOffsetInUtf16 = group.StartOffsetInUtf16, EndOffsetInUtf16 = group.EndOffsetInUtf16,
                    ValueText = cursor.GetText(group.StartOffsetInBytes, group.EndOffsetInBytes),
                });
                cursor.Checkpoint();
            }
            matches = list.ToArray();
            cursor.Checkpoint();
            return true;
        }
        finally { cursor.Dispose(); }
    }

    private bool TryFindAllScalarStrings(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonFindAllResult result)
    {
        result = default;
        if (!TryCreateScalarCursor(input, startOffsetInBytes, cancellationToken, out var cursor)) return false;
        try
        {
            List<string> scalars = [];
            List<string[]> tuples = [];
            var count = _translation.CaptureGroupCount;
            while (cursor.MoveNext())
            {
                var groups = cursor.SnapshotGroups();
                if (count <= 1)
                {
                    var group = groups[count];
                    scalars.Add(!group.Success ? string.Empty : count == 1 && _optionalExactCaptureFindAllValue is { } exact
                        ? exact : cursor.GetText(group.StartOffsetInBytes, group.EndOffsetInBytes));
                }
                else
                {
                    var tuple = new string[count];
                    for (var i = 0; i < count; i++)
                    {
                        var group = groups[i + 1];
                        tuple[i] = group.Success ? cursor.GetText(group.StartOffsetInBytes, group.EndOffsetInBytes) : string.Empty;
                        cursor.Checkpoint();
                    }
                    tuples.Add(tuple);
                }
                cursor.Checkpoint();
            }
            result = new Utf8PythonFindAllResult
            {
                Shape = count == 0 ? Utf8PythonFindAllShape.FullMatch : count == 1 ? Utf8PythonFindAllShape.SingleGroup : Utf8PythonFindAllShape.GroupTuple,
                ScalarValues = scalars.ToArray(), TupleValues = tuples.ToArray(),
            };
            cursor.Checkpoint();
            return true;
        }
        finally { cursor.Dispose(); }
    }

    private bool TryFindAllScalarUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonFindAllUtf8Result result)
    {
        result = default;
        if (!TryCreateScalarCursor(input, startOffsetInBytes, cancellationToken, out var cursor)) return false;
        try
        {
            List<byte[]> scalars = [];
            List<byte[][]> tuples = [];
            var count = _translation.CaptureGroupCount;
            while (cursor.MoveNext())
            {
                var groups = cursor.SnapshotGroups();
                if (count <= 1)
                {
                    var group = groups[count];
                    scalars.Add(group.Success ? cursor.GetBytes(group.StartOffsetInBytes, group.EndOffsetInBytes) : []);
                }
                else
                {
                    var tuple = new byte[count][];
                    for (var i = 0; i < count; i++)
                    {
                        var group = groups[i + 1];
                        tuple[i] = group.Success ? cursor.GetBytes(group.StartOffsetInBytes, group.EndOffsetInBytes) : [];
                        cursor.Checkpoint();
                    }
                    tuples.Add(tuple);
                }
                cursor.Checkpoint();
            }
            result = new Utf8PythonFindAllUtf8Result
            {
                Shape = count == 0 ? Utf8PythonFindAllShape.FullMatch : count == 1 ? Utf8PythonFindAllShape.SingleGroup : Utf8PythonFindAllShape.GroupTuple,
                ScalarValues = scalars.ToArray(), TupleValues = tuples.ToArray(),
            };
            cursor.Checkpoint();
            return true;
        }
        finally { cursor.Dispose(); }
    }

    private bool TrySplitScalar(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes,
        CancellationToken cancellationToken, bool stringsOnly, out Utf8PythonSplitItem[] items, out string?[] values)
    {
        items = [];
        values = [];
        if (!TryCreateScalarCursor(input, startOffsetInBytes, cancellationToken, out var cursor)) return false;
        try
        {
            List<Utf8PythonSplitItem> parts = [];
            var last = startOffsetInBytes;
            var splits = 0;
            while ((maxSplit == 0 || splits < maxSplit) && cursor.MoveNext())
            {
                parts.Add(new Utf8PythonSplitItem { ValueText = cursor.GetText(last, cursor.StartOffsetInBytes) });
                var groups = cursor.SnapshotGroups();
                for (var i = 1; i < groups.Length; i++)
                {
                    var group = groups[i];
                    parts.Add(new Utf8PythonSplitItem
                    {
                        IsCapture = true, CaptureGroupNumber = i,
                        ValueText = group.Success ? cursor.GetText(group.StartOffsetInBytes, group.EndOffsetInBytes) : null,
                    });
                    cursor.Checkpoint();
                }
                last = cursor.EndOffsetInBytes;
                splits++;
            }
            parts.Add(new Utf8PythonSplitItem { ValueText = cursor.GetText(last, input.Length) });
            items = parts.ToArray();
            if (stringsOnly)
            {
                values = new string?[items.Length];
                for (var i = 0; i < values.Length; i++)
                {
                    if ((i & 255) == 0) cursor.Checkpoint();
                    values[i] = items[i].ValueText;
                }
            }
            cursor.Checkpoint();
            return true;
        }
        finally { cursor.Dispose(); }
    }

    private bool TrySubstituteScalar<TState>(ReadOnlySpan<byte> input, TState state,
        PythonReScalarReplacementFactory<TState> replacementFactory, int count, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonSubnUtf8Result result)
    {
        result = default;
        if (!TryCreateScalarCursor(input, startOffsetInBytes, cancellationToken, out var cursor)) return false;
        try
        {
            var builder = new ArrayBufferWriter<byte>();
            var last = 0;
            var replaced = 0;
            while ((count == 0 || replaced < count) && cursor.MoveNext())
            {
                cursor.AppendBytes(builder, input[last..cursor.StartOffsetInBytes]);
                var replacement = replacementFactory(input, state, ref cursor);
                // User callback time/storage is excluded from resource accounting;
                // the invocation deadline/token is observed immediately on return.
                cursor.Checkpoint();
                cursor.AppendBytes(builder, replacement);
                last = cursor.EndOffsetInBytes;
                replaced++;
            }
            cursor.AppendBytes(builder, input[last..]);
            result = new Utf8PythonSubnUtf8Result { ResultBytes = builder.WrittenSpan.ToArray(), ReplacementCount = replaced };
            cursor.Checkpoint();
            return true;
        }
        finally { cursor.Dispose(); }
    }
}
