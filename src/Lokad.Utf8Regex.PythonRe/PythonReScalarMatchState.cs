using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
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
    private readonly int _leadingAsciiLow;
    private readonly int _leadingAsciiHigh;
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
        int startOffsetInBytes, TimeSpan timeout, string pattern,
        Utf8SliceInvocation invocation, Utf8Regex? candidateRegex)
    {
        this = default;
        _program = program;
        _leadingAsciiLow = _leadingAsciiHigh = -1;
        _pattern = pattern;
        _timeout = timeout;
        _position = startOffsetInBytes;
        _slots = new Utf8SliceSlots([], []);
        _workspace = new Utf8SliceRunner.AttemptWorkspace();
        _invocation = invocation;
        try
        {
            _validation = Utf8Validation.Validate(input, ref _invocation);
            // The native rendering proves only necessity. A missing exact
            // literal can finish before acquiring scalar scratch; a present
            // literal retains the original Python VM and capture semantics.
            if (!program.CanMatchEmpty && !program.IgnoreCase && candidateRegex is { } required &&
                required.RejectMissingExactRequiredLiteral(input, ref _invocation))
            {
                Checkpoint(input);
                _finished = true;
                return;
            }
            if (CanUseCandidateRegex(program, candidateRegex)) _candidateRegex = candidateRegex;
            if (_candidateRegex is null && TryGetLeadingAsciiRange(program, _validation.IsAscii, out var low, out var high))
            {
                _leadingAsciiLow = low;
                _leadingAsciiHigh = high;
            }
            _slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref _invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { Dispose(); throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            Dispose();
            throw TimeoutFailure(input);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    // Shared with preparation so the facade retains only Core hints this cursor
    // can actually consume. Captured native results still need scalar verification.
    internal static bool CanUseCandidateRegex(Utf8SliceProgram program, Utf8Regex? candidateRegex)
    {
        var candidatePlan = candidateRegex?.Inspection.SearchPlan ?? default;
        var nativeResult = program.GroupCount == 0 && candidateRegex?.CanFindNativeLiteralForResult == true;
        return !program.CanMatchEmpty && !program.HasContiguousAnchor && !program.RightToLeft &&
            !program.IgnoreCase && (nativeResult ||
                candidatePlan.CanGuideFallbackStarts && candidatePlan.Distance == 0 &&
                candidatePlan.Kind is Utf8SearchKind.ExactAsciiLiteral or Utf8SearchKind.ExactAsciiLiterals or Utf8SearchKind.ExactUtf8Literals);
    }

    internal int StartOffsetInBytes { get; private set; }
    internal int EndOffsetInBytes { get; private set; }
    internal readonly ulong ReservedBytes => _invocation.Ledger.ReservedBytes;
    internal readonly ulong ResultReservation => _results.ReservedBytes;
    internal readonly bool HasCoordinateMap => _hasMap;
    internal readonly bool IsAscii => _validation.IsAscii;
    [UnscopedRef] internal ref Utf8SliceInvocation WorkingInvocation => ref _invocation;

    internal T[] AllocateResultArray<T>(ReadOnlySpan<byte> input, int count)
    {
        try { return _results.AllocateArray<T>(count, ref _invocation); }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal void ReleaseResultsSince(ulong marker)
        => _results.ReleaseSince(marker, ref _invocation);

    internal ulong TransferResultsSince(ulong marker)
        => _results.TransferSince(marker, ref _invocation);

    internal Utf8PythonRetainedRangeMatch RetainRanges(ReadOnlySpan<byte> input, Utf8PythonRangeMatch match)
    {
        try
        {
            var index = 0;
            foreach (var entry in match.NameEntries)
            {
                if ((index++ & 255) == 0) Checkpoint(input);
                _results.ReserveSharedStorage(Utf8ConstructionBudget.StringBytes(entry.Name.Length), ref _invocation);
            }
            return _results.AllocateObject((Match: match, Allowance: _invocation.Ledger.MemoryAllowance!),
                static state => new Utf8PythonRetainedRangeMatch(state.Match, state.Allowance), ref _invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal Utf8PythonRetainedSubnResult RetainSubstitution(ReadOnlySpan<byte> input, Utf8PythonSubnUtf8Result result)
    {
        try
        {
            return _results.AllocateObject((Result: result, Allowance: _invocation.Ledger.MemoryAllowance!),
                static state => new Utf8PythonRetainedSubnResult(state.Result, state.Allowance), ref _invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

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

    internal readonly RegexMatchTimeoutException TimeoutFailure(ReadOnlySpan<byte> input)
        => Utf8Regex.CreateMatchTimeoutException(_invocation.Ledger.HasMemoryAllowance ? ReadOnlySpan<byte>.Empty : input,
            _pattern, _timeout, _invocation.Budget.Limits);

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
            throw TimeoutFailure(input);
        }
    }

    internal bool MoveNext(ReadOnlySpan<byte> input) => MoveNextCore(input, true);

    internal bool MatchAtCurrentPosition(ReadOnlySpan<byte> input) => MoveNextCore(input, false);

    internal static bool TryGetLeadingAsciiRange(Utf8SliceProgram program, bool asciiInput, out int low, out int high)
    {
        low = high = -1;
        if (program.CanMatchEmpty || program.HasContiguousAnchor || program.RightToLeft || program.IgnoreCase ||
            program.Instructions.Length == 0) return false;
        var index = FindLeadingPredicate(program.Instructions);
        if (index < 0) return false;
        var atom = program.Instructions[index];
        if (atom is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } literal && (uint)literal.A < 128)
        {
            low = high = literal.A;
            return true;
        }

        if (atom is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } &&
            !(asciiInput && atom is { Kind: Utf8SliceInstructionKind.CategorySet, B: 0, C: 0 }))
            return false;
        var mask = program.SetMasks[atom.A];
        if (mask.Length != 16) return false;
        // ASCII masks store bit n at byte n/8, bit n%8, independently of host endian.
        var lower = BinaryPrimitives.ReadUInt64LittleEndian(mask);
        var upper = BinaryPrimitives.ReadUInt64LittleEndian(mask.AsSpan(8));
        if (atom.Kind == Utf8SliceInstructionKind.CategorySet)
        {
            // The stored mask is only one term in a category union. Only the
            // profile's fixed predicates supply these exact ASCII projections;
            // a general table/subtraction keeps the scalar candidate loop.
            var members = program.CategorySetMembers[atom.D];
            if (members.Length != 1 || members[0].Negated) return false;
            var predicate = members[0].Table.ScalarPredicate;
            var ascii = predicate == PythonReScalarClassData.IsDigit ? PythonReScalarClassData.DigitAscii
                : predicate == PythonReScalarClassData.IsWord ? PythonReScalarClassData.WordAscii
                : predicate == PythonReScalarClassData.IsSpace ? PythonReScalarClassData.SpaceAscii : null;
            if (ascii is null) return false;
            lower |= BinaryPrimitives.ReadUInt64LittleEndian(ascii);
            upper |= BinaryPrimitives.ReadUInt64LittleEndian(ascii.AsSpan(8));
        }
        if ((lower | upper) == 0) return false;
        var first = lower != 0 ? BitOperations.TrailingZeroCount(lower) : 64 + BitOperations.TrailingZeroCount(upper);
        var last = upper != 0 ? 127 - BitOperations.LeadingZeroCount(upper) : 63 - BitOperations.LeadingZeroCount(lower);
        // This is a necessary start interval, not a complete class predicate.
        // Holes are harmless false candidates: the original VM verifies the mask.
        low = first;
        high = last;
        return true;
    }

    private static int FindLeadingPredicate(ReadOnlySpan<Utf8SliceInstruction> code)
    {
        // This optional proof is bounded independently of pattern depth/size.
        // Every zero-width branch must converge on the same consuming atom;
        // it remains necessary only, and the original VM verifies assertions.
        Span<int> pending = stackalloc int[16];
        var count = 0;
        var index = 0;
        var leading = -1;
        for (var work = 0; work < 64 && (uint)index < (uint)code.Length; work++)
        {
            var instruction = code[index];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.CaptureStart:
                case Utf8SliceInstructionKind.CaptureEnd:
                case Utf8SliceInstructionKind.Anchor:
                case Utf8SliceInstructionKind.Boundary:
                case Utf8SliceInstructionKind.Lookbehind:
                    index++;
                    continue;
                case Utf8SliceInstructionKind.RepeatGreedy when instruction.RepeatMinimum > 0:
                    index++;
                    continue;
                case Utf8SliceInstructionKind.Jump:
                    index = instruction.A;
                    continue;
                case Utf8SliceInstructionKind.Fork when instruction.B == 0:
                    if (count == pending.Length) return -1;
                    pending[count++] = instruction.A;
                    index++;
                    continue;
                case Utf8SliceInstructionKind.Fork when instruction.B == 1:
                    // Lookahead restores its input position. Recognize the
                    // lowerer's negative Commit/Fail and positive Commit/Jump/
                    // Fail trailers; never treat its body as consuming input.
                    var target = instruction.A;
                    if (target < index + 3 || target > code.Length ||
                        code[target - 2].Kind != Utf8SliceInstructionKind.Commit) return -1;
                    if (code[target - 1].Kind == Utf8SliceInstructionKind.Fail) index = target;
                    else if (target < code.Length && code[target].Kind == Utf8SliceInstructionKind.Fail &&
                        code[target - 1] is { Kind: Utf8SliceInstructionKind.Jump } jump && jump.A == target + 1)
                        index = target + 1;
                    else return -1;
                    continue;
            }
            if (leading >= 0 && leading != index) return -1;
            leading = index;
            if (count == 0) return leading;
            index = pending[--count];
        }
        return -1;
    }

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
                Utf8SliceMatchOutcome outcome;
                try
                {
                    // A complete native literal match needs no capture/VM state.
                    // Match and the derived FullMatch program use search=false;
                    // their additional position/end constraints keep the VM.
                    if (search && _program.GroupCount == 0 && _candidateRegex is { } candidate &&
                        candidate.TryFindNativeLiteralForResult(input, _position, ref _invocation,
                            out var found, out var start, out var end))
                    {
                        if (!found) { _finished = true; return false; }
                        _position = start;
                        _invocation.AnchorOriginByte = start;
                        outcome = new Utf8SliceMatchOutcome(true, end, Utf8ExecutionLimitKind.None, null, null);
                    }
                    else
                    {
                        // A necessary start hint still requires profile verification.
                        // Avoid its search helper when the current byte meets the hint.
                        if (search && (_candidateRegex is not null ||
                            _leadingAsciiLow >= 0 && ((uint)_position >= (uint)input.Length ||
                                (uint)(input[_position] - _leadingAsciiLow) > (uint)(_leadingAsciiHigh - _leadingAsciiLow))) &&
                            !SeekCandidate(input)) { _finished = true; return false; }
                        _invocation.AnchorOriginByte = _position;
                        outcome = Utf8SliceRunner.TryMatchAt(_program, input, _position,
                            _slots.Starts, _slots.Ends, false, _retryNonEmpty, true, ref _workspace, ref _invocation);
                    }
                }
                catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
                { throw ResourceFailure(); }
                catch (Utf8ExecutionDeadlineExpiredException)
                {
                    throw TimeoutFailure(input);
                }
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                    throw new InvalidOperationException("Scalar discovery exceeded the invocation budget.");
                if (outcome.IsMatch)
                {
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
                    // The VM can fail on its last instruction after the entry
                    // poll. Keep EOF and pure Count publication under the
                    // original authority, just like candidate-search declines.
                    Checkpoint(input);
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
            if (_candidateRegex is not { } candidateRegex)
            {
                if (_leadingAsciiLow < 0) return true;
                while (_position < input.Length)
                {
                    if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None) throw ResourceFailure();
                    var length = Math.Min(2048, input.Length - _position);
                    var block = input.Slice(_position, length);
                    var offset = _leadingAsciiLow == _leadingAsciiHigh
                        ? block.IndexOf((byte)_leadingAsciiLow)
                        : block.IndexOfAnyInRange((byte)_leadingAsciiLow, (byte)_leadingAsciiHigh);
                    if (offset >= 0) { _position += offset; return true; }
                    _position += length;
                }
                Checkpoint(input);
                return false;
            }
            var plan = candidateRegex.Inspection.SearchPlan;
            if (plan.LiteralSearch is { } literal)
            {
                if (_position < input.Length && input[_position] == literal.Needle[0]) return true;
                breach = Utf8SearchExecutor.TryFindLiteralCandidate(literal, input, _position,
                    input.Length - literal.Length, false, ref _invocation, out _position);
            }
            else if (plan.AlternateLiteralsUtf8 is { } literals)
            {
                breach = Utf8SearchExecutor.TryFindLiteralFamilyCandidate(literals, false,
                    input, _position, input.Length, false, ref _invocation, out var candidate, out var found);
                _position = found ? candidate.Index : -1;
            }
            else return true;
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
        var groups = AllocateResultArray<PythonReGroupData>(input, _program.GroupCount + 1);
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
        if ((uint)number > (uint)_program.GroupCount) return false;
        // Python group n owns slot n-1; group0 projects the whole-match range.
        var slot = number - 1;
        start = number == 0 ? StartOffsetInBytes : _slots.Starts[slot];
        end = number == 0 ? EndOffsetInBytes : _slots.Ends[slot];
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
                throw TimeoutFailure(input);
            }
        }
    }

    internal int? GetLastIndex(ReadOnlySpan<byte> input)
    {
        if (_program.GroupCount == 0) return null;
        try
        {
            // The successful workspace keeps its capture journal until the next
            // advance. Only detailed/context results observe completion order;
            // value/string/UTF8 discovery needs no extra scan or metadata state.
            var breach = _workspace.Stacks.FindLastClosedSlot(ref _invocation, out var slot);
            if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
            if (slot < 0) return null;
            Checkpoint(input);
            return slot + 1;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(input); }
    }

    internal Utf8PythonRangeMatch SnapshotRanges(ReadOnlyMemory<byte> input, PythonReNameEntry[] names)
    {
        var bytes = input.Span;
        var lastIndex = GetLastIndex(bytes);
        var groups = AllocateResultArray<Utf8PythonCaptureRange>(bytes, _program.GroupCount + 1);
        for (var number = 0; number < groups.Length; number++)
        {
            if ((number & 255) == 0) Checkpoint(bytes);
            groups[number] = TryGetCaptureByteRange(number, out var start, out var end)
                ? new Utf8PythonCaptureRange { Number = number, Success = true, StartOffsetInBytes = start, EndOffsetInBytes = end }
                : new Utf8PythonCaptureRange { Number = number };
        }
        var copiedNames = SnapshotNameEntries(bytes, names);
        Checkpoint(bytes);
        return new Utf8PythonRangeMatch(input, groups, copiedNames, lastIndex);
    }

    internal Utf8PythonDetailedMatchData SnapshotDetailed(ReadOnlySpan<byte> input, PythonReNameEntry[] names)
    {
        var lastIndex = GetLastIndex(input);
        Checkpoint(input);
        EnsureMap(input);
        // Owned snapshots project directly from capture slots. Borrowed contexts
        // still use SnapshotGroups; no temporary range array is needed here.
        var projected = AllocateResultArray<Utf8PythonGroupMatchData>(input, _program.GroupCount + 1);
        for (var i = 0; i < projected.Length; i++)
        {
            Checkpoint(input);
            if (!TryGetCaptureByteRange(i, out var start, out var end))
            {
                projected[i] = new Utf8PythonGroupMatchData { Number = i, ValueText = string.Empty };
                continue;
            }
            var startInUtf16 = _map.GetUtf16OffsetForByteOffset(start);
            var endInUtf16 = _map.GetUtf16OffsetForByteOffset(end);
            projected[i] = new Utf8PythonGroupMatchData
            {
                Number = i, Success = true,
                StartOffsetInBytes = start, EndOffsetInBytes = end,
                StartOffsetInUtf16 = startInUtf16,
                EndOffsetInUtf16 = endInUtf16,
                HasContiguousByteRange = true,
                // Equal ranges can share owned immutable text; keep every
                // group's independently projected metadata and mutable slot.
                ValueText = i > 0 && start == StartOffsetInBytes && end == EndOffsetInBytes
                    ? projected[0].ValueText : GetText(input, start, end, endInUtf16 - startInUtf16),
            };
        }
        var copiedNames = SnapshotNameEntries(input, names);
        var result = new Utf8PythonDetailedMatchData { Groups = projected, NameEntries = copiedNames, LastIndex = lastIndex };
        Checkpoint(input);
        return result;
    }

    internal string GetText(ReadOnlySpan<byte> input, int start, int end, int? characterCount)
    {
        Checkpoint(input);
        try
        {
            // String-only results need the selected scalar range's char count,
            // not a coordinate map for the complete subject. Detailed results
            // already project coordinates and can supply their character count.
            // Original whole-subject validation already proves the character
            // count for ASCII captures; do not validate those bytes again.
            var count = characterCount ?? (_validation.IsAscii ? end - start
                : start == 0 && end == input.Length ? _validation.Utf16Length : _hasMap
                ? _map.GetUtf16OffsetForByteOffset(end) - _map.GetUtf16OffsetForByteOffset(start)
                : Utf8Validation.Validate(input[start..end], ref _invocation).Utf16Length);
            var text = _results.DecodeUtf8(input[start..end], count, ref _invocation);
            Checkpoint(input);
            return text;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw ResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw TimeoutFailure(input);
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
            throw TimeoutFailure(input);
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
