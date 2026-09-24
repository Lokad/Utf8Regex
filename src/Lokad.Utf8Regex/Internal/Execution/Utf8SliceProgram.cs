namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Immutable tree node kinds for the owned R02a slice: concatenation,
/// ordered alternation, greedy/lazy repetition, captures, single bytes, ASCII
/// sets, dots, anchors, word boundaries, shorthand classes, backreferences, lookarounds,
/// and empty. Trees are built once and lowered to <see cref="Utf8SliceProgram"/>.</summary>
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
    Boundary = 9,
    Shorthand = 10,
    Backref = 11,
    Lookahead = 12,
    Lookbehind = 13,
    Balance = 14,
    Atomic = 15,
}

/// <summary>Unicode shorthand classes for the owned slice. Odd values are
/// the complements of the preceding even value.</summary>
internal enum Utf8SliceShorthand : byte
{
    Digit = 0,
    NonDigit = 1,
    Word = 2,
    NonWord = 3,
    Space = 4,
    NonSpace = 5,
}

/// <summary>Anchor flavors for the owned slice: line edges honor Multiline,
/// absolute edges ignore it.</summary>
internal enum Utf8SliceAnchorKind : byte
{
    Beginning = 0,
    End = 1,
    VeryEnd = 2,
    Contiguous = 3,
}

/// <summary>Immutable tree node for the owned R02a slice.</summary>
internal abstract record Utf8SliceNode(Utf8SliceNodeKind Kind)
{
    internal sealed record EmptyNode() : Utf8SliceNode(Utf8SliceNodeKind.Empty);

    internal sealed record LiteralNode(byte Value) : Utf8SliceNode(Utf8SliceNodeKind.Literal);

    internal sealed record SetNode(byte[] Mask16, bool Negated, Utf8SliceShorthand[] Members) : Utf8SliceNode(Utf8SliceNodeKind.Set)
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

    internal sealed record AnchorNode(Utf8SliceAnchorKind Anchor, bool MatchMultiline) : Utf8SliceNode(Utf8SliceNodeKind.Anchor);

    internal sealed record BoundaryNode(bool IsWordBoundary) : Utf8SliceNode(Utf8SliceNodeKind.Boundary);

    internal sealed record ShorthandNode(Utf8SliceShorthand Shorthand) : Utf8SliceNode(Utf8SliceNodeKind.Shorthand);

    internal sealed record BackrefNode(int Slot) : Utf8SliceNode(Utf8SliceNodeKind.Backref);
    internal sealed record LookaheadNode(Utf8SliceNode Body, bool Positive) : Utf8SliceNode(Utf8SliceNodeKind.Lookahead);
    internal sealed record LookbehindNode(Utf8SliceNode Body, bool Positive) : Utf8SliceNode(Utf8SliceNodeKind.Lookbehind);
    internal sealed record BalanceNode(int PopSlot, int CaptureSlot, Utf8SliceNode Body) : Utf8SliceNode(Utf8SliceNodeKind.Balance);
    internal sealed record AtomicNode(Utf8SliceNode Body) : Utf8SliceNode(Utf8SliceNodeKind.Atomic);
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
    Boundary = 13,
    Shorthand = 14,
    Backref = 15,
    Fail = 16,
    Lookbehind = 17,
    Commit = 18,
    Hybrid = 19,
    BalancePop = 20,
    AtomicCommit = 21,
    Lookahead = 22,
}

/// <summary>Hybrid member bits for the negated shorthands (NonDigit, NonWord, NonSpace).</summary>
internal static class Utf8SliceHybridMembers
{
    internal const int NegatedBits = 42;
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
/// <summary>Lowered lookbehind body: a private instruction stream with local
/// repeat ids and set masks, plus byte-length bounds for the start search.
/// The body shares the parent capture slots at run time.</summary>
internal sealed class Utf8SliceAssertion
{
    internal Utf8SliceAssertion(Utf8SliceProgram body, int minLengthBytes, int maxLengthBytes, bool isLookbehind)
    {
        Body = body;
        MinLengthBytes = minLengthBytes;
        MaxLengthBytes = maxLengthBytes;
        IsLookbehind = isLookbehind;
    }

