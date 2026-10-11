using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;

// One operation owns this cursor and its invocation. Search positions and empty
// advancement are bytes; the lazy map serves only returned UTF-16 coordinates.
// Python retains the last capture, so discovery does not collect capture history.
internal ref struct PythonReScalarMatchCursor
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8SliceProgram _program;
    private readonly int[] _numbersByPython;
    private readonly string _pattern;
    private readonly TimeSpan _timeout;
    private Utf8SliceInvocation _invocation;
    private Utf8InvocationResultLedger _results;
    private Utf8SliceSlots _slots;
    private Utf8ValidationResult _validation;
    private Utf8BoundaryMap _map;
    private ulong _mapReservation;
    private bool _hasMap;
    private bool _retryNonEmpty;
    private bool _finished;
    private int _position;

    internal PythonReScalarMatchCursor(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int[] numbersByPython, int startOffsetInBytes, TimeSpan timeout,
        string pattern, CancellationToken cancellationToken)
        : this(input, program, numbersByPython, startOffsetInBytes, timeout, pattern,
            cancellationToken, Utf8ExecutionLimits.Unlimited)
    { }

    internal PythonReScalarMatchCursor(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int[] numbersByPython, int startOffsetInBytes, TimeSpan timeout,
        string pattern, CancellationToken cancellationToken, Utf8ExecutionLimits limits)
    {
        this = default;
        _input = input;
        _program = program;
        _numbersByPython = numbersByPython;
        _pattern = pattern;
        _timeout = timeout;
        _position = startOffsetInBytes;
        _slots = new Utf8SliceSlots([], []);
        _invocation = new Utf8SliceInvocation(limits, timeout, cancellationToken);
        try
        {
            _validation = Utf8Validation.Validate(input, ref _invocation);
            _slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref _invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            Dispose();
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), pattern, timeout);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal int StartOffsetInBytes { get; private set; }
    internal int EndOffsetInBytes { get; private set; }
    internal readonly ulong ReservedBytes => _invocation.Ledger.ReservedBytes;
    internal readonly ulong ResultReservation => _results.ReservedBytes;
    [UnscopedRef] internal ref Utf8SliceInvocation WorkingInvocation => ref _invocation;

    internal T[] AllocateResultArray<T>(int count)
    {
        try { return _results.AllocateArray<T, Utf8InvocationBufferControl>(count, new(ref _invocation)); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
    }

    internal void ReleaseResultArray<T>(T[] array)
        => _results.ReleaseArray(array, new Utf8InvocationBufferControl(ref _invocation));

    internal void ReleaseResultsSince(ulong marker)
        => _results.ReleaseSince(marker, new Utf8InvocationBufferControl(ref _invocation));

    internal void AddResult<T>(ref Utf8InvocationResultBuffer<T> buffer, T value)
    {
        try { buffer.Add(value, new Utf8InvocationBufferControl(ref _invocation)); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
    }

    internal T[] CompleteResults<T>(ref Utf8InvocationResultBuffer<T> buffer)
    {
        try { return buffer.ToArray(ref _results, new Utf8InvocationBufferControl(ref _invocation)); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
    }

    internal void ReleaseResults<T>(ref Utf8InvocationResultBuffer<T> buffer)
        => buffer.Release(new Utf8InvocationBufferControl(ref _invocation));

    private readonly RegexMatchTimeoutException TimeoutFailure()
        => new(Encoding.UTF8.GetString(_input), _pattern, _timeout);

    internal void Checkpoint()
    {
        try
        {
            if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Scalar discovery exceeded the invocation budget.");
            _invocation.Budget.ThrowIfCancellationRequested();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(_input), _pattern, _timeout);
        }
    }

    internal bool MoveNext() => MoveNextCore(true);

    internal bool MatchAtCurrentPosition() => MoveNextCore(false);

    private bool MoveNextCore(bool search)
    {
        while (!_finished)
        {
            Checkpoint();
            _invocation.AnchorOriginByte = _position;
            Utf8SliceMatchOutcome outcome;
            try
            {
                outcome = Utf8SliceRunner.TryMatchAt(_program, _input, _position,
                    _slots.Starts, _slots.Ends, false, _retryNonEmpty, true, ref _invocation);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(_input), _pattern, _timeout);
            }
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Scalar discovery exceeded the invocation budget.");
            if (outcome.IsMatch)
            {
                StartOffsetInBytes = _position;
                EndOffsetInBytes = outcome.EndIndex;
                _retryNonEmpty = outcome.EndIndex == _position;
                _position = outcome.EndIndex;
                return true;
            }
            _retryNonEmpty = false;
            if (!search) { _finished = true; return false; }
            if (_position == _input.Length)
            {
                _finished = true;
                return false;
            }
            if (Rune.DecodeFromUtf8(_input[_position..], out _, out var width) != OperationStatus.Done)
                throw Utf8Validation.CreateInvalidUtf8Exception(_position);
            _position += width;
        }
        return false;
    }

    internal PythonReGroupData[] SnapshotGroups()
    {
        Checkpoint();
        EnsureMap();
        var groups = AllocateResultArray<PythonReGroupData>(_numbersByPython.Length);
        for (var pythonGroup = 0; pythonGroup < groups.Length; pythonGroup++)
        {
            if ((pythonGroup & 255) == 0) Checkpoint();
            groups[pythonGroup] = !TryGetCaptureByteRange(pythonGroup, out var start, out var end)
                ? new PythonReGroupData { Number = pythonGroup, Success = false }
                : new PythonReGroupData
                {
                    Number = pythonGroup, Success = true,
                    StartOffsetInBytes = start, EndOffsetInBytes = end,
                    StartOffsetInUtf16 = _map.GetUtf16OffsetForByteOffset(start),
                    EndOffsetInUtf16 = _map.GetUtf16OffsetForByteOffset(end),
                };
        }
        Checkpoint();
        return groups;
    }

    internal bool TryGetCaptureByteRange(int number, out int start, out int end)
    {
        start = end = -1;
        if ((uint)number >= (uint)_numbersByPython.Length) return false;
        var slot = Array.BinarySearch(_program.GroupNumbers, _numbersByPython[number]) - 1;
        start = number == 0 ? StartOffsetInBytes : slot >= 0 ? _slots.Starts[slot] : -1;
        end = number == 0 ? EndOffsetInBytes : slot >= 0 ? _slots.Ends[slot] : -1;
        // Reverse lookbehind captures publish raw edges inside an LTR program.
        // Byte-only template expansion does not need a coordinate map/snapshot.
        if (start < 0 || end < 0) return false;
        if (end < start) (start, end) = (end, start);
        return true;
    }

    internal void EnsureMap()
    {
        if (!_hasMap)
        {
            try
            {
                _map = Utf8BoundaryMap.Create(_input, _validation, ref _invocation, out _mapReservation);
                _hasMap = true;
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(_input), _pattern, _timeout);
            }
        }
    }

    internal Utf8PythonDetailedMatchData SnapshotDetailed(PythonReNameEntry[] names)
    {
        var groups = SnapshotGroups();
        try
        {
            var projected = AllocateResultArray<Utf8PythonGroupMatchData>(groups.Length);
            for (var i = 0; i < groups.Length; i++)
            {
                Checkpoint();
                var group = groups[i];
                projected[i] = new Utf8PythonGroupMatchData
                {
                    Number = group.Number, Success = group.Success,
                    StartOffsetInBytes = group.StartOffsetInBytes, EndOffsetInBytes = group.EndOffsetInBytes,
                    StartOffsetInUtf16 = group.StartOffsetInUtf16, EndOffsetInUtf16 = group.EndOffsetInUtf16,
                    HasContiguousByteRange = group.HasContiguousByteRange,
                    ValueText = group.Success ? GetText(group.StartOffsetInBytes, group.EndOffsetInBytes) : string.Empty,
                };
            }
            var copiedNames = AllocateResultArray<PythonReNameEntry>(names.Length);
            try { Utf8InvocationResultLedger.Copy<PythonReNameEntry, Utf8InvocationBufferControl>(names, copiedNames, new(ref _invocation)); }
            catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
            var result = new Utf8PythonDetailedMatchData { Groups = projected, NameEntries = copiedNames };
            Checkpoint();
            return result;
        }
        finally { ReleaseResultArray(groups); }
    }

    internal string GetText(int start, int end)
    {
        Checkpoint();
        try
        {
            // String-only results need the selected scalar range's char count,
            // not a coordinate map for the complete subject. Detailed results
            // already own a map and can reuse its derived coordinates.
            var count = _hasMap
                ? _map.GetUtf16OffsetForByteOffset(end) - _map.GetUtf16OffsetForByteOffset(start)
                : Utf8Validation.Validate(_input[start..end], ref _invocation).Utf16Length;
            var text = _results.DecodeUtf8(_input[start..end], count, ref _invocation);
            Checkpoint();
            return text;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(_input), _pattern, _timeout);
        }
    }

    internal byte[] GetBytes(int start, int end)
    {
        Checkpoint();
        var bytes = AllocateResultArray<byte>(end - start);
        CopyBytes(_input[start..end], bytes);
        return bytes;
    }

    internal Utf8SliceOutputWriter CreateOutputWriter()
    {
        try
        {
            if (!Utf8SliceOutputWriter.Rent(256, ref _invocation, out var writer))
                throw new InvalidOperationException("Python output exceeded the working storage budget.");
            return writer;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
    }

    internal void AppendBytes(scoped ref Utf8SliceOutputWriter writer, scoped ReadOnlySpan<byte> bytes)
    {
        try
        {
            Checkpoint();
            if (!writer.Append(bytes, ref _invocation))
                throw new InvalidOperationException("Python output exceeded the working storage budget.");
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
    }

    internal void AppendText(scoped ref Utf8SliceOutputWriter writer, string text)
    {
        try
        {
            // Observe a callback's token/deadline before scanning its return.
            Checkpoint();
            if (!writer.AppendUtf16(text.AsSpan(0, text.Length), ref _invocation))
                throw new InvalidOperationException("Python output exceeded the working storage budget.");
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
    }

    internal byte[] CompleteOutputBytes(scoped ref Utf8SliceOutputWriter writer, bool validateOutput)
    {
        try
        {
            Checkpoint();
            if (validateOutput) _ = Utf8Validation.Validate(writer.WrittenBytes, ref _invocation);
            var result = AllocateResultArray<byte>(writer.Length);
            CopyBytes(writer.WrittenBytes, result);
            writer.Release(ref _invocation);
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
    }

    internal string CompleteOutputText(scoped ref Utf8SliceOutputWriter writer)
    {
        try
        {
            Checkpoint();
            var validation = Utf8Validation.Validate(writer.WrittenBytes, ref _invocation);
            var result = _results.DecodeUtf8(writer.WrittenBytes, validation.Utf16Length, ref _invocation);
            Checkpoint();
            writer.Release(ref _invocation);
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
    }

    internal void ReleaseOutput(scoped ref Utf8SliceOutputWriter writer)
        => writer.Release(ref _invocation);

    internal void CopyBytes(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        try
        {
            Utf8SliceBufferWork.Copy(source, destination, ref _invocation);
            Checkpoint();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(_input), _pattern, _timeout);
        }
    }

    internal void Dispose()
    {
        _results.Release(new Utf8InvocationBufferControl(ref _invocation));
        Utf8SliceRunner.ReleaseSlots(_slots, ref _invocation);
        _slots = new Utf8SliceSlots([], []);
        _invocation.Ledger.Release(_mapReservation);
        _mapReservation = 0;
        _map = default;
        _hasMap = false;
        _finished = true;
    }
}
