using Lokad.Utf8Regex.Internal.FrontEnd;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

internal static partial class PythonReTranslator
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

    public static bool CanMatchNonEmpty(PythonReNode node, PythonReParseResult parseResult) =>
        GetWidth(node, parseResult).Max > 0;

    // Native FindAll over UTF-8 is admissible only when alternation order is
    // unobservable: prefix-free exact literals guarantee at most one branch
    // matches at any position, so a literal-family candidate scan cannot diverge
    // from ordered-alternation semantics. Conditionals use the scalar program. The facade
    // additionally requires no empty match. Pinned by
    // FindAllCanUseUtf8RegexBackendForPrefixFreeLiteralAlternation.
    public static bool CanUseUtf8IterationFastPath(PythonReNode node) => IsUtf8IterationSafe(node);

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

    public static bool TryGetOptionalExactCaptureFindAllPlan(
        PythonReNode node,
        PythonReCompileOptions options,
        out string captureValue)
    {
        captureValue = string.Empty;
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

        return true;
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

        using var construction = Utf8ConstructionScope.Enter();
        using var literal = Utf8ConstructionText.Create();
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

        prefixUtf8 = literal.ToAscii();
        construction.Complete();
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

        using var construction = Utf8ConstructionScope.Enter();
        using var prefix = Utf8ConstructionText.Create();
        using var suffix = Utf8ConstructionText.Create();
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

        prefixUtf8 = prefix.ToAscii();
        suffixUtf8 = suffix.ToAscii();
        construction.Complete();
        return true;
    }

    public static PythonReTranslation Translate(PythonReParseResult parseResult)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var widths = Utf8ConstructionMap<PythonReNode, PythonReWidth>.Create(construction.Budget, ReferenceEqualityComparer.Instance);
        Inspect(parseResult.Root, InspectionKind.References, parseResult, null, null);
        // Validate lookbehind content widths before either spelling is emitted.
        Inspect(parseResult.Root, InspectionKind.Lookbehind, parseResult, widths, null);

        using var arena = Utf8ConstructionArena.Create(construction.Budget);
        var emittedGroupNames = BuildEmittedGroupNames(parseResult.NamedGroups, arena);
        using var builder = Utf8ConstructionText.Create();
        // PLAN.md C07: .NET numbers unnamed groups first (opening-paren order), then named groups (opening-paren order), while Python numbers strictly by opening paren. Precompute the Python-slot to core-slot permutation for the owned scalar program.
        var coreNumbersByPython = arena.Allocate<int>(checked(parseResult.CaptureGroupCount + 1), true);
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

        AppendTree(parseResult.Root, builder, emittedGroupNames, coreNumbersByPython, false);
        var translatedPattern = builder.ToText();
        builder.Clear();
        AppendTree(parseResult.Root, builder, emittedGroupNames, coreNumbersByPython, true);
        var nativePattern = builder.ToText();
        if ((parseResult.Options & PythonReCompileOptions.Verbose) == 0)
        {
            // Regex.Escape protects spaces for IgnorePatternWhitespace. Outside
            // verbose mode a raw space is equivalent and keeps exact literals
            // visible to the UTF-8 planner instead of forcing fallback.
            translatedPattern = Utf8ConstructionText.Replace(translatedPattern, @"\ ", " ");
            nativePattern = Utf8ConstructionText.Replace(nativePattern, @"\ ", " ");
        }

        var result = new PythonReTranslation(translatedPattern, nativePattern, ToRegexOptions(parseResult.Options),
            ToRegexOptions(parseResult.Options) & ~RegexOptions.IgnoreCase, parseResult.CaptureGroupCount,
            emittedGroupNames, coreNumbersByPython);
        arena.Complete();
        construction.Complete();
        return result;
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
        using var construction = Utf8ConstructionScope.Enter();
        using var builder = Utf8ConstructionText.Create();
        AppendTree(node, builder, Utf8ConstructionReadOnlyMap<string, string>.Empty, [], true);
        var result = builder.ToText();
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

    private static IReadOnlyDictionary<string, string> BuildEmittedGroupNames(IReadOnlyDictionary<string, int> namedGroups, Utf8ConstructionArena arena)
    {
        using var map = Utf8ConstructionMap<string, string>.Create(arena.Budget, StringComparer.Ordinal);
        using var names = Utf8ConstructionBuffer<KeyValuePair<string, int>>.Create(arena.Budget);
        foreach (var pair in namedGroups) {
            Utf8ConstructionScope.ChargeWork(1);
            names.Add(pair);
        }
        names.Sort(static (left, right) => left.Value.CompareTo(right.Value));
        for (var index = 0; index < names.Count; index++)
        {
            var pair = names[index];
            Utf8ConstructionScope.ChargeWork(1);
            if (IsDotNetSafeGroupName(pair.Key)) map[pair.Key] = pair.Key;
            else
            {
                using var name = Utf8ConstructionText.Create();
                map[pair.Key] = name.Append('g').Append(pair.Value).ToText();
            }
        }

        return map.Freeze(arena);
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

    private static uint EnsureFixedWidth(PythonReNode node, PythonReParseResult parseResult, Utf8ConstructionMap<PythonReNode, PythonReWidth> widths)
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
        return (uint)width.Min;
    }

    internal static PythonReWidth GetWidth(PythonReNode node, PythonReParseResult parseResult)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var widths = Utf8ConstructionMap<PythonReNode, PythonReWidth>.Create(construction.Budget, ReferenceEqualityComparer.Instance);
        return GetWidth(node, parseResult, widths);
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

    private static bool IsUtf8IterationSafe(PythonReNode node) => InspectSelection(node, InspectionKind.Iteration);

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
        using var literals = Utf8ConstructionBuffer<string>.Create(construction.Budget);
        for (var branchIndex = 0; branchIndex < alternation.Branches.Count; branchIndex++)
        {
            var branch = alternation.Branches[branchIndex];
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
        using var construction = Utf8ConstructionScope.Enter();
        using var builder = Utf8ConstructionText.Create();
        var complete = Inspect(node, InspectionKind.Literal, default, null, builder);
        literal = complete ? builder.ToText() : string.Empty;
        if (complete) construction.Complete();
        return complete;
    }

    private static void AppendCharacterClass(PythonReCharacterClassNode node, Utf8ConstructionText builder, bool native)
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
        Utf8ConstructionText.Replace(fragment, AnyUnicodeScalarPattern, NativeScalarPattern);

    private static string BuildScalarCharacterClassAtom(IReadOnlyList<PythonReCharacterClassItem> items)
    {
        using var ranges = Utf8ConstructionBuffer<(int Start, int End)>.Create(Utf8ConstructionScope.CurrentBudget);
        using var fragments = Utf8ConstructionBuffer<string>.Create(Utf8ConstructionScope.CurrentBudget);
        using var branches = Utf8ConstructionBuffer<string>.Create(Utf8ConstructionScope.CurrentBudget);
        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            var item = items[itemIndex];
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
        return JoinClassBranches(rangeClass, branches.Span, []);
    }

    private static void AddScalarRange(Utf8ConstructionBuffer<(int Start, int End)> ranges, int start, int end)
    {
        if (start <= 0xD7FF) ranges.Add((start, Math.Min(end, 0xD7FF)));
        if (end >= 0xE000) ranges.Add((Math.Max(start, 0xE000), end));
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
                        standaloneAtoms.Add(predefined.RegexClassFragment);
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
        for (var high = startHigh; high <= endHigh; high++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var lowStart = high == startHigh ? (char)(0xdc00 + ((startScalar - 0x10000) & 1023)) : '\uDC00';
            var lowEnd = high == endHigh ? (char)(0xdc00 + ((endScalar - 0x10000) & 1023)) : '\uDFFF';
            branches.Add(BuildSurrogateBranch((char)high, lowStart, lowEnd));
        }
    }

    private static string BuildSurrogateBranch(char high, char lowStart, char lowEnd)
    {
        using var builder = Utf8ConstructionText.Create();
        builder.Append(@"(?:\u").AppendHex(high);
        if (lowStart == lowEnd) builder.Append(@"\u").AppendHex(lowStart);
        else builder.Append(@"[\u").AppendHex(lowStart).Append(@"-\u").AppendHex(lowEnd).Append(']');
        return builder.Append(')').ToText();
    }
}

internal readonly record struct PythonReTranslation(
    string Pattern,
    string NativePattern,
    RegexOptions RegexOptions,
    RegexOptions NativeRegexOptions,
    int CaptureGroupCount,
    IReadOnlyDictionary<string, string> EmittedGroupNames,
    int[] CoreNumbersByPython);
