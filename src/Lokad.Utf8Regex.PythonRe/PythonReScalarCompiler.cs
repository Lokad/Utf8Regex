using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

// The Python parser resolves its own lexical scalar semantics. Lower that tree
// to the shared VM rather than rediscovering folding from a .NET option flag.
internal static partial class PythonReScalarCompiler
{
    // Lowering ends the top-level instruction array with one Accept; all jumps
    // use absolute indices. Replacing that terminal instruction leaves every
    // existing target intact, including branches that must backtrack at EOF.
    // Assertion bodies and immutable predicate/capture tables stay shared.
    internal static Utf8SliceProgram RequireFullMatch(Utf8SliceProgram program)
    {
        if (program.IsNonBacktracking || program.RightToLeft ||
            program.Instructions.Length == 0 ||
            program.Instructions[^1].Kind != Utf8SliceInstructionKind.Accept)
            throw new InvalidOperationException("A forward Python scalar program must end with Accept.");

        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        budget.ChargeWork(1);
        var instructions = budget.AllocateArray<Utf8SliceInstruction>(checked(program.Instructions.Length + 1), true);
        for (var offset = 0; offset < program.Instructions.Length;)
        {
            var count = Math.Min(256, program.Instructions.Length - offset);
            budget.ChargeWork((ulong)count);
            program.Instructions.AsSpan(offset, count).CopyTo(instructions.AsSpan(offset, count));
            offset += count;
        }
        instructions[^2] = new(Utf8SliceInstructionKind.Anchor, (int)Utf8SliceAnchorKind.VeryEnd, 0, 0, 0);
        instructions[^1] = new(Utf8SliceInstructionKind.Accept, 0, 0, 0, 0);
        var result = Utf8SliceProgram.Create(instructions, program.SetMasks, program.CategoryTables,
            program.CategorySetMembers, program.RepeatCount, program.GroupCount,
            program.RequiresUnicodeWordBoundary, program.RequiresUnicodeShorthand, program.RequiresUnicodeCategory,
            program.Assertions, program.HasNonFixedLookbehind, program.GroupNames, program.HasMixedNamedGroups,
            program.HasBalancing, program.IgnoreCase, program.HasContiguousAnchor, program.HasConditionalTest,
            program.RightToLeft, program.SawLookaround, program.SawCapturingLookbehind, program.CanMatchEmpty,
            program.CaptureCatalog, program.UsesInterpretedLazyLoopSemantics, program.UsesLiteralClassBracketSyntax,
            program.CaseBehavior, false, 0, [], program.BackreferenceLowerings);
        construction.Complete();
        return result;
    }

    private struct ConversionFrame(PythonReNode node, PythonReCompileOptions options)
    {
        internal readonly PythonReNode Node = node;
        internal readonly PythonReCompileOptions Options = options;
        internal int Stage;
        internal int Index;
        internal Utf8SliceNode[]? Children;
        internal Utf8SliceNode[][]? Branches;
        internal Utf8SliceNode? Yes;
    }

