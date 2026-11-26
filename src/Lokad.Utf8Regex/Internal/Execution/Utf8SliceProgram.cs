using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
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

    internal sealed record AlternationNode(Utf8SliceNode[] Branches) : Utf8SliceNode;

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
    internal readonly struct CategorySetMember
    {
        internal CategorySetMember(Utf8SliceCategoryTable table, bool negated)
        {
            Table = table;
            Negated = negated;
            PlainCategoryMask = !negated && table is { Ranges.Length: 0, ClassProgram: null, ScalarPredicate: null }
                ? table.Mask : 0;
        }

        internal Utf8SliceCategoryTable Table { get; }
        internal bool Negated { get; }
        internal uint PlainCategoryMask { get; }
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
    internal const int RepeatExitRejectsShorterFlag = 1;
    internal const int RepeatExitAcceptsEmptyTailFlag = 2;
    internal const int RepeatExitBeforeTerminalDotFlag = 4;
    internal const int RepeatExitUnobservedDecimalFlag = 8;
    internal const int RepeatExitCoversOptionalTailFlag = 1 << 29;
    internal const int RepeatExitSharesTerminalRunFlag = 1 << 30;
    internal const int LeadingCaptureMarkerLimit = 31;
    internal const int AsciiIntervalPairFlag = 1 << 16;

    internal Utf8SliceInstruction(Utf8SliceInstructionKind kind, int a, int b, int c, int d,
        bool isLiteralChoice = false, bool isDecimalDigitSet = false)
    {
        Kind = kind;
        IsLiteralChoice = isLiteralChoice;
        IsDecimalDigitSet = isDecimalDigitSet;
        A = a;
        B = b;
        C = c;
        D = d;
    }

    internal Utf8SliceInstructionKind Kind { get; }
    // A leading set lowered from a choice retains the bounded prefix proof.
    internal bool IsLiteralChoice { get; }
    // A positive Nd category with no other members has exact ASCII digits.
    internal bool IsDecimalDigitSet { get; }
    internal bool IsInlineAssertionClass => Kind is Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
        Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.CategorySet;
    internal bool HasSingleAtomBody { get; init; }
    internal int A { get; }
    // RepeatExit uses a positive B as an exclusive byte bound to the input end.
    // A nonzero RepeatEnd B packs three ASCII stop bytes for its guarded fallback.
    internal int B { get; }
    // A backward Jump can carry an excluded starting ASCII byte plus one in C.
    // Negative Jump C holds the complemented pair of two-byte terminal loop arms.
    // A normal Fork can carry exclusive remaining byte bounds in C and D
    // for the fall-through and target respectively. Zero leaves an arm unknown.
    // A negative C on a normal Fork encodes a disjoint target byte as ~C.
    // That captured fork's target Literal can pack two following ASCII bytes here, plus one.
    // A negative Literal C holds the complemented jump of a fixed arm with a common delimiter.
    // A RepeatEnd can mark a proved consuming body for ordered discovery.
    // A leading Beginning anchor records a required first ASCII byte plus one,
    // or -1 for a folded scalar followed by a folded repeat and an end anchor.
    // A Contiguous anchor can record the exact literal width before a terminal LF-delimited line.
    // It uses -1 for a folded scalar followed by a terminal folded repeat.
    // Capture markers can record where a consecutive marker run ends.
    internal int C { get; }
    // For a positive unfolded Set, the low byte is the first ASCII byte
    // and the next byte is the interval width. Zero means no exact interval.
    // Bit 16 certifies that the following instruction tests the same interval.
    // A Literal at a fork entry can carry the destination after two equal
    // first literals fail the bounded prefix proof.
    // With negative Fork C, D can hold the packed captured prefix plus one.
    // A proved backward Jump can hold the negated ASCII body mask index plus one.
    // A Beginning or Contiguous anchor stores 1 for the short repeat proof.
    // A Beginning anchor can also store the negated start of a longer proof.
    // A RepeatEnd can pack the first instruction and byte length of a dot-tail prefix.
    internal int D { get; }

    // Inline lookahead forks use negative C for a scalar or repeat body.
    internal const int InlineRepeatAssertion = -1;
    internal const int InlineClassAssertion = -2;

    internal int AsciiIntervalStart => D & 255;
    internal int AsciiIntervalWidth => (D >> 8) & 255;
    internal bool HasAdjacentAsciiInterval => (D & AsciiIntervalPairFlag) != 0;
    internal bool MatchesAsciiInterval(int value)
        => (uint)(value - AsciiIntervalStart) < (uint)AsciiIntervalWidth;

    // RepeatExit C records independent proofs about the continuation.
    internal bool RepeatExitRejectsShorterEndpoints => (C & RepeatExitRejectsShorterFlag) != 0;
    internal bool RepeatExitAcceptsEmptyTail => (C & RepeatExitAcceptsEmptyTailFlag) != 0;
    internal bool RepeatExitBeforeTerminalDot => (C & RepeatExitBeforeTerminalDotFlag) != 0;
    internal bool RepeatExitUnobservedDecimal => (C & RepeatExitUnobservedDecimalFlag) != 0;
    internal bool RepeatExitCoversOptionalTail => (C & RepeatExitCoversOptionalTailFlag) != 0;
    internal bool RepeatExitSharesTerminalRun => (C & RepeatExitSharesTerminalRunFlag) != 0;
    // The remaining bits can pack an optional's required ASCII delimiter and its offset.
    internal int RepeatExitOptionalAsciiDelimiter => (C & ~(RepeatExitCoversOptionalTailFlag | RepeatExitSharesTerminalRunFlag)) >> 4;

    // A RepeatExit can pack up to three required ASCII starters for rewind.
    internal int RepeatExitRewindStarts => D;

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
        for (var skipped = 0; skipped < LeadingCaptureMarkerLimit && (uint)index < (uint)instructions.Length &&
            instructions[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd; skipped++)
            index++;
        return index;
    }

    // The lowering passes visit the next instruction before this marker.
    internal static void MarkCaptureRun(Span<Utf8SliceInstruction> instructions, int index)
    {
        var next = index + 1;
        if (next >= instructions.Length ||
            instructions[next].Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd))
            return;
        ref var marker = ref instructions[index];
        marker = new(marker.Kind, marker.A, marker.B, Math.Max(next + 1, instructions[next].C), marker.D);
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
        if (inlineLiteralPrefixLength < 0 && code is
            [{ IsInlineAssertionClass: true }, { Kind: Utf8SliceInstructionKind.Accept }])
            inlineLiteralPrefixLength = Utf8SliceInstruction.InlineClassAssertion;
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
    internal bool CanRunInline => InlineLiteralPrefixLength != -1;
}

internal enum Utf8SliceClassWindowCompletion : byte
{
    None,
    Whitespace,
    TerminalRuns,
    CapturedRepeat,
    CapturedSharedRun,
}

internal readonly record struct Utf8SliceClassWindowProof(
    byte LiteralLength, byte FirstSuffixClass, byte SecondSuffixClass,
    bool CanReusePrefix, bool CanAcceptAfterPair, bool? ExcludesDelimiter)
{
    internal Utf8SliceClassWindowCompletion Completion { get; init; }
    internal bool CanFinishWhitespace => Completion == Utf8SliceClassWindowCompletion.Whitespace;
    internal bool CanFinishTerminalRuns => Completion == Utf8SliceClassWindowCompletion.TerminalRuns;
    internal bool CanFinishCapturedRuns =>
        Completion is Utf8SliceClassWindowCompletion.CapturedRepeat or Utf8SliceClassWindowCompletion.CapturedSharedRun;

    internal (int First, int Second) SuffixAfter(int length)
        => length == LiteralLength ? (FirstSuffixClass, SecondSuffixClass) : default;
}

internal sealed class Utf8SliceProgram
{

    // Conservative carrier allowance; major children have separate admission.
    internal static ulong ObjectBytes =>
        512 + 4UL * (ulong)IntPtr.Size + (ulong)Unsafe.SizeOf<(int, byte, byte, byte)>() +
        (ulong)Unsafe.SizeOf<(long, Utf8SliceClassWindowProof)>() - sizeof(long) +
        (ulong)Unsafe.SizeOf<(int, int, byte[]?)>();

