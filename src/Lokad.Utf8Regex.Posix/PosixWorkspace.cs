using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix;

// One exclusive synchronous call/cursor owns this workspace. Enumerate accepting
// paths at the earliest start; keep the longest, then compare hierarchical tag
// histories. Empty-repeat progress guards bound epsilon traversal. All growing
// storage uses the existing admitted invocation buffers, without a second ledger.
// Whole-only output needs no tag history: captures cannot affect ERE acceptance.
// A cursor reserves tag capacity because later advances can request captures.
internal sealed class PosixWorkspace
{
    private readonly PosixProgram _program;
    private int[] _state = [];
    private ulong _stateBytes;
    private readonly int _openOffset;
    private readonly int _lastOffset;
    private Utf8InvocationStateBuffer<Frame> _frames;
    private Utf8InvocationStateBuffer<Mutation> _undo;
    private Utf8InvocationStateBuffer<TagEvent> _trace;
    private Utf8InvocationStateBuffer<TagEvent> _best;

    private readonly record struct Frame(int Pc, int Position, int TraceCount, int UndoCount);
    private readonly record struct Mutation(int Index, int OldValue);
    private readonly record struct TagEvent(int Tag, int Position, int Opening);

    internal PosixWorkspace(PosixProgram program, ref Utf8SliceInvocation invocation, bool captureHistory = true)
    {
        _program = program;
        _openOffset = checked(program.Scalar.RepeatCount * 2);
        var tags = captureHistory ? program.Parents.Length : 0;
        _lastOffset = checked(_openOffset + tags);
        var count = checked(_lastOffset + tags);
        if (count == 0) return;
        var bytes = Utf8ConstructionBudget.ArrayBytes<int>(count);
        Check(invocation.TryReserveWorking(bytes), ref invocation);
        try
        {
            Charge(ref invocation);
            _state = new int[count];
            _stateBytes = bytes;
        }
        catch { invocation.Ledger.Release(bytes); throw; }
    }

