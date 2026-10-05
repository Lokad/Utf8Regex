using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Immutable tree node kinds for the owned R02a slice: concatenation,
/// ordered alternation, greedy/lazy repetition, captures, UTF-16 literals, ASCII
/// sets, dots, anchors, word boundaries, shorthand classes, Unicode property
/// escapes, backreferences, lookarounds,
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
    Conditional = 16,
    Category = 17,
    CategorySet = 18,
    Fail = 19,
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
    internal sealed record FailNode() : Utf8SliceNode(Utf8SliceNodeKind.Fail);

    internal sealed record LiteralNode(ushort Value, bool FoldCase) : Utf8SliceNode(Utf8SliceNodeKind.Literal);

    internal sealed record SetNode(byte[] Mask16, bool Negated, Utf8SliceShorthand[] Members, bool FoldCase) : Utf8SliceNode(Utf8SliceNodeKind.Set)
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

    internal sealed record BackrefNode(int Slot, bool FoldCase) : Utf8SliceNode(Utf8SliceNodeKind.Backref);
    internal sealed record UnresolvedBackrefNode(string Name, bool FoldCase) : Utf8SliceNode(Utf8SliceNodeKind.Backref);
    internal sealed record UnresolvedNumericNode(string Digits, bool FoldCase) : Utf8SliceNode(Utf8SliceNodeKind.Backref);
    internal sealed record UnresolvedNumericRepeatNode(string Digits, bool FoldCase, int Minimum, int Maximum, bool Lazy) : Utf8SliceNode(Utf8SliceNodeKind.Repeat);
    internal sealed record UnresolvedCaptureNode(int Number, Utf8SliceNode Body) : Utf8SliceNode(Utf8SliceNodeKind.Capture);
    internal sealed record UnresolvedBalanceNode(string PopName, int CaptureSlot, int CaptureNumber, Utf8SliceNode Body) : Utf8SliceNode(Utf8SliceNodeKind.Balance);
    internal sealed record UnresolvedConditionalNode(string Number, Utf8SliceNode YesBody, Utf8SliceNode NoBody) : Utf8SliceNode(Utf8SliceNodeKind.Conditional);
    internal sealed record CategoryNode(uint Mask, ushort[] Ranges, bool Negated, bool FoldCase) : Utf8SliceNode(Utf8SliceNodeKind.Category);
    internal sealed record CategorySetMember(Utf8SliceCategoryTable Table, bool Negated);
    internal sealed record CategorySetNode(byte[] Mask16, bool Negated, Utf8SliceShorthand[] Members, CategorySetMember[] Categories, bool FoldCase) : Utf8SliceNode(Utf8SliceNodeKind.CategorySet);
    internal sealed record LookaheadNode(Utf8SliceNode Body, bool Positive) : Utf8SliceNode(Utf8SliceNodeKind.Lookahead);
    internal sealed record LookbehindNode(Utf8SliceNode Body, bool Positive) : Utf8SliceNode(Utf8SliceNodeKind.Lookbehind);
    internal sealed record BalanceNode(int PopSlot, int CaptureSlot, Utf8SliceNode Body) : Utf8SliceNode(Utf8SliceNodeKind.Balance);
    internal sealed record AtomicNode(Utf8SliceNode Body) : Utf8SliceNode(Utf8SliceNodeKind.Atomic);
    internal sealed record ConditionalNode(int TestSlot, Utf8SliceNode YesBody, Utf8SliceNode NoBody, Utf8SliceNode? TestBody) : Utf8SliceNode(Utf8SliceNodeKind.Conditional);
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
    TestGroup = 23,
    Category = 24,
    CategorySet = 25,
    RepeatGreedyUtf16 = 26,
    BalanceStart = 27,
}

/// <summary>Hybrid member bits for the negated shorthands (NonDigit, NonWord, NonSpace).</summary>
internal static class Utf8SliceHybridMembers
{
    internal const int NegatedBits = 42;
}

/// <summary>Unicode property table for one owned category atom: a
/// UnicodeCategory bit mask for general categories/groups plus half-open
/// range endpoint pairs (with a trailing lone endpoint spanning to char.MaxValue) for
/// named blocks, or one immutable class subtraction program.</summary>
internal sealed class Utf8SliceCategoryTable
{
    internal Utf8SliceCategoryTable(uint mask, ushort[] ranges)
    {
        Mask = mask;
        Ranges = ranges;
    }

    internal Utf8SliceCategoryTable(Utf8SliceClassProgram classProgram)
    {
        Mask = 0;
        Ranges = [];
        ClassProgram = classProgram;
    }

    internal uint Mask { get; }

    internal ushort[] Ranges { get; }

