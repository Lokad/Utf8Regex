using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// Span and durable cursors borrow this single discovery implementation. Search positions and empty
// advancement are bytes; the lazy map serves only returned UTF-16 coordinates.
// Python retains the last capture, so discovery does not collect capture history.
internal struct PythonReScalarMatchState
{
    private readonly Utf8SliceProgram _program;
    private readonly Utf8Regex? _candidateRegex;
    private readonly int[] _numbersByPython;
    private readonly string _pattern;
    private readonly TimeSpan _timeout;
    private Utf8SliceInvocation _invocation;
    private Utf8InvocationResultLedger _results;
    private Utf8SliceSlots _slots;
    private Utf8SliceRunner.AttemptWorkspace _workspace;
    private Utf8ValidationResult _validation;
    private Utf8BoundaryMap _map;
    private ulong _mapReservation;
    private bool _hasMap;
    private bool _retryNonEmpty;
    private bool _finished;
    private int _position;

    internal PythonReScalarMatchState(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int[] numbersByPython, int startOffsetInBytes, TimeSpan timeout,
        string pattern, CancellationToken cancellationToken, Utf8ExecutionLimits limits)
        : this(input, program, numbersByPython, startOffsetInBytes, timeout, pattern,
            new Utf8SliceInvocation(limits, timeout, cancellationToken))
    { }

    internal PythonReScalarMatchState(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int[] numbersByPython, int startOffsetInBytes, TimeSpan timeout, string pattern,
        Utf8SliceInvocation invocation)
        : this(input, program, numbersByPython, startOffsetInBytes, timeout, pattern, invocation, default)
    { }

