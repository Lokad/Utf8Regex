namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8ExecutionInterpreter
{
    // Immutable active-node versions let saved choices restore any enclosing
    // group or loop without cloning a continuation stack or losing inner choices.
    private readonly record struct ProgramFrame(int Parent, int Enter, int Start, int Count, uint Depth, bool Stop);
    private readonly record struct ProgramChoice(int Pc, int Index, int Head, Utf8CaptureCheckpoint Checkpoint, bool Repeat);

    internal static bool TryMatchProgram(
        ReadOnlySpan<byte> input, Utf8ExecutionProgram program, int instructionIndex, int startIndex,
        Utf8CaptureSlots? captures, ref Utf8SliceInvocation invocation,
        out int endIndex, out Utf8ExecutionLimitKind breach)
    {
        endIndex = 0;
        breach = Utf8ExecutionLimitKind.None;
        var nodes = new Utf8InvocationStateBuffer<ProgramFrame>();
        var choices = new Utf8InvocationStateBuffer<ProgramChoice>();
        var journaling = false;
        var successful = false;
        var head = -1;
        var pc = instructionIndex;
        var index = startIndex;
        var repeat = false;
        var origin = invocation.AnchorOriginByte < 0 ? startIndex : invocation.AnchorOriginByte;
        try
        {
            if (captures is not null) breach = captures.TryClear(ref invocation);
            if (breach != Utf8ExecutionLimitKind.None || (uint)startIndex > (uint)input.Length ||
                (uint)instructionIndex >= (uint)program.Instructions.Count ||
                program.Instructions[instructionIndex].Kind != Utf8ExecutionInstructionKind.Enter) return false;
            if (captures is not null) { captures.BeginInvocation(); journaling = true; }
            while (true)
            {
                breach = invocation.TryChargeStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                var failed = false;
                if (repeat)
                {
                    var loop = nodes[head];
                    var instruction = program.Instructions[loop.Enter];
                    breach = PushProgramNode(ref nodes, loop with { Start = index }, ref invocation, out head);
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    repeat = false;
                    if (loop.Enter + 1 < instruction.PartnerIndex) pc = loop.Enter + 1;
                    else
                    {
                        var hit = instruction.Text is { Length: > 0 }
                            ? TryMatchSet(input, instruction, index, out var next)
                            : TryMatchOne(input, instruction, index, out next);
                        if (!hit) failed = true;
                        else
                        {
                            index = next;
                            breach = CompleteProgramIteration(program, ref nodes, ref choices, captures, index,
                                ref invocation, ref head, out pc, out repeat);
                        }
                    }
                }
                else
                {
                    var instruction = program.Instructions[pc];
                    if (instruction.Kind == Utf8ExecutionInstructionKind.Enter)
                    {
                        if (head >= 0 && program.Instructions[nodes[head].Enter].NodeKind == Utf8ExecutionNodeKind.Alternate)
                        {
                            var sibling = instruction.PartnerIndex + 1;
                            if (sibling < program.Instructions[nodes[head].Enter].PartnerIndex)
                                breach = choices.TryPush(new ProgramChoice(sibling, index, head, captures?.CreateCheckpoint() ?? default, false), ref invocation);
                            if (breach != Utf8ExecutionLimitKind.None) return false;
                        }
                        var depth = head < 0 ? 1U : nodes[head].Depth + 1;
                        breach = PushProgramNode(ref nodes, new ProgramFrame(head, pc, index, 0, depth, false), ref invocation, out head);
                        if (breach != Utf8ExecutionLimitKind.None) return false;
                        switch (instruction.NodeKind)
                        {
                            case Utf8ExecutionNodeKind.Capture:
                            case Utf8ExecutionNodeKind.Group:
                            case Utf8ExecutionNodeKind.Concatenate:
                                pc++;
                                break;
                            case Utf8ExecutionNodeKind.Alternate:
                                if (pc + 1 >= instruction.PartnerIndex) failed = true;
                                else pc++;
                                break;
                            case Utf8ExecutionNodeKind.Loop:
                            case Utf8ExecutionNodeKind.LazyLoop:
                                if (instruction.Max < instruction.Min) failed = true;
                                else breach = ScheduleProgramLoop(program, nodes[head], head, index, captures,
                                    ref choices, ref invocation, out pc, out repeat);
                                break;
                            default:
                                if (!TryMatchProgramAtom(input, instruction, index, origin, ref invocation, out index, out breach)) failed = true;
                                else pc = instruction.PartnerIndex;
                                break;
                        }
                    }
                    else
                    {
                        var closed = nodes[head];
                        var enter = program.Instructions[closed.Enter];
                        if (closed.Enter != instruction.PartnerIndex)
                            throw new InvalidOperationException("A program exit does not match its active continuation.");
                        if (enter.NodeKind == Utf8ExecutionNodeKind.Capture && captures is not null)
                            breach = captures.TrySet(enter.CaptureNumber, closed.Start, index - closed.Start, ref invocation);
                        if (breach != Utf8ExecutionLimitKind.None) return false;
                        head = closed.Parent;
                        if (head < 0)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return false;
                            endIndex = index;
                            successful = true;
                            return true;
                        }
                        pc++;
                        var parent = nodes[head];
                        var parentInstruction = program.Instructions[parent.Enter];
                        if (parentInstruction.NodeKind == Utf8ExecutionNodeKind.Alternate)
                            pc = parentInstruction.PartnerIndex;
                        else if (parentInstruction.NodeKind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop &&
                            pc == parentInstruction.PartnerIndex)
                            breach = CompleteProgramIteration(program, ref nodes, ref choices, captures, index,
                                ref invocation, ref head, out pc, out repeat);
                    }
                }
                if (breach != Utf8ExecutionLimitKind.None) return false;
                if (!failed) continue;
                if (choices.Count == 0) return false;
                var choice = choices[choices.Count - 1];
                choices.Pop();
                if (captures is not null) breach = captures.TryRollback(choice.Checkpoint, ref invocation);
                if (breach != Utf8ExecutionLimitKind.None) return false;
                pc = choice.Pc;
                index = choice.Index;
                head = choice.Head;
                repeat = choice.Repeat;
            }
        }
        finally
        {
            if (journaling && captures is not null)
            {
                if (!successful) captures.Rollback(default);
                captures.EndInvocation(ref invocation);
            }
            if (!successful) captures?.Clear();
            choices.Release(ref invocation);
            nodes.Release(ref invocation);
        }
    }

    private static Utf8ExecutionLimitKind PushProgramNode(
        ref Utf8InvocationStateBuffer<ProgramFrame> nodes, ProgramFrame node, ref Utf8SliceInvocation invocation, out int head)
    {
        head = nodes.Count;
        var breach = invocation.Budget.TryNoteDepth(node.Depth);
        return breach != Utf8ExecutionLimitKind.None ? breach : nodes.TryPush(node, ref invocation);
    }

    private static Utf8ExecutionLimitKind CompleteProgramIteration(
        Utf8ExecutionProgram program, ref Utf8InvocationStateBuffer<ProgramFrame> nodes,
        ref Utf8InvocationStateBuffer<ProgramChoice> choices, Utf8CaptureSlots? captures, int index,
        ref Utf8SliceInvocation invocation, ref int head, out int pc, out bool repeat)
    {
        var loop = nodes[head];
        var updated = loop with { Count = loop.Count + 1, Stop = index == loop.Start };
        var breach = PushProgramNode(ref nodes, updated, ref invocation, out head);
        pc = 0; repeat = false;
        return breach != Utf8ExecutionLimitKind.None ? breach
            : ScheduleProgramLoop(program, updated, head, index, captures, ref choices, ref invocation, out pc, out repeat);
    }

    private static Utf8ExecutionLimitKind ScheduleProgramLoop(
        Utf8ExecutionProgram program, ProgramFrame loop, int head, int index, Utf8CaptureSlots? captures,
        ref Utf8InvocationStateBuffer<ProgramChoice> choices, ref Utf8SliceInvocation invocation, out int pc, out bool repeat)
    {
        var instruction = program.Instructions[loop.Enter];
        pc = instruction.PartnerIndex;
        repeat = false;
        if (loop.Count >= instruction.Max || loop.Stop && loop.Count >= instruction.Min) return Utf8ExecutionLimitKind.None;
        if (loop.Count < instruction.Min) { repeat = true; return Utf8ExecutionLimitKind.None; }
        var lazy = instruction.NodeKind == Utf8ExecutionNodeKind.LazyLoop;
        var breach = choices.TryPush(new ProgramChoice(pc, index, head, captures?.CreateCheckpoint() ?? default, lazy), ref invocation);
        repeat = !lazy;
        return breach;
    }

    private static bool TryMatchProgramAtom(ReadOnlySpan<byte> input, Utf8ExecutionInstruction instruction, int index, int origin,
        ref Utf8SliceInvocation invocation, out int endIndex, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        endIndex = index;
        return instruction.NodeKind switch
        {
            Utf8ExecutionNodeKind.Empty => true,
            Utf8ExecutionNodeKind.One => TryMatchOne(input, instruction, index, out endIndex),
            Utf8ExecutionNodeKind.Multi => TryMatchProgramText(input, instruction, index, ref invocation, out endIndex, out breach),
            Utf8ExecutionNodeKind.Set => TryMatchSet(input, instruction, index, out endIndex),
            Utf8ExecutionNodeKind.Bol => IsBeginningOfLine(input, index, instruction.Options),
            Utf8ExecutionNodeKind.Beginning => index == 0,
            Utf8ExecutionNodeKind.Start => index == origin,
            Utf8ExecutionNodeKind.Eol => IsEndAnchorMatch(input, index, instruction.Options),
            Utf8ExecutionNodeKind.EndZ => index == input.Length || index == input.Length - 1 && input[index] == (byte)'\n',
            Utf8ExecutionNodeKind.End => index == input.Length,
            _ => false,
        };
    }
}