    internal Utf8SliceClassProgram? ClassProgram { get; }
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
    internal Utf8SliceProgram(Utf8SliceInstruction[] instructions, byte[][] setMasks, Utf8SliceCategoryTable[] categoryTables, Utf8SliceNode.CategorySetMember[][] categorySetMembers, int repeatCount, int groupCount, bool requiresAstralFreeInput, bool requiresUnicodeWordBoundary, bool requiresUnicodeShorthand, bool requiresUnicodeCategory, Utf8SliceAssertion[] assertions, bool hasNonFixedLookbehind, string[] groupNames, bool hasMixedNamedGroups, bool hasBalancing, bool ignoreCase, bool hasContiguousAnchor, bool hasConditionalTest, bool rightToLeft, bool sawLookaround, bool sawCapturingLookbehind, bool canMatchEmpty, bool halvesClosedEmptyLoop, Utf8CaptureCatalog? captureCatalog, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax)
    {
        Instructions = instructions;
        SetMasks = setMasks;
        CategoryTables = categoryTables;
        CategorySetMembers = categorySetMembers;
        RepeatCount = repeatCount;
        GroupCount = groupCount;
        RequiresAstralFreeInput = requiresAstralFreeInput;
        RequiresUnicodeWordBoundary = requiresUnicodeWordBoundary;
        RequiresUnicodeShorthand = requiresUnicodeShorthand;
        RequiresUnicodeCategory = requiresUnicodeCategory;
        Assertions = assertions;
        HasNonFixedLookbehind = hasNonFixedLookbehind;
        GroupNames = groupNames;
        CaptureCatalog = captureCatalog;
        HasMixedNamedGroups = hasMixedNamedGroups;
        HasBalancing = hasBalancing;
        IgnoreCase = ignoreCase;
        HasContiguousAnchor = hasContiguousAnchor;
        HasConditionalTest = hasConditionalTest;
        RightToLeft = rightToLeft;
        SawLookaround = sawLookaround;
        SawCapturingLookbehind = sawCapturingLookbehind;
        CanMatchEmpty = canMatchEmpty;
        HalvesClosedEmptyLoop = halvesClosedEmptyLoop;
        UsesInterpretedLazyLoopSemantics = usesInterpretedLazyLoopSemantics;
        UsesLiteralClassBracketSyntax = usesLiteralClassBracketSyntax;
        UsesCaptureOpeningJournal = HasSharedCaptureDeclarations;
        foreach (var instruction in instructions)
        {
            UsesCaptureOpeningJournal |= instruction.Kind == Utf8SliceInstructionKind.Backref;
            HasUnicodePatternAtoms |= instruction.Kind == Utf8SliceInstructionKind.CategorySet ||
                instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.A >= 128;
            if (instruction.Kind == Utf8SliceInstructionKind.Literal &&
                instruction.A is >= 0xD800 and <= 0xDFFF)
            {
                HasSurrogateLiteral = true;
            }
        }
        foreach (var assertion in assertions)
        {
            UsesCaptureOpeningJournal |= assertion.Body.UsesCaptureOpeningJournal;
            HasSurrogateLiteral |= assertion.Body.HasSurrogateLiteral;
            HasUnicodePatternAtoms |= assertion.Body.HasUnicodePatternAtoms;
            HasOrderSensitiveLookbehind |= assertion.Body.HasOrderSensitiveLookbehind;
            UsesLiteralClassBracketSyntax |= assertion.Body.UsesLiteralClassBracketSyntax;
            if (assertion.IsLookbehind)
                foreach (var instruction in assertion.Body.Instructions)
                    HasOrderSensitiveLookbehind |= instruction.Kind is Utf8SliceInstructionKind.Backref
                        or Utf8SliceInstructionKind.TestGroup or Utf8SliceInstructionKind.Lookahead
                        or Utf8SliceInstructionKind.Lookbehind or Utf8SliceInstructionKind.AtomicCommit;
        }
        GroupNumbers = captureCatalog?.GroupNumbers ?? new int[groupCount + 1];
        if (captureCatalog is null)
            for (var group = 0; group <= groupCount; group++) GroupNumbers[group] = group;
    }

    internal Utf8SliceInstruction[] Instructions { get; }
    internal byte[][] SetMasks { get; }

    internal Utf8SliceCategoryTable[] CategoryTables { get; }

    internal Utf8SliceNode.CategorySetMember[][] CategorySetMembers { get; }
    internal int RepeatCount { get; }
    internal int GroupCount { get; }
    internal Utf8SliceAssertion[] Assertions { get; }
    internal bool HasNonFixedLookbehind { get; }
    // PCRE2-INTEGRATION-POINT: profiles that execute fixed lookbehind forward
    // cannot reuse these bodies.
    internal bool HasOrderSensitiveLookbehind { get; }
    // PCRE2-INTEGRATION-POINT: .NET reads POSIX-looking class syntax as
    // literal members; profiles with a different grammar keep their programs.
    internal bool UsesLiteralClassBracketSyntax { get; }
    internal bool HasMixedNamedGroups { get; }
    internal Utf8CaptureCatalog? CaptureCatalog { get; }
    internal bool UsesCaptureOpeningJournal { get; }
    internal bool HasExplicitCaptureNumbers => CaptureCatalog is { HasExplicitCaptureNumbers: true };
    internal bool HasSharedCaptureDeclarations => CaptureCatalog is { HasSharedCaptureDeclarations: true };
    internal bool HasBalancing { get; }
    internal bool IgnoreCase { get; }
    internal bool HasContiguousAnchor { get; }
    internal bool HasConditionalTest { get; }
    internal bool SawLookaround { get; }
    internal bool SawCapturingLookbehind { get; }
    internal bool RightToLeft { get; }
    internal bool CanMatchEmpty { get; }
    // Native scalar literal plans cannot represent a lone pattern half. Keep
    // their qualified lanes for BMP patterns; half literals always use units.
    internal bool HasSurrogateLiteral { get; }
    internal bool HasUnicodePatternAtoms { get; }

    // The compiled .NET 10 lazy-loop defect is excluded from our semantics.
    // Route these programs through the owned VM even when a native plan exists.
    internal bool UsesInterpretedLazyLoopSemantics { get; }

    // R05: whole-pattern greedy minimum-zero loops over halves-closed atoms
    // (dots and member-free unfolded negated sets). The unit-space runner tiles
    // these unit-for-unit with the oracle (empties only ever arise at ASCII
    // positions), so string-space splicing serves them exactly like the byte
    // cores. Every other empty-capable shape keeps the string-core BCL route.
    internal bool HalvesClosedEmptyLoop { get; }

