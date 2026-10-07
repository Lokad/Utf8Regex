using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Immutable tree node kinds for the owned R02a slice: concatenation,
/// ordered alternation, greedy/lazy repetition, captures, scalar literals, ASCII
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

    internal sealed record LiteralNode(int Value, bool FoldCase) : Utf8SliceNode(Utf8SliceNodeKind.Literal);

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

    internal sealed record BoundaryNode(bool IsWordBoundary, bool Ecma) : Utf8SliceNode(Utf8SliceNodeKind.Boundary)
    {
        internal BoundaryNode(bool isWordBoundary) : this(isWordBoundary, false) { }
    }

    internal sealed record ShorthandNode(Utf8SliceShorthand Shorthand) : Utf8SliceNode(Utf8SliceNodeKind.Shorthand);

    internal sealed record BackrefNode(int Slot, bool FoldCase, bool AllowUnmatched) : Utf8SliceNode(Utf8SliceNodeKind.Backref)
    {
        internal BackrefNode(int slot, bool foldCase) : this(slot, foldCase, false) { }

        // An owned profile may supply a bounded simple-scalar lowercase table.
        internal Func<int, int>? LowerScalar { get; init; }
    }
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
    BalanceStart = 27,
}

/// <summary>Unicode property table for one owned category atom: a
/// UnicodeCategory bit mask for general categories/groups plus half-open
/// scalar-valued half-open range endpoint pairs for named blocks/literal ranges,
/// or one immutable class subtraction program.</summary>
internal sealed class Utf8SliceCategoryTable
{
    internal Utf8SliceCategoryTable(uint mask, ushort[] ranges)
    {
        using var construction = Utf8ConstructionScope.Enter();
        Mask = mask;
        Ranges = construction.Budget.AllocateArray<int>(ranges.Length + (ranges.Length & 1), false);
        for (var index = 0; index < ranges.Length; index++) Ranges[index] = ranges[index];
        if ((ranges.Length & 1) != 0) Ranges[^1] = 0x10000;
        construction.Complete();
    }

    private Utf8SliceCategoryTable(int[] ranges)
    {
        Mask = 0;
        Ranges = ranges;
    }

    internal static Utf8SliceCategoryTable ForScalarRanges(int[] ranges) => new(ranges);

    internal Utf8SliceCategoryTable(Utf8SliceClassProgram classProgram)
    {
        Mask = 0;
        Ranges = [];
        ClassProgram = classProgram;
    }

    internal uint Mask { get; }

    internal int[] Ranges { get; }

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
    internal Utf8SliceProgram(Utf8SliceInstruction[] instructions, byte[][] setMasks, Utf8SliceCategoryTable[] categoryTables, Utf8SliceNode.CategorySetMember[][] categorySetMembers, int repeatCount, int groupCount, bool requiresUnicodeWordBoundary, bool requiresUnicodeShorthand, bool requiresUnicodeCategory, Utf8SliceAssertion[] assertions, bool hasNonFixedLookbehind, string[] groupNames, bool hasMixedNamedGroups, bool hasBalancing, bool ignoreCase, bool hasContiguousAnchor, bool hasConditionalTest, bool rightToLeft, bool sawLookaround, bool sawCapturingLookbehind, bool canMatchEmpty, Utf8CaptureCatalog? captureCatalog, bool usesInterpretedLazyLoopSemantics, bool usesLiteralClassBracketSyntax, Utf8ScalarCaseBehavior caseBehavior)
    {
        using var construction = Utf8ConstructionScope.Enter();
        Instructions = instructions;
        SetMasks = setMasks;
        CategoryTables = categoryTables;
        CategorySetMembers = categorySetMembers;
        RepeatCount = repeatCount;
        GroupCount = groupCount;
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
        CaseBehavior = caseBehavior;
        HasContiguousAnchor = hasContiguousAnchor;
        HasConditionalTest = hasConditionalTest;
        RightToLeft = rightToLeft;
        SawLookaround = sawLookaround;
        SawCapturingLookbehind = sawCapturingLookbehind;
        CanMatchEmpty = canMatchEmpty;
        UsesInterpretedLazyLoopSemantics = usesInterpretedLazyLoopSemantics;
        UsesLiteralClassBracketSyntax = usesLiteralClassBracketSyntax;
        UsesCaptureOpeningJournal = HasSharedCaptureDeclarations;
        foreach (var instruction in instructions)
        {
            UsesCaptureOpeningJournal |= instruction.Kind == Utf8SliceInstructionKind.Backref;
            HasUnicodePatternAtoms |= instruction.Kind == Utf8SliceInstructionKind.CategorySet ||
                instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.A >= 128;
        }
        foreach (var assertion in assertions)
        {
            UsesCaptureOpeningJournal |= assertion.Body.UsesCaptureOpeningJournal;
            HasUnicodePatternAtoms |= assertion.Body.HasUnicodePatternAtoms;
            HasOrderSensitiveLookbehind |= assertion.Body.HasOrderSensitiveLookbehind;
            UsesLiteralClassBracketSyntax |= assertion.Body.UsesLiteralClassBracketSyntax;
            if (assertion.IsLookbehind)
                foreach (var instruction in assertion.Body.Instructions)
                    HasOrderSensitiveLookbehind |= instruction.Kind is Utf8SliceInstructionKind.Backref
                        or Utf8SliceInstructionKind.TestGroup or Utf8SliceInstructionKind.Lookahead
                        or Utf8SliceInstructionKind.Lookbehind or Utf8SliceInstructionKind.AtomicCommit;
        }
        GroupNumbers = captureCatalog?.GroupNumbers ?? construction.Budget.AllocateArray<int>(groupCount + 1, false);
        if (captureCatalog is null)
            for (var group = 0; group <= groupCount; group++) GroupNumbers[group] = group;
        construction.Complete();
    }

