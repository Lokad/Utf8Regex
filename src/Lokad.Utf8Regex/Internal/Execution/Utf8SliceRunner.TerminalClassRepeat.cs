using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8SliceTerminalRootKind : byte
{
    None,
    Capture,
    Boundary,
    ClassRepeat
}

internal static partial class Utf8SliceRunner
{
    internal static Utf8SliceTerminalRootKind ClassifyTerminalRoot(Utf8SliceProgram program)
    {
        if (program.IsNonBacktracking || program.RightToLeft) return Utf8SliceTerminalRootKind.None;
        if (CanRunTerminalCaptureRoot(program)) return Utf8SliceTerminalRootKind.Capture;
        if (program.GroupCount == 0 && program.Assertions.Length == 0 &&
            program.Instructions is [{ Kind: Utf8SliceInstructionKind.Boundary }, { Kind: Utf8SliceInstructionKind.Accept }])
            return Utf8SliceTerminalRootKind.Boundary;
        return CanRunTerminalClassRepeat(program) ? Utf8SliceTerminalRootKind.ClassRepeat : Utf8SliceTerminalRootKind.None;
    }

    internal static bool HasRequiredLiteralWitness(Utf8SliceProgram program, ReadOnlySpan<byte> needle)
    {
        var code = program.Instructions.AsSpan();
        if (program.IsNonBacktracking || program.HasBalancing || code.Length is < 6 or > 32 ||
            needle.Length is < 1 or > 64 || code[^1].Kind != Utf8SliceInstructionKind.Accept)
            return false;

        // At the literal's own start the initial repeat can consume zero. Only
        // consuming atoms may be skipped, and the remaining path must be the
        // exact literal through Accept, with plain capture markers allowed.
        var repeat = code[0];
        if (repeat.Kind is not (Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy) ||
            repeat.RepeatMinimum != 0 || !IsSingleAtomRepeat(code, 0, repeat) ||
            code[1].Kind is not (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or
                Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
                Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid))
            return false;

        var offset = 0;
        foreach (ref readonly var op in code[(repeat.A + 1)..^1])
        {
            if (op.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) continue;
            if (op is not { Kind: Utf8SliceInstructionKind.Literal, B: 0 } ||
                !TryReadScalar(needle, offset, false, out var scalar, out var width) || scalar.Value != op.A)
                return false;
            offset += width;
        }
        return offset == needle.Length;
    }

