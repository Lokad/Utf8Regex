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
// advancement and returned capture ranges are bytes.
// Python retains the last capture, so discovery does not collect capture history.
internal struct PythonReScalarMatchState
{
    private Utf8SliceProgram _program;
    private Utf8Regex? _candidateRegex;
    private int _leadingAsciiLow;
    private int _leadingAsciiHigh;
    private int _laterAsciiDistance;
    private byte _laterAsciiLow;
    private byte _laterAsciiHigh;
    private int _delimiter;
    private int _delimiterLeadingClass;
    // Production retains the expression for lazy diagnostics. Standalone
    // diagnostic/test cursors can supply text without constructing a facade.
    private object _timeoutSource;
    private TimeSpan _timeout;
    private Utf8SliceInvocation _invocation;
    private Utf8InvocationResultLedger _results;
    private Utf8SliceSlots _slots;
    private Utf8SliceRunner.AttemptWorkspace _workspace;
    private bool _leadingAsciiMayMatchUnicode;
    private bool _retryNonEmpty;
    private bool _finished;
    private int _position;

    internal static void Initialize(out PythonReScalarMatchState state,
        ReadOnlySpan<byte> input, Utf8SliceProgram program, int startOffsetInBytes, TimeSpan timeout,
        object pattern, scoped in Utf8SliceInvocation invocation, Utf8Regex? candidateRegex, bool prepareSearch = true)
    {
        state = default;
        state._invocation = invocation;
        state.Initialize(input, program, startOffsetInBytes, timeout, pattern, candidateRegex, prepareSearch);
    }

