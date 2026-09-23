namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Immutable tree node kinds for the owned R02a slice: concatenation,
/// ordered alternation, greedy/lazy repetition, captures, single bytes, ASCII
/// sets, dots, anchors, and empty. Trees are built once and lowered to <see cref="Utf8SliceProgram"/>.</summary>
internal enum Utf8SliceNodeKind : byte
{
    Empty = 0,
    Literal = 1,
    Set = 2,
    Concat = 3,
    Alternation = 4,
    Repeat = 5,
    Capture = 6,
    Dot = 7,
    Anchor = 8,
}

/// <summary>Immutable tree node for the owned R02a slice.</summary>
internal abstract record Utf8SliceNode(Utf8SliceNodeKind Kind)
{
    internal sealed record EmptyNode() : Utf8SliceNode(Utf8SliceNodeKind.Empty);

    internal sealed record LiteralNode(byte Value) : Utf8SliceNode(Utf8SliceNodeKind.Literal);

    internal sealed record SetNode(byte[] Mask16, bool Negated) : Utf8SliceNode(Utf8SliceNodeKind.Set)
    {
        internal bool Matches(byte value)
        {
            return (Mask16[value >> 3] & (byte)(1 << (value & 7))) != 0;
        }
    }

    internal sealed record ConcatNode(Utf8SliceNode[] Children) : Utf8SliceNode(Utf8SliceNodeKind.Concat);

    internal sealed record AlternationNode(Utf8SliceNode[][] Branches) : Utf8SliceNode(Utf8SliceNodeKind.Alternation);

    internal sealed record RepeatNode(Utf8SliceNode Body, int Minimum, int Maximum, bool Lazy) : Utf8SliceNode(Utf8SliceNodeKind.Repeat);

    internal sealed record CaptureNode(int Slot, Utf8SliceNode Body) : Utf8SliceNode(Utf8SliceNodeKind.Capture);

    internal sealed record DotNode(bool MatchNewline) : Utf8SliceNode(Utf8SliceNodeKind.Dot);

    internal sealed record AnchorNode(bool AtBeginning, bool MatchMultiline) : Utf8SliceNode(Utf8SliceNodeKind.Anchor);
}

/// <summary>Flat immutable instruction kinds for the owned slice runner.</summary>
internal enum Utf8SliceInstructionKind : byte
{
    Literal = 0,
    Set = 1,
    Fork = 2,
    Jump = 3,
    RepeatGreedy = 4,
    RepeatLazy = 5,
    RepeatEnd = 6,
    RepeatExit = 7,
    CaptureStart = 8,
    CaptureEnd = 9,
    Accept = 10,
    Dot = 11,
    Anchor = 12,
}

internal readonly struct Utf8SliceInstruction
{
    internal Utf8SliceInstruction(Utf8SliceInstructionKind kind, int a, int b, int c, int d)
    {
        Kind = kind;
        A = a;
        B = b;
        C = c;
        D = d;
    }

    internal Utf8SliceInstructionKind Kind { get; }
    internal int A { get; }
    internal int B { get; }
    internal int C { get; }
    internal int D { get; }
}

/// <summary>Immutable lowered slice program: instruction array, ASCII set masks,
/// repeat count, and capture slot count. All mutable run state is invocation-owned.</summary>
internal sealed class Utf8SliceProgram
{
    internal Utf8SliceProgram(Utf8SliceInstruction[] instructions, byte[][] setMasks, int repeatCount, int groupCount, bool requiresAstralFreeInput)
    {
        Instructions = instructions;
        SetMasks = setMasks;
        RepeatCount = repeatCount;
        GroupCount = groupCount;
        RequiresAstralFreeInput = requiresAstralFreeInput;
    }

    internal Utf8SliceInstruction[] Instructions { get; }
    internal byte[][] SetMasks { get; }
    internal int RepeatCount { get; }
    internal int GroupCount { get; }
    // R03 astral-divergence marker: the shared runner consumes one whole
    // scalar per dot while the .NET oracle matches one UTF-16 unit per dot,
    // and negated ASCII classes consume whole non-ASCII scalars while the
    // oracle matches astral halves. Programs containing a dot or a negated
    // class set this marker; routing declines astral inputs for them so
    // unit-exact shapes keep oracle parity. ASCII and BMP inputs stay owned:
    // their units are scalars, so both semantics agree. Other shapes never
    // set this: literals, positive classes, concatenation, alternation,
    // repeats, captures, and anchors are encoding-indifferent, so their
    // routes skip the input scan below entirely.
    internal bool RequiresAstralFreeInput { get; }

    // Routing gate: true unless this program needs astral-free input and
    // the subject carries astral lead bytes (which validated inputs hold only
    // as four-byte scalar starts). Vector-scanned; BMP and ASCII pass through.
    internal bool ServesInput(ReadOnlySpan<byte> input)
    {
        return !RequiresAstralFreeInput || input.IndexOfAnyInRange((byte)0xF0, byte.MaxValue) < 0;
    }
}

/// <summary>Lowers immutable slice trees to flat programs with patched jumps.</summary>
internal static class Utf8SliceLowerer
{
    // Expansion-site backstop for the parser length cap: lowering is linear, so
    // this only trips on synthetic trees or future parser changes, never on
    // admitted patterns. The bound keeps retained programs near twenty megabytes
    // worst case (one million twenty-byte instructions); over-limit trees decline
    // to null and callers keep legacy routing.
    internal const int MaxInstructions = 1048576;

