using System.Buffers.Binary;
using System.Numerics;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

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

// Boundary instruction B stores word membership; its existing encodings stay
// Unicode=0 and ECMA=1. Strict ASCII excludes ECMA's U+0130 member.
internal enum Utf8SliceWordKind : byte
{
    Unicode,
    Ecma,
    Ascii,
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

/// <summary>Immutable scalar node lowered into a slice program.</summary>
internal abstract record Utf8SliceNode
{
    internal sealed record EmptyNode() : Utf8SliceNode;
    internal sealed record FailNode() : Utf8SliceNode;

    internal sealed record LiteralNode(int Value, bool FoldCase) : Utf8SliceNode;

    internal sealed record SetNode(byte[] Mask16, bool Negated, Utf8SliceShorthand[] Members, bool FoldCase) : Utf8SliceNode;

    internal sealed record ConcatNode(Utf8SliceNode[] Children) : Utf8SliceNode;

    internal sealed record AlternationNode(Utf8SliceNode[][] Branches) : Utf8SliceNode;

    // Finite bounds fit uint below UINT_MAX; long.MaxValue alone means unbounded.
    internal sealed record RepeatNode(Utf8SliceNode Body, long Minimum, long Maximum, bool Lazy) : Utf8SliceNode
    {
        // Mandatory iterations need not participate in the optional loop's
        // zero-progress guard. Python's REPEAT/UNTIL distinction requires this.
        internal bool GuardEmptyAfterMinimum { get; init; }
    }

    internal sealed record CaptureNode(int Slot, Utf8SliceNode Body) : Utf8SliceNode;

    internal sealed record DotNode(bool MatchNewline) : Utf8SliceNode;

    internal sealed record AnchorNode(Utf8SliceAnchorKind Anchor, bool MatchMultiline) : Utf8SliceNode;

    internal sealed record BoundaryNode(bool IsWordBoundary, Utf8SliceWordKind WordKind = Utf8SliceWordKind.Unicode)
        : Utf8SliceNode
    {
        // ASCII word membership is shared by all boundary kinds. The optional
        // fixed, bounded predicate handles only wider scalars.
        internal Func<int, bool>? NonAsciiWordPredicate { get; init; }
    }

    internal sealed record ShorthandNode(Utf8SliceShorthand Shorthand) : Utf8SliceNode;

    internal sealed record BackrefNode(int Slot, bool FoldCase, bool AllowUnmatched) : Utf8SliceNode
    {

        // An owned profile may supply a bounded simple-scalar lowercase table.
        internal Func<int, int>? LowerScalar { get; init; }
    }
    internal sealed record CategorySetMember
    {
        private CategorySetMember(Utf8SliceCategoryTable table, bool negated) { Table = table; Negated = negated; }
        internal Utf8SliceCategoryTable Table { get; }
        internal bool Negated { get; }
        internal static CategorySetMember Create(Utf8SliceCategoryTable table, bool negated)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var result = owner.Budget.AllocateObject((table, negated), static state => new CategorySetMember(state.table, state.negated));
            owner.Complete(); return result;
        }
    }
    internal sealed record CategorySetNode(byte[] Mask16, bool Negated, Utf8SliceShorthand[] Members, CategorySetMember[] Categories, bool FoldCase) : Utf8SliceNode;
    internal sealed record LookaheadNode(Utf8SliceNode Body, bool Positive) : Utf8SliceNode;
    internal sealed record LookbehindNode(Utf8SliceNode Body, bool Positive) : Utf8SliceNode
    {
        internal uint? ForwardScalarCount { get; init; }
    }
    internal sealed record BalanceNode(int PopSlot, int CaptureSlot, Utf8SliceNode Body) : Utf8SliceNode;
    internal sealed record AtomicNode(Utf8SliceNode Body) : Utf8SliceNode;
    internal sealed record ConditionalNode(int TestSlot, Utf8SliceNode YesBody, Utf8SliceNode NoBody, Utf8SliceNode? TestBody) : Utf8SliceNode;
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
    CategorySet = 25,
    BalanceStart = 27,
    RepeatGreedyAfterMinimum = 28,
    RepeatLazyAfterMinimum = 29,
}

/// <summary>Unicode property table for one owned category atom: a
/// UnicodeCategory bit mask for general categories/groups plus half-open
/// scalar-valued half-open range endpoint pairs for named blocks/literal ranges,
/// or one immutable class subtraction program.</summary>
internal sealed class Utf8SliceCategoryTable
{
    private Utf8SliceCategoryTable(uint mask, int[] ranges, Utf8SliceClassProgram? classProgram, Func<int, bool>? scalarPredicate)
    { Mask = mask; Ranges = ranges; ClassProgram = classProgram; ScalarPredicate = scalarPredicate; }

    internal static ulong ObjectBytes => Utf8ConstructionBudget.ObjectBytes<(uint, int[], Utf8SliceClassProgram?, Func<int, bool>?)>();

    internal static Utf8SliceCategoryTable Create(uint mask, ushort[] ranges)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var copied = owner.Budget.AllocateArray<int>(checked(ranges.Length + (ranges.Length & 1)), false);
        owner.Budget.ChargeWork((ulong)ranges.Length + (ulong)(ranges.Length & 1));
        for (var index = 0; index < ranges.Length; index++) copied[index] = ranges[index];
        if ((ranges.Length & 1) != 0) copied[^1] = 0x10000;
        var result = owner.Budget.AllocateObject((mask, copied, ClassProgram: (Utf8SliceClassProgram?)null, Predicate: (Func<int, bool>?)null),
            static state => new Utf8SliceCategoryTable(state.mask, state.copied, state.ClassProgram, state.Predicate));
        owner.Complete(); return result;
    }

    internal static Utf8SliceCategoryTable ForScalarRanges(int[] ranges)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = owner.Budget.AllocateObject((Mask: 0u, ranges, ClassProgram: (Utf8SliceClassProgram?)null, Predicate: (Func<int, bool>?)null),
            static state => new Utf8SliceCategoryTable(state.Mask, state.ranges, state.ClassProgram, state.Predicate));
        owner.Complete(); return result;
    }

    internal static Utf8SliceCategoryTable Create(Utf8SliceClassProgram classProgram)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = owner.Budget.AllocateObject((Mask: 0u, Ranges: Array.Empty<int>(), classProgram, Predicate: (Func<int, bool>?)null),
            static state => new Utf8SliceCategoryTable(state.Mask, state.Ranges, state.classProgram, state.Predicate));
        owner.Complete(); return result;
    }

    // An owned profile can supply a fixed, bounded, invocation-free predicate.
    // It includes its lexical case semantics; callers still charge the existing
    // class instruction and retain the original cancellation/deadline authority.
    internal static Utf8SliceCategoryTable ForScalarPredicate(Func<int, bool> predicate)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = owner.Budget.AllocateObject((Mask: 0u, Ranges: Array.Empty<int>(), ClassProgram: (Utf8SliceClassProgram?)null, predicate),
            static state => new Utf8SliceCategoryTable(state.Mask, state.Ranges, state.ClassProgram, state.predicate));
        owner.Complete(); return result;
    }

    internal uint Mask { get; }
    internal int[] Ranges { get; }
    internal Utf8SliceClassProgram? ClassProgram { get; }
    internal Func<int, bool>? ScalarPredicate { get; }
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
    // RepeatExit uses a positive B as an exclusive byte bound to the input end.
    // A nonzero RepeatEnd B packs three ASCII stop bytes for its guarded fallback.
    internal int B { get; }
    // A backward Jump can carry an excluded starting ASCII byte plus one in C.
    // A normal Fork can carry exclusive remaining byte bounds in C and D
    // for the fall-through and target respectively. Zero leaves an arm unknown.
    internal int C { get; }
    // For a positive unfolded Set, packed ASCII interval endpoints:
    // low byte is first+1, high byte is last; zero means no exact interval.
    internal int D { get; }

    // On a RepeatExit, C marks a greedy ASCII run whose following arms
    // all reject shorter endpoints.
    internal bool RepeatExitRejectsShorterEndpoints => C != 0;

    // Only repeat instructions interpret C/D as unsigned bounds. Keep the
    // existing instruction layout; UINT_MAX is their distinct infinity marker.
    internal long RepeatMinimum => unchecked((uint)C);
    internal long RepeatMaximum => D == -1 ? long.MaxValue : unchecked((uint)D);

    internal static bool TryGetLiteralAfterCaptureMarkers(ReadOnlySpan<Utf8SliceInstruction> instructions,
        int index, out int scalar)
    {
        index = SkipLeadingCaptureMarkers(instructions, index);
        if ((uint)index < (uint)instructions.Length &&
            instructions[index] is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } literal &&
            System.Text.Rune.IsValid(literal.A))
        {
            scalar = literal.A;
            return true;
        }
        scalar = -1;
        return false;
    }

    internal static int SkipLeadingCaptureMarkers(ReadOnlySpan<Utf8SliceInstruction> instructions, int index)
    {
        for (var skipped = 0; skipped < 31 && (uint)index < (uint)instructions.Length &&
            instructions[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd; skipped++)
            index++;
        return index;
    }
}

/// <summary>Immutable lowered slice program: instruction array, ASCII set masks,
/// repeat count, and capture slot count. All mutable run state is invocation-owned.</summary>
/// <summary>Lowered lookbehind body: a private instruction stream with local
/// repeat ids and set masks, plus byte-length bounds for the start search.
/// The body shares the parent capture slots at run time.</summary>
internal sealed class Utf8SliceAssertion
{
    private Utf8SliceAssertion(Utf8SliceProgram body, int minLengthBytes, int maxLengthBytes, bool isLookbehind,
        uint? forwardScalarCount, int inlineLiteralPrefixLength)
    {
        Body = body;
        MinLengthBytes = minLengthBytes;
        MaxLengthBytes = maxLengthBytes;
        IsLookbehind = isLookbehind;
        ForwardScalarCount = forwardScalarCount;
        InlineLiteralPrefixLength = inlineLiteralPrefixLength;
    }

    internal static ulong ObjectBytes => Utf8ConstructionBudget.ObjectBytes<(Utf8SliceProgram, int, int, bool, uint?, int)>();
    internal static Utf8SliceAssertion Create(Utf8SliceProgram body, int minLengthBytes, int maxLengthBytes, bool isLookbehind)
        => Create(body, minLengthBytes, maxLengthBytes, isLookbehind, null);