    // The owner establishes the invocation in a fresh state before preparation.
    internal void Initialize(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startOffsetInBytes, TimeSpan timeout, object pattern, Utf8Regex? candidateRegex, bool prepareSearch = true)
    {
        _program = program;
        _leadingAsciiLow = _leadingAsciiHigh = -1;
        _delimiter = _delimiterLeadingClass = -1;
        _timeoutSource = pattern;
        _timeout = timeout;
        _position = startOffsetInBytes;
        _slots = new Utf8SliceSlots([], []);
        try
        {
            // Python re consumes text. The caller supplies valid unchanged UTF-8;
            // admission/interruption still precede preparation and early results.
            Checkpoint();
            if (program.RejectsByFinalAscii(input))
            {
                if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None) throw ResourceFailure();
                _finished = true;
                return;
            }
            // Anchored calls cannot skip a rejected starting byte. Reuse the
            // bounded necessary-start proof before scanning for a required
            // literal elsewhere; possible starts keep that useful screen.
            // Preparation and final publication still use this invocation.
            if (program.Instructions[0].Kind == Utf8SliceInstructionKind.Fail ||
                !prepareSearch && candidateRegex is not null &&
                (uint)startOffsetInBytes <= (uint)input.Length &&
                TryGetLeadingAsciiRange(program, out var startLow, out var startHigh, out var widerStart) && !widerStart &&
                (startOffsetInBytes == input.Length ||
                 (uint)(input[startOffsetInBytes] - startLow) > (uint)(startHigh - startLow)) ||
                !program.CanMatchEmpty && !program.IgnoreCase && candidateRegex is { } required &&
                required.RejectMissingExactRequiredLiteral(input, ref _invocation))
            {
                Checkpoint();
                _finished = true;
                return;
            }
            // Anchored Match/FullMatch never seek a candidate. Their existing
            // operation intent avoids preparing hints they cannot consume.
            if (prepareSearch)
            {
                if (CanUseCandidateRegex(program, candidateRegex)) _candidateRegex = candidateRegex;
                if (_candidateRegex is null)
                {
                    if (TryGetLeadingAsciiRange(program, out var low, out var high, out var mayMatchUnicode))
                    {
                        _leadingAsciiLow = low;
                        _leadingAsciiHigh = high;
                        _leadingAsciiMayMatchUnicode = mayMatchUnicode;
                        if (!mayMatchUnicode && TryGetLaterAsciiRange(program, low, high, ref _invocation,
                            out var distance, out var laterLow, out var laterHigh))
                        {
                            _laterAsciiDistance = distance;
                            _laterAsciiLow = (byte)laterLow;
                            _laterAsciiHigh = (byte)laterHigh;
                        }
                    }
                    // The program and subject extent are immutable for this
                    // discovery lifetime. Prepare the existing direct-window
                    // proof once, rather than rediscovering it for every match.
                    if (input.Length >= 16 && !program.CanMatchEmpty && !program.HasContiguousAnchor &&
                        !program.RightToLeft && !program.IgnoreCase)
                    {
                        var (delimiter, leadingClass, _, _, directClassLiteral) = program.LeadingClassDelimiter;
                        // Short direct windows after a leading boundary can
                        // rewind Unicode scalars with the exact class predicate.
                        // The start hint still admits all possible wider scalars.
                        if (directClassLiteral &&
                            (program.Instructions[leadingClass] is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } atom &&
                                program.SetMasks[atom.A].Length == 16 ||
                             _leadingAsciiLow >= 0 && program.Instructions[0] is { Kind: Utf8SliceInstructionKind.Boundary, A: 1 }))
                        {
                            _delimiter = delimiter;
                            _delimiterLeadingClass = leadingClass;
                        }
                    }
                }
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
    [UnscopedRef] internal ref Utf8SliceInvocation WorkingInvocation => ref _invocation;

    internal T[] AllocateResultArray<T>(int count)
    {
        return _results.AllocateArray<T>(count, ref _invocation);
    }

    internal void ReleaseResultsSince(ulong marker)
        => _results.ReleaseSince(marker, ref _invocation);

    internal ulong TransferResultsSince(ulong marker)
        => _results.TransferSince(marker, ref _invocation);

    internal Utf8PythonRetainedRangeMatch RetainRanges(Utf8PythonRangeMatch match)
    {
        return _results.AllocateObject((Match: match, Allowance: _invocation.Ledger.MemoryAllowance!),
            static state => new Utf8PythonRetainedRangeMatch(state.Match, state.Allowance), ref _invocation);
    }

    internal Utf8PythonRetainedSubnResult RetainSubstitution(Utf8PythonSubnUtf8Result result)
    {
        return _results.AllocateObject((Result: result, Allowance: _invocation.Ledger.MemoryAllowance!),
            static state => new Utf8PythonRetainedSubnResult(state.Result, state.Allowance), ref _invocation);
    }

    internal void AddResult<T>(ref Utf8InvocationResultBuffer<T> buffer, T value)
    {
        buffer.Add(value, ref _invocation);
    }

    internal T[] CompleteResults<T>(ref Utf8InvocationResultBuffer<T> buffer)
    {
        return buffer.ToArray(ref _results, ref _invocation);
    }

    internal void ReleaseResults<T>(ref Utf8InvocationResultBuffer<T> buffer)
        => buffer.Release(ref _invocation);

    // Construction owns its failure boundary. After construction, logical operations
    // map neutral helper failures before releasing this invocation or cursor state.
    private readonly Utf8ResourceLimitExceededException ResourceFailure()
        => _invocation.Budget.CreateResourceFailure();

    internal readonly RegexMatchTimeoutException TimeoutFailure(ReadOnlySpan<byte> input)
        => Utf8Regex.CreateMatchTimeoutException(_invocation.Ledger.HasMemoryAllowance ? ReadOnlySpan<byte>.Empty : input,
            _timeoutSource is Utf8PythonRegex regex ? regex.Pattern : (string)_timeoutSource,
            _timeout, _invocation.Budget.Limits);

    internal void Checkpoint()
    {
        if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
            throw ResourceFailure();
        _invocation.Budget.ThrowIfCancellationRequested();
    }

    internal bool MatchAtCurrentPosition(ReadOnlySpan<byte> input, bool requireFullInput = false)
    {
        var matched = false;
        try
        {
            if (_finished) return false;
            Checkpoint();
            matched = RecordMatch(VerifyCurrentPosition(input, existenceOnly: false, requireFullInput));
            _finished = !matched;
            return matched;
        }
        finally { if (!matched) _workspace.Release(ref _invocation); }
    }

    private static bool TryGetLaterAsciiRange(Utf8SliceProgram program, int leadingLow, int leadingHigh,
        ref Utf8SliceInvocation invocation, out int distance, out int low, out int high)
    {
        distance = 0;
        low = high = -1;
        var code = program.Instructions;
        for (var index = 0; index < code.Length; index++)
        {
            // Inspect existing bounded program storage without retaining another
            // descriptor. Major matching work remains in the original verifier.
            if ((index & 255) == 0 && invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
                throw invocation.Budget.CreateResourceFailure();
            var atom = code[index];
            if (atom.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                continue;
            if (atom is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && (uint)atom.A < 128)
            {
                distance++;
                continue;
            }
            // The same bounded first-consumer proof must cover every suffix
            // branch. Optional/empty suffixes and opaque effects decline. A
            // disjoint later interval rejects repeated false prefix heads.
            return distance > 0 && TryGetLeadingAsciiRange(program, out low, out high, out var widerStart, index) && !widerStart &&
                (high < leadingLow || low > leadingHigh);
        }
        return false;
    }

    internal static bool TryGetLeadingAsciiRange(Utf8SliceProgram program, out int low, out int high,
        out bool mayMatchUnicode, int startIndex = 0)
    {
        low = high = -1;
        mayMatchUnicode = false;
        if (program.CanMatchEmpty || program.HasContiguousAnchor || program.RightToLeft || program.IgnoreCase ||
            program.Instructions.Length == 0) return false;
        // Union necessary starts across zero-width branches. This proof stays
        // bounded independently of pattern depth; the VM still verifies every
        // candidate and retains the original captures/assertions/repeat order.
        var code = program.Instructions;
        Span<int> pending = stackalloc int[16];
        var count = 0;
        var index = startIndex;
        var minimum = 128;
        var maximum = -1;
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
                case Utf8SliceInstructionKind.RepeatExit:
                    index++;
                    continue;
                case Utf8SliceInstructionKind.RepeatGreedy:
                    if (instruction.RepeatMinimum == 0)
                    {
                        if (count == pending.Length) return false;
                        pending[count++] = instruction.A;
                    }
                    index++;
                    continue;
                case Utf8SliceInstructionKind.Jump:
                    index = instruction.A;
                    continue;
                case Utf8SliceInstructionKind.Fork when instruction.B == 0:
                    if (count == pending.Length) return false;
                    pending[count++] = instruction.A;
                    index++;
                    continue;
                case Utf8SliceInstructionKind.Fork when instruction.B == 2:
                    // An atomic fork's alternate is Fail, not an empty success.
                    // Its body must supply the first consumer. The VM still
                    // executes every atomic commit and verifies the whole match.
                    index++;
                    continue;
                case Utf8SliceInstructionKind.Fork when instruction.B == 1:
                    // Lookahead restores its input position. Skip only the
                    // lowerer's known negative/positive assertion trailers.
                    var target = instruction.A;
                    if (target < index + 3 || target > code.Length ||
                        code[target - 2].Kind != Utf8SliceInstructionKind.Commit) return false;
                    if (code[target - 1].Kind == Utf8SliceInstructionKind.Fail) index = target;
                    else if (target < code.Length && code[target].Kind == Utf8SliceInstructionKind.Fail &&
                        code[target - 1] is { Kind: Utf8SliceInstructionKind.Jump } jump && jump.A == target + 1)
                        index = target + 1;
                    else return false;
                    continue;
            }
            if (!TryGetAsciiInterval(program, instruction, out var first, out var last)) return false;
            minimum = Math.Min(minimum, first);
            maximum = Math.Max(maximum, last);
            mayMatchUnicode |= instruction.Kind == Utf8SliceInstructionKind.CategorySet;
            if (count == 0)
            {
                low = minimum;
                high = maximum;
                return true;
            }
            index = pending[--count];
        }
        return false;
    }

    private static bool TryGetAsciiInterval(Utf8SliceProgram program, Utf8SliceInstruction atom, out int low, out int high)
    {
        low = high = -1;
        if (atom is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } literal && (uint)literal.A < 128)
        {
            low = high = literal.A;
            return true;
        }

        if (atom is not { Kind: Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.CategorySet, B: 0, C: 0 })
            return false;
        var mask = program.SetMasks[atom.A];
        if (mask.Length != 16) return false;
        if (atom.Kind == Utf8SliceInstructionKind.CategorySet)
        {
            // Python lowering puts each predefined predicate's complete ASCII
            // projection in this mask, including when it combines positive
            // bracket operands. A range wholly above ASCII adds no bits.
            // Bound this proof and leave other table kinds to the verifier.
            var members = program.CategorySetMembers[atom.D];
            if (members.Length > 4) return false;
            foreach (var member in members)
            {
                if (member.Negated || member.Table is not ({ ScalarPredicate: not null } or
                    { Mask: 0, ClassProgram: null, Ranges: [>= 128, _] })) return false;
            }
        }
        // ASCII masks store bit n at byte n/8, bit n%8, independently of host endian.
        var lower = BinaryPrimitives.ReadUInt64LittleEndian(mask);
        var upper = BinaryPrimitives.ReadUInt64LittleEndian(mask.AsSpan(8));
        if ((lower | upper) == 0) return false;
        var first = lower != 0 ? BitOperations.TrailingZeroCount(lower) : 64 + BitOperations.TrailingZeroCount(upper);
        var last = upper != 0 ? 127 - BitOperations.LeadingZeroCount(upper) : 63 - BitOperations.LeadingZeroCount(lower);
        // This is a necessary start interval, not a complete class predicate.
        // Holes are harmless false candidates: the original VM verifies the mask.
        low = first;
        high = last;
        return true;
    }

