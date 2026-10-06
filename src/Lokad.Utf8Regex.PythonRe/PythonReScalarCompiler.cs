using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

// The Python parser resolves its own lexical scalar semantics. Lower that tree
// to the shared VM rather than rediscovering folding from a .NET option flag.
internal static class PythonReScalarCompiler
{
    internal static Utf8SliceProgram? Compile(PythonReParseResult parse,
        PythonReTranslation translation, bool fullMatch)
    {
        var root = Convert(parse.Root, parse.Options, false, 0);
        if (root is null) return null;
        if (fullMatch) root = new Utf8SliceNode.ConcatNode(
            [root, new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.VeryEnd, false)]);
        var names = new string[parse.CaptureGroupCount + 1];
        for (var group = 0; group < names.Length; group++)
            names[group] = group.ToString(CultureInfo.InvariantCulture);
        foreach (var named in parse.NamedGroups)
            names[translation.ManagedNumbersByPython[named.Value]] = translation.EmittedGroupNames[named.Key];
        return Utf8SliceLowerer.Lower(root, names, false, false, false);

        Utf8SliceNode? Convert(PythonReNode node, PythonReCompileOptions options, bool reversed, int depth)
        {
            if (depth > 2 * Utf8SliceParser.MaxNestingDepth) return null;
            switch (node)
            {
                case PythonReSequenceNode sequence:
                {
                    var children = new Utf8SliceNode[sequence.Elements.Count];
                    for (var index = 0; index < children.Length; index++)
                    {
                        var child = Convert(sequence.Elements[reversed ? children.Length - 1 - index : index], options, reversed, depth + 1);
                        if (child is null) return null;
                        children[index] = child;
                    }
                    return new Utf8SliceNode.ConcatNode(children);
                }
                case PythonReAlternationNode alternation:
                {
                    var branches = new Utf8SliceNode[alternation.Branches.Count][];
                    for (var index = 0; index < branches.Length; index++)
                    {
                        var child = Convert(alternation.Branches[index], options, reversed, depth + 1);
                        if (child is null) return null;
                        branches[index] = [child];
                    }
                    return new Utf8SliceNode.AlternationNode(branches);
                }
                case PythonReQuantifierNode repeat:
                {
                    var body = Convert(repeat.Inner, options, reversed, depth + 1);
                    if (body is null) return null;
                    var result = new Utf8SliceNode.RepeatNode(body, repeat.Min,
                        repeat.Max ?? int.MaxValue, repeat.Flavor == PythonReQuantifierFlavor.Reluctant);
                    return repeat.Flavor == PythonReQuantifierFlavor.Possessive
                        ? new Utf8SliceNode.AtomicNode(result) : result;
                }
                case PythonReGroupNode group:
                {
                    var scope = options;
                    if (group.Kind == PythonReGroupKind.ScopedFlags)
                    {
                        if ((group.AddOptions & PythonReCompileOptions.Ascii) != 0) scope &= ~PythonReCompileOptions.Unicode;
                        if ((group.AddOptions & PythonReCompileOptions.Unicode) != 0) scope &= ~PythonReCompileOptions.Ascii;
                        scope = (scope | group.AddOptions) & ~group.RemoveOptions;
                    }
                    var direction = group.Kind switch
                    {
                        PythonReGroupKind.PositiveLookbehind or PythonReGroupKind.NegativeLookbehind => true,
                        PythonReGroupKind.PositiveLookahead or PythonReGroupKind.NegativeLookahead => false,
                        _ => reversed,
                    };
                    var body = Convert(group.Inner, scope, direction, depth + 1);
                    if (body is null) return null;
                    return group.Kind switch
                    {
                        PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing =>
                            new Utf8SliceNode.CaptureNode(translation.ManagedNumbersByPython[group.GroupNumber] - 1, body),
                        PythonReGroupKind.PositiveLookbehind => new Utf8SliceNode.LookbehindNode(body, true),
                        PythonReGroupKind.NegativeLookbehind => new Utf8SliceNode.LookbehindNode(body, false),
                        PythonReGroupKind.PositiveLookahead => new Utf8SliceNode.LookaheadNode(body, true),
                        PythonReGroupKind.NegativeLookahead => new Utf8SliceNode.LookaheadNode(body, false),
                        PythonReGroupKind.Atomic => new Utf8SliceNode.AtomicNode(body),
                        _ => body,
                    };
                }
                case PythonReNumericBackreferenceNode reference:
                    return new Utf8SliceNode.BackrefNode(translation.ManagedNumbersByPython[reference.GroupNumber] - 1,
                        reference.LowerScalar is not null) { LowerScalar = reference.LowerScalar };
                case PythonReNamedBackreferenceNode reference:
                    return new Utf8SliceNode.BackrefNode(translation.ManagedNumbersByPython[parse.NamedGroups[reference.Name]] - 1,
                        reference.LowerScalar is not null) { LowerScalar = reference.LowerScalar };
                case PythonReConditionalNode conditional:
                {
                    var yes = Convert(conditional.YesBranch, options, reversed, depth + 1);
                    var no = conditional.NoBranch is null ? new Utf8SliceNode.EmptyNode()
                        : Convert(conditional.NoBranch, options, reversed, depth + 1);
                    if (yes is null || no is null) return null;
                    var number = conditional.Reference.Kind == PythonReConditionalReferenceKind.GroupNumber
                        ? conditional.Reference.GroupNumber
                        : parse.NamedGroups[conditional.Reference.GroupName ?? throw new InvalidOperationException("Conditional name missing.")];
                    return new Utf8SliceNode.ConditionalNode(translation.ManagedNumbersByPython[number] - 1, yes, no, null);
                }
                default:
                {
                    // Scalar classes and profile shorthands reuse the owned
                    // front end's class decoder. This fragment contains no
                    // captures/references or profile case flags.
                    var fragment = PythonReTranslator.RenderNativeAtom(node);
                    var atomOptions = RegexOptions.CultureInvariant
                        | ((options & PythonReCompileOptions.Multiline) != 0 ? RegexOptions.Multiline : RegexOptions.None);
                    var tree = Utf8OwnedTreeParser.TryParse(fragment, atomOptions);
                    if (tree?.CaptureCatalog is not { } catalog) return null;
                    var balancing = false;
                    var lazyCaptures = false;
                    return Utf8OwnedTreeSliceLowerer.TryConvertRoot(tree.Root, catalog, ref balancing, ref lazyCaptures);
                }
            }
        }
    }
}