    internal static Utf8SliceAssertion Create(Utf8SliceProgram body, int minLengthBytes, int maxLengthBytes, bool isLookbehind, uint? forwardScalarCount)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var code = body.Instructions;
        var inlineLiteralPrefixLength = code.Length != 0 && code[^1].Kind == Utf8SliceInstructionKind.Accept
            ? FindInlineLiteralPrefixLength(code, 0, code.Length - 1, owner.Budget) : -1;
        var result = owner.Budget.AllocateObject(
            (body, minLengthBytes, maxLengthBytes, isLookbehind, forwardScalarCount, inlineLiteralPrefixLength),
            static state => new Utf8SliceAssertion(state.body, state.minLengthBytes, state.maxLengthBytes,
                state.isLookbehind, state.forwardScalarCount, state.inlineLiteralPrefixLength));
        owner.Complete(); return result;
    }

    internal static int FindInlineLiteralPrefixLength(ReadOnlySpan<Utf8SliceInstruction> code,
        int first, int end, Utf8ConstructionBudget budget)
    {
        var length = end - first;
        if (length is < 1 or > 32) return -1;
        budget.ChargeWork((ulong)length);
        var prefixEnd = EndOfAsciiLiterals(code, first, end);
        var position = prefixEnd;
        while (position < end)
        {
            // Flat alternatives rejoin at the assertion end. No path can update captures,
            // repeat or suspend the parent for a nested assertion.
            if (code[position] is not { Kind: Utf8SliceInstructionKind.Fork, B: 0 } choice ||
                choice.A <= position + 1 || choice.A > end ||
                EndOfAsciiLiterals(code, position + 1, end) != choice.A - 1 ||
                code[choice.A - 1].Kind != Utf8SliceInstructionKind.Jump || code[choice.A - 1].A != end)
                return -1;
            position = EndOfAsciiLiterals(code, choice.A, end);
            if (position != end && position != choice.A) return -1;
        }
        return prefixEnd - first;
    }

    private static int EndOfAsciiLiterals(ReadOnlySpan<Utf8SliceInstruction> code, int position, int end)
    {
        while (position < end && code[position] is { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 })
            position++;
        return position;
    }

    internal Utf8SliceProgram Body { get; }
    internal int MinLengthBytes { get; }
    internal int MaxLengthBytes { get; }
    internal bool IsLookbehind { get; }
    internal uint? ForwardScalarCount { get; }
    internal int InlineLiteralPrefixLength { get; }
}

internal sealed class Utf8SliceProgram
{

    // Conservative carrier allowance; major children have separate admission.
    internal static ulong ObjectBytes => 512;

    private Utf8SliceProgram(
        Utf8SliceInstruction[] instructions,
        bool isNonBacktracking,
        int nonBacktrackingCaptureOpeningCount,
        int[] nonBacktrackingEpsilonOrder,
        ulong[] nonBacktrackingEpsilonAncestors,
        ulong[] nonBacktrackingOrderedClosures,
        bool nonBacktrackingOrderedForwardOnly,
        ulong nonBacktrackingConsumerMask,
        ulong nonBacktrackingAcceptClosure,
        Func<int, int>[] backreferenceLowerings,
        byte[][] setMasks,
        Utf8SliceNode.CategorySetMember[][] categorySetMembers,
        int repeatCount,
        int groupCount,
        Utf8SliceAssertion[] assertions,
        bool hasNonFixedLookbehind,
        bool hasOrderSensitiveLookbehind,
        bool usesLiteralClassBracketSyntax,
        bool hasMixedNamedGroups,
        Utf8CaptureCatalog? captureCatalog,
        bool usesCaptureOpeningJournal,
        bool readsCaptures,
        bool hasBalancing,
        bool ignoreCase,
        Utf8ScalarCaseBehavior caseBehavior,
        bool hasContiguousAnchor,
        bool hasConditionalTest,
        bool sawLookaround,
        bool sawCapturingLookbehind,
        bool rightToLeft,
        bool canMatchEmpty,
        bool hasUnicodePatternAtoms,
        bool usesInterpretedLazyLoopSemantics,
        int[] groupNumbers,
        string[] groupNames,
        bool requiresUnicodeWordBoundary,
        bool requiresUnicodeShorthand,
        bool requiresUnicodeCategory)
    {
        Instructions = instructions;
        IsNonBacktracking = isNonBacktracking;
        NonBacktrackingCaptureOpeningCount = nonBacktrackingCaptureOpeningCount;
        NonBacktrackingEpsilonOrder = nonBacktrackingEpsilonOrder;
        NonBacktrackingEpsilonAncestors = nonBacktrackingEpsilonAncestors;
        NonBacktrackingOrderedClosures = nonBacktrackingOrderedClosures;
        NonBacktrackingOrderedForwardOnly = nonBacktrackingOrderedForwardOnly;
        NonBacktrackingConsumerMask = nonBacktrackingConsumerMask;
        NonBacktrackingAcceptClosure = nonBacktrackingAcceptClosure;
        BackreferenceLowerings = backreferenceLowerings;
        SetMasks = setMasks;
        CategorySetMembers = categorySetMembers;
        RepeatCount = repeatCount;
        GroupCount = groupCount;
        Assertions = assertions;
        HasNonFixedLookbehind = hasNonFixedLookbehind;
        HasOrderSensitiveLookbehind = hasOrderSensitiveLookbehind;
        UsesLiteralClassBracketSyntax = usesLiteralClassBracketSyntax;
        HasMixedNamedGroups = hasMixedNamedGroups;
        CaptureCatalog = captureCatalog;
        UsesCaptureOpeningJournal = usesCaptureOpeningJournal;
        ReadsCaptures = readsCaptures;
        HasBalancing = hasBalancing;
        IgnoreCase = ignoreCase;
        CaseBehavior = caseBehavior;
        HasContiguousAnchor = hasContiguousAnchor;
        HasConditionalTest = hasConditionalTest;
        SawLookaround = sawLookaround;
        SawCapturingLookbehind = sawCapturingLookbehind;
        RightToLeft = rightToLeft;
        CanMatchEmpty = canMatchEmpty;
        HasUnicodePatternAtoms = hasUnicodePatternAtoms;
        UsesInterpretedLazyLoopSemantics = usesInterpretedLazyLoopSemantics;
        GroupNumbers = groupNumbers;
        GroupNames = groupNames;
        RequiresUnicodeWordBoundary = requiresUnicodeWordBoundary;
        RequiresUnicodeShorthand = requiresUnicodeShorthand;
        RequiresUnicodeCategory = requiresUnicodeCategory;
        // Instructions and flags are complete before publishing the program.
        // Keep this bounded, allocation-free proof off repeated match attempts.
        HasTerminalCaptureRoot = Utf8SliceRunner.CanRunTerminalCaptureRoot(this);
        HasSingleCapturePerGroup = CanReadCapturesFromSlots();
        if (!rightToLeft && !isNonBacktracking && !ignoreCase && !canMatchEmpty && !hasContiguousAnchor &&
            instructions is not [{ Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning }, ..])
            LeadingAsciiCandidate = Utf8SliceRunner.FindLeadingAsciiCandidate(instructions, Utf8ConstructionScope.CurrentBudget);
        if (!rightToLeft && !isNonBacktracking && !ignoreCase && !hasContiguousAnchor && repeatCount != 0)
        {
            // The class/delimiter shape is independent of the subject and its
            // search plan. Charge for the bounded inspection before it starts.
            Utf8ConstructionScope.CurrentBudget.ChargeWork((ulong)Math.Min(instructions.Length, 64));
            var literal = Utf8SliceRunner.FindLeadingClassDelimiter(this,
                out var leadingClass, out var literalIndex, out var gapMaximum, out var direct);
            LeadingClassDelimiter = (literal, leadingClass, literalIndex, gapMaximum, direct);
        }
        if (nonBacktrackingOrderedClosures.Length != 0)
            NonBacktrackingOrderedStartBytes = Utf8SliceRunner.GetOrderedRegularStartBytes(this, Utf8ConstructionScope.CurrentBudget);
        if (!rightToLeft && !isNonBacktracking && repeatCount != 0)
            TerminalRepeatsWithoutRewind = Utf8SliceRunner.FindTerminalRepeatsWithoutRewind(this, Utf8ConstructionScope.CurrentBudget);
        // An absolute start confines an empty match to byte zero. A terminal
        // anchor can then still exclude the last scalar on nonempty extents.
        if (!rightToLeft && !isNonBacktracking && (!canMatchEmpty || instructions is
            [{ Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning, B: 0 }, ..]))
        {
            RequiredFinalAscii = FindRequiredFinalAscii(Utf8ConstructionScope.CurrentBudget, out var unanchoredByte);
            RequiredUnanchoredFinalByte = unanchoredByte;
        }
    }

