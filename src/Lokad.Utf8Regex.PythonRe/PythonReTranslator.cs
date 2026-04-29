using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReTranslator
{
    private const string AsciiWordBoundary = @"(?:(?<![A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<=[A-Za-z0-9_])(?![A-Za-z0-9_]))";
    private const string AsciiNonWordBoundary = @"(?:(?<=[A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<![A-Za-z0-9_])(?![A-Za-z0-9_]))";
    private const string AnyUnicodeScalarPattern = @"(?:[\u0000-\uD7FF\uE000-\uFFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF])";

    public static bool CanMatchEmpty(PythonReNode node) => GetWidth(node).Min == 0;

    public static bool CanUseUtf8IterationFastPath(PythonReNode node) => IsUtf8IterationSafe(node);

    public static bool CanUseUtf8ReplacementFastPath(PythonReNode node) => !ContainsConditional(node);

    public static PythonReTranslation Translate(PythonReParseResult parseResult)
    {
        ValidateReferences(parseResult.Root, parseResult.CaptureGroupCount, parseResult.NamedGroups);
        var emittedGroupNames = BuildEmittedGroupNames(parseResult.NamedGroups);
        var builder = new StringBuilder();
        AppendNode(parseResult.Root, builder, emittedGroupNames);
        return new PythonReTranslation(
            builder.ToString(),
            ToRegexOptions(parseResult.Options),
            parseResult.CaptureGroupCount,
            emittedGroupNames);
    }

    private static void ValidateReferences(
        PythonReNode node,
        int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
    {
        switch (node)
        {
            case PythonReAlternationNode alternation:
                foreach (var branch in alternation.Branches)
                {
                    ValidateReferences(branch, captureGroupCount, namedGroups);
                }

                break;

            case PythonReSequenceNode sequence:
                foreach (var element in sequence.Elements)
                {
                    ValidateReferences(element, captureGroupCount, namedGroups);
                }

                break;

            case PythonReNamedBackreferenceNode namedBackreference:
                if (!namedGroups.ContainsKey(namedBackreference.Name))
                {
                    throw new PythonRePatternException($"unknown group name '{namedBackreference.Name}'");
                }

                break;

            case PythonReNumericBackreferenceNode numericBackreference:
                if (numericBackreference.GroupNumber > captureGroupCount)
                {
                    throw new PythonRePatternException($"invalid group reference {numericBackreference.GroupNumber}");
                }

                break;

            case PythonReConditionalNode conditional:
                ValidateConditionalReference(conditional.Reference, captureGroupCount, namedGroups);
                ValidateReferences(conditional.YesBranch, captureGroupCount, namedGroups);
                if (conditional.NoBranch is not null)
                {
                    ValidateReferences(conditional.NoBranch, captureGroupCount, namedGroups);
                }

                break;

            case PythonReQuantifierNode quantifier:
                ValidateReferences(quantifier.Inner, captureGroupCount, namedGroups);
                break;

            case PythonReGroupNode group:
                ValidateReferences(group.Inner, captureGroupCount, namedGroups);
                break;
        }
    }

    private static void ValidateConditionalReference(
        PythonReConditionalReference reference,
        int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
    {
        switch (reference.Kind)
        {
            case PythonReConditionalReferenceKind.GroupNumber:
                if (reference.GroupNumber > captureGroupCount)
                {
                    throw new PythonRePatternException($"invalid group reference {reference.GroupNumber}");
                }

                break;
            case PythonReConditionalReferenceKind.GroupName:
                if (!namedGroups.ContainsKey(reference.GroupName!))
                {
                    throw new PythonRePatternException($"unknown group name '{reference.GroupName}'");
                }

                break;
        }
    }

    private static void AppendNode(PythonReNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        switch (node)
        {
            case PythonReAlternationNode alternation:
                for (var i = 0; i < alternation.Branches.Count; i++)
                {
                    if (i != 0)
                    {
                        builder.Append('|');
                    }

                    AppendNode(alternation.Branches[i], builder, emittedGroupNames);
                }
                break;

            case PythonReSequenceNode sequence:
                foreach (var element in sequence.Elements)
                {
                    AppendNode(element, builder, emittedGroupNames);
                }
                break;

            case PythonReLiteralNode literal:
                builder.Append(Regex.Escape(literal.Value.ToString()));
                break;

            case PythonReCharacterClassNode characterClass:
                AppendCharacterClass(characterClass, builder);
                break;

            case PythonReRawNode raw:
                builder.Append(raw.Text);
                break;

            case PythonReNamedBackreferenceNode namedBackreference:
                builder.Append(@"\k<");
                builder.Append(emittedGroupNames[namedBackreference.Name]);
                builder.Append('>');
                break;

            case PythonReNumericBackreferenceNode numericBackreference:
                builder.Append('\\');
                builder.Append(numericBackreference.GroupNumber);
                break;

            case PythonReConditionalNode conditional:
                AppendConditional(conditional, builder, emittedGroupNames);
                break;

            case PythonReQuantifierNode quantifier:
                AppendQuantifier(quantifier, builder, emittedGroupNames);
                break;

            case PythonReGroupNode group:
                AppendGroup(group, builder, emittedGroupNames);
                break;

            default:
                throw new PythonRePatternException("Unsupported AST node.");
        }
    }

    private static void AppendQuantifier(PythonReQuantifierNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        if (node.Flavor == PythonReQuantifierFlavor.Possessive)
        {
            builder.Append("(?>");
            AppendQuantifierCore(node, builder, PythonReQuantifierFlavor.Greedy, emittedGroupNames);
            builder.Append(')');
            return;
        }

        AppendQuantifierCore(node, builder, node.Flavor, emittedGroupNames);
    }

    private static void AppendQuantifierCore(PythonReQuantifierNode node, StringBuilder builder, PythonReQuantifierFlavor flavor, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        AppendInnerAsAtom(node.Inner, builder, emittedGroupNames);
        if (node.Min == 0 && node.Max is null)
        {
            builder.Append('*');
        }
        else if (node.Min == 1 && node.Max is null)
        {
            builder.Append('+');
        }
        else if (node.Min == 0 && node.Max == 1)
        {
            builder.Append('?');
        }
        else if (node.Max == node.Min)
        {
            builder.Append('{').Append(node.Min).Append('}');
        }
        else
        {
            builder.Append('{').Append(node.Min).Append(',');
            if (node.Max.HasValue)
            {
                builder.Append(node.Max.Value);
            }

            builder.Append('}');
        }

        builder.Append(flavor switch
        {
            PythonReQuantifierFlavor.Greedy => string.Empty,
            PythonReQuantifierFlavor.Reluctant => "?",
            PythonReQuantifierFlavor.Possessive => throw new PythonRePatternException("Possessive quantifiers must be lowered before emission."),
            _ => throw new PythonRePatternException("Unsupported quantifier flavor."),
        });
    }

    private static void AppendGroup(PythonReGroupNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        switch (node.Kind)
        {
            case PythonReGroupKind.Capturing:
                builder.Append('(');
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            case PythonReGroupKind.NamedCapturing:
                builder.Append("(?<").Append(emittedGroupNames[node.Name!]).Append('>');
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            case PythonReGroupKind.NonCapturing:
                builder.Append("(?:");
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            case PythonReGroupKind.PositiveLookahead:
                builder.Append("(?=");
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            case PythonReGroupKind.NegativeLookahead:
                builder.Append("(?!");
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            case PythonReGroupKind.PositiveLookbehind:
                EnsureFixedWidth(node.Inner);
                builder.Append("(?<=");
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            case PythonReGroupKind.NegativeLookbehind:
                EnsureFixedWidth(node.Inner);
                builder.Append("(?<!");
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            case PythonReGroupKind.Atomic:
                builder.Append("(?>");
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            case PythonReGroupKind.ScopedFlags:
                builder.Append("(?");
                AppendInlineFlags(node.AddOptions, builder);
                if (node.RemoveOptions != PythonReCompileOptions.None)
                {
                    builder.Append('-');
                    AppendInlineFlags(node.RemoveOptions, builder);
                }

                builder.Append(':');
                AppendNode(node.Inner, builder, emittedGroupNames);
                builder.Append(')');
                return;

            default:
                throw new PythonRePatternException("Unsupported group kind.");
        }
    }

    private static void AppendConditional(PythonReConditionalNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        builder.Append("(?(");
        switch (node.Reference.Kind)
        {
            case PythonReConditionalReferenceKind.GroupNumber:
                builder.Append(node.Reference.GroupNumber);
                break;
            case PythonReConditionalReferenceKind.GroupName:
                builder.Append(emittedGroupNames[node.Reference.GroupName!]);
                break;
            default:
                throw new PythonRePatternException("Unsupported conditional reference.");
        }

        builder.Append(')');
        AppendNode(node.YesBranch, builder, emittedGroupNames);
        if (node.NoBranch is not null)
        {
            builder.Append('|');
            AppendNode(node.NoBranch, builder, emittedGroupNames);
        }

        builder.Append(')');
    }

    private static void AppendInlineFlags(PythonReCompileOptions options, StringBuilder builder)
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

    private static void AppendInnerAsAtom(PythonReNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        switch (node)
        {
            case PythonReLiteralNode:
            case PythonReRawNode:
            case PythonReNamedBackreferenceNode:
            case PythonReNumericBackreferenceNode:
            case PythonReGroupNode:
                AppendNode(node, builder, emittedGroupNames);
                break;

            default:
                builder.Append("(?:");
                AppendNode(node, builder, emittedGroupNames);
                builder.Append(')');
                break;
        }
    }

    private static IReadOnlyDictionary<string, string> BuildEmittedGroupNames(IReadOnlyDictionary<string, int> namedGroups)
    {
        var map = new Dictionary<string, string>(namedGroups.Count, StringComparer.Ordinal);
        foreach (var pair in namedGroups.OrderBy(x => x.Value))
        {
            map[pair.Key] = IsDotNetSafeGroupName(pair.Key)
                ? pair.Key
                : "g" + pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return map;
    }

    private static bool IsDotNetSafeGroupName(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsAsciiLetter(name[0]) && name[0] != '_')
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!(char.IsAsciiLetterOrDigit(name[i]) || name[i] == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static void EnsureFixedWidth(PythonReNode node)
    {
        var width = GetWidth(node);
        if (!width.IsFixed)
        {
            throw new PythonRePatternException("look-behind requires fixed-width pattern");
        }
    }

    private static PythonReWidth GetWidth(PythonReNode node)
    {
        return node switch
        {
            PythonReAlternationNode alternation => GetAlternationWidth(alternation),
            PythonReSequenceNode sequence => GetSequenceWidth(sequence),
            PythonReLiteralNode => new PythonReWidth(1, 1),
            PythonReCharacterClassNode => new PythonReWidth(1, 1),
            PythonReRawNode raw => raw.Kind switch
            {
                PythonReRawKind.StartAnchor or PythonReRawKind.EndAnchor => new PythonReWidth(0, 0),
                PythonReRawKind.Escape when IsZeroWidthEscape(raw.Text) => new PythonReWidth(0, 0),
                _ => new PythonReWidth(1, 1),
            },
            PythonReNamedBackreferenceNode or PythonReNumericBackreferenceNode => new PythonReWidth(0, null),
            PythonReConditionalNode conditional => GetConditionalWidth(conditional),
            PythonReQuantifierNode quantifier => GetQuantifierWidth(quantifier),
            PythonReGroupNode group => group.Kind switch
            {
                PythonReGroupKind.PositiveLookahead or
                PythonReGroupKind.NegativeLookahead or
                PythonReGroupKind.PositiveLookbehind or
                PythonReGroupKind.NegativeLookbehind => new PythonReWidth(0, 0),
                _ => GetWidth(group.Inner),
            },
            _ => throw new PythonRePatternException("Unsupported AST node."),
        };
    }

    private static PythonReWidth GetAlternationWidth(PythonReAlternationNode alternation)
    {
        var min = int.MaxValue;
        int? max = 0;
        foreach (var branch in alternation.Branches)
        {
            var width = GetWidth(branch);
            min = Math.Min(min, width.Min);
            max = CombineMax(max, width.Max, add: false);
        }

        return new PythonReWidth(min == int.MaxValue ? 0 : min, max);
    }

    private static PythonReWidth GetSequenceWidth(PythonReSequenceNode sequence)
    {
        var min = 0;
        int? max = 0;
        foreach (var element in sequence.Elements)
        {
            var width = GetWidth(element);
            min += width.Min;
            max = AddMax(max, width.Max);
        }

        return new PythonReWidth(min, max);
    }

    private static PythonReWidth GetQuantifierWidth(PythonReQuantifierNode quantifier)
    {
        var inner = GetWidth(quantifier.Inner);
        var min = inner.Min * quantifier.Min;
        int? max = null;
        if (quantifier.Max.HasValue && inner.Max.HasValue)
        {
            max = inner.Max.Value * quantifier.Max.Value;
        }

        return new PythonReWidth(min, max);
    }

    private static PythonReWidth GetConditionalWidth(PythonReConditionalNode conditional)
    {
        var yesWidth = GetWidth(conditional.YesBranch);
        if (conditional.NoBranch is null)
        {
            return new PythonReWidth(0, yesWidth.Max);
        }

        var noWidth = GetWidth(conditional.NoBranch);
        var min = Math.Min(yesWidth.Min, noWidth.Min);
        int? max;
        if (!yesWidth.Max.HasValue || !noWidth.Max.HasValue)
        {
            max = null;
        }
        else
        {
            max = Math.Max(yesWidth.Max.Value, noWidth.Max.Value);
        }

        return new PythonReWidth(min, max);
    }

    private static int? AddMax(int? left, int? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return null;
        }

        return left.Value + right.Value;
    }

    private static int? CombineMax(int? left, int? right, bool add)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return null;
        }

        return add ? left.Value + right.Value : Math.Max(left.Value, right.Value);
    }

    private static RegexOptions ToRegexOptions(PythonReCompileOptions options)
    {
        var translated = RegexOptions.CultureInvariant;
        if ((options & PythonReCompileOptions.IgnoreCase) != 0)
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

    private static bool IsUtf8IterationSafe(PythonReNode node)
    {
        return node switch
        {
            PythonReConditionalNode => false,
            PythonReAlternationNode alternation => IsPrefixFreeExactLiteralAlternation(alternation),
            PythonReSequenceNode sequence => sequence.Elements.All(IsUtf8IterationSafe),
            PythonReQuantifierNode quantifier => IsUtf8IterationSafe(quantifier.Inner),
            PythonReGroupNode group => IsUtf8IterationSafe(group.Inner),
            _ => true,
        };
    }

    private static bool ContainsConditional(PythonReNode node)
    {
        return node switch
        {
            PythonReConditionalNode => true,
            PythonReSequenceNode sequence => sequence.Elements.Any(ContainsConditional),
            PythonReAlternationNode alternation => alternation.Branches.Any(ContainsConditional),
            PythonReQuantifierNode quantifier => ContainsConditional(quantifier.Inner),
            PythonReGroupNode group => ContainsConditional(group.Inner),
            _ => false,
        };
    }

    private static bool IsZeroWidthEscape(string text)
    {
        return text is @"\A" or @"\Z" or @"\z" or @"\b" or @"\B" ||
            text == AsciiWordBoundary ||
            text == AsciiNonWordBoundary;
    }

    private static bool IsPrefixFreeExactLiteralAlternation(PythonReAlternationNode alternation)
    {
        List<string> literals = [];
        foreach (var branch in alternation.Branches)
        {
            if (!TryGetExactLiteral(branch, out var literal) || literal.Length == 0)
            {
                return false;
            }

            literals.Add(literal);
        }

        for (var i = 0; i < literals.Count; i++)
        {
            for (var j = 0; j < literals.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                if (literals[j].StartsWith(literals[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryGetExactLiteral(PythonReNode node, out string literal)
    {
        switch (node)
        {
            case PythonReLiteralNode value:
                literal = value.Value.ToString();
                return true;

            case PythonReSequenceNode sequence:
            {
                var builder = new StringBuilder();
                foreach (var element in sequence.Elements)
                {
                    if (!TryGetExactLiteral(element, out var segment))
                    {
                        literal = string.Empty;
                        return false;
                    }

                    builder.Append(segment);
                }

                literal = builder.ToString();
                return true;
            }

            case PythonReGroupNode { Kind: PythonReGroupKind.NonCapturing } group:
                return TryGetExactLiteral(group.Inner, out literal);

            default:
                literal = string.Empty;
                return false;
        }
    }

    private static void AppendCharacterClass(PythonReCharacterClassNode node, StringBuilder builder)
    {
        var positiveAtom = BuildPositiveCharacterClassAtom(node.Items);
        if (!node.IsNegated)
        {
            builder.Append(positiveAtom);
            return;
        }

        builder.Append("(?:(?!").Append(positiveAtom).Append(')').Append(AnyUnicodeScalarPattern).Append(')');
    }

    private static string BuildPositiveCharacterClassAtom(IReadOnlyList<PythonReCharacterClassItem> items)
    {
        var bmpRanges = new List<(int Start, int End)>();
        var bmpFragments = new List<string>();
        var astralBranches = new List<string>();

        foreach (var item in items)
        {
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
                        astralBranches.AddRange(BuildAstralRangeBranches(range.StartScalar, range.EndScalar));
                    }
                    else
                    {
                        AddBmpRange(bmpRanges, range.StartScalar, 0xFFFF);
                        astralBranches.AddRange(BuildAstralRangeBranches(0x10000, range.EndScalar));
                    }
                    break;

                case PythonReCharacterClassPredefinedItem predefined:
                    bmpFragments.Add(predefined.RegexClassFragment);
                    break;
            }
        }

        var branches = new List<string>();
        var bmpClass = BuildBmpCharacterClass(bmpRanges, bmpFragments);
        if (bmpClass.Length != 0)
        {
            branches.Add(bmpClass);
        }

        branches.AddRange(astralBranches);
        return branches.Count switch
        {
            0 => "(?!)",
            1 => branches[0],
            _ => "(?:" + string.Join("|", branches) + ")",
        };
    }

    private static string BuildBmpCharacterClass(List<(int Start, int End)> ranges, List<string> fragments)
    {
        if (ranges.Count == 0 && fragments.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append('[');
        foreach (var fragment in fragments)
        {
            builder.Append(fragment);
        }

        foreach (var (start, end) in MergeRanges(ranges))
        {
            AppendBmpRange(builder, start, end);
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static IEnumerable<(int Start, int End)> MergeRanges(List<(int Start, int End)> ranges)
    {
        if (ranges.Count == 0)
        {
            yield break;
        }

        ranges.Sort(static (x, y) => x.Start.CompareTo(y.Start));
        var currentStart = ranges[0].Start;
        var currentEnd = ranges[0].End;
        for (var i = 1; i < ranges.Count; i++)
        {
            var next = ranges[i];
            if (next.Start <= currentEnd + 1)
            {
                currentEnd = Math.Max(currentEnd, next.End);
                continue;
            }

            yield return (currentStart, currentEnd);
            currentStart = next.Start;
            currentEnd = next.End;
        }

        yield return (currentStart, currentEnd);
    }

    private static void AddBmpRange(List<(int Start, int End)> ranges, int start, int end)
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

    private static void AppendBmpRange(StringBuilder builder, int start, int end)
    {
        if (start == end)
        {
            AppendBmpClassChar(builder, start);
            return;
        }

        AppendBmpClassChar(builder, start);
        builder.Append('-');
        AppendBmpClassChar(builder, end);
    }

    private static void AppendBmpClassChar(StringBuilder builder, int scalar)
    {
        var ch = (char)scalar;
        if (ch is '\\' or '-' or ']' or '^')
        {
            builder.Append('\\');
            builder.Append(ch);
            return;
        }

        if (scalar < 0x20 || scalar > 0x7E)
        {
            builder.Append(@"\u").Append(scalar.ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        builder.Append(ch);
    }

    private static string BuildAstralScalarBranch(int scalar)
    {
        var text = char.ConvertFromUtf32(scalar);
        return Regex.Escape(text);
    }

    private static IEnumerable<string> BuildAstralRangeBranches(int startScalar, int endScalar)
    {
        var startText = char.ConvertFromUtf32(startScalar);
        var endText = char.ConvertFromUtf32(endScalar);
        var startHigh = startText[0];
        var startLow = startText[1];
        var endHigh = endText[0];
        var endLow = endText[1];

        for (var high = startHigh; high <= endHigh; high++)
        {
            var lowStart = high == startHigh ? startLow : '\uDC00';
            var lowEnd = high == endHigh ? endLow : '\uDFFF';
            yield return BuildSurrogateBranch((char)high, lowStart, lowEnd);
        }
    }

    private static string BuildSurrogateBranch(char high, char lowStart, char lowEnd)
    {
        var builder = new StringBuilder();
        builder.Append(@"\u").Append(((int)high).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        if (lowStart == lowEnd)
        {
            builder.Append(@"\u").Append(((int)lowStart).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        builder.Append('[');
        builder.Append(@"\u").Append(((int)lowStart).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append('-');
        builder.Append(@"\u").Append(((int)lowEnd).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(']');
        return builder.ToString();
    }
}

internal readonly record struct PythonReTranslation(
    string Pattern,
    RegexOptions RegexOptions,
    int CaptureGroupCount,
    IReadOnlyDictionary<string, string> EmittedGroupNames);