    internal Utf8SliceProgram Body { get; }
    internal int MinLengthBytes { get; }
    internal int MaxLengthBytes { get; }
    internal bool IsLookbehind { get; }
}

internal sealed class Utf8SliceProgram
{
    internal Utf8SliceProgram(Utf8SliceInstruction[] instructions, byte[][] setMasks, int repeatCount, int groupCount, bool requiresAstralFreeInput, bool requiresUnicodeWordBoundary, bool requiresUnicodeShorthand, Utf8SliceAssertion[] assertions, bool hasNonFixedLookbehind, string[] groupNames, bool hasMixedNamedGroups, bool hasBalancing, bool ignoreCase, bool hasContiguousAnchor, bool rightToLeft)
    {
        Instructions = instructions;
        SetMasks = setMasks;
        RepeatCount = repeatCount;
        GroupCount = groupCount;
        RequiresAstralFreeInput = requiresAstralFreeInput;
        RequiresUnicodeWordBoundary = requiresUnicodeWordBoundary;
        RequiresUnicodeShorthand = requiresUnicodeShorthand;
        Assertions = assertions;
        HasNonFixedLookbehind = hasNonFixedLookbehind;
        GroupNames = groupNames;
        HasMixedNamedGroups = hasMixedNamedGroups;
        HasBalancing = hasBalancing;
        IgnoreCase = ignoreCase;
        HasContiguousAnchor = hasContiguousAnchor;
        RightToLeft = rightToLeft;
        OrdinalGroupNumbers = new int[groupCount + 1];
        for (var group = 0; group <= groupCount; group++)
        {
            OrdinalGroupNumbers[group] = group;

        }
    }

    internal Utf8SliceInstruction[] Instructions { get; }
    internal byte[][] SetMasks { get; }
    internal int RepeatCount { get; }
    internal int GroupCount { get; }
    internal Utf8SliceAssertion[] Assertions { get; }
    internal bool HasNonFixedLookbehind { get; }
    internal bool HasMixedNamedGroups { get; }
    internal bool HasBalancing { get; }
    internal bool IgnoreCase { get; }
    internal bool HasContiguousAnchor { get; }
    internal bool RightToLeft { get; }
    internal int[] OrdinalGroupNumbers { get; }
    internal string[] GroupNames { get; }
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

    // R03 word-boundary marker: the shared runner applies Unicode word
    // semantics (BCL-exact, including zero-width joiners). Consumers whose
    // native boundary is ASCII-scoped decline marked programs.
    internal bool RequiresUnicodeWordBoundary { get; }

    // R03 shorthand marker: the shared runner applies Unicode shorthand
    // semantics (decimal digits, word chars, and separators beyond ASCII).
    // Consumers whose native shorthands are ASCII-scoped decline marked
    // programs.
    internal bool RequiresUnicodeShorthand { get; }

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

    internal static Utf8SliceProgram? Lower(Utf8SliceNode root, string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool rightToLeft)
    {
        var lowerer = new Lowering(groupNames, maintainBalance, matchCaseInsensitive, rightToLeft);
        // Right-to-left programs execute backreferences before their groups in
        // reversed order, so seed the slot count from the final group tally
        // (parser-validated digits always land inside it); forward programs grow
        // it in encounter order exactly as before.
        if (groupNames is not null)
        {
            lowerer.GroupCount = groupNames.Length - 1;
        }
        if (!lowerer.EmitNode(root))
        {
            return null;
        }

        lowerer.Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Accept, 0, 0, 0, 0));
        var requiresAstralFreeInput = false;
        var requiresUnicodeWordBoundary = false;
        var requiresUnicodeShorthand = false;
        static void Classify(
            Utf8SliceInstruction instruction,
            ref bool astral,
            ref bool word,
            ref bool shorthand)
        {
            if (instruction.Kind == Utf8SliceInstructionKind.Dot ||
                (instruction.Kind == Utf8SliceInstructionKind.Set && instruction.B != 0) ||
                (instruction.Kind == Utf8SliceInstructionKind.Shorthand && instruction.A % 2 == 1))
            {
                astral = true;
            }

            if (instruction.Kind == Utf8SliceInstructionKind.Boundary)
            {
                word = true;
            }

            if (instruction.Kind == Utf8SliceInstructionKind.Shorthand)
            {
                shorthand = true;
            }

            // Mixed classes always carry Unicode shorthand semantics (even
            // digit-only mixes match non-ASCII digits), so they decline
            // ASCII-scoped consumers. Astral inputs decline when the class
            // can match astral scalars whole while the oracle matches
            // halves: negated hybrids, or any negated-shorthand member.
            if (instruction.Kind == Utf8SliceInstructionKind.Hybrid)
            {
                shorthand = true;
                if (instruction.B != 0 || (instruction.C & Utf8SliceHybridMembers.NegatedBits) != 0)
                {
                    astral = true;
                }
            }
        }

