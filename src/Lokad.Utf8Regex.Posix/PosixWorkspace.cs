using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
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
// Own one mutable local or cursor field; pass by reference, never copy live loans.
// Projection borrows readonly state; Release also accepts the default empty value.
internal struct PosixWorkspace
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
                    else if (count >= maximum || count > 0 && _state[slot + 1] == at)
                    {
                        // The saved optional exit has the same endpoint and
                        // earlier occurrences. A redundant empty body loses to
                        // its shorter history before descendants can break ties.
                        // Keep first/mandatory empties; saturated min0 counts
                        // cannot establish a prior occurrence and remain1.
                        if (count > 1 && count > minimum && _state[slot + 1] == at) failed = true;
                        else pc = instruction.A;
                    }
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
                    {
                        Check(_trace.TryPush(new(instruction.A, at, _state[_openOffset + instruction.A]), ref invocation), ref invocation);
                        // A proved ceiling fixes the highest-priority whole endpoint. Tag1 is
                        // the first child of root0: a completed losing occurrence
                        // cannot be repaired by appending more events. Never use
                        // this argument for a descendant with an unresolved parent.
                        if (instruction.A == 1 && end == maximumEnd)
                            failed = WorseClosedPrefix(ref invocation);
                    }
                    pc++;
                    break;
                case Utf8SliceInstructionKind.Accept:
                    if (end < 0 && at < input.Length && !existenceOnly && !_program.FirstAcceptanceIsLongest)
                        maximumEnd = FindAlphabetCeiling(input, at, ref invocation);
                    // Without requested captures, the ceiling proves the longest whole
                    // match regardless of the histories on other pending paths.
                    if (existenceOnly || !captureHistory && at == maximumEnd) { end = at; return true; }
                    if (_program.FirstAcceptanceIsLongest)
                    {
                        end = at;
                        // No competing acceptance remains. Transfer the admitted
                        // winning trace instead of allocating/copying a second
                        // history; both owners still reset and release normally.
                        if (captureHistory) (_best, _trace) = (_trace, _best);
                        return true;
                    }
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
                    failed = true;
                    break;
                default: throw new InvalidOperationException($"Unsupported POSIX instruction {instruction.Kind}.");
            }
            if (failed && !Backtrack(ref pc, ref at, ref invocation)) return end >= 0;
        }
    }

    private int FindAlphabetCeiling(ReadOnlySpan<byte> input, int acceptedEnd, ref Utf8SliceInvocation invocation)
    {
        var program = _program.Scalar;
        var code = program.Instructions;
        if (code.Length > 64) return input.Length;
        Charge(ref invocation);
        var first = int.MaxValue; var last = -1;
        foreach (var instruction in code)
        {
            if (instruction is { Kind: Utf8SliceInstructionKind.Literal, B: 0 })
            {
                first = Math.Min(first, instruction.A); last = Math.Max(last, instruction.A);
            }
            else if (instruction is { Kind: Utf8SliceInstructionKind.CategorySet, B: 0, C: 0 } &&
                program.CategorySetMembers[instruction.D].Length == 0)
            {
                var mask = program.SetMasks[instruction.A];
                if (mask.Length != 16) return input.Length;
                var low = BinaryPrimitives.ReadUInt64LittleEndian(mask);
                var high = BinaryPrimitives.ReadUInt64LittleEndian(mask.AsSpan(8));
                if ((low | high) == 0) continue;
                first = Math.Min(first, low != 0 ? BitOperations.TrailingZeroCount(low) :
                    64 + BitOperations.TrailingZeroCount(high));
                last = Math.Max(last, high != 0 ? 127 - BitOperations.LeadingZeroCount(high) :
                    63 - BitOperations.LeadingZeroCount(low));
            }
            else if (instruction.Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
                Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or Utf8SliceInstructionKind.RepeatGreedy or
                Utf8SliceInstructionKind.RepeatEnd or Utf8SliceInstructionKind.RepeatExit or Utf8SliceInstructionKind.Anchor or
                Utf8SliceInstructionKind.Accept)) return input.Length;
        }
        if (last < first) return input.Length;
        // Every consuming atom stays in this conservative scalar interval. No
        // accepting path can cross its first excluded scalar. Keep the actual
        // subject for anchors, validation and output.
        // A bounded miss retains physical EOF as the original safe upper bound.
        var tail = input.Slice(acceptedEnd, Math.Min(2048, input.Length - acceptedEnd));
        if (last < 128)
        {
            Charge(ref invocation);
            var stop = tail.IndexOfAnyExceptInRange((byte)first, (byte)last);
            return stop < 0 ? input.Length : acceptedEnd + stop;
        }
        for (int offset = 0, scalars = 0; offset < tail.Length; scalars++)
        {
            if ((scalars & 63) == 0) Charge(ref invocation);
            // A probe ending inside a scalar supplies no endpoint proof. Keep
            // the original bound; subject validation belongs to the caller.
            if (Rune.DecodeFromUtf8(tail[offset..], out var scalar, out var width) != OperationStatus.Done)
                return input.Length;
            if (scalar.Value < first || scalar.Value > last) return acceptedEnd + offset;
            offset += width;
        }
        return input.Length;
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
        while (_trace.Count > frame.TraceCount)
        {
            Charge(ref invocation);
            _trace.Pop(Math.Min(64, _trace.Count - frame.TraceCount));
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

    private bool WorseClosedPrefix(ref Utf8SliceInvocation invocation)
    {
        var current = _trace.WrittenItems; var best = _best.WrittenItems;
        var a = 0; var b = 0; var first = true;
        while (true)
        {
            var ac = NextClose(current, 1, a, current.Length, ref invocation);
            if (ac < 0) return false; // A continuation can still add occurrences.
            var bc = NextClose(best, 1, b, best.Length - 1, ref invocation);
            if (bc < 0) return !first; // Participation wins; an extra equal-prefix tail loses.
            var aStart = current[current[ac].Opening].Position;
            var bStart = best[best[bc].Opening].Position;
            if (aStart != bStart) return aStart > bStart;
            if (current[ac].Position != best[bc].Position)
                return current[ac].Position < best[bc].Position;
            a = ac + 1; b = bc + 1; first = false;
        }
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

    internal readonly bool GetCapture(int slot, ref Utf8SliceInvocation invocation, out int start, out int end)
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
        while (buffer.Count != 0)
        {
            Charge(ref invocation);
            buffer.Pop(Math.Min(64, buffer.Count));
        }
    }

    internal void Release(ref Utf8SliceInvocation invocation)
    {
        _frames.Release(ref invocation); _undo.Release(ref invocation);
        _trace.Release(ref invocation); _best.Release(ref invocation);
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
