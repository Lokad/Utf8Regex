using System.Text;
using System.Runtime.InteropServices;
using Lokad.Utf8Regex.Internal.FrontEnd;
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
    private const string AnyUnicodeScalarPattern = PythonReParser.ManagedUnicodeScalarPattern;
    private const string NativeScalarPattern = "(?s:.)";

    public static bool CanMatchEmpty(PythonReNode node, PythonReParseResult parseResult)
    {
        // Backreferences keep CPython getwidth content semantics (PLAN.md C10): a backreference to an unset group fails in both CPython 3.13.2 and .NET 10 (verified: `(a)?b\1` on "b" matches in neither), so in every successful match each executed backreference contributes at least its group's content minimum, while alternation minima and zeroable quantifiers already absorb non-executed positions. The structural minimum is therefore a lower bound over successful matches: zero keeps the general route (always safe) and a positive minimum proves no empty match exists.
        return GetWidth(node, parseResult).Min == 0;
    }

    public static bool CanMatchNonEmpty(PythonReNode node, PythonReParseResult parseResult)
    {
        var max = GetWidth(node, parseResult).Max;
        return max > 0;
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
            Utf8ConstructionScope.ChargeWork(1);
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
            Utf8ConstructionScope.ChargeWork(1);
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
            Utf8ConstructionScope.ChargeWork(1);
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
            Utf8ConstructionScope.ChargeWork(1);
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
            Utf8ConstructionScope.ChargeWork(1);
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
            Utf8ConstructionScope.ChargeWork(1);
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
        using var construction = Utf8ConstructionScope.Enter();
        using var widths = new Utf8ConstructionMap<PythonReNode, PythonReWidth>(construction.Budget, ReferenceEqualityComparer.Instance);
        Utf8PreparationStep.TryRun(ValidateReferences(parseResult.Root, parseResult.CaptureGroupCount, parseResult.NamedGroups), construction.Budget);
        // PLAN.md C10: lookbehind width is validated up front over the whole tree (fixed content widths, backreferences resolved to their group widths) so emission never meets an invalid lookbehind.
        Utf8PreparationStep.TryRun(ValidateLookbehindWidths(parseResult.Root, parseResult), construction.Budget);

        IEnumerable<Utf8PreparationStep> ValidateLookbehindWidths(PythonReNode node, PythonReParseResult result)
        {
            switch (node)
            {
                case PythonReGroupNode { Kind: PythonReGroupKind.PositiveLookbehind, Inner: var positiveInner }:
                    EnsureFixedWidth(positiveInner, result, widths);
                    yield return new Utf8PreparationStep(ValidateLookbehindWidths(positiveInner, result), false);
                    break;
                case PythonReGroupNode { Kind: PythonReGroupKind.NegativeLookbehind, Inner: var negativeInner }:
                    EnsureFixedWidth(negativeInner, result, widths);
                    yield return new Utf8PreparationStep(ValidateLookbehindWidths(negativeInner, result), false);
                    break;
                case PythonReAlternationNode alternation:
                    foreach (var branch in alternation.Branches)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        yield return new Utf8PreparationStep(ValidateLookbehindWidths(branch, result), false);
                    }

                    break;
                case PythonReSequenceNode sequence:
                    foreach (var element in sequence.Elements)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        yield return new Utf8PreparationStep(ValidateLookbehindWidths(element, result), false);
                    }

                    break;
                case PythonReQuantifierNode quantifier:
                    yield return new Utf8PreparationStep(ValidateLookbehindWidths(quantifier.Inner, result), false);
                    break;
                case PythonReGroupNode group:
                    yield return new Utf8PreparationStep(ValidateLookbehindWidths(group.Inner, result), false);
                    break;
                case PythonReConditionalNode conditional:
                    yield return new Utf8PreparationStep(ValidateLookbehindWidths(conditional.YesBranch, result), false);
                    if (conditional.NoBranch is not null)
                    {
                        yield return new Utf8PreparationStep(ValidateLookbehindWidths(conditional.NoBranch, result), false);
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
            Utf8ConstructionScope.ChargeWork(1);
            if (!namedPythonGroups.Contains(pythonGroup))
            {
                managedNumbersByPython[pythonGroup] = nextManagedGroup++;
            }
        }

        for (var pythonGroup = 1; pythonGroup <= parseResult.CaptureGroupCount; pythonGroup++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (namedPythonGroups.Contains(pythonGroup))
            {
                managedNumbersByPython[pythonGroup] = nextManagedGroup++;
            }
        }

        Utf8PreparationStep.TryRun(AppendNode(parseResult.Root, builder, emittedGroupNames, managedNumbersByPython, false), construction.Budget);
        var translatedPattern = builder.ToString();
        builder.Clear();
        Utf8PreparationStep.TryRun(AppendNode(parseResult.Root, builder, emittedGroupNames, managedNumbersByPython, true), construction.Budget);
        var nativePattern = builder.ToString();
        if ((parseResult.Options & PythonReCompileOptions.Verbose) == 0)
        {
            // Regex.Escape protects spaces for IgnorePatternWhitespace. Outside
            // verbose mode a raw space is equivalent and keeps exact literals
            // visible to the UTF-8 planner instead of forcing fallback.
            translatedPattern = translatedPattern.Replace(@"\ ", " ", StringComparison.Ordinal);
            nativePattern = nativePattern.Replace(@"\ ", " ", StringComparison.Ordinal);
        }

        construction.Complete();
        return new PythonReTranslation(
            translatedPattern,
            nativePattern,
            ToRegexOptions(parseResult.Options),
            ToRegexOptions(parseResult.Options) & ~RegexOptions.IgnoreCase,
            parseResult.CaptureGroupCount,
            emittedGroupNames,
            managedNumbersByPython);
    }


    private static bool CapturesAreMandatory(PythonReNode node)
    {
        return Utf8PreparationStep.TryRun(Check(node, false));

        static IEnumerable<Utf8PreparationStep> Check(PythonReNode current, bool forbidCapture)
        {
            switch (current)
            {
                case PythonReGroupNode group:
                    if (forbidCapture && group.Kind is PythonReGroupKind.Capturing or PythonReGroupKind.NamedCapturing)
                    {
                        yield return Utf8PreparationStep.Decline;
                        yield break;
                    }
                    var assertion = group.Kind is PythonReGroupKind.PositiveLookahead or PythonReGroupKind.NegativeLookahead
                        or PythonReGroupKind.PositiveLookbehind or PythonReGroupKind.NegativeLookbehind;
                    yield return new Utf8PreparationStep(Check(group.Inner, forbidCapture || assertion), false);
                    break;
                case PythonReQuantifierNode repeat:
                    yield return new Utf8PreparationStep(Check(repeat.Inner, forbidCapture || repeat.Min == 0), false);
                    break;
                case PythonReSequenceNode sequence:
                    foreach (var element in sequence.Elements)
                        {
                        Utf8ConstructionScope.ChargeWork(1);
                        yield return new Utf8PreparationStep(Check(element, forbidCapture), false);
                    }
                    break;
                case PythonReAlternationNode alternation:
                    foreach (var branch in alternation.Branches)
                        {
                        Utf8ConstructionScope.ChargeWork(1);
                        yield return new Utf8PreparationStep(Check(branch, true), false);
                    }
                    break;
                case PythonReConditionalNode conditional:
                    yield return new Utf8PreparationStep(Check(conditional.YesBranch, true), false);
                    if (conditional.NoBranch is PythonReNode no)
                        yield return new Utf8PreparationStep(Check(no, true), false);
                    break;
            }
        }
    }

    private static IEnumerable<Utf8PreparationStep> ValidateReferences(
        PythonReNode node,
        int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups)
    {
        switch (node)
        {
            case PythonReAlternationNode alternation:
                foreach (var branch in alternation.Branches)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    yield return new Utf8PreparationStep(ValidateReferences(branch, captureGroupCount, namedGroups), false);
                }

                break;

            case PythonReSequenceNode sequence:
                foreach (var element in sequence.Elements)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    yield return new Utf8PreparationStep(ValidateReferences(element, captureGroupCount, namedGroups), false);
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
                yield return new Utf8PreparationStep(ValidateReferences(conditional.YesBranch, captureGroupCount, namedGroups), false);
                if (conditional.NoBranch is not null)
                {
                    yield return new Utf8PreparationStep(ValidateReferences(conditional.NoBranch, captureGroupCount, namedGroups), false);
                }

                break;

            case PythonReQuantifierNode quantifier:
                yield return new Utf8PreparationStep(ValidateReferences(quantifier.Inner, captureGroupCount, namedGroups), false);
                break;

            case PythonReGroupNode group:
                yield return new Utf8PreparationStep(ValidateReferences(group.Inner, captureGroupCount, namedGroups), false);
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

    internal static string RenderNativeAtom(PythonReNode node)
    {
        var builder = new StringBuilder();
        Utf8PreparationStep.TryRun(AppendNode(node, builder, new Dictionary<string, string>(), [], true));
        return builder.ToString();
    }

    private static IEnumerable<Utf8PreparationStep> AppendNode(PythonReNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython, bool native)
    {
        switch (node)
        {
            case PythonReAlternationNode alternation:
                for (var i = 0; i < alternation.Branches.Count; i++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    if (i != 0)
                    {
                        builder.Append('|');
                    }

                    yield return new Utf8PreparationStep(AppendNode(alternation.Branches[i], builder, emittedGroupNames, managedNumbersByPython, native), false);
                }
                break;

            case PythonReSequenceNode sequence:
                foreach (var element in sequence.Elements)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    yield return new Utf8PreparationStep(AppendNode(element, builder, emittedGroupNames, managedNumbersByPython, native), false);
                }
                break;

            case PythonReLiteralNode literal:
                // Python's separate \uD800/\uDC00 atoms cannot combine into a
                // scalar merely because the core accepts adjacent pair spellings.
                builder.Append(native && char.IsSurrogate(literal.Value)
                    ? "(?!)" : Regex.Escape(literal.Value.ToString()));
                break;

            case PythonReCharacterClassNode characterClass:
                AppendCharacterClass(characterClass, builder, native);
                break;

            case PythonReRawNode raw:
                Utf8ConstructionScope.ChargeWork((ulong)raw.Text.Length);
                builder.Append(native
                    ? raw.Kind == PythonReRawKind.Dot
                        ? raw.Text == AnyUnicodeScalarPattern ? NativeScalarPattern : "(?-s:.)"
                        : RenderScalarFragment(raw.Text)
                    : raw.Text);
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
                yield return new Utf8PreparationStep(AppendConditional(conditional, builder, emittedGroupNames, managedNumbersByPython, native), false);
                break;

            case PythonReQuantifierNode quantifier:
                yield return new Utf8PreparationStep(AppendQuantifier(quantifier, builder, emittedGroupNames, managedNumbersByPython, native), false);
                break;

            case PythonReGroupNode group:
                yield return new Utf8PreparationStep(AppendGroup(group, builder, emittedGroupNames, managedNumbersByPython, native), false);
                break;

            default:
                throw new PythonRePatternException("Unsupported AST node.");
        }
    }

    private static IEnumerable<Utf8PreparationStep> AppendQuantifier(PythonReQuantifierNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython, bool native)
    {
        if (node.Flavor == PythonReQuantifierFlavor.Possessive)
        {
            builder.Append("(?>");
            yield return new Utf8PreparationStep(AppendQuantifierCore(node, builder, PythonReQuantifierFlavor.Greedy, emittedGroupNames, managedNumbersByPython, native), false);
            builder.Append(')');
            yield break;
        }

        yield return new Utf8PreparationStep(AppendQuantifierCore(node, builder, node.Flavor, emittedGroupNames, managedNumbersByPython, native), false);
    }

    private static IEnumerable<Utf8PreparationStep> AppendQuantifierCore(PythonReQuantifierNode node, StringBuilder builder, PythonReQuantifierFlavor flavor, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython, bool native)
    {
        yield return new Utf8PreparationStep(AppendInnerAsAtom(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
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

    private static IEnumerable<Utf8PreparationStep> AppendGroup(PythonReGroupNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython, bool native)
    {
        switch (node.Kind)
        {
            case PythonReGroupKind.Capturing:
                builder.Append('(');
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            case PythonReGroupKind.NamedCapturing:
                if (node.Name is not string groupName)
                {
                    throw new PythonRePatternException("Named capturing group has no group name.");
                }

                builder.Append("(?<").Append(emittedGroupNames[groupName]).Append('>');
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            case PythonReGroupKind.NonCapturing:
                builder.Append("(?:");
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            case PythonReGroupKind.PositiveLookahead:
                builder.Append("(?=");
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            case PythonReGroupKind.NegativeLookahead:
                builder.Append("(?!");
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            case PythonReGroupKind.PositiveLookbehind:
                builder.Append("(?<=");
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            case PythonReGroupKind.NegativeLookbehind:
                builder.Append("(?<!");
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            case PythonReGroupKind.Atomic:
                builder.Append("(?>");
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            case PythonReGroupKind.ScopedFlags:
                builder.Append("(?");
                AppendInlineFlags(native ? node.AddOptions & ~PythonReCompileOptions.IgnoreCase : node.AddOptions, builder);
                var removedFlags = native ? node.RemoveOptions & ~PythonReCompileOptions.IgnoreCase : node.RemoveOptions;
                if (removedFlags != PythonReCompileOptions.None)
                {
                    builder.Append('-');
                    AppendInlineFlags(removedFlags, builder);
                }

                builder.Append(':');
                yield return new Utf8PreparationStep(AppendNode(node.Inner, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                yield break;

            default:
                throw new PythonRePatternException("Unsupported group kind.");
        }
    }

    private static IEnumerable<Utf8PreparationStep> AppendConditional(PythonReConditionalNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython, bool native)
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
        yield return new Utf8PreparationStep(AppendNode(node.YesBranch, builder, emittedGroupNames, managedNumbersByPython, native), false);
        if (node.NoBranch is not null)
        {
            builder.Append('|');
            yield return new Utf8PreparationStep(AppendNode(node.NoBranch, builder, emittedGroupNames, managedNumbersByPython, native), false);
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

    private static IEnumerable<Utf8PreparationStep> AppendInnerAsAtom(PythonReNode node, StringBuilder builder, IReadOnlyDictionary<string, string> emittedGroupNames, int[] managedNumbersByPython, bool native)
    {
        switch (node)
        {
            case PythonReLiteralNode:
            // PLAN.md C06: an escaped astral pair is two UTF-16 units, so it takes the grouped default while every other raw atom stays bare.
            case PythonReRawNode raw when raw.Kind != PythonReRawKind.AstralLiteral:
            case PythonReNamedBackreferenceNode:
            case PythonReNumericBackreferenceNode:
            case PythonReGroupNode:
                yield return new Utf8PreparationStep(AppendNode(node, builder, emittedGroupNames, managedNumbersByPython, native), false);
                break;

            default:
                builder.Append("(?:");
                yield return new Utf8PreparationStep(AppendNode(node, builder, emittedGroupNames, managedNumbersByPython, native), false);
                builder.Append(')');
                break;
        }
    }

    private static IReadOnlyDictionary<string, string> BuildEmittedGroupNames(IReadOnlyDictionary<string, int> namedGroups)
    {
        var map = new Dictionary<string, string>(namedGroups.Count, StringComparer.Ordinal);
        using var names = new Utf8ConstructionBuffer<KeyValuePair<string, int>>(Utf8ConstructionScope.CurrentBudget);
        foreach (var pair in namedGroups) {
            Utf8ConstructionScope.ChargeWork(1);
            names.Add(pair);
        }
        names.Sort(static (left, right) => left.Value.CompareTo(right.Value));
        foreach (var pair in names)
        {
            Utf8ConstructionScope.ChargeWork(1);
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
            Utf8ConstructionScope.ChargeWork(1);
            if (!(char.IsAsciiLetterOrDigit(name[i]) || name[i] == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static void EnsureFixedWidth(PythonReNode node, PythonReParseResult parseResult, Utf8ConstructionMap<PythonReNode, PythonReWidth> widths)
    {
        var width = GetWidth(node, parseResult, widths);
        if (!width.IsFixed)
        {
            throw new PythonRePatternException("look-behind requires fixed-width pattern");
        }
        if (width.Min > uint.MaxValue)
        {
            throw new PythonRePatternException("looks too much behind");
        }
    }

    private static PythonReWidth GetWidth(PythonReNode node, PythonReParseResult parseResult)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var widths = new Utf8ConstructionMap<PythonReNode, PythonReWidth>(construction.Budget, ReferenceEqualityComparer.Instance);
        return GetWidth(node, parseResult, widths);
    }

    private static PythonReWidth GetWidth(PythonReNode node, PythonReParseResult parseResult,
        Utf8ConstructionMap<PythonReNode, PythonReWidth> widths)
    {
        Utf8PreparationStep.TryRun(Measure(node), Utf8ConstructionScope.CurrentBudget);
        return widths[node];

        IEnumerable<Utf8PreparationStep> Measure(PythonReNode current)
        {
            if (widths.ContainsKey(current)) yield break;
            var width = new PythonReWidth(1, 1);
            switch (current)
            {
                case PythonReAlternationNode alternation:
                    var alternateMin = PythonReWidth.Maximum;
                    UInt128 alternateMax = 0;
                    foreach (var branch in alternation.Branches)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        yield return new Utf8PreparationStep(Measure(branch), false);
                        var branchWidth = widths[branch];
                        alternateMin = alternateMin < branchWidth.Min ? alternateMin : branchWidth.Min;
                        alternateMax = UnionMax(alternateMax, branchWidth.Max);
                    }
                    // A saturated positive minimum is not an empty alternation.
                    width = new(alternation.Branches.Count == 0 ? 0 : alternateMin, alternateMax);
                    break;
                case PythonReSequenceNode sequence:
                    UInt128 sequenceMin = 0;
                    UInt128 sequenceMax = 0;
                    foreach (var element in sequence.Elements)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        yield return new Utf8PreparationStep(Measure(element), false);
                        var elementWidth = widths[element];
                        sequenceMin = AddMax(sequenceMin, elementWidth.Min);
                        sequenceMax = AddMax(sequenceMax, elementWidth.Max);
                    }
                    width = new(sequenceMin, sequenceMax);
                    break;
                case PythonReQuantifierNode repeat:
                    yield return new Utf8PreparationStep(Measure(repeat.Inner), false);
                    var repeated = widths[repeat.Inner];
                    var repeatMin = ClampWidth(repeated.Min * (uint)repeat.Min);
                    var repeatMax = repeat.Max.HasValue
                        ? ClampWidth(repeated.Max * (uint)repeat.Max.Value) : PythonReWidth.Maximum;
                    width = new(repeatMin, repeatMax);
                    break;
                case PythonReConditionalNode conditional:
                    yield return new Utf8PreparationStep(Measure(conditional.YesBranch), false);
                    var yes = widths[conditional.YesBranch];
                    if (conditional.NoBranch is PythonReNode no)
                    {
                        yield return new Utf8PreparationStep(Measure(no), false);
                        var otherwise = widths[no];
                        width = new(yes.Min < otherwise.Min ? yes.Min : otherwise.Min, UnionMax(yes.Max, otherwise.Max));
                    }
                    else width = new(0, yes.Max);
                    break;
                case PythonReGroupNode group when group.Kind is PythonReGroupKind.PositiveLookahead
                    or PythonReGroupKind.NegativeLookahead or PythonReGroupKind.PositiveLookbehind or PythonReGroupKind.NegativeLookbehind:
                    width = new(0, 0);
                    break;
                case PythonReGroupNode group:
                    yield return new Utf8PreparationStep(Measure(group.Inner), false);
                    width = widths[group.Inner];
                    break;
                case PythonReNamedBackreferenceNode named:
                    if (parseResult.NamedGroups.TryGetValue(named.Name, out var namedSlot) && parseResult.GroupBodies.TryGetValue(namedSlot, out var namedBody))
                    {
                        yield return new Utf8PreparationStep(Measure(namedBody), false);
                        width = widths[namedBody];
                    }
                    else width = new(0, PythonReWidth.Maximum);
                    break;
                case PythonReNumericBackreferenceNode numeric:
                    if (parseResult.GroupBodies.TryGetValue(numeric.GroupNumber, out var numberedBody))
                    {
                        yield return new Utf8PreparationStep(Measure(numberedBody), false);
                        width = widths[numberedBody];
                    }
                    else width = new(0, PythonReWidth.Maximum);
                    break;
                case PythonReRawNode raw:
                    if (raw.Kind is PythonReRawKind.StartAnchor or PythonReRawKind.EndAnchor ||
                        raw.Kind == PythonReRawKind.Escape && IsZeroWidthEscape(raw.Text)) width = new(0, 0);
                    break;
                case PythonReLiteralNode or PythonReCharacterClassNode:
                    break;
                default:
                    throw new PythonRePatternException("Unsupported AST node.");
            }
            widths[current] = width;
        }
    }

    private static UInt128 ClampWidth(UInt128 value) => value > PythonReWidth.Maximum ? PythonReWidth.Maximum : value;

    private static UInt128 AddMax(UInt128 left, UInt128 right) => ClampWidth(left + right);

    private static UInt128 UnionMax(UInt128 left, UInt128 right) => left > right ? left : right;

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
        return Utf8PreparationStep.TryRun(Check(node));

        static IEnumerable<Utf8PreparationStep> Check(PythonReNode current)
        {
            switch (current)
            {
                case PythonReConditionalNode:
                    yield return Utf8PreparationStep.Decline;
                    break;
                case PythonReAlternationNode alternation:
                    if (!IsPrefixFreeExactLiteralAlternation(alternation)) yield return Utf8PreparationStep.Decline;
                    break;
                case PythonReSequenceNode sequence:
                    foreach (var element in sequence.Elements)
                        {
                        Utf8ConstructionScope.ChargeWork(1);
                        yield return new Utf8PreparationStep(Check(element), false);
                    }
                    break;
                case PythonReQuantifierNode repeat:
                    yield return new Utf8PreparationStep(Check(repeat.Inner), false);
                    break;
                case PythonReGroupNode group:
                    yield return new Utf8PreparationStep(Check(group.Inner), false);
                    break;
            }
        }
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
        using var construction = Utf8ConstructionScope.Enter();
        using var literals = new Utf8ConstructionBuffer<string>(construction.Budget);
        foreach (var branch in alternation.Branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
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
                Utf8ConstructionScope.ChargeWork(1);
                if (i == j)
                {
                    continue;
                }

                Utf8ConstructionScope.ChargeWork((ulong)literals[i].Length);
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
        // Flatten once into the actual literal consumer's text, avoiding
        // recursive intermediate strings for nested sequences/groups.
        var builder = new StringBuilder();
        var complete = Utf8PreparationStep.TryRun(AppendLiteral(node));
        literal = complete ? builder.ToString() : string.Empty;
        return complete;

        IEnumerable<Utf8PreparationStep> AppendLiteral(PythonReNode current)
        {
            switch (current)
            {
                case PythonReLiteralNode value:
                    builder.Append(value.Value);
                    break;
                case PythonReSequenceNode sequence:
                    foreach (var element in sequence.Elements)
                        {
                        Utf8ConstructionScope.ChargeWork(1);
                        yield return new Utf8PreparationStep(AppendLiteral(element), false);
                    }
                    break;
                case PythonReGroupNode { Kind: PythonReGroupKind.NonCapturing } group:
                    yield return new Utf8PreparationStep(AppendLiteral(group.Inner), false);
                    break;
                default:
                    yield return Utf8PreparationStep.Decline;
                    break;
            }
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
            Utf8ConstructionScope.ChargeWork(1);
            switch (item)
            {
                case PythonReCharacterClassLiteralItem literal when IsAsciiWordCharacter(literal.Scalar) || literal.Scalar > 0x7F:
                    break;

                case PythonReCharacterClassRangeItem range
                    when range.StartScalar <= range.EndScalar &&
                         range.StartScalar >= 0:
                    for (var scalar = range.StartScalar; scalar <= Math.Min(range.EndScalar, 0x7F); scalar++)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
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

    private static void AppendCharacterClass(PythonReCharacterClassNode node, StringBuilder builder, bool native)
    {
        var positiveAtom = native ? BuildScalarCharacterClassAtom(node.Items) : BuildPositiveCharacterClassAtom(node.Items);
        if (!node.IsNegated)
        {
            builder.Append(positiveAtom);
            return;
        }

        builder.Append("(?:(?!").Append(positiveAtom).Append(')').Append(native ? NativeScalarPattern : AnyUnicodeScalarPattern).Append(')');
    }

    // Only parser-generated raw/predefined fragments contain this internal atom.
    // User literals and class endpoints are emitted from their AST values.
    private static string RenderScalarFragment(string fragment) =>
        fragment.Replace(AnyUnicodeScalarPattern, NativeScalarPattern, StringComparison.Ordinal);

    private static string BuildScalarCharacterClassAtom(IReadOnlyList<PythonReCharacterClassItem> items)
    {
        var ranges = new List<(int Start, int End)>();
        var fragments = new List<string>();
        var branches = new List<string>();
        foreach (var item in items)
        {
            Utf8ConstructionScope.ChargeWork(1);
            switch (item)
            {
                case PythonReCharacterClassLiteralItem literal:
                    AddScalarRange(ranges, literal.Scalar, literal.Scalar);
                    break;
                case PythonReCharacterClassRangeItem range:
                    AddScalarRange(ranges, range.StartScalar, range.EndScalar);
                    break;
                case PythonReCharacterClassPredefinedItem predefined:
                    if (predefined.IsStandaloneAtom) branches.Add(RenderScalarFragment(predefined.RegexClassFragment));
                    else fragments.Add(predefined.RegexClassFragment);
                    break;
            }
        }
        var rangeClass = BuildRangeCharacterClass(ranges, fragments);
        if (rangeClass.Length != 0) branches.Insert(0, rangeClass);
        return branches.Count switch
        {
            0 => "(?!)",
            1 => branches[0],
            _ => "(?:" + string.Join("|", branches) + ")",
        };
    }

    private static void AddScalarRange(List<(int Start, int End)> ranges, int start, int end)
    {
        if (start <= 0xD7FF) ranges.Add((start, Math.Min(end, 0xD7FF)));
        if (end >= 0xE000) ranges.Add((Math.Max(start, 0xE000), end));
    }

    private static string BuildPositiveCharacterClassAtom(IReadOnlyList<PythonReCharacterClassItem> items)
    {
        var bmpRanges = new List<(int Start, int End)>();
        var bmpFragments = new List<string>();
        var standaloneAtoms = new List<string>();
        var astralBranches = new List<string>();

        foreach (var item in items)
        {
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
        var bmpClass = BuildRangeCharacterClass(bmpRanges, bmpFragments);
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

    private static string BuildRangeCharacterClass(List<(int Start, int End)> ranges, List<string> fragments)
    {
        if (ranges.Count == 0 && fragments.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append('[');
        foreach (var fragment in fragments)
        {
            Utf8ConstructionScope.ChargeWork(1);
            builder.Append(fragment);
        }

        foreach (var (start, end) in MergeRanges(ranges))
        {
            Utf8ConstructionScope.ChargeWork(1);
            AppendClassRange(builder, start, end);
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

        Utf8ConstructionSort.Sort(CollectionsMarshal.AsSpan(ranges), static (x, y) => x.Start.CompareTo(y.Start), Utf8ConstructionScope.CurrentBudget);
        var currentStart = ranges[0].Start;
        var currentEnd = ranges[0].End;
        for (var i = 1; i < ranges.Count; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
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

    private static void AppendClassRange(StringBuilder builder, int start, int end)
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

    private static void AppendClassChar(StringBuilder builder, int scalar)
    {
        if (scalar > 0xFFFF)
        {
            builder.Append(char.ConvertFromUtf32(scalar));
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
            Utf8ConstructionScope.ChargeWork(1);
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
    string NativePattern,
    RegexOptions RegexOptions,
    RegexOptions NativeRegexOptions,
    int CaptureGroupCount,
    IReadOnlyDictionary<string, string> EmittedGroupNames,
    int[] ManagedNumbersByPython);