    private static bool CanRunTerminalCaptureRoot(Utf8SliceProgram program)
    {
        var code = program.Instructions;
        if (code.Length == 4 && program.GroupCount == 1 && program.RepeatCount == 0 &&
            !program.IgnoreCase &&
            !program.HasBalancing && !program.UsesCaptureOpeningJournal && program.Assertions.Length == 0 &&
            code[0] is { Kind: Utf8SliceInstructionKind.CaptureStart, A: 0 } &&
            code[1] is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } &&
            code[2] is { Kind: Utf8SliceInstructionKind.CaptureEnd, A: 0 } &&
            code[3].Kind == Utf8SliceInstructionKind.Accept) return true;
        if (program.GroupCount is not (1 or 2) || program.RepeatCount != program.GroupCount || code.Length is < 7 or > 64 ||
            program.IgnoreCase || program.HasBalancing ||
            program.UsesCaptureOpeningJournal || program.Assertions.Length != 0) return false;
        var repeatIndex = code.Length - 6;
        var prefixLength = repeatIndex - 1;
        var slot = program.GroupCount - 1;
        var literalStart = 0;
        var repeat = code[repeatIndex];
        if (code[prefixLength].Kind != Utf8SliceInstructionKind.CaptureStart || code[prefixLength].A != slot ||
            repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy || repeat.B != slot || repeat.A != repeatIndex + 3 ||
            repeat.RepeatMinimum != 1 || repeat.RepeatMaximum != long.MaxValue ||
            code[repeatIndex + 1] is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } atom ||
            GetUnfoldedAsciiRange(atom, out _) < 0 ||
            code[repeatIndex + 2].Kind != Utf8SliceInstructionKind.RepeatEnd || code[repeatIndex + 2].A != repeatIndex ||
            code[repeatIndex + 3].Kind != Utf8SliceInstructionKind.RepeatExit || code[repeatIndex + 3].A != slot ||
            code[^2].Kind != Utf8SliceInstructionKind.CaptureEnd || code[^2].A != slot ||
            code[^1].Kind != Utf8SliceInstructionKind.Accept) return false;
        if (slot != 0)
        {
            // A required excluded literal fixes the first greedy endpoint.
            // Keep the original two closing entries: detailed Python results
            // still observe which group closed last.
            if (prefixLength <= 6 ||
                code[0] is not { Kind: Utf8SliceInstructionKind.CaptureStart, A: 0 } ||
                code[1] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, A: 4, B: 0,
                    RepeatMinimum: 1, RepeatMaximum: long.MaxValue } ||
                code[2] is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } firstAtom ||
                code[3] is not { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 1 } ||
                code[4] is not { Kind: Utf8SliceInstructionKind.RepeatExit, A: 0 } ||
                code[5] is not { Kind: Utf8SliceInstructionKind.CaptureEnd, A: 0 }) return false;
            var firstLow = GetUnfoldedAsciiRange(firstAtom, out var firstHigh);
            if (firstLow < 0 || (uint)(code[6].A - firstLow) <= (uint)(firstHigh - firstLow)) return false;
            literalStart = 6;
        }
        for (var index = literalStart; index < prefixLength; index++)
            if (code[index] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0 } literal ||
                (uint)literal.A >= 128) return false;
        return true;
    }

    private static Utf8SliceMatchOutcome RunTerminalCaptureRoot(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int start, int[] captureStarts, int[] captureEnds, ref ChunkedStacks stacks, ref Utf8SliceInvocation invocation,
        bool minimumOnly)
    {
        var code = program.Instructions;
        if (program.RepeatCount == 0)
        {
            // One consuming atom has one endpoint and no rollback or competing
            // closing order. Keep its original scalar decoder and predicate.
            var single = code[1];
            var singleBreach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (singleBreach != Utf8ExecutionLimitKind.None)
                return new(false, -1, singleBreach, null, null);
            if (!TryReadScalar(input, start, false, out var scalar, out var width) ||
                !Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[single.A], false, program.CaseBehavior))
                return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
            singleBreach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (singleBreach != Utf8ExecutionLimitKind.None)
                return new(false, -1, singleBreach, null, null);
            if (captureStarts.Length != 0)
            {
                captureStarts[0] = start;
                captureEnds[0] = start + width;
            }
            return new(true, start + width, Utf8ExecutionLimitKind.None, null, null);
        }
        var repeatIndex = code.Length - 6;
        var prefixLength = repeatIndex - 1;
        var slot = program.GroupCount - 1;
        var literalStart = slot == 0 ? 0 : 6;

        // This complete root cannot rewind. Reuse its initialized workspace,
        // admitted closing journal and the existing bounded repeat algorithm.
        // Orchestration uses VM step cadence; RunClassRepeat checks the original
        // deadline per bounded byte block and at each completed repeat. Explicit
        // original-token checks and caller publication checks remain.
        var breach = invocation.TryChargeStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None)
            return new(false, -1, breach, null, null);
        var firstEnd = start;
        if (slot != 0)
        {
            // The complete-root proof already establishes unbounded positive
            // ASCII intervals with minimum one. Share the bounded scan without
            // rediscovering generic repeat/atom eligibility for each capture.
            var firstLow = GetUnfoldedAsciiRange(code[2], out var firstHigh);
            var first = RunUnboundedAsciiRange(input, start, (byte)firstLow, firstHigh, ref invocation);
            if (first.EndIndex <= start || first.Breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, first.Breach, null, null);
            firstEnd = first.EndIndex;
            breach = invocation.TryChargeStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, breach, null, null);
        }
        var literalLength = prefixLength - literalStart;
        if (input.Length - firstEnd < literalLength)
            return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
        for (var index = 0; index < literalLength; index++)
            if (input[firstEnd + index] != code[literalStart + index].A)
                return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
        var captureStart = firstEnd + literalLength;
        var lastLow = GetUnfoldedAsciiRange(code[repeatIndex + 1], out var lastHigh);
        var last = minimumOnly ? RunClassRepeat(program, repeatIndex, input, captureStart, ref invocation, minimumOnly: true)
            : RunUnboundedAsciiRange(input, captureStart, (byte)lastLow, lastHigh, ref invocation);
        if (last.EndIndex <= captureStart || last.Breach != Utf8ExecutionLimitKind.None)
            return new(false, -1, last.Breach, null, null);
        if (slot != 0 && captureStarts.Length != 0 &&
            (!stacks.PushTrail(0, captureEnds[0], firstEnd, false, ref invocation) ||
             !stacks.PushTrail(1, captureEnds[1], last.EndIndex, false, ref invocation)))
            return new(false, -1, stacks.Breach, null, null);
        breach = invocation.TryChargeStep();
        invocation.Budget.ThrowIfCancellationRequested();
        if (breach != Utf8ExecutionLimitKind.None)
            return new(false, -1, breach, null, null);
        if (captureStarts.Length != 0)
        {
            if (slot != 0) { captureStarts[0] = start; captureEnds[0] = firstEnd; }
            captureStarts[slot] = captureStart; captureEnds[slot] = last.EndIndex;
        }
        return new(true, last.EndIndex, Utf8ExecutionLimitKind.None, null, null);
    }

    private static bool CanRunTerminalClassRepeat(Utf8SliceProgram program)
    {
        if (program.GroupCount != 0 || program.RepeatCount != 1 || program.Instructions.Length != 5)
            return false;
        var instructions = program.Instructions;
        var repeat = instructions[0];
        var atom = instructions[1];
        if (repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy ||
            repeat.A != 3 || repeat.B != 0 || repeat.RepeatMinimum < 0 || repeat.RepeatMaximum < repeat.RepeatMinimum ||
            instructions[2].Kind != Utf8SliceInstructionKind.RepeatEnd || instructions[2].A != 0 ||
            instructions[3].Kind != Utf8SliceInstructionKind.RepeatExit || instructions[3].A != 0 ||
            instructions[4].Kind != Utf8SliceInstructionKind.Accept)
            return false;
        return atom.Kind == Utf8SliceInstructionKind.Dot || CanRepeatScalarAtom(program, atom);
    }

    private static bool CanRepeatScalarAtom(Utf8SliceProgram program, Utf8SliceInstruction atom)
    {
        if (atom.Kind is not (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
            Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid)) return false;
        if (atom.Kind == Utf8SliceInstructionKind.Literal && atom.B != 0) return false;
        // Keep each fused base step bounded. A nested class program retains its
        // own metering; a long list of independent members stays on the VM path.
        return atom.Kind != Utf8SliceInstructionKind.CategorySet ||
            program.CategorySetMembers[atom.D].Length <= 1;
    }

    internal static bool CanCountTerminalScalar(Utf8SliceProgram program)
        => TryGetTerminalCategory(program, out _, out _, out _) ||
            TryGetCountTerminalClassRepeat(program, out _, out _, out _);

    private static bool TryGetTerminalCategory(Utf8SliceProgram program,
        out uint categories, out ulong asciiLow, out ulong asciiHigh)
    {
        categories = 0;
        asciiLow = asciiHigh = 0;
        var code = program.Instructions.AsSpan();
        if (program.IsNonBacktracking || program.RightToLeft || code.Length is < 2 or > 64 ||
            code[^1].Kind != Utf8SliceInstructionKind.Accept) return false;

        // A complete single-scalar expression has one endpoint. Plain capture
        // markers cannot affect discovery here, and Count observes no captures.
        var index = 0;
        while (code[index].Kind == Utf8SliceInstructionKind.CaptureStart) ++index;
        var atom = code[index++];
        if (atom.Kind != Utf8SliceInstructionKind.CategorySet) return false;
        while (code[index].Kind == Utf8SliceInstructionKind.CaptureEnd) ++index;
        return index == code.Length - 1 &&
            TryGetPositiveCategorySet(program, atom, out categories, out asciiLow, out asciiHigh);
    }

    private static bool TryGetCountTerminalClassRepeat(Utf8SliceProgram program,
        out int repeatIndex, out int atomIndex, out (byte First, byte Second)? asciiStops)
    {
        repeatIndex = 0;
        atomIndex = 1;
        asciiStops = null;
        if (program.TerminalRoot == Utf8SliceTerminalRootKind.ClassRepeat)
        {
            var terminal = program.Instructions[0];
            if (terminal.RepeatMinimum <= 1 && terminal.RepeatMaximum == long.MaxValue &&
                TryGetAsciiRepeatStops(program, program.Instructions[1], out var first, out var second))
                asciiStops = (first, second);
            return terminal.RepeatMinimum > 0 || asciiStops.HasValue;
        }
        if (program.IsNonBacktracking || program.RightToLeft || program.RepeatCount != 1 ||
            program.GroupCount == 0 || program.HasBalancing || program.Assertions.Length != 0 ||
            program.Instructions.Length > 64) return false;

        // Count has no capture observer. A bounded walk ignores only plain
        // markers, retaining the original repeat's absolute control-flow targets.
        Span<int> positions = stackalloc int[5];
        var count = 0;
        var code = program.Instructions;
        for (var pc = 0; pc < code.Length; pc++)
        {
            if (code[pc].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) continue;
            if (count == positions.Length) return false;
            positions[count++] = pc;
        }
        if (count != positions.Length) return false;
        repeatIndex = positions[0];
        atomIndex = positions[1];
        var repeat = code[repeatIndex];
        return repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy && repeat.A == positions[3] && repeat.B == 0 &&
            repeat.RepeatMinimum > 0 && repeat.RepeatMaximum >= repeat.RepeatMinimum &&
            code[positions[2]].Kind == Utf8SliceInstructionKind.RepeatEnd && code[positions[2]].A == repeatIndex &&
            code[positions[3]].Kind == Utf8SliceInstructionKind.RepeatExit && code[positions[3]].A == repeat.B &&
            code[positions[4]].Kind == Utf8SliceInstructionKind.Accept && CanRepeatScalarAtom(program, code[atomIndex]);
    }

    internal static bool TryCountTerminalScalar(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int start, ref Utf8SliceInvocation invocation, out Utf8SliceCountOutcome outcome)
    {
        outcome = default;
        var repeatIndex = -1;
        var atomIndex = -1;
        (byte First, byte Second)? asciiStops = null;
        if (!TryGetTerminalCategory(program, out var categories, out var asciiLow, out var asciiHigh) &&
            !TryGetCountTerminalClassRepeat(program, out repeatIndex, out atomIndex, out asciiStops)) return false;
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)start > (uint)input.Length || start < input.Length && (input[start] & 0xc0) == 0x80) return true;
        invocation.AnchorOriginByte = start;

        if (repeatIndex >= 0 && program.Instructions[repeatIndex] is { RepeatMinimum: 1, RepeatMaximum: 1 } &&
            program.Instructions[atomIndex].Kind == Utf8SliceInstructionKind.CategorySet)
            TryGetPositiveCategorySet(program, program.Instructions[atomIndex], out categories, out asciiLow, out asciiHigh);
        if (categories != 0)
        {
            var breach = Utf8UnicodeCategoryExecutor.TryCount(input[start..], categories, asciiLow, asciiHigh,
                ref invocation, out var count);
            outcome = new(count, breach);
            return true;
        }
        if (asciiStops is { } stops)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach == Utf8ExecutionLimitKind.None) breach = invocation.TryReserveWorking(0);
            outcome = breach == Utf8ExecutionLimitKind.None
                ? CountTerminalAsciiRuns(input, start, program.Instructions[atomIndex] is { Kind: Utf8SliceInstructionKind.Dot, A: not 0 },
                    stops.First, stops.Second, program.Instructions[repeatIndex].RepeatMinimum == 0, ref invocation)
                : new(0, breach);
        }
        else outcome = CountTerminalClassRepeat(program, input, start, repeatIndex, atomIndex, ref invocation);
        return true;
    }

    private static Utf8SliceCountOutcome CountTerminalClassRepeat(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int start, int repeatIndex, int atomIndex, ref Utf8SliceInvocation invocation)
    {
        // This complete root has no tail to reconsider a greedy boundary.
        // Publish full maxima and discard each final run below the minimum.
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
        // Observe an enclosing loan without acquiring any local scratch.
        breach = invocation.TryReserveWorking(0);
        if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
        var repeat = program.Instructions[repeatIndex];
        var atom = program.Instructions[atomIndex];
        ReadOnlySpan<byte> asciiMask = atom.Kind == Utf8SliceInstructionKind.Set && atom.B == 0 && atom.C == 0
            ? program.SetMasks[atom.A] : [];
        var minimum = repeat.RepeatMinimum;
        var maximum = repeat.RepeatMaximum;
        byte asciiLast = 0;
        var asciiFirst = input.Length - start >= 256
            ? GetUnfoldedAsciiRange(atom, out asciiLast) : -1;
        var position = start;
        var run = 0;
        var count = 0;
        while (position < input.Length)
        {
            breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            if (run == 0 && asciiFirst >= 0 &&
                (uint)(input[position] - asciiFirst) > (uint)(asciiLast - asciiFirst))
            {
                // The complete atom cannot consume outside this exact ASCII
                // interval. Reuse bounded discovery; active runs still finish
                // through the original greedy minimum/maximum handling below.
                breach = TrySeekAsciiCandidate(input, ref position, (byte)asciiFirst, asciiLast, ref invocation);
                if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                if (position < 0) break;
            }
            bool hit;
            int width;
            if (!asciiMask.IsEmpty && input[position] < 128)
            {
                // Same positive ASCII predicate as RunClassRepeat, with the
                // original per-scalar charge. Wider/folded/complemented masks
                // retain full scalar decoding and membership semantics.
                var value = input[position];
                hit = (asciiMask[value >> 3] & (1 << (value & 7))) != 0;
                width = 1;
            }
            else
            {
                if (!TryReadScalar(input, position, false, out var scalar, out width)) break;
                breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out hit);
                if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            }
            if (hit)
            {
                position += width;
                if (++run == maximum)
                {
                    count++;
                    run = 0;
                    invocation.AnchorOriginByte = position;
                }
            }
            else
            {
                if (run >= minimum)
                {
                    count++;
                    invocation.AnchorOriginByte = position;
                }
                run = 0;
                position += width;
            }
        }
        if (run >= minimum)
        {
            count++;
            invocation.AnchorOriginByte = position;
        }
        breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return new(count, breach);
    }

    private static Utf8SliceCountOutcome CountTerminalAsciiRuns(ReadOnlySpan<byte> input, int position,
        bool dotAll, byte first, byte second, bool allowsEmpty, ref Utf8SliceInvocation invocation)
    {
        // Every excluded scalar gives one empty match for a nullable repeat.
        // Nonempty runs end at the stops, and EOF contributes one last empty.
        var emptyCount = allowsEmpty ? 1 : 0;
        var count = 0;
        var inRun = false;
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            var end = position + Math.Min(2048, input.Length - position);
            while (position < end)
            {
                var stop = IndexOfRepeatStop(input[position..end], dotAll, first, second);
                if (stop < 0)
                {
                    // A block may end inside a scalar. Only ASCII stops and
                    // the full subject end can complete this run.
                    inRun = true;
                    position = end;
                    break;
                }
                var matches = (inRun || stop > 0 ? 1 : 0) + emptyCount;
                if (matches != 0)
                {
                    count = checked(count + matches);
                    invocation.AnchorOriginByte = position + stop;
                }
                position += stop + 1;
                inRun = false;
            }
        }
        if (inRun || allowsEmpty)
        {
            count = checked(count + (inRun ? 1 : 0) + emptyCount);
            invocation.AnchorOriginByte = position;
        }
        var final = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return new(count, final);
    }

    private static Utf8SliceMatchOutcome RunTerminalClassRepeat(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int start, bool requireNonEmpty, ref Utf8SliceInvocation invocation, bool minimumOnly = false)
    {
        var outcome = RunClassRepeat(program, 0, input, start, ref invocation, minimumOnly);
        // A complete greedy atom has no other positive endpoint when its
        // longest match is empty. The profile caller owns scalar advancement.
        var found = outcome.EndIndex >= 0 && (!requireNonEmpty || outcome.EndIndex != start);
        return new(found, found ? outcome.EndIndex : -1, outcome.Breach, null, null);
    }

    internal static (ulong Mask, int Anchor) FindTerminalRepeatsWithoutRewind(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        var code = program.Instructions.AsSpan();
        if (code.Length < 6 || code[^1].Kind != Utf8SliceInstructionKind.Accept) return default;
        var end = Utf8SliceProgram.FindLastBeforeCaptureEnds(code[..^1]);
        if (end < 0) return default;
        var terminal = code[end];
        var wordBoundary = terminal is
            { Kind: Utf8SliceInstructionKind.Boundary, A: 1, B: (int)Utf8SliceWordKind.Unicode, C: 0 };
        if (!wordBoundary && !Utf8SliceProgram.IsAbsoluteEndConstraint(terminal)) return default;
        var equivalent = !wordBoundary && program.RepeatCount >= 3
            ? FindEquivalentTerminalRepeat(program, code, end, budget) : -1;
        var rewindFree = equivalent >= 0 ? 1UL << (end - equivalent) : 0;

        // Track ASCII bytes on which the suffix can start. The lax anchor can
        // also succeed before LF without consuming it. A branch that skips
        // directly to a word boundary may place it before any scalar.
        var first = wordBoundary ? Utf8SearchAsciiSet.FromMasks(ulong.MaxValue, ulong.MaxValue) : default;
        if (terminal.Kind == Utf8SliceInstructionKind.Anchor && terminal.A == (int)Utf8SliceAnchorKind.End)
            first = first.With((byte)'\n');
        var firstAsciiOnly = !wordBoundary;
        var repeatContinuation = -1;
        var firstAtRepeatContinuation = default(Utf8SearchAsciiSet);
        var asciiOnlyAtRepeatContinuation = false;
        var floor = Math.Max(0, end - 63);
        for (var pc = end - 1; pc >= floor; pc--)
        {
            budget.ChargeWork(1);
            var entry = code[pc];
            if (entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) continue;
            if (entry.Kind == Utf8SliceInstructionKind.RepeatExit)
            {
                var repeatIndex = pc - 3;
                if (repeatIndex < floor) break;
                var repeat = code[repeatIndex];
                if (repeat.Kind is not (Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy) ||
                    !IsSingleAtomRepeat(code, repeatIndex, repeat)) break;
                var instruction = code[repeatIndex + 1];
                Utf8SearchAsciiSet atom;
                bool asciiOnly;
                if (instruction is { Kind: Utf8SliceInstructionKind.Dot, A: 0 })
                {
                    // A dot consumes every scalar except LF. Only an ASCII-only
                    // suffix can prove that it excludes all of those scalars.
                    atom = Utf8SearchAsciiSet.FromMasks(ulong.MaxValue & ~(1UL << '\n'), ulong.MaxValue);
                    asciiOnly = false;
                }
                else if (!program.TryGetUnfoldedAsciiSet(instruction, out atom, out asciiOnly)) break;
                budget.ChargeWork(3);
                if (wordBoundary)
                {
                    // Every shorter nonempty decimal run ends between two
                    // Unicode word scalars. The boundary cannot succeed there.
                    if (repeat.RepeatMinimum == 0 || !IsDecimalDigitSet(program, code[repeatIndex + 1])) break;
                    rewindFree |= 1UL << (end - repeatIndex);
                    wordBoundary = false;
                }
                // At a shorter endpoint the next scalar belongs to the repeat.
                // An ASCII-only side makes the ASCII intersection decisive.
                else if ((asciiOnly || firstAsciiOnly) && (first.Low & atom.Low) == 0 && (first.High & atom.High) == 0)
                    rewindFree |= 1UL << (end - repeatIndex);

                // An adjacent alternative may jump over this repeat to the
                // same continuation. Save that already proved first-byte set.
                repeatContinuation = pc + 1;
                firstAtRepeatContinuation = first;
                asciiOnlyAtRepeatContinuation = firstAsciiOnly;
                firstAsciiOnly = asciiOnly && (repeat.RepeatMinimum != 0 || firstAsciiOnly);
                first = repeat.RepeatMinimum == 0
                    ? Utf8SearchAsciiSet.FromMasks(first.Low | atom.Low, first.High | atom.High) : atom;
                pc = repeatIndex;
            }
            else if (wordBoundary) break;
            else if (entry.Kind == Utf8SliceInstructionKind.Jump && entry.A == repeatContinuation)
            {
                first = firstAtRepeatContinuation;
                firstAsciiOnly = asciiOnlyAtRepeatContinuation;
            }
            else if (program.TryGetUnfoldedAsciiSet(entry, out var atom, out var asciiOnly))
            {
                first = atom;
                firstAsciiOnly = asciiOnly;
            }
            else break;
        }
        return (rewindFree, end);
    }

    private static int FindEquivalentTerminalRepeat(Utf8SliceProgram program, ReadOnlySpan<Utf8SliceInstruction> code,
        int end, Utf8ConstructionBudget budget)
    {
        if (end < 12) return -1;
        var afterRepeats = end;
        for (var skipped = 0; skipped < 32 && code[afterRepeats - 1].Kind == Utf8SliceInstructionKind.CaptureEnd; skipped++)
        {
            budget.ChargeWork(1);
            if (--afterRepeats < 12) return -1;
        }

        // Three adjacent single-atom repeats, each with an instruction for
        // entering, consuming, looping and leaving.
        var last = afterRepeats - 4;
        var optional = last - 4;
        var first = optional - 4;
        budget.ChargeWork(3);
        if (code[first] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMaximum: long.MaxValue } ||
            code[optional] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy,
                RepeatMinimum: 0, RepeatMaximum: 1 } ||
            code[last] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy,
                RepeatMinimum: 0, RepeatMaximum: long.MaxValue } ||
            !IsSingleAtomRepeat(code, first, code[first]) || !IsSingleAtomRepeat(code, optional, code[optional]) ||
            !IsSingleAtomRepeat(code, last, code[last]) ||
            code[optional + 1] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 and not '\n' } separator)
            return -1;

        var atom = code[first + 1];
        var repeated = code[last + 1];
        if (atom.Kind != repeated.Kind || atom.B != 0 || repeated.B != 0 || atom.C != 0 || repeated.C != 0) return -1;
        // Giving back part of the first run cannot expose the separator.
        // The second repeat can reach the same end and no capture separates them.
        if (atom.Kind == Utf8SliceInstructionKind.Literal)
            return atom.A == repeated.A && atom.A != separator.A && atom.A != '\n' ? first : -1;
        if (atom.Kind != Utf8SliceInstructionKind.Set)
        {
            var categories = program.GetCompleteAsciiCategoryMask(atom);
            if (categories == 0 || program.GetCompleteAsciiCategoryMask(repeated) != categories) return -1;
        }
        var mask = program.SetMasks[atom.A];
        return (mask[separator.A >> 3] & (1 << (separator.A & 7))) == 0 && (mask['\n' >> 3] & (1 << ('\n' & 7))) == 0 &&
            mask.AsSpan().SequenceEqual(program.SetMasks[repeated.A]) ? first : -1;
    }

    private static bool TryRunClassRepeatWithoutRewind(Utf8SliceProgram program, int repeatIndex,
        ReadOnlySpan<byte> input, int start, int? requiredEnd, ref Utf8SliceInvocation invocation,
        out (int EndIndex, Utf8ExecutionLimitKind Breach) outcome, out int continuation, bool existenceOnly)
    {
        outcome = (-1, Utf8ExecutionLimitKind.None);
        continuation = 0;
        var instructions = program.Instructions;
        var repeat = instructions[repeatIndex];
        if (program.RightToLeft || instructions.Length - repeatIndex < 5 ||
            repeat.Kind is not (Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy) ||
            !IsSingleAtomRepeat(instructions, repeatIndex, repeat)) return false;
        var atom = instructions[repeatIndex + 1];
        if (atom.Kind is not (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Set or
                Utf8SliceInstructionKind.Shorthand or Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid) ||
            atom.Kind == Utf8SliceInstructionKind.Literal && atom.B != 0)
            return false;
        continuation = repeat.A + 1;
        if (!existenceOnly &&
            repeat is { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 0, RepeatMaximum: long.MaxValue } &&
            atom is { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } &&
            (uint)start < (uint)input.Length && input[start] < 128)
        {
            var first = input[start];
            var member = (program.SetMasks[atom.A][first >> 3] & (1 << (first & 7))) != 0;
            if (member == (atom.B != 0))
            {
                // This complete ASCII predicate rejects the first scalar. Zero
                // is the only endpoint, so the suffix cannot cause a rewind.
                var emptyRunBreach = invocation.TryChargeInputStep();
                if (emptyRunBreach == Utf8ExecutionLimitKind.None)
                    emptyRunBreach = ChargeRepeatBoundary(ref invocation, checkDeadline: true);
                outcome = (emptyRunBreach == Utf8ExecutionLimitKind.None ? start : -1, emptyRunBreach);
                return true;
            }
        }
        if (repeat is { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 0, RepeatMaximum: 1 } &&
            requiredEnd is null && (start == input.Length || input[start] != '\n') &&
            program.HasTerminalGreedyDot(continuation))
        {
            // The terminal dot reaches the same end on either side of one
            // non-LF scalar. Run the greedy choice and leave captures to the VM.
            var optionalBreach = TryMatchOptionalScalar(program, atom, input, start, ref invocation, out var width);
            outcome = (optionalBreach == Utf8ExecutionLimitKind.None ? start + width : -1, optionalBreach);
            return true;
        }
        if (requiredEnd is null && (existenceOnly || repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy) &&
            CanAcceptEmptyTail(instructions, continuation))
        {
            // The unconditional empty path makes shorter greedy endpoints
            // unnecessary. Value results still run the tail in normal order.
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation, minimumOnly: existenceOnly);
            if (existenceOnly) continuation = instructions.Length - 1;
            return true;
        }
        // A fixed count excludes shorter endpoints.
        if (repeat.RepeatMinimum == repeat.RepeatMaximum)
        {
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
            return true;
        }
        var tail = repeatIndex + 4;
        // Capture boundaries still execute at the selected endpoint. Opening
        // another capture exposes new proof work: keep folded/table predicates
        // on their prior route, and inspect only cheap direct atoms through it.
        for (var skipped = 0; skipped < 32 && tail < instructions.Length &&
            (instructions[tail].Kind == Utf8SliceInstructionKind.CaptureEnd ||
             instructions[tail].Kind == Utf8SliceInstructionKind.CaptureStart &&
             (atom.Kind is Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot ||
              atom is { Kind: Utf8SliceInstructionKind.Set, C: 0 })); skipped++) tail++;
        if (tail == instructions.Length) return false;
        var suffix = instructions[tail];
        var requiresInputEnd = requiredEnd == input.Length;
        if ((requiredEnd is null || requiresInputEnd) && atom is { Kind: Utf8SliceInstructionKind.Dot, A: not 0 } &&
            repeat.RepeatMinimum <= 1 && repeat.RepeatMaximum == long.MaxValue &&
            suffix is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } &&
            TryGetStrictEndLiteralWidth(instructions.AsSpan(tail), requiresInputEnd, out var suffixWidth))
        {
            // An exact-width tail at absolute end allows just one endpoint.
            // Leave literal checks and captures to the VM at that endpoint.
            var end = input.Length - suffixWidth;
            if (end < 0 || (uint)start > (uint)end ||
                (input[start] & 0xC0) == 0x80 || (input[end] & 0xC0) == 0x80) return true;
            outcome = RunClassRepeat(program, repeatIndex, input[..end], start, ref invocation);
            return true;
        }
        if (repeat.Kind == Utf8SliceInstructionKind.RepeatLazy && atom.Kind == Utf8SliceInstructionKind.Dot &&
            repeat.RepeatMinimum == 0 && repeat.RepeatMaximum == long.MaxValue && requiredEnd is null &&
            suffix is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && (uint)suffix.A < 128 &&
            (uint)start <= (uint)input.Length &&
            (start == input.Length || (input[start] & 0xC0) != 0x80))
        {
            var afterLiteral = tail + 1;
            for (var skipped = 0; skipped < 32 && afterLiteral < instructions.Length &&
                instructions[afterLiteral].Kind == Utf8SliceInstructionKind.CaptureEnd; skipped++)
                afterLiteral++;
            if (afterLiteral == instructions.Length - 1 &&
                instructions[afterLiteral].Kind == Utf8SliceInstructionKind.Accept)
            {
                // The first reachable literal is the shortest successful end
                // of this repeat. Leave the literal and capture effects to the VM.
                outcome = FindLazyDotLiteral(input, start, (byte)suffix.A, atom.A != 0, ref invocation);
                return true;
            }
        }
        var endAnchor = suffix is
            { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd };
        // A lazy repeat needs a unique viable boundary to take its maximal
        // run. An end anchor can provide that proof as well as an excluded literal.
        if (repeat.Kind == Utf8SliceInstructionKind.RepeatLazy &&
            suffix.Kind != Utf8SliceInstructionKind.Literal && !endAnchor)
            return false;
        // RunFrom can retry a child after Accept to satisfy its required end.
        // Without that constraint, no shorter successful endpoint is requested.
        // An atomic commit discards shorter greedy endpoints even if its tail
        // later fails. Absolute end rejects every shorter endpoint, even when
        // the atom can consume LF. Capture endings and the tail still execute.
        if (suffix.Kind == Utf8SliceInstructionKind.Accept && (requiredEnd is null || requiresInputEnd) ||
            suffix.Kind == Utf8SliceInstructionKind.AtomicCommit ||
            suffix is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.VeryEnd })
        {
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
            return true;
        }
        if (requiredEnd is null && repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
            repeat.RepeatMinimum <= 1 && repeat.RepeatMaximum == long.MaxValue &&
            atom.Kind == Utf8SliceInstructionKind.Dot && atom.A != 0 &&
            endAnchor)
        {
            var afterEnd = tail + 1;
            for (var skipped = 0; skipped < 32 && afterEnd < instructions.Length &&
                instructions[afterEnd].Kind == Utf8SliceInstructionKind.CaptureEnd; skipped++) afterEnd++;
            if (afterEnd == instructions.Length - 1 && instructions[afterEnd].Kind == Utf8SliceInstructionKind.Accept)
            {
                // Greedy DotAll reaches EOF, where this complete end tail always
                // succeeds. Required assertion endpoints and consuming tails stay opaque.
                outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
                return true;
            }
        }
        if (suffix.Kind == Utf8SliceInstructionKind.Boundary && suffix.A == 1 &&
            repeat.RepeatMinimum >= 1 && repeat.RepeatMaximum <= 8)
        {
            // Every shorter nonempty endpoint lies between consumed word scalars,
            // where a positive boundary cannot succeed. Prove only this bounded
            // ASCII input window using the actual profile predicate. Zero minima
            // can rewind to a boundary before the first scalar and stay on the VM.
            var proofBreach = invocation.TryChargeStep();
            if (proofBreach != Utf8ExecutionLimitKind.None)
            {
                outcome = (-1, proofBreach);
                return true;
            }
            var window = input.Slice(start, Math.Min(input.Length - start, (int)repeat.RepeatMaximum));
            var onlyWordCanBeConsumed = true;
            foreach (var value in window)
            {
                if (value >= 128) { onlyWordCanBeConsumed = false; break; }
                if (Utf8SliceScalarClasses.IsBoundaryWord(new Rune(value), program, suffix)) continue;
                // An excluded nonword stops the run before this scalar. Later
                // bytes cannot introduce a consumed nonword or useful rewind.
                proofBreach = TryMatchScalarClass(program, atom, new Rune(value), ref invocation, out var consumesNonword);
                if (proofBreach != Utf8ExecutionLimitKind.None)
                {
                    outcome = (-1, proofBreach);
                    return true;
                }
                onlyWordCanBeConsumed = !consumesNonword;
                break;
            }
            if (onlyWordCanBeConsumed)
            {
                outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
                return true;
            }
        }
        // A mandatory positive ASCII set excludes every shorter endpoint when
        // none of its members can be consumed by this repeat. A nonempty simple
        // following repeat has the same required first atom. Nullable/folded or
        // complemented followers keep their normal backtracking paths.
        var following = suffix;
        if (following.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy)
        {
            if (following.RepeatMinimum < 1 || instructions.Length - tail < 5 || following.A != tail + 3 ||
                instructions[tail + 2].Kind != Utf8SliceInstructionKind.RepeatEnd || instructions[tail + 2].A != tail ||
                instructions[tail + 3].Kind != Utf8SliceInstructionKind.RepeatExit || instructions[tail + 3].A != following.B)
                return false;
            following = instructions[tail + 1];
            if (repeat.RepeatMinimum > 0 &&
                atom is { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } &&
                following is { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } &&
                RejectsAdjacentHybridRuns(program, atom, following, input, start)) return true;
        }
        if (following is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 })
        {
            ReadOnlySpan<byte> mask = program.SetMasks[following.A].AsSpan(0, 16);
            if (atom.Kind == Utf8SliceInstructionKind.Literal)
            {
                // The repeat's exact literal can intersect the following
                // ASCII set at only one scalar. Check that member directly.
                var proofBreach = invocation.TryChargeStep();
                if (proofBreach != Utf8ExecutionLimitKind.None)
                { outcome = (-1, proofBreach); return true; }
                var value = atom.A;
                if ((uint)value < 128 && (mask[value >> 3] & (1 << (value & 7))) != 0) return false;
                outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
                return true;
            }
            if (atom is { Kind: Utf8SliceInstructionKind.Set, C: 0 } or
                { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 })
            {
                // Unfolded hybrid masks already include their shorthand's
                // ASCII members. The follower contains no wider scalars.
                var own = program.SetMasks[atom.A].AsSpan(0, 16);
                for (var offset = 0; offset < 16; offset += 8)
                {
                    var proofBreach = invocation.TryChargeStep();
                    if (proofBreach != Utf8ExecutionLimitKind.None)
                    { outcome = (-1, proofBreach); return true; }
                    var bits = BinaryPrimitives.ReadUInt64LittleEndian(own[offset..]);
                    if (atom.B != 0) bits = ~bits;
                    if ((bits & BinaryPrimitives.ReadUInt64LittleEndian(mask[offset..])) != 0) return false;
                }
                outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
                return true;
            }
            Func<int, bool>? predicate = null;
            ReadOnlySpan<byte> predicateMask = default;
            var predicateNegated = false;
            if (atom.Kind == Utf8SliceInstructionKind.CategorySet && (atom.B & 2) == 0 && atom.C == 0 &&
                program.CategorySetMembers[atom.D] is { Length: 1 } members &&
                members[0].Table.ClassProgram is null && members[0].Table.ScalarPredicate is { } scalarPredicate)
            {
                predicate = scalarPredicate;
                predicateMask = program.SetMasks[atom.A];
                predicateNegated = members[0].Negated;
            }
            for (var octet = 0; octet < mask.Length; octet++)
            {
                var bits = (uint)mask[octet];
                while (bits != 0)
                {
                    var value = octet * 8 + BitOperations.TrailingZeroCount(bits);
                    // At most128 members; retain each profile's actual predicate
                    // and charge proof work before inspecting a member.
                    var proofBreach = invocation.TryChargeStep();
                    var overlaps = false;
                    if (proofBreach == Utf8ExecutionLimitKind.None)
                    {
                        // Bind a sole pure table once; preserve mask short-circuit,
                        // both negations and the original per-member authority.
                        if (predicate is not null)
                            overlaps = (((predicateMask[value >> 3] & (1 << (value & 7))) != 0 ||
                                predicate(value) != predicateNegated) != ((atom.B & 1) != 0));
                        else
                            proofBreach = TryMatchScalarClass(program, atom, new Rune(value), ref invocation, out overlaps);
                    }
                    if (proofBreach != Utf8ExecutionLimitKind.None)
                    {
                        outcome = (-1, proofBreach);
                        return true;
                    }
                    if (overlaps) return false;
                    bits &= bits - 1;
                }
            }
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
            return true;
        }
        if (following.Kind is Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid &&
            TryGetPositiveCategorySet(program, atom, out var ownCategories, out var ownLow, out var ownHigh) &&
            TryGetPositiveCategorySet(program, following, out var nextCategories, out var nextLow, out var nextHigh) &&
            (ownCategories & nextCategories) == 0 && (ownLow & nextLow) == 0 && (ownHigh & nextHigh) == 0)
        {
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
            return true;
        }
        var leadingBoundary = -1;
        if (suffix.Kind == Utf8SliceInstructionKind.Boundary && tail + 1 < instructions.Length &&
            instructions[tail + 1] is { Kind: Utf8SliceInstructionKind.Literal, B: 0 })
        {
            // A boundary consumes nothing. Its following literal is still
            // necessary at the repeat endpoint, including for absence checks.
            leadingBoundary = tail;
            suffix = instructions[++tail];
        }
        // End anchors can succeed only at the subject end or before LF. If the
        // atom excludes LF, no shorter endpoint can satisfy either kind of $.
        var alternateScalar = -1;
        int excludedScalar;
        if (suffix is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } && suffix.A > tail)
        {
            // Each alternative's first consuming atom must be an excluded literal.
            // Only inspect through bounded capture markers;
            // the VM still executes their effects and chooses the branch normally.
            if (!Utf8SliceInstruction.TryGetLiteralAfterCaptureMarkers(instructions, tail + 1, out excludedScalar) ||
                !Utf8SliceInstruction.TryGetLiteralAfterCaptureMarkers(instructions, suffix.A, out alternateScalar))
                return false;
        }
        else
        {
            excludedScalar = suffix.Kind == Utf8SliceInstructionKind.Literal && suffix.B == 0 && Rune.IsValid(suffix.A)
                ? suffix.A
                : endAnchor ? '\n' : -1;
        }
        if (excludedScalar < 0) return false;

        // Every shorter boundary points at a scalar accepted by this
        // side-effect-free atom. If the following literal/end stop is excluded,
        // none of those alternatives can succeed. CaptureEnd still executes
        // normally, with its original trail and publication authority.
        var breach = invocation.TryChargeStep();
        var includesLiteral = false;
        if (breach == Utf8ExecutionLimitKind.None)
            breach = TryMatchScalarClass(program, atom, new Rune(excludedScalar), ref invocation, out includesLiteral);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            outcome = (-1, breach);
            return true;
        }
        if (includesLiteral)
        {
            if (repeat.Kind == Utf8SliceInstructionKind.RepeatLazy || suffix.Kind != Utf8SliceInstructionKind.Literal)
                return false;
            if (atom.Kind == Utf8SliceInstructionKind.Dot && (uint)suffix.A < 128 && repeat.RepeatMaximum is >= 8 and <= 511)
            {
                // An absent prefix also rejects bounded dots with nonterminal tails.
                // Four bytes per scalar is a conservative upper bound on its reach.
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None)
                { outcome = (-1, breach); return true; }
                Span<byte> prefix = stackalloc byte[8];
                prefix[0] = (byte)suffix.A;
                var prefixLength = 1;
                while (prefixLength < prefix.Length && prefixLength < instructions.Length - tail)
                {
                    var next = instructions[tail + prefixLength];
                    if (next.Kind != Utf8SliceInstructionKind.Literal || next.B != 0 || (uint)next.A >= 128)
                        break;
                    prefix[prefixLength++] = (byte)next.A;
                }
                var length = Math.Min(input.Length - start, (int)repeat.RepeatMaximum * 4 + prefixLength);
                var window = input.Slice(start, length);
                var possible = prefixLength == 1 ? window.IndexOf(prefix[0]) >= 0
                    : window.IndexOf(prefix[..prefixLength]) >= 0;
                breach = invocation.TryChargeInputStep();
                invocation.Budget.ThrowIfCancellationRequested();
                if (breach != Utf8ExecutionLimitKind.None || !possible)
                { outcome = (-1, breach); return true; }
            }
            return TryRunGreedyRepeatTerminalLiteral(program, repeatIndex, tail, leadingBoundary, input, start,
                requiredEnd, ref invocation, out outcome);
        }
        if (alternateScalar >= 0)
        {
            breach = invocation.TryChargeStep();
            if (breach == Utf8ExecutionLimitKind.None)
                breach = TryMatchScalarClass(program, atom, new Rune(alternateScalar), ref invocation, out includesLiteral);
            if (breach != Utf8ExecutionLimitKind.None)
            {
                outcome = (-1, breach);
                return true;
            }
            if (includesLiteral) return false;
        }
        outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
        return true;
    }

    internal static void PrepareScalarRepeatContinuation(Span<Utf8SliceInstruction> code, byte[][] setMasks,
        int repeatIndex, Utf8ConstructionBudget budget)
    {
        var repeat = code[repeatIndex];
        if (!IsSingleAtomRepeat(code, repeatIndex, repeat) || repeat.RepeatMinimum == repeat.RepeatMaximum)
            return;
        var atom = code[repeatIndex + 1];
        if (atom is not ({ Kind: Utf8SliceInstructionKind.Dot } or
                         { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } or
                         { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, D: 0 }))
            return;

        var continuation = repeat.A + 1;
        var exit = code[repeat.A];
        if (repeat is { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: > 0 } &&
            atom is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 })
        {
            // Every visited instruction moves forward. The same bounded proof
            // used by execution can be paid once before publishing the program.
            budget.ChargeWork((ulong)Math.Min(32, code.Length - continuation));
            if (CanAcceptEmptyTail(code, continuation))
            {
                exit = new(exit.Kind, exit.A, exit.B, exit.C | Utf8SliceInstruction.RepeatExitAcceptsEmptyTailFlag, exit.D);
                code[repeat.A] = exit;
            }
        }
        for (var remaining = 32; continuation < code.Length && remaining > 0 &&
            code[continuation].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd; remaining--)
        {
            budget.ChargeWork(1);
            continuation++;
        }
        if (continuation >= code.Length || code[continuation] is not
            { Kind: Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Literal, B: 0 })
            return;

        if (atom.Kind == Utf8SliceInstructionKind.Dot)
        {
            if (code[continuation].Kind != Utf8SliceInstructionKind.Fork) return;
            var starts = FindRepeatRewindStarts(code, setMasks, continuation, budget);
            if (starts != 0) code[repeat.A] = new(exit.Kind, exit.A, exit.B, exit.C, starts);
            return;
        }

        // A direct literal needs one visit. A fork shares a bounded allowance
        // among its arms. Both tests use the same repeat-exit certificate.
        var work = code[continuation].Kind == Utf8SliceInstructionKind.Literal ? 1 : 12;
        budget.ChargeWork((ulong)work + 2);
        var mask = setMasks[atom.A].AsSpan();
        var consumed = Utf8SearchAsciiSet.FromMasks(BinaryPrimitives.ReadUInt64LittleEndian(mask),
            BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]));
        if (ExcludesShorterRepeatEnds(code, continuation, consumed, atom.Kind == Utf8SliceInstructionKind.Set, ref work))
            code[repeat.A] = new(exit.Kind, exit.A, exit.B, exit.C | Utf8SliceInstruction.RepeatExitRejectsShorterFlag, exit.D);
    }

    private static int FindRepeatRewindStarts(ReadOnlySpan<Utf8SliceInstruction> code, byte[][] setMasks,
        int continuation, Utf8ConstructionBudget budget)
    {
        var remaining = 12;
        budget.ChargeWork((ulong)(remaining * 3));
        Span<byte> starts = stackalloc byte[3];
        var count = 0;
        if (!CollectStarts(code, setMasks, continuation, starts, ref count, ref remaining) || count == 0) return 0;
        starts[count..].Fill(starts[count - 1]);
        return (1 << 24) | starts[0] | (starts[1] << 8) | (starts[2] << 16);

        static bool CollectStarts(ReadOnlySpan<Utf8SliceInstruction> code, byte[][] masks, int index,
            Span<byte> starts, ref int count, ref int remaining)
        {
            // Every arm must consume one of these ASCII bytes first. Unresolved
            // effects, nullable arms and wider or folded classes keep scalar rewind.
            for (; remaining > 0 && (uint)index < (uint)code.Length; index++)
            {
                remaining--;
                switch (code[index])
                {
                    case { Kind: Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd }:
                        continue;
                    case { Kind: Utf8SliceInstructionKind.Fork, B: 0 } fork when fork.A > index:
                        if (!CollectStarts(code, masks, fork.A, starts, ref count, ref remaining)) return false;
                        continue;
                    case { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } literal:
                        return TryAddStartByte((byte)literal.A, starts, ref count);
                    case { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } set when masks[set.A].Length == 16:
                        var mask = masks[set.A].AsSpan();
                        for (var part = 0; part < 2; part++)
                        {
                            var bits = BinaryPrimitives.ReadUInt64LittleEndian(mask[(part * 8)..]);
                            while (bits != 0)
                            {
                                var value = (byte)(part * 64 + BitOperations.TrailingZeroCount(bits));
                                if (!TryAddStartByte(value, starts, ref count)) return false;
                                bits &= bits - 1;
                            }
                        }
                        return true;
                    default:
                        return false;
                }
            }
            return false;
        }

        static bool TryAddStartByte(byte first, Span<byte> starts, ref int count)
        {
            if (starts[..count].Contains(first)) return true;
            if (count == starts.Length) return false;
            starts[count++] = first;
            return true;
        }
    }

    private static Utf8ExecutionLimitKind FindPreviousRepeatStart(ReadOnlySpan<byte> input, int lower,
        int upper, int starts, ref Utf8SliceInvocation invocation, out int position)
    {
        // ASCII bytes are scalar boundaries. Exclude the endpoint already tried
        // and keep the same descending order within the admitted scalar run.
        while (upper > lower)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) { position = -1; return breach; }
            var blockStart = Math.Max(lower, upper - 2048);
            var found = input[blockStart..upper].LastIndexOfAny((byte)starts, (byte)(starts >> 8), (byte)(starts >> 16));
            if (found >= 0) { position = blockStart + found; return Utf8ExecutionLimitKind.None; }
            upper = blockStart;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    internal static void PrepareAsciiFork(Span<Utf8SliceInstruction> code, byte[][] setMasks,
        int forkIndex, Utf8ConstructionBudget budget)
    {
        ref var fork = ref code[forkIndex];
        if (fork is not { B: 0, C: 0, D: 0 } || fork.A <= forkIndex + 1)
            return;

        ref var first = ref code[forkIndex + 1];
        if (first is { Kind: Utf8SliceInstructionKind.Literal, B: 0, D: 0, A: >= 0 and < 128 } &&
            fork.A < code.Length - 1 &&
            code[fork.A] is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } next && next.A > fork.A + 1 &&
            code[fork.A + 1] is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } repeated && repeated.A == first.A)
        {
            budget.ChargeWork(1);
            first = new(first.Kind, first.A, first.B, first.C, next.A);
            return;
        }

        if (first is not { Kind: Utf8SliceInstructionKind.Set, B: 1, C: 0 } ||
            code[fork.A] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } alternate)
            return;
        budget.ChargeWork(1);
        var mask = setMasks[first.A];
        if (mask.Length == 16 && (mask[alternate.A >> 3] & (1 << (alternate.A & 7))) != 0)
            fork = new(fork.Kind, fork.A, 0, ~alternate.A, 0);
    }

    internal static void MarkExcludedLoopStart(Span<Utf8SliceInstruction> code, byte[][] setMasks,
        int jumpIndex, Utf8ConstructionBudget budget)
    {
        var jump = code[jumpIndex];
        if (jump.A < 0 || jump.A >= jumpIndex - 1 || jumpIndex + 1 >= code.Length ||
            code[jumpIndex - 1] is not { Kind: Utf8SliceInstructionKind.Fork, B: 0, C: 0, D: 0 } fork ||
            fork.A != jumpIndex + 1 ||
            code[fork.A] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } exit)
            return;

        // A loop exit competes with one more iteration. Mark its ASCII
        // literal when every body arm rejects that byte at its first atom.
        var remaining = 12;
        budget.ChargeWork((ulong)remaining);
        if (RejectsLoopStart(code[..(jumpIndex - 1)], setMasks, jump.A, exit.A, ref remaining))
        {
            // A plain back edge and its literal exit are disjoint at this
            // byte. A delimited back edge still needs its own viability check.
            if (jump.B == 0)
                code[jumpIndex - 1] = new(fork.Kind, fork.A, 0, ~exit.A, 0);
            else
                code[jumpIndex] = new(jump.Kind, jump.A, jump.B, exit.A + 1, jump.D);
        }

        static bool RejectsLoopStart(ReadOnlySpan<Utf8SliceInstruction> body, byte[][] masks,
            int index, int value, ref int remaining)
        {
            for (; index < body.Length && remaining > 0; index++)
            {
                remaining--;
                var entry = body[index];
                switch (entry)
                {
                    case { Kind: Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd }:
                        continue;
                    case { Kind: Utf8SliceInstructionKind.Fork, B: 0 } when entry.A > index:
                        if (!RejectsLoopStart(body, masks, entry.A, value, ref remaining)) return false;
                        continue;
                    case { Kind: Utf8SliceInstructionKind.Literal, B: 0 }:
                        return entry.A != value;
                    case { Kind: Utf8SliceInstructionKind.Set, C: 0 } when masks[entry.A].Length == 16:
                        var contains = (masks[entry.A][value >> 3] & (1 << (value & 7))) != 0;
                        return contains == (entry.B != 0);
                    case { Kind: Utf8SliceInstructionKind.Dot, A: 0 }:
                        return value == '\n';
                    default:
                        return false;
                }
            }
            return false;
        }
    }

    private static bool ExcludesShorterRepeatEnds(ReadOnlySpan<Utf8SliceInstruction> instructions,
        int index, Utf8SearchAsciiSet consumedAscii, bool asciiOnly, ref int remaining)
    {
        // Each arm must reject the scalar at a shorter endpoint. An unfolded
        // hybrid mask describes all ASCII members but may also admit wider ones.
        for (; remaining > 0 && (uint)index < (uint)instructions.Length; index++)
        {
            remaining--;
            var entry = instructions[index];
            switch (entry)
            {
                case { Kind: Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd }:
                    continue;
                case { Kind: Utf8SliceInstructionKind.Fork, B: 0 } when entry.A > index:
                    if (!ExcludesShorterRepeatEnds(instructions, entry.A, consumedAscii, asciiOnly, ref remaining)) return false;
                    continue;
                case { Kind: Utf8SliceInstructionKind.Literal, B: 0 }:
                    return (uint)entry.A < 128 ? !consumedAscii.Contains((byte)entry.A) : asciiOnly;
                case { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.VeryEnd }:
                    return true;
                case { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.End }:
                    return !consumedAscii.Contains((byte)'\n');
                default:
                    return false;
            }
        }
        return false;
    }

    private static bool TryGetPositiveCategorySet(Utf8SliceProgram program,
        in Utf8SliceInstruction atom, out uint unicodeCategories, out ulong asciiLow, out ulong asciiHigh)
    {
        asciiLow = asciiHigh = 0;
        unicodeCategories = program.GetCompleteAsciiCategoryMask(atom);
        if (unicodeCategories == 0)
        {
            if (atom is not { Kind: Utf8SliceInstructionKind.Hybrid, B: 0, D: 0 } ||
                atom.C != 1 << (int)Utf8SliceShorthand.Space) return false;
            // The separator categories plus controls bound Unicode whitespace.
            // Add the exact ASCII shorthand members to the explicit mask.
            unicodeCategories = (1u << (int)UnicodeCategory.SpaceSeparator) |
                (1u << (int)UnicodeCategory.LineSeparator) |
                (1u << (int)UnicodeCategory.ParagraphSeparator) |
                (1u << (int)UnicodeCategory.Control);
            asciiLow = (1UL << ' ') | 0x3e00UL; // U+0009..U+000D and U+0020
        }

        ReadOnlySpan<byte> mask = program.SetMasks[atom.A];
        asciiLow |= BinaryPrimitives.ReadUInt64LittleEndian(mask);
        asciiHigh = BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]);
        return true;
    }

    private static (int EndIndex, Utf8ExecutionLimitKind Breach) FindLazyDotLiteral(ReadOnlySpan<byte> input, int position,
        byte literal, bool singleline, ref Utf8SliceInvocation invocation)
    {
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
                return (-1, breach);
            var length = Math.Min(2048, input.Length - position);
            var block = input.Slice(position, length);
            var relative = singleline ? block.IndexOf(literal) : block.IndexOfAny(literal, (byte)'\n');
            invocation.Budget.ThrowIfCancellationRequested();
            if (relative >= 0)
            {
                // ASCII endpoints cannot land inside a valid UTF-8 scalar.
                // LF itself is a valid endpoint if the literal consumes it.
                var found = block[relative] == literal;
                return (found ? position + relative : -1, Utf8ExecutionLimitKind.None);
            }
            position += length;
        }
        return (-1, Utf8ExecutionLimitKind.None);
    }

    private static bool TryRunGreedyRepeatTerminalLiteral(Utf8SliceProgram program, int repeatIndex, int tail, int leadingBoundary,
        ReadOnlySpan<byte> input, int start, int? requiredEnd, ref Utf8SliceInvocation invocation,
        out (int EndIndex, Utf8ExecutionLimitKind Breach) outcome)
    {
        outcome = (-1, Utf8ExecutionLimitKind.None);
        // Prove the literal through Accept before discarding rewind choices.
        // Capture boundaries still execute in the VM at the selected endpoint.
        if (program.HasBalancing) return false;
        Span<byte> literal = stackalloc byte[64];
        var length = 0;
        var cursor = tail;
        var instructions = program.Instructions;
        while (cursor < instructions.Length && instructions[cursor].Kind == Utf8SliceInstructionKind.Literal)
        {
            var item = instructions[cursor++];
            if (item.B != 0 || length == literal.Length) return false;
            if ((uint)item.A < 128)
                literal[length++] = (byte)item.A;
            else
            {
                if (!Rune.TryCreate(item.A, out var scalar) ||
                    !scalar.TryEncodeToUtf8(literal[length..], out var written)) return false;
                length += written;
            }
        }
        var trailingBoundary = -1;
        for (var skipped = 0; skipped < 32 && cursor < instructions.Length; skipped++, cursor++)
        {
            if (instructions[cursor].Kind == Utf8SliceInstructionKind.CaptureEnd) continue;
            if (trailingBoundary >= 0 || instructions[cursor].Kind != Utf8SliceInstructionKind.Boundary) break;
            trailingBoundary = cursor;
        }
        if (cursor < instructions.Length && instructions[cursor].Kind == Utf8SliceInstructionKind.Jump)
            cursor = instructions[cursor].A;
        if (length == 0 || (uint)cursor >= (uint)instructions.Length ||
            instructions[cursor].Kind != Utf8SliceInstructionKind.Accept) return false;

        // Each reverse-search block checks the deadline before reading bytes.
        // An empty window or a failed repeat still checks it before returning.
        var run = RunClassRepeat(program, repeatIndex, input, start, ref invocation, out var minimumEnd,
            checkExitDeadline: false);
        if (run.EndIndex < 0 || run.Breach != Utf8ExecutionLimitKind.None)
        {
            invocation.Budget.CheckDeadline();
            outcome = (-1, run.Breach);
            return true;
        }

        var last = Math.Min(run.EndIndex, input.Length - length);
        if (requiredEnd is int end)
        {
            // Within an assertion only one endpoint can reach the required end.
            last = Math.Min(last, end - length);
            minimumEnd = Math.Max(minimumEnd, end - length);
        }

        // Every scalar boundary between the two run endpoints is reachable.
        // A complete UTF-8 literal starts with a lead byte, so an exact byte
        // hit supplies a scalar boundary even when the search window splits one.
        var selected = -1;
        while (last >= minimumEnd)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
            {
                outcome = (-1, breach);
                return true;
            }
            var first = Math.Max(minimumEnd, last - (2048 - length));
            var block = input.Slice(first, last - first + length);
            var hit = length == 1 ? block.LastIndexOf(literal[0]) : block.LastIndexOf(literal[..length]);
            if (hit < 0)
            {
                last = first - 1;
                continue;
            }
            var candidate = first + hit;
            if ((leadingBoundary < 0 || MatchesBoundary(program, instructions[leadingBoundary], input, candidate)) &&
                (trailingBoundary < 0 || MatchesBoundary(program, instructions[trailingBoundary], input, candidate + length)))
            {
                selected = candidate;
                break;
            }
            // A byte-identical literal can fail either boundary. Try the next
            // earlier occurrence, including overlapping literals, in order.
            last = candidate - 1;
        }
        invocation.Budget.ThrowIfCancellationRequested();
        invocation.Budget.CheckDeadline();
        outcome = (selected, Utf8ExecutionLimitKind.None);
        return true;
    }

    private static bool TryGetStrictEndLiteralWidth(ReadOnlySpan<Utf8SliceInstruction> tail, bool implicitEnd, out int width)
    {
        width = 0;
        if (tail.Length is < 2 or > 64 || tail[^1].Kind != Utf8SliceInstructionKind.Accept) return false;
        var anchor = tail.Length - 1;
        if (!implicitEnd)
        {
            anchor--;
            while (anchor > 0 && tail[anchor].Kind == Utf8SliceInstructionKind.CaptureEnd) anchor--;
            if (tail[anchor] is not { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.VeryEnd })
                return false;
        }

        foreach (ref readonly var instruction in tail[..anchor])
        {
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                continue;
            if (instruction is not { Kind: Utf8SliceInstructionKind.Literal, B: 0 } ||
                !Rune.TryCreate(instruction.A, out var scalar)) return false;
            width += scalar.Utf8SequenceLength;
        }
        return width != 0;
    }

    // Shared by generic repeats and complete capture roots. Scalar alignment is
    // preserved because every consuming byte belongs to the proved ASCII range.
    private static (int EndIndex, Utf8ExecutionLimitKind Breach) RunUnboundedAsciiRange(
        ReadOnlySpan<byte> input, int start, byte first, byte last, ref Utf8SliceInvocation invocation,
        bool checkExitDeadline = true)
    {
        var position = start;
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
            var block = input.Slice(position, Math.Min(2048, input.Length - position));
            var stop = block.IndexOfAnyExceptInRange(first, last);
            if (stop >= 0) { position += stop; break; }
            position += block.Length;
        }
        var terminal = ChargeRepeatBoundary(ref invocation, checkExitDeadline);
        return terminal == Utf8ExecutionLimitKind.None ? (position, terminal) : (-1, terminal);
    }

    private static bool StartsWithFourAsciiDigits(ReadOnlySpan<byte> input, int start) =>
        input.Length - start >= 4 &&
        (uint)(input[start] - '0') <= 9 && (uint)(input[start + 1] - '0') <= 9 &&
        (uint)(input[start + 2] - '0') <= 9 && (uint)(input[start + 3] - '0') <= 9;

    private static (int EndIndex, Utf8ExecutionLimitKind Breach) RunUnboundedDecimalDigits(
        ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation, bool checkExitDeadline)
    {
        var position = start;
        while (position < input.Length)
        {
            if (StartsWithFourAsciiDigits(input, position))
            {
                // The block already rejects an ASCII stop. A wider stop can
                // still belong to the Unicode decimal class.
                var ascii = RunUnboundedAsciiRange(input, position, (byte)'0', (byte)'9', ref invocation,
                    checkExitDeadline: false);
                if (ascii.Breach != Utf8ExecutionLimitKind.None) return ascii;
                position = ascii.EndIndex;
                if (position == input.Length || input[position] < 128) break;
            }

            var breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
            var value = input[position];
            if ((uint)(value - '0') <= 9)
            {
                position++;
                continue;
            }
            if (value < 128 || !TryReadScalar(input, position, false, out var scalar, out var width) || !Rune.IsDigit(scalar)) break;
            position += width;
        }
        var terminal = ChargeRepeatBoundary(ref invocation, checkExitDeadline);
        return terminal == Utf8ExecutionLimitKind.None ? (position, terminal) : (-1, terminal);
    }

    private static (int AtomIndex, bool Guarded) GetCompoundRepeatAtom(Utf8SliceProgram program, int repeatIndex,
        bool omitCaptures)
    {
        var code = program.Instructions;
        var repeat = code[repeatIndex];
        var exit = repeat.A;
        if (program.RightToLeft || exit <= repeatIndex + 3 || exit >= code.Length - 1 ||
            code[exit] is not { Kind: Utf8SliceInstructionKind.RepeatExit } clear || clear.A != repeat.B ||
            code[exit - 1] is not { Kind: Utf8SliceInstructionKind.RepeatEnd } loop || loop.A != repeatIndex)
            return (-1, false);

        var atomIndex = exit - 2;
        var guard = code[repeatIndex + 1];
        if (omitCaptures && exit - repeatIndex == 5 && guard.Kind == Utf8SliceInstructionKind.CaptureStart &&
            code[exit - 2] is { Kind: Utf8SliceInstructionKind.CaptureEnd } close && close.A == guard.A)
        {
            // Empty capture slots mean these ordinary markers have no observer.
            // The body consumes one scalar, with no other effects or alternatives.
            var atom = code[repeatIndex + 2];
            if (atom.Kind == Utf8SliceInstructionKind.Dot || CanRepeatScalarAtom(program, atom))
                return (repeatIndex + 2, false);
        }

        // The prepared lookahead has no capture or rollback effects. A
        // successful iteration advances by exactly the following scalar.
        if (guard is not { Kind: Utf8SliceInstructionKind.Fork, B: 1, C: > 0 } || guard.A + guard.D != atomIndex)
            return (-1, false);
        return code[atomIndex].Kind is Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Set or
            Utf8SliceInstructionKind.Shorthand or Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid ||
            code[atomIndex] is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } ? (atomIndex, true) : (-1, false);
    }

    private static (int EndIndex, Utf8ExecutionLimitKind Breach) RunGuardedClassRepeat(Utf8SliceProgram program,
        int repeatIndex, int atomIndex, ReadOnlySpan<byte> input, int start, uint assertionDepth,
        ref Utf8SliceInvocation invocation, out int minimumEnd)
    {
        var code = program.Instructions;
        var repeat = code[repeatIndex];
        var guard = code[repeatIndex + 1];
        var atom = code[atomIndex];
        var minimum = repeat.RepeatMinimum;
        var maximum = repeat.RepeatMaximum;
        var position = start;
        var count = 0;
        minimumEnd = start;
        while (count < maximum)
        {
            // Once the minimum is met, one logical rewind remains available
            // while the next scalar's assertion is being evaluated.
            var depth = count >= minimum ? assertionDepth + 1 : assertionDepth;
            var breach = TryMatchInlineLiteralAssertion(code, repeatIndex + 2, guard.A - 2, guard.C - 1,
                input, position, false, null, depth, ref invocation, out var matched);
            if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
            if (matched != (guard.D != 0)) break;
            breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
            if (!TryReadScalar(input, position, false, out var scalar, out var width)) break;
            breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var hit);
            if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
            if (!hit) break;
            position += width;
            if (++count == minimum) minimumEnd = position;
        }
        var terminal = ChargeRepeatBoundary(ref invocation, checkDeadline: true);
        return terminal == Utf8ExecutionLimitKind.None && count >= minimum ? (position, terminal) : (-1, terminal);
    }

    // A side-effect-free atom repeat has only an endpoint and resource status.
    // Capture/history owners stay at the caller; a negative endpoint is a miss.
    private static (int EndIndex, Utf8ExecutionLimitKind Breach) RunClassRepeat(Utf8SliceProgram program, int repeatIndex,
        ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation, bool minimumOnly = false,
        bool checkExitDeadline = true)
        => RunClassRepeat(program, repeatIndex, input, start, ref invocation, out _, minimumOnly, checkExitDeadline);

    private static (int EndIndex, Utf8ExecutionLimitKind Breach) RunClassRepeat(Utf8SliceProgram program, int repeatIndex,
        ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation, out int minimumEnd,
        bool minimumOnly = false, bool checkExitDeadline = true)
    {
        var code = program.Instructions;
        return RunClassRepeat(program, code[repeatIndex], code[repeatIndex + 1], input, start, ref invocation,
            out minimumEnd, minimumOnly, checkExitDeadline);
    }

    private static (int EndIndex, Utf8ExecutionLimitKind Breach) RunClassRepeat(Utf8SliceProgram program,
        in Utf8SliceInstruction repeat, in Utf8SliceInstruction atom, ReadOnlySpan<byte> input, int start,
        ref Utf8SliceInvocation invocation, out int minimumEnd, bool minimumOnly = false, bool checkExitDeadline = true)
    {
        var minimum = repeat.RepeatMinimum;
        var maximum = minimumOnly ? minimum : repeat.RepeatMaximum;
        var dot = atom.Kind == Utf8SliceInstructionKind.Dot;
        var unbounded = minimum <= 1 && maximum == long.MaxValue;
        if (unbounded && TryGetAsciiRepeatStops(program, atom, out var first, out var second))
            return RunUnboundedByteStops(input, start, minimum != 0, dot && atom.A != 0, first, second,
                ref invocation, out minimumEnd, checkExitDeadline);

        if (maximum is >= 2 and <= 16 && atom is { B: 0, C: 0 } &&
            (atom.Kind == Utf8SliceInstructionKind.Set || maximum <= 4 && atom.Kind == Utf8SliceInstructionKind.CategorySet))
        {
            var window = input.Slice(start, Math.Min(input.Length - start, (int)maximum));
            var category = atom.Kind == Utf8SliceInstructionKind.CategorySet;
            var consumed = ScanAsciiMask(window, program.SetMasks[atom.A], category);
            if (consumed >= 0 && (!category || program.GetCompleteAsciiCategoryMask(atom) != 0))
            {
                // The span probe reads at most sixteen bytes. Charge it before
                // publishing a result, and keep the repeat-boundary check.
                minimumEnd = start;
                var breach = invocation.TryChargeStep();
                if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
                breach = ChargeRepeatBoundary(ref invocation, checkExitDeadline);
                if (breach != Utf8ExecutionLimitKind.None || consumed < minimum) return (-1, breach);
                minimumEnd = start + (int)minimum;
                return (start + consumed, Utf8ExecutionLimitKind.None);
            }
        }

        minimumEnd = start;
        var position = start;
        var count = 0;
        var unboundedAsciiSet = unbounded && atom is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 };
        byte asciiLast = 0;
        var asciiFirst = unboundedAsciiSet ? GetUnfoldedAsciiRange(atom, out asciiLast) : -1;
        if (asciiFirst >= 0)
        {
            var ascii = RunUnboundedAsciiRange(input, start, (byte)asciiFirst, asciiLast, ref invocation, checkExitDeadline);
            if (minimum != 0 && ascii.EndIndex > start) minimumEnd = start + 1;
            return ascii.EndIndex == start && minimum != 0 ? (-1, ascii.Breach) : ascii;
        }
        if (unbounded && atom.Kind == Utf8SliceInstructionKind.CategorySet &&
            (input.Length - start >= 64 || StartsWithFourAsciiDigits(input, start)) && IsDecimalDigitSet(program, atom))
        {
            var digits = RunUnboundedDecimalDigits(input, start, ref invocation, checkExitDeadline);
            if (minimum != 0 && digits.EndIndex > start) minimumEnd = AdvancePastScalar(input, start, reverse: false);
            return digits.EndIndex == start && minimum != 0 ? (-1, digits.Breach) : digits;
        }
        // Lowering includes every shorthand's ASCII members in an unfolded
        // Hybrid mask. Its shorthand bits still classify non-ASCII scalars.
        var hybrid = atom is { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 };
        var asciiNegated = hybrid && atom.B != 0;
        ReadOnlySpan<byte> exactAsciiMask = unboundedAsciiSet || unbounded && hybrid ? program.SetMasks[atom.A] : [];
        var asciiWord = unboundedAsciiSet && IsAsciiWordMask(exactAsciiMask);
        if (unbounded && (unboundedAsciiSet || hybrid))
        {
            var blockSize = asciiWord ? 2048 : 64;
            // The mask walk stops at any wider scalar. Hybrid repeats test it
            // before resuming the next bounded block.
            while (position < input.Length)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
                var block = input.Slice(position, Math.Min(blockSize, input.Length - position));
                var stop = asciiWord ? Utf8AsciiBytePredicates.WordSearch.IndexOfAnyExcept(block) :
                    ScanAsciiMask(block, exactAsciiMask, category: false, asciiNegated);
                if ((uint)stop >= (uint)block.Length)
                {
                    position += block.Length;
                    continue;
                }
                position += stop;
                if (!hybrid || input[position] < 128 || !TryReadScalar(input, position, false, out var scalar, out var width))
                    break;
                breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var hit);
                if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
                if (!hit) break;
                position += width;
            }
            var terminalBreach = ChargeRepeatBoundary(ref invocation, checkExitDeadline);
            if (terminalBreach != Utf8ExecutionLimitKind.None) return (-1, terminalBreach);
            // With no maximum and minimum at most one, nonempty byte extent
            // suffices; bounded/scalar-counted repeats keep the normal loop.
            if (minimum != 0 && position > start)
                minimumEnd = unboundedAsciiSet ? start + 1 : AdvancePastScalar(input, start, reverse: false);
            return position != start || minimum == 0
                ? (position, Utf8ExecutionLimitKind.None)
                : (-1, Utf8ExecutionLimitKind.None);
        }
        var completeMask = hybrid || atom.Kind == Utf8SliceInstructionKind.Set;
        ReadOnlySpan<byte> asciiMask = hybrid ||
            atom.Kind is Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.CategorySet &&
            atom.B == 0 && atom.C == 0 ? program.SetMasks[atom.A] : [];
        while (count < maximum)
        {
            var breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
            // Exact ASCII masks also reject a miss without scalar dispatch.
            if (!asciiMask.IsEmpty && (uint)position < (uint)input.Length && input[position] < 128)
            {
                var value = input[position];
                var member = (asciiMask[value >> 3] & (1 << (value & 7))) != 0;
                if (member != asciiNegated)
                {
                    position++;
                    if (++count == minimum) minimumEnd = position;
                    continue;
                }
                if (completeMask || program.GetCompleteAsciiCategoryMask(atom) != 0)
                    break;
            }
            if (!TryReadScalar(input, position, false, out var scalar, out var width)) break;
            if (dot)
            {
                if (atom.A == 0 && scalar.Value == '\n') break;
            }
            else
            {
                breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var hit);
                if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
                if (!hit) break;
            }
            position += width;
            if (++count == minimum) minimumEnd = position;
        }
        var terminal = ChargeRepeatBoundary(ref invocation, checkExitDeadline);
        if (terminal != Utf8ExecutionLimitKind.None) return (-1, terminal);
        return count >= minimum ? (position, Utf8ExecutionLimitKind.None)
            : (-1, Utf8ExecutionLimitKind.None);
    }

    private static (int EndIndex, Utf8ExecutionLimitKind Breach) RunUnboundedByteStops(ReadOnlySpan<byte> input,
        int start, bool requiresScalar, bool dotAll, byte first, byte second, ref Utf8SliceInvocation invocation,
        out int minimumEnd, bool checkExitDeadline)
    {
        minimumEnd = start;
        var position = start;
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return (-1, breach);
            var length = Math.Min(2048, input.Length - position);
            // A block can split a scalar. Only an ASCII stop or the subject
            // end determines the run's endpoint.
            var stop = IndexOfRepeatStop(input.Slice(position, length), dotAll, first, second);
            if (stop >= 0) { position += stop; break; }
            position += length;
        }
        var terminal = ChargeRepeatBoundary(ref invocation, checkExitDeadline);
        if (terminal != Utf8ExecutionLimitKind.None || requiresScalar && position == start) return (-1, terminal);
        if (requiresScalar) minimumEnd = AdvancePastScalar(input, start, reverse: false);
        return (position, Utf8ExecutionLimitKind.None);
    }

    private static int ScanAsciiMask(ReadOnlySpan<byte> window, ReadOnlySpan<byte> mask, bool category, bool negated = false)
    {
        var membership = Utf8SearchAsciiSet.FromMasks(BinaryPrimitives.ReadUInt64LittleEndian(mask),
            BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]));
        var consumed = 0;
        foreach (var value in window)
        {
            // A wider candidate still needs the scalar predicate. An earlier
            // ASCII miss already ends a repeat with a complete ASCII mask.
            if (value >= 128) return category ? -1 : consumed;
            if (membership.Contains(value) == negated) break;
            consumed++;
        }
        return consumed;
    }

    private static Utf8ExecutionLimitKind ChargeRepeatBoundary(ref Utf8SliceInvocation invocation, bool checkDeadline)
    {
        var breach = invocation.TryChargeStep();
        if (checkDeadline || breach != Utf8ExecutionLimitKind.None) invocation.Budget.CheckDeadline();
        return breach;
    }

    private static bool RejectsAdjacentHybridRuns(Utf8SliceProgram program, in Utf8SliceInstruction first,
        in Utf8SliceInstruction second, ReadOnlySpan<byte> input, int start)
    {
        // The caller charges this bounded proof and establishes that both runs
        // are nonempty with exact ASCII masks. The second scalar belongs to one of them.
        if (input.Length - start < 2) return true;
        var value = input[start + 1];
        if (value >= 128 || input[start] >= 128) return false;
        var octet = value >> 3;
        var bit = 1 << (value & 7);
        var firstMember = (program.SetMasks[first.A][octet] & bit) != 0;
        var secondMember = (program.SetMasks[second.A][octet] & bit) != 0;
        return firstMember == (first.B != 0) && secondMember == (second.B != 0);
    }

    private static bool TryRunTerminalHybridMinimum(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int index, int position, ref Utf8SliceInvocation invocation, out (int EndIndex, Utf8ExecutionLimitKind Breach) result)
    {
        result = default;
        var instructions = program.Instructions.AsSpan();
        if (position == input.Length || (uint)index >= (uint)instructions.Length) return false;
        var repeat = instructions[index];
        if (repeat is not { Kind: Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy, RepeatMinimum: 1 } ||
            !IsSingleAtomRepeat(instructions, index, repeat) ||
            instructions[index + 1] is not { Kind: Utf8SliceInstructionKind.Hybrid, D: 0 } ||
            !CanAcceptEmptyTail(instructions, repeat.A + 1)) return false;

        // At the preferred endpoint one more scalar can prove existence.
        // Keep the normal repeat entry, scalar and boundary charges, and leave
        // Accept to the caller. A failed probe enters the original rewind path
        // directly because the next repeat cannot meet its minimum here.
        var breach = invocation.TryChargeStep();
        result = breach == Utf8ExecutionLimitKind.None
            ? RunClassRepeat(program, index, input, position, ref invocation, minimumOnly: true)
            : (-1, breach);
        return true;
    }

    private static bool CanAcceptEmptyTail(ReadOnlySpan<Utf8SliceInstruction> code, int index)
    {
        for (var remaining = 32; remaining > 0 && (uint)index < (uint)code.Length; remaining--)
        {
            var entry = code[index];
            if (entry.Kind == Utf8SliceInstructionKind.Accept) return index == code.Length - 1;
            if (entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                index++;
            // A normal forward alternative needs only one empty path. The
            // target is the skip edge of a lowered greedy optional group.
            else if (entry.A > index && entry is { Kind: Utf8SliceInstructionKind.Jump } or
                         { Kind: Utf8SliceInstructionKind.Fork, B: 0 })
                index = entry.A;
            else if (entry.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy &&
                entry.RepeatMinimum == 0 && entry.A > index && entry.A < code.Length &&
                code[entry.A] is { Kind: Utf8SliceInstructionKind.RepeatExit } exit && exit.A == entry.B)
                index = entry.A + 1;
            else return false;
        }
        return false;
    }

    private static int IndexOfRepeatStop(ReadOnlySpan<byte> input, bool dotAll, byte first, byte second)
        => dotAll ? -1 : first == second ? input.IndexOf(first) : input.IndexOfAny(first, second);

    private static bool TryGetAsciiRepeatStops(Utf8SliceProgram program, in Utf8SliceInstruction atom,
        out byte first, out byte second)
    {
        if (atom is { Kind: Utf8SliceInstructionKind.Set, B: 1, C: 0 })
            return TryGetOneOrTwoAsciiBytes(program.SetMasks[atom.A], out first, out second);
        first = second = (byte)'\n';
        return atom.Kind == Utf8SliceInstructionKind.Dot;
    }

    private static bool IsAsciiWordMask(ReadOnlySpan<byte> mask)
        => BinaryPrimitives.ReadUInt64LittleEndian(mask) == Utf8AsciiBytePredicates.WordSearch.Mask0 &&
            BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]) == Utf8AsciiBytePredicates.WordSearch.Mask1;

    private static bool TryGetOneOrTwoAsciiBytes(ReadOnlySpan<byte> mask, out byte first, out byte second)
    {
        first = second = 0;
        var low = BinaryPrimitives.ReadUInt64LittleEndian(mask);
        var high = BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]);
        var size = BitOperations.PopCount(low) + BitOperations.PopCount(high);
        if (size is < 1 or > 2) return false;
        first = (byte)(low != 0 ? BitOperations.TrailingZeroCount(low) : 64 + BitOperations.TrailingZeroCount(high));
        if (size == 1) { second = first; return true; }
        if (low != 0) low &= low - 1;
        else high &= high - 1;
        second = (byte)(low != 0 ? BitOperations.TrailingZeroCount(low) : 64 + BitOperations.TrailingZeroCount(high));
        return true;
    }

    private static Utf8ExecutionLimitKind TryMatchOptionalScalar(Utf8SliceProgram program,
        in Utf8SliceInstruction atom, ReadOnlySpan<byte> input, int start,
        ref Utf8SliceInvocation invocation, out int width)
    {
        width = 0;
        var breach = invocation.TryChargeStep();
        if (breach == Utf8ExecutionLimitKind.None &&
            TryReadScalar(input, start, false, out var scalar, out var scalarWidth))
        {
            breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var hit);
            if (hit) width = scalarWidth;
        }
        return breach;
    }

    private static Utf8ExecutionLimitKind TryMatchScalarClass(Utf8SliceProgram program,
        in Utf8SliceInstruction instruction, Rune scalar, ref Utf8SliceInvocation invocation, out bool hit)
    {
        if (instruction.Kind == Utf8SliceInstructionKind.Literal)
        {
            hit = scalar.Value == instruction.A;
            return Utf8ExecutionLimitKind.None;
        }
        if (instruction.Kind == Utf8SliceInstructionKind.Dot)
        {
            hit = instruction.A != 0 || scalar.Value != '\n';
            return Utf8ExecutionLimitKind.None;
        }
        if (instruction.Kind == Utf8SliceInstructionKind.Set)
        {
            hit = Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[instruction.A],
                instruction.C != 0, program.CaseBehavior) != (instruction.B != 0);
            return Utf8ExecutionLimitKind.None;
        }
        if (instruction.Kind == Utf8SliceInstructionKind.Shorthand)
        {
            hit = Utf8SliceScalarClasses.MatchesShorthand(scalar, (Utf8SliceShorthand)instruction.A);
            return Utf8ExecutionLimitKind.None;
        }
        var categorySet = instruction.Kind == Utf8SliceInstructionKind.CategorySet;
        var members = categorySet ? program.CategorySetMembers[instruction.D] : null;
        if (members is { Length: 1 } && members[0].Table.ClassProgram is { } classProgram)
        {
            hit = classProgram.TryMatch(scalar, program.CaseBehavior, ref invocation, out var breach);
            return breach;
        }
        var fold = categorySet ? (instruction.B & 2) != 0 : instruction.D != 0;
        var memberHit = Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[instruction.A], fold, program.CaseBehavior) ||
            Utf8SliceScalarClasses.MatchesMembers(scalar, instruction.C);
        if (!memberHit && members is not null)
        {
            memberHit = Utf8SliceScalarClasses.MatchesTables(scalar, members, fold, program.CaseBehavior,
                ref invocation, out var breach);
            if (breach != Utf8ExecutionLimitKind.None) { hit = false; return breach; }
        }
        hit = memberHit != ((instruction.B & 1) != 0);
        return Utf8ExecutionLimitKind.None;
    }
}