    internal Utf8SliceInstruction[] Instructions { get; }
    internal bool IsNonBacktracking { get; init; }
    internal int NonBacktrackingCaptureOpeningCount { get; init; }
    internal int[] NonBacktrackingEpsilonOrder { get; init; } = [];
    internal Func<int, int>[] BackreferenceLowerings { get; init; } = [];

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
    internal Utf8ScalarCaseBehavior CaseBehavior { get; }
    internal bool HasContiguousAnchor { get; }
    internal bool HasConditionalTest { get; }
    internal bool SawLookaround { get; }
    internal bool SawCapturingLookbehind { get; }
    internal bool RightToLeft { get; }
    internal bool CanMatchEmpty { get; }
    internal bool HasUnicodePatternAtoms { get; }

    // The compiled .NET 10 lazy-loop defect is excluded from our semantics.
    // Route these programs through the owned VM even when a native plan exists.
    internal bool UsesInterpretedLazyLoopSemantics { get; }

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
        using var frames = Utf8ConstructionFrames<AnalysisFrame>.Create(budget, new(root, new(root, budget)));
        var facts = Utf8ConstructionMap<Utf8SliceNode, NodeFacts>.Create(budget, ReferenceEqualityComparer.Instance);
        try
        {
            while (frames.Count > 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                bool hasChild;
                try { hasChild = frame.Children.MoveNext(); }
                finally { frames[index] = frame; }
                if (hasChild)
                {
                    var child = frame.Children.Current;
                    if (!facts.ContainsKey(child)) frames.Push(new(child, new(child, budget)));
                    continue;
                }
                frames.Pop();
                var node = frame.Node;
                var writes = node is Utf8SliceNode.CaptureNode or Utf8SliceNode.BalanceNode;
                var children = new ChildCursor(node, budget);
                while (children.MoveNext()) writes |= facts[children.Current].WritesGroup;
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
                facts[node] = new NodeFacts(empty, writes, zeroWidth, minimum, maximum);
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
                case Utf8SliceNode.ShorthandNode or Utf8SliceNode.CategoryNode or Utf8SliceNode.CategorySetNode: return (1, 4);
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
                        repeat.Maximum == int.MaxValue || body.Maximum == int.MaxValue ? int.MaxValue : Saturate((long)body.Maximum * repeat.Maximum));
                default: return (0, int.MaxValue);
            }
        }

        static int Saturate(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
    }

    private struct AnalysisFrame(Utf8SliceNode node, ChildCursor children)
    {
        internal Utf8SliceNode Node = node;
        internal ChildCursor Children = children;
    }

    // Borrowed IR traversal has no enumerable/boxed state. Empty alternatives
    // spend shared work even when they contain no child to push.
    private struct ChildCursor(Utf8SliceNode node, Utf8ConstructionBudget budget)
    {
        private int _index, _branch;
        internal Utf8SliceNode Current = node;
        internal bool MoveNext()
        {
            budget.ChargeWork(1);
            switch (node)
            {
                case Utf8SliceNode.ConcatNode concat:
                    if (_index == concat.Children.Length) return false;
                    Current = concat.Children[_index++]; return true;
                case Utf8SliceNode.AlternationNode alternate:
                    while (_branch < alternate.Branches.Length)
                    {
                        budget.ChargeWork(1);
                        if (_index < alternate.Branches[_branch].Length)
                        {
                            Current = alternate.Branches[_branch][_index++]; return true;
                        }
                        _branch++; _index = 0;
                    }
                    return false;
                case Utf8SliceNode.ConditionalNode conditional:
                    var index = _index++;
                    if (index == 0) { Current = conditional.YesBody; return true; }
                    if (index == 1) { Current = conditional.NoBody; return true; }
                    if (index == 2 && conditional.TestBody is { } test) { Current = test; return true; }
                    return false;
                default:
                    if (_index++ != 0) return false;
                    var body = node switch
                    {
                        Utf8SliceNode.CaptureNode capture => capture.Body,
                        Utf8SliceNode.BalanceNode balance => balance.Body,
                        Utf8SliceNode.AtomicNode atomic => atomic.Body,
                        Utf8SliceNode.RepeatNode repeat => repeat.Body,
                        Utf8SliceNode.LookaheadNode ahead => ahead.Body,
                        Utf8SliceNode.LookbehindNode behind => behind.Body,
                        _ => null,
                    };
                    if (body is null) return false;
                    Current = body; return true;
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
        internal int Stage, Index, Branch, Mark, Jump, RepeatId, Minimum, Maximum, Opening;
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
            category |= instruction.Kind is Utf8SliceInstructionKind.Category or Utf8SliceInstructionKind.CategorySet;
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
        // context) carry no names; ordinals are BCL-exact for them since
        // only the parser admits named shapes, always with real names.
        var resolvedNames = lowerer.GroupNames;
        if (resolvedNames is null)
        {
            resolvedNames = lowerer.Budget.AllocateArray<string>(lowerer.GroupCount + 1, false);
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

        var instructions = lowerer.Instructions.Take();
        if (lowerer.HasNullableNonBacktrackingLoop && lowerer.NonBacktrackingScopes is { } scopes)
        {
            var lifted = Utf8NonBacktrackingLoopGraph.Lift(instructions, scopes, lowerer.Budget);
            lowerer.Budget.ReleaseArray(instructions, true);
            instructions = lifted;
        }
        return new Utf8SliceProgram(
            instructions,
            lowerer.SetMasks.Take(),
            lowerer.CategoryTables.Take(),
            lowerer.CategorySetMembers.Take(),
            lowerer.RepeatCount,
            lowerer.GroupCount,
            requiresUnicodeWordBoundary,
            requiresUnicodeShorthand,
            requiresUnicodeCategory,
            assertions,
            hasNonFixedLookbehind, resolvedNames, hasUnnamedGroups && hasNamedGroups, lowerer.MaintainBalance, lowerer.MatchCaseInsensitive, lowerer.SawContiguousAnchor, lowerer.HasConditionalTest, lowerer.MirrorExecute, lowerer.SawLookaround, lowerer.SawCapturingLookbehind, lowerer.Facts[root].CanMatchEmpty, lowerer.CaptureCatalog, usesInterpretedLazyLoopSemantics, usesLiteralClassBracketSyntax, lowerer.CaseBehavior)
        {
            BackreferenceLowerings = lowerer.BackreferenceLowerings.Take(),
            IsNonBacktracking = lowerer.NonBacktracking,
            NonBacktrackingCaptureOpeningCount = lowerer.CaptureOpeningCount,
            NonBacktrackingEpsilonOrder = lowerer.NonBacktracking
                ? Utf8NonBacktrackingSearchState.BuildEpsilonOrder(instructions) : [],
        };
    }

    private sealed class Lowering
    {
        internal readonly Utf8ConstructionBuffer<Utf8SliceInstruction> Instructions;
        internal readonly Utf8ConstructionBudget Budget;
        internal readonly Utf8ConstructionBuffer<Func<int, int>> BackreferenceLowerings;
        internal readonly Utf8ConstructionBuffer<byte[]> SetMasks;
        internal readonly Utf8ConstructionBuffer<Utf8SliceNode.CategorySetMember[]> CategorySetMembers;
        internal readonly Utf8ConstructionBuffer<Utf8SliceCategoryTable> CategoryTables;
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
            CategoryTables = new(budget);
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
            CategoryTables.Dispose();
            Assertions.Dispose();
            CaptureOpenings?.Dispose();
            NonBacktrackingScopes?.Dispose();
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
                    Emit(System.Text.Rune.IsValid(literal.Value)
                        ? new Utf8SliceInstruction(Utf8SliceInstructionKind.Literal, literal.Value, literal.FoldCase ? 1 : 0, 0, 0)
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
                    if (repeat.Minimum < 0 || repeat.Maximum < repeat.Minimum)
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
                                var copies = frame.Maximum == int.MaxValue ? frame.Minimum : frame.Maximum;
                                Budget.CheckReplacementMinimum((ulong)Instructions.Capacity, (ulong)Instructions.Count + (ulong)copies);
                            }
                            frame.Stage = 1;
                        }
                        if (Failed) return false;
                        if (frame.Stage == 1)
                        {
                            if (frame.Index < frame.Minimum)
                            {
                                frame.Index++;
                                request = new(repeat.Body, false, false, 0);
                                return true;
                            }
                            frame.Stage = 2;
                        }
                        if (frame.Stage == 3)
                        {
                            if (frame.Maximum == int.MaxValue)
                            {
                                if (frame.NullableLoop && NonBacktrackingScope is { } scope) scope.End = Instructions.Count;
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
                            frame.NullableLoop = frame.Maximum == int.MaxValue && Facts[repeat.Body].CanMatchEmpty;
                            if (frame.NullableLoop)
                            {
                                NonBacktrackingScope = new(frame.ParentScope, frame.Mark);
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
                    if (frame.Stage == 0)
                    {
                        frame.RepeatId = RepeatCount++;
                        frame.Mark = Instructions.Count;
                        Emit(new(Utf8SliceInstructionKind.RepeatGreedy, 0, 0, 0, 0));
                        frame.Stage = 1;
                        request = new(repeat.Body, false, MirrorExecute, 0);
                        return true;
                    }
                    Emit(new(Utf8SliceInstructionKind.RepeatEnd, frame.Mark, 0, 0, 0));
                    var repeatExit = Instructions.Count;
                    Emit(new(Utf8SliceInstructionKind.RepeatExit, frame.RepeatId, 0, 0, 0));
                    Instructions[frame.Mark] = new(repeat.Lazy ? Utf8SliceInstructionKind.RepeatLazy : Utf8SliceInstructionKind.RepeatGreedy,
                        repeatExit, frame.RepeatId, repeat.Minimum, repeat.Maximum);
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
                    Emit(new Utf8SliceInstruction(Utf8SliceInstructionKind.Boundary, boundary.IsWordBoundary ? 1 : 0, boundary.Ecma ? 1 : 0, 0, 0));
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
                        request = new(behind.Body, true, true, 0); return true;
                    }
                    var body = LastSubprogram;
                    if (body is null) { Failed = true; return false; }
                    GroupCount = Math.Max(GroupCount, body.GroupCount);
                    var width = Facts[behind.Body];
                    var assertionIndex = Assertions.Count;
                    Assertions.Add(new(body, width.Minimum, width.Maximum, true));
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
                    throw new ArgumentException($"Unsupported slice node {node.Kind}.", nameof(node));
            }
            return false;
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
                Assertions.Add(new(program, width.Minimum, width.Maximum, false));
                Emit(new(Utf8SliceInstructionKind.Lookahead, index, positive ? 1 : 0, 0, 0));
                return false;
            }
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
            return false;
        }

        private void Patch(int index, int target)
        {
            var existing = Instructions[index];
            Instructions[index] = new Utf8SliceInstruction(existing.Kind, target, existing.B, existing.C, existing.D);
        }
    }
}