    internal bool MoveNext(ReadOnlySpan<byte> input, bool existenceOnly = false)
    {
        // A scalar cursor exclusively owns this admitted scratch. Published
        // snapshots copy values; ResetAttempt never touches retained results.
        var keepWorkspace = false;
        var leadingClass = _delimiterLeadingClass;
        var delimiter = _delimiter;
        var windowEnd = -1;
        var verifyFirstCandidate = delimiter >= 0 && _leadingAsciiLow >= 0 &&
            (delimiter < _leadingAsciiLow || delimiter > _leadingAsciiHigh);
        var followingWindow = verifyFirstCandidate &&
            _program.Instructions[0] is { Kind: Utf8SliceInstructionKind.Boundary, A: 1 }
            ? Utf8SliceRunner.GetShortRepeatLiteralWindow(_program, _program.LeadingClassDelimiter.LiteralIndex + 2)
            : default;
        var proveWindow = followingWindow.Min != 0 && existenceOnly && _program.GroupCount == 0 &&
            !_program.IsNonBacktracking && !_program.ReadsCaptures;
        try
        {
            while (!_finished)
            {
                Checkpoint();
                // The mandatory initial anchor already bounds candidate starts.
                // Reuse Core's original-origin/LF finder instead of retrying the
                // VM at every scalar, including after an anchored match.
                if (!_program.RightToLeft && !_program.IsNonBacktracking &&
                    _program.Instructions[0] is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning })
                {
                    var breach = Utf8SliceRunner.TrySeekBeginning(_program, input, ref _position, ref _invocation);
                    if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
                    if (_position < 0) { _finished = true; return false; }
                }
                Utf8SliceMatchOutcome outcome;
                // A complete native literal match needs no capture or VM state.
                if (_program.GroupCount == 0 && _candidateRegex is { } candidate &&
                    candidate.TryFindNativeLiteralForResult(input, _position, ref _invocation,
                        out var found, out var start, out var end))
                {
                    if (!found) { _finished = true; return false; }
                    _position = start;
                    _invocation.AnchorOriginByte = start;
                    outcome = new Utf8SliceMatchOutcome(true, end, Utf8ExecutionLimitKind.None);
                }
                else
                {
                    // A necessary range already identifies a possible start.
                    // Verify it once before seeking/rewinding its delimiter;
                    // a failure resumes the original window-guided search.
                    var bypassWindow = false;
                    if (verifyFirstCandidate)
                    {
                        verifyFirstCandidate = false;
                        if (((uint)_position >= (uint)input.Length ||
                            (uint)(input[_position] - _leadingAsciiLow) > (uint)(_leadingAsciiHigh - _leadingAsciiLow)) &&
                            !SeekCandidate(input)) { _finished = true; return false; }
                        // A proved complete capture root has no rewind: one
                        // direct attempt scans its first run once, even without
                        // a nearby delimiter. Failure resumes window discovery.
                        // Other roots still need the short delimiter preview.
                        bypassWindow = _program.HasTerminalCaptureRoot ||
                            input.Slice(_position, Math.Min(64, input.Length - _position)).IndexOf((byte)delimiter) >= 0;
                    }
                    if (delimiter >= 0 && _position > windowEnd && !bypassWindow)
                    {
                        var breach = Utf8SliceRunner.TrySeekLeadingClassLiteralWindow(_program, input,
                            leadingClass, (byte)delimiter, search: default, gapMaximum: 0, direct: true,
                            ref _position, ref _invocation, out windowEnd);
                        if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
                        if (_position < 0) { Checkpoint(); _finished = true; return false; }
                        // The leading boundary leaves the repeat at the start
                        // and its range excludes this delimiter. Every start
                        // in the window must reach the next literal from here.
                        if (followingWindow.Min != 0 && _position <= windowEnd &&
                            !Utf8SliceRunner.MayReachShortRepeatLiteral(input, windowEnd + 1, followingWindow, admitsWideScalars: true))
                        {
                            _position = windowEnd + 1;
                            continue;
                        }
                    }
                    // A necessary start hint still requires profile verification.
                    // Avoid its search helper when the current byte meets the hint.
                    if ((_laterAsciiDistance > 0 || _candidateRegex is not null ||
                        _leadingAsciiLow >= 0 && ((uint)_position >= (uint)input.Length ||
                            (uint)(input[_position] - _leadingAsciiLow) > (uint)(_leadingAsciiHigh - _leadingAsciiLow))) &&
                        !SeekCandidate(input)) { _finished = true; return false; }
                    // The range hint may have jumped past this delimiter.
                    // Discover the next window before entering the verifier.
                    if (delimiter >= 0 && _position > windowEnd && !bypassWindow) continue;
                    // Once scratch is admitted, a positive leading boundary
                    // cannot match between two ASCII word scalars.
                    // A complete boundary-only root has no consuming hint,
                    // but admits the same proof without changing its retries.
                    if (_workspace.Initialized &&
                        _program.Instructions[0] is { Kind: Utf8SliceInstructionKind.Boundary, A: 1 } &&
                        (_leadingAsciiLow >= 0 || _program.GroupCount == 0 && !_program.IsNonBacktracking &&
                            _program.Instructions is { Length: 2 } boundaryCode &&
                            boundaryCode[1].Kind == Utf8SliceInstructionKind.Accept) &&
                        _position > 0 && _position < input.Length &&
                        Utf8AsciiBytePredicates.IsWord(input[_position - 1]) &&
                        Utf8AsciiBytePredicates.IsWord(input[_position]))
                    {
                        // Bound each skip by the next loop's interruption poll.
                        // Non-ASCII context and EOF still reach the verifier.
                        var block = input.Slice(_position, Math.Min(2048, input.Length - _position));
                        var stop = Utf8AsciiBytePredicates.WordSearch.IndexOfAnyExcept(block);
                        _position += stop < 0 ? block.Length : stop;
                        continue;
                    }
                    // A complete boundary-only root cannot satisfy a nonempty
                    // retry. Reuse failure's scalar advance/EOF checks; actual
                    // candidates still run the VM with its admitted workspace.
                    else if (_retryNonEmpty && _program.GroupCount == 0 && !_program.IsNonBacktracking &&
                        _program.Instructions is { Length: 2 } code &&
                        code[0].Kind == Utf8SliceInstructionKind.Boundary && code[1].Kind == Utf8SliceInstructionKind.Accept)
                        outcome = new(false, -1, Utf8ExecutionLimitKind.None);
                    // A complete greedy scalar repeat returned empty only
                    // because it could not consume here. Its nonempty retry
                    // cannot succeed. Lazy repeats and tails can choose an
                    // empty endpoint despite a consuming alternative.
                    else if (_retryNonEmpty && _program.GroupCount == 0 && !_program.IsNonBacktracking &&
                        !_program.RightToLeft && _program.Instructions is { Length: 5 } repeatCode &&
                        repeatCode[0] is { Kind: Utf8SliceInstructionKind.RepeatGreedy, A: 3, RepeatMinimum: 0 } repeat &&
                        repeatCode[1].Kind is (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or
                            Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
                            Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.CategorySet) &&
                        repeatCode[2] is { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 0 } &&
                        repeatCode[3].Kind == Utf8SliceInstructionKind.RepeatExit && repeatCode[3].A == repeat.B &&
                        repeatCode[4].Kind == Utf8SliceInstructionKind.Accept)
                        outcome = new(false, -1, Utf8ExecutionLimitKind.None);
                    else
                    {
                        // The candidate checkpoint funds the same bounded proof
                        // as Core's first-match search. The original VM verifies
                        // any candidate the proof leaves undecided.
                        var provedEnd = -1;
                        var possible = !proveWindow || Utf8SliceRunner.MayMatchBranchedAsciiPrefix(
                            _program, input, _position, 0, out provedEnd, proveMatch: true);
                        outcome = !possible || provedEnd >= 0
                            ? new(possible, provedEnd, Utf8ExecutionLimitKind.None)
                            : VerifyCurrentPosition(input, existenceOnly);
                    }
                }
                if (RecordMatch(outcome))
                {
                    keepWorkspace = true;
                    return true;
                }
                if (_position == input.Length)
                {
                    // The VM can fail on its last instruction after the entry
                    // poll. Keep EOF and pure Count publication under the
                    // original authority, just like candidate-search declines.
                    Checkpoint();
                    _finished = true;
                    return false;
                }
                if (Rune.DecodeFromUtf8(input[_position..], out _, out var width) != OperationStatus.Done)
                    throw Utf8Validation.CreateInvalidUtf8Exception(_position);
                _position += width;
            }
            return false;
        }