    internal static Utf8SliceProgram Compile(PythonReParseResult parse,
        PythonReTranslation translation)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<ConversionFrame>.Create(construction.Budget, new(parse.Root, parse.Options));
        using var arena = Utf8ConstructionArena.Create(construction.Budget);
        using var nodes = Utf8ConstructionMap<PythonReNode, Utf8SliceNode>.Create(construction.Budget, ReferenceEqualityComparer.Instance);
        while (frames.Count != 0)
        {
            construction.Budget.ChargeWork(1);
            var frameIndex = frames.Count - 1;
            var frame = frames[frameIndex];
            var node = frame.Node;
            var completed = true;
            ConversionFrame child = default;
            try
            {
                switch (node)
                {
                    case PythonReSequenceNode sequence:
                    {
                        if (frame.Stage == 0) frame.Children = arena.Allocate<Utf8SliceNode>(sequence.Elements.Count, false);
                        var children = frame.Children ?? throw new InvalidOperationException("Conversion children are missing.");
                        if (frame.Stage == 2)
                        {
                            children[frame.Index] = nodes[sequence.Elements[frame.Index]];
                            frame.Index++;
                        }
                        if (frame.Index == children.Length) nodes[node] = PythonReScalarNodeFactory.Concat(children);
                        else
                        {
                            frame.Stage = 2;
                            child = new(sequence.Elements[frame.Index], frame.Options);
                            completed = false;
                        }
                        break;
                    }
                    case PythonReAlternationNode alternation:
                    {
                        if (frame.Stage == 0) frame.Branches = arena.Allocate<Utf8SliceNode[]>(alternation.Branches.Count, false);
                        var branches = frame.Branches ?? throw new InvalidOperationException("Conversion branches are missing.");
                        if (frame.Stage == 2)
                        {
                            var converted = nodes[alternation.Branches[frame.Index]];
                            branches[frame.Index] = arena.Allocate<Utf8SliceNode>(1, false);
                            branches[frame.Index][0] = converted;
                            frame.Index++;
                        }
                        if (frame.Index == branches.Length) nodes[node] = PythonReScalarNodeFactory.Alternation(branches);
                        else
                        {
                            frame.Stage = 2;
                            child = new(alternation.Branches[frame.Index], frame.Options);
                            completed = false;
                        }
                        break;
                    }
                    case PythonReQuantifierNode repeat:
                        if (frame.Stage++ == 0)
                        {
                            child = new(repeat.Inner, frame.Options);
                            completed = false;
                        }
                        else
                        {
                            var result = PythonReScalarNodeFactory.Repeat(nodes[repeat.Inner], repeat.Min,
                                repeat.Max ?? int.MaxValue, repeat.Flavor == PythonReQuantifierFlavor.Reluctant);
                            nodes[node] = repeat.Flavor == PythonReQuantifierFlavor.Possessive
                                ? PythonReScalarNodeFactory.Atomic(result) : result;
                        }
                        break;
                    case PythonReGroupNode group:
                        if (frame.Stage++ == 0)
                        {
                            var scope = frame.Options;
                            if (group.Kind == PythonReGroupKind.ScopedFlags)
                            {
                                if ((group.AddOptions & PythonReCompileOptions.Ascii) != 0) scope &= ~PythonReCompileOptions.Unicode;
                                if ((group.AddOptions & PythonReCompileOptions.Unicode) != 0) scope &= ~PythonReCompileOptions.Ascii;
                                scope = (scope | group.AddOptions) & ~group.RemoveOptions;
                            }
                            child = new(group.Inner, scope);
                            completed = false;
                        }
                        else
                        {
                            var body = nodes[group.Inner];
                            nodes[node] = group.Kind switch
                            {
                                PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing =>
                                    PythonReScalarNodeFactory.Capture(translation.CoreNumbersByPython[group.GroupNumber] - 1, body),
                                PythonReGroupKind.PositiveLookbehind => PythonReScalarNodeFactory.Lookbehind(body, true, group.FixedLookbehindWidth ?? throw new InvalidOperationException("Validated lookbehind width is missing.")),
                                PythonReGroupKind.NegativeLookbehind => PythonReScalarNodeFactory.Lookbehind(body, false, group.FixedLookbehindWidth ?? throw new InvalidOperationException("Validated lookbehind width is missing.")),
                                PythonReGroupKind.PositiveLookahead => PythonReScalarNodeFactory.Lookahead(body, true),
                                PythonReGroupKind.NegativeLookahead => PythonReScalarNodeFactory.Lookahead(body, false),
                                PythonReGroupKind.Atomic => PythonReScalarNodeFactory.Atomic(body),
                                _ => body,
                            };
                        }
                        break;
                    case PythonReNumericBackreferenceNode reference:
                        nodes[node] = PythonReScalarNodeFactory.Backref(translation.CoreNumbersByPython[reference.GroupNumber] - 1, reference.LowerScalar);
                        break;
                    case PythonReNamedBackreferenceNode reference:
                        nodes[node] = PythonReScalarNodeFactory.Backref(translation.CoreNumbersByPython[parse.NamedGroups[reference.Name]] - 1, reference.LowerScalar);
                        break;
                    case PythonReConditionalNode conditional:
                        if (frame.Stage == 0)
                        {
                            frame.Stage = 1;
                            child = new(conditional.YesBranch, frame.Options);
                            completed = false;
                            break;
                        }
                        if (frame.Stage == 1)
                        {
                            frame.Yes = nodes[conditional.YesBranch];
                            if (conditional.NoBranch is { } noBranch)
                            {
                                frame.Stage = 2;
                                child = new(noBranch, frame.Options);
                                completed = false;
                                break;
                            }
                        }
                        var no = conditional.NoBranch is { } branch ? nodes[branch] : PythonReScalarNodeFactory.Empty();
                        var number = conditional.Reference.Kind == PythonReConditionalReferenceKind.GroupNumber
                            ? conditional.Reference.GroupNumber
                            : parse.NamedGroups[conditional.Reference.GroupName ?? throw new InvalidOperationException("Conditional name missing.")];
                        nodes[node] = PythonReScalarNodeFactory.Conditional(translation.CoreNumbersByPython[number] - 1,
                            frame.Yes ?? throw new InvalidOperationException("Conditional yes branch is missing."), no);
                        break;
                    default:
                        var atomOptions = RegexOptions.CultureInvariant
                            | ((frame.Options & PythonReCompileOptions.Multiline) != 0 ? RegexOptions.Multiline : RegexOptions.None);
                        nodes[node] = node is PythonReRawNode { Kind: PythonReRawKind.Escape } raw
                            && ConvertPredefined(raw.Text, arena) is { } predefined ? predefined
                            : node is PythonReCharacterClassNode characterClass
                                && ConvertBracket(characterClass, atomOptions, arena) is { } bracket ? bracket
                                : ParseLeaf(node, atomOptions, arena);
                        break;
                }
            }
            finally { frames[frameIndex] = frame; }
            if (completed) frames.Pop();
            else frames.Push(child);
        }
        // Release conversion-array scratch before acquiring the lowered program.
        // The shared value-frame owner is idempotent on the later using exit.
        frames.Dispose();
        var root = nodes[parse.Root];
        var names = arena.Allocate<string>(checked(parse.CaptureGroupCount + 1), true);
        names[0] = "0"; // Borrowed immutable whole-match slot spelling.
        if (names.Length > 1)
        {
            using var nameText = Utf8ConstructionText.Create();
            for (var group = 1; group < names.Length; group++)
            {
                nameText.Clear();
                names[group] = nameText.Append(group).ToText();
            }
        }
        foreach (var named in parse.NamedGroups)
            names[translation.CoreNumbersByPython[named.Value]] = translation.EmittedGroupNames[named.Key];
        var program = Utf8SliceLowerer.Lower(root, names, false, false, false)
            ?? throw new InvalidOperationException("The validated Python tree has no owned scalar program.");
        arena.Complete();
        construction.Complete();
        return program;
    }

    private static Utf8SliceNode ParseLeaf(PythonReNode node, RegexOptions options, Utf8ConstructionArena arena)
    {
        var fragment = PythonReTranslator.RenderNativeAtom(node);
        var tree = Utf8OwnedTreeParser.TryParse(fragment, options);
        if (tree?.CaptureCatalog is not { } catalog)
            throw new InvalidOperationException("The validated Python atom has no owned scalar tree: " + fragment);
        var balancing = false;
        var lazyCaptures = false;
        return Utf8OwnedTreeSliceLowerer.TryConvertRoot(tree.Root, catalog, arena, ref balancing, ref lazyCaptures)
            ?? throw new InvalidOperationException("The validated Python atom has no owned scalar lowering: " + fragment);
    }
}