    internal PythonReScalarMatchState(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int[] numbersByPython, int startOffsetInBytes, TimeSpan timeout, string pattern,
        Utf8SliceInvocation invocation, Utf8Regex? candidateRegex)
    {
        this = default;
        _program = program;
        var candidatePlan = candidateRegex?.Inspection.SearchPlan ?? default;
        if (!program.CanMatchEmpty && !program.HasContiguousAnchor && !program.RightToLeft &&
            !program.IgnoreCase && candidatePlan.CanGuideFallbackStarts && candidatePlan.Distance == 0 &&
            candidatePlan.Kind is Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals)
        {
            _candidateRegex = candidateRegex;
        }
        _numbersByPython = numbersByPython;
        _pattern = pattern;
        _timeout = timeout;
        _position = startOffsetInBytes;
        _slots = new Utf8SliceSlots([], []);
        _workspace = new Utf8SliceRunner.AttemptWorkspace();
        _invocation = invocation;
        try
        {
            _validation = Utf8Validation.Validate(input, ref _invocation);
            _slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref _invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { Dispose(); throw ResourceFailure(); }
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
    internal int? LastIndex { get; private set; }
    internal readonly ulong ReservedBytes => _invocation.Ledger.ReservedBytes;
    internal readonly ulong ResultReservation => _results.ReservedBytes;
    internal readonly bool HasCoordinateMap => _hasMap;
    [UnscopedRef] internal ref Utf8SliceInvocation WorkingInvocation => ref _invocation;

    internal T[] AllocateResultArray<T>(ReadOnlySpan<byte> input, int count)
    {
        try { return _results.AllocateArray<T>(count, ref _invocation); }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal void ReleaseResultArray<T>(T[] array)
        => _results.ReleaseArray(array, ref _invocation);

    internal void ReleaseResultsSince(ulong marker)
        => _results.ReleaseSince(marker, ref _invocation);

    internal void AddResult<T>(ReadOnlySpan<byte> input, ref Utf8InvocationResultBuffer<T> buffer, T value)
    {
        try { buffer.Add(value, ref _invocation); }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal T[] CompleteResults<T>(ReadOnlySpan<byte> input, ref Utf8InvocationResultBuffer<T> buffer)
    {
        try { return buffer.ToArray(ref _results, ref _invocation); }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal void ReleaseResults<T>(ref Utf8InvocationResultBuffer<T> buffer)
        => buffer.Release(ref _invocation);

    // Map neutral helper failures only after actual resource denial, without using
    // exception text. Internal kernels and PCRE2 keep their failure vocabulary.
    private readonly Utf8ResourceLimitExceededException ResourceFailure()
        => _invocation.Budget.CreateResourceFailure();

    private readonly RegexMatchTimeoutException TimeoutFailure(ReadOnlySpan<byte> input)
        => new(Encoding.UTF8.GetString(input), _pattern, _timeout);

    internal void Checkpoint(ReadOnlySpan<byte> input)
    {
        try
        {
            if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Scalar discovery exceeded the invocation budget.");
            _invocation.Budget.ThrowIfCancellationRequested();
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), _pattern, _timeout);
        }
    }

    internal bool MoveNext(ReadOnlySpan<byte> input) => MoveNextCore(input, true);

    internal bool MatchAtCurrentPosition(ReadOnlySpan<byte> input) => MoveNextCore(input, false);

    private bool MoveNextCore(ReadOnlySpan<byte> input, bool search)
    {
        // A scalar cursor exclusively owns this admitted scratch. Published
        // snapshots copy values; ResetAttempt never touches retained results.
        var keepWorkspace = false;
        try
        {
            while (!_finished)
            {
                Checkpoint(input);
                if (search && _candidateRegex is not null &&
                    !SeekCandidate(input)) { _finished = true; return false; }
                _invocation.AnchorOriginByte = _position;
                Utf8SliceMatchOutcome outcome;
                try
                {
                    outcome = Utf8SliceRunner.TryMatchAt(_program, input, _position,
                        _slots.Starts, _slots.Ends, false, _retryNonEmpty, true, ref _workspace, ref _invocation);
                }
                catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
                { throw ResourceFailure(); }
                catch (Utf8ExecutionDeadlineExpiredException)
                {
                    throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), _pattern, _timeout);
                }
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                    throw new InvalidOperationException("Scalar discovery exceeded the invocation budget.");
                if (outcome.IsMatch)
                {
                    LastIndex = null;
                    if (outcome.LastClosedSlot >= 0)
                    {
                        var coreNumber = _program.GroupNumbers[outcome.LastClosedSlot + 1];
                        for (var number = 1; number < _numbersByPython.Length; number++)
                        {
                            if ((number & 63) == 1) Checkpoint(input);
                            if (_numbersByPython[number] == coreNumber) { LastIndex = number; break; }
                        }
                        if (LastIndex is null) throw new InvalidOperationException("Completed scalar capture has no profile slot.");
                    }
                    StartOffsetInBytes = _position;
                    EndOffsetInBytes = outcome.EndIndex;
                    _retryNonEmpty = outcome.EndIndex == _position;
                    _position = outcome.EndIndex;
                    keepWorkspace = true;
                    return true;
                }
                _retryNonEmpty = false;
                if (!search) { _finished = true; return false; }
                if (_position == input.Length)
                {
                    _finished = true;
                    return false;
                }
                if (Rune.DecodeFromUtf8(input[_position..], out _, out var width) != OperationStatus.Done)
                    throw Utf8Validation.CreateInvalidUtf8Exception(_position);
                _position += width;
            }
            return false;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        finally { if (!keepWorkspace) _workspace.Release(ref _invocation); }
    }

    private bool SeekCandidate(ReadOnlySpan<byte> input)
    {
        // Native rendering expands lexical folding before planning. Prefixes
        // prove starts only; the profile VM still verifies captures/assertions.
        try
        {
            Utf8ExecutionLimitKind breach;
            var plan = _candidateRegex!.Inspection.SearchPlan;
            if (plan.LiteralSearch is { } literal)
            {
                if (_position < input.Length && input[_position] == literal.Needle[0]) return true;
                breach = Utf8SearchExecutor.TryFindLiteralCandidate(literal, input, _position,
                    input.Length - literal.Length, false, ref _invocation, out _position);
            }
            else
            {
                breach = Utf8SearchExecutor.TryFindLiteralFamilyCandidate(plan.AlternateLiteralsUtf8!, false,
                    input, _position, input.Length, false, ref _invocation, out var candidate, out var found);
                _position = found ? candidate.Index : -1;
            }
            if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
            if (_position >= 0) return true;
            Checkpoint(input);
            return false;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal PythonReGroupData SnapshotValueData(ReadOnlySpan<byte> input)
    {
        Checkpoint(input);
        try
        {
            var start = StartOffsetInBytes;
            var end = EndOffsetInBytes;
            // Collections that publish coordinates reuse their single map. A
            // one-shot fixed value still needs no whole-subject map allocation.
            var startUtf16 = _validation.IsAscii ? start : _hasMap ? _map.GetUtf16OffsetForByteOffset(start)
                : Utf8Validation.Validate(input[..start], ref _invocation).Utf16Length;
            var endUtf16 = _validation.IsAscii ? end : _hasMap ? _map.GetUtf16OffsetForByteOffset(end)
                : startUtf16 + Utf8Validation.Validate(input[start..end], ref _invocation).Utf16Length;
            var value = new PythonReGroupData
            {
                Number = 0, Success = true,
                StartOffsetInBytes = start, EndOffsetInBytes = end,
                StartOffsetInUtf16 = startUtf16, EndOffsetInUtf16 = endUtf16,
            };
            Checkpoint(input);
            return value;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal PythonReNameEntry[] SnapshotNameEntries(ReadOnlySpan<byte> input, PythonReNameEntry[] names)
    {
        Checkpoint(input);
        var copied = AllocateResultArray<PythonReNameEntry>(input, names.Length);
        try { Utf8InvocationResultLedger.Copy<PythonReNameEntry>(names, copied, ref _invocation); }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
        Checkpoint(input);
        return copied;
    }

    internal PythonReGroupData[] SnapshotGroups(ReadOnlySpan<byte> input)
    {
        Checkpoint(input);
        EnsureMap(input);
        var groups = AllocateResultArray<PythonReGroupData>(input, _numbersByPython.Length);
        for (var pythonGroup = 0; pythonGroup < groups.Length; pythonGroup++)
        {
            if ((pythonGroup & 255) == 0) Checkpoint(input);
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
        Checkpoint(input);
        return groups;
    }

    internal bool TryGetCaptureByteRange(int number, out int start, out int end)
    {
        start = end = -1;
        if ((uint)number >= (uint)_numbersByPython.Length) return false;
        var slot = Array.BinarySearch(_program.GroupNumbers, _numbersByPython[number]) - 1;
        start = number == 0 ? StartOffsetInBytes : slot >= 0 ? _slots.Starts[slot] : -1;
        end = number == 0 ? EndOffsetInBytes : slot >= 0 ? _slots.Ends[slot] : -1;
        // The profile runs fixed-width lookbehind forward as well. Byte-only
        // template expansion needs neither edge normalization nor a map.
        if (start < 0 || end < 0) return false;
        if (end < start) throw new InvalidOperationException("Forward scalar capture has reversed edges.");
        return true;
    }

    internal void EnsureMap(ReadOnlySpan<byte> input)
    {
        if (!_hasMap)
        {
            try
            {
                _map = Utf8BoundaryMap.Create(input, _validation, ref _invocation, out _mapReservation);
                _hasMap = true;
            }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            { throw ResourceFailure(); }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), _pattern, _timeout);
            }
        }
    }

    internal Utf8PythonDetailedMatchData SnapshotDetailed(ReadOnlySpan<byte> input, PythonReNameEntry[] names)
    {
        Checkpoint(input);
        EnsureMap(input);
        // Owned snapshots project directly from capture slots. Borrowed contexts
        // still use SnapshotGroups; no temporary range array is needed here.
        var projected = AllocateResultArray<Utf8PythonGroupMatchData>(input, _numbersByPython.Length);
        for (var i = 0; i < projected.Length; i++)
        {
            Checkpoint(input);
            if (!TryGetCaptureByteRange(i, out var start, out var end))
            {
                projected[i] = new Utf8PythonGroupMatchData { Number = i, ValueText = string.Empty };
                continue;
            }
            projected[i] = new Utf8PythonGroupMatchData
            {
                Number = i, Success = true,
                StartOffsetInBytes = start, EndOffsetInBytes = end,
                StartOffsetInUtf16 = _map.GetUtf16OffsetForByteOffset(start),
                EndOffsetInUtf16 = _map.GetUtf16OffsetForByteOffset(end),
                HasContiguousByteRange = true,
                ValueText = GetText(input, start, end),
            };
        }
        var copiedNames = SnapshotNameEntries(input, names);
        var result = new Utf8PythonDetailedMatchData { Groups = projected, NameEntries = copiedNames, LastIndex = LastIndex };
        Checkpoint(input);
        return result;
    }

    internal string GetText(ReadOnlySpan<byte> input, int start, int end)
    {
        Checkpoint(input);
        try
        {
            // String-only results need the selected scalar range's char count,
            // not a coordinate map for the complete subject. Detailed results
            // already own a map and can reuse its derived coordinates.
            var count = _hasMap
                ? _map.GetUtf16OffsetForByteOffset(end) - _map.GetUtf16OffsetForByteOffset(start)
                : Utf8Validation.Validate(input[start..end], ref _invocation).Utf16Length;
            var text = _results.DecodeUtf8(input[start..end], count, ref _invocation);
            Checkpoint(input);
            return text;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), _pattern, _timeout);
        }
    }

    internal byte[] GetBytes(ReadOnlySpan<byte> input, int start, int end)
    {
        Checkpoint(input);
        var bytes = AllocateResultArray<byte>(input, end - start);
        CopyBytes(input, input[start..end], bytes);
        return bytes;
    }

    internal Utf8SliceOutputWriter CreateOutputWriter(ReadOnlySpan<byte> input)
    {
        try
        {
            if (!Utf8SliceOutputWriter.Rent(256, ref _invocation, out var writer))
                throw new InvalidOperationException("Python output exceeded the working storage budget.");
            return writer;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal void AppendBytes(ReadOnlySpan<byte> input, scoped ref Utf8SliceOutputWriter writer, scoped ReadOnlySpan<byte> bytes)
    {
        try
        {
            Checkpoint(input);
            if (!writer.Append(bytes, ref _invocation))
                throw new InvalidOperationException("Python output exceeded the working storage budget.");
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal void AppendText(ReadOnlySpan<byte> input, scoped ref Utf8SliceOutputWriter writer, string text)
    {
        try
        {
            // Observe a callback's token/deadline before scanning its return.
            Checkpoint(input);
            if (!writer.AppendUtf16(text.AsSpan(0, text.Length), ref _invocation))
                throw new InvalidOperationException("Python output exceeded the working storage budget.");
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal byte[] CompleteOutputBytes(ReadOnlySpan<byte> input, scoped ref Utf8SliceOutputWriter writer, bool validateOutput)
    {
        try
        {
            Checkpoint(input);
            if (validateOutput) _ = Utf8Validation.Validate(writer.WrittenBytes, ref _invocation);
            var result = AllocateResultArray<byte>(input, writer.Length);
            CopyBytes(input, writer.WrittenBytes, result);
            writer.Release(ref _invocation);
            return result;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal string CompleteOutputText(ReadOnlySpan<byte> input, scoped ref Utf8SliceOutputWriter writer)
    {
        try
        {
            Checkpoint(input);
            var validation = Utf8Validation.Validate(writer.WrittenBytes, ref _invocation);
            var result = _results.DecodeUtf8(writer.WrittenBytes, validation.Utf16Length, ref _invocation);
            Checkpoint(input);
            writer.Release(ref _invocation);
            return result;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal void ReleaseOutput(scoped ref Utf8SliceOutputWriter writer)
        => writer.Release(ref _invocation);

    internal void CopyBytes(ReadOnlySpan<byte> input, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        try
        {
            Utf8SliceBufferWork.Copy(source, destination, ref _invocation);
            Checkpoint(input);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(Encoding.UTF8.GetString(input), _pattern, _timeout);
        }
    }

    internal void Dispose()
    {
        _workspace.Release(ref _invocation);
        _results.Release(ref _invocation);
        Utf8SliceRunner.ReleaseSlots(_slots, ref _invocation);
        _slots = new Utf8SliceSlots([], []);
        _invocation.Ledger.Release(_mapReservation);
        _mapReservation = 0;
        _map = default;
        _hasMap = false;
        _finished = true;
    }
}
