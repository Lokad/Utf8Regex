using ByteBuilder = Lokad.Utf8Regex.Internal.Utf8ConstructionBuffer<byte>;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

internal static partial class PythonReTranslator
{
    private enum InspectionKind { References, Lookbehind, Iteration, Literal }
    private struct InspectionFrame(PythonReNode node)
    {
        internal readonly PythonReNode Node = node;
        internal int Index = -1;
    }

    // Index borrowed AST lists directly. An arbitrary IReadOnlyList enumerator
    // would acquire or box a descriptor before its element could be admitted.
    private static bool InspectSelection(PythonReNode node, InspectionKind kind)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var result = Inspect(node, kind, default, null, null);
        if (result) construction.Complete();
        return result;
    }

    private static bool Inspect(PythonReNode node, InspectionKind kind,
        PythonReParseResult parseResult,
        Utf8ConstructionMap<PythonReNode, PythonReWidth>? widths,
        ByteBuilder? builder)
    {
        // Translation owns the width map across this walk. Roll it back only
        // after its arrays are disposed by Translate, never in a nested scope.
        var budget = Utf8ConstructionScope.CurrentBudget;
        using var frames = Utf8ConstructionFrames<InspectionFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var current = frame.Node;
            if (frame.Index < 0)
            {
                if (kind == InspectionKind.References)
                {
                    switch (current)
                    {
                        case PythonReNamedBackreferenceNode named when !parseResult.NamedGroups.ContainsKey(named.Name):
                            throw new PythonRePatternException($"unknown group name '{named.Name}'");
                        case PythonReNumericBackreferenceNode numeric when numeric.GroupNumber > parseResult.CaptureGroupCount:
                            throw new PythonRePatternException($"invalid group reference {numeric.GroupNumber}");
                        case PythonReConditionalNode conditional:
                            ValidateConditionalReference(conditional.Reference, parseResult.CaptureGroupCount, parseResult.NamedGroups);
                            break;
                    }
                }
                else if (kind == InspectionKind.Lookbehind && current is PythonReGroupNode
                    { Kind: PythonReGroupKind.PositiveLookbehind or PythonReGroupKind.NegativeLookbehind } behind)
                    behind.FixedLookbehindWidth = EnsureFixedWidth(behind.Inner, parseResult, widths ?? throw new InvalidOperationException("Width storage is missing."));
                else if (kind == InspectionKind.Iteration)
                {
                    if (current is PythonReConditionalNode) return false;
                    // The core's host categories do not define this profile's
                    // pinned classes. Their direct scalar lowering is authoritative.
                    if (current is PythonReRawNode { Kind: PythonReRawKind.Escape } raw
                        && PythonReParser.IsUnicodePredefinedFragment(raw.Text)) return false;
                    if (current is PythonReCharacterClassNode characterClass)
                        for (var classIndex = 0; classIndex < characterClass.Items.Count; classIndex++)
                        {
                            budget.ChargeWork(1);
                            if (characterClass.Items[classIndex] is PythonReCharacterClassPredefinedItem predefined
                                && PythonReParser.IsUnicodePredefinedFragment(predefined.RegexClassFragment)) return false;
                        }
                    if (current is PythonReAlternationNode alternation)
                    {
                        if (!IsPrefixFreeExactLiteralAlternation(alternation)) return false;
                        frames.Pop();
                        continue;
                    }
                }
                else if (kind == InspectionKind.Literal)
                {
                    if (current is PythonReLiteralNode literal)
                    {
                        if (char.IsSurrogate(literal.Value)) return false;
                        builder?.Append(literal.Value);
                    }
                    else if (current is not PythonReSequenceNode and not PythonReGroupNode { Kind: PythonReGroupKind.NonCapturing })
                        return false;
                }
                frame.Index = 0;
            }
            PythonReNode? child = null;
            switch (current)
            {
                case PythonReAlternationNode alternation when frame.Index < alternation.Branches.Count:
                    budget.ChargeWork(1);
                    child = alternation.Branches[frame.Index++];
                    break;
                case PythonReSequenceNode sequence when frame.Index < sequence.Elements.Count:
                    budget.ChargeWork(1);
                    child = sequence.Elements[frame.Index++];
                    break;
                case PythonReQuantifierNode repeat when frame.Index++ == 0:
                    child = repeat.Inner;
                    break;
                case PythonReGroupNode group when frame.Index++ == 0:
                    child = group.Inner;
                    break;
                case PythonReConditionalNode conditional:
                    child = frame.Index++ switch { 0 => conditional.YesBranch, 1 => conditional.NoBranch, _ => null };
                    break;
            }
            frames[index] = frame;
            if (child is null) frames.Pop();
            else frames.Push(new(child));
        }
        return true;
    }

    private struct WidthFrame(PythonReNode node)
    {
        internal readonly PythonReNode Node = node;
        internal int Index;
        internal PythonReNode? Pending;
        internal UInt128 Min = node is PythonReAlternationNode or PythonReConditionalNode ? PythonReWidth.Maximum : 0;
        internal UInt128 Max;
    }

    private static PythonReWidth GetWidth(PythonReNode node, PythonReParseResult parseResult,
        Utf8ConstructionMap<PythonReNode, PythonReWidth> widths)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        using var frames = Utf8ConstructionFrames<WidthFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.Index == 0 && widths.ContainsKey(frame.Node)) { frames.Pop(); continue; }
            if (frame.Pending is PythonReNode pending)
            {
                var childWidth = widths[pending];
                if (frame.Node is PythonReAlternationNode or PythonReConditionalNode)
                {
                    frame.Min = frame.Min < childWidth.Min ? frame.Min : childWidth.Min;
                    frame.Max = UnionMax(frame.Max, childWidth.Max);
                }
                else if (frame.Node is PythonReSequenceNode)
                {
                    frame.Min = AddMax(frame.Min, childWidth.Min);
                    frame.Max = AddMax(frame.Max, childWidth.Max);
                }
                else { frame.Min = childWidth.Min; frame.Max = childWidth.Max; }
                frame.Pending = null;
            }
            if (TryGetWidthChild(frame.Node, frame.Index, parseResult, out var child))
            {
                if (frame.Node is PythonReSequenceNode or PythonReAlternationNode) budget.ChargeWork(1);
                frame.Index++;
                frame.Pending = child;
                frames[index] = frame;
                frames.Push(new(child));
                continue;
            }
            var width = frame.Node switch
            {
                PythonReAlternationNode alternate => new PythonReWidth(alternate.Branches.Count == 0 ? 0 : frame.Min, frame.Max),
                PythonReSequenceNode => new(frame.Min, frame.Max),
                PythonReQuantifierNode repeat => new(ClampWidth(frame.Min * (uint)repeat.Min),
                    repeat.Max.HasValue ? ClampWidth(frame.Max * (uint)repeat.Max.Value) : frame.Max == 0 ? 0 : PythonReWidth.Maximum),
                PythonReConditionalNode conditional => new(conditional.NoBranch is null ? 0 : frame.Min, frame.Max),
                PythonReGroupNode { Kind: PythonReGroupKind.PositiveLookahead or PythonReGroupKind.NegativeLookahead
                    or PythonReGroupKind.PositiveLookbehind or PythonReGroupKind.NegativeLookbehind } => new(0, 0),
                PythonReGroupNode => new(frame.Min, frame.Max),
                PythonReNamedBackreferenceNode or PythonReNumericBackreferenceNode => frame.Index == 0
                    ? new(0, PythonReWidth.Maximum) : new(frame.Min, frame.Max),
                PythonReRawNode raw => raw.Kind is PythonReRawKind.StartAnchor or PythonReRawKind.EndAnchor ||
                    raw.Kind == PythonReRawKind.Escape && PythonReParser.IsZeroWidthEscape(raw.Text) ? new(0, 0) : new(1, 1),
                PythonReLiteralNode or PythonReCharacterClassNode => new(1, 1),
                _ => throw new PythonRePatternException("Unsupported AST node."),
            };
            widths[frame.Node] = width;
            frames.Pop();
        }
        return widths[node];
    }

    private static bool TryGetWidthChild(PythonReNode node, int index, PythonReParseResult parseResult,
        out PythonReNode child)
    {
        PythonReNode? next = node switch
        {
            PythonReAlternationNode alternate when index < alternate.Branches.Count => alternate.Branches[index],
            PythonReSequenceNode sequence when index < sequence.Elements.Count => sequence.Elements[index],
            PythonReQuantifierNode repeat when index == 0 => repeat.Inner,
            PythonReGroupNode { Kind: PythonReGroupKind.PositiveLookahead or PythonReGroupKind.NegativeLookahead
                or PythonReGroupKind.PositiveLookbehind or PythonReGroupKind.NegativeLookbehind } => null,
            PythonReGroupNode group when index == 0 => group.Inner,
            PythonReConditionalNode conditional => index switch { 0 => conditional.YesBranch, 1 => conditional.NoBranch, _ => null },
            PythonReNamedBackreferenceNode named when index == 0 && parseResult.NamedGroups.TryGetValue(named.Name, out var slot)
                && parseResult.TryGetGroupBody(slot, out var body) => body,
            PythonReNumericBackreferenceNode numeric when index == 0 && parseResult.TryGetGroupBody(numeric.GroupNumber, out var body) => body,
            _ => null,
        };
        child = next ?? PythonReAstFactory.Empty;
        return next is not null;
    }

    private enum RenderKind { Node, Group, Conditional, Quantifier, QuantifierCore, Atom }
    private struct RenderFrame(PythonReNode node, RenderKind kind, PythonReQuantifierFlavor flavor)
    {
        internal RenderFrame(PythonReNode node) : this(node, RenderKind.Node, PythonReQuantifierFlavor.Greedy) { }
        internal RenderFrame(PythonReNode node, RenderKind kind) : this(node, kind, PythonReQuantifierFlavor.Greedy) { }
        internal readonly PythonReNode Node = node;
        internal readonly RenderKind Kind = kind;
        internal readonly PythonReQuantifierFlavor Flavor = flavor;
        internal int Stage;
        internal bool Verbose;
    }

    private static void AppendTree(PythonReNode node, ByteBuilder builder,
        IReadOnlyDictionary<string, int> pythonGroupNumbers, int[] coreNumbersByPython, bool verbose = false)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        using var frames = Utf8ConstructionFrames<RenderFrame>.Create(budget, new(node) { Verbose = verbose });
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var complete = ResumeRender(ref frame, builder, pythonGroupNumbers, coreNumbersByPython, out var child);
            frames[index] = frame;
            if (complete) frames.Pop();
            else
            {
                child.Verbose = frame.Verbose;
                if (frame.Kind == RenderKind.Group && frame.Node is PythonReGroupNode { Kind: PythonReGroupKind.ScopedFlags } scoped)
                    child.Verbose = (scoped.AddOptions & PythonReCompileOptions.Verbose) != 0 ||
                        frame.Verbose && (scoped.RemoveOptions & PythonReCompileOptions.Verbose) == 0;
                frames.Push(child);
            }
        }
    }

    private static bool ResumeRender(ref RenderFrame frame, ByteBuilder builder,
        IReadOnlyDictionary<string, int> names, int[] numbers, out RenderFrame child)
    {
        child = default;
        switch (frame.Kind)
        {
            case RenderKind.Node:
                return ResumeRenderedNode(ref frame, builder, names, numbers, out child);
            case RenderKind.Group:
                if (frame.Stage++ == 0)
                {
                    var group = (PythonReGroupNode)frame.Node;
                    AppendGroupPrefix(group, builder);
                    child = new(group.Inner);
                    return false;
                }
                builder.Append(')');
                return true;
            case RenderKind.Conditional:
                var conditional = (PythonReConditionalNode)frame.Node;
                if (frame.Stage == 0)
                {
                    AppendConditionalPrefix(conditional, builder, names, numbers);
                    frame.Stage = 1;
                    child = new(conditional.YesBranch);
                    return false;
                }
                if (frame.Stage == 1 && conditional.NoBranch is PythonReNode no)
                {
                    builder.Append('|');
                    frame.Stage = 2;
                    child = new(no);
                    return false;
                }
                builder.Append(')');
                return true;
            case RenderKind.Quantifier:
                var repeat = (PythonReQuantifierNode)frame.Node;
                if (frame.Stage++ == 0)
                {
                    var possessive = repeat.Flavor == PythonReQuantifierFlavor.Possessive;
                    if (possessive) builder.Append("(?>");
                    child = new(repeat, RenderKind.QuantifierCore, possessive ? PythonReQuantifierFlavor.Greedy : repeat.Flavor);
                    return false;
                }
                if (repeat.Flavor == PythonReQuantifierFlavor.Possessive) builder.Append(')');
                return true;
            case RenderKind.QuantifierCore:
                if (frame.Stage++ == 0)
                {
                    child = new(((PythonReQuantifierNode)frame.Node).Inner, RenderKind.Atom);
                    return false;
                }
                AppendQuantifierSuffix((PythonReQuantifierNode)frame.Node, builder, frame.Flavor);
                return true;
            case RenderKind.Atom:
                // Keep compound atoms and raw supplementary literals grouped so
                // the following quantifier applies to the complete atom.
                var bare = frame.Node is PythonReLiteralNode or PythonReNamedBackreferenceNode
                    or PythonReNumericBackreferenceNode or PythonReGroupNode
                    or PythonReRawNode { Kind: not PythonReRawKind.AstralLiteral };
                if (frame.Stage++ == 0)
                {
                    if (!bare) builder.Append("(?:");
                    child = new(frame.Node);
                    return false;
                }
                if (!bare) builder.Append(')');
                return true;
            default:
                throw new InvalidOperationException("Unknown rendering frame.");
        }
    }

    private static bool ResumeRenderedNode(ref RenderFrame frame, ByteBuilder builder,
        IReadOnlyDictionary<string, int> names, int[] numbers, out RenderFrame child)
    {
        child = default;
        switch (frame.Node)
        {
            case PythonReAlternationNode alternation:
                if (frame.Stage == alternation.Branches.Count) return true;
                Utf8ConstructionScope.ChargeWork(1);
                if (frame.Stage != 0) builder.Append('|');
                child = new(alternation.Branches[frame.Stage++]);
                return false;
            case PythonReSequenceNode sequence:
                if (frame.Stage == sequence.Elements.Count) return true;
                Utf8ConstructionScope.ChargeWork(1);
                child = new(sequence.Elements[frame.Stage++]);
                return false;
            case PythonReLiteralNode literal:
                if (char.IsSurrogate(literal.Value)) builder.Append("(?!)");
                else builder.AppendEscaped(literal.Value, frame.Verbose);
                return true;
            case PythonReCharacterClassNode characterClass:
                AppendCharacterClass(characterClass, builder);
                return true;
            case PythonReRawNode raw:
                Utf8ConstructionScope.ChargeWork((ulong)raw.Text.Length);
                if (raw.Text == @"\ " && !frame.Verbose) builder.Append(' ');
                else builder.Append(raw.Text);
                return true;
            case PythonReNamedBackreferenceNode named:
                builder.Append(@"\k<g").Append(names[named.Name]).Append('>');
                return true;
            case PythonReNumericBackreferenceNode numeric:
                builder.Append('\\').Append(numbers[numeric.GroupNumber]);
                return true;
            case PythonReGroupNode or PythonReConditionalNode or PythonReQuantifierNode:
                if (frame.Stage++ != 0) return true;
                child = new(frame.Node, frame.Node is PythonReGroupNode ? RenderKind.Group
                    : frame.Node is PythonReConditionalNode ? RenderKind.Conditional : RenderKind.Quantifier);
                return false;
            default:
                throw new PythonRePatternException("Unsupported AST node.");
        }
    }

    private static void AppendGroupPrefix(PythonReGroupNode node, ByteBuilder builder)
    {
        if (node.Kind == PythonReGroupKind.NamedCapturing)
        {
            if (node.Name is null) throw new PythonRePatternException("Named capturing group has no group name.");
            // Every named capture uses its unique Python slot. User spellings
            // never enter this namespace, so names such as g1 cannot collide.
            builder.Append("(?<g").Append(node.GroupNumber).Append('>');
        }
        else if (node.Kind == PythonReGroupKind.ScopedFlags)
        {
            builder.Append("(?");
            AppendInlineFlags(node.AddOptions & ~PythonReCompileOptions.IgnoreCase, builder);
            var removed = node.RemoveOptions & ~PythonReCompileOptions.IgnoreCase;
            if (removed != PythonReCompileOptions.None) { builder.Append('-'); AppendInlineFlags(removed, builder); }
            builder.Append(':');
        }
        else builder.Append(node.Kind switch
        {
            PythonReGroupKind.Capturing => "(", PythonReGroupKind.NonCapturing => "(?:",
            PythonReGroupKind.PositiveLookahead => "(?=", PythonReGroupKind.NegativeLookahead => "(?!",
            PythonReGroupKind.PositiveLookbehind => "(?<=", PythonReGroupKind.NegativeLookbehind => "(?<!",
            PythonReGroupKind.Atomic => "(?>", _ => throw new PythonRePatternException("Unsupported group kind."),
        });
    }

    private static void AppendConditionalPrefix(PythonReConditionalNode node, ByteBuilder builder,
        IReadOnlyDictionary<string, int> names, int[] numbers)
    {
        builder.Append("(?(");
        switch (node.Reference.Kind)
        {
            case PythonReConditionalReferenceKind.GroupNumber: builder.Append(numbers[node.Reference.GroupNumber]); break;
            case PythonReConditionalReferenceKind.GroupName:
                if (node.Reference.GroupName is not string name) throw new PythonRePatternException("Named conditional reference has no group name.");
                builder.Append('g').Append(names[name]);
                break;
            default: throw new PythonRePatternException("Unsupported conditional reference.");
        }
        builder.Append(')');
    }

    private static void AppendQuantifierSuffix(PythonReQuantifierNode node, ByteBuilder builder, PythonReQuantifierFlavor flavor)
    {
        if (node.Min == 0 && node.Max is null) builder.Append('*');
        else if (node.Min == 1 && node.Max is null) builder.Append('+');
        else if (node.Min == 0 && node.Max == 1) builder.Append('?');
        else if (node.Max == node.Min) builder.Append('{').Append(node.Min).Append('}');
        else
        {
            builder.Append('{').Append(node.Min).Append(',');
            if (node.Max.HasValue) builder.Append(node.Max.Value);
            builder.Append('}');
        }
        builder.Append(flavor switch
        {
            PythonReQuantifierFlavor.Greedy => string.Empty, PythonReQuantifierFlavor.Reluctant => "?",
            PythonReQuantifierFlavor.Possessive => throw new PythonRePatternException("Possessive quantifiers must be lowered before emission."),
            _ => throw new PythonRePatternException("Unsupported quantifier flavor."),
        });
    }
}
