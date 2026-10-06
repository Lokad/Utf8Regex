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
        var nodes = new Dictionary<PythonReNode, Utf8SliceNode>(ReferenceEqualityComparer.Instance);
        if (!Utf8PreparationStep.TryRun(Convert(parse.Root, parse.Options, false))) return null;
        var root = nodes[parse.Root];
        if (fullMatch) root = new Utf8SliceNode.ConcatNode(
            [root, new Utf8SliceNode.AnchorNode(Utf8SliceAnchorKind.VeryEnd, false)]);
        var names = new string[parse.CaptureGroupCount + 1];
        for (var group = 0; group < names.Length; group++)
            names[group] = group.ToString(CultureInfo.InvariantCulture);
        foreach (var named in parse.NamedGroups)
            names[translation.ManagedNumbersByPython[named.Value]] = translation.EmittedGroupNames[named.Key];
        return Utf8SliceLowerer.Lower(root, names, false, false, false);

        IEnumerable<Utf8PreparationStep> Convert(PythonReNode node, PythonReCompileOptions options, bool reversed)
        {
            switch (node)
            {
                case PythonReSequenceNode sequence:
                    {
                        var children = new Utf8SliceNode[sequence.Elements.Count];
                        for (var index = 0; index < children.Length; index++)
                        {
                            yield return new Utf8PreparationStep(Convert(sequence.Elements[reversed ? children.Length - 1 - index : index], options, reversed), false);
                            var child = nodes[sequence.Elements[reversed ? children.Length - 1 - index : index]];
                            children[index] = child;
                        }
                        nodes[node] = new Utf8SliceNode.ConcatNode(children);
                        yield break;
                    }
                case PythonReAlternationNode alternation:
                    {
                        var branches = new Utf8SliceNode[alternation.Branches.Count][];
                        for (var index = 0; index < branches.Length; index++)
                        {
                            yield return new Utf8PreparationStep(Convert(alternation.Branches[index], options, reversed), false);
                            var child = nodes[alternation.Branches[index]];
                            branches[index] = [child];
                        }
                        nodes[node] = new Utf8SliceNode.AlternationNode(branches);
                        yield break;
                    }
                case PythonReQuantifierNode repeat:
                    {
                        yield return new Utf8PreparationStep(Convert(repeat.Inner, options, reversed), false);
                        var body = nodes[repeat.Inner];
                        var result = new Utf8SliceNode.RepeatNode(body, repeat.Min,
                            repeat.Max ?? int.MaxValue, repeat.Flavor == PythonReQuantifierFlavor.Reluctant);
                        nodes[node] = repeat.Flavor == PythonReQuantifierFlavor.Possessive
                            ? new Utf8SliceNode.AtomicNode(result) : result;
                        yield break;
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
                        yield return new Utf8PreparationStep(Convert(group.Inner, scope, direction), false);
                        var body = nodes[group.Inner];
                        nodes[node] = group.Kind switch
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
                        yield break;
                    }
                case PythonReNumericBackreferenceNode reference:
                    nodes[node] = new Utf8SliceNode.BackrefNode(translation.ManagedNumbersByPython[reference.GroupNumber] - 1,
                        reference.LowerScalar is not null)
                    { LowerScalar = reference.LowerScalar };
                    yield break;
                case PythonReNamedBackreferenceNode reference:
                    nodes[node] = new Utf8SliceNode.BackrefNode(translation.ManagedNumbersByPython[parse.NamedGroups[reference.Name]] - 1,
                        reference.LowerScalar is not null)
                    { LowerScalar = reference.LowerScalar };
                    yield break;
                case PythonReConditionalNode conditional:
                    {
                        yield return new Utf8PreparationStep(Convert(conditional.YesBranch, options, reversed), false);
                        var yes = nodes[conditional.YesBranch];
                        if (conditional.NoBranch is { } noBranch)
                            yield return new Utf8PreparationStep(Convert(noBranch, options, reversed), false);
                        var no = conditional.NoBranch is { } branch ? nodes[branch] : new Utf8SliceNode.EmptyNode();
                        var number = conditional.Reference.Kind == PythonReConditionalReferenceKind.GroupNumber
                            ? conditional.Reference.GroupNumber
                            : parse.NamedGroups[conditional.Reference.GroupName ?? throw new InvalidOperationException("Conditional name missing.")];
                        nodes[node] = new Utf8SliceNode.ConditionalNode(translation.ManagedNumbersByPython[number] - 1, yes, no, null);
                        yield break;
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
                        if (tree?.CaptureCatalog is not { } catalog)
                        {
                            yield return Utf8PreparationStep.Decline;
                            yield break;
                        }
                        var balancing = false;
                        var lazyCaptures = false;
                        if (Utf8OwnedTreeSliceLowerer.TryConvertRoot(tree.Root, catalog, ref balancing, ref lazyCaptures) is not { } atom)
                        {
                            yield return Utf8PreparationStep.Decline;
                            yield break;
                        }
                        nodes[node] = atom;
                        yield break;
                    }
            }
        }
    }
}
