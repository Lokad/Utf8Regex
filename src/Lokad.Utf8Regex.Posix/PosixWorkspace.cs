using System.Buffers;
using System.Numerics;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix;

// One exclusive call/cursor owns these admitted buffers. Scalar execution finds
// the earliest longest whole extent; proven first-acceptance programs also keep
// their native capture trace. General captures select closed syntax intervals.
// State belongs to the synchronous stack/heap vector or the cursor's array.
// Pass this owner by reference; no borrowed span or result aliases its buffers.
internal partial struct PosixWorkspace
{
    private readonly PosixProgram _program;
    private ulong _stateBytes;
    private readonly int _openOffset;
    private readonly int _lastOffset;
    private Utf8InvocationStateBuffer<Frame> _frames;
    private Utf8InvocationStateBuffer<Mutation> _undo;
    private Utf8InvocationStateBuffer<TagEvent> _trace;
    private Dictionary<ChartKey, int>? _chartMemo;
    private int _chartCapacity;
    private ulong _chartBytes;
    private Utf8InvocationStateBuffer<ChartFrame> _chartFrames;
    private bool _chartResult;

    private readonly record struct Frame(int Pc, int Position, int TraceCount, int UndoCount);
    private readonly record struct Mutation(int Index, int OldValue);
    private readonly record struct TagEvent(int Tag, int Position, int Opening);

    internal readonly int StateLength => _lastOffset + (_lastOffset - _openOffset);

    internal PosixWorkspace(PosixProgram program, ref Utf8SliceInvocation invocation, bool captureHistory = true)
    {
        _program = program;
        _openOffset = program.DisjointAsciiRuns ? 0 : checked(program.Scalar.RepeatCount * 2);
        var tags = captureHistory ? program.FirstAcceptanceIsLongest ? program.Parents.Length : program.CaptureTags.Length - 1 : 0;
        _lastOffset = checked(_openOffset + tags);
        var count = checked(_lastOffset + tags);
        if (count == 0) return;
        var bytes = Utf8ConstructionBudget.ArrayBytes<int>(count);
        Check(invocation.TryReserveWorking(bytes), ref invocation);
        try
        {
            Charge(ref invocation);
            _stateBytes = bytes;
        }
        catch { invocation.Ledger.Release(bytes); throw; }
    }

    internal bool Find(ReadOnlySpan<byte> input, scoped Span<int> state, int fromByte, bool existenceOnly,
        ref Utf8SliceInvocation invocation, out int start, out int end, bool captureHistory = true)
    {
        captureHistory &= !existenceOnly && _program.CaptureTags.Length > 1;
        var generalCaptures = captureHistory && !_program.FirstAcceptanceIsLongest;
        for (var candidate = fromByte; candidate <= input.Length;)
        {
            Charge(ref invocation);
            Reset(state, ref invocation);
            if (MatchAt(input, state, candidate, existenceOnly, captureHistory && !generalCaptures, ref invocation, out end))
            {
                start = candidate;
                if (generalCaptures)
                {
                    // The closed whole winner needs no pending scalar choice or
                    // undo state. Refund those capacities before chart growth.
                    _frames.Release(ref invocation); _undo.Release(ref invocation);
                    SelectChart(input, state, candidate, end, ref invocation);
                }
                else if (captureHistory)
                {
                    var trace = _trace.WrittenItems;
                    for (var index = 0; index < trace.Length; index++)
                    {
                        if ((index & 63) == 0) Charge(ref invocation);
                        if (trace[index].Opening >= 0) state[_lastOffset + trace[index].Tag] = index;
                    }
                }
                Finish(ref invocation);
                return true;
            }
            if (candidate == input.Length) break;
            candidate = Utf8SliceRunner.AdvancePastScalar(input, candidate, false);
        }
        start = end = -1;
        Finish(ref invocation);
        return false;
    }