    internal static Utf8SliceProgram Create(Utf8SliceInstruction[] instructions, byte[][] setMasks, Utf8SliceNode.CategorySetMember[][] categorySetMembers, int repeatCount, int groupCount, bool requiresUnicodeWordBoundary, bool requiresUnicodeShorthand, bool requiresUnicodeCategory, Utf8SliceAssertion[] assertions, bool hasNonFixedLookbehind, string[] groupNames, bool hasMixedNamedGroups, bool hasBalancing, bool ignoreCase, bool hasContiguousAnchor, bool hasConditionalTest, bool rightToLeft, bool sawLookaround, bool sawCapturingLookbehind, bool canMatchEmpty, Utf8CaptureCatalog? captureCatalog, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax, Utf8ScalarCaseBehavior caseBehavior, bool isNonBacktracking, int nonBacktrackingCaptureOpeningCount, int[] nonBacktrackingEpsilonOrder, ulong[] nonBacktrackingEpsilonAncestors, Func<int, int>[] backreferenceLowerings,
        ulong[]? nonBacktrackingOrderedClosures = null)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        var usesCaptureOpeningJournal = captureCatalog is { HasSharedCaptureDeclarations: true };
        var canUseCompletedCapture = groupCount == 1 && !rightToLeft && !hasBalancing &&
            !hasConditionalTest && !sawLookaround && assertions.Length == 0;
        var captureIsOpen = false;
        var hasClosedCapture = false;
        var readsCaptures = false;
        var hasUnicodePatternAtoms = false;
        var hasOrderSensitiveLookbehind = false;
        var nonBacktrackingOrderedForwardOnly = nonBacktrackingOrderedClosures is { Length: > 0 };
        ulong consumers = 0, accepts = 0;
        budget.ChargeWork((ulong)instructions.Length);
        for (var pc = 0; pc < instructions.Length; pc++)
        {
            var instruction = instructions[pc];
            if (instruction.Kind == Utf8SliceInstructionKind.RepeatGreedy && !rightToLeft && !isNonBacktracking)
                Utf8SliceRunner.MarkDisjointAsciiRepeatContinuation(instructions, setMasks, pc, budget);
            else if (instruction.Kind == Utf8SliceInstructionKind.Jump && !rightToLeft && !isNonBacktracking)
                Utf8SliceRunner.MarkExcludedLoopStart(instructions, setMasks, pc, budget);
            readsCaptures |= instruction.Kind is Utf8SliceInstructionKind.Backref or Utf8SliceInstructionKind.TestGroup or
                Utf8SliceInstructionKind.BalanceStart or Utf8SliceInstructionKind.BalancePop;
            if (instruction.Kind == Utf8SliceInstructionKind.CaptureStart) captureIsOpen = true;
            else if (instruction.Kind == Utf8SliceInstructionKind.CaptureEnd)
            {
                captureIsOpen = false;
                hasClosedCapture = true;
            }
            // One unique, locally closed capture already occupies its final
            // slot. Only references before closure or inside its body need the
            // opening journal's separate last-completed value. An external
            // assertion slot has no local close and retains the journal.
            usesCaptureOpeningJournal |= instruction.Kind == Utf8SliceInstructionKind.Backref &&
                (!canUseCompletedCapture || captureIsOpen || !hasClosedCapture);
            hasUnicodePatternAtoms |= instruction.Kind == Utf8SliceInstructionKind.CategorySet ||
                instruction.Kind == Utf8SliceInstructionKind.Boundary && instruction.C != 0 ||
                instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.A >= 128;
            if (!isNonBacktracking || instructions.Length > 64) continue;
            var bit = 1UL << pc;
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Accept:
                    accepts |= nonBacktrackingEpsilonAncestors[pc];
                    break;
                case Utf8SliceInstructionKind.Fail:
                    break;
                case Utf8SliceInstructionKind.Fork:
                case Utf8SliceInstructionKind.Jump:
                    nonBacktrackingOrderedForwardOnly &= instruction.A > pc;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                case Utf8SliceInstructionKind.CaptureEnd:
                case Utf8SliceInstructionKind.Anchor:
                case Utf8SliceInstructionKind.Boundary:
                    break;
                default:
                    consumers |= bit;
                    break;
            }
        }
        budget.ChargeWork((ulong)assertions.Length);
        foreach (var assertion in assertions)
        {
            usesCaptureOpeningJournal |= assertion.Body.UsesCaptureOpeningJournal;
            readsCaptures |= assertion.Body.ReadsCaptures;
            hasUnicodePatternAtoms |= assertion.Body.HasUnicodePatternAtoms;
            hasOrderSensitiveLookbehind |= assertion.Body.HasOrderSensitiveLookbehind;
            usesLiteralClassBracketSyntax |= assertion.Body.UsesLiteralClassBracketSyntax;
            if (assertion.IsLookbehind)
            {
                budget.ChargeWork((ulong)assertion.Body.Instructions.Length);
                foreach (var instruction in assertion.Body.Instructions)
                    hasOrderSensitiveLookbehind |= instruction.Kind is Utf8SliceInstructionKind.Backref
                        or Utf8SliceInstructionKind.TestGroup or Utf8SliceInstructionKind.Lookahead
                        or Utf8SliceInstructionKind.Lookbehind or Utf8SliceInstructionKind.AtomicCommit;
            }
        }
        var groupNumbers = captureCatalog?.GroupNumbers ?? budget.AllocateArray<int>(checked(groupCount + 1), false);
        if (captureCatalog is null)
        {
            budget.ChargeWork((ulong)groupNumbers.Length);
            for (var group = 0; group < groupNumbers.Length; group++) groupNumbers[group] = group;
        }
        budget.CheckBytes(ObjectBytes);
        budget.ChargeWork(2);
        budget.ReserveBytes(ObjectBytes);
        var result = new Utf8SliceProgram(
            instructions,
            isNonBacktracking,
            nonBacktrackingCaptureOpeningCount,
            nonBacktrackingEpsilonOrder,
            nonBacktrackingEpsilonAncestors,
            nonBacktrackingOrderedClosures ?? [],
            nonBacktrackingOrderedForwardOnly,
            consumers,
            accepts,
            backreferenceLowerings,
            setMasks,
            categorySetMembers,
            repeatCount,
            groupCount,
            assertions,
            hasNonFixedLookbehind,
            hasOrderSensitiveLookbehind,
            usesLiteralClassBracketSyntax,
            hasMixedNamedGroups,
            captureCatalog,
            usesCaptureOpeningJournal,
            readsCaptures,
            hasBalancing,
            ignoreCase,
            caseBehavior,
            hasContiguousAnchor,
            hasConditionalTest,
            sawLookaround,
            sawCapturingLookbehind,
            rightToLeft,
            canMatchEmpty,
            hasUnicodePatternAtoms,
            usesInterpretedLazyLoopSemantics,
            groupNumbers,
            groupNames,
            requiresUnicodeWordBoundary,
            requiresUnicodeShorthand,
            requiresUnicodeCategory);
        construction.Complete(); return result;
    }

    internal Utf8SliceInstruction[] Instructions { get; }
    internal bool IsNonBacktracking { get; }
    internal int NonBacktrackingCaptureOpeningCount { get; }
    internal int[] NonBacktrackingEpsilonOrder { get; }
    internal ulong[] NonBacktrackingEpsilonAncestors { get; }
    internal ulong[] NonBacktrackingOrderedClosures { get; }
    internal bool NonBacktrackingOrderedForwardOnly { get; }
    internal (byte First, byte Second, byte Third, bool Present) NonBacktrackingOrderedStartBytes { get; }
    internal ulong NonBacktrackingConsumerMask { get; }
    internal ulong NonBacktrackingAcceptClosure { get; }
    internal Func<int, int>[] BackreferenceLowerings { get; }

    internal byte[][] SetMasks { get; }

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
    internal bool ReadsCaptures { get; }
    internal bool HasExplicitCaptureNumbers => CaptureCatalog is { HasExplicitCaptureNumbers: true };
    internal bool HasSharedCaptureDeclarations => CaptureCatalog is { HasSharedCaptureDeclarations: true };
    internal bool HasBalancing { get; }
    internal bool IgnoreCase { get; }
    internal Utf8ScalarCaseBehavior CaseBehavior { get; }
    internal bool HasContiguousAnchor { get; }
    internal bool HasConditionalTest { get; }
    internal bool SawLookaround { get; }
    internal bool SawCapturingLookbehind { get; }
    internal bool RightToLeft { get; }
    internal bool CanMatchEmpty { get; }
    internal bool HasUnicodePatternAtoms { get; }
    internal bool HasTerminalCaptureRoot { get; }
    internal bool HasSingleCapturePerGroup { get; }
    internal (int First, byte Last, int Second, byte SecondLast, int InstructionIndex, bool FromBranch) LeadingAsciiCandidate { get; }
        = (-1, 0, -1, 0, -1, false);
    internal (int Literal, int LeadingClass, int LiteralIndex, long GapMaximum, bool Direct) LeadingClassDelimiter { get; }
        = (-1, -1, -1, 0, false);
    private (ulong Mask, int Anchor) TerminalRepeatsWithoutRewind { get; }
    private (Utf8SliceAnchorKind End, Utf8SearchAsciiSet Ascii) RequiredFinalAscii { get; }
    internal byte? RequiredUnanchoredFinalByte { get; }

    private bool CanReadCapturesFromSlots()
    {
        if (GroupCount is < 1 or > 64 || Instructions.Length > 64 || IsNonBacktracking || RightToLeft ||
            UsesCaptureOpeningJournal || HasBalancing || SawLookaround || Assertions.Length != 0)
            return false;

        // Forward forks retry with their captured slots restored. A repeat can
        // revisit its body without unwinding, so its back edge must cross no
        // capture marker. Count emitted closes to also reject duplicated bodies.
        ulong closes = 0;
        var lastCapture = -1;
        for (var index = 0; index < Instructions.Length; index++)
        {
            var instruction = Instructions[index];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.CaptureStart:
                    lastCapture = index;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    var bit = 1UL << instruction.A;
                    if ((closes & bit) != 0) return false;
                    closes |= bit;
                    lastCapture = index;
                    break;
                case Utf8SliceInstructionKind.Fork when instruction.B == 0 && instruction.A > index:
                case Utf8SliceInstructionKind.Jump or Utf8SliceInstructionKind.RepeatGreedy or
                    Utf8SliceInstructionKind.RepeatLazy or Utf8SliceInstructionKind.RepeatGreedyAfterMinimum or
                    Utf8SliceInstructionKind.RepeatLazyAfterMinimum when instruction.A > index:
                case Utf8SliceInstructionKind.Jump or Utf8SliceInstructionKind.RepeatEnd when instruction.A > lastCapture:
                case Utf8SliceInstructionKind.RepeatExit or Utf8SliceInstructionKind.Accept or Utf8SliceInstructionKind.Fail or
                    Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Set or
                    Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.Shorthand or Utf8SliceInstructionKind.CategorySet or
                    Utf8SliceInstructionKind.Anchor or Utf8SliceInstructionKind.Boundary:
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    internal bool SkipsTerminalRewind(int repeatIndex)
    {
        var (mask, anchor) = TerminalRepeatsWithoutRewind;
        var distance = anchor - repeatIndex;
        return mask != 0 && (uint)distance < 64 && (mask & (1UL << distance)) != 0;
    }

    internal bool RejectsByFinalAscii(ReadOnlySpan<byte> input)
    {
        var (end, ascii) = RequiredFinalAscii;
        return end != Utf8SliceAnchorKind.Beginning &&
            ascii.ExcludesFinalScalar(input, end == Utf8SliceAnchorKind.End, CanMatchEmpty);
    }

    // The lowerer includes every ASCII member for this single positive table.
    internal uint GetCompleteAsciiCategoryMask(in Utf8SliceInstruction atom)
        => atom is { Kind: Utf8SliceInstructionKind.CategorySet, B: 0, C: 0 } &&
            CategorySetMembers[atom.D] is [{ Negated: false, Table: {
                Ranges.Length: 0, ClassProgram: null, ScalarPredicate: null } table }] ? table.Mask : 0;

    internal bool TryGetUnfoldedAsciiSet(in Utf8SliceInstruction atom, out Utf8SearchAsciiSet ascii, out bool asciiOnly)
    {
        ascii = default;
        asciiOnly = false;
        if (atom is { Kind: Utf8SliceInstructionKind.Literal, B: 0 })
        {
            asciiOnly = (uint)atom.A < 128;
            if (asciiOnly) ascii = ascii.With((byte)atom.A);
            return true;
        }
        if (atom is not { Kind: Utf8SliceInstructionKind.Set, C: 0 } && GetCompleteAsciiCategoryMask(atom) == 0)
            return false;
        var mask = SetMasks[atom.A].AsSpan();
        var low = BinaryPrimitives.ReadUInt64LittleEndian(mask);
        var high = BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]);
        if (atom.B != 0) { low = ~low; high = ~high; }
        ascii = Utf8SearchAsciiSet.FromMasks(low, high);
        asciiOnly = atom is { Kind: Utf8SliceInstructionKind.Set, B: 0 };
        return true;
    }

    private (Utf8SliceAnchorKind End, Utf8SearchAsciiSet Ascii) FindRequiredFinalAscii(
        Utf8ConstructionBudget budget, out byte? unanchoredByte)
    {
        unanchoredByte = null;
        var code = Instructions.AsSpan();
        if (code.Length is < 2 or > 128 || code[^1].Kind != Utf8SliceInstructionKind.Accept) return default;
        var end = FindTerminalEnd(code[..^1]);
        var unanchored = end < 0;
        if (unanchored)
        {
            if (CanMatchEmpty || LeadingAsciiCandidate is not { FromBranch: true, First: >= 0 } ||
                code[^2] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 }) return default;
            end = code.Length - 1;
        }

        // An atom can provide the final scalar if its continuation can reach
        // the anchor or Accept without consuming. Ignore repeat minima for this proof:
        // admitting an extra possible last atom only widens the set.
        Span<bool> empty = stackalloc bool[end + 1];
        empty[end] = true;
        var ascii = default(Utf8SearchAsciiSet);
        var hasRepeat = RepeatCount != 0;
        for (var pc = end - 1; pc >= 0; pc--)
        {
            budget.ChargeWork(1);
            var instruction = code[pc];
            var next = empty[pc + 1];
            empty[pc] = false;
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Jump when pc > 0 && instruction.A < pc - 1 &&
                    code[pc - 1] is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } loop && loop.A == pc + 1:
                    // A counterless loop consumes before it can revisit
                    // this fork. The forward exit accounts for the empty tail.
                    hasRepeat = true;
                    break;
                case Utf8SliceInstructionKind.Jump:
                case Utf8SliceInstructionKind.Fork:
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                case Utf8SliceInstructionKind.RepeatGreedyAfterMinimum:
                case Utf8SliceInstructionKind.RepeatLazyAfterMinimum:
                    // In particular, a jump around the anchor makes it optional.
                    if (instruction.A <= pc || instruction.A > end) return default;
                    empty[pc] = empty[instruction.A] || instruction.Kind == Utf8SliceInstructionKind.Fork && next;
                    break;
                case Utf8SliceInstructionKind.RepeatEnd:
                    // Its next instruction is the repeat's exit. Any path
                    // that uses only empty further iterations can exit there.
                    if (code[pc + 1].Kind != Utf8SliceInstructionKind.RepeatExit) return default;
                    empty[pc] = next;
                    break;
                case Utf8SliceInstructionKind.RepeatExit:
                case Utf8SliceInstructionKind.CaptureStart:
                case Utf8SliceInstructionKind.CaptureEnd:
                case Utf8SliceInstructionKind.Anchor:
                case Utf8SliceInstructionKind.Boundary:
                case Utf8SliceInstructionKind.Lookahead:
                case Utf8SliceInstructionKind.Lookbehind:
                case Utf8SliceInstructionKind.AtomicCommit:
                    empty[pc] = next;
                    break;
                case Utf8SliceInstructionKind.Fail:
                    break;
                case Utf8SliceInstructionKind.Literal:
                case Utf8SliceInstructionKind.Set:
                case Utf8SliceInstructionKind.Dot:
                case Utf8SliceInstructionKind.CategorySet:
                case Utf8SliceInstructionKind.Shorthand:
                case Utf8SliceInstructionKind.Hybrid:
                case Utf8SliceInstructionKind.Backref:
                    if (next)
                    {
                        if (!TryGetUnfoldedAsciiSet(instruction, out var set, out var asciiOnly) ||
                            unanchored && !asciiOnly) return default;
                        ascii = Utf8SearchAsciiSet.FromMasks(ascii.Low | set.Low, ascii.High | set.High);
                    }
                    break;
                default:
                    return default;
            }
        }
        if (unanchored)
        {
            // Collecting every possible last atom also accounts for branches
            // that jump past the last literal in storage.
            var last = (byte)code[^2].A;
            var required = default(Utf8SearchAsciiSet).With(last);
            if (hasRepeat && ascii.Low == required.Low && ascii.High == required.High) unanchoredByte = last;
            return default;
        }
        return ascii.Low == ulong.MaxValue && ascii.High == ulong.MaxValue
            ? default : ((Utf8SliceAnchorKind)code[end].A, ascii);
    }

    internal static int FindTerminalEnd(ReadOnlySpan<Utf8SliceInstruction> code)
    {
        var index = FindLastBeforeCaptureEnds(code);
        return index >= 0 && IsAbsoluteEndConstraint(code[index]) ? index : -1;
    }

    internal static bool IsAbsoluteEndConstraint(in Utf8SliceInstruction instruction)
        => instruction.Kind == Utf8SliceInstructionKind.Anchor &&
            (instruction.A == (int)Utf8SliceAnchorKind.VeryEnd ||
                instruction.A == (int)Utf8SliceAnchorKind.End && instruction.B == 0);

    internal static int FindLastBeforeCaptureEnds(ReadOnlySpan<Utf8SliceInstruction> code)
    {
        var index = code.Length - 1;
        while (index >= 0 && code.Length - index < 32 && code[index].Kind == Utf8SliceInstructionKind.CaptureEnd)
            index--;
        return index;
    }

    // The compiled .NET 10 lazy-loop defect is excluded from our semantics.
    // Route these programs through the owned VM even when a native plan exists.
    internal bool UsesInterpretedLazyLoopSemantics { get; }

    internal int[] GroupNumbers { get; }
    internal string[] GroupNames { get; }
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

}