        foreach (var instruction in lowerer.Instructions)
        {
            Classify(instruction, ref requiresAstralFreeInput, ref requiresUnicodeWordBoundary, ref requiresUnicodeShorthand);
            if (requiresAstralFreeInput && requiresUnicodeWordBoundary && requiresUnicodeShorthand)
            {
                break;
            }
        }

        // Lookbehind bodies run on the same bytes through the shared runner,
        // so their markers hoist: a dot, negated class, or negated shorthand
        // inside a body needs astral-free input exactly like the main stream.
        // Bodies whose byte length is not fixed are also recorded for
        // consumers whose native lookbehind is fixed-length only.
        var assertions = lowerer.Assertions.ToArray();
        var hasNonFixedLookbehind = false;
        foreach (var assertion in assertions)
        {
            if (assertion.IsLookbehind && assertion.MinLengthBytes != assertion.MaxLengthBytes)
            {
                hasNonFixedLookbehind = true;
            }

            foreach (var nested in assertion.Body.Instructions)
            {
                Classify(nested, ref requiresAstralFreeInput, ref requiresUnicodeWordBoundary, ref requiresUnicodeShorthand);
                if (requiresAstralFreeInput && requiresUnicodeWordBoundary && requiresUnicodeShorthand)
                {
                    break;
                }
            }
        }

        // Hand-built trees (tests, assertion bodies without renumbering
        // context) carry no names; ordinals are BCL-exact for them since
        // only the parser admits named shapes, always with real names.
        var resolvedNames = groupNames;
        if (resolvedNames is null)
        {
            resolvedNames = new string[lowerer.GroupCount + 1];
            for (var number = 0; number <= lowerer.GroupCount; number++)
            {
                resolvedNames[number] = ((uint)number).ToString();
            }
        }

        // Mixed named/unnamed shapes number differently across oracles
        // (BCL unnamed-first vs positional), so consumers with positional
        // native numbering decline them. All-digit spellings can never be
        // names (explicit numbers decline at parse), so a name matching
        // its slot number is exactly an unnamed group.
        var hasUnnamedGroups = false;
        var hasNamedGroups = false;
        for (var number = 1; number < resolvedNames.Length; number++)
        {
            if (resolvedNames[number] == number.ToString())
            {
                hasUnnamedGroups = true;
            }
            else
            {
                hasNamedGroups = true;
            }
        }