        finally { if (!keepWorkspace) _workspace.Release(ref _invocation); }
    }

    private Utf8SliceMatchOutcome VerifyCurrentPosition(ReadOnlySpan<byte> input, bool existenceOnly,
        bool requireFullInput = false)
    {
        _invocation.AnchorOriginByte = _position;
        return Utf8SliceRunner.TryMatchAt(_program, input, _position, _slots.Starts, _slots.Ends,
            false, _retryNonEmpty, true, ref _workspace, ref _invocation, existenceOnly, requireFullInput: requireFullInput);
    }

    private bool RecordMatch(Utf8SliceMatchOutcome outcome)
    {
        if (outcome.Breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
        _retryNonEmpty = outcome.IsMatch && outcome.EndIndex == _position;
        if (!outcome.IsMatch) return false;
        StartOffsetInBytes = _position;
        EndOffsetInBytes = outcome.EndIndex;
        _position = outcome.EndIndex;
        return true;
    }

    private bool SeekCandidate(ReadOnlySpan<byte> input)
    {
        // Native rendering expands lexical folding before planning. Prefixes
        // prove starts only; the profile VM still verifies captures/assertions.
        Utf8ExecutionLimitKind breach;
        if (_laterAsciiDistance > 0)
        {
            var start = _position;
            while (start < input.Length)
            {
                // Intersect the two necessary ranges in order. Seeking heads
                // first also skips dense later-class bytes with no viable head.
                breach = Utf8SliceRunner.TrySeekAsciiCandidate(input, ref start,
                    (byte)_leadingAsciiLow, (byte)_leadingAsciiHigh, ref _invocation);
                if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
                if (start < 0 || _laterAsciiDistance >= input.Length - start) break;
                var later = start + _laterAsciiDistance;
                breach = Utf8SliceRunner.TrySeekAsciiCandidate(input, ref later,
                    _laterAsciiLow, _laterAsciiHigh, ref _invocation);
                if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
                if (later < 0) break;
                start = later - _laterAsciiDistance;
                // An ASCII start proves scalar alignment even when the
                // intervening false prefix contains multibyte text.
                if ((uint)(input[start] - _leadingAsciiLow) <= (uint)(_leadingAsciiHigh - _leadingAsciiLow))
                {
                    _position = start;
                    return true;
                }
                start++;
            }
            Checkpoint();
            return false;
        }
        if (_candidateRegex is not { } candidateRegex)
        {
            if (_leadingAsciiLow < 0) return true;
            while (_position < input.Length)
            {
                if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None) throw ResourceFailure();
                var length = Math.Min(2048, input.Length - _position);
                var block = input.Slice(_position, length);
                // Scan only bytes considered by this candidate search.
                // A Unicode candidate remains a scalar start: all bytes before
                // the first non-ASCII byte were ASCII, even at a block edge.
                var nonAscii = _leadingAsciiMayMatchUnicode
                    ? block.IndexOfAnyInRange((byte)128, byte.MaxValue) : -1;
                var ascii = nonAscii < 0 ? block : block[..nonAscii];
                var offset = _leadingAsciiLow == _leadingAsciiHigh
                    ? ascii.IndexOf((byte)_leadingAsciiLow)
                    : ascii.IndexOfAnyInRange((byte)_leadingAsciiLow, (byte)_leadingAsciiHigh);
                if (offset >= 0) { _position += offset; return true; }
                if (nonAscii >= 0) { _position += nonAscii; return true; }
                _position += length;
            }
            Checkpoint();
            return false;
        }
        ref readonly var plan = ref candidateRegex.Inspection.PreparedRegex.SearchPlan;
        if (plan.LiteralSearch is { } literal)
        {
            if (_position < input.Length && input[_position] == literal.Needle[0]) return true;
            breach = Utf8SearchExecutor.TryFindLiteralCandidate(literal, input, _position,
                input.Length - literal.Length, false, ref _invocation, out _position);
        }
        else if (plan.AlternateLiteralsUtf8 is not null)
        {
            breach = Utf8SearchExecutor.TryFindExactLiteralFamilyCandidate(plan,
                input, _position, input.Length, ref _invocation, out _position);
        }
        else return true;
        if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
        if (_position >= 0) return true;
        Checkpoint();
        return false;
    }

    internal PythonReGroupData SnapshotValueData()
    {
        Checkpoint();
        var start = StartOffsetInBytes;
        var end = EndOffsetInBytes;
        var value = new PythonReGroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = start,
            EndOffsetInBytes = end
        };
        Checkpoint();
        return value;
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

    internal int? GetLastIndex()
    {
        var groupCount = _program.GroupCount;
        if (groupCount == 0) return null;
        // One participating group or a complete root with fixed closing
        // order identifies the last group from the completed slot.
        if (groupCount == 1 || _program.HasTerminalCaptureRoot)
        {
            if (!_workspace.Initialized || !TryGetCaptureByteRange(groupCount, out _, out _)) return null;
            Checkpoint();
            return groupCount;
        }
        // The successful workspace keeps its capture journal until the next
        // advance. Only capture range snapshots observe completion order;
        // value/UTF8 discovery needs no extra scan or metadata state.
        var breach = _workspace.Stacks.FindLastClosedSlot(ref _invocation, out var slot);
        if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
        if (slot < 0) return null;
        Checkpoint();
        return slot + 1;
    }

    internal Utf8PythonRangeMatch SnapshotRanges(ReadOnlyMemory<byte> input)
    {
        var lastIndex = GetLastIndex();
        var groups = AllocateResultArray<Utf8PythonCaptureRange>(_program.GroupCount + 1);
        for (var number = 0; number < groups.Length; number++)
        {
            if ((number & 255) == 0) Checkpoint();
            groups[number] = TryGetCaptureByteRange(number, out var start, out var end)
                ? new Utf8PythonCaptureRange { Number = number, Success = true, StartOffsetInBytes = start, EndOffsetInBytes = end }
                : new Utf8PythonCaptureRange { Number = number };
        }
        Checkpoint();
        return new Utf8PythonRangeMatch(input, groups, lastIndex);
    }

    internal byte[] GetBytes(ReadOnlySpan<byte> input, int start, int end)
    {
        Checkpoint();
        var bytes = AllocateResultArray<byte>(end - start);
        CopyBytes(input[start..end], bytes);
        return bytes;
    }

    internal Utf8SliceOutputWriter CreateOutputWriter()
    {
        if (!Utf8SliceOutputWriter.Rent(256, ref _invocation, out var writer))
            throw new InvalidOperationException("Python output exceeded the working storage budget.");
        return writer;
    }

    internal void AppendBytes(scoped ref Utf8SliceOutputWriter writer, scoped ReadOnlySpan<byte> bytes)
    {
        Checkpoint();
        if (!writer.Append(bytes, ref _invocation))
            throw new InvalidOperationException("Python output exceeded the working storage budget.");
    }

    internal byte[] CompleteOutputBytes(scoped ref Utf8SliceOutputWriter writer)
    {
        Checkpoint();
        // Strict templates and scalar-aligned captures already form valid UTF-8.
        var result = AllocateResultArray<byte>(writer.Length);
        CopyBytes(writer.WrittenBytes, result);
        writer.Release(ref _invocation);
        return result;
    }

    internal void ReleaseOutput(scoped ref Utf8SliceOutputWriter writer)
        => writer.Release(ref _invocation);

    internal void CopyBytes(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Utf8SliceBufferWork.Copy(source, destination, ref _invocation);
        Checkpoint();
    }

    internal void Dispose()
    {
        _workspace.Release(ref _invocation);
        _results.Release(ref _invocation);
        Utf8SliceRunner.ReleaseSlots(_slots, ref _invocation);
        _slots = new Utf8SliceSlots([], []);
        _finished = true;
    }
}