    private bool MatchAt(ReadOnlySpan<byte> input, scoped Span<int> state, int start, bool existenceOnly, bool captureHistory,
        ref Utf8SliceInvocation invocation, out int end)
    {
        var code = _program.Scalar.Instructions;
        var pc = 0; var at = start; end = -1;
        var maximumEnd = input.Length;
        while (true)
        {
            Charge(ref invocation);
            var instruction = code[pc];
            var failed = false;
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Literal:
                case Utf8SliceInstructionKind.Dot:
                case Utf8SliceInstructionKind.CategorySet:
                    if (Rune.DecodeFromUtf8(input[at..], out var scalar, out var width) != OperationStatus.Done)
                    { failed = true; break; }
                    var hit = instruction.Kind switch
                    {
                        Utf8SliceInstructionKind.Literal => scalar.Value == instruction.A,
                        Utf8SliceInstructionKind.Dot => instruction.A != 0 || scalar.Value != '\n',
                        _ => MatchesClass(scalar, instruction, ref invocation),
                    };
                    if (hit) { at += width; pc++; } else failed = true;
                    break;
                case Utf8SliceInstructionKind.Anchor:
                    hit = (Utf8SliceAnchorKind)instruction.A switch
                    {
                        Utf8SliceAnchorKind.Beginning => at == 0 || instruction.B != 0 && input[at - 1] == '\n',
                        Utf8SliceAnchorKind.VeryEnd => at == input.Length,
                        Utf8SliceAnchorKind.End => at == input.Length || instruction.B != 0 && input[at] == '\n',
                        _ => throw new InvalidOperationException("Unsupported POSIX anchor."),
                    };
                    if (hit) pc++; else failed = true;
                    break;
                case Utf8SliceInstructionKind.Fork:
                    if (instruction.B != 0) throw new InvalidOperationException("Unsupported POSIX fork.");
                    PushFrame(new(instruction.C == 0 ? instruction.A : pc + 1, at, _trace.Count, _undo.Count), ref invocation);
                    pc = instruction.C == 0 ? pc + 1 : instruction.A;
                    break;
                case Utf8SliceInstructionKind.Jump: pc = instruction.A; break;
                case Utf8SliceInstructionKind.RepeatGreedy:
                    if (_program.DisjointAsciiRuns)
                    {
                        // The existing compiler proof admits only positive,
                        // pairwise-disjoint ASCII classes with captures outside
                        // their bodies. A shorter run cannot help its successor
                        // or an anchor, so no alternative exit/counter undo is
                        // useful. Keep ordinary capture events around the run.
                        var mask = _program.Scalar.SetMasks[code[pc + 1].A];
                        var limit = at + (int)Math.Min(input.Length - (long)at, instruction.RepeatMaximum);
                        var runStart = at;
                        while (at < limit)
                        {
                            Charge(ref invocation);
                            var value = input[at];
                            if (value >= 128 || (mask[value >> 3] & (1 << (value & 7))) == 0) break;
                            at++;
                        }
                        if (at - runStart < instruction.RepeatMinimum) { failed = true; break; }
                        pc = instruction.A + 1; // Skip the unused counter reset.
                        break;
                    }
                    var slot = instruction.B * 2;
                    var count = state[slot];
                    if (count < 0) { count = 0; SetState(state, slot, 0, ref invocation); }
                    var minimum = instruction.RepeatMinimum;
                    var maximum = instruction.RepeatMaximum;
                    if (count < minimum)
                    {
                        SetState(state, slot, count + 1, ref invocation);
                        SetState(state, slot + 1, at, ref invocation);
                        pc++;
                    }
                    else if (count >= maximum || count > 0 && state[slot + 1] == at)
                    {
                        // The saved optional exit has the same endpoint and
                        // earlier occurrences. A redundant empty body loses to
                        // its shorter history before descendants can break ties.
                        // Keep first/mandatory empties; saturated min0 counts
                        // cannot establish a prior occurrence and remain1.
                        if (count > 1 && count > minimum && state[slot + 1] == at) failed = true;
                        else pc = instruction.A;
                    }
                    else
                    {
                        PushFrame(new(instruction.A, at, _trace.Count, _undo.Count), ref invocation);
                        SetState(state, slot, maximum == long.MaxValue ? (int)minimum + 1 : count + 1, ref invocation);
                        SetState(state, slot + 1, at, ref invocation);
                        pc++;
                    }
                    break;
                case Utf8SliceInstructionKind.RepeatEnd: pc = instruction.A; break;
                case Utf8SliceInstructionKind.RepeatExit:
                    SetState(state, instruction.A * 2, -1, ref invocation);
                    SetState(state, instruction.A * 2 + 1, -1, ref invocation);
                    pc++;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                    if (captureHistory)
                    {
                        SetState(state, _openOffset + instruction.A, _trace.Count, ref invocation);
                        Check(_trace.TryPush(new(instruction.A, at, -1), ref invocation), ref invocation);
                    }
                    pc++;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    if (captureHistory)
                    {
                        Check(_trace.TryPush(new(instruction.A, at, state[_openOffset + instruction.A]), ref invocation), ref invocation);
                    }
                    pc++;
                    break;
                case Utf8SliceInstructionKind.Accept:
                    if (!existenceOnly && !_program.FirstAcceptanceIsLongest &&
                        (end < 0 || at > end && maximumEnd == input.Length))
                    {
                        if (at < input.Length) maximumEnd = FindBoundaryCeiling(input, start, at, ref invocation);
                    }
                    // Without requested captures, the ceiling proves the longest whole
                    // match regardless of the histories on other pending paths.
                    if (existenceOnly || !captureHistory && at == maximumEnd || _program.FirstAcceptanceIsLongest)
                    { end = at; return true; }
                    end = Math.Max(end, at);
                    failed = true;
                    break;
                default: throw new InvalidOperationException($"Unsupported POSIX instruction {instruction.Kind}.");
            }
            if (failed)
            {
                if (!Backtrack(state, ref pc, ref at, ref invocation)) return end >= 0;
            }
        }
    }

    private int FindBoundaryCeiling(ReadOnlySpan<byte> input, int start, int acceptedEnd, ref Utf8SliceInvocation invocation)
    {
        var code = _program.Scalar.Instructions;
        // Global exclusion needs no bitset. Keep the 64-instruction cap only in
        // the reachable-state proof; this walk retains its bounded work polls.
        Charge(ref invocation);
        if (Rune.DecodeFromUtf8(input[acceptedEnd..], out var boundary, out _) != OperationStatus.Done)
            return input.Length;
        // Every path beyond this acceptance must cross its next scalar. Use
        // the normal predicates, including Unicode classes and negation, rather
        // than constructing a coarse alphabet and searching the remaining input.
        for (var pc = 0; pc < code.Length; pc++)
        {
            if ((pc & 31) == 0) Charge(ref invocation);
            var instruction = code[pc];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Literal:
                    if (instruction.A == boundary.Value)
                        return FindReachableBoundaryCeiling(input, start, acceptedEnd, boundary, 0, code.Length - 1, ref invocation);
                    break;
                case Utf8SliceInstructionKind.Dot:
                    if (instruction.A != 0 || boundary.Value != '\n')
                        return FindReachableBoundaryCeiling(input, start, acceptedEnd, boundary, 0, code.Length - 1, ref invocation);
                    break;
                case Utf8SliceInstructionKind.CategorySet:
                    if (MatchesClass(boundary, instruction, ref invocation))
                        return FindReachableBoundaryCeiling(input, start, acceptedEnd, boundary, 0, code.Length - 1, ref invocation);
                    break;
                case Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
                    Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or Utf8SliceInstructionKind.RepeatGreedy or
                    Utf8SliceInstructionKind.RepeatEnd or Utf8SliceInstructionKind.RepeatExit or Utf8SliceInstructionKind.Anchor or
                    Utf8SliceInstructionKind.Accept:
                    break;
                default: return input.Length;
            }
        }
        return acceptedEnd;
    }

    private int FindReachableBoundaryCeiling(ReadOnlySpan<byte> input, int start, int acceptedEnd,
        Rune boundary, int firstPc, int terminalPc, ref Utf8SliceInvocation invocation)
    {
        var code = _program.Scalar.Instructions;
        if (code.Length is < 1 or > 64 || (uint)terminalPc >= (uint)code.Length ||
            (uint)firstPc > (uint)terminalPc ||
            code[terminalPc].Kind is not (Utf8SliceInstructionKind.Accept or Utf8SliceInstructionKind.CaptureEnd) ||
            (uint)(acceptedEnd - start) > 2048) return input.Length;
        // Capture events do not affect ERE acceptance. Let every repeat take
        // either edge, relaxing its counters/progress guards. The reachable
        // instruction set therefore contains every concrete path at this byte.
        // Stop at the proved scope's terminal, never its following suffix. No
        // reachable consumer across the boundary proves that scope's ceiling;
        // this analysis never selects captures or publishes a match.
        ulong frontier = 1UL << firstPc;
        var at = start;
        while (true)
        {
            var scalar = boundary;
            var width = 0;
            if (at != acceptedEnd &&
                (Rune.DecodeFromUtf8(input[at..], out scalar, out width) != OperationStatus.Done ||
                 width > acceptedEnd - at)) return input.Length;
            ulong pending = frontier, visited = 0, next = 0;
            while (pending != 0)
            {
                var pc = BitOperations.TrailingZeroCount(pending);
                var bit = 1UL << pc;
                pending &= ~bit;
                if ((visited & bit) != 0) continue;
                visited |= bit;
                Charge(ref invocation);
                if (pc == terminalPc) continue;
                var instruction = code[pc];
                var following = bit << 1; // A non-terminal remains inside the scope.
                switch (instruction.Kind)
                {
                    case Utf8SliceInstructionKind.Literal:
                    case Utf8SliceInstructionKind.Dot:
                    case Utf8SliceInstructionKind.CategorySet:
                        var hit = instruction.Kind switch
                        {
                            Utf8SliceInstructionKind.Literal => scalar.Value == instruction.A,
                            Utf8SliceInstructionKind.Dot => instruction.A != 0 || scalar.Value != '\n',
                            _ => MatchesClass(scalar, instruction, ref invocation),
                        };
                        if (hit)
                        {
                            if (at == acceptedEnd) return input.Length;
                            next |= following;
                        }
                        break;
                    case Utf8SliceInstructionKind.CaptureStart:
                    case Utf8SliceInstructionKind.CaptureEnd:
                    case Utf8SliceInstructionKind.RepeatExit:
                        pending |= following;
                        break;
                    case Utf8SliceInstructionKind.Jump:
                    case Utf8SliceInstructionKind.RepeatEnd:
                        if (instruction.A < firstPc || instruction.A > terminalPc) return input.Length;
                        pending |= 1UL << instruction.A;
                        break;
                    case Utf8SliceInstructionKind.Fork:
                    case Utf8SliceInstructionKind.RepeatGreedy:
                        if (instruction.A < firstPc || instruction.A > terminalPc ||
                            instruction.Kind == Utf8SliceInstructionKind.Fork && instruction.B != 0) return input.Length;
                        pending |= following | (1UL << instruction.A);
                        break;
                    case Utf8SliceInstructionKind.Anchor:
                        var allows = (Utf8SliceAnchorKind)instruction.A switch
                        {
                            Utf8SliceAnchorKind.Beginning => at == 0 || instruction.B != 0 && input[at - 1] == '\n',
                            Utf8SliceAnchorKind.VeryEnd => at == input.Length,
                            Utf8SliceAnchorKind.End => at == input.Length || instruction.B != 0 && input[at] == '\n',
                            _ => false,
                        };
                        if (instruction.A is < 0 or > (int)Utf8SliceAnchorKind.VeryEnd) return input.Length;
                        if (allows) pending |= following;
                        break;
                    default: return input.Length;
                }
            }
            if (at == acceptedEnd) return acceptedEnd;
            if (next == 0) return input.Length;
            frontier = next;
            at += width;
        }
    }

    private bool MatchesClass(Rune scalar, Utf8SliceInstruction instruction, ref Utf8SliceInvocation invocation)
    {
        var program = _program.Scalar;
        var hit = Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[instruction.A], false, program.CaseBehavior) ||
            Utf8SliceScalarClasses.MatchesMembers(scalar, instruction.C);
        if (!hit)
        {
            hit = Utf8SliceScalarClasses.MatchesTables(scalar, program.CategorySetMembers[instruction.D],
                false, program.CaseBehavior, ref invocation, out var breach);
            Check(breach, ref invocation);
        }
        return hit != ((instruction.B & 1) != 0);
    }

    private void PushFrame(Frame frame, ref Utf8SliceInvocation invocation)
    {
        Check(invocation.Budget.TryNoteDepth(checked((uint)_frames.Count + 1)), ref invocation);
        Check(_frames.TryPush(frame, ref invocation), ref invocation);
    }

    private void SetState(scoped Span<int> state, int index, int value, ref Utf8SliceInvocation invocation)
    {
        if (state[index] == value) return;
        if (_frames.Count != 0) Check(_undo.TryPush(new(index, state[index]), ref invocation), ref invocation);
        state[index] = value;
    }

    private bool Backtrack(scoped Span<int> state, ref int pc, ref int at, ref Utf8SliceInvocation invocation)
    {
        if (_frames.Count == 0) return false;
        var frame = _frames[_frames.Count - 1]; _frames.Pop();
        for (var iteration = 0; _undo.Count > frame.UndoCount; iteration++)
        {
            if ((iteration & 63) == 0) Charge(ref invocation);
            var mutation = _undo[_undo.Count - 1]; _undo.Pop(); state[mutation.Index] = mutation.OldValue;
        }
        while (_trace.Count > frame.TraceCount)
        {
            Charge(ref invocation);
            _trace.Pop(Math.Min(64, _trace.Count - frame.TraceCount));
        }
        pc = frame.Pc; at = frame.Position;
        return true;
    }

    internal readonly bool GetCapture(scoped ReadOnlySpan<int> state, int slot, ref Utf8SliceInvocation invocation, out int start, out int end)
    {
        if (!_program.FirstAcceptanceIsLongest)
        {
            start = state[_openOffset + slot - 1]; end = state[_lastOffset + slot - 1];
            return start >= 0;
        }
        var tag = _program.CaptureTags[slot];
        var close = state[_lastOffset + tag];
        start = end = -1;
        if (close < 0) return false;
        var open = _trace[close].Opening;
        // Tag zero encloses the entire winning trace, including when it is a
        // public root capture. Only repeated inner parents can make a group's
        // last occurrence absent from its parent's final occurrence.
        for (var parent = _program.Parents[tag]; parent > 0; parent = _program.Parents[parent])
        {
            Charge(ref invocation);
            var parentClose = state[_lastOffset + parent];
            if (parentClose < 0 || _trace[parentClose].Opening > open || parentClose < close) return false;
        }
        start = _trace[open].Position; end = _trace[close].Position;
        return true;
    }

    private void Reset(scoped Span<int> state, ref Utf8SliceInvocation invocation)
    {
        Clear(ref _frames, ref invocation); Clear(ref _undo, ref invocation);
        Clear(ref _trace, ref invocation);
        ResetChart(ref invocation);
        for (var offset = 0; offset < state.Length;)
        {
            Charge(ref invocation);
            var count = Math.Min(2048, state.Length - offset);
            state.Slice(offset, count).Fill(-1); offset += count;
        }
    }

    private static void Clear<T>(ref Utf8InvocationStateBuffer<T> buffer, ref Utf8SliceInvocation invocation)
    {
        while (buffer.Count != 0)
        {
            Charge(ref invocation);
            buffer.Pop(Math.Min(64, buffer.Count));
        }
    }

    internal void Release(ref Utf8SliceInvocation invocation)
    {
        _frames.Release(ref invocation); _undo.Release(ref invocation);
        _trace.Release(ref invocation);
        ReleaseChart(ref invocation);
        invocation.Ledger.Release(_stateBytes);
        this = default;
    }

    internal static void Charge(ref Utf8SliceInvocation invocation)
        => Check(invocation.TryChargeStep(), ref invocation);

    internal static void Finish(ref Utf8SliceInvocation invocation)
    { invocation.Budget.ThrowIfCancellationRequested(); invocation.Budget.CheckDeadline(); }

    private static void Check(Utf8ExecutionLimitKind breach, ref Utf8SliceInvocation invocation)
    { if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure(); }
}