    private Utf8SliceProgram(
        Utf8SliceInstruction[] instructions,
        bool isNonBacktracking,
        int nonBacktrackingCaptureOpeningCount,
        int[] nonBacktrackingEpsilonOrder,
        ulong[] nonBacktrackingEpsilonAncestors,
        (ulong[]? Closures, byte[]? Leaves, uint Conditions) nonBacktrackingOrdered,
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
        Utf8CaptureCatalog? captureCatalog,
        bool usesCaptureOpeningJournal,
        bool readsCaptures,
        bool hasBalancing,
        bool ignoreCase,
        Utf8ScalarCaseBehavior caseBehavior,
        bool hasContiguousAnchor,
        bool hasConditionalTest,
        bool sawLookaround,
        bool rightToLeft,
        bool canMatchEmpty,
        bool hasUnicodePatternAtoms,
        bool usesInterpretedLazyLoopSemantics,
        int[] groupNumbers,
        string[] groupNames,
        bool requiresUnicodeWordBoundary,
        bool requiresUnicodeShorthand,
        bool isRoot, Utf8BoundedProgramEmitter.Compilation proofCompilation,
        (int Start, int TrailingBoundary, byte[]? Bytes) terminalDotTail)
    {
        Instructions = instructions;
        TerminalDotTail = terminalDotTail;
        IsNonBacktracking = isNonBacktracking;
        NonBacktrackingCaptureOpeningCount = nonBacktrackingCaptureOpeningCount;
        NonBacktrackingEpsilonOrder = nonBacktrackingEpsilonOrder;
        NonBacktrackingEpsilonAncestors = nonBacktrackingEpsilonAncestors;
        NonBacktrackingOrderedClosures = nonBacktrackingOrdered.Closures ?? [];
        NonBacktrackingOrderedLeaves = nonBacktrackingOrdered.Leaves ?? [];
        NonBacktrackingOrderedConditions = nonBacktrackingOrdered.Conditions;
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
        CaptureCatalog = captureCatalog;
        UsesCaptureOpeningJournal = usesCaptureOpeningJournal;
        ReadsCaptures = readsCaptures;
        HasBalancing = hasBalancing;
        IgnoreCase = ignoreCase;
        CaseBehavior = caseBehavior;
        HasContiguousAnchor = hasContiguousAnchor;
        HasConditionalTest = hasConditionalTest;
        SawLookaround = sawLookaround;
        RightToLeft = rightToLeft;
        CanMatchEmpty = canMatchEmpty;
        HasUnicodePatternAtoms = hasUnicodePatternAtoms;
        UsesInterpretedLazyLoopSemantics = usesInterpretedLazyLoopSemantics;
        GroupNumbers = groupNumbers;
        GroupNames = groupNames;
        RequiresUnicodeWordBoundary = requiresUnicodeWordBoundary;
        RequiresUnicodeShorthand = requiresUnicodeShorthand;
        if (!rightToLeft && !isNonBacktracking && repeatCount != 0)
            TerminalRepeatsWithoutRewind = Utf8SliceRunner.FindTerminalRepeatsWithoutRewind(this, Utf8ConstructionScope.CurrentBudget);
        // An assertion executes at an edge chosen by its parent. It uses the
        // repeat proofs inside RunSegment and never searches for its own origin.
        if (!isRoot) return;

        // Instructions and flags are complete before publishing the program.
        // Keep this bounded, allocation-free proof off repeated match attempts.
        TerminalRoot = Utf8SliceRunner.ClassifyTerminalRoot(this);
        HasSingleCapturePerGroup = CanReadCapturesFromSlots();
        if (!rightToLeft && !isNonBacktracking)
        {
            var first = instructions[0];
            if (first is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning })
            {
                var ascii = !ignoreCase && !canMatchEmpty && instructions.Length > 1 &&
                    instructions[1] is { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } literal
                    ? literal.A + 1 : 0;
                var proof = first.D;
                if (proof == 0 && repeatCount != 0)
                    proof = Utf8SliceRunner.PrepareAnchoredRepeatProof(this, Utf8ConstructionScope.CurrentBudget);
                if (proof == -1 && first.B == 0 && instructions.Length == 8 &&
                    instructions[6] is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd, B: 0 } &&
                    instructions[7].Kind == Utf8SliceInstructionKind.Accept &&
                    HasFoldedLeadingRepeat(instructions, setMasks))
                    ascii = -1;
                if (ascii != 0 || proof != 0)
                    instructions[0] = new(first.Kind, first.A, first.B, ascii, proof);
            }
            else if (first is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Contiguous } &&
                !readsCaptures && repeatCount != 0 && instructions.Length <= 32)
            {
                var index = 1;
                var exactPrefix = true;
                if (groupCount == 0)
                {
                    // These atoms can share the bounded walk with a repeat
                    // whose greedy endpoint needs no rewind.
                    while (index < instructions.Length - 1 &&
                        instructions[index] is ({ Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 } or
                            { Kind: Utf8SliceInstructionKind.Set, B: 0, C: not 0 }))
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        exactPrefix &= instructions[index].Kind == Utf8SliceInstructionKind.Literal;
                        index++;
                    }
                }
                if (instructions[index] is { Kind: Utf8SliceInstructionKind.RepeatGreedy } repeat &&
                    (index == 1 || repeat.HasSingleAtomBody) &&
                    (instructions[repeat.A].RepeatExitRejectsShorterEndpoints ||
                        (index == 1 || repeat.RepeatMaximum == long.MaxValue) &&
                        instructions[repeat.A].RepeatExitAcceptsEmptyTail))
                {
                    var terminalPrefix = exactPrefix && index > 1 && instructions.AsSpan(index) is
                    [
                        { Kind: Utf8SliceInstructionKind.RepeatGreedy, RepeatMinimum: 0, RepeatMaximum: long.MaxValue, HasSingleAtomBody: true },
                        { Kind: Utf8SliceInstructionKind.Dot, A: 0 }, _, _,
                        { Kind: Utf8SliceInstructionKind.Literal, A: '\n', B: 0 }, { Kind: Utf8SliceInstructionKind.Accept }
                    ] ? index - 1 : 0;
                    if (instructions.Length == 7 && instructions[6].Kind == Utf8SliceInstructionKind.Accept &&
                        HasFoldedLeadingRepeat(instructions, setMasks))
                        terminalPrefix = -1;
                    instructions[0] = new(first.Kind, first.A, first.B, terminalPrefix, index == 1 ? 1 : -1);
                }
            }
            else if (!canMatchEmpty && !hasContiguousAnchor)
            {
                if (caseBehavior == Utf8ScalarCaseBehavior.Invariant &&
                    first is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 1 })
                {
                    // The lowered invariant K/k mask still needs scalar
                    // folding. Its only wider equivalent is Kelvin.
                    Utf8ConstructionScope.ChargeWork(1);
                    var mask = SetMasks[first.A].AsSpan();
                    const ulong kelvinLetters = (1UL << ('K' - 64)) | (1UL << ('k' - 64));
                    if (BinaryPrimitives.ReadUInt64LittleEndian(mask) == 0 &&
                        BinaryPrimitives.ReadUInt64LittleEndian(mask[sizeof(ulong)..]) == kelvinLetters)
                        LeadingByteCandidate = ('K', (byte)'k', -1, 0, 0, false, true);
                }
                else if (!ignoreCase || first is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 })
                    LeadingByteCandidate = Utf8SliceRunner.FindLeadingByteCandidate(this, Utf8ConstructionScope.CurrentBudget);
            }
        }
        if (!rightToLeft && !isNonBacktracking && !ignoreCase && !hasContiguousAnchor && repeatCount != 0)
        {
            // The class/delimiter shape is independent of the subject and its
            // search plan. Charge for the bounded inspection before it starts.
            var construction = Utf8ConstructionScope.CurrentBudget;
            construction.ChargeWork((ulong)Math.Min(instructions.Length, 64));
            var literal = Utf8SliceRunner.FindLeadingClassDelimiter(this,
                out var leadingClass, out var literalIndex, out var gapMaximum, out var direct);
            LeadingClassDelimiter = (literal, leadingClass, literalIndex, gapMaximum, direct);
            if (direct)
                LeadingClassWindowProof = Utf8SliceRunner.PrepareDirectClassWindow(
                    this, leadingClass, literalIndex, (byte)literal, construction);
        }
        var leadingIndex = LeadingByteCandidate.InstructionIndex;
        LeadingScalarIsDecimal = leadingIndex >= 0 && instructions[leadingIndex].IsDecimalDigitSet;
        if (groupCount == 0 && LeadingByteCandidate is { First: >= 0 and < 128, FromBranch: true, InstructionIndex: 0 } &&
            instructions[0].Kind == Utf8SliceInstructionKind.Fork)
            RequiredBranchLiteral = Utf8SliceRunner.PrepareRequiredBranchLiteral(this);
        if (leadingIndex > 0 && repeatCount != 0)
        {
            Utf8ConstructionScope.CurrentBudget.ChargeWork(4);
            LeadingRepeatLiteralWindow = Utf8SliceRunner.GetShortRepeatLiteralWindow(this, leadingIndex);
        }
        if (LeadingScalarIsDecimal && LeadingClassDelimiter is { Direct: true, LiteralIndex: >= 0 } delimiter &&
            instructions[0] is { Kind: Utf8SliceInstructionKind.Boundary, A: 1 } boundary)
        {
            Utf8ConstructionScope.CurrentBudget.ChargeWork(4);
            FollowingDecimalLiteralWindow = Utf8SliceRunner.GetShortRepeatLiteralWindow(this, delimiter.LiteralIndex + 2);
            if (FollowingDecimalLiteralWindow is { Min: > 0, Literal: var next } &&
                !Utf8AsciiBytePredicates.IsDigit(next) && instructions[delimiter.LiteralIndex + 2].IsDecimalDigitSet)
            {
                var index = delimiter.LiteralIndex + 6;
                Utf8ConstructionScope.CurrentBudget.ChargeWork(4);
                // The class delimiter has already proved forward, case-sensitive
                // search with no contiguous anchor. Cache the remaining program tests.
                var aligned = !readsCaptures && boundary is { B: (int)Utf8SliceWordKind.Unicode, C: 0 } &&
                    delimiter is { LeadingClass: 2, LiteralIndex: 5, GapMaximum: 0 } &&
                    !Utf8AsciiBytePredicates.IsDigit((byte)delimiter.Literal) &&
                    instructions[1].RepeatMinimum == 1 && SkipsTerminalRewind(1);
                if (instructions.Length - index == 6 &&
                    instructions[index] is { Kind: Utf8SliceInstructionKind.RepeatGreedy, HasSingleAtomBody: true,
                        RepeatMinimum: > 0 and var minimum, RepeatMaximum: <= 4 and var maximum } &&
                    instructions[index + 1].IsDecimalDigitSet &&
                    instructions[index + 4] is { Kind: Utf8SliceInstructionKind.Boundary,
                        A: 1, B: (int)Utf8SliceWordKind.Unicode, C: 0 } &&
                    instructions[index + 5].Kind == Utf8SliceInstructionKind.Accept)
                    FollowingDecimalContinuation = (index, (byte)minimum, (byte)maximum, aligned);
                else
                    FollowingDecimalContinuation = (index, 0, 0, aligned);
            }
        }
        if (isNonBacktracking && instructions.Length > 64)
            NonBacktrackingWideMasks = Utf8SliceRunner.PrepareWideRegularMasks(this, Utf8ConstructionScope.CurrentBudget);
        if (NonBacktrackingOrderedClosures.Length != 0)
            NonBacktrackingOrderedStartBytes = Utf8SliceRunner.GetOrderedRegularStartBytes(this, Utf8ConstructionScope.CurrentBudget);
        else if (!rightToLeft && isNonBacktracking && !canMatchEmpty && instructions.Length > 64)
            NonBacktrackingOrderedStartBytes = Utf8SliceRunner.GetWideRegularStartBytes(this, Utf8ConstructionScope.CurrentBudget);
        // An absolute start confines an empty match to byte zero. A terminal
        // anchor can then still exclude the last scalar on nonempty extents.
        if (!rightToLeft && !isNonBacktracking && (!canMatchEmpty || instructions is
            [{ Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning, B: 0 }, ..]))
        {
            RequiredFinalAscii = FindRequiredFinalAscii(Utf8ConstructionScope.CurrentBudget, out var unanchoredByte);
            RequiredUnanchoredFinalByte = unanchoredByte;
        }
        if (proofCompilation == Utf8BoundedProgramEmitter.Compilation.TieredBranches && !HasCaptureFreeAsciiBranch)
            proofCompilation = Utf8BoundedProgramEmitter.Compilation.None;
        var eager = proofCompilation is Utf8BoundedProgramEmitter.Compilation.Eager or Utf8BoundedProgramEmitter.Compilation.EagerOrTiered;
        if (eager || proofCompilation == Utf8BoundedProgramEmitter.Compilation.Tiered)
            EmittedHistoryOffsets = Utf8BoundedProgramEmitter.PrepareHistoryOffsets(this, Utf8ConstructionScope.CurrentBudget);
        if (eager)
            EmittedMatch = Utf8BoundedProgramEmitter.TryEmit(this, Utf8ConstructionScope.CurrentBudget);
        if (EmittedMatch is null && proofCompilation is not
            (Utf8BoundedProgramEmitter.Compilation.None or Utf8BoundedProgramEmitter.Compilation.Eager))
        {
            var budget = Utf8ConstructionScope.CurrentBudget;
            if (Utf8BoundedProgramEmitter.CanTierUp(this, budget, proofCompilation))
            {
                // Branch-only consumers recover compilation cost on searches
                // that have more than a single candidate's bounded work.
                var minimumVisits = proofCompilation is Utf8BoundedProgramEmitter.Compilation.Tiered or Utf8BoundedProgramEmitter.Compilation.EagerOrTiered
                    ? 1 : Utf8SliceRunner.BranchedPrefixInstructionLimit + 1;
                _proofTier = budget.AllocateObject(minimumVisits, static visits => new ProofTier(visits));
            }
        }
    }

    private static bool HasFoldedLeadingRepeat(ReadOnlySpan<Utf8SliceInstruction> instructions, byte[][] setMasks)
        => instructions is
            [_, { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 1 } head,
                { Kind: Utf8SliceInstructionKind.RepeatGreedy, HasSingleAtomBody: true, RepeatMinimum: 0, RepeatMaximum: long.MaxValue },
                { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 1 } body, ..] &&
            setMasks[head.A].Length == 16 && setMasks[body.A].Length == 16;

    internal static Utf8SliceProgram Create(Utf8SliceInstruction[] instructions, byte[][] setMasks, Utf8SliceNode.CategorySetMember[][] categorySetMembers, int repeatCount, int groupCount, Utf8SliceAssertion[] assertions, string[] groupNames, bool hasBalancing, bool ignoreCase, bool hasContiguousAnchor, bool hasConditionalTest, bool rightToLeft, bool sawLookaround, bool canMatchEmpty, Utf8CaptureCatalog? captureCatalog, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax, Utf8ScalarCaseBehavior caseBehavior, bool isNonBacktracking, int nonBacktrackingCaptureOpeningCount, int[] nonBacktrackingEpsilonOrder, ulong[] nonBacktrackingEpsilonAncestors, Func<int, int>[] backreferenceLowerings,
        bool isRoot, (ulong[]? Closures, byte[]? Leaves, uint Conditions) nonBacktrackingOrdered = default,
        Utf8BoundedProgramEmitter.Compilation proofCompilation = default)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        var usesCaptureOpeningJournal = captureCatalog is { HasSharedCaptureDeclarations: true };
        var canUseCompletedCapture = groupCount == 1 && !rightToLeft && !hasBalancing &&
            !hasConditionalTest && !sawLookaround && assertions.Length == 0;
        var captureIsOpen = false;
        var hasClosedCapture = false;
        var readsCaptures = false;
        var requiresUnicodeWordBoundary = false;
        var requiresUnicodeShorthand = false;
        var hasUnicodePatternAtoms = false;
        var hasNonFixedLookbehind = false;
        var hasOrderSensitiveLookbehind = false;
        var nonBacktrackingOrderedForwardOnly = nonBacktrackingOrdered.Closures is { Length: > 0 };
        var terminalDotTail = default((int Start, int TrailingBoundary, byte[]? Bytes));
        ulong consumers = 0, accepts = 0;
        budget.ChargeWork((ulong)instructions.Length);
        for (var pc = 0; pc < instructions.Length; pc++)
        {
            var instruction = instructions[pc];
            if (instruction.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy)
            {
                // Record the final repeat layout once for runtime verification.
                instructions[pc] = instruction with { HasSingleAtomBody = Utf8SliceRunner.IsSingleAtomRepeat(instructions, pc, instruction) };
                if (!rightToLeft && !isNonBacktracking)
                {
                    Utf8SliceRunner.PrepareScalarRepeatContinuation(instructions, setMasks, categorySetMembers, pc, budget);
                    if (!hasBalancing && terminalDotTail.Bytes is null &&
                        instructions[pc].HasSingleAtomBody && instructions[pc + 1].Kind == Utf8SliceInstructionKind.Dot &&
                        instructions[instruction.A - 1].D is > 0 and var packedTail)
                        terminalDotTail = Utf8SliceRunner.PrepareTerminalDotTail(instructions, packedTail >> 4, budget);
                }
            }
            else if (instruction.Kind == Utf8SliceInstructionKind.Jump && !rightToLeft && !isNonBacktracking)
                Utf8SliceRunner.MarkExcludedLoopStart(instructions, setMasks, pc, budget);
            else if (instruction.Kind == Utf8SliceInstructionKind.Fork && !rightToLeft && !isNonBacktracking)
                Utf8SliceRunner.PrepareAsciiFork(instructions, setMasks, pc, budget);
            else if (instruction is { Kind: Utf8SliceInstructionKind.Set, D: > 0 } &&
                !rightToLeft && !isNonBacktracking && pc + 1 < instructions.Length)
            {
                budget.ChargeWork(1);
                if (instructions[pc + 1] is { Kind: Utf8SliceInstructionKind.Set } follower && follower.D == instruction.D)
                    instructions[pc] = new(instruction.Kind, instruction.A, instruction.B, instruction.C,
                        instruction.D | Utf8SliceInstruction.AsciiIntervalPairFlag, instruction.IsLiteralChoice);
            }
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
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Boundary:
                    requiresUnicodeWordBoundary = true;
                    hasUnicodePatternAtoms |= instruction.C != 0;
                    break;
                case Utf8SliceInstructionKind.CategorySet:
                case Utf8SliceInstructionKind.Shorthand:
                case Utf8SliceInstructionKind.Hybrid:
                    requiresUnicodeShorthand = true;
                    hasUnicodePatternAtoms |= instruction.Kind == Utf8SliceInstructionKind.CategorySet;
                    break;
                case Utf8SliceInstructionKind.Literal:
                    hasUnicodePatternAtoms |= instruction.A >= 128;
                    break;
            }
            if (nonBacktrackingEpsilonAncestors.Length == 0) continue;
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
                case Utf8SliceInstructionKind.RepeatEnd:
                    nonBacktrackingOrderedForwardOnly = false;
                    break;
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                case Utf8SliceInstructionKind.RepeatExit:
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
            requiresUnicodeWordBoundary |= assertion.Body.RequiresUnicodeWordBoundary;
            requiresUnicodeShorthand |= assertion.Body.RequiresUnicodeShorthand;
            hasUnicodePatternAtoms |= assertion.Body.HasUnicodePatternAtoms;
            hasNonFixedLookbehind |= assertion.Body.HasNonFixedLookbehind;
            hasOrderSensitiveLookbehind |= assertion.Body.HasOrderSensitiveLookbehind;
            hasConditionalTest |= assertion.Body.HasConditionalTest;
            usesLiteralClassBracketSyntax |= assertion.Body.UsesLiteralClassBracketSyntax;
            if (assertion.IsLookbehind)
            {
                hasNonFixedLookbehind |= assertion.MinLengthBytes != assertion.MaxLengthBytes;
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
            nonBacktrackingOrdered,
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
            captureCatalog,
            usesCaptureOpeningJournal,
            readsCaptures,
            hasBalancing,
            ignoreCase,
            caseBehavior,
            hasContiguousAnchor,
            hasConditionalTest,
            sawLookaround,
            rightToLeft,
            canMatchEmpty,
            hasUnicodePatternAtoms,
            usesInterpretedLazyLoopSemantics,
            groupNumbers,
            groupNames,
            requiresUnicodeWordBoundary,
            requiresUnicodeShorthand,
            isRoot, proofCompilation, terminalDotTail);
        construction.Complete(); return result;
    }

    internal Utf8SliceInstruction[] Instructions { get; }
    internal (int Start, int TrailingBoundary, byte[]? Bytes) TerminalDotTail { get; }
    internal bool IsNonBacktracking { get; }
    internal int NonBacktrackingCaptureOpeningCount { get; }
    internal int[] NonBacktrackingEpsilonOrder { get; }
    internal ulong[] NonBacktrackingWideMasks { get; } = [];
    internal ulong[] NonBacktrackingEpsilonAncestors { get; }
    internal ulong[] NonBacktrackingOrderedClosures { get; }
    internal byte[] NonBacktrackingOrderedLeaves { get; }
    internal uint NonBacktrackingOrderedConditions { get; }
    internal bool NonBacktrackingOrderedForwardOnly { get; }
    internal PreparedByteSearch NonBacktrackingOrderedStartBytes { get; }
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
    internal bool RightToLeft { get; }
    internal bool CanMatchEmpty { get; }
    internal bool HasUnicodePatternAtoms { get; }
    internal Utf8SliceTerminalRootKind TerminalRoot { get; }
    // Every capture in this complete root participates and closes in slot order.
    internal bool HasTerminalCaptureRoot => TerminalRoot == Utf8SliceTerminalRootKind.Capture;
    internal bool HasSingleCapturePerGroup { get; }
    internal (int First, byte Last, int Second, byte SecondLast, int InstructionIndex, bool FromBranch, bool SparsePair) LeadingByteCandidate { get; }
        = (-1, 0, -1, 0, -1, false, false);
    internal bool HasCaptureFreeAsciiBranch => GroupCount == 0 &&
        LeadingByteCandidate is { First: >= 0 and < 128, FromBranch: true, InstructionIndex: 0 };
    internal (int Instruction, byte Min, byte Max, byte Value) RequiredBranchLiteral { get; }
    internal bool LeadingByteAdmitsKelvin => LeadingByteCandidate is { First: 'K', InstructionIndex: 0, SparsePair: true } &&
        Instructions[0] is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 1 };
    internal (int Literal, int LeadingClass, int LiteralIndex, long GapMaximum, bool Direct) LeadingClassDelimiter { get; }
        = (-1, -1, -1, 0, false);
    internal Utf8SliceClassWindowProof LeadingClassWindowProof { get; }
    internal bool LeadingScalarIsDecimal { get; }
    internal bool HasShortDecimalHead => LeadingScalarIsDecimal && Instructions.Length <= 32 &&
        LeadingByteCandidate.InstructionIndex is > 0 and var index &&
        Instructions[index - 1] is { Kind: Utf8SliceInstructionKind.RepeatGreedy,
            RepeatMinimum: > 0, RepeatMaximum: <= 4 };
    internal (int Min, int Max, byte Literal) LeadingRepeatLiteralWindow { get; }
    internal (int Min, int Max, byte Literal) FollowingDecimalLiteralWindow { get; }
    internal (int Index, byte TerminalMin, byte TerminalMax, bool Aligned) FollowingDecimalContinuation { get; }
    internal Utf8BoundedProgramEmitter.Match? EmittedMatch { get; }
    internal int[]? EmittedHistoryOffsets { get; }
    internal const int ProofTierThreshold = 512 * 1024;
    // Construction admits a separate state object only for eligible roots.
    // Published instructions and their metadata remain immutable.
    private readonly ProofTier? _proofTier;
    internal bool ObservesProofWork => _proofTier is { } tier && Volatile.Read(ref tier.InterpretedVisits) >= 0;
    internal Utf8BoundedProgramEmitter.Match? TieredProof => _proofTier?.Emitted;

    private sealed class ProofTier(int minimumVisits)
    {
        internal readonly int MinimumVisits = minimumVisits;
        internal int InterpretedVisits; // Negative once preparation is claimed.
        internal volatile Utf8BoundedProgramEmitter.Match? Emitted;
    }

    internal void ObserveCaptureProofWork(ref Utf8SliceInvocation invocation)
    {
        // An accepted straight-line capture result accounts for one bounded
        // walk. Candidate misses and controlled invocations add no credit.
        if (EmittedHistoryOffsets is not null && ObservesProofWork &&
            invocation.IsUnmetered && invocation.CheckpointHook is null)
            ObserveProofWork(Instructions.Length);
    }

    internal void ObserveProofWork(int visits)
    {
        if (_proofTier is not { } tier || visits < tier.MinimumVisits) return;
        int previous, next;
        do
        {
            previous = Volatile.Read(ref tier.InterpretedVisits);
            if (previous < 0) return;
            next = previous + visits;
            if (next >= ProofTierThreshold)
            {
                // A host callback may be inside another construction owner.
                if (Utf8ConstructionScope.IsActive) return;
                next = -1;
            }
        } while (Interlocked.CompareExchange(ref tier.InterpretedVisits, next, previous) != previous);

        if (next >= 0) return;
        using var construction = Utf8ConstructionScope.Enter();
        try
        {
            var emitted = Utf8BoundedProgramEmitter.TryEmit(this, construction.Budget, allowSpanRuns: true);
            construction.Complete();
            tier.Emitted = emitted;
        }
        catch (Utf8ResourceLimitExceededException)
        {
            // A failed speculative optimization leaves the interpreter available.
        }
    }

    private (ulong Mask, int Anchor) TerminalRepeatsWithoutRewind { get; }
    internal int TerminalRepeatAnchor => TerminalRepeatsWithoutRewind.Anchor;
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
        => GetCompleteAsciiCategoryMask(atom, CategorySetMembers);

    internal static uint GetCompleteAsciiCategoryMask(in Utf8SliceInstruction atom,
        Utf8SliceNode.CategorySetMember[][] categories)
        => atom is { Kind: Utf8SliceInstructionKind.CategorySet, B: 0, C: 0 } &&
            categories[atom.D] is [{ PlainCategoryMask: var mask }] ? mask : 0;

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
        if (atom.IsDecimalDigitSet)
        {
            ascii = Utf8SearchAsciiSet.FromMasks(Utf8AsciiBytePredicates.DecimalMask, 0);
            return true;
        }
        if (atom is not { Kind: Utf8SliceInstructionKind.Set, C: 0 } && GetCompleteAsciiCategoryMask(atom) == 0)
            return false;
        ascii = ReadAsciiSetMask(atom);
        asciiOnly = atom is { Kind: Utf8SliceInstructionKind.Set, B: 0 };
        return true;
    }

    internal Utf8SearchAsciiSet ReadAsciiSetMask(in Utf8SliceInstruction atom)
    {
        var mask = SetMasks[atom.A].AsSpan();
        var low = BinaryPrimitives.ReadUInt64LittleEndian(mask);
        var high = BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]);
        if (atom.B != 0) { low = ~low; high = ~high; }
        return Utf8SearchAsciiSet.FromMasks(low, high);
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
            if (CanMatchEmpty || LeadingByteCandidate is not { FromBranch: true, First: >= 0 and < 128 } ||
                code[^2] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0, A: >= 0 and < 128 }) return default;
            end = code.Length - 1;
        }

        // An atom can provide the final scalar if its continuation can reach
        // the anchor or Accept without consuming. A required scalar repeat
        // rules out earlier atoms. Other repeat bodies may be nullable.
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
                    empty[pc] = instruction switch
                    {
                        { Kind: Utf8SliceInstructionKind.Fork } => empty[instruction.A] || next,
                        { Kind: Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy,
                            HasSingleAtomBody: true, RepeatMinimum: > 0 } when code[pc + 1].Kind is
                            Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Dot or
                            Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.Shorthand => false,
                        _ => empty[instruction.A]
                    };
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
    // Consumers with ASCII-only boundaries decline programs using Unicode boundaries.
    internal bool RequiresUnicodeWordBoundary { get; }

    // Consumers with ASCII-only predicates decline shorthands, hybrid classes and Unicode categories.
    internal bool RequiresUnicodeShorthand { get; }

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

    internal static Utf8SliceProgram? Lower(Utf8SliceNode root, string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool rightToLeft, Utf8CaptureCatalog? captureCatalog, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax, Utf8ScalarCaseBehavior caseBehavior, bool nonBacktracking,
        bool prepareUnbranchedSearch = false, Utf8BoundedProgramEmitter.Compilation proofCompilation = default)
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
                    var program = FinishLowering(owner, frame.Root, frames.Count == 0,
                        usesInterpretedLazyLoopSemantics, usesLiteralClassBracketSyntax, prepareUnbranchedSearch, proofCompilation);
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

    private readonly record struct NodeFacts(bool CanMatchEmpty, bool IsZeroWidth, int Minimum, int Maximum);

    private static bool TryGetLeafFacts(Utf8SliceNode node, bool nonBacktracking, out NodeFacts facts)
    {
        switch (node)
        {
            case Utf8SliceNode.LiteralNode literal:
                if (!Rune.IsValid(literal.Value))
                {
                    facts = new(false, nonBacktracking, 0, 0);
                    return true;
                }
                var width = literal.Value < 0x80 ? 1 : literal.Value < 0x800 ? 2 : literal.Value < 0x10000 ? 3 : 4;
                facts = literal.FoldCase ? new(false, false, 1, 4) : new(false, false, width, width);
                return true;
            case Utf8SliceNode.EmptyNode or Utf8SliceNode.AnchorNode or Utf8SliceNode.BoundaryNode:
                facts = new(true, nonBacktracking, 0, 0);
                return true;
            case Utf8SliceNode.FailNode:
                facts = new(false, nonBacktracking, 0, int.MaxValue);
                return true;
            case Utf8SliceNode.SetNode set:
                facts = new(false, false, 1, set.Negated || set.FoldCase || set.Members.Length != 0 ? 4 : 1);
                return true;
            case Utf8SliceNode.DotNode or Utf8SliceNode.ShorthandNode or Utf8SliceNode.CategorySetNode:
                facts = new(false, false, 1, 4);
                return true;
            case Utf8SliceNode.BackrefNode:
                facts = new(true, false, 0, int.MaxValue);
                return true;
            default:
                facts = default;
                return false;
        }
    }

    private static NodeFacts ReadNodeFacts(Utf8SliceNode node, Utf8ConstructionMap<Utf8SliceNode, NodeFacts>? facts,
        bool nonBacktracking, Utf8ConstructionBudget budget)
    {
        if (!TryGetLeafFacts(node, nonBacktracking, out var leaf)) return facts![node];
        budget.ChargeWork(1);
        return leaf;
    }

    private static Utf8ConstructionMap<Utf8SliceNode, NodeFacts>? AnalyzeNodes(Utf8SliceNode root, bool nonBacktracking, Utf8ConstructionBudget budget)
    {
        // Slice nodes are records. Reference identity avoids recursive hashing.
        // The main and assertion programs share composite facts. Leaf facts
        // are derived on demand, so this traversal visits only composite nodes.
        if (TryGetLeafFacts(root, nonBacktracking, out _)) return null;

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
                    frames[index] = frame;
                    if (!TryGetLeafFacts(child, nonBacktracking, out _) && !facts.TryGetValue(child, out _))
                        frames.Push(new(child));
                    continue;
                }
                frames.Pop();
                facts[frame.Node] = Summarize(frame.Node);
            }
            return facts;
        }
        catch { facts.Dispose(); throw; }

        NodeFacts Get(Utf8SliceNode node) => ReadNodeFacts(node, facts, nonBacktracking, budget);

        NodeFacts Summarize(Utf8SliceNode node)
        {
            switch (node)
            {
                case Utf8SliceNode.LookaheadNode or Utf8SliceNode.LookbehindNode:
                    return new(true, nonBacktracking, 0, 0);
                case Utf8SliceNode.CaptureNode capture:
                    return Get(capture.Body);
                case Utf8SliceNode.BalanceNode balance:
                    return Get(balance.Body) with { CanMatchEmpty = true };
                case Utf8SliceNode.AtomicNode atomic:
                    return Get(atomic.Body);
                case Utf8SliceNode.ConditionalNode conditional:
                    var yes = Get(conditional.YesBody);
                    var no = Get(conditional.NoBody);
                    return new(yes.CanMatchEmpty || no.CanMatchEmpty, yes.IsZeroWidth && no.IsZeroWidth,
                        Math.Min(yes.Minimum, no.Minimum), Math.Max(yes.Maximum, no.Maximum));
                case Utf8SliceNode.ConcatNode concat:
                    return SummarizeSequence(concat.Children);
                case Utf8SliceNode.AlternationNode alternation:
                    var anyEmpty = false;
                    var allZeroWidth = nonBacktracking;
                    var minimum = int.MaxValue;
                    var maximum = 0;
                    foreach (var branch in alternation.Branches)
                    {
                        budget.ChargeWork(1);
                        var fact = Get(branch);
                        anyEmpty |= fact.CanMatchEmpty;
                        allZeroWidth &= fact.IsZeroWidth;
                        minimum = Math.Min(minimum, fact.Minimum);
                        maximum = Math.Max(maximum, fact.Maximum);
                    }
                    return new(anyEmpty, allZeroWidth, alternation.Branches.Length == 0 ? 0 : minimum, maximum);
                case Utf8SliceNode.RepeatNode repeat:
                    var body = Get(repeat.Body);
                    return new(repeat.Minimum == 0 || body.CanMatchEmpty, nonBacktracking && (repeat.Maximum == 0 || body.IsZeroWidth),
                        Saturate((long)body.Minimum * repeat.Minimum),
                        repeat.Maximum == long.MaxValue || body.Maximum == int.MaxValue ? int.MaxValue : Saturate((long)body.Maximum * repeat.Maximum));
                default:
                    return new(false, false, 0, int.MaxValue);
            }
        }

        NodeFacts SummarizeSequence(Utf8SliceNode[] nodes)
        {
            var empty = true;
            var zeroWidth = nonBacktracking;
            long minimum = 0, maximum = 0;
            foreach (var node in nodes)
            {
                var fact = Get(node);
                empty &= fact.CanMatchEmpty;
                zeroWidth &= fact.IsZeroWidth;
                minimum += fact.Minimum;
                maximum += fact.Maximum;
            }
            return new(empty, zeroWidth, Saturate(minimum), Saturate(maximum));
        }

        static int Saturate(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
    }

    private struct AnalysisFrame(Utf8SliceNode node)
    {
        internal readonly Utf8SliceNode Node = node;
        private int _index;

        internal Utf8SliceNode? NextChild(Utf8ConstructionBudget budget)
        {
            budget.ChargeWork(1);
            switch (Node)
            {
                case Utf8SliceNode.ConcatNode concat:
                    return _index == concat.Children.Length ? null : concat.Children[_index++];
                case Utf8SliceNode.AlternationNode alternate:
                    return _index == alternate.Branches.Length ? null : alternate.Branches[_index++];
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
        internal int Stage, Index, Mark, Jump, RepeatId, Opening;
        internal long Minimum, Maximum;
        internal bool NullableLoop;
        internal Utf8NonBacktrackingLoopScope? ParentScope;
        internal Utf8ConstructionBuffer<int>? Scratch;
    }

    private static Utf8SliceProgram? FinishLowering(Lowering lowerer, Utf8SliceNode root, bool isRoot,
        bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax, bool prepareUnbranchedSearch,
        Utf8BoundedProgramEmitter.Compilation proofCompilation)
    {
        lowerer.Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Accept, 0, 0, 0, 0));
        var assertions = lowerer.Assertions?.Take() ?? [];

        // Hand-built trees (tests, assertion bodies without renumbering
        // context) carry no names; decimal ordinals identify their slots since
        // only the parser admits named shapes, always with real names.
        var resolvedNames = lowerer.GroupNames;
        if (resolvedNames is null)
        {
            Span<char> numberText = stackalloc char[10];
            resolvedNames = lowerer.Budget.AllocateArray<string>(lowerer.GroupCount + 1, false);
            for (var number = 0; number <= lowerer.GroupCount; number++)
            {
                lowerer.Budget.ChargeWork(1);
                if (!((uint)number).TryFormat(numberText, out var written))
                    throw new InvalidOperationException("A scalar group number exceeds its decimal scratch.");
                resolvedNames[number] = lowerer.Budget.CopyText(numberText[..written]);
            }
        }

        var instructions = lowerer.Instructions.Take();
        if (!lowerer.MirrorExecute && !lowerer.NonBacktracking && lowerer.RepeatCount != 0)
            MarkBoundedEndTails(instructions, lowerer.HasAlternation, lowerer.Budget);
        if (lowerer.HasNullableNonBacktrackingLoop && lowerer.NonBacktrackingScopes is { } scopes)
        {
            var lifted = Utf8NonBacktrackingLoopGraph.Lift(instructions, scopes, lowerer.Budget);
            lowerer.Budget.ReleaseArray(instructions, true);
            instructions = lifted;
        }
        ulong[] epsilonAncestors = [];
        (ulong[] Closures, byte[] Leaves, uint Conditions) ordered = ([], [], 0);
        var prepareEpsilon = lowerer.NonBacktracking ||
            isRoot && lowerer.HasCounterlessAlternationLoop && lowerer.RepeatCount == 0 &&
            Utf8SliceRunner.CanPrepareOrdinaryOrderedSearch(instructions, lowerer.Budget);
        var epsilonOrder = prepareEpsilon
            ? Utf8NonBacktrackingSearchState.BuildEpsilonOrder(instructions, out epsilonAncestors, out ordered)
            : isRoot && lowerer.RepeatCount != 0 && (lowerer.HasAlternation || prepareUnbranchedSearch)
                ? Utf8SliceRunner.BuildOrdinaryRepeatClosures(instructions, lowerer.HasAlternation,
                    lowerer.Budget, out epsilonAncestors, out ordered)
                : [];
        return Utf8SliceProgram.Create(
            instructions,
            lowerer.SetMasks?.Take() ?? [],
            lowerer.CategorySetMembers?.Take() ?? [],
            lowerer.RepeatCount,
            lowerer.GroupCount,
            assertions,
            resolvedNames, lowerer.MaintainBalance, lowerer.MatchCaseInsensitive, lowerer.SawContiguousAnchor, lowerer.HasConditionalTest, lowerer.MirrorExecute, lowerer.SawLookaround, lowerer.GetFacts(root).CanMatchEmpty, lowerer.CaptureCatalog,
            isRoot && usesInterpretedLazyLoopSemantics, isRoot && usesLiteralClassBracketSyntax, lowerer.CaseBehavior,
            lowerer.NonBacktracking, lowerer.CaptureOpeningCount,
            epsilonOrder, epsilonAncestors,
            lowerer.BackreferenceLowerings?.Take() ?? [], isRoot, ordered, proofCompilation);
    }

    private static void MarkBoundedEndTails(Span<Utf8SliceInstruction> code, bool hasAlternation, Utf8ConstructionBudget budget)
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
        var shortAsciiAlternation = hasAlternation && code.Length <= 32;
        for (var index = end - 1; index >= start; index--)
        {
            budget.ChargeWork(1);
            var instruction = code[index];
            var next = limits[index - start + 1];
            long limit = 0;
            var width = ScalarMaximumBytes(instruction);
            shortAsciiAlternation &= width <= 1;
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
                    Utf8SliceInstruction.MarkCaptureRun(code, index);
                    limit = next;
                    break;
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

        // Reuse the short anchored proof when both ends bound a small
        // ASCII alternation. Wider consumers stay on the original path.
        if (shortAsciiAlternation && end > 0 && limits[1] is > 0 and <= 32 &&
            code[0] is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning, B: 0 } begin)
            code[0] = new(begin.Kind, begin.A, begin.B, begin.C, 1);

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
        internal Utf8ConstructionBuffer<Func<int, int>>? BackreferenceLowerings;
        internal Utf8ConstructionBuffer<byte[]>? SetMasks;
        internal Utf8ConstructionBuffer<Utf8SliceNode.CategorySetMember[]>? CategorySetMembers;
        internal int RepeatCount;
        internal int CaptureOpeningCount;
        internal readonly Utf8ConstructionMap<Utf8SliceNode.CaptureNode, int>? CaptureOpenings;
        internal readonly Utf8ConstructionBuffer<Utf8NonBacktrackingLoopScope?>? NonBacktrackingScopes;
        internal Utf8NonBacktrackingLoopScope? NonBacktrackingScope;
        internal bool HasNullableNonBacktrackingLoop;
        internal bool HasCounterlessAlternationLoop;
        internal bool HasAlternation;
        internal int GroupCount;
        internal bool SawContiguousAnchor;
        internal bool HasConditionalTest;
        internal bool SawLookaround;
        internal Utf8ConstructionBuffer<Utf8SliceAssertion>? Assertions;
        internal readonly string[]? GroupNames;
        internal Utf8CaptureCatalog? CaptureCatalog;
        internal readonly bool MaintainBalance;
        internal readonly bool MatchCaseInsensitive;
        internal readonly Utf8ScalarCaseBehavior CaseBehavior;
        internal readonly bool MirrorExecute;
        internal readonly bool NonBacktracking;
        private readonly Utf8ConstructionMap<Utf8SliceNode, NodeFacts>? _facts;
        internal Utf8SliceProgram? LastSubprogram;
        internal bool Failed;

        internal Lowering(string[]? groupNames, bool maintainBalance, bool matchCaseInsensitive, bool mirrorExecute, Utf8ConstructionMap<Utf8SliceNode, NodeFacts>? facts, Utf8ScalarCaseBehavior caseBehavior, bool nonBacktracking, Utf8ConstructionBudget budget)
        {
            Budget = budget;
            Instructions = new(budget, true);
            GroupNames = groupNames;
            MaintainBalance = maintainBalance;
            MatchCaseInsensitive = matchCaseInsensitive;
            CaseBehavior = caseBehavior;
            MirrorExecute = mirrorExecute;
            NonBacktracking = nonBacktracking;
            CaptureOpenings = nonBacktracking ? new(budget, ReferenceEqualityComparer.Instance) : null;
            NonBacktrackingScopes = nonBacktracking ? new(budget) : null;
            _facts = facts;
            // RTL references can precede their captures. Every program starts
            // with the final catalog tally when supplied, just as the old entry.
            if (groupNames is not null) GroupCount = groupNames.Length - 1;
        }

        internal void Dispose()
        {
            Instructions.Dispose();
            BackreferenceLowerings?.Dispose();
            SetMasks?.Dispose();
            CategorySetMembers?.Dispose();
            Assertions?.Dispose();
            CaptureOpenings?.Dispose();
            NonBacktrackingScopes?.Dispose();
        }

        internal NodeFacts GetFacts(Utf8SliceNode node) => ReadNodeFacts(node, _facts, NonBacktracking, Budget);

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
            var width = last - first + 1;
            return BitOperations.PopCount(low) + BitOperations.PopCount(high) == width
                ? (width << 8) | first : 0;
        }

        internal void Emit(Utf8SliceInstruction instruction)
        {
            Instructions.Add(instruction);
            NonBacktrackingScopes?.Add(NonBacktrackingScope);
        }

        private void EmitLiteral(Utf8SliceNode.LiteralNode literal)
        {
            // An invalid scalar is an impossible atom. Repetition still
            // controls whether that atom must execute.
            Emit(Rune.TryCreate(literal.Value, out var scalar)
                ? new Utf8SliceInstruction(Utf8SliceInstructionKind.Literal, literal.Value,
                    literal.FoldCase && Utf8SliceCaseEquivalences.HasAlternateScalar(scalar, CaseBehavior) ? 1 : 0, 0, 0)
                : new Utf8SliceInstruction(Utf8SliceInstructionKind.Fail, 0, 0, 0, 0));
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
                    EmitLiteral(literal);
                    break;
                case Utf8SliceNode.SetNode set:
                    if (set.Mask16.Length != 16)
                    {
                        throw new ArgumentException("Slice set masks cover 16 bytes (128 ASCII bits).", nameof(node));
                    }

                    var masks = SetMasks ??= new(Budget);
                    var maskIndex = masks.Count;
                    masks.Add(set.Mask16);
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
                    while (frame.Index < concat.Children.Length)
                    {
                        var child = GetConcatChild(concat.Children, frame.Index++);
                        if (child is Utf8SliceNode.LiteralNode concatenatedLiteral)
                        {
                            // A literal emits one instruction without suspending this frame.
                            Budget.ChargeWork(1);
                            EmitLiteral(concatenatedLiteral);
                            continue;
                        }
                        request = new(child, false, MirrorExecute, 0);
                        return true;
                    }
                    break;
                case Utf8SliceNode.AlternationNode { Branches:
                    [Utf8SliceNode.LiteralNode { FoldCase: false, Value: >= 0 and < 128 } firstByte,
                     Utf8SliceNode.LiteralNode { FoldCase: false, Value: >= 0 and < 128 } secondByte] }:
                    // Both alternatives consume one byte and share a continuation.
                    var choiceMask = Budget.AllocateArray<byte>(16, false);
                    choiceMask[firstByte.Value >> 3] |= (byte)(1 << (firstByte.Value & 7));
                    choiceMask[secondByte.Value >> 3] |= (byte)(1 << (secondByte.Value & 7));
                    var choiceMasks = SetMasks ??= new(Budget);
                    choiceMasks.Add(choiceMask);
                    Emit(new(Utf8SliceInstructionKind.Set, choiceMasks.Count - 1, 0, 0, GetAsciiRange(choiceMask), isLiteralChoice: true));
                    break;
                case Utf8SliceNode.AlternationNode alternate:
                    if (alternate.Branches.Length == 0)
                        throw new ArgumentException("Slice alternations need at least one branch.", nameof(node));
                    if (frame.Stage == 0)
                    {
                        HasAlternation |= alternate.Branches.Length > 1;
                        if (alternate.Branches.Length > 2) frame.Scratch = Utf8ConstructionBuffer<int>.Create(Budget);
                        frame.Stage = 1;
                    }
                    while (frame.Index < alternate.Branches.Length)
                    {
                        Budget.ChargeWork(1);
                        if (frame.Stage == 1)
                        {
                            frame.Mark = Instructions.Count;
                            if (frame.Index + 1 < alternate.Branches.Length)
                                Emit(new(Utf8SliceInstructionKind.Fork, 0, 0, 0, 0));
                            frame.Stage = 2;
                            request = new(alternate.Branches[frame.Index], false, MirrorExecute, 0);
                            return true;
                        }
                        if (frame.Index + 1 < alternate.Branches.Length)
                        {
                            if (frame.Scratch is { } jumps) jumps.Add(Instructions.Count);
                            else frame.Jump = Instructions.Count;
                            Emit(new(Utf8SliceInstructionKind.Jump, 0, 0, 0, 0));
                            Patch(frame.Mark, Instructions.Count);
                        }
                        frame.Index++;
                        frame.Stage = 1;
                    }
                    if (frame.Scratch is { } pending)
                    {
                        foreach (var jump in pending) { Budget.ChargeWork(1); Patch(jump, Instructions.Count); }
                    }
                    else if (alternate.Branches.Length == 2)
                    {
                        Budget.ChargeWork(1);
                        Patch(frame.Jump, Instructions.Count);
                    }
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
                            if (GetFacts(repeat.Body).IsZeroWidth)
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
                                    GetFacts(repeat.Body) is { CanMatchEmpty: true, IsZeroWidth: false })
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
                            frame.NullableLoop = GetFacts(repeat.Body) is { CanMatchEmpty: true, IsZeroWidth: false };
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
                            !GetFacts(candidateBody).CanMatchEmpty && IsSmallPureBody(candidateBody, ref remaining, fixedOuter: true))
                            frame.Stage = 2;
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
                            candidateBody is Utf8SliceNode.AlternationNode && GetFacts(candidateBody).Minimum > 0 &&
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
                        var delimiter = GetDelimitedAsciiRunByte(repeat.Body);
                        HasCounterlessAlternationLoop |= delimiter < 0;
                        Emit(new(Utf8SliceInstructionKind.Jump, frame.Mark, delimiter + 1, 0, 0));
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
                    var orderedBodyConsumes = repeat is { Maximum: long.MaxValue, Minimum: <= 1 } &&
                        Instructions.Count < 64 && Instructions.Count - frame.Mark > 2 &&
                        GetFacts(repeat.Body).Minimum > 0;
                    Emit(new(Utf8SliceInstructionKind.RepeatEnd, frame.Mark, stopBytes, orderedBodyConsumes ? 1 : 0, 0));
                    var repeatExit = Instructions.Count;
                    Emit(new(Utf8SliceInstructionKind.RepeatExit, frame.RepeatId, 0, 0, 0));
                    var repeatKind = repeat.Lazy ? Utf8SliceInstructionKind.RepeatLazy : Utf8SliceInstructionKind.RepeatGreedy;
                    if (repeat.GuardEmptyAfterMinimum && repeat.Minimum > 0 && repeat.Minimum < repeat.Maximum &&
                        GetFacts(repeat.Body).CanMatchEmpty)
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
                        members[0] = new Utf8SliceNode.CategorySetMember(
                            Utf8SliceCategoryTable.ForScalarPredicate(wordPredicate), false);
                        var wordMembers = CategorySetMembers ??= new(Budget);
                        wordTable = wordMembers.Count + 1;
                        wordMembers.Add(members);
                    }
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Boundary, boundary.IsWordBoundary ? 1 : 0,
                        (int)boundary.WordKind, wordTable, 0));
                    break;
                case Utf8SliceNode.ShorthandNode shorthand:
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Shorthand, (int)shorthand.Shorthand, 0, 0, 0));
                    break;
                case Utf8SliceNode.CategorySetNode categorySet:
                    var categoryMasks = SetMasks ??= new(Budget);
                    var categorySetMaskIndex = categoryMasks.Count;
                    var categoryMask = categorySet.Mask16;
                    var foldCategorySet = categorySet.FoldCase;
                    var isDecimalDigitSet = false;
                    var pureCategory = categorySet.Categories is [{ PlainCategoryMask: var mask }] ? mask : 0;
                    if (!categorySet.Negated && pureCategory == 1u << (int)UnicodeCategory.DecimalDigitNumber)
                    {
                        var low = BinaryPrimitives.ReadUInt64LittleEndian(categoryMask);
                        var high = BinaryPrimitives.ReadUInt64LittleEndian(categoryMask.AsSpan(8));
                        // Decimal digits have no case equivalents. An explicit
                        // member outside the digits retains its original folding.
                        if ((low & ~Utf8AsciiBytePredicates.DecimalMask) == 0 && high == 0)
                        {
                            foldCategorySet = false;
                            isDecimalDigitSet = categorySet.Members.Length == 0;
                        }
                        var complete = low | Utf8AsciiBytePredicates.DecimalMask;
                        if (!foldCategorySet && complete != low)
                        {
                            Budget.ChargeWork(2);
                            categoryMask = Budget.AllocateArray<byte>(16, false);
                            BinaryPrimitives.WriteUInt64LittleEndian(categoryMask, complete);
                            BinaryPrimitives.WriteUInt64LittleEndian(categoryMask.AsSpan(8), high);
                        }
                    }
                    else if (!categorySet.Negated && !foldCategorySet && pureCategory != 0)
                    {
                        // Complete the existing positive ASCII mask once. Runtime
                        // scalar tables still govern wider characters and misses.
                        // Keep the immutable source mask available to other lowerings.
                        Budget.ChargeWork(128);
                        Span<byte> completed = stackalloc byte[16];
                        categoryMask.CopyTo(completed);
                        for (var value = 0; value < 128; value++)
                            if ((pureCategory & (1u << (int)Rune.GetUnicodeCategory(new Rune(value)))) != 0)
                                completed[value >> 3] |= (byte)(1 << (value & 7));
                        if (!completed.SequenceEqual(categoryMask))
                        {
                            categoryMask = Budget.AllocateArray<byte>(16, false);
                            completed.CopyTo(categoryMask);
                        }
                    }
                    categoryMasks.Add(categoryMask);
                    var categorySetMemberBits = 0;
                    foreach (var shorthandMember in categorySet.Members)
                    {
                        categorySetMemberBits |= 1 << (int)shorthandMember;
                    }

                    var categoryMembers = CategorySetMembers ??= new(Budget);
                    var categorySetMemberIndex = categoryMembers.Count;
                    categoryMembers.Add(categorySet.Categories);
                    Emit(new Utf8SliceInstruction(
                        Utf8SliceInstructionKind.CategorySet,
                        categorySetMaskIndex,
                        (categorySet.Negated ? 1 : 0) | (foldCategorySet ? 2 : 0),
                        categorySetMemberBits,
                        categorySetMemberIndex,
                        isDecimalDigitSet: isDecimalDigitSet));
                    break;
                case Utf8SliceNode.BackrefNode backref:
                    if (backref.Slot < 0 || backref.Slot >= GroupCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(node), "Slice backreferences require a defined group.");
                    }

                    var loweringIndex = 0;
                    if (backref.LowerScalar is { } lowerScalar)
                    {
                        var lowerings = BackreferenceLowerings ??= new(Budget);
                        var index = lowerings.IndexOf(lowerScalar);
                        if (index < 0) { index = lowerings.Count; lowerings.Add(lowerScalar); }
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
                    if (frame.Stage == 0)
                    {
                        frame.Stage = 1;
                        request = new(behind.Body, true, !behind.ForwardScalarCount.HasValue, 0); return true;
                    }
                    var body = LastSubprogram;
                    if (body is null) { Failed = true; return false; }
                    GroupCount = Math.Max(GroupCount, body.GroupCount);
                    var width = GetFacts(behind.Body);
                    var lookbehinds = Assertions ??= new(Budget);
                    var assertionIndex = lookbehinds.Count;
                    lookbehinds.Add(Utf8SliceAssertion.Create(body, width.Minimum, width.Maximum, true, behind.ForwardScalarCount));
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

        private Utf8SliceNode GetConcatChild(Utf8SliceNode[] children, int index)
        {
            var child = children[index];
            if (NonBacktracking || MirrorExecute || index + 1 == children.Length ||
                child is not Utf8SliceNode.RepeatNode { Minimum: 1, Maximum: long.MaxValue, Lazy: false,
                    Body: Utf8SliceNode.SetNode { Negated: true, FoldCase: false } first } ||
                children[index + 1] is not Utf8SliceNode.RepeatNode { Minimum: 1, Maximum: long.MaxValue, Lazy: false,
                    Body: Utf8SliceNode.SetNode { Negated: true, FoldCase: false } second })
                return child;

            Budget.ChargeWork((ulong)first.Members.Length + (ulong)second.Members.Length + 16);
            if (!first.Members.AsSpan().SequenceEqual(second.Members)) return child;
            for (var i = 0; i < first.Mask16.Length; i++)
                if ((second.Mask16[i] & ~first.Mask16[i]) != 0) return child;

            // Both atoms are effect-free, and every first-class member also
            // belongs to the second. A+ B+ tries the same candidate ends in
            // descending order as A B+; additional splits only repeat ends.
            return first;
        }

        private bool IsSmallPureBody(Utf8SliceNode node, ref int remaining, bool fixedOuter = false)
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
                        if (!IsSmallPureBody(child, ref remaining, fixedOuter)) return false;
                    return true;
                case Utf8SliceNode.AlternationNode alternate:
                    foreach (var branch in alternate.Branches)
                    {
                        // Each preceding branch adds a fork and jump.
                        remaining -= 2;
                        if (remaining < 0 || !IsSmallPureBody(branch, ref remaining, fixedOuter)) return false;
                    }
                    return true;
                case Utf8SliceNode.RepeatNode { Maximum: <= 8, Body:
                    Utf8SliceNode.LiteralNode or Utf8SliceNode.SetNode or Utf8SliceNode.ShorthandNode or
                    Utf8SliceNode.CategorySetNode or Utf8SliceNode.DotNode } repeat
                    when repeat.Minimum == repeat.Maximum || repeat is { Minimum: 0, Maximum: 1 } ||
                        fixedOuter && repeat is { Minimum: > 0, Maximum: <= 4 }:
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
                var width = GetFacts(body);
                var assertions = Assertions ??= new(Budget);
                var index = assertions.Count;
                assertions.Add(Utf8SliceAssertion.Create(program, width.Minimum, width.Maximum, false));
                Emit(new(Utf8SliceInstructionKind.Lookahead, index, positive ? 1 : 0, 0, 0));
                return false;
            }
            var first = frame.Mark + 1;
            var inlinePrefixLength = Utf8SliceAssertion.FindInlineLiteralPrefixLength(Instructions.Span,
                first, Instructions.Count, Budget);
            var inlineBody = inlinePrefixLength >= 0 ? inlinePrefixLength + 1 : 0;
            if (Instructions.Count - first == 1 && Instructions[first].IsInlineAssertionClass)
                inlineBody = Utf8SliceInstruction.InlineClassAssertion;
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
            if (inlineBody != 0)
            {
                var target = Instructions[frame.Mark].A;
                Instructions[frame.Mark] = new(Utf8SliceInstructionKind.Fork, target, 1,
                    inlineBody, positive ? 1 : 0);
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
