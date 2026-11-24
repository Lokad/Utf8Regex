using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

// Emit a bounded forward program for an origin admitted by the caller.
// Retry targets are fixed during emission. A repeat with one viable endpoint
// can leave its offset in a local without changing the offset at a retry.
internal static class Utf8BoundedProgramEmitter
{
    internal delegate int Match(ReadOnlySpan<byte> input);

    private const int MaxUnsplitInstructions = 64;
    private const int MaxExpandedVisits = Utf8SliceRunner.BranchedPrefixInstructionLimit;
    private const int Unsupported = MaxExpandedVisits + 1;
    private static readonly MethodInfo s_spanLength = typeof(ReadOnlySpan<byte>).GetProperty(nameof(ReadOnlySpan<byte>.Length))!.GetMethod!;
    private static readonly MethodInfo s_spanItem = typeof(ReadOnlySpan<byte>).GetProperty("Item")!.GetMethod!;
    private static readonly MethodInfo s_matchDecimalRun = typeof(Utf8SliceRunner).GetMethod(
        nameof(Utf8SliceRunner.MatchBoundedDecimalRun), BindingFlags.Static | BindingFlags.NonPublic)!;

    internal static Match? TryEmit(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        var code = program.Instructions;
        if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled ||
            program.RightToLeft || program.IsNonBacktracking ||
            program.ReadsCaptures || code.Length is < 2 or > MaxExpandedVisits) return null;

        var anchored = code[0] is { Kind: Utf8SliceInstructionKind.Anchor,
            A: (int)Utf8SliceAnchorKind.Beginning, B: 0, D: not 0 };
        if (!anchored && !program.HasShortDecimalHead &&
            program.LeadingByteCandidate is not { FromBranch: true, First: >= 0 and < 128 }) return null;
        var entry = anchored ? 1 : 0;
        Span<int> splits = stackalloc int[MaxExpandedVisits];
        var splitCount = 0;
        if (code.Length > MaxUnsplitInstructions ||
            CountExpandedVisits(program, entry, code.Length, budget) > MaxExpandedVisits)
        {
            if (!TrySplitAtDelimiters(program, entry, budget, splits, out splitCount)) return null;
        }

        using var method = Utf8DynamicIlBuilder.Create(anchored ? "Utf8Regex_AnchoredProgram" : "Utf8Regex_CandidateProgram",
            typeof(int), typeof(Utf8BoundedProgramEmitter), typeof(ReadOnlySpan<byte>));
        var il = new Utf8IlEmitter(method, s_spanLength, s_spanItem, inputArgument: 0);
        var length = il.DeclareLocal<int>();
        var value = il.DeclareLocal<int>();
        il.LoadInputLength();
        il.StoreLocal(length);
        var fail = il.DefineLabel();
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
        return method.CreateDelegate<Match>().Callback;

        void EmitPath(int pc, Utf8IlLocal? origin, int offset, Utf8IlLabel retry,
            (int Index, Utf8IlLocal Position, Utf8IlLabel Label) continuation = default)
        {
            budget.EnterDepth();
            try
            {
                while (true)
                {
                    budget.ChargeWork(1);
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
                            EmitRun(instruction, origin, offset++, 1, retry);
                            break;
                        case Utf8SliceInstructionKind.CaptureStart:
                        case Utf8SliceInstructionKind.CaptureEnd:
                            break;
                        case Utf8SliceInstructionKind.Jump:
                            pc = instruction.A;
                            break;
                        case Utf8SliceInstructionKind.Fork:
                            var second = il.DefineLabel();
                            EmitPath(pc, origin, offset, second, continuation);
                            il.MarkLabel(second);
                            pc = instruction.A;
                            break;
                        case Utf8SliceInstructionKind.RepeatGreedy:
                        case Utf8SliceInstructionKind.RepeatLazy:
                            var min = (int)instruction.RepeatMinimum;
                            var max = (int)instruction.RepeatMaximum;
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
                            if (min != max && code[instruction.A].RepeatExitRejectsShorterEndpoints)
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
                            var last = lazy ? max : min;
                            EmitRun(code[pc], origin, offset, last, retry);
                            offset += last;
                            pc = instruction.A + 1;
                            break;
                        case Utf8SliceInstructionKind.Anchor:
                            EmitEnd(origin, offset, instruction.A == (int)Utf8SliceAnchorKind.VeryEnd, retry);
                            break;
                        case Utf8SliceInstructionKind.Accept:
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

        void EmitRun(in Utf8SliceInstruction atom, Utf8IlLocal? origin, int offset, int width, Utf8IlLabel retry)
        {
            if (width == 0) return;
            il.LoadLocal(length);
            LoadPosition(origin, offset + width);
            il.Emit(OpCodes.Blt_Un, retry);
            for (var step = 0; step < width; step++) EmitMember(atom, origin, offset + step, retry);
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

    private static bool TrySplitAtDelimiters(Utf8SliceProgram program, int entry,
        Utf8ConstructionBudget budget, Span<int> splits, out int count)
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
                budget.ChargeWork(1);
                if (program.TryGetUnfoldedAsciiSet(code[pc], out var bytes, out _) && bytes.Contains(literal.Value))
                {
                    consumesDelimiter = true;
                    break;
                }
            }
            if (consumesDelimiter) break;
            var end = literal.Instruction + 1;
            total += CountExpandedVisits(program, entry, end, budget);
            if (total > MaxExpandedVisits) return false;
            splits[count++] = end;
            entry = end;
        }
        return count != 0 && total + CountExpandedVisits(program, entry, code.Length, budget) <= MaxExpandedVisits;
    }

    private static int CountExpandedVisits(Utf8SliceProgram program, int entry, int end, Utf8ConstructionBudget budget)
    {
        var code = program.Instructions;
        budget.ChargeWork((ulong)(end - entry));
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
                Utf8SliceInstructionKind.Fork when instruction.B == 0 && instruction.A > pc && instruction.A < end
                    => 1 + visits[pc + 1] + visits[instruction.A],
                Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy when
                    instruction.HasSingleAtomBody && instruction.RepeatMaximum <= 4 && instruction.A + 1 < end &&
                    code[pc + 1].IsDecimalDigitSet && (instruction.RepeatMinimum == instruction.RepeatMaximum ||
                        instruction.Kind == Utf8SliceInstructionKind.RepeatGreedy && instruction.RepeatMinimum > 0 &&
                        (code[instruction.A + 1].Kind == Utf8SliceInstructionKind.Accept || program.SkipsTerminalRewind(pc)))
                    => 1 + (int)instruction.RepeatMaximum + visits[instruction.A + 1],
                Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy when
                    instruction.HasSingleAtomBody && instruction.RepeatMaximum <= 4 && instruction.A + 1 < end &&
                    IsAsciiAtom(program, code[pc + 1])
                    => RepeatVisits(instruction, visits[instruction.A + 1]),
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

    private static bool IsAsciiAtom(Utf8SliceProgram program, in Utf8SliceInstruction instruction)
        => instruction.Kind is Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set &&
           program.TryGetUnfoldedAsciiSet(instruction, out _, out var asciiOnly) && asciiOnly;
}