    internal static Utf8SliceProgram? Lower(Utf8SliceNode root)
    {
        var lowerer = new Lowering();
        if (!lowerer.EmitNode(root))
        {
            return null;
        }

        lowerer.Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Accept, 0, 0, 0, 0));
        var requiresAstralFreeInput = false;
        foreach (var instruction in lowerer.Instructions)
        {
            if (instruction.Kind == Utf8SliceInstructionKind.Dot ||
                (instruction.Kind == Utf8SliceInstructionKind.Set && instruction.B != 0))
            {
                requiresAstralFreeInput = true;
                break;
            }
        }

        return new Utf8SliceProgram(
            lowerer.Instructions.ToArray(),
            lowerer.SetMasks.ToArray(),
            lowerer.RepeatCount,
            lowerer.GroupCount,
            requiresAstralFreeInput);
    }

    private sealed class Lowering
    {
        internal readonly List<Utf8SliceInstruction> Instructions = new();
        internal readonly List<byte[]> SetMasks = new();
        internal int RepeatCount;
        internal int GroupCount;

        internal void Emit(Utf8SliceInstruction instruction)
        {
            Instructions.Add(instruction);
        }

        internal bool EmitNode(Utf8SliceNode node)
        {
            if (Instructions.Count >= Utf8SliceLowerer.MaxInstructions)
            {
                return false;
            }

            switch (node)
            {
                case Utf8SliceNode.EmptyNode:
                    break;
                case Utf8SliceNode.LiteralNode literal:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Literal, literal.Value, 0, 0, 0));
                    break;
                case Utf8SliceNode.SetNode set:
                    if (set.Mask16.Length != 16)
                    {
                        throw new ArgumentException("Slice set masks cover 16 bytes (128 ASCII bits).", nameof(node));
                    }

                    var maskIndex = SetMasks.Count;
                    SetMasks.Add(set.Mask16);
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Set, maskIndex, set.Negated ? 1 : 0, 0, 0));
                    break;
                case Utf8SliceNode.ConcatNode concat:
                    foreach (var child in concat.Children)
                    {
                        if (!EmitNode(child))
                        {
                            return false;
                        }
                    }

                    break;
                case Utf8SliceNode.AlternationNode alternation:
                    if (alternation.Branches.Length == 0)
                    {
                        throw new ArgumentException("Slice alternations need at least one branch.", nameof(node));
                    }

                    var endJumps = new List<int>();
                    for (var branch = 0; branch < alternation.Branches.Length; branch++)
                    {
                        if (branch + 1 < alternation.Branches.Length)
                        {
                            var forkIndex = Instructions.Count;
                            Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fork, 0, 0, 0, 0));
                            foreach (var child in alternation.Branches[branch])
                            {
                                if (!EmitNode(child))
                                {
                                    return false;
                                }
                            }

                            endJumps.Add(Instructions.Count);
                            Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                            Patch(forkIndex, Instructions.Count);
                        }
                        else
                        {
                            foreach (var child in alternation.Branches[branch])
                            {
                                if (!EmitNode(child))
                                {
                                    return false;
                                }
                            }
                        }
                    }

                    foreach (var jump in endJumps)
                    {
                        Patch(jump, Instructions.Count);
                    }

                    break;
                case Utf8SliceNode.RepeatNode repeat:
                    if (repeat.Minimum < 0 || repeat.Maximum < repeat.Minimum)
                    {
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice repeat bounds need 0 <= min <= max.");
                    }

                    var repeatId = RepeatCount++;
                    var repeatIndex = Instructions.Count;
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.RepeatGreedy, 0, 0, 0, 0));
                    if (!EmitNode(repeat.Body))
                    {
                        return false;
                    }
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.RepeatEnd, repeatIndex, 0, 0, 0));
                    var exitIndex = Instructions.Count;
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.RepeatExit, repeatId, 0, 0, 0));
                    Instructions[repeatIndex] = new Utf8SliceInstruction(
                        repeat.Lazy ? Utf8SliceInstructionKind.RepeatLazy : Utf8SliceInstructionKind.RepeatGreedy,
                        exitIndex, repeatId, repeat.Minimum, repeat.Maximum);
                    break;
                case Utf8SliceNode.DotNode dot:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Dot, dot.MatchNewline ? 1 : 0, 0, 0, 0));
                    break;
                case Utf8SliceNode.AnchorNode anchor:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Anchor, anchor.AtBeginning ? 0 : 1, anchor.MatchMultiline ? 1 : 0, 0, 0));
                    break;
                case Utf8SliceNode.CaptureNode capture:
                    if (capture.Slot < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice capture slots start at zero.");
                    }

                    if (capture.Slot + 1 > GroupCount)
                    {
                        GroupCount = capture.Slot + 1;
                    }

                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.CaptureStart, capture.Slot, 0, 0, 0));
                    if (!EmitNode(capture.Body))
                    {
                        return false;
                    }
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.CaptureEnd, capture.Slot, 0, 0, 0));
                    break;
                default:
                    throw new ArgumentException($"Unsupported slice node {node.Kind}.", nameof(node));
            }

            return true;
        }

        private void Patch(int index, int target)
        {
            var existing = Instructions[index];
            Instructions[index] = new Utf8SliceInstruction(existing.Kind, target, existing.B, existing.C, existing.D);
        }
    }
}
