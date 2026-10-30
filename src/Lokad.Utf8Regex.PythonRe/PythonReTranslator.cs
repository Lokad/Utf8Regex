using ByteBuilder = Lokad.Utf8Regex.Internal.Utf8ConstructionBuffer<byte>;
using Lokad.Utf8Regex.Internal.FrontEnd;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe;

internal static partial class PythonReTranslator
{
    private const string NativeScalarPattern = PythonReParser.UnicodeScalarPattern;

    public static bool CanMatchEmpty(PythonReNode node, PythonReParseResult parseResult)
    {
        // Backreferences keep CPython getwidth content semantics: a backreference to an unset group fails in both CPython 3.13.2 and .NET 10 (verified: `(a)?b\1` on "b" matches in neither), so in every successful match each executed backreference contributes at least its group's content minimum, while alternation minima and zeroable quantifiers already absorb non-executed positions. The structural minimum is therefore a lower bound over successful matches: zero keeps the general route (always safe) and a positive minimum proves no empty match exists.
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

    public static bool IsExactLiteral(PythonReNode node) => InspectSelection(node, InspectionKind.Literal);

    public static bool TryGetCaseSensitiveExactLiteral(
        PythonReNode node,
        PythonReCompileOptions options,
        out byte[] literal)
    {
        if ((options & PythonReCompileOptions.IgnoreCase) == 0 &&
            TryGetExactLiteral(node, out literal))
        {
            return true;
        }

        literal = [];
        return false;
    }

    public static bool CanUseOptionalExactCaptureFindAll(
        PythonReNode node,
        PythonReCompileOptions options)
    {
        // Only a Boolean proof escapes. All literal/continuation acquisitions
        // are scratch; leave their capacity with neither the AST nor facade.
        using var scratch = Utf8ConstructionScope.Enter();
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
        using var literal = ByteBuilder.Create(construction.Budget);
        for (var index = 0; index < sequence.Elements.Count - 1; index++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!TryGetExactLiteral(sequence.Elements[index], out var segment) ||
                segment.Any(static character => character > 0x7f))
            {
                return false;
            }

            literal.AddRange(segment);
        }

        if (literal.Count == 0)
        {
            return false;
        }

        prefixUtf8 = literal.Take();
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
        using var prefix = ByteBuilder.Create(construction.Budget);
        using var suffix = ByteBuilder.Create(construction.Budget);
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

            (index < wildcardIndex ? prefix : suffix).AddRange(segment);
        }

        if (prefix.Count == 0 || suffix.Count == 0)
        {
            return false;
        }

        prefixUtf8 = prefix.Take();
        suffixUtf8 = suffix.Take();
        construction.Complete();
        return true;
    }

    public static PythonReTranslation Translate(PythonReParseResult parseResult, bool renderNativePattern)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var widths = Utf8ConstructionMap<PythonReNode, PythonReWidth>.Create(construction.Budget, ReferenceEqualityComparer.Instance);
        Inspect(parseResult.Root, InspectionKind.References, parseResult, null, null);
        // Validate scalar lookbehind widths before emitting native pattern text.
        Inspect(parseResult.Root, InspectionKind.Lookbehind, parseResult, widths, null);

        // References and scalar lookbehind widths are validated even when no
        // operation can use the optional Core program. Its text rendering and
        // numbering permutation then have no consumer.
        if (!renderNativePattern)
        {
            construction.Complete();
            return new PythonReTranslation([], ToNativeRegexOptions(parseResult.Options));
        }
        using var arena = Utf8ConstructionArena.Create(construction.Budget);
        using var builder = ByteBuilder.Create(construction.Budget);
        // Core numbers unnamed groups before named groups; Python numbers by
        // opening order. This temporary permutation serves text rendering only.
        // The owned Python scalar program uses Python numbers directly.
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

        AppendTree(parseResult.Root, builder, parseResult.NamedGroups, coreNumbersByPython,
            (parseResult.Options & PythonReCompileOptions.Verbose) != 0);
        var nativePattern = builder.Take();

        var result = new PythonReTranslation(nativePattern, ToNativeRegexOptions(parseResult.Options));
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

    internal static byte[] RenderNativeAtom(PythonReNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var builder = ByteBuilder.Create(construction.Budget);
        AppendTree(node, builder, Utf8ConstructionReadOnlyMap<string, int>.Empty, []);
        var result = builder.Take();
        construction.Complete();
        return result;
    }

    private static void AppendInlineFlags(PythonReCompileOptions options, ByteBuilder builder)
    {
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

    private static RegexOptions ToNativeRegexOptions(PythonReCompileOptions options)
    {
        var translated = RegexOptions.CultureInvariant;
        // The parser expands Python folding explicitly. Core must not apply a
        // second, different case-folding policy to the emitted scalar pattern.
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

    private static bool IsPrefixFreeExactLiteralAlternation(PythonReAlternationNode alternation)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var literals = Utf8ConstructionBuffer<byte[]>.Create(construction.Budget);
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
                if (literals[j].AsSpan().StartsWith(literals[i]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryGetExactLiteral(PythonReNode node, out byte[] literal)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var builder = ByteBuilder.Create(construction.Budget);
        var complete = Inspect(node, InspectionKind.Literal, default, null, builder);
        literal = complete ? builder.Take() : [];
        if (complete) construction.Complete();
        return complete;
    }

    private static void AppendCharacterClass(PythonReCharacterClassNode node, ByteBuilder builder)
    {
        var complete = false;
        for (var index = 0; index < node.Items.Count; index++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (node.Items[index] is PythonReCharacterClassRangeItem { StartScalar: 0, EndScalar: 0x10ffff })
            { complete = true; break; }
        }
        if (node.IsNegated) builder.Append("(?:(?!");
        if (complete) builder.Append(NativeScalarPattern);
        else AppendScalarCharacterClassAtom(node.Items, builder);
        if (node.IsNegated) builder.Append(')').Append(NativeScalarPattern).Append(')');
    }

    private static void AppendScalarCharacterClassAtom(IReadOnlyList<PythonReCharacterClassItem> items, ByteBuilder builder)
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
                    if (predefined.IsStandaloneAtom) branches.Add(predefined.RegexClassFragment);
                    else fragments.Add(predefined.RegexClassFragment);
                    break;
            }
        }
        var hasRanges = ranges.Count != 0 || fragments.Count != 0;
        var count = (hasRanges ? 1 : 0) + branches.Count;
        if (count == 0) { builder.Append("(?!)"); return; }
        if (count > 1) builder.Append("(?:");
        if (hasRanges) AppendRangeCharacterClass(ranges, fragments, builder);
        for (var index = 0; index < branches.Count; index++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (hasRanges || index != 0) builder.Append('|');
            builder.Append(branches[index]);
        }
        if (count > 1) builder.Append(')');
    }

    private static void AddScalarRange(Utf8ConstructionBuffer<(int Start, int End)> ranges, int start, int end)
    {
        if (start <= 0xD7FF) ranges.Add((start, Math.Min(end, 0xD7FF)));
        if (end >= 0xE000) ranges.Add((Math.Max(start, 0xE000), end));
    }

    private static void AppendRangeCharacterClass(Utf8ConstructionBuffer<(int Start, int End)> ranges,
        Utf8ConstructionBuffer<string> fragments, ByteBuilder builder)
    {
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
        builder.Append(']');
    }

    private static void AppendClassRange(ByteBuilder builder, int start, int end)
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

    private static void AppendClassChar(ByteBuilder builder, int scalar)
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


}

internal readonly record struct PythonReTranslation(
    byte[] NativePatternUtf8,
    RegexOptions NativeRegexOptions);