        return new Utf8SliceProgram(
            lowerer.Instructions.ToArray(),
            lowerer.SetMasks.ToArray(),
            lowerer.RepeatCount,
            lowerer.GroupCount,
            requiresAstralFreeInput,
            requiresUnicodeWordBoundary,
            requiresUnicodeShorthand,
            assertions,
            hasNonFixedLookbehind, resolvedNames, hasUnnamedGroups && hasNamedGroups, maintainBalance, lowerer.MatchCaseInsensitive, lowerer.SawContiguousAnchor, rightToLeft);
    }

    private sealed class Lowering
    {
        internal readonly List<Utf8SliceInstruction> Instructions = new();
        internal readonly List<byte[]> SetMasks = new();
        internal int RepeatCount;
        internal int GroupCount;
        internal bool SawContiguousAnchor;
        internal readonly List<Utf8SliceAssertion> Assertions = new();
        internal readonly string[]? GroupNames;
        internal readonly bool MaintainBalance;
        internal readonly bool MatchCaseInsensitive;
        internal readonly bool MirrorExecute;

        internal Lowering(string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool mirrorExecute)
        {
            GroupNames = groupNames;
            MaintainBalance = maintainBalance;
            MatchCaseInsensitive = matchCaseInsensitive;
            MirrorExecute = mirrorExecute;
        }

        internal void Emit(Utf8SliceInstruction instruction)
        {
            Instructions.Add(instruction);
        }

        internal bool EmitNode(Utf8SliceNode node)
        {
            // Byte-length bounds for lookbehind start search, saturated:
            // the lower bound keeps the search inside the subject, the
            // upper bound comes from the longest servable body. Captures
            // are unbounded above and empty below; nested assertions are
            // zero-width.
            static int Saturate(long value)
            {
                return value > int.MaxValue ? int.MaxValue : (int)value;
            }

            static (int Minimum, int Maximum) Measure(Utf8SliceNode measured)
            {
                switch (measured)
                {
                    case Utf8SliceNode.EmptyNode:
                    case Utf8SliceNode.AnchorNode:
                    case Utf8SliceNode.BoundaryNode:
                    case Utf8SliceNode.LookaheadNode:
                    case Utf8SliceNode.LookbehindNode:
                        return (0, 0);
                    case Utf8SliceNode.LiteralNode:
                        return (1, 1);
                    case Utf8SliceNode.SetNode set:
                        return set.Negated ? (1, 4) : (1, 1);
                    case Utf8SliceNode.DotNode:
                        return (1, 4);
                    case Utf8SliceNode.ShorthandNode shorthand:
                        return (int)shorthand.Shorthand % 2 == 1 ? (1, 4) : (1, 3);
                    case Utf8SliceNode.BackrefNode:
                        return (0, int.MaxValue);
                    case Utf8SliceNode.CaptureNode capture:
                        return Measure(capture.Body);
                    case Utf8SliceNode.BalanceNode balance:
                        return Measure(balance.Body);
                    case Utf8SliceNode.AtomicNode atomic:
                        return Measure(atomic.Body);
                    case Utf8SliceNode.ConcatNode concat:
                        var totalMinimum = 0L;
                        var totalMaximum = 0L;
                        foreach (var child in concat.Children)
                        {
                            var (childMinimum, childMaximum) = Measure(child);
                            totalMinimum += childMinimum;
                            totalMaximum += childMaximum;
                        }

                        return (Saturate(totalMinimum), Saturate(totalMaximum));
                    case Utf8SliceNode.AlternationNode alternation:
                        var branchMinimum = int.MaxValue;
                        var branchMaximum = 0;
                        foreach (var branch in alternation.Branches)
                        {
                            var totalBranchMinimum = 0L;
                            var totalBranchMaximum = 0L;
                            foreach (var child in branch)
                            {
                                var (childMinimum, childMaximum) = Measure(child);
                                totalBranchMinimum += childMinimum;
                                totalBranchMaximum += childMaximum;
                            }

                            branchMinimum = Math.Min(branchMinimum, Saturate(totalBranchMinimum));
                            branchMaximum = Math.Max(branchMaximum, Saturate(totalBranchMaximum));
                        }

                        return alternation.Branches.Length == 0
                            ? (0, 0)
                            : (branchMinimum, branchMaximum);
                    case Utf8SliceNode.RepeatNode repeat:
                        var (bodyMinimum, bodyMaximum) = Measure(repeat.Body);
                        var repeatMinimum = Saturate((long)bodyMinimum * repeat.Minimum);
                        var repeatMaximum = repeat.Maximum == int.MaxValue || bodyMaximum == int.MaxValue
                            ? int.MaxValue
                            : Saturate((long)bodyMaximum * repeat.Maximum);
                        return (repeatMinimum, repeatMaximum);
                    default:
                        return (0, int.MaxValue);
                }
            }

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
                    if (set.Members.Length == 0)
                    {
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Set, maskIndex, set.Negated ? 1 : 0, 0, 0));
                    }
                    else
                    {
                        // Mixed classes keep shorthand members alongside the
                        // ASCII mask: the mask (with shorthand ASCII parts
                        // folded in) decides ASCII bytes, the members decide
                        // non-ASCII scalars with full Unicode categories.
                        var memberMask = 0;
                        foreach (var member in set.Members)
                        {
                            memberMask |= 1 << (int)member;
                        }

                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Hybrid, maskIndex, set.Negated ? 1 : 0, memberMask, 0));
                    }

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
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Anchor, (int)anchor.Anchor, anchor.MatchMultiline ? 1 : 0, 0, 0));
                    if (anchor.Anchor == Utf8SliceAnchorKind.Contiguous)
                    {
                        SawContiguousAnchor = true;
                    }

                    break;
                case Utf8SliceNode.BoundaryNode boundary:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Boundary, boundary.IsWordBoundary ? 1 : 0, 0, 0, 0));
                    break;
                case Utf8SliceNode.ShorthandNode shorthand:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Shorthand, (int)shorthand.Shorthand, 0, 0, 0));
                    break;
                case Utf8SliceNode.BackrefNode backref:
                    if (backref.Slot < 0 || backref.Slot >= GroupCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice backreferences bind earlier groups.");
                    }

                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Backref, backref.Slot, 0, 0, 0));
                    break;
                case Utf8SliceNode.BalanceNode balance:
                    if (balance.PopSlot < 0 || (balance.CaptureSlot < -1))
                    {
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice balance slots start at zero.");
                    }

                    if (balance.PopSlot + 1 > GroupCount)
                    {
                        GroupCount = balance.PopSlot + 1;
                    }

                    if (balance.CaptureSlot + 1 > GroupCount)
                    {
                        GroupCount = balance.CaptureSlot + 1;
                    }

                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.BalancePop, balance.PopSlot, balance.CaptureSlot, 0, 0));
                    if (!EmitNode(balance.Body))
                    {
                        return false;
                    }

                    break;
                case Utf8SliceNode.AtomicNode atomic:
                    // Possessive groups: a surviving body commits (popping to the
                    // atomic fork and keeping captures, input, and repeat state)
                    // while any failing path unwinds to the fork and fails through
                    // the Fail branch.
                    var atomicFork = Instructions.Count;
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fork, 0, 2, 0, 0));
                    if (!EmitNode(atomic.Body))
                    {
                        return false;
                    }

                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.AtomicCommit, 0, 0, 0, 0));
                    var atomicJump = Instructions.Count;
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                    var atomicFail = Instructions.Count;
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
                    Patch(atomicFork, atomicFail);
                    Patch(atomicJump, Instructions.Count);
                    break;
                case Utf8SliceNode.LookaheadNode lookahead:
                    // Right-to-left programs run the main stream mirrored while BCL
                    // parses lookahead bodies left-to-right, so mirrored mains serve
                    // lookahead bodies as left-to-right sub-programs (shared slots,
                    // zero width, captures persisting on positive survival exactly
                    // like lookbehind bodies).
                    if (MirrorExecute)
                    {
                        var lookaheadBody = Utf8SliceLowerer.Lower(lookahead.Body, GroupNames, MaintainBalance, MatchCaseInsensitive, false);
                        if (lookaheadBody is null)
                        {
                            return false;
                        }

                        if (lookaheadBody.GroupCount > GroupCount)
                        {
                            GroupCount = lookaheadBody.GroupCount;
                        }

                        var (lookaheadMinimum, lookaheadMaximum) = Measure(lookahead.Body);
                        var lookaheadAssertionIndex = Assertions.Count;
                        Assertions.Add(new Utf8SliceAssertion(lookaheadBody, lookaheadMinimum, lookaheadMaximum, false));
                        Emit(new Utf8SliceInstruction(
                            Utf8SliceInstructionKind.Lookahead,
                            lookaheadAssertionIndex,
                            lookahead.Positive ? 1 : 0,
                            0,
                            0));
                        break;
                    }

                    // Atomic first-success assertions on the shared trail:
                    // a surviving body commits (popping to the assertion
                    // fork, restoring repeat state, keeping captures) while
                    // any failing path unwinds to the fork and truncates the
                    // body writes.
                    if (lookahead.Positive)
                    {
                        var forkIndex = Instructions.Count;
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fork, 0, 1, 0, 0));
                        if (!EmitNode(lookahead.Body))
                        {
                            return false;
                        }

                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Commit, 0, 0, 0, 0));
                        var jumpIndex = Instructions.Count;
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                        var failIndex = Instructions.Count;
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
                        Patch(forkIndex, failIndex);
                        Patch(jumpIndex, Instructions.Count);
                    }
                    else
                    {
                        var forkIndex = Instructions.Count;
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fork, 0, 1, 0, 0));
                        if (!EmitNode(lookahead.Body))
                        {
                            return false;
                        }

                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Commit, 0, 0, 0, 0));
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
                        Patch(forkIndex, Instructions.Count);
                    }

                    break;
                case Utf8SliceNode.LookbehindNode lookbehind:
                    {
                        var body = Utf8SliceLowerer.Lower(lookbehind.Body, GroupNames, MaintainBalance, MatchCaseInsensitive, false);
                        if (body is null)
                        {
                            return false;
                        }

                        if (body.GroupCount > GroupCount)
                        {
                            GroupCount = body.GroupCount;
                        }

                        var (minimum, maximum) = Measure(lookbehind.Body);
                        var assertionIndex = Assertions.Count;
                        Assertions.Add(new Utf8SliceAssertion(body, minimum, maximum, true));
                        Emit(new Utf8SliceInstruction(
                            Utf8SliceInstructionKind.Lookbehind,
                            assertionIndex,
                            lookbehind.Positive ? 1 : 0,
                            0,
                            0));
                        break;
                    }

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