    // Legacy fallback taxonomy, probed: backreference outranks conditional,
    // conditional outranks lookaround, lookaround outranks atomic, atomic outranks
    // loop, and balance-only or degenerate shapes report the residual. Capturing
    // lookbehinds lower inline with SawCapturingLookbehind marked, but the reason
    // follows the same precedence as the pipeline classifier (which keys off tree
    // features), so the program mapping agrees with the pipeline oracle.
    // Lookarounds mark lowering flags (they
    // lower inline or to assertion bodies depending on position, so neither
    // instructions nor assertions see them all); bare Commit rides lookahead
    // arms, so only AtomicCommit marks atomicity.
    internal string LegacyFallbackReason
    {
        get
        {
            var loop = false;
            var atomic = false;
            var lookaround = SawLookaround;
            var conditional = HasConditionalTest;
            var backreference = false;
            foreach (var instruction in Instructions)
            {
                switch (instruction.Kind)
                {
                    case Utf8SliceInstructionKind.Backref:
                        backreference = true;
                        break;
                    case Utf8SliceInstructionKind.TestGroup:
                        conditional = true;
                        break;
                    case Utf8SliceInstructionKind.AtomicCommit:
                        atomic = true;
                        break;
                    case Utf8SliceInstructionKind.RepeatGreedy:
                    case Utf8SliceInstructionKind.RepeatGreedyUtf16:
                    case Utf8SliceInstructionKind.RepeatLazy:
                        loop = true;
                        break;
                    default:
                        break;
                }
            }

            if (backreference)
            {
                return "unsupported_backreference";
            }

            if (conditional)
            {
                return "unsupported_conditional";
            }

            if (lookaround)
            {
                return "unsupported_lookaround";
            }

            if (atomic)
            {
                return "unsupported_atomic";
            }

            if (loop)
            {
                return "unsupported_loop";
            }

            return "unsupported_pattern";
        }
    }
    internal int[] GroupNumbers { get; }
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

