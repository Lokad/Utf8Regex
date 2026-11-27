using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

// Emit a bounded forward program for an origin admitted by the caller.
// Retry targets are fixed during emission. A repeat with one viable endpoint
// can leave its offset in a local without changing the offset at a retry.
internal static class Utf8BoundedProgramEmitter
{
    internal delegate int Match(ReadOnlySpan<byte> input, Span<Utf8CaptureRange> captures = default);
    internal enum Compilation : byte { None, Eager, EagerOrTiered, Tiered, TieredBranches, TieredLiteralExistence }

    private const int MaxUnsplitInstructions = 64;
    private const int MaxExpandedVisits = Utf8SliceRunner.BranchedPrefixInstructionLimit;
    private const int Unsupported = MaxExpandedVisits + 1;
    // Only the tiered absolute-origin span proof can reach its scan limit.
    internal const int Incomplete = -2;
    private const int DotRun = 256;
    internal const int MaxHistoryCaptures = 8;
    private static readonly MethodInfo s_spanLength = typeof(ReadOnlySpan<byte>).GetProperty(nameof(ReadOnlySpan<byte>.Length))!.GetMethod!;
    private static readonly MethodInfo s_spanItem = typeof(ReadOnlySpan<byte>).GetProperty("Item")!.GetMethod!;
    private static readonly MethodInfo s_matchDecimalRun = typeof(Utf8SliceRunner).GetMethod(
        nameof(Utf8SliceRunner.MatchBoundedDecimalRun), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo s_matchSpanRun = typeof(Utf8BoundedProgramEmitter).GetMethod(
        nameof(MatchSpanRun), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo s_matchMaskSpanRun = typeof(Utf8BoundedProgramEmitter).GetMethod(
        nameof(MatchMaskSpanRun), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo s_canExitDelimitedLoop = typeof(Utf8BoundedProgramEmitter).GetMethod(
        nameof(CanExitDelimitedLoop), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo s_captureLength = typeof(Span<Utf8CaptureRange>).GetProperty(nameof(Span<Utf8CaptureRange>.Length))!.GetMethod!;
    private static readonly MethodInfo s_storeCapture = typeof(Utf8BoundedProgramEmitter).GetMethod(
        nameof(StoreCapture), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static void StoreCapture(Span<Utf8CaptureRange> captures, int index, int start, int end)
        => captures[index] = new(start, end);

    internal static int[]? PrepareHistoryOffsets(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        // The emitted history has a fixed shape only when every capture
        // closes on the sole path and none of the repeats retries it.
        if (program.GroupCount is 0 or > MaxHistoryCaptures || !TryGetEntry(program, out var entry) || entry != 0 ||
            !program.HasShortDecimalHead || !Utf8SliceRunner.TryGetFixedDecimalFollower(program, out _, out _, out _)) return null;

        var code = program.Instructions;
        Span<int> counts = stackalloc int[MaxHistoryCaptures];
        counts.Clear();
        var opened = 0;
        var captures = 0;
        budget.Checkpoint();
        for (var pc = 0; pc < code.Length - 1; pc++)
        {
            ref readonly var instruction = ref code[pc];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.CaptureStart:
                    var bit = 1 << instruction.A;
                    if ((opened & bit) != 0) return null;
                    opened |= bit;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    bit = 1 << instruction.A;
                    if ((opened & bit) == 0 || ++captures > MaxHistoryCaptures) return null;
                    opened &= ~bit;
                    counts[instruction.A]++;
                    break;
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                    if (!instruction.HasSingleAtomBody || instruction.A + 1 >= code.Length) return null;
                    if (code[pc + 1].IsDecimalDigitSet)
                    {
                        if (!CanEmitDecimalRepeat(program, pc, instruction, shortOrigin: false)) return null;
                    }
                    else if (instruction.RepeatMinimum != instruction.RepeatMaximum || !IsAsciiAtom(program, code[pc + 1]))
                        return null;
                    pc = instruction.A;
                    break;
                case Utf8SliceInstructionKind.Literal:
                case Utf8SliceInstructionKind.Set:
                    if (!IsAsciiAtom(program, instruction)) return null;
                    break;
                default:
                    return null;
            }
        }
        if (opened != 0 || captures == 0 || code[^1].Kind != Utf8SliceInstructionKind.Accept ||
            CountExpandedVisits(program, entry, code.Length, budget) > MaxExpandedVisits) return null;

        var offsets = budget.AllocateArray<int>(program.GroupCount + 2, false);
        var next = 1;
        for (var group = 0; group < program.GroupCount; group++)
        {
            offsets[group + 1] = next;
            next += counts[group];
        }
        offsets[^1] = next;
        return offsets;
    }

    internal static bool CanTierUp(Utf8SliceProgram program, Utf8ConstructionBudget budget,
        Compilation compilation = Compilation.Tiered)
    {
        if (!TryGetEntry(program, out var entry)) return false;
        if (compilation == Compilation.TieredLiteralExistence)
        {
            // Capture tags and branch priority cannot affect existence in
            // this finite literal graph. Reject profile-specific atom and
            // anchor behavior before admitting the shared runner.
            if (entry != 0 || program.CanMatchEmpty || program.LeadingByteCandidate is not { FromBranch: true })
                return false;
            budget.Checkpoint();
            foreach (var instruction in program.Instructions)
                if (instruction is not ({ Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } or
                    { Kind: Utf8SliceInstructionKind.Fork or Utf8SliceInstructionKind.Jump, B: 0 } or
                    { Kind: Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
                        Utf8SliceInstructionKind.Accept or Utf8SliceInstructionKind.Fail }))
                    return false;
        }
        var code = program.Instructions;
        if (compilation == Compilation.EagerOrTiered)
            return HasLongAbsoluteOrigin(code) && code.Length <= MaxUnsplitInstructions &&
                CountExpandedVisits(program, entry, code.Length, budget, spanRuns: true) <= MaxExpandedVisits;
        if (code[0].Kind == Utf8SliceInstructionKind.Anchor)
            return entry == 1 && code[0].D == 1 && code.Length <= MaxUnsplitInstructions &&
                CountExpandedVisits(program, entry, code.Length, budget) <= MaxExpandedVisits;
        Span<int> splits = stackalloc int[MaxExpandedVisits];
        return TryPlan(program, entry, budget, splits, out _);
    }

    internal static Match? TryEmit(Utf8SliceProgram program, Utf8ConstructionBudget budget, bool allowSpanRuns = false)
    {
        var code = program.Instructions;
        if (!TryGetEntry(program, out var entry)) return null;
        var spanRuns = allowSpanRuns && HasLongAbsoluteOrigin(code);
        Span<int> splits = stackalloc int[MaxExpandedVisits];
        if (!TryPlan(program, entry, budget, splits, out var splitCount, spanRuns)) return null;

        using var method = Utf8DynamicIlBuilder.Create(entry != 0 ? "Utf8Regex_AnchoredProgram" : "Utf8Regex_CandidateProgram",
            typeof(int), typeof(Utf8BoundedProgramEmitter), typeof(ReadOnlySpan<byte>), typeof(Span<Utf8CaptureRange>));
        var il = new Utf8IlEmitter(method, s_spanLength, s_spanItem, inputArgument: 0);
        var layout = program.EmittedHistoryOffsets;
        var captures = layout is null ? null : il.AllocateScratch<(Utf8IlLocal Start, Utf8IlLocal End)>(layout[^1] - 1);
        var openings = layout is null ? null : il.AllocateScratch<Utf8IlLocal>(program.GroupCount);
        var nextCapture = layout is null ? null : il.AllocateScratch<int>(program.GroupCount);
        if (layout is not null) layout.AsSpan(1, program.GroupCount).CopyTo(nextCapture);
        var length = il.DeclareLocal<int>();
        var value = il.DeclareLocal<int>();
        il.LoadInputLength();
        il.StoreLocal(length);
        var fail = il.DefineLabel();
        Utf8IlLocal? scanned = null;
        Utf8IlLabel? incomplete = null;
        Utf8IlLocal? start = null;
        foreach (var split in splits[..splitCount])
        {
            var next = il.DefineLabel();
            var end = il.DeclareLocal<int>();
            EmitPath(entry, start, 0, fail, (split, end, next));
            il.MarkLabel(next);
            entry = split;
            start = end;
        }
        EmitPath(entry, start, 0, fail);
        il.MarkLabel(fail);
        il.LdcI4(-1);
        il.Emit(OpCodes.Ret);
        if (incomplete is { } exhausted)
        {
            il.MarkLabel(exhausted);
            il.LdcI4(Incomplete);
            il.Emit(OpCodes.Ret);
        }
        return method.CreateDelegate<Match>().Callback;

        void EmitPath(int pc, Utf8IlLocal? origin, int offset, Utf8IlLabel retry,
            (int Index, Utf8IlLocal Position, Utf8IlLabel Label) continuation = default)
        {
            budget.EnterDepth();
            try
            {
                while (true)
                {
                    budget.Checkpoint();
                    if (continuation.Index != 0 && continuation.Index == pc)
                    {
                        LoadPosition(origin, offset);
                        il.StoreLocal(continuation.Position);
                        il.Emit(OpCodes.Br, continuation.Label);
                        return;
                    }
                    ref readonly var instruction = ref code[pc++];
                    switch (instruction.Kind)
                    {
                        case Utf8SliceInstructionKind.Literal:
                        case Utf8SliceInstructionKind.Set:
                            var runStart = pc - 1;
                            // A straight run of byte tests shares a retry. Admit it
                            // before the first checked span index.
                            while (pc < code.Length && pc != continuation.Index && code[pc].Kind is
                                Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set)
                            {
                                budget.Checkpoint();
                                pc++;
                            }
                            EmitByteLimit(origin, offset + pc - runStart, retry);
                            for (var atom = runStart; atom < pc; atom++)
                                EmitMember(code[atom], origin, offset++, retry);
                            break;
                        case Utf8SliceInstructionKind.CaptureStart:
                            if (openings is not null) openings[instruction.A] = SavePosition(origin, offset);
                            break;
                        case Utf8SliceInstructionKind.CaptureEnd:
                            if (captures is not null)
                                captures[nextCapture![instruction.A]++ - 1] = (openings![instruction.A], SavePosition(origin, offset));
                            break;
                        case Utf8SliceInstructionKind.Jump:
                            pc = instruction.A;
                            break;
                        case Utf8SliceInstructionKind.Fork:
                            if (spanRuns && DelimitedBackEdgeByte(code, pc - 1, instruction) is >= 0 and var delimiter)
                            {
                                // A further iteration must consume its delimiter. Keep that
                                // greedy choice with the interpreter when a delimiter remains.
                                il.LoadArg(0);
                                LoadPosition(origin, offset);
                                il.LdcI4(delimiter);
                                LoadScanCounter();
                                il.Emit(OpCodes.Call, s_canExitDelimitedLoop);
                                il.Emit(OpCodes.Brfalse, incomplete!.Value);
                                pc = instruction.A;
                                break;
                            }
                            var second = il.DefineLabel();
                            EmitPath(pc, origin, offset, second, continuation);
                            il.MarkLabel(second);
                            pc = instruction.A;
                            break;
                        case Utf8SliceInstructionKind.RepeatGreedy:
                        case Utf8SliceInstructionKind.RepeatLazy:
                            var min = (int)instruction.RepeatMinimum;
                            var max = EmittedRepeatMaximum(instruction);
                            if (spanRuns && CanEmitSpanRun(program, pc - 1, instruction))
                            {
                                ref readonly var atom = ref code[pc];
                                var first = Utf8SliceRunner.GetUnfoldedAsciiRange(atom, out var last);
                                if (atom.Kind == Utf8SliceInstructionKind.Dot)
                                    origin = EmitSpanRun(origin, offset, min, DotRun, 0, retry);
                                else if (first >= 0 && instruction.RepeatMaximum == long.MaxValue)
                                    origin = EmitSpanRun(origin, offset, min, first, last, retry);
                                else
                                {
                                    _ = program.TryGetUnfoldedAsciiSet(atom, out var mask, out _);
                                    var maximum = instruction.RepeatMaximum == long.MaxValue ? int.MaxValue : max;
                                    origin = EmitSpanRun(origin, offset, min, -1, 0, retry, maximum, mask);
                                }
                                offset = 0;
                                pc = instruction.A + 1;
                                break;
                            }
                            if (code[pc].IsDecimalDigitSet)
                            {
                                var end = il.DeclareLocal<int>();
                                il.LoadArg(0);
                                LoadPosition(origin, offset);
                                il.LdcI4(min);
                                il.LdcI4(max);
                                il.Emit(OpCodes.Call, s_matchDecimalRun);
                                il.StoreLocal(end);
                                il.LoadLocal(end);
                                il.LdcI4(0);
                                il.Emit(OpCodes.Blt, retry);
                                origin = end;
                                offset = 0;
                                pc = instruction.A + 1;
                                break;
                            }
                            if (instruction.RepeatMaximum == long.MaxValue)
                            {
                                origin = EmitGreedyOriginRun(code[pc], origin, offset, min, retry);
                                offset = 0;
                                pc = instruction.A + 1;
                                break;
                            }
                            if (min != max && CanSkipRepeatRetries(program, pc - 1, instruction))
                            {
                                origin = EmitSingleEndpointRun(code[pc], origin, offset, min, max, retry);
                                offset = 0;
                                pc = instruction.A + 1;
                                break;
                            }
                            var lazy = instruction.Kind == Utf8SliceInstructionKind.RepeatLazy;
                            for (var attempt = 0; attempt < max - min; attempt++)
                            {
                                var next = il.DefineLabel();
                                var width = lazy ? min + attempt : max - attempt;
                                EmitRun(code[pc], origin, offset, width, next);
                                EmitPath(instruction.A + 1, origin, offset + width, next, continuation);
                                il.MarkLabel(next);
                            }
                            var lastWidth = lazy ? max : min;
                            EmitRun(code[pc], origin, offset, lastWidth, retry);
                            offset += lastWidth;
                            pc = instruction.A + 1;
                            break;
                        case Utf8SliceInstructionKind.Anchor:
                            EmitEnd(origin, offset, instruction.A == (int)Utf8SliceAnchorKind.VeryEnd, retry);
                            break;
                        case Utf8SliceInstructionKind.Accept:
                            if (captures is not null)
                            {
                                var done = il.DefineLabel();
                                method.Emit(OpCodes.Ldarga_S, (byte)1);
                                il.Emit(OpCodes.Call, s_captureLength);
                                il.Emit(OpCodes.Brfalse, done);
                                for (var capture = 0; capture < captures.Length; capture++)
                                {
                                    il.LoadArg(1);
                                    il.LdcI4(capture);
                                    il.LoadLocal(captures[capture].Start);
                                    il.LoadLocal(captures[capture].End);
                                    il.Emit(OpCodes.Call, s_storeCapture);
                                }
                                il.MarkLabel(done);
                            }
                            LoadPosition(origin, offset);
                            il.Emit(OpCodes.Ret);
                            return;
                        case Utf8SliceInstructionKind.Fail:
                            il.Emit(OpCodes.Br, retry);
                            return;
                        default:
                            throw new InvalidOperationException("The verified program contains an unsupported instruction.");
                    }
                }
            }
            finally { budget.LeaveDepth(); }
        }

        Utf8IlLocal SavePosition(Utf8IlLocal? origin, int offset)
        {
            if (origin is { } local && offset == 0) return local;
            var result = il.DeclareLocal<int>();
            LoadPosition(origin, offset);
            il.StoreLocal(result);
            return result;
        }

        void LoadPosition(Utf8IlLocal? origin, int offset)
        {
            if (origin is { } local)
            {
                il.LoadLocal(local);
                if (offset == 0) return;
                il.LdcI4(offset);
                il.Emit(OpCodes.Add);
            }
            else il.LdcI4(offset);
        }

        void LoadByte(Utf8IlLocal? origin, int offset)
        {
            if (origin is { } local) il.LoadInputByte(local, offset);
            else il.LoadInputByte(offset);
        }

        void EmitEnd(Utf8IlLocal? origin, int offset, bool strict, Utf8IlLabel retry)
        {
            il.LoadLocal(length);
            LoadPosition(origin, offset);
            if (strict) { il.Emit(OpCodes.Bne_Un, retry); return; }

            var atEnd = il.DefineLabel();
            il.Emit(OpCodes.Beq, atEnd);
            il.LoadLocal(length);
            LoadPosition(origin, offset + 1);
            il.Emit(OpCodes.Bne_Un, retry);
            LoadByte(origin, offset);
            il.LdcI4('\n');
            il.Emit(OpCodes.Bne_Un, retry);
            il.MarkLabel(atEnd);
        }

        Utf8IlLocal EmitSingleEndpointRun(in Utf8SliceInstruction atom, Utf8IlLocal? origin,
            int offset, int min, int max, Utf8IlLabel retry)
        {
            EmitRun(atom, origin, offset, min, retry);
            // Each shorter endpoint leaves this atom at the start of a
            // continuation proved to reject it. Preserve the old origin on
            // outer retries by assigning the chosen endpoint to a new local.
            var end = il.DeclareLocal<int>();
            var done = il.DefineLabel();
            LoadPosition(origin, offset + min);
            il.StoreLocal(end);
            for (var width = min; width < max; width++)
            {
                EmitRun(atom, origin, offset + width, 1, done);
                LoadPosition(origin, offset + width + 1);
                il.StoreLocal(end);
            }
            il.MarkLabel(done);
            return end;
        }

        Utf8IlLocal EmitSpanRun(Utf8IlLocal? origin, int offset, int minimum, int first, byte last, Utf8IlLabel retry,
            int maximum = 0, Utf8SearchAsciiSet mask = default)
        {
            var end = il.DeclareLocal<int>();
            il.LoadArg(0);
            LoadPosition(origin, offset);
            il.LdcI4(minimum);
            if (first >= 0)
            {
                il.LdcI4(first);
                il.LdcI4(last);
            }
            else
            {
                il.LdcI4(maximum);
                il.LdcI8(mask.Low);
                il.LdcI8(mask.High);
            }
            LoadScanCounter();
            il.Emit(OpCodes.Call, first >= 0 ? s_matchSpanRun : s_matchMaskSpanRun);
            il.StoreLocal(end);
            il.LoadLocal(end);
            il.LdcI4(Incomplete);
            il.Emit(OpCodes.Beq, incomplete!.Value);
            il.LoadLocal(end);
            il.LdcI4(0);
            il.Emit(OpCodes.Blt, retry);
            return end;
        }

        void LoadScanCounter()
        {
            scanned ??= il.DeclareLocal<int>();
            incomplete ??= il.DefineLabel();
            method.Emit(OpCodes.Ldloca, scanned.Value);
        }

        Utf8IlLocal EmitGreedyOriginRun(in Utf8SliceInstruction atom, Utf8IlLocal? origin,
            int offset, int min, Utf8IlLabel retry)
        {
            EmitRun(atom, origin, offset, min, retry);
            // The caller bounds the entire origin suffix. The verified
            // continuation uses the greedy endpoint without repeat retries.
            var end = il.DeclareLocal<int>();
            var scan = il.DefineLabel();
            var done = il.DefineLabel();
            LoadPosition(origin, offset + min);
            il.StoreLocal(end);
            il.MarkLabel(scan);
            il.LoadLocal(end);
            il.LoadLocal(length);
            il.Emit(OpCodes.Bge_Un, done);
            EmitMember(atom, end, 0, done);
            LoadPosition(end, 1);
            il.StoreLocal(end);
            il.Emit(OpCodes.Br, scan);
            il.MarkLabel(done);
            return end;
        }

        void EmitRun(in Utf8SliceInstruction atom, Utf8IlLocal? origin, int offset, int width, Utf8IlLabel retry)
        {
            if (width == 0) return;
            EmitByteLimit(origin, offset + width, retry);
            for (var step = 0; step < width; step++) EmitMember(atom, origin, offset + step, retry);
        }

        void EmitByteLimit(Utf8IlLocal? origin, int end, Utf8IlLabel retry)
        {
            il.LoadLocal(length);
            LoadPosition(origin, end);
            il.Emit(OpCodes.Blt_Un, retry);
        }

        void EmitMember(in Utf8SliceInstruction atom, Utf8IlLocal? origin, int offset, Utf8IlLabel retry)
        {
            LoadByte(origin, offset);
            if (atom.Kind == Utf8SliceInstructionKind.Literal)
            {
                il.LdcI4(atom.A);
                il.Emit(OpCodes.Bne_Un, retry);
                return;
            }
            if (atom.AsciiIntervalWidth != 0)
            {
                il.LdcI4(atom.AsciiIntervalStart);
                il.Emit(OpCodes.Sub);
                il.LdcI4(atom.AsciiIntervalWidth);
                il.Emit(OpCodes.Bge_Un, retry);
                return;
            }

            _ = program.TryGetUnfoldedAsciiSet(atom, out var ascii, out _);
            if (ascii.Low == 0 || ascii.High == 0)
            {
                // One unsigned bound on the relative index covers both the
                // absent half of ASCII and all non-ASCII bytes.
                if (ascii.Low == 0)
                {
                    il.LdcI4(64);
                    il.Emit(OpCodes.Sub);
                }
                il.StoreLocal(value);
                il.LoadLocal(value);
                il.LdcI4(64);
                il.Emit(OpCodes.Bge_Un, retry);
                il.LdcI8(ascii.Low | ascii.High);
                il.LoadLocal(value);
            }
            else
            {
                il.StoreLocal(value);
                il.LoadLocal(value);
                il.LdcI4(128);
                il.Emit(OpCodes.Bge_Un, retry);
                var lower = il.DefineLabel();
                var test = il.DefineLabel();
                il.LoadLocal(value);
                il.LdcI4(64);
                il.Emit(OpCodes.Blt_Un, lower);
                il.LdcI8(ascii.High);
                il.Emit(OpCodes.Br, test);
                il.MarkLabel(lower);
                il.LdcI8(ascii.Low);
                il.MarkLabel(test);
                il.LoadLocal(value);
                il.LdcI4(63);
                il.Emit(OpCodes.And);
            }
            il.Emit(OpCodes.Shr_Un);
            il.LdcI8(1);
            il.Emit(OpCodes.And);
            il.Emit(OpCodes.Brfalse, retry);
        }
    }

    private static bool TryGetEntry(Utf8SliceProgram program, out int entry)
    {
        entry = 0;
        var code = program.Instructions;
        if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled ||
            program.RightToLeft || program.IsNonBacktracking || program.ReadsCaptures ||
            code.Length is < 2 or > MaxExpandedVisits) return false;

        if (code[0] is { Kind: Utf8SliceInstructionKind.Anchor, B: 0 } and
            ({ A: (int)Utf8SliceAnchorKind.Beginning, D: not 0 } or
             { A: (int)Utf8SliceAnchorKind.Contiguous, D: 1 }))
        {
            entry = 1;
            return true;
        }
        return program.HasShortDecimalHead || program.LeadingByteCandidate is { FromBranch: true, First: >= 0 and < 128 };
    }

    private static bool TryPlan(Utf8SliceProgram program, int entry, Utf8ConstructionBudget budget,
        Span<int> splits, out int splitCount, bool spanRuns = false)
    {
        splitCount = 0;
        var length = program.Instructions.Length;
        return length <= MaxUnsplitInstructions && CountExpandedVisits(program, entry, length, budget, spanRuns) <= MaxExpandedVisits ||
            TrySplitAtDelimiters(program, entry, budget, splits, out splitCount, spanRuns);
    }

    private static bool TrySplitAtDelimiters(Utf8SliceProgram program, int entry,
        Utf8ConstructionBudget budget, Span<int> splits, out int count, bool spanRuns)
    {
        var code = program.Instructions;
        var total = 0;
        count = 0;
        while (code[entry] is { Kind: Utf8SliceInstructionKind.Fork, B: 0 })
        {
            var literal = entry == 0 && program.RequiredBranchLiteral.Min != 0
                ? program.RequiredBranchLiteral
                : Utf8SliceRunner.PrepareRequiredBranchLiteral(program, entry);
            if (literal.Min == 0) break;
            // All arms reach the same required literal. If no earlier atom
            // can consume it, a segment can end only at its first occurrence.
            // A failure in the next segment does not need to retry these arms.
            var consumesDelimiter = false;
            for (var pc = entry; pc < literal.Instruction; pc++)
            {
                budget.Checkpoint();
                if (program.TryGetUnfoldedAsciiSet(code[pc], out var bytes, out _) && bytes.Contains(literal.Value))
                {
                    consumesDelimiter = true;
                    break;
                }
            }
            if (consumesDelimiter) break;
            var end = literal.Instruction + 1;
            total += CountExpandedVisits(program, entry, end, budget, spanRuns);
            if (total > MaxExpandedVisits) return false;
            splits[count++] = end;
            entry = end;
        }
        return count != 0 && total + CountExpandedVisits(program, entry, code.Length, budget, spanRuns) <= MaxExpandedVisits;
    }

    private static int CountExpandedVisits(Utf8SliceProgram program, int entry, int end, Utf8ConstructionBudget budget,
        bool spanRuns = false)
    {
        var code = program.Instructions;
        var shortOrigin = code[0] is { Kind: Utf8SliceInstructionKind.Anchor,
            A: (int)Utf8SliceAnchorKind.Beginning, B: 0, D: 1 };
        var shortContiguousOrigin = code[0] is { Kind: Utf8SliceInstructionKind.Anchor,
            A: (int)Utf8SliceAnchorKind.Contiguous, B: 0, D: 1 };
        budget.Checkpoint();
        // Count every expanded continuation, including retries that never
        // run on a successful input. Reject before allocating the IL builder.
        Span<int> visits = stackalloc int[end + 1];
        visits[end] = end == code.Length ? Unsupported : 1;
        for (var pc = end - 1; pc >= entry; pc--)
        {
            ref readonly var instruction = ref code[pc];
            var cost = instruction.Kind switch
            {
                Utf8SliceInstructionKind.Accept or Utf8SliceInstructionKind.Fail => 1,
                Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd => 1 + visits[pc + 1],
                Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set when IsAsciiAtom(program, instruction)
                    => 1 + visits[pc + 1],
                Utf8SliceInstructionKind.Anchor when instruction is { B: 0, A: (int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd }
                    => 1 + visits[pc + 1],
                Utf8SliceInstructionKind.Jump when instruction.B == 0 && instruction.A > pc && instruction.A < end
                    => 1 + visits[instruction.A],
                Utf8SliceInstructionKind.Fork when spanRuns && instruction.A < end &&
                    DelimitedBackEdgeByte(code, pc, instruction) >= 0
                    => 2 + visits[instruction.A],
                Utf8SliceInstructionKind.Fork when instruction.B == 0 && instruction.A > pc && instruction.A < end
                    => 1 + visits[pc + 1] + visits[instruction.A],
                Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy when
                    instruction.HasSingleAtomBody && instruction.A + 1 < end && code[pc + 1].IsDecimalDigitSet &&
                    CanEmitDecimalRepeat(program, pc, instruction, shortOrigin)
                    => 1 + EmittedRepeatMaximum(instruction) + visits[instruction.A + 1],
                Utf8SliceInstructionKind.RepeatGreedy when spanRuns && instruction.A + 1 < end &&
                    CanEmitSpanRun(program, pc, instruction)
                    => 2 + visits[instruction.A + 1],
                // A loop emits its consuming test once. Its runtime work is
                // bounded by the caller's suffix and the expanded paths.
                Utf8SliceInstructionKind.RepeatGreedy when shortContiguousOrigin &&
                    instruction.HasSingleAtomBody && instruction.A + 1 < end &&
                    instruction.RepeatMaximum == long.MaxValue &&
                    instruction.RepeatMinimum <= Utf8SliceRunner.ShortSingleOriginProofInputLimit &&
                    IsAsciiAtom(program, code[pc + 1]) &&
                    (code[instruction.A].RepeatExitAcceptsEmptyTail || CanSkipRepeatRetries(program, pc, instruction))
                    => 2 + (int)instruction.RepeatMinimum + visits[instruction.A + 1],
                // Fixed repeats add byte checks without duplicating the continuation.
                Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy when
                    instruction.HasSingleAtomBody && instruction.A + 1 < end &&
                    (instruction.RepeatMaximum <= 4 || instruction.RepeatMinimum == instruction.RepeatMaximum &&
                        instruction.RepeatMaximum <= MaxExpandedVisits) && IsAsciiAtom(program, code[pc + 1])
                    => CanSkipRepeatRetries(program, pc, instruction)
                        ? 1 + (int)instruction.RepeatMaximum + visits[instruction.A + 1]
                        : RepeatVisits(instruction, visits[instruction.A + 1]),
                _ => Unsupported
            };
            visits[pc] = Math.Min(cost, Unsupported);
        }
        return visits[entry];

        static int RepeatVisits(in Utf8SliceInstruction repeat, int continuation)
        {
            var min = (int)repeat.RepeatMinimum;
            var max = (int)repeat.RepeatMaximum;
            var choices = max - min + 1;
            return 1 + choices * continuation + choices * (min + max) / 2;
        }
    }

    private static int EmittedRepeatMaximum(in Utf8SliceInstruction repeat)
        => repeat.RepeatMaximum == long.MaxValue
            ? Utf8SliceRunner.ShortSingleOriginProofInputLimit
            : (int)repeat.RepeatMaximum;

    private static bool CanEmitDecimalRepeat(Utf8SliceProgram program, int pc, in Utf8SliceInstruction repeat,
        bool shortOrigin)
    {
        if (repeat.RepeatMaximum == long.MaxValue)
        {
            // This entry only receives extents within the single-origin
            // allowance. A longer subject still uses the original matcher.
            return shortOrigin && repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy &&
                repeat.RepeatMinimum <= Utf8SliceRunner.ShortSingleOriginProofInputLimit &&
                program.SkipsTerminalRewind(pc);
        }
        return repeat.RepeatMaximum <= 4 && (repeat.RepeatMinimum == repeat.RepeatMaximum ||
            repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy && repeat.RepeatMinimum > 0 &&
            (program.Instructions[repeat.A + 1].Kind == Utf8SliceInstructionKind.Accept || program.SkipsTerminalRewind(pc)));
    }

    private static bool CanSkipRepeatRetries(Utf8SliceProgram program, int pc, in Utf8SliceInstruction repeat)
        => program.Instructions[repeat.A].RepeatExitRejectsShorterEndpoints ||
           repeat.Kind == Utf8SliceInstructionKind.RepeatGreedy && program.SkipsTerminalRewind(pc);

    private static bool HasLongAbsoluteOrigin(ReadOnlySpan<Utf8SliceInstruction> code)
        => code[0] is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning, B: 0, D: < 0 };

    private static int DelimitedBackEdgeByte(ReadOnlySpan<Utf8SliceInstruction> code, int pc, in Utf8SliceInstruction fork)
        => fork.B == 0 && fork.A == pc + 2 && fork.A < code.Length &&
           code[pc + 1] is { Kind: Utf8SliceInstructionKind.Jump, B: > 0 and <= 128 } jump && jump.A < pc
            ? jump.B - 1 : -1;

    private static bool CanEmitSpanRun(Utf8SliceProgram program, int pc, in Utf8SliceInstruction repeat)
    {
        if (repeat is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, HasSingleAtomBody: true,
            RepeatMaximum: long.MaxValue or (> 4 and <= Utf8SliceRunner.ShortSingleOriginProofInputLimit),
            RepeatMinimum: <= Utf8SliceRunner.ShortSingleOriginProofInputLimit } ||
            !CanSkipRepeatRetries(program, pc, repeat)) return false;

        ref readonly var atom = ref program.Instructions[pc + 1];
        return IsAsciiAtom(program, atom) || atom is { Kind: Utf8SliceInstructionKind.Dot, A: 0 } &&
            repeat.RepeatMaximum == long.MaxValue && repeat.RepeatMinimum <= 1;
    }

    private static int MatchSpanRun(ReadOnlySpan<byte> input, int start, int minimum, int first, byte last, ref int scanned)
    {
        var limit = Utf8SliceRunner.GetLongSingleOriginProofLimit(input.Length);
        if (input.Length > limit) return Incomplete;
        // Retries share one allowance. A clipped suffix can prove a stop,
        // but only the real end can prove that an unlimited run is complete.
        var suffix = input[start..];
        var window = suffix[..Math.Min(suffix.Length, limit - scanned)];
        var stop = first == DotRun ? window.IndexOf((byte)'\n') : window.IndexOfAnyExceptInRange((byte)first, last);
        if (stop < 0 && window.Length < suffix.Length) return Incomplete;
        var consumed = stop < 0 ? window.Length : stop;
        scanned += stop < 0 ? consumed : consumed + 1;
        return consumed < minimum ? -1 : start + consumed;
    }

    private static int MatchMaskSpanRun(ReadOnlySpan<byte> input, int start, int minimum, int maximum,
        ulong low, ulong high, ref int scanned)
    {
        var limit = Utf8SliceRunner.GetLongSingleOriginProofLimit(input.Length);
        if (input.Length > limit) return Incomplete;
        var available = Math.Min(maximum, input.Length - start);
        var window = input.Slice(start, Math.Min(available, limit - scanned));
        Span<byte> mask = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(mask, low);
        BinaryPrimitives.WriteUInt64LittleEndian(mask[8..], high);
        var consumed = Utf8SliceRunner.ScanAsciiMask(window, mask, category: false);
        if (consumed == window.Length && consumed < available) return Incomplete;
        scanned += consumed < window.Length ? consumed + 1 : consumed;
        return consumed < minimum ? -1 : start + consumed;
    }

    private static bool CanExitDelimitedLoop(ReadOnlySpan<byte> input, int start, byte delimiter, ref int scanned)
    {
        var limit = Utf8SliceRunner.GetLongSingleOriginProofLimit(input.Length);
        var remaining = input.Length - start;
        if (input.Length > limit || remaining > limit - scanned) return false;
        scanned += remaining;
        return !input[start..].Contains(delimiter);
    }

    private static bool IsAsciiAtom(Utf8SliceProgram program, in Utf8SliceInstruction instruction)
        => instruction.Kind is Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set &&
           program.TryGetUnfoldedAsciiSet(instruction, out _, out var asciiOnly) && asciiOnly;
}
