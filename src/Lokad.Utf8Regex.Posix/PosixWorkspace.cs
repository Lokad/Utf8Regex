using System.Buffers;
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
// State is borrowed from a synchronous stack/heap vector or the cursor's array;
// no span escapes a call. Projection reads it before publication. This owner
// reserves the same conservative capacity and Release accepts the default value.
internal struct PosixWorkspace
{
    private readonly PosixProgram _program;
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

    internal readonly int StateLength => _lastOffset + (_lastOffset - _openOffset);

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
            _stateBytes = bytes;
        }
        catch { invocation.Ledger.Release(bytes); throw; }
    }

    internal bool Find(ReadOnlySpan<byte> input, scoped Span<int> state, int fromByte, bool existenceOnly,
        ref Utf8SliceInvocation invocation, out int start, out int end, bool captureHistory = true)
    {
        captureHistory &= !existenceOnly;
        for (var candidate = fromByte; candidate <= input.Length;)
        {
            Charge(ref invocation);
            Reset(state, ref invocation);
            if (MatchAt(input, state, candidate, existenceOnly, captureHistory, ref invocation, out end))
            {
                start = candidate;
                if (captureHistory)
                {
                    var trace = _best.WrittenItems;
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
        // Saving the best copies the current trace exactly. Appending preserves
        // that shared prefix; rollback can only shorten it. No trace mutation or
        // speculative extension is needed to skip already equal occurrences.
        var sharedPrefix = 0; var firstBestClose = -1; var prefixTag = 1;
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
                        // A ceiling fixes the whole endpoint. The selected tag
                        // follows only parents proved fixed at that endpoint;
                        // later occurrences cannot repair its losing prefix.
                        if (instruction.A == prefixTag && end == maximumEnd)
                            failed = WorseClosedPrefix(prefixTag, sharedPrefix, firstBestClose, ref invocation);
                    }
                    pc++;
                    break;
                case Utf8SliceInstructionKind.Accept:
                    if (end < 0 && !existenceOnly && !_program.FirstAcceptanceIsLongest)
                    {
                        if (at < input.Length) maximumEnd = FindBoundaryCeiling(input, start, at, ref invocation);
                    }
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
                            // A better history can move a leading parent's end
                            // without moving the whole end. Derive its authority
                            // from this winner, before saving its closing prefix.
                            prefixTag = FindPrefixComparisonTag(input, at == maximumEnd, ref invocation);
                            Clear(ref _best, ref invocation);
                            firstBestClose = -1;
                            for (var index = 0; index < _trace.Count; index++)
                            {
                                if ((index & 63) == 0) Charge(ref invocation);
                                var item = _trace[index];
                                if (firstBestClose < 0 && item.Tag == prefixTag && item.Opening >= 0) firstBestClose = index;
                                Check(_best.TryPush(item, ref invocation), ref invocation);
                            }
                            sharedPrefix = _trace.Count;
                        }
                    }
                    failed = true;
                    break;
                default: throw new InvalidOperationException($"Unsupported POSIX instruction {instruction.Kind}.");
            }
            if (failed)
            {
                if (!Backtrack(state, ref pc, ref at, ref invocation)) return end >= 0;
                sharedPrefix = Math.Min(sharedPrefix, _trace.Count);
            }
        }
    }

    private int FindPrefixComparisonTag(ReadOnlySpan<byte> input, bool maximalAcceptance, ref Utf8SliceInvocation invocation)
    {
        var code = _program.Scalar.Instructions;
        var parents = _program.Parents;
        if (code.Length > 64 || code.Length < 3 || parents.Length < 3 ||
            code[0] is not { Kind: Utf8SliceInstructionKind.CaptureStart, A: 0 } ||
            code[1] is not { Kind: Utf8SliceInstructionKind.CaptureStart, A: 1 }) return 1;
        Charge(ref invocation);
        var tag = 1;
        // All earlier priorities open at the same start, before any body work.
        while (tag < parents.Length && tag < code.Length && parents[tag] == tag - 1 &&
            code[tag] is { Kind: Utf8SliceInstructionKind.CaptureStart } open && open.A == tag) tag++;
        if (tag >= parents.Length || parents[tag] != tag - 1) return 1;
        var close = -1;
        for (var pc = tag; pc < code.Length; pc++)
        {
            if (((pc - tag) & 31) == 0) Charge(ref invocation);
            if (code[pc] is { Kind: Utf8SliceInstructionKind.CaptureEnd } end && end.A == tag - 1)
            { close = pc; break; }
        }
        if (close < 0) return 1;
        // No body edge can re-enter an earlier opening or escape its enclosure.
        for (var pc = tag; pc < close; pc++)
        {
            if (((pc - tag) & 31) == 0) Charge(ref invocation);
            var instruction = code[pc];
            if (instruction.Kind is Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or
                Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatEnd)
            {
                if (instruction.A < tag || instruction.A > close) return 1;
            }
            else if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            {
                if (instruction.A < tag) return 1;
            }
            else if (instruction.Kind is not (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or
                Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Anchor or Utf8SliceInstructionKind.RepeatExit)) return 1;
        }
        // A fixed scalar suffix fixes every earlier closing boundary at a
        // maximal whole end, even when UTF-8 widths differ. Each enclosure
        // participates exactly once. Their
        // first child's occurrences are therefore the first unfixed priority.
        var enclosing = tag - 1;
        var fixedSuffix = true;
        for (var pc = close; pc < code.Length; pc++)
        {
            if (((pc - close) & 31) == 0) Charge(ref invocation);
            var instruction = code[pc];
            if (instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.B == 0) continue;
            if (instruction.Kind is Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.CategorySet) continue;
            if (instruction.Kind == Utf8SliceInstructionKind.CaptureEnd)
            {
                if (instruction.A < tag && instruction.A != enclosing--) return 1;
            }
            else if (instruction.Kind == Utf8SliceInstructionKind.CaptureStart && instruction.A >= tag) continue;
            else if (instruction.Kind == Utf8SliceInstructionKind.Accept && pc == code.Length - 1 && enclosing == -1)
                return fixedSuffix ? tag : maximalAcceptance
                    ? FindBoundaryBoundedParentTag(input, tag, close, ref invocation) : 1;
            else if (instruction.Kind is Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or
                Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatEnd or
                Utf8SliceInstructionKind.RepeatExit or Utf8SliceInstructionKind.Anchor) fixedSuffix = false;
            else return 1;
        }
        return 1;
    }

    private int FindBoundaryBoundedParentTag(ReadOnlySpan<byte> input, int tag, int close,
        ref Utf8SliceInvocation invocation)
    {
        var code = _program.Scalar.Instructions;
        var upper = code.Length - 1;
        var firstTarget = int.MaxValue; var lastTarget = -1;
        var traceIndex = _trace.Count - 1;
        // The preceding scan proves unique, reverse-ordered parent closings.
        // Each suffix segment may vary, but cannot skip or repeat those closes.
        for (var pc = code.Length - 2; pc >= close; pc--)
        {
            if (((code.Length - 2 - pc) & 31) == 0) Charge(ref invocation);
            var instruction = code[pc];
            if (instruction.Kind is Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump or
                Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatEnd)
            {
                firstTarget = Math.Min(firstTarget, instruction.A);
                lastTarget = Math.Max(lastTarget, instruction.A);
            }
            if (instruction.Kind != Utf8SliceInstructionKind.CaptureEnd || instruction.A >= tag) continue;
            if (firstTarget < pc + 1 || lastTarget > upper) return 1;
            firstTarget = int.MaxValue; lastTarget = -1; upper = pc;

            // Read the current maximal acceptance, not a hypothetical suffix
            // width. Its leading parents have the same fixed opening position.
            while (traceIndex >= 0)
            {
                if ((traceIndex & 63) == 0) Charge(ref invocation);
                var item = _trace[traceIndex--];
                if (item.Tag != instruction.A || item.Opening < 0) continue;
                if (instruction.A != 0 && item.Position < input.Length)
                {
                    // Only the next scalar matters: no body path can cross it
                    // if every consuming atom rejects it. Borrow the actual
                    // class predicate, including Unicode unions and negation.
                    if (Rune.DecodeFromUtf8(input[item.Position..], out var boundary, out _) != OperationStatus.Done) return 1;
                    for (var body = instruction.A + 1; body < pc; body++)
                    {
                        if (((body - instruction.A - 1) & 31) == 0) Charge(ref invocation);
                        var atom = code[body];
                        if (atom.Kind == Utf8SliceInstructionKind.Literal && atom.A == boundary.Value ||
                            atom.Kind == Utf8SliceInstructionKind.Dot && (atom.A != 0 || boundary.Value != '\n') ||
                            atom.Kind == Utf8SliceInstructionKind.CategorySet && MatchesClass(boundary, atom, ref invocation))
                        {
                            // An accepting atom may be unreachable here. The
                            // leading opening and confined enclosure identify
                            // the same parent on every competing history.
                            if (FindReachableBoundaryCeiling(input, _trace[item.Opening].Position,
                                item.Position, boundary, instruction.A + 1, pc, ref invocation) != item.Position) return 1;
                            break;
                        }
                    }
                }
                break;
            }
            if (traceIndex < 0) return 1;
        }
        // Each earlier parent is already at its independent upper bound. A
        // shorter continuation loses there; an equal one exposes this child.
        return tag;
    }

    private int FindBoundaryCeiling(ReadOnlySpan<byte> input, int start, int acceptedEnd, ref Utf8SliceInvocation invocation)
    {
        var code = _program.Scalar.Instructions;
        if (code.Length > 64) return input.Length;
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

    private bool WorseClosedPrefix(int tag, int sharedPrefix, int firstBestClose, ref Utf8SliceInvocation invocation)
    {
        var current = _trace.WrittenItems; var best = _best.WrittenItems;
        var a = sharedPrefix; var b = sharedPrefix;
        // Skipped equal occurrences still establish prior participation. A new
        // equal-prefix tail loses only after an earlier occurrence, including empty.
        var first = firstBestClose < 0 || firstBestClose >= sharedPrefix;
        while (true)
        {
            var ac = NextClose(current, tag, a, current.Length, ref invocation);
            if (ac < 0) return false; // A continuation can still add occurrences.
            var bc = NextClose(best, tag, b, best.Length - 1, ref invocation);
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

    internal readonly bool GetCapture(scoped ReadOnlySpan<int> state, int slot, ref Utf8SliceInvocation invocation, out int start, out int end)
    {
        var tag = _program.CaptureTags[slot];
        var close = state[_lastOffset + tag];
        start = end = -1;
        if (close < 0) return false;
        var open = _best[close].Opening;
        // Tag zero encloses the entire winning trace, including when it is a
        // public root capture. Only repeated inner parents can make a group's
        // last occurrence absent from its parent's final occurrence.
        for (var parent = _program.Parents[tag]; parent > 0; parent = _program.Parents[parent])
        {
            Charge(ref invocation);
            var parentClose = state[_lastOffset + parent];
            if (parentClose < 0 || _best[parentClose].Opening > open || parentClose < close) return false;
        }
        start = _best[open].Position; end = _best[close].Position;
        return true;
    }

    private void Reset(scoped Span<int> state, ref Utf8SliceInvocation invocation)
    {
        Clear(ref _frames, ref invocation); Clear(ref _undo, ref invocation);
        Clear(ref _trace, ref invocation); Clear(ref _best, ref invocation);
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