    internal bool Find(ReadOnlySpan<byte> input, int fromByte, bool existenceOnly,
        ref Utf8SliceInvocation invocation, out int start, out int end, bool captureHistory = true)
    {
        captureHistory &= !existenceOnly;
        for (var candidate = fromByte; candidate <= input.Length;)
        {
            Charge(ref invocation);
            Reset(ref invocation);
            if (MatchAt(input, candidate, existenceOnly, captureHistory, ref invocation, out end))
            {
                start = candidate;
                if (captureHistory)
                {
                    var trace = _best.WrittenItems;
                    for (var index = 0; index < trace.Length; index++)
                    {
                        if ((index & 63) == 0) Charge(ref invocation);
                        if (trace[index].Opening >= 0) _state[_lastOffset + trace[index].Tag] = index;
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

    private bool MatchAt(ReadOnlySpan<byte> input, int start, bool existenceOnly, bool captureHistory,
        ref Utf8SliceInvocation invocation, out int end)
    {
        var code = _program.Scalar.Instructions;
        var pc = 0; var at = start; end = -1;
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
                    PushFrame(new(instruction.A, at, _trace.Count, _undo.Count), ref invocation);
                    pc++;
                    break;
                case Utf8SliceInstructionKind.Jump: pc = instruction.A; break;
                case Utf8SliceInstructionKind.RepeatGreedy:
                    var slot = instruction.B * 2;
                    var count = _state[slot];
                    if (count < 0) { count = 0; SetState(slot, 0, ref invocation); }
                    var minimum = instruction.RepeatMinimum;
                    var maximum = instruction.RepeatMaximum;
                    if (count < minimum)
                    {
                        SetState(slot, count + 1, ref invocation);
                        SetState(slot + 1, at, ref invocation);
                        pc++;
                    }
                    else if (count >= maximum || count > 0 && _state[slot + 1] == at) pc = instruction.A;
                    else
                    {
                        PushFrame(new(instruction.A, at, _trace.Count, _undo.Count), ref invocation);
                        SetState(slot, maximum == long.MaxValue ? (int)minimum + 1 : count + 1, ref invocation);
                        SetState(slot + 1, at, ref invocation);
                        pc++;
                    }
                    break;
                case Utf8SliceInstructionKind.RepeatEnd: pc = instruction.A; break;
                case Utf8SliceInstructionKind.RepeatExit:
                    SetState(instruction.A * 2, -1, ref invocation);
                    SetState(instruction.A * 2 + 1, -1, ref invocation);
                    pc++;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                    if (captureHistory)
                    {
                        SetState(_openOffset + instruction.A, _trace.Count, ref invocation);
                        Check(_trace.TryPush(new(instruction.A, at, -1), ref invocation), ref invocation);
                    }
                    pc++;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    if (captureHistory)
                        Check(_trace.TryPush(new(instruction.A, at, _state[_openOffset + instruction.A]), ref invocation), ref invocation);
                    pc++;
                    break;
                case Utf8SliceInstructionKind.Accept:
                    if (existenceOnly) { end = at; return true; }
                    if (at > end || at == end && captureHistory && BetterHistory(ref invocation))
                    {
                        end = at;
                        if (captureHistory)
                        {
                            Clear(ref _best, ref invocation);
                            for (var index = 0; index < _trace.Count; index++)
                            {
                                if ((index & 63) == 0) Charge(ref invocation);
                                Check(_best.TryPush(_trace[index], ref invocation), ref invocation);
                            }
                        }
                    }
                    if (_program.FirstAcceptanceIsLongest) return true;
                    failed = true;
                    break;
                default: throw new InvalidOperationException($"Unsupported POSIX instruction {instruction.Kind}.");
            }
            if (failed && !Backtrack(ref pc, ref at, ref invocation)) return end >= 0;
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

    private void SetState(int index, int value, ref Utf8SliceInvocation invocation)
    {
        if (_state[index] == value) return;
        if (_frames.Count != 0) Check(_undo.TryPush(new(index, _state[index]), ref invocation), ref invocation);
        _state[index] = value;
    }

    private bool Backtrack(ref int pc, ref int at, ref Utf8SliceInvocation invocation)
    {
        if (_frames.Count == 0) return false;
        var frame = _frames[_frames.Count - 1]; _frames.Pop();
        for (var iteration = 0; _undo.Count > frame.UndoCount; iteration++)
        {
            if ((iteration & 63) == 0) Charge(ref invocation);
            var mutation = _undo[_undo.Count - 1]; _undo.Pop(); _state[mutation.Index] = mutation.OldValue;
        }
        for (var iteration = 0; _trace.Count > frame.TraceCount; iteration++)
        {
            if ((iteration & 63) == 0) Charge(ref invocation);
            _trace.Pop();
        }
        pc = frame.Pc; at = frame.Position;
        return true;
    }

    private bool BetterHistory(ref Utf8SliceInvocation invocation)
    {
        var current = _trace.WrittenItems; var best = _best.WrittenItems;
        for (var tag = 1; tag < _program.Parents.Length; tag++)
        {
            Charge(ref invocation);
            var parent = _program.Parents[tag];
            if (parent == 0)
            {
                // Compilation wraps the whole expression in tag0, outside all
                // alternatives/repeats. Every acceptance has its single opening
                // at0 and closing last; do not search both traces for it again.
                var order = CompareOccurrences(current, best, tag, 0, current.Length - 1,
                    0, best.Length - 1, ref invocation);
                if (order != 0) return order < 0;
                continue;
            }
            var a = 0; var b = 0;
            while (true)
            {
                var ae = NextClose(current, parent, a, current.Length, ref invocation);
                var be = NextClose(best, parent, b, best.Length, ref invocation);
                if (ae < 0 || be < 0)
                {
                    // Any differing higher-priority parent was already decisive.
                    if (ae != be) throw new InvalidOperationException("POSIX parent histories disagree after their priority passed.");
                    break;
                }
                var order = CompareOccurrences(current, best, tag, current[ae].Opening, ae, best[be].Opening, be, ref invocation);
                if (order != 0) return order < 0;
                a = ae + 1; b = be + 1;
            }
        }
        return false;
    }

    private static int CompareOccurrences(ReadOnlySpan<TagEvent> current, ReadOnlySpan<TagEvent> best,
        int tag, int a, int aEnd, int b, int bEnd, ref Utf8SliceInvocation invocation)
    {
        var first = true;
        while (true)
        {
            var ac = NextClose(current, tag, a + 1, aEnd, ref invocation);
            var bc = NextClose(best, tag, b + 1, bEnd, ref invocation);
            if (ac < 0 || bc < 0)
            {
                if (ac == bc) return 0;
                // A participating (even empty) subpattern beats absence. Once
                // equal occurrences exist, prefer no redundant empty tail.
                return first ? ac < 0 ? 1 : -1 : ac < 0 ? -1 : 1;
            }
            var aStart = current[current[ac].Opening].Position;
            var bStart = best[best[bc].Opening].Position;
            if (aStart != bStart) return aStart < bStart ? -1 : 1;
            if (current[ac].Position != best[bc].Position) return current[ac].Position > best[bc].Position ? -1 : 1;
            a = ac; b = bc; first = false;
        }
    }

    private static int NextClose(ReadOnlySpan<TagEvent> trace, int tag, int from, int end, ref Utf8SliceInvocation invocation)
    {
        for (var index = from; index < end; index++)
        {
            if (((index - from) & 63) == 0) Charge(ref invocation);
            if (trace[index].Tag == tag && trace[index].Opening >= 0) return index;
        }
        return -1;
    }

    internal bool GetCapture(int slot, ref Utf8SliceInvocation invocation, out int start, out int end)
    {
        var tag = _program.CaptureTags[slot];
        var close = _state[_lastOffset + tag];
        start = end = -1;
        if (close < 0) return false;
        var open = _best[close].Opening;
        // Tag zero encloses the entire winning trace, including when it is a
        // public root capture. Only repeated inner parents can make a group's
        // last occurrence absent from its parent's final occurrence.
        for (var parent = _program.Parents[tag]; parent > 0; parent = _program.Parents[parent])
        {
            Charge(ref invocation);
            var parentClose = _state[_lastOffset + parent];
            if (parentClose < 0 || _best[parentClose].Opening > open || parentClose < close) return false;
        }
        start = _best[open].Position; end = _best[close].Position;
        return true;
    }

    private void Reset(ref Utf8SliceInvocation invocation)
    {
        Clear(ref _frames, ref invocation); Clear(ref _undo, ref invocation);
        Clear(ref _trace, ref invocation); Clear(ref _best, ref invocation);
        for (var offset = 0; offset < _state.Length;)
        {
            Charge(ref invocation);
            var count = Math.Min(2048, _state.Length - offset);
            _state.AsSpan(offset, count).Fill(-1); offset += count;
        }
    }

    private static void Clear<T>(ref Utf8InvocationStateBuffer<T> buffer, ref Utf8SliceInvocation invocation)
    {
        for (var index = 0; buffer.Count != 0; index++)
        {
            if ((index & 63) == 0) Charge(ref invocation);
            buffer.Pop();
        }
    }

    internal void Release(ref Utf8SliceInvocation invocation)
    {
        _frames.Release(ref invocation); _undo.Release(ref invocation);
        _trace.Release(ref invocation); _best.Release(ref invocation);
        invocation.Ledger.Release(_stateBytes); _stateBytes = 0; _state = [];
    }

    internal static void Charge(ref Utf8SliceInvocation invocation)
        => Check(invocation.TryChargeStep(), ref invocation);

    internal static void Finish(ref Utf8SliceInvocation invocation)
    { invocation.Budget.ThrowIfCancellationRequested(); invocation.Budget.CheckDeadline(); }

    private static void Check(Utf8ExecutionLimitKind breach, ref Utf8SliceInvocation invocation)
    { if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure(); }
}
