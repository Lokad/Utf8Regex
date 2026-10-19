using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.PythonRe;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

// Reference-only UTF-16 spelling for the .NET test/benchmark comparator. The
// shipped adapter renders native scalar text only. This file is linked into the
// Python tests, so reference rendering never becomes a library dependency.
internal static class PythonReDotNetReference
{
    private const string AnyUnicodeScalarPattern = @"(?:[\u0000-\uD7FF\uE000-\uFFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF])";

    // The managed comparator consumes complete surrogate pairs. Native scalar
    // fragments belong to the parser; this spelling belongs only to tooling.
    private static string RenderFragment(string fragment)
    {
        var withoutNewline = Utf8ConstructionText.Replace(fragment, "(?-s:.)", @"(?!\n)" + AnyUnicodeScalarPattern);
        return Utf8ConstructionText.Replace(withoutNewline, "(?s:.)", AnyUnicodeScalarPattern);
    }

    internal static string Render(Utf8PythonRegex regex)
    {
        var parsed = new PythonReParser(regex.Pattern).Parse(regex.Options);
        return Render(parsed, PythonReTranslator.Translate(parsed));
    }

    internal static string Render(PythonReParseResult parseResult, PythonReTranslation translation)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var arena = Utf8ConstructionArena.Create(construction.Budget);
        using var builder = Utf8ConstructionText.Create();
        var coreNumbersByPython = arena.Allocate<int>(checked(parseResult.CaptureGroupCount + 1), false);
        var namedPythonGroups = arena.Allocate<bool>(coreNumbersByPython.Length, false);
        foreach (var number in parseResult.NamedGroups.Values)
        {
            construction.Budget.ChargeWork(1);
            namedPythonGroups[number] = true;
        }
        var nextCoreGroup = 1;
        for (var pythonGroup = 1; pythonGroup <= parseResult.CaptureGroupCount; pythonGroup++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!namedPythonGroups[pythonGroup])
            {
                coreNumbersByPython[pythonGroup] = nextCoreGroup++;
            }
        }

        for (var pythonGroup = 1; pythonGroup <= parseResult.CaptureGroupCount; pythonGroup++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (namedPythonGroups[pythonGroup])
            {
                coreNumbersByPython[pythonGroup] = nextCoreGroup++;
            }
        }

        AppendTree(parseResult.Root, builder, translation.EmittedGroupNames, coreNumbersByPython);
        var result = builder.ToText();
        if ((parseResult.Options & PythonReCompileOptions.Verbose) == 0)
            result = Utf8ConstructionText.Replace(result, @"\ ", " ");
        arena.Complete();
        construction.Complete();
        return result;
    }

    private static void AppendInlineFlags(PythonReCompileOptions options, Utf8ConstructionText builder)
    {
        if ((options & PythonReCompileOptions.IgnoreCase) != 0)
        {
            builder.Append('i');
        }

        if ((options & PythonReCompileOptions.Multiline) != 0)
        {
            builder.Append('m');
        }

        if ((options & PythonReCompileOptions.DotAll) != 0)
        {
            builder.Append('s');
        }

        if ((options & PythonReCompileOptions.Verbose) != 0)
        {
            builder.Append('x');
        }
    }

    internal static RegexOptions ToRegexOptions(PythonReCompileOptions options)
    {
        var translated = RegexOptions.CultureInvariant;
        // Top-level ASCII runs the backend case-sensitively (PLAN.md C05): every
        // ASCII case pair is expanded explicitly at parse time, and a folding
        // backend could not exclude non-ASCII fold partners (U+212A and friends).
        // Scoped (?i:) regions still fold through inline flags.
        if ((options & PythonReCompileOptions.IgnoreCase) != 0 &&
            (options & PythonReCompileOptions.Ascii) == 0)
        {
            translated |= RegexOptions.IgnoreCase;
        }

        if ((options & PythonReCompileOptions.Multiline) != 0)
        {
            translated |= RegexOptions.Multiline;
        }

        if ((options & PythonReCompileOptions.DotAll) != 0)
        {
            translated |= RegexOptions.Singleline;
        }

        if ((options & PythonReCompileOptions.Verbose) != 0)
        {
            translated |= RegexOptions.IgnorePatternWhitespace;
        }

        return translated;
    }

    private static void AppendCharacterClass(PythonReCharacterClassNode node, Utf8ConstructionText builder)
    {
        var complete = false;
        for (var index = 0; index < node.Items.Count; index++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (node.Items[index] is PythonReCharacterClassRangeItem { StartScalar: 0, EndScalar: 0x10ffff })
            { complete = true; break; }
        }
        // Reuse the existing scalar atom in both renderings. The managed
        // comparator still consumes a complete surrogate pair, without making
        // 1024 separate branches for a class that contains every scalar.
        var positiveAtom = complete ? AnyUnicodeScalarPattern : BuildPositiveCharacterClassAtom(node.Items);
        if (!node.IsNegated)
        {
            builder.Append(positiveAtom);
            return;
        }

        builder.Append("(?:(?!").Append(positiveAtom).Append(')').Append(AnyUnicodeScalarPattern).Append(')');
    }

    private static string BuildPositiveCharacterClassAtom(IReadOnlyList<PythonReCharacterClassItem> items)
    {
        using var bmpRanges = Utf8ConstructionBuffer<(int Start, int End)>.Create(Utf8ConstructionScope.CurrentBudget);
        using var bmpFragments = Utf8ConstructionBuffer<string>.Create(Utf8ConstructionScope.CurrentBudget);
        using var standaloneAtoms = Utf8ConstructionBuffer<string>.Create(Utf8ConstructionScope.CurrentBudget);
        using var astralBranches = Utf8ConstructionBuffer<string>.Create(Utf8ConstructionScope.CurrentBudget);

        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            var item = items[itemIndex];
            Utf8ConstructionScope.ChargeWork(1);
            switch (item)
            {
                case PythonReCharacterClassLiteralItem literal:
                    if (literal.Scalar <= 0xFFFF)
                    {
                        AddBmpRange(bmpRanges, literal.Scalar, literal.Scalar);
                    }
                    else
                    {
                        astralBranches.Add(BuildAstralScalarBranch(literal.Scalar));
                    }
                    break;

                case PythonReCharacterClassRangeItem range:
                    if (range.EndScalar <= 0xFFFF)
                    {
                        AddBmpRange(bmpRanges, range.StartScalar, range.EndScalar);
                    }
                    else if (range.StartScalar > 0xFFFF)
                    {
                        AppendAstralRangeBranches(astralBranches, range.StartScalar, range.EndScalar);
                    }
                    else
                    {
                        AddBmpRange(bmpRanges, range.StartScalar, 0xFFFF);
                        AppendAstralRangeBranches(astralBranches, 0x10000, range.EndScalar);
                    }
                    break;

                case PythonReCharacterClassPredefinedItem predefined:
                    if (predefined.IsStandaloneAtom)
                    {
                        // Complete class atoms (PLAN.md C04): emitted as their own
                        // alternation branch so astral scalars keep matching instead
                        // of being dropped by the BMP-only class below.
                        standaloneAtoms.Add(RenderFragment(predefined.RegexClassFragment));
                    }
                    else
                    {
                        bmpFragments.Add(predefined.RegexClassFragment);
                    }

                    break;
            }
        }

        var bmpClass = BuildRangeCharacterClass(bmpRanges, bmpFragments);
        return JoinClassBranches(bmpClass, standaloneAtoms.Span, astralBranches.Span);
    }

    private static string JoinClassBranches(string leading, ReadOnlySpan<string> branches, ReadOnlySpan<string> tail)
    {
        var count = (leading.Length == 0 ? 0 : 1) + branches.Length + tail.Length;
        if (count == 0) return "(?!)";
        if (count == 1) return leading.Length != 0 ? leading : branches.Length != 0 ? branches[0] : tail[0];
        using var builder = Utf8ConstructionText.Create();
        builder.Append("(?:");
        var first = true;
        if (leading.Length != 0) { builder.Append(leading); first = false; }
        foreach (var branch in branches)
        {
            if (!first) builder.Append('|');
            builder.Append(branch); first = false;
        }
        foreach (var branch in tail)
        {
            if (!first) builder.Append('|');
            builder.Append(branch); first = false;
        }
        return builder.Append(')').ToText();
    }

    private static string BuildRangeCharacterClass(Utf8ConstructionBuffer<(int Start, int End)> ranges, Utf8ConstructionBuffer<string> fragments)
    {
        if (ranges.Count == 0 && fragments.Count == 0)
        {
            return string.Empty;
        }

        using var builder = Utf8ConstructionText.Create();
        builder.Append('[');
        foreach (var fragment in fragments)
        {
            Utf8ConstructionScope.ChargeWork(1);
            builder.Append(fragment);
        }

        if (ranges.Count != 0)
        {
            ranges.Sort(static (x, y) => x.Start.CompareTo(y.Start));
            var currentStart = ranges[0].Start;
            var currentEnd = ranges[0].End;
            for (var index = 1; index < ranges.Count; index++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                var next = ranges[index];
                if (next.Start <= currentEnd + 1) currentEnd = Math.Max(currentEnd, next.End);
                else
                {
                    AppendClassRange(builder, currentStart, currentEnd);
                    currentStart = next.Start; currentEnd = next.End;
                }
            }
            AppendClassRange(builder, currentStart, currentEnd);
        }
        return builder.Append(']').ToText();
    }

    private static void AddBmpRange(Utf8ConstructionBuffer<(int Start, int End)> ranges, int start, int end)
    {
        if (start <= 0xD7FF)
        {
            ranges.Add((start, Math.Min(end, 0xD7FF)));
        }

        if (end >= 0xE000 && start <= 0xFFFF)
        {
            ranges.Add((Math.Max(start, 0xE000), Math.Min(end, 0xFFFF)));
        }
    }

    private static void AppendClassRange(Utf8ConstructionText builder, int start, int end)
    {
        if (start == end)
        {
            AppendClassChar(builder, start);
            return;
        }

        AppendClassChar(builder, start);
        builder.Append('-');
        AppendClassChar(builder, end);
    }

    private static void AppendClassChar(Utf8ConstructionText builder, int scalar)
    {
        if (scalar > 0xFFFF)
        {
            builder.AppendScalar(scalar);
            return;
        }
        var ch = (char)scalar;
        if (ch is '\\' or '-' or ']' or '^')
        {
            builder.Append('\\');
            builder.Append(ch);
            return;
        }

        if (scalar < 0x20 || scalar > 0x7E)
        {
            builder.Append(@"\u").AppendHex(scalar);
            return;
        }

        builder.Append(ch);
    }

    private static string BuildAstralScalarBranch(int scalar)
    {
        // The managed comparator groups the pair so a repeat binds the scalar.
        using var builder = Utf8ConstructionText.Create();
        return builder.Append("(?:").AppendScalar(scalar).Append(')').ToText();
    }

    private static void AppendAstralRangeBranches(Utf8ConstructionBuffer<string> branches, int startScalar, int endScalar)
    {
        var startHigh = 0xd800 + ((startScalar - 0x10000) >> 10);
        var endHigh = 0xd800 + ((endScalar - 0x10000) >> 10);
        var lowStart = (char)(0xdc00 + ((startScalar - 0x10000) & 1023));
        var lowEnd = (char)(0xdc00 + ((endScalar - 0x10000) & 1023));
        if (startHigh == endHigh)
        {
            branches.Add(BuildSurrogateBranch((char)startHigh, (char)endHigh, lowStart, lowEnd));
            return;
        }
        // Preserve ascending, disjoint scalar intervals. Complete middle rows
        // form one rectangle; only the two edge rows can need partial lows.
        if (lowStart != '\uDC00')
            branches.Add(BuildSurrogateBranch((char)startHigh, (char)startHigh, lowStart, '\uDFFF'));
        var firstComplete = lowStart == '\uDC00' ? startHigh : startHigh + 1;
        var lastComplete = lowEnd == '\uDFFF' ? endHigh : endHigh - 1;
        if (firstComplete <= lastComplete)
            branches.Add(BuildSurrogateBranch((char)firstComplete, (char)lastComplete, '\uDC00', '\uDFFF'));
        if (lowEnd != '\uDFFF')
            branches.Add(BuildSurrogateBranch((char)endHigh, (char)endHigh, '\uDC00', lowEnd));
    }

    private static string BuildSurrogateBranch(char highStart, char highEnd, char lowStart, char lowEnd)
    {
        Utf8ConstructionScope.ChargeWork(1);
        using var builder = Utf8ConstructionText.Create();
        builder.Append("(?:");
        if (highStart == highEnd) builder.Append(@"\u").AppendHex(highStart);
        else builder.Append(@"[\u").AppendHex(highStart).Append(@"-\u").AppendHex(highEnd).Append(']');
        if (lowStart == lowEnd) builder.Append(@"\u").AppendHex(lowStart);
        else builder.Append(@"[\u").AppendHex(lowStart).Append(@"-\u").AppendHex(lowEnd).Append(']');
        return builder.Append(')').ToText();
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
    }

    private static void AppendTree(PythonReNode node, Utf8ConstructionText builder,
        IReadOnlyDictionary<string, string> emittedGroupNames, int[] coreNumbersByPython)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        using var frames = Utf8ConstructionFrames<RenderFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var complete = ResumeRender(ref frame, builder, emittedGroupNames, coreNumbersByPython, out var child);
            frames[index] = frame;
            if (complete) frames.Pop();
            else frames.Push(child);
        }
    }

    private static bool ResumeRender(ref RenderFrame frame, Utf8ConstructionText builder,
        IReadOnlyDictionary<string, string> names, int[] numbers, out RenderFrame child)
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
                    AppendGroupPrefix(group, builder, names);
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
                // Group compound atoms and the managed spelling of supplementary
                // literals. This is pattern interoperability, not subject width.
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

    private static bool ResumeRenderedNode(ref RenderFrame frame, Utf8ConstructionText builder,
        IReadOnlyDictionary<string, string> names, int[] numbers, out RenderFrame child)
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
                builder.AppendEscaped(literal.Value);
                return true;
            case PythonReCharacterClassNode characterClass:
                AppendCharacterClass(characterClass, builder);
                return true;
            case PythonReRawNode raw:
                Utf8ConstructionScope.ChargeWork((ulong)raw.Text.Length);
                builder.Append(RenderFragment(raw.Text));
                return true;
            case PythonReNamedBackreferenceNode named:
                builder.Append(@"\k<").Append(names[named.Name]).Append('>');
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

    private static void AppendGroupPrefix(PythonReGroupNode node, Utf8ConstructionText builder,
        IReadOnlyDictionary<string, string> names)
    {
        if (node.Kind == PythonReGroupKind.NamedCapturing)
        {
            if (node.Name is not string name) throw new PythonRePatternException("Named capturing group has no group name.");
            builder.Append("(?<").Append(names[name]).Append('>');
        }
        else if (node.Kind == PythonReGroupKind.ScopedFlags)
        {
            builder.Append("(?");
            AppendInlineFlags(node.AddOptions, builder);
            var removed = node.RemoveOptions;
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

    private static void AppendConditionalPrefix(PythonReConditionalNode node, Utf8ConstructionText builder,
        IReadOnlyDictionary<string, string> names, int[] numbers)
    {
        builder.Append("(?(");
        switch (node.Reference.Kind)
        {
            case PythonReConditionalReferenceKind.GroupNumber: builder.Append(numbers[node.Reference.GroupNumber]); break;
            case PythonReConditionalReferenceKind.GroupName:
                if (node.Reference.GroupName is not string name) throw new PythonRePatternException("Named conditional reference has no group name.");
                builder.Append(names[name]);
                break;
            default: throw new PythonRePatternException("Unsupported conditional reference.");
        }
        builder.Append(')');
    }

    private static void AppendQuantifierSuffix(PythonReQuantifierNode node, Utf8ConstructionText builder, PythonReQuantifierFlavor flavor)
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