    // R03 category marker: the shared runner applies Unicode property
    // semantics (general categories/groups and named blocks beyond ASCII).
    // Consumers whose native properties are ASCII-scoped or unsupported
    // decline marked programs.
    internal bool RequiresUnicodeCategory { get; }

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
        return Lower(root, groupNames, maintainBalance, matchCaseInsensitive, rightToLeft, null);
    }

    internal static Utf8SliceProgram? Lower(Utf8SliceNode root, string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool rightToLeft, Utf8CaptureCatalog? captureCatalog)
        => Lower(root, groupNames, maintainBalance, matchCaseInsensitive, rightToLeft, captureCatalog, false, false);

    internal static Utf8SliceProgram? Lower(Utf8SliceNode root, string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool rightToLeft, Utf8CaptureCatalog? captureCatalog, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax)
    {
        var lowerer = new Lowering(groupNames, maintainBalance, matchCaseInsensitive, rightToLeft);
        lowerer.CaptureCatalog = captureCatalog;
        // Right-to-left programs execute backreferences before their groups in
        // reversed order, so seed the slot count from the final group tally
        // (parser-validated digits always land inside it); forward programs grow
        // it in encounter order exactly as before.
        if (groupNames is not null)
        {
            lowerer.GroupCount = groupNames.Length - 1;
        }
        static bool NodeCanMatchEmpty(Utf8SliceNode node)
        {
            switch (node)
            {
                case Utf8SliceNode.EmptyNode:
                case Utf8SliceNode.AnchorNode:
                case Utf8SliceNode.BoundaryNode:
                case Utf8SliceNode.LookaheadNode:
                case Utf8SliceNode.LookbehindNode:
                case Utf8SliceNode.BackrefNode:
                case Utf8SliceNode.BalanceNode:
                    return true;
                case Utf8SliceNode.ConditionalNode conditionalEmpty:
                    return NodeCanMatchEmpty(conditionalEmpty.YesBody) ||
                        NodeCanMatchEmpty(conditionalEmpty.NoBody);
                case Utf8SliceNode.FailNode:
                case Utf8SliceNode.LiteralNode:
                case Utf8SliceNode.SetNode:
                case Utf8SliceNode.DotNode:
                case Utf8SliceNode.ShorthandNode:
                case Utf8SliceNode.CategoryNode:
                case Utf8SliceNode.CategorySetNode:
                    return false;
                case Utf8SliceNode.CaptureNode capture:
                    return NodeCanMatchEmpty(capture.Body);
                case Utf8SliceNode.AtomicNode atomic:
                    return NodeCanMatchEmpty(atomic.Body);
                case Utf8SliceNode.ConcatNode concat:
                    foreach (var child in concat.Children)
                    {
                        if (!NodeCanMatchEmpty(child))
                        {
                            return false;
                        }
                    }
                    return true;
                case Utf8SliceNode.AlternationNode alternation:
                    foreach (var branch in alternation.Branches)
                    {
                        var branchEmpty = true;
                        foreach (var child in branch)
                        {
                            if (!NodeCanMatchEmpty(child))
                            {
                                branchEmpty = false;
                                break;
                            }
                        }
                        if (branchEmpty)
                        {
                            return true;
                        }
                    }
                    return false;
                case Utf8SliceNode.RepeatNode repeat:
                    if (repeat.Minimum == 0)
                    {
                        return true;
                    }
                    return NodeCanMatchEmpty(repeat.Body);
                default:
                    return true;
            }
        }
        var canMatchEmpty = NodeCanMatchEmpty(root);
        if (!lowerer.EmitNode(root))
        {
            return null;
        }

        lowerer.Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Accept, 0, 0, 0, 0));
        var requiresAstralFreeInput = false;
        var requiresUnicodeWordBoundary = false;
        var requiresUnicodeShorthand = false;
        var requiresUnicodeCategory = false;
        static void Classify(
            Utf8SliceInstruction instruction,
            ref bool astral,
            ref bool word,
            ref bool shorthand,
            ref bool category)
        {
            if (instruction.Kind == Utf8SliceInstructionKind.Dot ||
                (instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.A is >= 0xD800 and <= 0xDFFF) ||
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

            // Categories always carry Unicode property semantics (even ASCII
            // members match through categories), so they decline
            // ASCII-scoped consumers. The shared runner tests BMP scalars
            // while the oracle tests astral halves, so every category
            // program needs astral-free input.
            if (instruction.Kind == Utf8SliceInstructionKind.Category)
            {
                astral = true;
                category = true;
            }

            // Mixed category classes always carry Unicode property semantics
            // (even ASCII-only mixes match non-ASCII members), so they
            // decline ASCII-scoped consumers exactly like hybrids. The
            // shared runner tests BMP scalars while the oracle tests astral
            // halves, so every category-set program needs astral-free input.
            if (instruction.Kind == Utf8SliceInstructionKind.CategorySet)
            {
                astral = true;
                shorthand = true;
            }
        }

        foreach (var instruction in lowerer.Instructions)
        {
            Classify(instruction, ref requiresAstralFreeInput, ref requiresUnicodeWordBoundary, ref requiresUnicodeShorthand, ref requiresUnicodeCategory);
            if (requiresAstralFreeInput && requiresUnicodeWordBoundary && requiresUnicodeShorthand && requiresUnicodeCategory)
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

            // Hoist complete nested facts, including assertions below this body.
            requiresAstralFreeInput |= assertion.Body.RequiresAstralFreeInput;
            requiresUnicodeWordBoundary |= assertion.Body.RequiresUnicodeWordBoundary;
            requiresUnicodeShorthand |= assertion.Body.RequiresUnicodeShorthand;
            requiresUnicodeCategory |= assertion.Body.RequiresUnicodeCategory;
            hasNonFixedLookbehind |= assertion.Body.HasNonFixedLookbehind;
            lowerer.HasConditionalTest |= assertion.Body.HasConditionalTest;
            lowerer.SawCapturingLookbehind |= assertion.Body.SawCapturingLookbehind;
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

        // Whole-pattern greedy or lazy unbounded loops over unit-closed atoms
        // run scalarwise exactly like BCL units on astral input, so they clear
        // the astral-free requirement the instruction scan above may have set.
        requiresAstralFreeInput = requiresAstralFreeInput && !IsAstralIndifferentWholeLoop(root, rightToLeft);
        var halvesClosedEmptyLoop = IsHalvesClosedEmptyWholeLoop(root, rightToLeft);

        return new Utf8SliceProgram(
            lowerer.Instructions.ToArray(),
            lowerer.SetMasks.ToArray(),
            lowerer.CategoryTables.ToArray(),
            lowerer.CategorySetMembers.ToArray(),
            lowerer.RepeatCount,
            lowerer.GroupCount,
            requiresAstralFreeInput,
            requiresUnicodeWordBoundary,
            requiresUnicodeShorthand,
            requiresUnicodeCategory,
            assertions,
            hasNonFixedLookbehind, resolvedNames, hasUnnamedGroups && hasNamedGroups, maintainBalance, lowerer.MatchCaseInsensitive, lowerer.SawContiguousAnchor, lowerer.HasConditionalTest, rightToLeft, lowerer.SawLookaround, lowerer.SawCapturingLookbehind, canMatchEmpty, halvesClosedEmptyLoop, captureCatalog, usesInterpretedLazyLoopSemantics, usesLiteralClassBracketSyntax);
    }

    // R05: recognizes whole-pattern greedy or lazy unbounded loops whose scalar
    // stepping matches BCL unit stepping on every input, making astral-free
    // routing unnecessary. BCL can only start such a loop at a scalar boundary (a
    // mid-scalar start needs the lead half to fail every branch while the trail
    // half matches one, impossible when every branch is a single atom: positive
    // atoms fail both halves while dots and negations match both), and the loop
    // only stops at scalar boundaries (dots never fail; negations fail ASCII-only;
    // ASCII atoms fail identically in both). Multi-atom branches with a divergent
    // atom desynchronize mid-iteration (probed: ([^x]b)+ splits astral scalars in
    // BCL but not in the runner), so only single atoms or fully indifferent
    // branches qualify; nested captures would record different last-iteration spans
    // and stay out, as do empty-capable bounds and right-to-left. Lazy loops
    // transfer the same argument through first stops: minimum one is fenced
    // above, so the first expansion leaves an indifferent head by one unit
    // (BCL) or one scalar (runner), and both stop at the first scalar
    // boundary where the indifferent tail meets, since tails only ever meet
    // ASCII bytes while halves miss them exactly like BCL; wider expansions
    // stay boundary-locked for the same reason, so every reachable success
    // span is boundary-to-boundary on both sides and enumeration order
    // agrees. Tailless lazy loops stay out: with no tail forcing a boundary
    // stop, BCL succeeds minimally after one half (probed: `(.+?)` on astral
    // input replaces four times for BCL against three whole-scalar runner
    // matches), which scalar stepping cannot reproduce. Folding needs no
    // fence: it only refines which BMP scalars
    // meet ASCII atoms through the invariant pair (Kelvin included, still one
    // scalar per BCL unit), while admitted atoms stay byte/mask-based or
    // fold-neutral, the runner fold arms fail astral leads closed, and BCL
    // halves miss positives while meeting negations, so the boundary argument
    // holds folded exactly as unfolded. ASCII head/tail runs around the loop
    // stay byte-aligned in both semantics for the same reason, so one checked
    // loop with indifferent runs keeps the argument. Anything unmodeled fails
    // closed to existing routing.
    // R05: recognizes bare whole-pattern greedy star loops over halves-closed
    // atoms for the string-core empty-capable arm (see HalvesClosedEmptyLoop).
    // Only the shapes the indifference gate already clears for the byte cores
    // qualify: captures, atomics, and flag-prefix single-survivor alternations
    // unwrap transparently, and the surviving repeat must be greedy, unbounded,
    // minimum-zero, body-safe, and halves-closed. Head/tail runs, lazy loops,
    // higher minimums, and divergent bodies stay out exactly like the byte-side
    // gate; anything unmodeled fails closed.
    private static bool IsHalvesClosedEmptyWholeLoop(Utf8SliceNode root, bool rightToLeft)
    {
        if (rightToLeft)
        {
            return false;
        }

        var node = root;
        while (true)
        {
            while (node is Utf8SliceNode.CaptureNode capture)
            {
                node = capture.Body;
            }

            while (node is Utf8SliceNode.AtomicNode atomic)
            {
                node = atomic.Body;
            }

            if (node is not Utf8SliceNode.AlternationNode alternation ||
                alternation.Branches.Length != 1)
            {
                break;
            }

            Utf8SliceNode? survivor = null;
            foreach (var member in alternation.Branches[0])
            {
                if (member is Utf8SliceNode.EmptyNode)
                {
                    continue;
                }

                if (survivor is not null)
                {
                    return false;
                }

                survivor = member;
            }

            if (survivor is null)
            {
                return false;
            }

            node = survivor;
        }

        return node is Utf8SliceNode.RepeatNode repeat &&
            repeat.Minimum == 0 && repeat.Maximum == int.MaxValue && !repeat.Lazy &&
            WholeLoopBodyIsSafe(repeat.Body) && WholeLoopBodyMatchesHalves(repeat.Body);
    }

    private static bool IsAstralIndifferentWholeLoop(Utf8SliceNode root, bool rightToLeft)
    {
        if (rightToLeft)
        {
            return false;
        }

        var node = root;
        var lazySawTailAtom = false;
        while (true)
        {
            // Lazy loops additionally need a real tail atom after the loop:
            // literals, empty positive sets, and non-contiguous anchors force
            // boundary stops, while a bare loop or a head-only loop lets BCL
            // stop after one half. The flag is never reset: levels without a
            // tail scan only unwrap transparent captures, atomics, or
            // single-survivor sequences, so a scanned tail stays after the
            // loop; a level with its own scan overwrites it. Greedy loops
            // never need it (maximal swallowing stays boundary-safe) and the
            // all-indifferent early return below has no loop at all.
            // Captures and atomics are transparent to the stepping argument:
            // captures only record spans while atomicity only prunes
            // backtracking, which whole-pattern greedy loops never need on
            // either side, so both unwrap exactly like the bare shape.
            while (true)
            {
                if (node is Utf8SliceNode.CaptureNode capture)
                {
                    node = capture.Body;
                    continue;
                }

                if (node is Utf8SliceNode.AtomicNode atomic)
                {
                    node = atomic.Body;
                    continue;
                }

                break;
            }

            // A bare flag prefix like (?i) parses to a leading EmptyNode, so
            // flag-prefixed shapes root at a single-branch alternation whose
            // members form the real sequence (the parser keeps concatenations
            // as flat branch arrays). One surviving node continues descent
            // below; several survivors take the head/tail rule instead.
            if (node is not Utf8SliceNode.AlternationNode alternation ||
                alternation.Branches.Length != 1)
            {
                break;
            }

            var survivors = 0;
            foreach (var member in alternation.Branches[0])
            {
                if (member is not Utf8SliceNode.EmptyNode)
                {
                    survivors++;
                }
            }

            if (survivors == 0)
            {
                return false;
            }

            if (survivors == 1)
            {
                foreach (var member in alternation.Branches[0])
                {
                    if (member is not Utf8SliceNode.EmptyNode)
                    {
                        node = member;
                        break;
                    }
                }

                continue;
            }

            // Head/tail runs around one loop: leading and trailing indifferent
            // atoms (ASCII literals or empty positive sets, folded or not)
            // only ever meet ASCII bytes in both semantics while halves miss
            // them exactly like BCL, and line/absolute/contiguous anchors only
            // ever meet ASCII-determined positions (starts, ends, newline edges,
            // chained origins) in both semantics, so the loop keeps its
            // scalar-boundary start/stop argument and the runs stay aligned
            // throughout (origins are scan starts and prior match ends, hence
            // boundaries for indifferent shapes on every lane including
            // FromOffset). Word boundaries join as head/tail atoms: BCL meets
            // them only at scalar boundaries (a hold needs a full-scalar word
            // neighbor, never a half), where the runner evaluates the same
            // transition, so the loop keeps its argument and lazy tails stay
            // pinned. Negated edges stay out: both halves are non-word, so BCL
            // meets them mid-scalar where scalar stepping cannot follow.
            // Anything but one checked loop left standing fails closed.
            var members = alternation.Branches[0];
            var head = 0;
            while (head < members.Length &&
                (members[head] is Utf8SliceNode.EmptyNode || WholeLoopAtomIsIndifferent(members[head]) ||
                members[head] is Utf8SliceNode.AnchorNode ||
                (members[head] is Utf8SliceNode.BoundaryNode headEdge && headEdge.IsWordBoundary)))
            {
                head++;
            }

            var tail = members.Length - 1;
            while (tail >= head &&
                (members[tail] is Utf8SliceNode.EmptyNode || WholeLoopAtomIsIndifferent(members[tail]) ||
                members[tail] is Utf8SliceNode.AnchorNode ||
                (members[tail] is Utf8SliceNode.BoundaryNode tailEdge && tailEdge.IsWordBoundary)))
            {
                if (members[tail] is not Utf8SliceNode.EmptyNode)
                {
                    lazySawTailAtom = true;
                }

                tail--;
            }

            if (tail < head)
            {
                return true;
            }

            if (tail != head)
            {
                return false;
            }

            // One node left standing (a loop, or a capture around the
            // remaining sequence like ^(a[^x]+b)$): descend and let the loop
            // above unwrap it; anything else fails closed at the Repeat gate
            // below exactly like the bare shape.
            node = members[head];
            continue;
        }

        // Minimum one stays exact under the boundary argument above. Higher
        // greedy minimums over one halves-closed atom use a UTF-16 counter:
        // consuming an astral scalar contributes two units. The unbounded
        // greedy stop and indifferent head/tail keep every span aligned;
        // bodies with iteration captures, multiple atoms, and lazy stops
        // still need actual half-unit execution and stay out.
        if (node is not Utf8SliceNode.RepeatNode repeat || repeat.Maximum != int.MaxValue ||
            (repeat.Lazy && !lazySawTailAtom))
        {
            return false;
        }

        if (repeat.Minimum == 1)
        {
            return WholeLoopBodyIsSafe(repeat.Body);
        }

        if (repeat.Minimum > 1 && !repeat.Lazy && CanCountWholeLoopUtf16Units(repeat.Body))
        {
            return true;
        }

        // R05: greedy minimum-zero loops over halves-closed atoms (dots and
        // member-free unfolded negated sets, which meet every surrogate half in
        // both semantics) stay boundary-locked exactly like minimum-one loops:
        // empties only ever arise at ASCII positions (halves always match the
        // first iteration), non-empty matches end at scalar boundaries on both
        // sides, and the one-unit/one-scalar empty advance coincides there, so
        // global tiling agrees unit-for-unit. Lazy minimum-zero loops stay out
        // (BCL stops minimally after one half with no tail forcing a boundary
        // stop, exactly like tailless lazy plus), as does every other
        // body (positive sets, shorthands, categories, and hybrids can all meet
        // empty mid-scalar where scalar stepping cannot follow).
        if (repeat.Minimum == 0 && !repeat.Lazy &&
            WholeLoopBodyIsSafe(repeat.Body) && WholeLoopBodyMatchesHalves(repeat.Body))
        {
            return true;
        }

        return false;
    }

    // One atom per iteration, with no captures inside the body. The existing
    // atom qualifier proves uniform surrogate-half behavior for its scalar
    // arm: admitted astral scalars contribute two units; atoms missing both
    // halves never consume them. Transparent singleton sequences preserve
    // that argument. Captures surrounding the whole loop stay exact.
    private static bool CanCountWholeLoopUtf16Units(Utf8SliceNode node)
    {
        while (true)
        {
            switch (node)
            {
                case Utf8SliceNode.AlternationNode { Branches.Length: 1 } alternation when alternation.Branches[0].Length == 1:
                    node = alternation.Branches[0][0];
                    continue;
                case Utf8SliceNode.ConcatNode { Children.Length: 1 } concat:
                    node = concat.Children[0];
                    continue;
                case Utf8SliceNode.LiteralNode literal:
                    return literal.Value is < 0xD800 or > 0xDFFF;
                case Utf8SliceNode.DotNode:
                case Utf8SliceNode.SetNode:
                case Utf8SliceNode.ShorthandNode:
                    // ASCII masks miss both halves; every shorthand member
                    // has the same truth value on lead and trail surrogates.
                    return true;
                case Utf8SliceNode.CategoryNode category:
                    return Utf8SliceHalvesMirror.TableHalvesVerdict(category.Mask, category.Ranges) != 2;
                default:
                    return WholeLoopAtomIsSafe(node);
            }
        }
    }

    private static bool WholeLoopBodyIsSafe(Utf8SliceNode node)
    {
        if (node is Utf8SliceNode.AlternationNode alternation)
        {
            if (alternation.Branches.Length == 0)
            {
                return false;
            }

            foreach (var branch in alternation.Branches)
            {
                if (!WholeLoopBranchIsSafe(branch))
                {
                    return false;
                }
            }

            return true;
        }

        return WholeLoopBranchIsSafe([node]);
    }

    private static bool WholeLoopBranchIsSafe(Utf8SliceNode[] branch)
    {
        if (branch.Length == 0)
        {
            return false;
        }

        if (branch.Length == 1)
        {
            return WholeLoopAtomIsSafe(branch[0]);
        }

        foreach (var atom in branch)
        {
            if (!WholeLoopAtomIsIndifferent(atom))
            {
                return false;
            }
        }

        return true;
    }

    // Halves-closed bodies for greedy minimum-zero loops: every atom meets
    // every surrogate half in both semantics (dots fail ASCII-only newlines;
    // member-free unfolded negated sets exclude ASCII-only positives, which
    // halves never meet), so the first iteration can never fail mid-scalar.
    // Shorthands, hybrids, categories, positive sets, and folded sets stay out:
    // their runner arms may fail astral leads the oracle halves would meet
    // (or vice versa), which desynchronizes empty-match tiling.
    private static bool WholeLoopBodyMatchesHalves(Utf8SliceNode node)
    {
        if (node is Utf8SliceNode.AlternationNode alternation)
        {
            if (alternation.Branches.Length == 0)
            {
                return false;
            }

            foreach (var branch in alternation.Branches)
            {
                if (!WholeLoopBranchMatchesHalves(branch))
                {
                    return false;
                }
            }

            return true;
        }

        return WholeLoopBranchMatchesHalves([node]);
    }

    private static bool WholeLoopBranchMatchesHalves(Utf8SliceNode[] branch)
    {
        if (branch.Length == 0)
        {
            return false;
        }

        foreach (var atom in branch)
        {
            if (!WholeLoopAtomMatchesHalves(atom))
            {
                return false;
            }
        }

        return true;
    }

    private static bool WholeLoopAtomMatchesHalves(Utf8SliceNode node)
    {
        switch (node)
        {
            case Utf8SliceNode.DotNode:
                return true;
            case Utf8SliceNode.SetNode set:
                return set.Negated && !set.FoldCase && (set.Members is null || set.Members.Length == 0);
            default:
                return false;
        }
    }

    private static bool WholeLoopAtomIsSafe(Utf8SliceNode node)
    {
        switch (node)
        {
            case Utf8SliceNode.LiteralNode literal:
                return literal.Value is < 0xD800 or > 0xDFFF;
            case Utf8SliceNode.DotNode:
                return true;
            case Utf8SliceNode.SetNode set:
                if (set.Negated)
                {
                    return true;
                }

                if (set.Members is null)
                {
                    return false;
                }

                foreach (var member in set.Members)
                {
                    if ((int)member % 2 == 0)
                    {
                        return false;
                    }
                }

                return true;
            case Utf8SliceNode.ShorthandNode shorthand:
                return (int)shorthand.Shorthand % 2 == 1;
            // Category sets mirror halves through the shared verdict: the runner
            // consumes exactly when halves match, so the first-stop argument
            // transfers for every uniform set (miss-both or match-both members
            // alike, either parity). Split members stay out since lead and trail
            // halves would desynchronize iteration like divergent branches.
            case Utf8SliceNode.CategorySetNode categorySet:
                if (categorySet.Members is null || categorySet.Categories is null)
                {
                    return false;
                }

                var categorySetMemberBits = 0;
                foreach (var shorthandMember in categorySet.Members)
                {
                    categorySetMemberBits |= 1 << (int)shorthandMember;
                }

                return Utf8SliceHalvesMirror.CategorySetHalvesVerdict(categorySetMemberBits, categorySet.Categories) != 2;
            // The standing minimum-one qualifier admits tables whose positive
            // side misses both halves. The higher-minimum qualifier above
            // additionally admits uniformly matching tables such as Cs through
            // the runner's shared half verdict. Split surrogate blocks remain
            // outside both aligned-loop qualifiers.
            case Utf8SliceNode.CategoryNode category:
                return PositiveTableMissesHalves(category.Mask, category.Ranges);
            default:
                return false;
        }
    }

    // Positive tables miss halves unless they cover surrogates (the Cs mask
    // bit, or a block range overlapping U+D800-U+DFFF). Keep the conservative
    // minimum-one gate separate from the higher-minimum uniform-half proof.
    private static bool PositiveTableMissesHalves(uint mask, ushort[] ranges)
    {
        if ((mask & (1u << (int)System.Globalization.UnicodeCategory.Surrogate)) != 0)
        {
            return false;
        }

        for (var range = 0; range + 1 < ranges.Length; range += 2)
        {
            if (ranges[range] < 0xE000 && ranges[range + 1] > 0xD800)
            {
                return false;
            }
        }

        return (ranges.Length & 1) == 0 || ranges[ranges.Length - 1] > 0xDFFF;
    }

    private static bool WholeLoopAtomIsIndifferent(Utf8SliceNode node)
    {
        switch (node)
        {
            case Utf8SliceNode.LiteralNode literal:
                return literal.Value is < 0xD800 or > 0xDFFF;
            case Utf8SliceNode.SetNode set:
                return !set.Negated && (set.Members is null || set.Members.Length == 0);
            default:
                return false;
        }
    }

    private sealed class Lowering
    {
        internal readonly List<Utf8SliceInstruction> Instructions = new();
        internal readonly List<byte[]> SetMasks = new();
        internal readonly List<Utf8SliceNode.CategorySetMember[]> CategorySetMembers = new();
        internal readonly List<Utf8SliceCategoryTable> CategoryTables = new();
        internal int RepeatCount;
        internal int GroupCount;
        internal bool SawContiguousAnchor;
        internal bool HasConditionalTest;
        internal bool SawLookaround;
        internal bool SawCapturingLookbehind;
        internal readonly List<Utf8SliceAssertion> Assertions = new();
        internal readonly string[]? GroupNames;
        internal Utf8CaptureCatalog? CaptureCatalog;
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

            // Capturing lookbehinds force the residual legacy reason, so the
            // lookbehind arm detects group writes in the body tree. Balance
            // pops cannot reach owned bodies (they decline at parse), but
            // counting them keeps the predicate sound if that ever relaxes.
            static bool BodyWritesGroup(Utf8SliceNode candidate)
            {
                switch (candidate)
                {
                    case Utf8SliceNode.CaptureNode:
                    case Utf8SliceNode.BalanceNode:
                        return true;
                    case Utf8SliceNode.ConcatNode concat:
                        foreach (var child in concat.Children)
                        {
                            if (BodyWritesGroup(child))
                            {
                                return true;
                            }
                        }

                        break;
                    case Utf8SliceNode.AlternationNode alternation:
                        foreach (var branch in alternation.Branches)
                        {
                            foreach (var child in branch)
                            {
                                if (BodyWritesGroup(child))
                                {
                                    return true;
                                }
                            }
                        }

                        break;
                    case Utf8SliceNode.RepeatNode repeat:
                        return BodyWritesGroup(repeat.Body);
                    case Utf8SliceNode.AtomicNode atomic:
                        return BodyWritesGroup(atomic.Body);
                    case Utf8SliceNode.LookaheadNode lookaheadBody:
                        return BodyWritesGroup(lookaheadBody.Body);
                    case Utf8SliceNode.LookbehindNode lookbehindBody:
                        return BodyWritesGroup(lookbehindBody.Body);
                    case Utf8SliceNode.ConditionalNode conditional:
                        return BodyWritesGroup(conditional.YesBody) ||
                            BodyWritesGroup(conditional.NoBody) ||
                            (conditional.TestBody is not null && BodyWritesGroup(conditional.TestBody));
                    default:
                        break;
                }

                return false;
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
                    case Utf8SliceNode.LiteralNode literal:
                        var width = literal.Value < 0x80 ? 1 : literal.Value < 0x800 ? 2 : 3;
                        return literal.FoldCase ? (1, 3) : (width, width);
                    case Utf8SliceNode.SetNode set:
                        return set.Negated ? (1, 4) : (1, 1);
                    case Utf8SliceNode.DotNode:
                        return (1, 4);
                    case Utf8SliceNode.ShorthandNode shorthand:
                        return (int)shorthand.Shorthand % 2 == 1 ? (1, 4) : (1, 3);
                    case Utf8SliceNode.CategorySetNode:
                        // Served inputs hold no astral scalars (routing
                        // declines them), so every category-set atom spans
                        // one to three bytes; astral shapes keep legacy
                        // routing, exactly like single categories.
                        return (1, 3);
                    case Utf8SliceNode.CategoryNode:
                        // Served inputs hold no astral scalars (routing
                        // declines them), so every category atom spans one
                        // to three bytes; astral shapes keep legacy routing.
                        return (1, 3);
                    case Utf8SliceNode.BackrefNode:
                        return (0, int.MaxValue);
                    case Utf8SliceNode.CaptureNode capture:
                        return Measure(capture.Body);
                    case Utf8SliceNode.BalanceNode balance:
                        return Measure(balance.Body);
                    case Utf8SliceNode.AtomicNode atomic:
                        return Measure(atomic.Body);
                    case Utf8SliceNode.ConditionalNode conditional:
                        var (conditionalYesMinimum, conditionalYesMaximum) = Measure(conditional.YesBody);
                        var (conditionalNoMinimum, conditionalNoMaximum) = Measure(conditional.NoBody);
                        return (Math.Min(conditionalYesMinimum, conditionalNoMinimum), Math.Max(conditionalYesMaximum, conditionalNoMaximum));
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
                case Utf8SliceNode.FailNode:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
                    break;
                case Utf8SliceNode.LiteralNode literal:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Literal, literal.Value, literal.FoldCase ? 1 : 0, 0, 0));
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
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Set, maskIndex, set.Negated ? 1 : 0, set.FoldCase ? 1 : 0, 0));
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

                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Hybrid, maskIndex, set.Negated ? 1 : 0, memberMask, set.FoldCase ? 1 : 0));
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
                        repeat.Lazy ? Utf8SliceInstructionKind.RepeatLazy :
                        !MirrorExecute && repeat.Minimum > 1 && repeat.Maximum == int.MaxValue && CanCountWholeLoopUtf16Units(repeat.Body)
                            ? Utf8SliceInstructionKind.RepeatGreedyUtf16 : Utf8SliceInstructionKind.RepeatGreedy,
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
                case Utf8SliceNode.CategoryNode category:
                    // One table per atom (B holds the negated bit plus the
                    // IgnoreCase fold bit, which only widens Ll/Lu/Lt to
                    // their union at match time).
                    var categoryIndex = CategoryTables.Count;
                    CategoryTables.Add(new Utf8SliceCategoryTable(category.Mask, category.Ranges));
                    Emit(new Utf8SliceInstruction(
                        Utf8SliceInstructionKind.Category,
                        categoryIndex,
                        (category.Negated ? 1 : 0) | (category.FoldCase ? 2 : 0),
                        0,
                        0));
                    break;
                case Utf8SliceNode.CategorySetNode categorySet:
                    var categorySetMaskIndex = SetMasks.Count;
                    SetMasks.Add(categorySet.Mask16);
                    var categorySetMemberBits = 0;
                    foreach (var shorthandMember in categorySet.Members)
                    {
                        categorySetMemberBits |= 1 << (int)shorthandMember;
                    }

                    var categorySetMemberIndex = CategorySetMembers.Count;
                    CategorySetMembers.Add(categorySet.Categories);
                    Emit(new Utf8SliceInstruction(
                        Utf8SliceInstructionKind.CategorySet,
                        categorySetMaskIndex,
                        (categorySet.Negated ? 1 : 0) | (categorySet.FoldCase ? 2 : 0),
                        categorySetMemberBits,
                        categorySetMemberIndex));
                    break;
                case Utf8SliceNode.BackrefNode backref:
                    if (backref.Slot < 0 || backref.Slot >= GroupCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice backreferences require a defined group.");
                    }

                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Backref, backref.Slot, backref.FoldCase ? 1 : 0, 0, 0));
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

                    if (balance.CaptureSlot >= 0)
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.BalanceStart, balance.PopSlot, balance.CaptureSlot, 0, 0));
                    if (!EmitNode(balance.Body))
                    {
                        return false;
                    }
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.BalancePop, balance.PopSlot, balance.CaptureSlot, 0, 0));

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
                case Utf8SliceNode.ConditionalNode conditional:
                    if (conditional.TestBody is not null)
                    {
                        // Expression tests desugar to lookahead-guarded branches (the test
                        // is a positive lookaround per BCL); the marker preserves the
                        // distinction for consumers with different assertion capture flow.
                        HasConditionalTest = true;
                        if (!EmitNode(new Utf8SliceNode.AlternationNode(new[]
                        {
                            new Utf8SliceNode[] { new Utf8SliceNode.LookaheadNode(conditional.TestBody, true), conditional.YesBody },
                            new Utf8SliceNode[] { new Utf8SliceNode.LookaheadNode(conditional.TestBody, false), conditional.NoBody },
                        })))
                        {
                            return false;
                        }

                        break;
                    }

                    // One-shot group test with committed branches: the test reads current
                    // capture state and jumps to the no-branch on false; downstream
                    // failure never retries the other branch (BCL commits to the test).
                    var testIndex = Instructions.Count;
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.TestGroup, conditional.TestSlot, 0, 0, 0));
                    if (!EmitNode(conditional.YesBody))
                    {
                        return false;
                    }

                    var noJump = Instructions.Count;
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                    Instructions[testIndex] = new Utf8SliceInstruction(Utf8SliceInstructionKind.TestGroup, conditional.TestSlot, Instructions.Count, 0, 0);
                    if (!EmitNode(conditional.NoBody))
                    {
                        return false;
                    }

                    Patch(noJump, Instructions.Count);
                    break;
                case Utf8SliceNode.LookaheadNode lookahead:
                    SawLookaround = true;
                    // Right-to-left programs run the main stream mirrored while BCL
                    // parses lookahead bodies left-to-right, so mirrored mains serve
                    // lookahead bodies as left-to-right sub-programs (shared slots,
                    // zero width, captures persisting on positive survival exactly
                    // like lookbehind bodies).
                    if (MirrorExecute)
                    {
                        var lookaheadBody = Utf8SliceLowerer.Lower(lookahead.Body, GroupNames, MaintainBalance, MatchCaseInsensitive, false, CaptureCatalog);
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
                        SawLookaround = true;
                        if (BodyWritesGroup(lookbehind.Body))
                        {
                            SawCapturingLookbehind = true;
                        }
                        var body = Utf8SliceLowerer.Lower(lookbehind.Body, GroupNames, MaintainBalance, MatchCaseInsensitive, true, CaptureCatalog);
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
