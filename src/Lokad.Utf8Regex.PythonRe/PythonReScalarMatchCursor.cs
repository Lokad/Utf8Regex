using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

// The synchronous span facade owns inline state; durable cursors use the same
// state with a memory subject. No subject conversion or second runner.
internal ref struct PythonReScalarMatchCursor
{
    private readonly ReadOnlySpan<byte> _input;
    private PythonReScalarMatchState _state;

    internal PythonReScalarMatchCursor(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startOffsetInBytes, TimeSpan timeout, string pattern, CancellationToken cancellationToken)
        : this(input, program, startOffsetInBytes, timeout, pattern, cancellationToken, Utf8ExecutionLimits.Unlimited)
    { }

    internal PythonReScalarMatchCursor(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startOffsetInBytes, TimeSpan timeout, string pattern, CancellationToken cancellationToken,
        Utf8ExecutionLimits limits)
        : this(input, program, startOffsetInBytes, timeout, pattern,
            new Utf8SliceInvocation(limits, timeout, cancellationToken))
    { }

    internal PythonReScalarMatchCursor(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startOffsetInBytes, TimeSpan timeout, string pattern,
        Utf8SliceInvocation invocation)
        : this(input, program, startOffsetInBytes, timeout, null, pattern, invocation)
    { }

    internal PythonReScalarMatchCursor(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startOffsetInBytes, TimeSpan timeout, Utf8Regex? candidateRegex,
        string pattern, Utf8SliceInvocation invocation)
    {
        _input = input;
        _state = new PythonReScalarMatchState(input, program, startOffsetInBytes, timeout, pattern, invocation, candidateRegex);
    }

    internal int StartOffsetInBytes => _state.StartOffsetInBytes;
    internal int EndOffsetInBytes => _state.EndOffsetInBytes;
    internal int? GetLastIndex() => _state.GetLastIndex(_input);
    internal readonly ulong ReservedBytes => _state.ReservedBytes;
    internal readonly ulong ResultReservation => _state.ResultReservation;
    internal readonly bool HasCoordinateMap => _state.HasCoordinateMap;
    [UnscopedRef] internal ref Utf8SliceInvocation WorkingInvocation => ref _state.WorkingInvocation;
    internal T[] AllocateResultArray<T>(int count) => _state.AllocateResultArray<T>(_input, count);
    internal void ReleaseResultsSince(ulong marker) => _state.ReleaseResultsSince(marker);
    internal void AddResult<T>(ref Utf8InvocationResultBuffer<T> buffer, T value) => _state.AddResult(_input, ref buffer, value);
    internal T[] CompleteResults<T>(ref Utf8InvocationResultBuffer<T> buffer) => _state.CompleteResults(_input, ref buffer);
    internal void ReleaseResults<T>(ref Utf8InvocationResultBuffer<T> buffer) => _state.ReleaseResults(ref buffer);
    internal void Checkpoint() => _state.Checkpoint(_input);
    internal bool MoveNext() => _state.MoveNext(_input);
    internal bool MatchAtCurrentPosition() => _state.MatchAtCurrentPosition(_input);
    internal PythonReGroupData SnapshotValueData() => _state.SnapshotValueData(_input);
    internal PythonReNameEntry[] SnapshotNameEntries(PythonReNameEntry[] names) => _state.SnapshotNameEntries(_input, names);
    internal PythonReGroupData[] SnapshotGroups() => _state.SnapshotGroups(_input);
    internal bool TryGetCaptureByteRange(int number, out int start, out int end) => _state.TryGetCaptureByteRange(number, out start, out end);
    internal void EnsureMap() => _state.EnsureMap(_input);
    internal Utf8PythonDetailedMatchData SnapshotDetailed(PythonReNameEntry[] names) => _state.SnapshotDetailed(_input, names);
    internal string GetText(int start, int end) => _state.GetText(_input, start, end, null);
    internal byte[] GetBytes(int start, int end) => _state.GetBytes(_input, start, end);
    internal Utf8SliceOutputWriter CreateOutputWriter() => _state.CreateOutputWriter(_input);
    internal void AppendBytes(scoped ref Utf8SliceOutputWriter writer, scoped ReadOnlySpan<byte> bytes) => _state.AppendBytes(_input, ref writer, bytes);
    internal void AppendText(scoped ref Utf8SliceOutputWriter writer, string text) => _state.AppendText(_input, ref writer, text);
    internal byte[] CompleteOutputBytes(scoped ref Utf8SliceOutputWriter writer, bool validateOutput) => _state.CompleteOutputBytes(_input, ref writer, validateOutput);
    internal string CompleteOutputText(scoped ref Utf8SliceOutputWriter writer) => _state.CompleteOutputText(_input, ref writer);
    internal void ReleaseOutput(scoped ref Utf8SliceOutputWriter writer) => _state.ReleaseOutput(ref writer);
    internal void CopyBytes(ReadOnlySpan<byte> source, Span<byte> destination) => _state.CopyBytes(_input, source, destination);
    internal void Dispose() => _state.Dispose();
}