/// <summary>Lowers immutable slice trees to flat programs with patched jumps.</summary>
internal static class Utf8SliceLowerer
{
    internal static Utf8SliceProgram? Lower(Utf8SliceNode root, string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool rightToLeft)
    {
        return Lower(root, groupNames, maintainBalance, matchCaseInsensitive, rightToLeft, null);
    }

    internal static Utf8SliceProgram? Lower(Utf8SliceNode root, string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool rightToLeft, Utf8CaptureCatalog? captureCatalog)
        => Lower(root, groupNames, maintainBalance, matchCaseInsensitive, rightToLeft, captureCatalog, false, false, Utf8ScalarCaseBehavior.Invariant);

    internal static Utf8SliceProgram? Lower(Utf8SliceNode root, string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool rightToLeft, Utf8CaptureCatalog? captureCatalog, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax, Utf8ScalarCaseBehavior caseBehavior)
        => Lower(root, groupNames, maintainBalance, matchCaseInsensitive, rightToLeft,
            captureCatalog, usesInterpretedLazyLoopSemantics, usesLiteralClassBracketSyntax, caseBehavior, false);

    internal static Utf8SliceProgram? Lower(Utf8SliceNode root, string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool rightToLeft, Utf8CaptureCatalog? captureCatalog, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax, Utf8ScalarCaseBehavior caseBehavior, bool nonBacktracking)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var facts = AnalyzeNodes(root, nonBacktracking, budget);
        using var frames = Utf8ConstructionFrames<EmissionFrame>.Create(budget, new(root, true, rightToLeft, null, 0));
        try
        {
            while (frames.Count != 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                bool suspended;
                EmissionRequest request;
                try
                {
                    if (frame.Owner is null)
                    {
                        // The program frame is admitted before acquiring its
                        // lowerer and scratch descriptors. Their separate object
                        // admission remains work; referenced facts are borrowed.
                        frame.Owner = new Lowering(groupNames, maintainBalance, matchCaseInsensitive,
                            frame.RightToLeft, facts, caseBehavior, nonBacktracking, budget);
                        frame.Owner.CaptureCatalog = captureCatalog;
                    }
                    suspended = frame.Owner.Advance(ref frame, out request);
                }
                finally { frames[index] = frame; }
                var owner = frame.Owner ?? throw new InvalidOperationException("Missing emission owner.");
                if (suspended)
                {
                    frames.Push(new(request.Node, request.Program, request.RightToLeft,
                        request.Program ? null : owner, request.LookaheadPolarity));
                    continue;
                }
                frames.Pop();
                frame.Scratch?.Dispose();
                if (owner.Failed && !frame.Program) return null;
                if (!frame.Program) continue;
                try
                {
                    if (owner.Failed) return null;
                    var program = FinishLowering(owner, frame.Root,
                        frames.Count == 0 && usesInterpretedLazyLoopSemantics,
                        frames.Count == 0 && usesLiteralClassBracketSyntax);
                    if (program is null) return null;
                    if (frames.Count == 0) { construction.Complete(); return program; }
                    var parent = frames[frames.Count - 1].Owner
                        ?? throw new InvalidOperationException("Missing assertion parent.");
                    parent.LastSubprogram = program;
                }
                finally { owner.Dispose(); }
            }
            return null;
        }
        finally
        {
            while (frames.Count != 0)
            {
                var frame = frames[frames.Count - 1];
                frame.Scratch?.Dispose();
                if (frame.Program) frame.Owner?.Dispose();
                frames.Pop();
            }
        }
    }

    private readonly record struct NodeFacts(bool CanMatchEmpty, bool WritesGroup, bool IsZeroWidth, int Minimum, int Maximum);

    private static Utf8ConstructionMap<Utf8SliceNode, NodeFacts> AnalyzeNodes(Utf8SliceNode root, bool nonBacktracking, Utf8ConstructionBudget budget)
    {
        // Slice nodes are records: structural hashing itself would recurse.
        // One identity cache serves the main and every nested assertion program.
        using var frames = Utf8ConstructionFrames<AnalysisFrame>.Create(budget, new(root));
        var facts = Utf8ConstructionMap<Utf8SliceNode, NodeFacts>.Create(budget, ReferenceEqualityComparer.Instance);
        try
        {
            while (frames.Count > 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                if (frame.NextChild(budget) is { } child)
                {
                    var analyzed = facts.TryGetValue(child, out var childFacts);
                    if (analyzed) frame.WritesGroup |= childFacts.WritesGroup;
                    frames[index] = frame;
                    if (!analyzed) frames.Push(new(child));
                    continue;
                }
                frames.Pop();
                var node = frame.Node;
                var empty = node switch
                {
                    Utf8SliceNode.EmptyNode or Utf8SliceNode.AnchorNode or Utf8SliceNode.BoundaryNode or
                        Utf8SliceNode.LookaheadNode or Utf8SliceNode.LookbehindNode or Utf8SliceNode.BackrefNode or Utf8SliceNode.BalanceNode => true,
                    Utf8SliceNode.ConditionalNode conditional => facts[conditional.YesBody].CanMatchEmpty || facts[conditional.NoBody].CanMatchEmpty,
                    Utf8SliceNode.CaptureNode capture => facts[capture.Body].CanMatchEmpty,
                    Utf8SliceNode.AtomicNode atomic => facts[atomic.Body].CanMatchEmpty,
                    Utf8SliceNode.ConcatNode concat => All(concat.Children, facts, true, budget),
                    Utf8SliceNode.AlternationNode alternation => AnyEmptyBranch(alternation.Branches, facts, budget),
                    Utf8SliceNode.RepeatNode repeat => repeat.Minimum == 0 || facts[repeat.Body].CanMatchEmpty,
                    _ => false,
                };
                var (minimum, maximum) = Measure(node, facts);
                var zeroWidth = nonBacktracking && (node switch
                {
                    Utf8SliceNode.EmptyNode or Utf8SliceNode.FailNode or Utf8SliceNode.AnchorNode or
                        Utf8SliceNode.BoundaryNode or Utf8SliceNode.LookaheadNode or Utf8SliceNode.LookbehindNode => true,
                    Utf8SliceNode.LiteralNode literal => !System.Text.Rune.IsValid(literal.Value),
                    Utf8SliceNode.CaptureNode capture => facts[capture.Body].IsZeroWidth,
                    Utf8SliceNode.BalanceNode balance => facts[balance.Body].IsZeroWidth,
                    Utf8SliceNode.AtomicNode atomic => facts[atomic.Body].IsZeroWidth,
                    Utf8SliceNode.ConcatNode concat => All(concat.Children, facts, false, budget),
                    Utf8SliceNode.AlternationNode alternation => AllZeroWidthBranches(alternation.Branches, facts, budget),
                    Utf8SliceNode.RepeatNode repeat => repeat.Maximum == 0 || facts[repeat.Body].IsZeroWidth,
                    Utf8SliceNode.ConditionalNode conditional => facts[conditional.YesBody].IsZeroWidth && facts[conditional.NoBody].IsZeroWidth,
                    _ => false,
                });
                facts[node] = new NodeFacts(empty, frame.WritesGroup, zeroWidth, minimum, maximum);
                if (frame.WritesGroup && index > 0)
                {
                    var parent = frames[index - 1];
                    parent.WritesGroup = true;
                    frames[index - 1] = parent;
                }
            }
            return facts;
        }
        catch { facts.Dispose(); throw; }
        static bool All(Utf8SliceNode[] nodes, Utf8ConstructionMap<Utf8SliceNode, NodeFacts> facts, bool empty, Utf8ConstructionBudget budget)
        {
            foreach (var node in nodes)
            {
                budget.ChargeWork(1);
                var fact = facts[node];
                if (!(empty ? fact.CanMatchEmpty : fact.IsZeroWidth)) return false;
            }
            return true;
        }

        static bool AnyEmptyBranch(Utf8SliceNode[][] branches, Utf8ConstructionMap<Utf8SliceNode, NodeFacts> facts, Utf8ConstructionBudget budget)
        {
            foreach (var branch in branches)
            {
                budget.ChargeWork(1);
                if (All(branch, facts, true, budget)) return true;
            }
            return false;
        }

        static bool AllZeroWidthBranches(Utf8SliceNode[][] branches, Utf8ConstructionMap<Utf8SliceNode, NodeFacts> facts, Utf8ConstructionBudget budget)
        {
            foreach (var branch in branches)
            {
                budget.ChargeWork(1);
                if (!All(branch, facts, false, budget)) return false;
            }
            return true;
        }

        static (int Minimum, int Maximum) Measure(Utf8SliceNode node, Utf8ConstructionMap<Utf8SliceNode, NodeFacts> facts)
        {
            switch (node)
            {
                case Utf8SliceNode.EmptyNode or Utf8SliceNode.AnchorNode or Utf8SliceNode.BoundaryNode or Utf8SliceNode.LookaheadNode or Utf8SliceNode.LookbehindNode:
                    return (0, 0);
                case Utf8SliceNode.LiteralNode literal:
                    if (!System.Text.Rune.IsValid(literal.Value)) return (0, 0);
                    var width = literal.Value < 0x80 ? 1 : literal.Value < 0x800 ? 2 : literal.Value < 0x10000 ? 3 : 4;
                    return literal.FoldCase ? (1, 4) : (width, width);
                case Utf8SliceNode.SetNode set: return set.Negated || set.FoldCase || set.Members.Length != 0 ? (1, 4) : (1, 1);
                case Utf8SliceNode.DotNode: return (1, 4);
                case Utf8SliceNode.ShorthandNode or Utf8SliceNode.CategorySetNode: return (1, 4);
                case Utf8SliceNode.BackrefNode: return (0, int.MaxValue);
                case Utf8SliceNode.CaptureNode capture: return (facts[capture.Body].Minimum, facts[capture.Body].Maximum);
                case Utf8SliceNode.BalanceNode balance: return (facts[balance.Body].Minimum, facts[balance.Body].Maximum);
                case Utf8SliceNode.AtomicNode atomic: return (facts[atomic.Body].Minimum, facts[atomic.Body].Maximum);
                case Utf8SliceNode.ConditionalNode conditional:
                    return (Math.Min(facts[conditional.YesBody].Minimum, facts[conditional.NoBody].Minimum),
                        Math.Max(facts[conditional.YesBody].Maximum, facts[conditional.NoBody].Maximum));
                case Utf8SliceNode.ConcatNode concat:
                    long minimum = 0, maximum = 0;
                    foreach (var child in concat.Children)
                    {
                        minimum += facts[child].Minimum;
                        maximum += facts[child].Maximum;
                    }
                    return (Saturate(minimum), Saturate(maximum));
                case Utf8SliceNode.AlternationNode alternation:
                    var branchMinimum = int.MaxValue;
                    var branchMaximum = 0;
                    foreach (var branch in alternation.Branches)
                    {
                        long low = 0, high = 0;
                        foreach (var child in branch)
                        {
                            low += facts[child].Minimum;
                            high += facts[child].Maximum;
                        }
                        branchMinimum = Math.Min(branchMinimum, Saturate(low));
                        branchMaximum = Math.Max(branchMaximum, Saturate(high));
                    }
                    return alternation.Branches.Length == 0 ? (0, 0) : (branchMinimum, branchMaximum);
                case Utf8SliceNode.RepeatNode repeat:
                    var body = facts[repeat.Body];
                    return (Saturate((long)body.Minimum * repeat.Minimum),
                        repeat.Maximum == long.MaxValue || body.Maximum == int.MaxValue ? int.MaxValue : Saturate((long)body.Maximum * repeat.Maximum));
                default: return (0, int.MaxValue);
            }
        }

        static int Saturate(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
    }

    private struct AnalysisFrame(Utf8SliceNode node)
    {
        internal readonly Utf8SliceNode Node = node;
        internal bool WritesGroup = node is Utf8SliceNode.CaptureNode or Utf8SliceNode.BalanceNode;
        private int _index, _branch;

        // Empty alternatives spend shared work even when they contain no child to push.
        internal Utf8SliceNode? NextChild(Utf8ConstructionBudget budget)
        {
            budget.ChargeWork(1);
            switch (Node)
            {
                case Utf8SliceNode.ConcatNode concat:
                    return _index == concat.Children.Length ? null : concat.Children[_index++];
                case Utf8SliceNode.AlternationNode alternate:
                    while (_branch < alternate.Branches.Length)
                    {
                        budget.ChargeWork(1);
                        if (_index < alternate.Branches[_branch].Length)
                            return alternate.Branches[_branch][_index++];
                        _branch++; _index = 0;
                    }
                    return null;
                case Utf8SliceNode.ConditionalNode conditional:
                    return _index++ switch
                    {
                        0 => conditional.YesBody,
                        1 => conditional.NoBody,
                        2 => conditional.TestBody,
                        _ => null,
                    };
                default:
                    if (_index++ != 0) return null;
                    return Node switch
                    {
                        Utf8SliceNode.CaptureNode capture => capture.Body,
                        Utf8SliceNode.BalanceNode balance => balance.Body,
                        Utf8SliceNode.AtomicNode atomic => atomic.Body,
                        Utf8SliceNode.RepeatNode repeat => repeat.Body,
                        Utf8SliceNode.LookaheadNode ahead => ahead.Body,
                        Utf8SliceNode.LookbehindNode behind => behind.Body,
                        _ => null,
                    };
            }
        }
    }

    private readonly record struct EmissionRequest(Utf8SliceNode Node, bool Program, bool RightToLeft, int LookaheadPolarity);

    private struct EmissionFrame(Utf8SliceNode root, bool program, bool rightToLeft, Lowering? owner, int lookaheadPolarity)
    {
        internal Utf8SliceNode Root = root;
        internal bool Program = program;
        internal bool RightToLeft = rightToLeft;
        internal Lowering? Owner = owner;
        internal int LookaheadPolarity = lookaheadPolarity;
        internal int Stage, Index, Branch, Mark, Jump, RepeatId, Opening;
        internal long Minimum, Maximum;
        internal bool NullableLoop;
        internal Utf8NonBacktrackingLoopScope? ParentScope;
        internal Utf8ConstructionBuffer<int>? Scratch;
    }

    private static Utf8SliceProgram? FinishLowering(Lowering lowerer, Utf8SliceNode root, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax)
    {
        lowerer.Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Accept, 0, 0, 0, 0));
        var requiresUnicodeWordBoundary = false;
        var requiresUnicodeShorthand = false;
        var requiresUnicodeCategory = false;
        static void Classify(
            Utf8SliceInstruction instruction,
            ref bool word,
            ref bool shorthand,
            ref bool category)
        {
            if (instruction.Kind == Utf8SliceInstructionKind.Boundary)
            {
                word = true;
            }

            if (instruction.Kind == Utf8SliceInstructionKind.Shorthand)
            {
                shorthand = true;
            }

            // These flags describe language features for consumers whose own
            // predicates are ASCII-scoped; scalar execution needs no input fence.
            shorthand |= instruction.Kind is Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.CategorySet;
            category |= instruction.Kind is Utf8SliceInstructionKind.CategorySet;
        }

        foreach (var instruction in lowerer.Instructions)
        {
            Classify(instruction, ref requiresUnicodeWordBoundary, ref requiresUnicodeShorthand, ref requiresUnicodeCategory);
            if (requiresUnicodeWordBoundary && requiresUnicodeShorthand && requiresUnicodeCategory)
            {
                break;
            }
        }

        // Hoist nested language features and variable byte-length lookbehind
        // facts for consumers with narrower native admission.
        var assertions = lowerer.Assertions.Take();
        var hasNonFixedLookbehind = false;
        foreach (var assertion in assertions)
        {
            if (assertion.IsLookbehind && assertion.MinLengthBytes != assertion.MaxLengthBytes)
            {
                hasNonFixedLookbehind = true;
            }

            // Hoist complete nested facts, including assertions below this body.
            requiresUnicodeWordBoundary |= assertion.Body.RequiresUnicodeWordBoundary;
            requiresUnicodeShorthand |= assertion.Body.RequiresUnicodeShorthand;
            requiresUnicodeCategory |= assertion.Body.RequiresUnicodeCategory;
            hasNonFixedLookbehind |= assertion.Body.HasNonFixedLookbehind;
            lowerer.HasConditionalTest |= assertion.Body.HasConditionalTest;
            lowerer.SawCapturingLookbehind |= assertion.Body.SawCapturingLookbehind;
        }

        // Hand-built trees (tests, assertion bodies without renumbering
        // context) carry no names; decimal ordinals identify their slots since
        // only the parser admits named shapes, always with real names.
        var resolvedNames = lowerer.GroupNames;
        Span<char> numberText = stackalloc char[10];
        if (resolvedNames is null)
        {
            resolvedNames = lowerer.Budget.AllocateArray<string>(lowerer.GroupCount + 1, false);
            for (var number = 0; number <= lowerer.GroupCount; number++)
            {
                lowerer.Budget.ChargeWork(1);
                if (!((uint)number).TryFormat(numberText, out var written))
                    throw new InvalidOperationException("A scalar group number exceeds its decimal scratch.");
                resolvedNames[number] = lowerer.Budget.CopyText(numberText[..written]);
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
            lowerer.Budget.ChargeWork(1);
            if (!((uint)number).TryFormat(numberText, out var written))
                throw new InvalidOperationException("A scalar group number exceeds its decimal scratch.");
            if (resolvedNames[number].AsSpan().SequenceEqual(numberText[..written]))
            {
                hasUnnamedGroups = true;
            }
            else
            {
                hasNamedGroups = true;
            }
        }

        var instructions = lowerer.Instructions.Take();
        if (!lowerer.MirrorExecute && !lowerer.NonBacktracking && lowerer.RepeatCount != 0)
            MarkBoundedEndTails(instructions, lowerer.Budget);
        if (lowerer.HasNullableNonBacktrackingLoop && lowerer.NonBacktrackingScopes is { } scopes)
        {
            var lifted = Utf8NonBacktrackingLoopGraph.Lift(instructions, scopes, lowerer.Budget);
            lowerer.Budget.ReleaseArray(instructions, true);
            instructions = lifted;
        }
        ulong[] epsilonAncestors = [];
        ulong[] orderedClosures = [];
        var epsilonOrder = lowerer.NonBacktracking
            ? Utf8NonBacktrackingSearchState.BuildEpsilonOrder(instructions, out epsilonAncestors, out orderedClosures) : [];
        return Utf8SliceProgram.Create(
            instructions,
            lowerer.SetMasks.Take(),
            lowerer.CategorySetMembers.Take(),
            lowerer.RepeatCount,
            lowerer.GroupCount,
            requiresUnicodeWordBoundary,
            requiresUnicodeShorthand,
            requiresUnicodeCategory,
            assertions,
            hasNonFixedLookbehind, resolvedNames, hasUnnamedGroups && hasNamedGroups, lowerer.MaintainBalance, lowerer.MatchCaseInsensitive, lowerer.SawContiguousAnchor, lowerer.HasConditionalTest, lowerer.MirrorExecute, lowerer.SawLookaround, lowerer.SawCapturingLookbehind, lowerer.Facts[root].CanMatchEmpty, lowerer.CaptureCatalog, usesInterpretedLazyLoopSemantics, usesLiteralClassBracketSyntax, lowerer.CaseBehavior,
            lowerer.NonBacktracking, lowerer.CaptureOpeningCount,
            epsilonOrder, epsilonAncestors,
            lowerer.BackreferenceLowerings.Take(), orderedClosures);
    }

    private static void MarkBoundedEndTails(Span<Utf8SliceInstruction> code, Utf8ConstructionBudget budget)
    {
        if (code.Length < 2 || code[^1].Kind != Utf8SliceInstructionKind.Accept) return;
        var end = Utf8SliceProgram.FindTerminalEnd(code[..^1]);
        if (end < 0) return;

        // Walk a bounded suffix once. Zero means that reaching the anchor
        // cannot be proved. The lax end can leave an unconsumed final LF.
        const int precedingInstructions = 127;
        var start = Math.Max(0, end - precedingInstructions);
        Span<int> limits = stackalloc int[end - start + 1];
        limits[^1] = code[end].A == (int)Utf8SliceAnchorKind.VeryEnd ? 1 : 2;
        for (var index = end - 1; index >= start; index--)
        {
            budget.ChargeWork(1);
            var instruction = code[index];
            var next = limits[index - start + 1];
            long limit = 0;
            var width = ScalarMaximumBytes(instruction);
            if (width != 0 && next != 0)
                limit = (long)next + width;
            else switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.RepeatExit:
                    if (next != instruction.B)
                        code[index] = new(instruction.Kind, instruction.A, next, instruction.C, instruction.D);
                    limit = next;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                case Utf8SliceInstructionKind.CaptureEnd:
                case Utf8SliceInstructionKind.Boundary:
                    limit = next;
                    break;
                case Utf8SliceInstructionKind.Jump when instruction.A > index && instruction.A <= end:
                    limit = limits[instruction.A - start];
                    break;
                case Utf8SliceInstructionKind.Fork when instruction.B == 0:
                    var other = instruction.A > index && instruction.A <= end ? limits[instruction.A - start] : 0;
                    if (instruction.C != next || instruction.D != other)
                        code[index] = new(instruction.Kind, instruction.A, 0, next, other);
                    if (next != 0 && other != 0) limit = Math.Max(next, other);
                    break;
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                    var exit = instruction.A;
                    if (exit == index + 3 && exit < end && code[index + 2] is { Kind: Utf8SliceInstructionKind.RepeatEnd } back &&
                        back.A == index && code[exit] is { Kind: Utf8SliceInstructionKind.RepeatExit, B: > 0 } done &&
                        done.A == instruction.B && instruction.RepeatMaximum != long.MaxValue &&
                        ScalarMaximumBytes(code[index + 1]) is > 0 and var atomWidth)
                        limit = done.B + instruction.RepeatMaximum * atomWidth;
                    break;
            }
            limits[index - start] = limit <= int.MaxValue ? (int)limit : 0;
        }

        static int ScalarMaximumBytes(Utf8SliceInstruction instruction) => instruction switch
        {
            { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: < 128 } or
                { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } => 1,
            { Kind: Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Dot or
                Utf8SliceInstructionKind.Shorthand or Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid } => 4,
            _ => 0,
        };
    }

    private sealed class Lowering
    {
        internal readonly Utf8ConstructionBuffer<Utf8SliceInstruction> Instructions;
        internal readonly Utf8ConstructionBudget Budget;
        internal readonly Utf8ConstructionBuffer<Func<int, int>> BackreferenceLowerings;
        internal readonly Utf8ConstructionBuffer<byte[]> SetMasks;
        internal readonly Utf8ConstructionBuffer<Utf8SliceNode.CategorySetMember[]> CategorySetMembers;
        internal int RepeatCount;
        internal int CaptureOpeningCount;
        internal readonly Utf8ConstructionMap<Utf8SliceNode.CaptureNode, int>? CaptureOpenings;
        internal readonly Utf8ConstructionBuffer<Utf8NonBacktrackingLoopScope?>? NonBacktrackingScopes;
        internal Utf8NonBacktrackingLoopScope? NonBacktrackingScope;
        internal bool HasNullableNonBacktrackingLoop;
        internal int GroupCount;
        internal bool SawContiguousAnchor;
        internal bool HasConditionalTest;
        internal bool SawLookaround;
        internal bool SawCapturingLookbehind;
        internal readonly Utf8ConstructionBuffer<Utf8SliceAssertion> Assertions;
        internal readonly string[]? GroupNames;
        internal Utf8CaptureCatalog? CaptureCatalog;
        internal readonly bool MaintainBalance;
        internal readonly bool MatchCaseInsensitive;
        internal readonly Utf8ScalarCaseBehavior CaseBehavior;
        internal readonly bool MirrorExecute;
        internal readonly bool NonBacktracking;
        internal readonly Utf8ConstructionMap<Utf8SliceNode, NodeFacts> Facts;
        internal Utf8SliceProgram? LastSubprogram;
        internal bool Failed;

        internal Lowering(string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool mirrorExecute, Utf8ConstructionMap<Utf8SliceNode, NodeFacts> facts, Utf8ScalarCaseBehavior caseBehavior, bool nonBacktracking, Utf8ConstructionBudget budget)
        {
            Budget = budget;
            Instructions = new(budget, true);
            BackreferenceLowerings = new(budget);
            SetMasks = new(budget);
            CategorySetMembers = new(budget);
            Assertions = new(budget);
            GroupNames = groupNames;
            MaintainBalance = maintainBalance;
            MatchCaseInsensitive = matchCaseInsensitive;
            CaseBehavior = caseBehavior;
            MirrorExecute = mirrorExecute;
            NonBacktracking = nonBacktracking;
            CaptureOpenings = nonBacktracking ? new(budget, ReferenceEqualityComparer.Instance) : null;
            NonBacktrackingScopes = nonBacktracking ? new(budget) : null;
            Facts = facts;
            // RTL references can precede their captures. Every program starts
            // with the final catalog tally when supplied, just as the old entry.
            if (groupNames is not null) GroupCount = groupNames.Length - 1;
        }

        internal void Dispose()
        {
            Instructions.Dispose();
            BackreferenceLowerings.Dispose();
            SetMasks.Dispose();
            CategorySetMembers.Dispose();
            Assertions.Dispose();
            CaptureOpenings?.Dispose();
            NonBacktrackingScopes?.Dispose();
        }

        private static int GetAsciiRange(ReadOnlySpan<byte> mask)
        {
            var low = BinaryPrimitives.ReadUInt64LittleEndian(mask);
            var high = BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]);
            if ((low | high) == 0) return 0;
            var first = low != 0 ? BitOperations.TrailingZeroCount(low)
                : 64 + BitOperations.TrailingZeroCount(high);
            var last = high != 0 ? 127 - BitOperations.LeadingZeroCount(high)
                : 63 - BitOperations.LeadingZeroCount(low);
            // Equal population and width prove there are no holes.
            return BitOperations.PopCount(low) + BitOperations.PopCount(high) == last - first + 1
                ? (last << 8) | (first + 1) : 0;
        }

        internal void Emit(Utf8SliceInstruction instruction)
        {
            Instructions.Add(instruction);
            NonBacktrackingScopes?.Add(NonBacktrackingScope);
        }

        internal bool Advance(ref EmissionFrame frame, out EmissionRequest request)
        {
            request = default;
            var node = frame.Root;
            if (frame.LookaheadPolarity != 0)
                return EmitLookahead(node, frame.LookaheadPolarity == 1, ref frame, out request);
            switch (node)
            {
                case Utf8SliceNode.EmptyNode:
                    break;
                case Utf8SliceNode.FailNode:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
                    break;
                case Utf8SliceNode.LiteralNode literal:
                    // An invalid scalar is an impossible atom, not a three-byte
                    // literal or a code-unit special mode. Repetition still
                    // controls whether the atom must execute.
                    Emit(Rune.TryCreate(literal.Value, out var scalar)
                        ? new Utf8SliceInstruction(Utf8SliceInstructionKind.Literal, literal.Value,
                            literal.FoldCase && Utf8SliceCaseEquivalences.HasAlternateScalar(scalar, CaseBehavior) ? 1 : 0, 0, 0)
                        : new Utf8SliceInstruction(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
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
                        var range = !set.Negated && !set.FoldCase ? GetAsciiRange(set.Mask16) : 0;
                        Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Set, maskIndex, set.Negated ? 1 : 0, set.FoldCase ? 1 : 0, range));
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
                    if (frame.Index < concat.Children.Length)
                    {
                        request = new(concat.Children[frame.Index++], false, MirrorExecute, 0);
                        return true;
                    }
                    break;
                case Utf8SliceNode.AlternationNode alternate:
                    if (alternate.Branches.Length == 0)
                        throw new ArgumentException("Slice alternations need at least one branch.", nameof(node));
                    if (frame.Stage == 0)
                    {
                        frame.Scratch = Utf8ConstructionBuffer<int>.Create(Budget);
                        frame.Stage = 1;
                    }
                    var jumps = frame.Scratch ?? throw new InvalidOperationException("Missing alternation jumps.");
                    while (frame.Branch < alternate.Branches.Length)
                    {
                        Budget.ChargeWork(1);
                        if (frame.Stage == 1)
                        {
                            frame.Mark = Instructions.Count;
                            if (frame.Branch + 1 < alternate.Branches.Length)
                                Emit(new(Utf8SliceInstructionKind.Fork, 0, 0, 0, 0));
                            frame.Index = 0;
                            frame.Stage = 2;
                        }
                        var branch = alternate.Branches[frame.Branch];
                        if (frame.Index < branch.Length)
                        {
                            request = new(branch[frame.Index++], false, MirrorExecute, 0);
                            return true;
                        }
                        if (frame.Branch + 1 < alternate.Branches.Length)
                        {
                            jumps.Add(Instructions.Count);
                            Emit(new(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                            Patch(frame.Mark, Instructions.Count);
                        }
                        frame.Branch++;
                        frame.Stage = 1;
                    }
                    foreach (var jump in jumps) { Budget.ChargeWork(1); Patch(jump, Instructions.Count); }
                    break;
                case Utf8SliceNode.RepeatNode repeat:
                    if (repeat.Minimum < 0 || repeat.Minimum >= uint.MaxValue || repeat.Maximum < repeat.Minimum ||
                        repeat.Maximum != long.MaxValue && repeat.Maximum >= uint.MaxValue)
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice repeat bounds need 0 <= min <= max.");
                    if (NonBacktracking)
                    {
                        if (frame.Stage == 0)
                        {
                            frame.Minimum = repeat.Minimum;
                            frame.Maximum = repeat.Maximum;
                            if (Facts[repeat.Body].IsZeroWidth)
                            {
                                // Only the final zero-width completion is exposed.
                                // Keep predicates/preference without expanding a bound.
                                frame.Minimum = frame.Minimum == 0 ? 0 : 1;
                                frame.Maximum = frame.Maximum == 0 ? 0 : 1;
                            }
                            else
                            {
                                var copies = frame.Maximum == long.MaxValue ? frame.Minimum : frame.Maximum;
                                Budget.CheckReplacementMinimum((ulong)Instructions.Capacity, (ulong)Instructions.Count + (ulong)copies);
                            }
                            frame.Stage = 1;
                        }
                        if (Failed) return false;
                        if (frame.Stage == 5)
                        {
                            CompleteNullableNonBacktrackingIteration(ref frame, -1);
                            frame.Stage = 1;
                        }
                        if (frame.Stage == 1)
                        {
                            if (frame.Index < frame.Minimum)
                            {
                                frame.Index++;
                                // Earlier required copies must still reach the
                                // minimum. Once it is met, an empty completion
                                // exits rather than starting optional copies.
                                if (frame.Index == frame.Minimum && frame.Minimum < frame.Maximum &&
                                    Facts[repeat.Body].CanMatchEmpty && !Facts[repeat.Body].IsZeroWidth)
                                {
                                    frame.ParentScope = NonBacktrackingScope;
                                    NonBacktrackingScope = new(frame.ParentScope);
                                    frame.Stage = 5;
                                }
                                request = new(repeat.Body, false, false, 0);
                                return true;
                            }
                            frame.Stage = 2;
                        }
                        if (frame.Stage == 3)
                        {
                            if (frame.NullableLoop)
                                CompleteNullableNonBacktrackingIteration(ref frame,
                                    frame.Maximum == long.MaxValue ? frame.Mark : -1);
                            if (frame.Maximum == long.MaxValue)
                            {
                                if (!frame.NullableLoop)
                                    Emit(new(Utf8SliceInstructionKind.Jump, frame.Mark, 0, 0, 0));
                                NonBacktrackingScope = frame.ParentScope;
                                Patch(frame.Mark, Instructions.Count);
                                frame.Stage = 4;
                            }
                            else
                            {
                                var exits = frame.Scratch ?? throw new InvalidOperationException("Missing repeat exits.");
                                exits.Add(frame.Mark);
                                frame.Stage = 2;
                            }
                        }
                        if (frame.Stage == 2 && frame.Index < frame.Maximum)
                        {
                            frame.Scratch ??= Utf8ConstructionBuffer<int>.Create(Budget);
                            frame.Mark = Instructions.Count;
                            Emit(new(Utf8SliceInstructionKind.Fork, 0, repeat.Lazy ? 1 : 0, 0, 0));
                            frame.ParentScope = NonBacktrackingScope;
                            frame.NullableLoop = Facts[repeat.Body].CanMatchEmpty && !Facts[repeat.Body].IsZeroWidth;
                            if (frame.NullableLoop)
                            {
                                NonBacktrackingScope = new(frame.ParentScope);
                                HasNullableNonBacktrackingLoop = true;
                            }
                            frame.Index++;
                            frame.Stage = 3;
                            request = new(repeat.Body, false, false, 0);
                            return true;
                        }
                        if (frame.Scratch is { } remainingExits)
                            foreach (var exit in remainingExits) { Budget.ChargeWork(1); Patch(exit, Instructions.Count); }
                        break;
                    }
                    if (frame.Stage == 0 && !MirrorExecute && repeat.Minimum is 2 or 3 && repeat.Minimum == repeat.Maximum)
                    {
                        // Re-emit small consuming bodies without the repeat journal.
                        // Bound both the proof and emitted copies. Each copy still
                        // uses the ordinary work and instruction-buffer admission.
                        var remaining = 32;
                        var candidateBody = repeat.Body;
                        if (candidateBody is Utf8SliceNode.CaptureNode capture)
                        {
                            // Each copy opens and closes the same capture slot.
                            remaining -= 2;
                            candidateBody = capture.Body;
                        }
                        if (candidateBody is Utf8SliceNode.ConcatNode or Utf8SliceNode.AlternationNode &&
                            !Facts[candidateBody].CanMatchEmpty && IsSmallPureBody(candidateBody, ref remaining)) frame.Stage = 2;
                    }
                    if (frame.Stage == 2)
                    {
                        if (frame.Index == repeat.Minimum) break;
                        frame.Index++;
                        request = new(repeat.Body, false, false, 0);
                        return true;
                    }
                    if (frame.Stage == 0 && !MirrorExecute && repeat is { Minimum: 0 or 1, Maximum: long.MaxValue, Lazy: false })
                    {
                        var remaining = 32;
                        var candidateBody = repeat.Body;
                        if (candidateBody is Utf8SliceNode.CaptureNode capture)
                        {
                            remaining -= 2;
                            candidateBody = capture.Body;
                        }
                        if (repeat.Minimum == 1 && GetDelimitedAsciiRunByte(repeat.Body) >= 0 ||
                            candidateBody is Utf8SliceNode.AlternationNode && Facts[candidateBody].Minimum > 0 &&
                            IsSmallPureBody(candidateBody, ref remaining))
                        {
                            if (repeat.Minimum == 0)
                            {
                                frame.Jump = Instructions.Count;
                                Emit(new(Utf8SliceInstructionKind.Fork, 0, 0, 0, 0));
                            }
                            frame.Mark = Instructions.Count;
                            frame.Stage = 3;
                            request = new(repeat.Body, false, false, 0);
                            return true;
                        }
                    }
                    if (frame.Stage == 3)
                    {
                        // Each completed body has already advanced the input.
                        // A plain fork preserves greedy exits and capture undo.
                        Emit(new(Utf8SliceInstructionKind.Fork, Instructions.Count + 2, 0, 0, 0));
                        // Jump.B carries the required delimiter plus one, or zero
                        // for a loop without a proved delimiter.
                        Emit(new(Utf8SliceInstructionKind.Jump, frame.Mark, GetDelimitedAsciiRunByte(repeat.Body) + 1, 0, 0));
                        if (repeat.Minimum == 0) Patch(frame.Jump, Instructions.Count);
                        break;
                    }
                    // Zero or one iteration needs a branch, not a counter or
                    // rollback journal. Preserve single-atom repeats for the
                    // existing no-rewind/terminal kernels; compound bodies keep
                    // ordinary captures/assertions, including nullable completions.
                    if (repeat.Minimum == 0 && repeat.Maximum == 1 && repeat.Body is not
                        (Utf8SliceNode.LiteralNode or Utf8SliceNode.DotNode or Utf8SliceNode.SetNode or
                         Utf8SliceNode.ShorthandNode or Utf8SliceNode.CategorySetNode))
                    {
                        if (frame.Stage == 0)
                        {
                            frame.Mark = Instructions.Count;
                            Emit(new(Utf8SliceInstructionKind.Fork, 0, 0, 0, 0));
                            if (repeat.Lazy)
                            {
                                frame.Jump = Instructions.Count;
                                Emit(new(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                                Patch(frame.Mark, Instructions.Count);
                            }
                            frame.Stage = 1;
                            request = new(repeat.Body, false, MirrorExecute, 0);
                            return true;
                        }
                        Patch(repeat.Lazy ? frame.Jump : frame.Mark, Instructions.Count);
                        break;
                    }
                    if (frame.Stage == 0)
                    {
                        frame.RepeatId = RepeatCount++;
                        frame.Mark = Instructions.Count;
                        Emit(new(Utf8SliceInstructionKind.RepeatGreedy, 0, 0, 0, 0));
                        frame.Stage = 1;
                        request = new(repeat.Body, false, MirrorExecute, 0);
                        return true;
                    }
                    var stopBytes = FindGuardedRepeatStopBytes(repeat, frame.Mark);
                    Emit(new(Utf8SliceInstructionKind.RepeatEnd, frame.Mark, stopBytes, 0, 0));
                    var repeatExit = Instructions.Count;
                    Emit(new(Utf8SliceInstructionKind.RepeatExit, frame.RepeatId, 0, 0, 0));
                    var repeatKind = repeat.Lazy ? Utf8SliceInstructionKind.RepeatLazy : Utf8SliceInstructionKind.RepeatGreedy;
                    if (repeat.GuardEmptyAfterMinimum && repeat.Minimum > 0 && repeat.Minimum < repeat.Maximum &&
                        Facts[repeat.Body].CanMatchEmpty)
                        repeatKind = repeat.Lazy ? Utf8SliceInstructionKind.RepeatLazyAfterMinimum : Utf8SliceInstructionKind.RepeatGreedyAfterMinimum;
                    Instructions[frame.Mark] = new(repeatKind,
                        repeatExit, frame.RepeatId, unchecked((int)repeat.Minimum),
                        repeat.Maximum == long.MaxValue ? -1 : unchecked((int)repeat.Maximum));
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
                    var wordTable = 0;
                    if (boundary.NonAsciiWordPredicate is { } wordPredicate)
                    {
                        // Borrow the existing category-table pool, with one-based
                        // operand C; zero preserves the builtin boundary encoding.
                        var members = Budget.AllocateArray<Utf8SliceNode.CategorySetMember>(1, false);
                        members[0] = Utf8SliceNode.CategorySetMember.Create(
                            Utf8SliceCategoryTable.ForScalarPredicate(wordPredicate), false);
                        wordTable = CategorySetMembers.Count + 1;
                        CategorySetMembers.Add(members);
                    }
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Boundary, boundary.IsWordBoundary ? 1 : 0,
                        (int)boundary.WordKind, wordTable, 0));
                    break;
                case Utf8SliceNode.ShorthandNode shorthand:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Shorthand, (int)shorthand.Shorthand, 0, 0, 0));
                    break;
                case Utf8SliceNode.CategorySetNode categorySet:
                    var categorySetMaskIndex = SetMasks.Count;
                    var categoryMask = categorySet.Mask16;
                    if (!categorySet.Negated && !categorySet.FoldCase &&
                        categorySet.Categories is [{ Negated: false, Table: { Mask: not 0,
                            Ranges.Length: 0, ClassProgram: null, ScalarPredicate: null } table }])
                    {
                        // Complete the existing positive ASCII mask once. Runtime
                        // scalar tables still govern wider characters and misses.
                        // Keep the immutable source mask available to other lowerings.
                        Budget.ChargeWork(128);
                        Span<byte> completed = stackalloc byte[16];
                        categoryMask.CopyTo(completed);
                        for (var value = 0; value < 128; value++)
                            if ((table.Mask & (1u << (int)Rune.GetUnicodeCategory(new Rune(value)))) != 0)
                                completed[value >> 3] |= (byte)(1 << (value & 7));
                        if (!completed.SequenceEqual(categoryMask))
                        {
                            categoryMask = Budget.AllocateArray<byte>(16, false);
                            completed.CopyTo(categoryMask);
                        }
                    }
                    SetMasks.Add(categoryMask);
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

                    var loweringIndex = 0;
                    if (backref.LowerScalar is { } lowerScalar)
                    {
                        var index = BackreferenceLowerings.IndexOf(lowerScalar);
                        if (index < 0) { index = BackreferenceLowerings.Count; BackreferenceLowerings.Add(lowerScalar); }
                        loweringIndex = index + 1;
                    }
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Backref, backref.Slot, backref.FoldCase ? 1 : 0, backref.AllowUnmatched ? 1 : 0, loweringIndex));
                    break;
                case Utf8SliceNode.BalanceNode balance:
                    if (balance.PopSlot < 0 || balance.CaptureSlot < -1)
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice balance slots start at zero.");
                    if (frame.Stage == 0)
                    {
                        GroupCount = Math.Max(GroupCount, Math.Max(balance.PopSlot + 1, balance.CaptureSlot + 1));
                        if (balance.CaptureSlot >= 0) Emit(new(Utf8SliceInstructionKind.BalanceStart, balance.PopSlot, balance.CaptureSlot, 0, 0));
                        frame.Stage = 1;
                        request = new(balance.Body, false, MirrorExecute, 0);
                        return true;
                    }
                    Emit(new(Utf8SliceInstructionKind.BalancePop, balance.PopSlot, balance.CaptureSlot, 0, 0));
                    break;
                case Utf8SliceNode.AtomicNode atomic:
                    if (frame.Stage == 0)
                    {
                        frame.Mark = Instructions.Count;
                        Emit(new(Utf8SliceInstructionKind.Fork, 0, 2, 0, 0));
                        frame.Stage = 1;
                        request = new(atomic.Body, false, MirrorExecute, 0);
                        return true;
                    }
                    Emit(new(Utf8SliceInstructionKind.AtomicCommit, 0, 0, 0, 0));
                    var atomicJump = Instructions.Count;
                    Emit(new(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                    var atomicFail = Instructions.Count;
                    Emit(new(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
                    Patch(frame.Mark, atomicFail);
                    Patch(atomicJump, Instructions.Count);
                    break;
                case Utf8SliceNode.ConditionalNode conditional:
                    if (conditional.TestBody is { } testBody)
                    {
                        // Emit the two guarded branches directly, borrowing the
                        // test/body nodes. The former synthetic alternation,
                        // lookahead nodes and branch arrays are unnecessary.
                        HasConditionalTest = true;
                        switch (frame.Stage)
                        {
                            case 0:
                                frame.Mark = Instructions.Count;
                                Emit(new(Utf8SliceInstructionKind.Fork, 0, 0, 0, 0));
                                frame.Stage = 1;
                                request = new(testBody, false, MirrorExecute, 1); return true;
                            case 1:
                                frame.Stage = 2;
                                request = new(conditional.YesBody, false, MirrorExecute, 0); return true;
                            case 2:
                                frame.Jump = Instructions.Count;
                                Emit(new(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                                Patch(frame.Mark, Instructions.Count);
                                frame.Stage = 3;
                                request = new(testBody, false, MirrorExecute, 2); return true;
                            case 3:
                                frame.Stage = 4;
                                request = new(conditional.NoBody, false, MirrorExecute, 0); return true;
                            default:
                                Patch(frame.Jump, Instructions.Count); break;
                        }
                        break;
                    }
                    if (frame.Stage == 0)
                    {
                        frame.Mark = Instructions.Count;
                        Emit(new(Utf8SliceInstructionKind.TestGroup, conditional.TestSlot, 0, 0, 0));
                        frame.Stage = 1;
                        request = new(conditional.YesBody, false, MirrorExecute, 0); return true;
                    }
                    if (frame.Stage == 1)
                    {
                        frame.Jump = Instructions.Count;
                        Emit(new(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                        Instructions[frame.Mark] = new(Utf8SliceInstructionKind.TestGroup, conditional.TestSlot, Instructions.Count, 0, 0);
                        frame.Stage = 2;
                        request = new(conditional.NoBody, false, MirrorExecute, 0); return true;
                    }
                    Patch(frame.Jump, Instructions.Count);
                    break;
                case Utf8SliceNode.LookaheadNode ahead:
                    return EmitLookahead(ahead.Body, ahead.Positive, ref frame, out request);
                case Utf8SliceNode.LookbehindNode behind:
                    SawLookaround = true;
                    SawCapturingLookbehind |= Facts[behind.Body].WritesGroup;
                    if (frame.Stage == 0)
                    {
                        frame.Stage = 1;
                        request = new(behind.Body, true, !behind.ForwardScalarCount.HasValue, 0); return true;
                    }
                    var body = LastSubprogram;
                    if (body is null) { Failed = true; return false; }
                    GroupCount = Math.Max(GroupCount, body.GroupCount);
                    var width = Facts[behind.Body];
                    var assertionIndex = Assertions.Count;
                    Assertions.Add(Utf8SliceAssertion.Create(body, width.Minimum, width.Maximum, true, behind.ForwardScalarCount));
                    Emit(new(Utf8SliceInstructionKind.Lookbehind, assertionIndex, behind.Positive ? 1 : 0, 0, 0));
                    break;
                case Utf8SliceNode.CaptureNode capture:
                    if (capture.Slot < 0)
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice capture slots start at zero.");
                    if (frame.Stage == 0)
                    {
                        GroupCount = Math.Max(GroupCount, capture.Slot + 1);
                        if (CaptureOpenings is { } openings && !openings.TryGetValue(capture, out frame.Opening))
                        {
                            frame.Opening = CaptureOpeningCount++;
                            openings.Add(capture, frame.Opening);
                        }
                        Emit(new(Utf8SliceInstructionKind.CaptureStart, capture.Slot, frame.Opening, 0, 0));
                        frame.Stage = 1;
                        request = new(capture.Body, false, MirrorExecute, 0); return true;
                    }
                    Emit(new(Utf8SliceInstructionKind.CaptureEnd, capture.Slot, frame.Opening, 0, 0));
                    break;
                default:
                    throw new ArgumentException($"Unsupported slice node {node.GetType().Name}.", nameof(node));
            }
            return false;
        }

        private bool IsSmallPureBody(Utf8SliceNode node, ref int remaining)
        {
            if (--remaining < 0) return false;
            Budget.ChargeWork(1);
            switch (node)
            {
                case Utf8SliceNode.LiteralNode or Utf8SliceNode.SetNode or Utf8SliceNode.ShorthandNode or
                    Utf8SliceNode.CategorySetNode or Utf8SliceNode.DotNode:
                    return true;
                case Utf8SliceNode.ConcatNode concat:
                    foreach (var child in concat.Children)
                        if (!IsSmallPureBody(child, ref remaining)) return false;
                    return true;
                case Utf8SliceNode.AlternationNode alternate:
                    foreach (var branch in alternate.Branches)
                    {
                        // Each preceding branch adds a fork and jump.
                        remaining -= 2;
                        if (remaining < 0) return false;
                        foreach (var child in branch)
                            if (!IsSmallPureBody(child, ref remaining)) return false;
                    }
                    return true;
                case Utf8SliceNode.RepeatNode { Maximum: <= 8, Body:
                    Utf8SliceNode.LiteralNode or Utf8SliceNode.SetNode or Utf8SliceNode.ShorthandNode or
                    Utf8SliceNode.CategorySetNode or Utf8SliceNode.DotNode } repeat
                    when repeat.Minimum == repeat.Maximum || repeat is { Minimum: 0, Maximum: 1 }:
                    remaining -= 3;
                    return remaining >= 0;
                default:
                    return false;
            }
        }

        private static int GetDelimitedAsciiRunByte(Utf8SliceNode body)
        {
            if (body is Utf8SliceNode.CaptureNode capture) body = capture.Body;
            return body is Utf8SliceNode.ConcatNode { Children: [
                Utf8SliceNode.RepeatNode { Minimum: 1, Maximum: long.MaxValue, Lazy: false, Body:
                    Utf8SliceNode.SetNode { Mask16.Length: 16, Negated: false, Members.Length: 0, FoldCase: false } set },
                Utf8SliceNode.LiteralNode { Value: >= 0 and < 128, FoldCase: false } delimiter] } &&
                (set.Mask16[delimiter.Value >> 3] & (1 << (delimiter.Value & 7))) == 0 ? delimiter.Value : -1;
        }

        private void CompleteNullableNonBacktrackingIteration(ref EmissionFrame frame, int continueAt)
        {
            var scope = NonBacktrackingScope ?? throw new InvalidOperationException("Missing nullable iteration scope.");
            scope.End = Instructions.Count;
            scope.ContinueAt = continueAt < 0 ? Instructions.Count + 1 : continueAt;
            frame.Scratch ??= Utf8ConstructionBuffer<int>.Create(Budget);
            frame.Scratch.Add(Instructions.Count);
            // Lifting replaces this marker with the continuation after scalar
            // progress or the patched repeat exit after an empty completion.
            Emit(new(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
            NonBacktrackingScope = frame.ParentScope;
            HasNullableNonBacktrackingLoop = true;
        }

        internal bool EmitLookahead(Utf8SliceNode body, bool positive, ref EmissionFrame frame, out EmissionRequest request)
        {
            request = default;
            SawLookaround = true;
            if (frame.Stage == 0)
            {
                frame.Stage = 1;
                if (!MirrorExecute)
                {
                    frame.Mark = Instructions.Count;
                    Emit(new(Utf8SliceInstructionKind.Fork, 0, 1, 0, 0));
                }
                request = new(body, MirrorExecute, false, 0);
                return true;
            }
            if (MirrorExecute)
            {
                var program = LastSubprogram;
                if (program is null) { Failed = true; return false; }
                GroupCount = Math.Max(GroupCount, program.GroupCount);
                var width = Facts[body];
                var index = Assertions.Count;
                Assertions.Add(Utf8SliceAssertion.Create(program, width.Minimum, width.Maximum, false));
                Emit(new(Utf8SliceInstructionKind.Lookahead, index, positive ? 1 : 0, 0, 0));
                return false;
            }
            var inlinePrefixLength = Utf8SliceAssertion.FindInlineLiteralPrefixLength(Instructions.Span,
                frame.Mark + 1, Instructions.Count, Budget);
            Emit(new(Utf8SliceInstructionKind.Commit, 0, 0, 0, 0));
            if (positive)
            {
                var jump = Instructions.Count;
                Emit(new(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                var fail = Instructions.Count;
                Emit(new(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
                Patch(frame.Mark, fail);
                Patch(jump, Instructions.Count);
            }
            else
            {
                Emit(new(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
                Patch(frame.Mark, Instructions.Count);
            }
            // Preserve the emitted edges for program analyses. The runner can
            // evaluate a proved body at the fork without materializing its frame.
            if (inlinePrefixLength >= 0)
            {
                var target = Instructions[frame.Mark].A;
                Instructions[frame.Mark] = new(Utf8SliceInstructionKind.Fork, target, 1,
                    inlinePrefixLength + 1, positive ? 1 : 0);
            }
            return false;
        }

        private int FindGuardedRepeatStopBytes(Utf8SliceNode.RepeatNode repeat, int start)
        {
            if (MirrorExecute || repeat.Lazy || repeat.Minimum != 0 || repeat.Maximum != long.MaxValue ||
                repeat.Body is not Utf8SliceNode.AlternationNode)
                return 0;
            var code = Instructions.Span;
            var atom = code.Length - 1;
            if (code[atom] is not { Kind: Utf8SliceInstructionKind.Dot, A: 1 }) return 0;

            // At other bytes the earlier arms cannot reach their first literal.
            // The final arm must be a pure negative literal assertion and a dot.
            Span<byte> stops = stackalloc byte[3];
            var count = 0;
            var position = start + 1;
            for (var arms = 0; position < atom &&
                code[position] is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } arm; arms++)
            {
                Budget.ChargeWork(33);
                if (arms == 3 || arm.A <= position || arm.A >= atom ||
                    !Utf8SliceInstruction.TryGetLiteralAfterCaptureMarkers(code, position + 1, out var first) ||
                    !AddStop(stops, ref count, first))
                    return 0;
                position = arm.A;
            }
            if (position == start + 1 || position >= atom ||
                code[position] is not { Kind: Utf8SliceInstructionKind.Fork, B: 1, C: > 0, D: 0 } guard || guard.A != atom)
                return 0;

            var assertionEnd = atom - 2;
            position++;
            Budget.ChargeWork((ulong)(assertionEnd - position));
            // A common prefix contributes just its first byte. Otherwise all
            // arms must start with a literal. An empty arm would always match.
            if (guard.C == 1)
            {
                while (position < assertionEnd && code[position] is { Kind: Utf8SliceInstructionKind.Fork } choice)
                {
                    if (!AddLiteralStop(stops, ref count, code[position + 1])) return 0;
                    position = choice.A;
                }
            }
            if (position >= assertionEnd || !AddLiteralStop(stops, ref count, code[position])) return 0;
            stops[count..].Fill(stops[count - 1]);
            return (1 << 24) | stops[0] | (stops[1] << 8) | (stops[2] << 16);

            static bool AddLiteralStop(Span<byte> bytes, ref int length, Utf8SliceInstruction literal)
                => literal is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && AddStop(bytes, ref length, literal.A);

            static bool AddStop(Span<byte> bytes, ref int length, int scalar)
            {
                if ((uint)scalar >= 0x80) return false;
                if (bytes[..length].Contains((byte)scalar)) return true;
                if (length == bytes.Length) return false;
                bytes[length++] = (byte)scalar;
                return true;
            }
        }

        private void Patch(int index, int target)
        {
            var existing = Instructions[index];
            Instructions[index] = new Utf8SliceInstruction(existing.Kind, target, existing.B, existing.C, existing.D);
        }
    }
}
