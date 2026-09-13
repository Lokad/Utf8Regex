using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReTranslator
{
    private const string AsciiWordBoundary = @"(?:(?<![A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<=[A-Za-z0-9_])(?![A-Za-z0-9_]))";
    private const string AsciiNonWordBoundary = @"(?:(?<=[A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<![A-Za-z0-9_])(?![A-Za-z0-9_]))";
    // PLAN.md C04: Python-Unicode word boundaries over the same BCL-category
    // class used for \w. Must stay in IsZeroWidthEscape below.
    private const string UnicodeWordBoundary = @"(?:(?<![\p{L}\p{N}_])(?=[\p{L}\p{N}_])|(?<=[\p{L}\p{N}_])(?![\p{L}\p{N}_]))";
    private const string UnicodeNonWordBoundary = @"(?:(?<=[\p{L}\p{N}_])(?=[\p{L}\p{N}_])|(?<![\p{L}\p{N}_])(?![\p{L}\p{N}_]))";
    private const string AnyUnicodeScalarPattern = @"(?:[\u0000-\uD7FF\uE000-\uFFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF])";

    public static bool CanMatchEmpty(PythonReNode node, PythonReParseResult parseResult)
    {
        // Backreferences keep CPython getwidth content semantics (PLAN.md C10): a backreference to an unset group fails in both CPython 3.13.2 and .NET 10 (verified: `(a)?b\1` on "b" matches in neither), so in every successful match each executed backreference contributes at least its group's content minimum, while alternation minima and zeroable quantifiers already absorb non-executed positions. The structural minimum is therefore a lower bound over successful matches: zero keeps the general route (always safe) and a positive minimum proves no empty match exists.
        return GetWidth(node, parseResult).Min == 0;
    }

    public static bool CanMatchNonEmpty(PythonReNode node, PythonReParseResult parseResult)
    {
        var max = GetWidth(node, parseResult).Max;
        return !max.HasValue || max.Value > 0;
    }

    // Native FindAll over UTF-8 is admissible only when alternation order is
    // unobservable: prefix-free exact literals guarantee at most one branch
    // matches at any position, so a literal-family candidate scan cannot diverge
    // from ordered-alternation semantics. Conditionals stay managed. The facade
    // additionally requires no empty match. Pinned by
    // FindAllCanUseUtf8RegexBackendForPrefixFreeLiteralAlternation.
    public static bool CanUseUtf8IterationFastPath(PythonReNode node) => IsUtf8IterationSafe(node);

    // Managed Split is admissible only for structurally mandatory captures:
    // .NET reports an unmatched group as "" while Python reports None, so
    // optional or alternated captures take the manual loop. Unnamed-only keeps
    // managed numbers identical to Python slots (PLAN.md C07); the facade
    // additionally requires no empty match, or astral-free input at the call
    // site, because Regex.Split advances empty matches by UTF-16 unit while
    // Python advances by scalar. Pinned by
    // ManagedSplitFastPathRequiresMandatoryUnnamedCaptures.

    public static bool CanUseManagedSplitFastPath(PythonReNode node) => CapturesAreMandatory(node);

    public static bool CanUseManagedEmptyReplacementFastPath(PythonReNode node) =>
        node is PythonReQuantifierNode
        {
            Inner: var repeated,
            Min: 0,
            Max: null or > 0,
            Flavor: PythonReQuantifierFlavor.Greedy,
        } &&
        TryGetExactLiteral(repeated, out var literal) &&
        literal.Length > 0;

    public static bool TryGetAsciiGreedyStarFindAllByte(
        PythonReNode node,
        PythonReCompileOptions options,
        out byte repeatedByte)
    {
        if ((options & PythonReCompileOptions.IgnoreCase) == 0 &&
            node is PythonReQuantifierNode
            {
                Inner: var repeated,
                Min: 0,
                Max: null,
                Flavor: PythonReQuantifierFlavor.Greedy,
            } &&
            TryGetExactLiteral(repeated, out var literal) &&
            literal is [var character] &&
            character <= 0x7f)
        {
            repeatedByte = (byte)character;
            return true;
        }

        repeatedByte = default;
        return false;
    }

    public static bool IsExactLiteral(PythonReNode node) => TryGetExactLiteral(node, out _);

    public static bool TryGetCaseSensitiveExactLiteral(
        PythonReNode node,
        PythonReCompileOptions options,
        out string literal)
    {
        if ((options & PythonReCompileOptions.IgnoreCase) == 0 &&
            TryGetExactLiteral(node, out literal))
        {
            return true;
        }

        literal = string.Empty;
        return false;
    }

    public static bool TryGetSingleTrailingCapturePrefixLength(PythonReNode node, out int prefixLength)
    {
        if (node is PythonReGroupNode
            {
                Kind: PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing,
            })
        {
            prefixLength = 0;
            return true;
        }

        if (node is not PythonReSequenceNode { Elements.Count: > 0 } sequence ||
            sequence.Elements[^1] is not PythonReGroupNode
            {
                Kind: PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing,
            })
        {
            prefixLength = 0;
            return false;
        }

        var length = 0;
        for (var index = 0; index < sequence.Elements.Count - 1; index++)
        {
            if (!TryGetExactLiteral(sequence.Elements[index], out var segment))
            {
                prefixLength = 0;
                return false;
            }

            length = checked(length + segment.Length);
        }

        prefixLength = length;
        return true;
    }

    public static bool TryGetOptionalExactCaptureFindAllPlan(
        PythonReNode node,
        PythonReCompileOptions options,
        out string captureValue,
        out int presentMatchLength,
        out int absentMatchLength)
    {
        captureValue = string.Empty;
        presentMatchLength = 0;
        absentMatchLength = 0;
        if ((options & PythonReCompileOptions.IgnoreCase) != 0 ||
            node is not PythonReSequenceNode sequence)
        {
            return false;
        }

        var optionalCaptureIndex = -1;
        for (var index = 0; index < sequence.Elements.Count; index++)
        {
            if (sequence.Elements[index] is PythonReQuantifierNode
                {
                    Inner: PythonReGroupNode
                    {
                        Kind: PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing,
                        Inner: var capture,
                    },
                    Min: 0,
                    Max: 1,
                    Flavor: PythonReQuantifierFlavor.Greedy,
                } &&
                TryGetExactLiteral(capture, out var literal) &&
                literal.Length > 0)
            {
                if (optionalCaptureIndex >= 0)
                {
                    return false;
                }

                optionalCaptureIndex = index;
                captureValue = literal;
            }
        }

        if (optionalCaptureIndex < 0)
        {
            return false;
        }

        var fixedLength = 0;
        for (var index = 0; index < sequence.Elements.Count; index++)
        {
            if (index == optionalCaptureIndex)
            {
                continue;
            }

            if (!TryGetExactLiteral(sequence.Elements[index], out var segment))
            {
                return false;
            }

            fixedLength = checked(fixedLength + segment.Length);
        }

        if (fixedLength == 0)
        {
            return false;
        }

        absentMatchLength = fixedLength;
        presentMatchLength = checked(fixedLength + captureValue.Length);
        return true;
    }

    public static bool TryGetSeparatedCaptureTupleSeparator(PythonReNode node, out char separator)
    {
        if (node is PythonReSequenceNode
            {
                Elements.Count: 3,
                Elements:
                [
                    PythonReGroupNode
                    {
                        Kind: PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing,
                        Inner: var first,
                    },
                    var separatorNode,
                    PythonReGroupNode
                    {
                        Kind: PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing,
                        Inner: var second,
                    },
                ],
            } &&
            TryGetExactLiteral(separatorNode, out var literal) &&
            literal is [var candidate] &&
            IsAsciiPunctuation(candidate) &&
            IsNonEmptyAsciiWordClassRepeat(first) &&
            IsNonEmptyAsciiWordClassRepeat(second))
        {
            separator = candidate;
            return true;
        }

        separator = default;
        return false;
    }

    public static bool TryGetAsciiLiteralPrefixDigitRepeat(
        PythonReNode node,
        PythonReCompileOptions options,
        out byte[] prefixUtf8)
    {
        prefixUtf8 = [];
        if ((options & PythonReCompileOptions.IgnoreCase) != 0 ||
            node is not PythonReSequenceNode { Elements.Count: > 1 } sequence ||
            sequence.Elements[^1] is not PythonReQuantifierNode
            {
                Inner: PythonReCharacterClassNode
                {
                    IsNegated: false,
                    Items: [PythonReCharacterClassRangeItem { StartScalar: '0', EndScalar: '9' }],
                },
                Min: 1,
                Max: null,
                Flavor: PythonReQuantifierFlavor.Greedy,
            })
        {
            return false;
        }

        var literal = new StringBuilder();
        for (var index = 0; index < sequence.Elements.Count - 1; index++)
        {
            if (!TryGetExactLiteral(sequence.Elements[index], out var segment) ||
                segment.Any(static character => character > 0x7f))
            {
                return false;
            }

            literal.Append(segment);
        }

        if (literal.Length == 0)
        {
            return false;
        }

        prefixUtf8 = Encoding.ASCII.GetBytes(literal.ToString());
        return true;
    }

    public static bool TryGetAsciiDotAllFullMatchPlan(
        PythonReNode node,
        PythonReCompileOptions options,
        out byte[] prefixUtf8,
        out byte[] suffixUtf8)
    {
        prefixUtf8 = [];
        suffixUtf8 = [];
        if ((options & PythonReCompileOptions.DotAll) == 0 ||
            (options & PythonReCompileOptions.IgnoreCase) != 0 ||
            node is not PythonReSequenceNode sequence)
        {
            return false;
        }

        var wildcardIndex = -1;
        for (var index = 0; index < sequence.Elements.Count; index++)
        {
            if (sequence.Elements[index] is PythonReQuantifierNode
                {
                    Inner: PythonReRawNode { Kind: PythonReRawKind.Dot },
                    Min: 0,
                    Max: null,
                    Flavor: PythonReQuantifierFlavor.Greedy,
                })
            {
                if (wildcardIndex >= 0)
                {
                    return false;
                }

                wildcardIndex = index;
            }
        }

        if (wildcardIndex <= 0 || wildcardIndex >= sequence.Elements.Count - 1)
        {
            return false;
        }

        var prefix = new StringBuilder();
        var suffix = new StringBuilder();
        for (var index = 0; index < sequence.Elements.Count; index++)
        {
            if (index == wildcardIndex)
            {
                continue;
            }

            if (!TryGetExactLiteral(sequence.Elements[index], out var segment) ||
                segment.Any(static character => character > 0x7f))
            {
                return false;
            }

            (index < wildcardIndex ? prefix : suffix).Append(segment);
        }

        if (prefix.Length == 0 || suffix.Length == 0)
        {
            return false;
        }

        prefixUtf8 = Encoding.ASCII.GetBytes(prefix.ToString());
        suffixUtf8 = Encoding.ASCII.GetBytes(suffix.ToString());
        return true;
    }

    public static PythonReTranslation Translate(PythonReParseResult parseResult)
    {
        ValidateReferences(parseResult.Root, parseResult.CaptureGroupCount, parseResult.NamedGroups);
        // PLAN.md C10: lookbehind width is validated up front over the whole tree (fixed content widths, backreferences resolved to their group widths) so emission never meets an invalid lookbehind.
        ValidateLookbehindWidths(parseResult.Root, parseResult);

        static void ValidateLookbehindWidths(PythonReNode node, PythonReParseResult result)
        {
            switch (node)
            {
                case PythonReGroupNode { Kind: PythonReGroupKind.PositiveLookbehind, Inner: var positiveInner }:
                    EnsureFixedWidth(positiveInner, result);
                    ValidateLookbehindWidths(positiveInner, result);
                    break;
                case PythonReGroupNode { Kind: PythonReGroupKind.NegativeLookbehind, Inner: var negativeInner }:
                    EnsureFixedWidth(negativeInner, result);
                    ValidateLookbehindWidths(negativeInner, result);
                    break;
                case PythonReAlternationNode alternation:
                    foreach (var branch in alternation.Branches)
                    {
                        ValidateLookbehindWidths(branch, result);
                    }

                    break;
                case PythonReSequenceNode sequence:
                    foreach (var element in sequence.Elements)
                    {
                        ValidateLookbehindWidths(element, result);
                    }

                    break;
                case PythonReQuantifierNode quantifier:
                    ValidateLookbehindWidths(quantifier.Inner, result);
                    break;
                case PythonReGroupNode group:
                    ValidateLookbehindWidths(group.Inner, result);
                    break;
                case PythonReConditionalNode conditional:
                    ValidateLookbehindWidths(conditional.YesBranch, result);
                    if (conditional.NoBranch is not null)
                    {
                        ValidateLookbehindWidths(conditional.NoBranch, result);
                    }

                    break;
                default:
                    break;
            }
        }

        var emittedGroupNames = BuildEmittedGroupNames(parseResult.NamedGroups);
        var builder = new StringBuilder();
        // PLAN.md C07: .NET numbers unnamed groups first (opening-paren order), then named groups (opening-paren order), while Python numbers strictly by opening paren. Precompute the Python-slot to managed-number permutation once; the facade proves the same rule via GroupNumberFromName at runtime.
        var managedNumbersByPython = new int[parseResult.CaptureGroupCount + 1];
        var namedPythonGroups = new HashSet<int>(parseResult.NamedGroups.Values);
        var nextManagedGroup = 1;
        for (var pythonGroup = 1; pythonGroup <= parseResult.CaptureGroupCount; pythonGroup++)
        {
            if (!namedPythonGroups.Contains(pythonGroup))
            {
                managedNumbersByPython[pythonGroup] = nextManagedGroup++;
            }
        }

        for (var pythonGroup = 1; pythonGroup <= parseResult.CaptureGroupCount; pythonGroup++)
        {
            if (namedPythonGroups.Contains(pythonGroup))
            {
                managedNumbersByPython[pythonGroup] = nextManagedGroup++;
            }
        }

        AppendNode(parseResult.Root, builder, emittedGroupNames, managedNumbersByPython);
        var translatedPattern = builder.ToString();
        if ((parseResult.Options & PythonReCompileOptions.Verbose) == 0)
        {
            // Regex.Escape protects spaces for IgnorePatternWhitespace. Outside
            // verbose mode a raw space is equivalent and keeps exact literals
            // visible to the UTF-8 planner instead of forcing fallback.
            translatedPattern = translatedPattern.Replace(@"\ ", " ", StringComparison.Ordinal);
        }

        return new PythonReTranslation(
            translatedPattern,
            ToRegexOptions(parseResult.Options),
            parseResult.CaptureGroupCount,
            emittedGroupNames);
    }

    private static bool CapturesAreMandatory(PythonReNode node)
    {
        switch (node)
        {
            case PythonReAlternationNode alternation:
                return !ContainsCapture(alternation);
            case PythonReSequenceNode sequence:
                return sequence.Elements.All(CapturesAreMandatory);
            case PythonReQuantifierNode quantifier:
                return (quantifier.Min > 0 || !ContainsCapture(quantifier.Inner)) &&
                    CapturesAreMandatory(quantifier.Inner);
            case PythonReGroupNode group when group.Kind is
                PythonReGroupKind.Capturing or
                PythonReGroupKind.NamedCapturing or
                PythonReGroupKind.NonCapturing or
                PythonReGroupKind.Atomic or
                PythonReGroupKind.ScopedFlags:
                return CapturesAreMandatory(group.Inner);
            case PythonReGroupNode group:
                return !ContainsCapture(group.Inner);
            case PythonReConditionalNode conditional:
                return !ContainsCapture(conditional);
            default:
                return true;
        }
    }

    private static bool ContainsCapture(PythonReNode node)
    {
        switch (node)
        {
            case PythonReAlternationNode alternation:
                return alternation.Branches.Any(ContainsCapture);
            case PythonReSequenceNode sequence:
                return sequence.Elements.Any(ContainsCapture);
            case PythonReQuantifierNode quantifier:
                return ContainsCapture(quantifier.Inner);
            case PythonReGroupNode group when group.Kind is
                PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing:
                return true;
            case PythonReGroupNode group:
                return ContainsCapture(group.Inner);
            case PythonReConditionalNode conditional:
                return ContainsCapture(conditional.YesBranch) ||
                    conditional.NoBranch is not null && ContainsCapture(conditional.NoBranch);
            default:
                return false;
        }
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
                if (reference.GroupName is not string groupName)
                {
                    throw new PythonRePatternException("Named conditional reference has no group name.");
                }

                if (!namedGroups.ContainsKey(groupName))
                {
                    throw new PythonRePatternException($"unknown group name '{reference.GroupName}'");
                }

                break;
        }
    }

    private static void AppendNode(PythonReNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython)
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

                    AppendNode(alternation.Branches[i], builder, emittedGroupNames, managedNumbersByPython);
                }
                break;

            case PythonReSequenceNode sequence:
                foreach (var element in sequence.Elements)
                {
                    AppendNode(element, builder, emittedGroupNames, managedNumbersByPython);
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
                // PLAN.md C07: numeric references name Python slots; emit the managed number.
                builder.Append('\\');
                builder.Append(managedNumbersByPython[numericBackreference.GroupNumber]);
                break;

            case PythonReConditionalNode conditional:
                AppendConditional(conditional, builder, emittedGroupNames, managedNumbersByPython);
                break;

            case PythonReQuantifierNode quantifier:
                AppendQuantifier(quantifier, builder, emittedGroupNames, managedNumbersByPython);
                break;

            case PythonReGroupNode group:
                AppendGroup(group, builder, emittedGroupNames, managedNumbersByPython);
                break;

            default:
                throw new PythonRePatternException("Unsupported AST node.");
        }
    }

    private static void AppendQuantifier(PythonReQuantifierNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython)
    {
        if (node.Flavor == PythonReQuantifierFlavor.Possessive)
        {
            builder.Append("(?>");
            AppendQuantifierCore(node, builder, PythonReQuantifierFlavor.Greedy, emittedGroupNames, managedNumbersByPython);
            builder.Append(')');
            return;
        }

        AppendQuantifierCore(node, builder, node.Flavor, emittedGroupNames, managedNumbersByPython);
    }

    private static void AppendQuantifierCore(PythonReQuantifierNode node, StringBuilder builder, PythonReQuantifierFlavor flavor, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython)
    {
        AppendInnerAsAtom(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
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

    private static void AppendGroup(PythonReGroupNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython)
    {
        switch (node.Kind)
        {
            case PythonReGroupKind.Capturing:
                builder.Append('(');
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
                builder.Append(')');
                return;

            case PythonReGroupKind.NamedCapturing:
                if (node.Name is not string groupName)
                {
                    throw new PythonRePatternException("Named capturing group has no group name.");
                }

                builder.Append("(?<").Append(emittedGroupNames[groupName]).Append('>');
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
                builder.Append(')');
                return;

            case PythonReGroupKind.NonCapturing:
                builder.Append("(?:");
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
                builder.Append(')');
                return;

            case PythonReGroupKind.PositiveLookahead:
                builder.Append("(?=");
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
                builder.Append(')');
                return;

            case PythonReGroupKind.NegativeLookahead:
                builder.Append("(?!");
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
                builder.Append(')');
                return;

            case PythonReGroupKind.PositiveLookbehind:
                builder.Append("(?<=");
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
                builder.Append(')');
                return;

            case PythonReGroupKind.NegativeLookbehind:
                builder.Append("(?<!");
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
                builder.Append(')');
                return;

            case PythonReGroupKind.Atomic:
                builder.Append("(?>");
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
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
                AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython);
                builder.Append(')');
                return;

            default:
                throw new PythonRePatternException("Unsupported group kind.");
        }
    }

    private static void AppendConditional(PythonReConditionalNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython)
    {
        builder.Append("(?(");
        switch (node.Reference.Kind)
        {
            case PythonReConditionalReferenceKind.GroupNumber:
                builder.Append(managedNumbersByPython[node.Reference.GroupNumber]);
                break;
            case PythonReConditionalReferenceKind.GroupName:
                if (node.Reference.GroupName is not string groupName)
                {
                    throw new PythonRePatternException("Named conditional reference has no group name.");
                }

                builder.Append(emittedGroupNames[groupName]);
                break;
            default:
                throw new PythonRePatternException("Unsupported conditional reference.");
        }

        builder.Append(')');
        AppendNode(node.YesBranch, builder, emittedGroupNames, managedNumbersByPython);
        if (node.NoBranch is not null)
        {
            builder.Append('|');
            AppendNode(node.NoBranch, builder, emittedGroupNames, managedNumbersByPython);
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

    private static void AppendInnerAsAtom(PythonReNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython)
    {
        switch (node)
        {
            case PythonReLiteralNode:
            // PLAN.md C06: an escaped astral pair is two UTF-16 units, so it takes the grouped default while every other raw atom stays bare.
            case PythonReRawNode raw when raw.Kind != PythonReRawKind.AstralLiteral:
            case PythonReNamedBackreferenceNode:
            case PythonReNumericBackreferenceNode:
            case PythonReGroupNode:
                AppendNode(node, builder, emittedGroupNames, managedNumbersByPython);
                break;

            default:
                builder.Append("(?:");
                AppendNode(node, builder, emittedGroupNames, managedNumbersByPython);
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

    private static void EnsureFixedWidth(PythonReNode node, PythonReParseResult parseResult)
    {
        var width = GetWidth(node, parseResult);
        if (!width.IsFixed)
        {
            throw new PythonRePatternException("look-behind requires fixed-width pattern");
        }
    }

    private static PythonReWidth GetWidth(PythonReNode node, PythonReParseResult parseResult)
    {
        return node switch
        {
            PythonReAlternationNode alternation => GetAlternationWidth(alternation, parseResult),
            PythonReSequenceNode sequence => GetSequenceWidth(sequence, parseResult),
            PythonReLiteralNode => new PythonReWidth(1, 1),
            PythonReCharacterClassNode => new PythonReWidth(1, 1),
            PythonReRawNode raw => raw.Kind switch
            {
                PythonReRawKind.StartAnchor or PythonReRawKind.EndAnchor => new PythonReWidth(0, 0),
                PythonReRawKind.Escape when IsZeroWidthEscape(raw.Text) => new PythonReWidth(0, 0),
                PythonReRawKind.AstralLiteral => new PythonReWidth(1, 1),
                _ => new PythonReWidth(1, 1),
            },
            PythonReNamedBackreferenceNode named => parseResult.NamedGroups.TryGetValue(named.Name, out var namedSlot) && parseResult.GroupBodies.TryGetValue(namedSlot, out var namedBody)
                ? GetWidth(namedBody, parseResult)
                : new PythonReWidth(0, null),
            PythonReNumericBackreferenceNode numeric => parseResult.GroupBodies.TryGetValue(numeric.GroupNumber, out var numberedBody)
                ? GetWidth(numberedBody, parseResult)
                : new PythonReWidth(0, null),
            PythonReConditionalNode conditional => GetConditionalWidth(conditional, parseResult),
            PythonReQuantifierNode quantifier => GetQuantifierWidth(quantifier, parseResult),
            PythonReGroupNode group => group.Kind switch
            {
                PythonReGroupKind.PositiveLookahead or
                PythonReGroupKind.NegativeLookahead or
                PythonReGroupKind.PositiveLookbehind or
                PythonReGroupKind.NegativeLookbehind => new PythonReWidth(0, 0),
                _ => GetWidth(group.Inner, parseResult),
            },
            _ => throw new PythonRePatternException("Unsupported AST node."),
        };
    }

    private static PythonReWidth GetAlternationWidth(PythonReAlternationNode alternation, PythonReParseResult parseResult)
    {
        var min = int.MaxValue;
        int? max = 0;
        foreach (var branch in alternation.Branches)
        {
            var width = GetWidth(branch, parseResult);
            min = Math.Min(min, width.Min);
            max = CombineMax(max, width.Max, add: false);
        }

        return new PythonReWidth(min == int.MaxValue ? 0 : min, max);
    }

    private static PythonReWidth GetSequenceWidth(PythonReSequenceNode sequence, PythonReParseResult parseResult)
    {
        var min = 0;
        int? max = 0;
        foreach (var element in sequence.Elements)
        {
            var width = GetWidth(element, parseResult);
            // Widths saturate like CPython MAXWIDTH instead of wrapping (PLAN.md C10): verdicts stay exact wherever exact bounds fit int32, i.e. every feasible pattern.
            min = (int)Math.Min((long)min + width.Min, int.MaxValue);
            max = AddMax(max, width.Max);
        }

        return new PythonReWidth(min, max);
    }

    private static PythonReWidth GetQuantifierWidth(PythonReQuantifierNode quantifier, PythonReParseResult parseResult)
    {
        var inner = GetWidth(quantifier.Inner, parseResult);
        var min = (int)Math.Min((long)inner.Min * quantifier.Min, int.MaxValue);
        int? max = null;
        if (quantifier.Max.HasValue && inner.Max.HasValue)
        {
            max = (int)Math.Min((long)inner.Max.Value * quantifier.Max.Value, int.MaxValue);
        }

        return new PythonReWidth(min, max);
    }

    private static PythonReWidth GetConditionalWidth(PythonReConditionalNode conditional, PythonReParseResult parseResult)
    {
        var yesWidth = GetWidth(conditional.YesBranch, parseResult);
        if (conditional.NoBranch is null)
        {
            return new PythonReWidth(0, yesWidth.Max);
        }

        var noWidth = GetWidth(conditional.NoBranch, parseResult);
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

        var sum = (long)left.Value + right.Value;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }

    private static int? CombineMax(int? left, int? right, bool add)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return null;
        }

        if (!add)
        {
            return Math.Max(left.Value, right.Value);
        }

        var sum = (long)left.Value + right.Value;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }

    private static RegexOptions ToRegexOptions(PythonReCompileOptions options)
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

    private static bool IsZeroWidthEscape(string text)
    {
        return text is @"\A" or @"\Z" or @"\z" or @"\b" or @"\B" ||
            text == AsciiWordBoundary ||
            text == AsciiNonWordBoundary ||
            text == UnicodeWordBoundary ||
            text == UnicodeNonWordBoundary;
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

    // The tuple splitter divides each managed match at the first ASCII separator, so a capture qualifies when none of its members can equal that separator. PLAN.md C05 IgnoreCase expansion adds non-ASCII fold partners (U+0130, `_EXTRA_CASES` members) to ASCII classes; those can never equal ASCII punctuation, and capture text still matches through the fully expanded managed regex. Only ASCII members need the word check, and ranges are scanned over their ASCII prefix alone so admission stays bounded for astral runs.
    private static bool IsNonEmptyAsciiWordClassRepeat(PythonReNode node)
    {
        if (node is not PythonReQuantifierNode
            {
                Inner: PythonReCharacterClassNode { IsNegated: false } characterClass,
                Min: > 0,
            })
        {
            return false;
        }

        foreach (var item in characterClass.Items)
        {
            switch (item)
            {
                case PythonReCharacterClassLiteralItem literal when IsAsciiWordCharacter(literal.Scalar) || literal.Scalar > 0x7F:
                    break;

                case PythonReCharacterClassRangeItem range
                    when range.StartScalar <= range.EndScalar &&
                         range.StartScalar >= 0:
                    for (var scalar = range.StartScalar; scalar <= Math.Min(range.EndScalar, 0x7F); scalar++)
                    {
                        if (!IsAsciiWordCharacter(scalar))
                        {
                            return false;
                        }
                    }

                    break;

                default:
                    return false;
            }
        }

        return characterClass.Items.Count > 0;
    }

    private static bool IsAsciiPunctuation(char value) =>
        value is >= '\u0021' and <= '\u007E' && !IsAsciiWordCharacter(value);

    private static bool IsAsciiWordCharacter(int value) =>
        value is >= '0' and <= '9' or >= 'A' and <= 'Z' or '_' or >= 'a' and <= 'z';

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
        var standaloneAtoms = new List<string>();
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
                    if (predefined.IsStandaloneAtom)
                    {
                        // Complete class atoms (PLAN.md C04): emitted as their own
                        // alternation branch so astral scalars keep matching instead
                        // of being dropped by the BMP-only class below.
                        standaloneAtoms.Add(predefined.RegexClassFragment);
                    }
                    else
                    {
                        bmpFragments.Add(predefined.RegexClassFragment);
                    }

                    break;
            }
        }

        var branches = new List<string>();
        var bmpClass = BuildBmpCharacterClass(bmpRanges, bmpFragments);
        if (bmpClass.Length != 0)
        {
            branches.Add(bmpClass);
        }

        branches.AddRange(standaloneAtoms);
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
        // PLAN.md C06: self-grouped so a repeat binds the whole scalar even when this is the only alternation branch (a bare pair would rebind the low half).
        return "(?:" + Regex.Escape(text) + ")";
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
            // Self-grouped per BuildAstralScalarBranch: a low-half unit must not capture the repeat.
            return "(?:" + builder.ToString() + ")";
        }

        builder.Append('[');
        builder.Append(@"\u").Append(((int)lowStart).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append('-');
        builder.Append(@"\u").Append(((int)lowEnd).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(']');
        // Self-grouped per BuildAstralScalarBranch.
        return "(?:" + builder.ToString() + ")";
    }
}

internal readonly record struct PythonReTranslation(
    string Pattern,
    RegexOptions RegexOptions,
    int CaptureGroupCount,
    IReadOnlyDictionary<string, string> EmittedGroupNames);
