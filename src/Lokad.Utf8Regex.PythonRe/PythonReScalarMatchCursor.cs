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
        int startOffsetInBytes, TimeSpan timeout, Utf8Regex? candidateRegex,
        object pattern, scoped in Utf8SliceInvocation invocation, bool prepareSearch = true)
    {
        _input = input;
        _state = new PythonReScalarMatchState(input, program, startOffsetInBytes, timeout, pattern, invocation, candidateRegex, prepareSearch);
    }

    internal int StartOffsetInBytes => _state.StartOffsetInBytes;
    internal int EndOffsetInBytes => _state.EndOffsetInBytes;
    internal readonly ulong ReservedBytes => _state.ReservedBytes;
    internal readonly ulong ResultReservation => _state.ResultReservation;
    [UnscopedRef] internal ref Utf8SliceInvocation WorkingInvocation => ref _state.WorkingInvocation;
    internal ulong TransferResultsSince(ulong marker) => _state.TransferResultsSince(marker);
    internal Utf8PythonRetainedSubnResult RetainSubstitution(Utf8PythonSubnUtf8Result result) => _state.RetainSubstitution(result);
    internal void AddResult<T>(ref Utf8InvocationResultBuffer<T> buffer, T value) => _state.AddResult(ref buffer, value);
    internal T[] CompleteResults<T>(ref Utf8InvocationResultBuffer<T> buffer) => _state.CompleteResults(ref buffer);
    internal void ReleaseResults<T>(ref Utf8InvocationResultBuffer<T> buffer) => _state.ReleaseResults(ref buffer);
    internal void Checkpoint() => _state.Checkpoint();
    internal bool MoveNext() => _state.MoveNext(_input);
    internal bool MatchAtCurrentPosition(bool requireFullInput = false) => _state.MatchAtCurrentPosition(_input, requireFullInput);
    internal PythonReGroupData SnapshotValueData() => _state.SnapshotValueData();
    internal bool TryGetCaptureByteRange(int number, out int start, out int end) => _state.TryGetCaptureByteRange(number, out start, out end);
    internal byte[] GetBytes(int start, int end) => _state.GetBytes(_input, start, end);
    internal Utf8SliceOutputWriter CreateOutputWriter() => _state.CreateOutputWriter();
    internal void AppendBytes(scoped ref Utf8SliceOutputWriter writer, scoped ReadOnlySpan<byte> bytes) => _state.AppendBytes(ref writer, bytes);
    internal byte[] CompleteOutputBytes(scoped ref Utf8SliceOutputWriter writer) => _state.CompleteOutputBytes(ref writer);
    internal void ReleaseOutput(scoped ref Utf8SliceOutputWriter writer) => _state.ReleaseOutput(ref writer);

    internal readonly System.Text.RegularExpressions.RegexMatchTimeoutException TimeoutFailure() => _state.TimeoutFailure(_input);

    internal void Dispose() => _state.Dispose();
}
